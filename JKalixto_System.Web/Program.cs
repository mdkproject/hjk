using System.IO;
using Microsoft.EntityFrameworkCore;
using JKalixto_System.Application.Services;
using JKalixto_System.Infrastructure.Data;
using JKalixto_System.Infrastructure.Repositories;
using JKalixto_System.Web.Components;
using JKalixto_System.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ------------------------------------------------------------------
// BASE DE DATOS — mismo archivo SQLite que ya usa la app de escritorio (MAUI),
// pero ahora accedido por UN SOLO proceso: este servidor. Eso es justo lo que
// resuelve, de raíz, el riesgo de compartir un .db por red entre varias
// instalaciones de escritorio — acá los navegadores de la laptop y la PC son
// solo clientes livianos, no procesos que abren el archivo cada uno por su
// cuenta.
//
// Ruta configurable en appsettings.json ("RutaBaseDeDatos") en vez de
// FileSystem.AppDataDirectory (eso es API de MAUI, no existe en un servidor).
// Por defecto apunta a una carpeta "Data" al lado del ejecutable, para que
// levantar el servidor no dependa de una ruta absoluta hardcodeada.
// ------------------------------------------------------------------
var rutaConfigurada = builder.Configuration["RutaBaseDeDatos"];
var dbPath = string.IsNullOrWhiteSpace(rutaConfigurada)
    ? Path.Combine(builder.Environment.ContentRootPath, "Data", "jkalixto.db")
    : rutaConfigurada;
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

// Scoped (no Transient como en MAUI): en Blazor Server cada pestaña/circuito de
// usuario ya tiene su propio "scope" de DI manejado por el framework — Scoped es
// el equivalente correcto acá a "una instancia por request" en ASP.NET Core.
builder.Services.AddDbContext<AppDbContext>(
    options => options
        .UseSqlite($"Data Source={dbPath};Default Timeout=5")
        .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking),
    ServiceLifetime.Scoped);

// --- Repositorios ---
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();

// --- Servicios de aplicación (mismos que MauiProgram.cs, Scoped en vez de
// Transient por la misma razón que el DbContext de arriba) ---
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAuditoriaService, AuditoriaService>();
builder.Services.AddScoped<IHabitacionService, HabitacionService>();
builder.Services.AddScoped<IReservaService, ReservaService>();
builder.Services.AddScoped<IClientesService, ClientesService>();
builder.Services.AddScoped<IGastosService, GastosService>();
builder.Services.AddScoped<ICalendarioService, CalendarioService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ISaunaService, SaunaService>();
builder.Services.AddScoped<ICierreCajaService, CierreCajaService>();
builder.Services.AddScoped<IInventarioService, InventarioService>();
builder.Services.AddScoped<IComprobanteNumeracionService, ComprobanteNumeracionService>();
builder.Services.AddScoped<IRegistroHuespedesService, RegistroHuespedesService>();
builder.Services.AddScoped<IReclamosService, ReclamosService>();

// ISessionService sigue siendo Singleton en MAUI porque solo hay UN usuario por
// instalación de escritorio. Acá NO puede ser Singleton — varias personas están
// conectadas al mismo servidor a la vez, cada una en su propio circuito/pestaña,
// así que tiene que ser Scoped (una "sesión" de negocio por circuito).
builder.Services.AddScoped<ISessionService, SessionService>();

// Notificador de cambios en vivo: SÍ es Singleton a propósito — es el único punto
// compartido entre TODOS los circuitos/usuarios conectados. Un servicio (ej.
// HabitacionService) avisa acá cuando algo cambió, y cada página Blazor
// suscrita (de cualquier pestaña/máquina) se entera y se refresca sola. Ver
// Services/INotificadorCambios.cs.
builder.Services.AddSingleton<INotificadorCambios, NotificadorCambios>();

// Ver el comentario en SesionWebService.cs — parche temporal hasta que haya
// login real en la web. Scoped porque, igual que ISessionService acá arriba,
// tiene que ser "una sesión de negocio por circuito/pestaña".
builder.Services.AddScoped<SesionWebService>();

var app = builder.Build();

InicializarBaseDeDatos(app.Services, dbPath);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Sin UseHttpsRedirection a propósito: este servidor corre en la red local del
// hotel (LAN), sin certificado propio — forzar HTTPS acá solo generaría
// advertencias de "sitio no seguro" en el navegador de cada terminal sin
// aportar nada, porque el tráfico ya está dentro de la red del negocio.
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

/// <summary>
/// A propósito NO es igual a MauiProgram.InicializarBaseDeDatos: esa versión borra
/// el archivo .db entero cada vez que arranca en Debug, porque cada instalación de
/// escritorio tiene su propia base aislada y descartable. ACÁ la base es
/// compartida — el servidor se puede reiniciar sin perder lo que ya cargaron el
/// hotel/sauna. Por eso: EnsureCreated crea el esquema solo si no existe, y los
/// datos de prueba se siembran SOLO la primera vez (cuando el archivo no existía
/// todavía), nunca en un reinicio posterior.
/// </summary>
static void InicializarBaseDeDatos(IServiceProvider servicios, string dbPath)
{
    var esNueva = !File.Exists(dbPath);

    using var scope = servicios.CreateScope();
    var contexto = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    contexto.Database.EnsureCreated();

#if DEBUG
    if (esNueva)
    {
        var usuarioParaSeed = contexto.Usuarios.FirstOrDefault(u => u.Activo);
        if (usuarioParaSeed is not null)
        {
            DatosPruebaSeeder.Sembrar(contexto, usuarioParaSeed.Id);
        }
    }
#endif
}
