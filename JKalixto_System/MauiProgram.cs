using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Storage;
using JKalixto_System.Infrastructure.Data;
using JKalixto_System.Infrastructure.Repositories;
using JKalixto_System.Application.Services;
using JKalixto_System.Presentation.Pages;
using JKalixto_System.Presentation.ViewModels;
#if WINDOWS
using Microsoft.UI;
using Microsoft.UI.Windowing;
using WinRT.Interop;
#endif

namespace JKalixto_System;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if WINDOWS
        // ------------------------------------------------------------------
        // BARRA DE TÍTULO — por defecto, en Windows, el ☰ y el título de
        // Shell se dibujan DENTRO de la barra de título NATIVA del sistema
        // operativo (blanca, ajena a los temas de la app — por eso pintar
        // Shell.TitleColor con {DynamicResource} no alcanzaba: esa franja no
        // es contenido de MAUI, es chrome de Windows).
        //
        // ExtendsContentIntoTitleBar hace que esa franja pase a ser parte del
        // contenido de la app: Shell dibuja su propio ☰/título ahí adentro,
        // ya con los colores del tema actual, y sigue siendo arrastrable
        // (Shell se encarga de marcar esa zona como región de arrastre). Solo
        // quedan 100% nativos los botones de minimizar/maximizar/cerrar, así
        // que sus colores se pintan aparte en TemaService, cada vez que
        // cambia el tema.
        // ------------------------------------------------------------------
        builder.ConfigureMauiHandlers(handlers =>
        {
            Microsoft.Maui.Handlers.WindowHandler.Mapper.AppendToMapping("ExtenderBarraDeTitulo", (handler, view) =>
            {
                if (handler.PlatformView is not Microsoft.UI.Xaml.Window ventanaNativa)
                {
                    return;
                }

                var idVentana = Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(ventanaNativa));
                var appWindow = AppWindow.GetFromWindowId(idVentana);
                if (appWindow?.TitleBar is null)
                {
                    return;
                }

                appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                TemaService.RegistrarBarraTitulo(appWindow.TitleBar);
            });
        });
#endif

        // ------------------------------------------------------------------
        // BASE DE DATOS — SQLite local. Se registra como Transient: cada vez
        // que algo pide un AppDbContext recibe uno nuevo. Es el enfoque más
        // simple y seguro en apps de escritorio/MAUI (evita problemas de
        // "scope" que sí aplican en apps web tipo ASP.NET Core).
        // ------------------------------------------------------------------
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "jkalixto.db");
        // "Default Timeout=5": si dos terminales escriben casi al mismo tiempo (por
        // ejemplo, dos Check-in casi simultáneos), SQLite solo permite un escritor a
        // la vez. Sin este timeout, el segundo recibe un error crudo de "database is
        // locked" al instante. Con esto, espera hasta 5 segundos a que el primero
        // termine antes de fallar — tiempo de sobra en el uso real del hotel.
        // "NoTracking" por defecto: la gran mayoría de las consultas de este sistema
        // son de solo lectura (listar habitaciones, el dashboard, el calendario,
        // reportes) y nunca se vuelven a guardar — pedirle a EF Core que las siga
        // rastreando para detectar cambios es trabajo de más que nunca se usa. Los
        // pocos métodos que sí leen una fila para modificarla y guardarla (Check-in,
        // Check-out, registrar una venta, etc.) piden seguimiento explícito con
        // ".AsTracking()" en esa consulta puntual — ver Servicios.cs.
        builder.Services.AddDbContext<AppDbContext>(
            options => options
                .UseSqlite($"Data Source={dbPath};Default Timeout=5")
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking),
            ServiceLifetime.Transient);

        // --- Repositorios ---
        builder.Services.AddTransient<IUsuarioRepository, UsuarioRepository>();

        // --- Servicios de aplicación ---
        builder.Services.AddTransient<IAuthService, AuthService>();
        builder.Services.AddTransient<IAuditoriaService, AuditoriaService>();
        builder.Services.AddTransient<IHabitacionService, HabitacionService>();
        builder.Services.AddTransient<IReservaService, ReservaService>();
        builder.Services.AddTransient<IClientesService, ClientesService>();
        builder.Services.AddTransient<IGastosService, GastosService>();
        builder.Services.AddTransient<ICalendarioService, CalendarioService>();
        builder.Services.AddTransient<IDashboardService, DashboardService>();
        builder.Services.AddTransient<ISaunaService, SaunaService>();
        builder.Services.AddTransient<ICierreCajaService, CierreCajaService>();
        builder.Services.AddTransient<IInventarioService, InventarioService>();
        builder.Services.AddTransient<IComprobanteNumeracionService, ComprobanteNumeracionService>();
        builder.Services.AddTransient<IRegistroHuespedesService, RegistroHuespedesService>();
        builder.Services.AddTransient<IReclamosService, ReclamosService>();

        // ISessionService es Singleton a propósito: debe recordar quién inició
        // sesión mientras la app sigue abierta, sin importar a qué página se navegue.
        builder.Services.AddSingleton<ISessionService, SessionService>();

        // --- Shell y páginas (con sus ViewModels) ---
        builder.Services.AddTransient<AppShell>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<RecepcionPage>();
        builder.Services.AddTransient<RecepcionViewModel>();
        builder.Services.AddTransient<CheckInPage>();
        builder.Services.AddTransient<CheckInViewModel>();
        builder.Services.AddTransient<SaunaPage>();
        builder.Services.AddTransient<SaunaViewModel>();
        builder.Services.AddTransient<SaunaRegistroPage>();
        builder.Services.AddTransient<SaunaRegistroViewModel>();
        builder.Services.AddTransient<SaunaVentaPage>();
        builder.Services.AddTransient<SaunaVentaViewModel>();
        builder.Services.AddTransient<CierreCajaPage>();
        builder.Services.AddTransient<CierreCajaViewModel>();
        builder.Services.AddTransient<ReservasPage>();
        builder.Services.AddTransient<ReservasViewModel>();
        builder.Services.AddTransient<ReservaNuevaPage>();
        builder.Services.AddTransient<ReservaNuevaViewModel>();
        builder.Services.AddTransient<ClientesPage>();
        builder.Services.AddTransient<ClientesViewModel>();
        builder.Services.AddTransient<GastosPage>();
        builder.Services.AddTransient<GastosViewModel>();
        builder.Services.AddTransient<GastoNuevoPage>();
        builder.Services.AddTransient<GastoNuevoViewModel>();
        builder.Services.AddTransient<CalendarioPage>();
        builder.Services.AddTransient<CalendarioViewModel>();
        builder.Services.AddTransient<AuditoriaPage>();
        builder.Services.AddTransient<AuditoriaViewModel>();
        builder.Services.AddTransient<AlmacenPage>();
        builder.Services.AddTransient<AlmacenViewModel>();
        builder.Services.AddTransient<AlmacenMovimientoPage>();
        builder.Services.AddTransient<AlmacenMovimientoViewModel>();
        builder.Services.AddTransient<CafeteriaPage>();
        builder.Services.AddTransient<CafeteriaViewModel>();
        builder.Services.AddTransient<ReclamosPage>();
        builder.Services.AddTransient<ReclamosViewModel>();
        builder.Services.AddTransient<ReclamoNuevoPage>();
        builder.Services.AddTransient<ReclamoNuevoViewModel>();
        builder.Services.AddTransient<RegistroHuespedesPage>();
        builder.Services.AddTransient<RegistroHuespedesViewModel>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        InicializarBaseDeDatos(app.Services, dbPath);

        return app;
    }

    /// <summary>
    /// MODO DE PRUEBAS: mientras esto sea "true", la app arranca directo en el
    /// Dashboard con una sesión ya iniciada (usuario "marcelo.dev", rol
    /// Desarrollador, que ve todo el sistema sin restricciones) — así te ahorrás
    /// escribir usuario/clave cada vez que abrís la app para probar.
    ///
    /// Cuando quieras volver a pedir usuario/clave de verdad (por ejemplo, para
    /// repartir credenciales distintas a cada persona del hotel), cambiá esto a
    /// "false" y listo: no hay que tocar nada más.
    /// </summary>
    private const bool ModoPruebaSinLogin = true;

    /// <summary>Username del usuario que se auto-loguea en Modo de Pruebas. Tiene que
    /// existir en el seed de Usuarios de AppDbContext.</summary>
    private const string UsuarioDePrueba = "marcelo.dev";

    /// <summary>
    /// Si es "true", cada vez que arranca la app se generan datos de prueba
    /// ALEATORIOS Y FICTICIOS: 5 huéspedes con Check-in en el hotel, 5 clientes de
    /// sauna, algunos consumos de POS, y un par de reservas a futuro. Los nombres,
    /// DNI y celulares son inventados (ver DatosPruebaSeeder.cs) — no reemplaza
    /// datos reales, solo evita tener que probar todo con el sistema vacío.
    /// Cuando el sistema pase a usarse con datos reales, cambiá esto a "false".
    /// </summary>
    private const bool GenerarDatosDePrueba = true;

    /// <summary>
    /// Prepara la base de datos SQLite al arrancar la app.
    ///
    /// NOTA IMPORTANTE PARA MARCELO: mientras seguimos construyendo el sistema
    /// (y el modelo de datos sigue creciendo de sesión en sesión), en una build
    /// de DESARROLLO (Debug) esta función BORRA el archivo .db viejo y lo vuelve
    /// a crear con el esquema actualizado cada vez que abres la app. Esto es
    /// intencional: evita el error "no such table" cuando agregamos tablas
    /// nuevas, y siempre vas a tener datos de prueba frescos.
    ///
    /// SEGURO POR CONSTRUCCIÓN: todo lo destructivo (borrar la base, auto-login,
    /// generar datos ficticios) vive dentro de "#if DEBUG". Una build Release
    /// —la que se instala en el hotel— NUNCA ejecuta ese bloque, sin importar el
    /// valor de ModoPruebaSinLogin/GenerarDatosDePrueba: no hace falta acordarse
    /// de "apagar" nada antes de compilar para producción, el compilador ya lo
    /// saca. En Release solo se crea el esquema si todavía no existe
    /// (EnsureCreated no toca una base que ya tiene datos).
    ///
    /// Cuando el sistema esté terminado y listo para usarse con datos reales,
    /// pasamos a Migrations formales de EF Core (que actualizan el esquema SIN
    /// perder datos, incluso en Debug).
    /// </summary>
    private static void InicializarBaseDeDatos(IServiceProvider services, string dbPath)
    {
#if DEBUG
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }
#endif

        using var db = services.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();

#if DEBUG
        if (ModoPruebaSinLogin)
        {
            // Se hace acá (síncrono, ANTES de que se cree la primera pantalla)
            // por la misma razón que EnsureCreated() se hace acá: para que
            // cuando el Dashboard aparezca, el usuario ya esté listo — sin esto,
            // habría una carrera contra el tiempo entre "la pantalla ya se
            // dibujó" y "el usuario todavía no se cargó de la BD".
            var sessionService = services.GetRequiredService<ISessionService>();
            sessionService.UsuarioActual = db.Usuarios.FirstOrDefault(u => u.Username == UsuarioDePrueba && u.Activo);
        }

        if (GenerarDatosDePrueba)
        {
            // Los datos de prueba necesitan un usuario "autor" para quedar bien
            // registrados en la auditoría (quién hizo el Check-in, quién registró
            // la reserva, etc.) — se busca uno aunque ModoPruebaSinLogin esté en
            // "false", para que este flag funcione de forma independiente del otro.
            var usuarioParaSeed = db.Usuarios.FirstOrDefault(u => u.Username == UsuarioDePrueba && u.Activo)
                                   ?? db.Usuarios.FirstOrDefault(u => u.Activo);

            if (usuarioParaSeed is not null)
            {
                DatosPruebaSeeder.Sembrar(db, usuarioParaSeed.Id);
            }
        }
#endif
    }
}
