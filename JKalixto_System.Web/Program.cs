using System.IO;
using System.Security.Claims;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using JKalixto_System.Application.Services;
using JKalixto_System.Domain.Models;
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

        // "Cerrar sesión en todos los dispositivos" sin llevar una lista de
        // sesiones activas: la cookie guarda el SecurityStamp vigente al momento
        // del login; acá, en CADA request autenticado, se lo compara contra el
        // valor actual en la base. Si no coincide (la contraseña cambió después
        // de que se emitió esta cookie, en este mismo navegador o en cualquier
        // otro), se rechaza la sesión y se manda a /login -- sin esperar a que
        // expire sola en 12 horas.
        options.Events.OnValidatePrincipal = async context =>
        {
            var usuarioIdTexto = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var stampDeLaCookie = context.Principal?.FindFirst("SecurityStamp")?.Value;
            if (!int.TryParse(usuarioIdTexto, out var usuarioId))
            {
                context.RejectPrincipal();
                return;
            }

            using var scope = context.HttpContext.RequestServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUsuarioRepository>();
            var usuario = await repo.ObtenerPorIdAsync(usuarioId);

            if (usuario is null || !usuario.Activo || usuario.SecurityStamp != stampDeLaCookie)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
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
builder.Services.AddScoped<IInformeMensualService, InformeMensualService>();
builder.Services.AddScoped<IUsuarioAdminService, UsuarioAdminService>();

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

// Panel de Salud del Sistema (/salud) — ver SaludSistemaService.cs.
builder.Services.AddScoped<SaludSistemaService>();

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
        return Results.Redirect($"/login?error=1&mensaje={Uri.EscapeDataString(resultado.Mensaje)}");
    }

    var usuario = resultado.Usuario;
    await FirmarSesionAsync(http, usuario);

    // Cuenta sembrada con contraseña conocida (ej. "1234"): no la dejamos
    // seguir de largo hasta que elija una propia.
    if (usuario.DebeCambiarPassword)
    {
        return Results.Redirect("/cambiar-password?obligatorio=1");
    }

    var returnUrl = form["returnUrl"].ToString();
    var destino = !string.IsNullOrWhiteSpace(returnUrl) && returnUrl.StartsWith('/') ? returnUrl : "/recepcion";
    return Results.Redirect(destino);
}).AllowAnonymous(); // si no, el FallbackPolicy de arriba bloquearía el propio POST de login

app.MapPost("/account/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
}).AllowAnonymous();

// ------------------------------------------------------------------
// CAMBIAR CONTRASEÑA — mismo motivo que /account/login para ser un endpoint
// mínimo y no una página Blazor: hay que volver a firmar la cookie (con el
// SecurityStamp nuevo) para que ESTA misma sesión no quede deslogueada por su
// propio cambio de contraseña, y eso solo se puede hacer antes de que la
// respuesta HTTP empiece a enviarse.
// ------------------------------------------------------------------
app.MapPost("/account/cambiar-password", async (HttpContext http, IUsuarioRepository usuarioRepo) =>
{
    if (http.User.Identity?.IsAuthenticated != true)
    {
        return Results.Redirect("/login");
    }

    var usuarioId = int.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    var usuario = await usuarioRepo.ObtenerPorIdAsync(usuarioId);
    if (usuario is null)
    {
        return Results.Redirect("/login");
    }

    var form = await http.Request.ReadFormAsync();
    var actual = form["actual"].ToString();
    var nueva = form["nueva"].ToString();
    var confirmacion = form["confirmacion"].ToString();

    string? error = null;
    if (!usuario.DebeCambiarPassword && !BCrypt.Net.BCrypt.Verify(actual, usuario.PasswordHash))
    {
        error = "La contraseña actual no es correcta.";
    }
    else if (nueva.Length < 6)
    {
        error = "La contraseña nueva debe tener al menos 6 caracteres.";
    }
    else if (nueva != confirmacion)
    {
        error = "La confirmación no coincide con la contraseña nueva.";
    }
    else if (BCrypt.Net.BCrypt.Verify(nueva, usuario.PasswordHash))
    {
        error = "La contraseña nueva tiene que ser distinta de la actual.";
    }

    if (error is not null)
    {
        var obligatorioTexto = usuario.DebeCambiarPassword ? "&obligatorio=1" : "";
        return Results.Redirect($"/cambiar-password?error={Uri.EscapeDataString(error)}{obligatorioTexto}");
    }

    usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(nueva);
    usuario.DebeCambiarPassword = false;
    usuario.SecurityStamp = Guid.NewGuid().ToString("N");
    await usuarioRepo.ActualizarAsync(usuario);

    // Re-firma esta misma sesión con el stamp nuevo -- si no, OnValidatePrincipal
    // (arriba) la rechazaría en el próximo request, como corresponde a cualquier
    // OTRA sesión abierta en otro dispositivo con la contraseña vieja.
    await FirmarSesionAsync(http, usuario);

    return Results.Redirect("/recepcion?password-cambiada=1");
});

// ------------------------------------------------------------------
// EXPORTAR REPORTES A EXCEL — endpoint mínimo, no una página Blazor, porque
// una descarga de archivo es una respuesta HTTP normal (Results.File), no
// algo que un componente interactivo sobre SignalR pueda devolver. Requiere
// sesión (el FallbackPolicy de arriba ya lo exige) y, dentro de esa sesión,
// repite el mismo chequeo de rol que ya hace Dashboard.razor para decidir
// qué ve un Recepcionista: la app nunca manda montos de dinero en el Excel
// a quien no los vería tampoco en la pantalla.
// ------------------------------------------------------------------
app.MapGet("/reportes/exportar-excel", async (HttpContext http, IDashboardService dashboardService) =>
{
    var rol = http.User.FindFirst(ClaimTypes.Role)?.Value;
    var puedeVerFinanzas = rol is "Gerencia" or "Desarrollador";

    var resumen = await dashboardService.ObtenerResumenAsync();
    string Monto(decimal valor) => puedeVerFinanzas ? valor.ToString("C") : "***";

    using var libro = new XLWorkbook();
    var hoja = libro.Worksheets.Add("Reporte");

    hoja.Cell(1, 1).Value = "Reporte gerencial — " + DateTime.Now.ToString("dd/MM/yyyy HH:mm");
    hoja.Cell(1, 1).Style.Font.Bold = true;
    hoja.Cell(1, 1).Style.Font.FontSize = 14;

    var fila = 3;
    void Kpi(string etiqueta, string valor)
    {
        hoja.Cell(fila, 1).Value = etiqueta;
        hoja.Cell(fila, 2).Value = valor;
        fila++;
    }

    Kpi("Total Hoy", Monto(resumen.IngresosTotalHoy));
    Kpi("Ingresos Hotel", Monto(resumen.IngresosHotelHoy));
    Kpi("Ingresos Sauna", Monto(resumen.IngresosSaunaHoy));
    Kpi("Ocupación", $"{resumen.TasaOcupacion:0.0}%");
    Kpi("Tarifa Promedio (ADR)", Monto(resumen.TarifaPromedioDiaria));
    Kpi("RevPAR", Monto(resumen.IngresoPorHabitacionDisponible));
    Kpi("Clientes Sauna Hoy", resumen.ClientesSaunaHoy.ToString());

    fila++;
    hoja.Cell(fila, 1).Value = "Estado de habitaciones";
    hoja.Cell(fila, 1).Style.Font.Bold = true;
    fila++;
    foreach (var estado in resumen.ResumenEstados)
    {
        hoja.Cell(fila, 1).Value = estado.Etiqueta;
        hoja.Cell(fila, 2).Value = estado.Cantidad;
        fila++;
    }

    fila++;
    hoja.Cell(fila, 1).Value = "Últimos eventos";
    hoja.Cell(fila, 1).Style.Font.Bold = true;
    fila++;
    hoja.Cell(fila, 1).Value = "Fecha/Hora";
    hoja.Cell(fila, 2).Value = "Descripción";
    hoja.Range(fila, 1, fila, 2).Style.Font.Bold = true;
    fila++;
    foreach (var log in resumen.UltimosEventos)
    {
        hoja.Cell(fila, 1).Value = log.Timestamp.ToString("dd/MM/yyyy HH:mm");
        hoja.Cell(fila, 2).Value = log.Descripcion;
        fila++;
    }

    hoja.Columns(1, 2).AdjustToContents();

    using var stream = new MemoryStream();
    libro.SaveAs(stream);

    return Results.File(
        stream.ToArray(),
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        $"reporte-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
});

// ------------------------------------------------------------------
// EXPORTAR INFORME MENSUAL A EXCEL — mismo mecanismo que el de arriba, pero
// para el mes elegido en /reportes/mensual (Ingreso/Egreso/Saldo, desglose
// por método y por categoría, y el libro diario completo del mes).
// ------------------------------------------------------------------
app.MapGet("/reportes/mensual/exportar-excel", async (HttpContext http, IInformeMensualService informeService, int anio, int mes) =>
{
    var rol = http.User.FindFirst(ClaimTypes.Role)?.Value;
    var puedeVerFinanzas = rol is "Gerencia" or "Desarrollador";
    string Monto(decimal valor) => puedeVerFinanzas ? valor.ToString("C") : "***";

    var informe = await informeService.ObtenerInformeMensualAsync(anio, mes);

    using var libro = new XLWorkbook();
    var hoja = libro.Worksheets.Add("Informe " + informe.NombreMes);

    hoja.Cell(1, 1).Value = "Informe Mensual — " + informe.NombreMes;
    hoja.Cell(1, 1).Style.Font.Bold = true;
    hoja.Cell(1, 1).Style.Font.FontSize = 14;

    var fila = 3;
    void Kpi(string etiqueta, string valor)
    {
        hoja.Cell(fila, 1).Value = etiqueta;
        hoja.Cell(fila, 2).Value = valor;
        fila++;
    }

    Kpi("Ingreso del mes", Monto(informe.IngresoTotal));
    Kpi("Egreso del mes", Monto(informe.EgresoTotal));
    Kpi("Saldo del mes", Monto(informe.Saldo));
    Kpi("Saldo anterior (acumulado)", Monto(informe.SaldoAnterior));

    void Seccion(string titulo, IEnumerable<(string etiqueta, decimal monto)> filas)
    {
        fila++;
        hoja.Cell(fila, 1).Value = titulo;
        hoja.Cell(fila, 1).Style.Font.Bold = true;
        fila++;
        foreach (var (etiqueta, monto) in filas)
        {
            hoja.Cell(fila, 1).Value = etiqueta;
            hoja.Cell(fila, 2).Value = Monto(monto);
            fila++;
        }
    }

    Seccion("Ingresos por método", informe.IngresosPorMetodo.Select(m => (m.Etiqueta, m.Monto)));
    Seccion("Ingresos por categoría", informe.IngresosPorCategoria.Select(c => (c.Etiqueta, c.Monto)));
    Seccion("Egresos por método", informe.EgresosPorMetodo.Select(m => (m.Etiqueta, m.Monto)));
    Seccion("Egresos por categoría", informe.EgresosPorCategoria.Select(c => (c.Etiqueta, c.Monto)));

    fila++;
    hoja.Cell(fila, 1).Value = "Libro diario";
    hoja.Cell(fila, 1).Style.Font.Bold = true;
    fila++;
    hoja.Cell(fila, 1).Value = "Fecha";
    hoja.Cell(fila, 2).Value = "Concepto";
    hoja.Cell(fila, 3).Value = "Ingreso";
    hoja.Cell(fila, 4).Value = "Salida";
    hoja.Cell(fila, 5).Value = "Medio";
    hoja.Cell(fila, 6).Value = "Responsable";
    hoja.Range(fila, 1, fila, 6).Style.Font.Bold = true;
    fila++;
    foreach (var m in informe.LibroDiario)
    {
        hoja.Cell(fila, 1).Value = m.Fecha.ToString("dd/MM/yyyy HH:mm");
        hoja.Cell(fila, 2).Value = m.Concepto;
        hoja.Cell(fila, 3).Value = m.Ingreso is { } ing ? Monto(ing) : "";
        hoja.Cell(fila, 4).Value = m.Salida is { } sal ? Monto(sal) : "";
        hoja.Cell(fila, 5).Value = m.Medio;
        hoja.Cell(fila, 6).Value = m.Responsable;
        fila++;
    }

    hoja.Columns(1, 6).AdjustToContents();

    using var stream = new MemoryStream();
    libro.SaveAs(stream);

    return Results.File(
        stream.ToArray(),
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        $"informe-mensual-{anio:0000}-{mes:00}.xlsx");
});

// ------------------------------------------------------------------
// EXPORTAR GASTOS (caja chica del día) A EXCEL — mismo mecanismo que los dos
// de arriba. A diferencia del Dashboard/Informe Mensual, Gastos.razor no
// esconde montos a ningún rol (ver esa página), así que este export tampoco
// enmascara nada — coincide con lo que cualquiera que entra a /gastos ya ve.
// ------------------------------------------------------------------
app.MapGet("/gastos/exportar-excel", async (HttpContext http, IGastosService gastosService) =>
{
    var hoy = DateTime.Now;
    var movimientos = await gastosService.ObtenerDelDiaAsync(hoy);
    var resumen = await gastosService.ObtenerResumenCajaChicaAsync(hoy);

    using var libro = new XLWorkbook();
    var hoja = libro.Worksheets.Add("Gastos");

    hoja.Cell(1, 1).Value = "Movimientos de caja chica — " + hoy.ToString("dd/MM/yyyy");
    hoja.Cell(1, 1).Style.Font.Bold = true;
    hoja.Cell(1, 1).Style.Font.FontSize = 14;

    hoja.Cell(3, 1).Value = "Caja Chica Hotel (base " + resumen.MontoBaseHotel.ToString("C") + ")";
    hoja.Cell(3, 2).Value = resumen.MontoEsperadoHotel.ToString("C");
    hoja.Cell(4, 1).Value = "Caja Chica Sauna (base " + resumen.MontoBaseSauna.ToString("C") + ")";
    hoja.Cell(4, 2).Value = resumen.MontoEsperadoSauna.ToString("C");

    var fila = 6;
    hoja.Cell(fila, 1).Value = "Hora";
    hoja.Cell(fila, 2).Value = "Origen";
    hoja.Cell(fila, 3).Value = "Categoría";
    hoja.Cell(fila, 4).Value = "Descripción";
    hoja.Cell(fila, 5).Value = "Personal relacionado";
    hoja.Cell(fila, 6).Value = "Cajero";
    hoja.Cell(fila, 7).Value = "Método";
    hoja.Cell(fila, 8).Value = "Monto";
    hoja.Range(fila, 1, fila, 8).Style.Font.Bold = true;
    fila++;

    foreach (var m in movimientos)
    {
        hoja.Cell(fila, 1).Value = m.FechaHora.ToString("HH:mm");
        hoja.Cell(fila, 2).Value = m.OrigenCaja.ToString();
        hoja.Cell(fila, 3).Value = m.Categoria.ToString();
        hoja.Cell(fila, 4).Value = m.Descripcion;
        hoja.Cell(fila, 5).Value = m.PersonalRelacionado ?? "";
        hoja.Cell(fila, 6).Value = m.UsuarioNombre;
        hoja.Cell(fila, 7).Value = m.MetodoPago.ToString();
        var signo = m.Direccion == DireccionMovimiento.Ingreso ? "+" : "-";
        hoja.Cell(fila, 8).Value = signo + m.Monto.ToString("C");
        fila++;
    }

    hoja.Columns(1, 8).AdjustToContents();

    using var stream = new MemoryStream();
    libro.SaveAs(stream);

    return Results.File(
        stream.ToArray(),
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        $"gastos-{hoy:yyyyMMdd}.xlsx");
});

// ------------------------------------------------------------------
// EXPORTAR AUDITORÍA A EXCEL — mismo mecanismo, pero además exige rol
// Gerencia/Desarrollador (igual que /auditoria). IAuditoriaService.
// ObtenerRecientesAsync repite ese mismo chequeo puertas adentro leyendo
// ISessionService.UsuarioActual — que en un endpoint minimal API (no un
// circuito Blazor) nadie pobló todavía, así que acá se resuelve el usuario
// directo desde la cookie ya autenticada por el middleware antes de llamar
// al servicio (mismo Usuario que vería la sesión real, sin pasar por
// AuthenticationStateProvider, que es específico de componentes Blazor).
// ------------------------------------------------------------------
app.MapGet("/auditoria/exportar-excel", async (HttpContext http, AppDbContext db, ISessionService sessionService, IAuditoriaService auditoriaService) =>
{
    var rol = http.User.FindFirst(ClaimTypes.Role)?.Value;
    if (rol != "Gerencia" && rol != "Desarrollador")
    {
        return Results.Forbid();
    }

    var usuarioId = int.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    sessionService.UsuarioActual = await db.Usuarios.FirstAsync(u => u.Id == usuarioId);

    var logs = await auditoriaService.ObtenerRecientesAsync(1000);

    using var libro = new XLWorkbook();
    var hoja = libro.Worksheets.Add("Auditoría");

    hoja.Cell(1, 1).Value = "Registro de auditoría — " + DateTime.Now.ToString("dd/MM/yyyy HH:mm");
    hoja.Cell(1, 1).Style.Font.Bold = true;
    hoja.Cell(1, 1).Style.Font.FontSize = 14;

    var fila = 3;
    hoja.Cell(fila, 1).Value = "Fecha/Hora";
    hoja.Cell(fila, 2).Value = "Acción";
    hoja.Cell(fila, 3).Value = "Descripción";
    hoja.Cell(fila, 4).Value = "Usuario";
    hoja.Cell(fila, 5).Value = "Entidad";
    hoja.Range(fila, 1, fila, 5).Style.Font.Bold = true;
    fila++;

    foreach (var log in logs)
    {
        hoja.Cell(fila, 1).Value = log.Timestamp.ToString("dd/MM/yyyy HH:mm:ss");
        hoja.Cell(fila, 2).Value = log.TipoAccion;
        hoja.Cell(fila, 3).Value = log.Descripcion;
        hoja.Cell(fila, 4).Value = log.UsuarioNombre;
        hoja.Cell(fila, 5).Value = log.EntidadAfectada + (log.EntidadId is { } id ? $" #{id}" : "");
        fila++;
    }

    hoja.Columns(1, 5).AdjustToContents();

    using var stream = new MemoryStream();
    libro.SaveAs(stream);

    return Results.File(
        stream.ToArray(),
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        $"auditoria-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
});

// ------------------------------------------------------------------
// DEMO -- simulación visual de un año de operación (ver
// Services/SimulacionVisualTemporal.cs). SOLO existe en modo Desarrollo:
// el servicio real de producción arranca sin ASPNETCORE_ENVIRONMENT=
// Development (ver deploy/instalar-servicio.ps1), así que esta ruta ni
// siquiera se registra ahí -- no hay forma de dispararla por accidente
// sobre los datos reales del hotel. Para usarla: arrancar el servidor a
// mano con RutaBaseDeDatos apuntando a una COPIA de la base (nunca la
// real) y ASPNETCORE_ENVIRONMENT=Development, y entrar a esta URL.
// ------------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.MapGet("/__demo/simular-timelapse", (IServiceScopeFactory scopeFactory) =>
    {
        _ = SimulacionVisualTemporal.EjecutarAsync(scopeFactory);
        return Results.Ok("Simulación de 1 año iniciada -- mirá /recepcion, /calendario, /reservas, /cafeteria, /reclamos, /almacen, /reportes/mensual...");
    });
}

app.Run();

/// <summary>
/// Arma los claims y firma la cookie de sesión para "usuario" — un solo lugar
/// para esta lógica, usado tanto por /account/login como por
/// /account/cambiar-password (que tiene que volver a firmar la MISMA sesión
/// con el SecurityStamp nuevo después de un cambio de contraseña propio).
/// </summary>
static async Task FirmarSesionAsync(HttpContext http, Usuario usuario)
{
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
        new(ClaimTypes.Name, usuario.Username),
        new(ClaimTypes.Role, usuario.Rol.ToString()),
        new("NombreCompleto", usuario.NombreCompleto),
        new("SecurityStamp", usuario.SecurityStamp),
    };
    var identidad = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

    await http.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identidad),
        new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12) });
}

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
