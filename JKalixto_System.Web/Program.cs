using System.IO;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using JKalixto_System.Application.Services;
using JKalixto_System.Infrastructure.Data;
using JKalixto_System.Infrastructure.Repositories;
using JKalixto_System.Web.Components;
using JKalixto_System.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Corre como consola (dotnet run, como hasta ahora) o como servicio de Windows,
// según cómo se lo inicie — ver JKalixto_System.Web/deploy/instalar-servicio.ps1.
// No cambia nada cuando se corre por consola; permite que el hotel lo deje
// arrancando solo con la PC, sin depender de dejar una terminal abierta.
builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "JKalixto Web";
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ------------------------------------------------------------------
// DATA PROTECTION — clave de cifrado para la cookie de sesión (ver login más
// abajo). Por defecto, ASP.NET Core guarda esa clave en el perfil del usuario
// de Windows que corre el proceso (%LOCALAPPDATA%). Corriendo como Servicio de
// Windows, el proceso corre como LocalSystem, no como el usuario de escritorio
// — esa carpeta de perfil no es confiable ahí, y en la práctica la app no podía
// ni cifrar ni descifrar la cookie: SignInAsync fallaba con una excepción, el
// login se veía "rechazado" sin ningún mensaje de error real. Se fija la
// carpeta explícitamente dentro de la propia carpeta de la app (accesible para
// cualquier cuenta que la ejecute) para que sea confiable sin importar cómo se
// inicie (consola o servicio).
builder.Services.AddDataProtection()
    .SetApplicationName("JKalixtoWeb")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "Data", "dpkeys")));

// ------------------------------------------------------------------
// LOGIN — cookie de ASP.NET Core, reutilizando IAuthService.IniciarSesionAsync
// (el mismo que ya usa la app de escritorio). A propósito NO se fuerza
// CookieSecurePolicy.Always: el servidor sigue sirviendo HTTP plano dentro de
// la LAN del hotel (sin certificado) — sería el mismo motivo que ya llevó a no
// usar UseHttpsRedirection más abajo. Forzar "Always" haría que el navegador
// nunca mande la cookie y el login quedaría roto sin excepción visible.
// ------------------------------------------------------------------
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "JKalixtoAuth";
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
    });
// FallbackPolicy en vez de un simple AddAuthorization(): sin esto,
// AuthorizeRouteView (Routes.razor) NO exige sesión en ninguna página a menos
// que cada una tenga su propio [Authorize] — habría que acordarse de
// decorarlas una por una, y bastaría con olvidarse de UNA para dejarla
// abierta sin querer. Con el fallback, TODA página exige sesión por defecto,
// y Login.razor es la única excepción explícita vía [AllowAnonymous].
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddCascadingAuthenticationState();

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

// Resuelve el Usuario real de la sesión a partir de la cookie de login (ver
// SesionWebService.cs). Scoped porque, igual que ISessionService acá arriba,
// tiene que ser "una sesión de negocio por circuito/pestaña".
builder.Services.AddScoped<SesionWebService>();

// Respaldo periódico de la base de datos — ver RespaldoBaseDeDatosService.cs.
builder.Services.AddHostedService<RespaldoBaseDeDatosService>();

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

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// AllowAnonymous a propósito: el FallbackPolicy de arriba, si no, también
// bloquearía el CSS/JS — y sin CSS la propia pantalla de /login se ve sin
// estilos (un archivo de hojas de estilo no es información sensible).
app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// ------------------------------------------------------------------
// LOGIN / LOGOUT — endpoints mínimos, NO páginas Blazor: necesitan llamar
// HttpContext.SignInAsync/SignOutAsync, que solo funciona si la respuesta
// HTTP todavía no se empezó a enviar. Un componente Blazor interactivo ya
// renderizado (sobre SignalR) no puede hacer eso — por eso Login.razor manda
// un <form> HTML normal (no un EditForm de Blazor) que termina acá.
//
// A propósito NO se llaman "/login" ni "/logout" — esas rutas ya las usa la
// propia página Blazor (@page "/login"), y el manejo de formularios de
// Blazor (enhanced form handling) también reclama esa misma ruta para un
// POST, chocando con este endpoint (AmbiguousMatchException en tiempo de
// ejecución). "/account/..." las deja sin ambigüedad.
// ------------------------------------------------------------------
app.MapPost("/account/login", async (HttpContext http, IAuthService authService) =>
{
    var form = await http.Request.ReadFormAsync();
    var username = form["username"].ToString();
    var password = form["password"].ToString();

    var resultado = await authService.IniciarSesionAsync(username, password);
    if (!resultado.Exito || resultado.Usuario is null)
    {
        return Results.Redirect("/login?error=1");
    }

    var usuario = resultado.Usuario;
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
        new(ClaimTypes.Name, usuario.Username),
        new(ClaimTypes.Role, usuario.Rol.ToString()),
        new("NombreCompleto", usuario.NombreCompleto),
    };
    var identidad = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

    await http.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identidad),
        new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12) });

    var returnUrl = form["returnUrl"].ToString();
    var destino = !string.IsNullOrWhiteSpace(returnUrl) && returnUrl.StartsWith('/') ? returnUrl : "/recepcion";
    return Results.Redirect(destino);
}).AllowAnonymous(); // si no, el FallbackPolicy de arriba bloquearía el propio POST de login

app.MapPost("/account/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
}).AllowAnonymous();

app.Run();

/// <summary>
/// A propósito NO es igual a MauiProgram.InicializarBaseDeDatos: esa versión borra
/// el archivo .db entero cada vez que arranca en Debug, porque cada instalación de
/// escritorio tiene su propia base aislada y descartable. ACÁ la base es
/// compartida — el servidor se puede reiniciar sin perder lo que ya cargaron el
/// hotel/sauna. Por eso: Migrate() crea o actualiza el esquema sin borrar nada, y
/// los datos de prueba se siembran SOLO la primera vez (cuando el archivo no
/// existía todavía), nunca en un reinicio posterior.
/// </summary>
static void InicializarBaseDeDatos(IServiceProvider servicios, string dbPath)
{
    var esNueva = !File.Exists(dbPath);

    using var scope = servicios.CreateScope();
    var contexto = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (!esNueva)
    {
        MarcarMigracionInicialSiHaceFalta(contexto);
    }

    contexto.Database.Migrate();

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

/// <summary>
/// Este proyecto usó Database.EnsureCreated() hasta que se adoptó EF Core
/// Migrations (ver JKalixto_System.Infrastructure/Data/Migrations). Una base ya
/// creada con EnsureCreated tiene el esquema completo de "InitialCreate" pero NO
/// tiene la tabla de historial de migraciones — sin esto, Migrate() intentaría
/// crear de nuevo tablas que ya existen y fallaría. Se marca esa migración como
/// "ya aplicada" a mano (documentado por EF Core para este escenario exacto), sin
/// tocar ninguna tabla de datos. Si la base ya tiene historial (porque ya se migró
/// normalmente antes), esto no hace nada.
/// </summary>
static void MarcarMigracionInicialSiHaceFalta(AppDbContext contexto)
{
    const string migracionInicial = "20260916222913_InitialCreate";

    var tieneHistorial = contexto.Database
        .SqlQueryRaw<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory'")
        .AsEnumerable()
        .First() > 0;

    if (tieneHistorial)
    {
        return;
    }

    contexto.Database.ExecuteSqlRaw(
        """
        CREATE TABLE "__EFMigrationsHistory" (
            "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
            "ProductVersion" TEXT NOT NULL
        )
        """);
    contexto.Database.ExecuteSqlRaw(
        $"""
        INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
        VALUES ('{migracionInicial}', '10.0.10')
        """);
}
