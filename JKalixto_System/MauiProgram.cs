using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Storage;
using JKalixto_System.Infrastructure.Data;
using JKalixto_System.Infrastructure.Repositories;
using JKalixto_System.Application.Services;
using JKalixto_System.Presentation.Pages;
using JKalixto_System.Presentation.ViewModels;

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

        // ------------------------------------------------------------------
        // BASE DE DATOS — SQLite local. Se registra como Transient: cada vez
        // que algo pide un AppDbContext recibe uno nuevo. Es el enfoque más
        // simple y seguro en apps de escritorio/MAUI (evita problemas de
        // "scope" que sí aplican en apps web tipo ASP.NET Core).
        // ------------------------------------------------------------------
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "jkalixto.db");
        builder.Services.AddDbContext<AppDbContext>(
            options => options.UseSqlite($"Data Source={dbPath}"),
            ServiceLifetime.Transient);

        // --- Repositorios ---
        builder.Services.AddTransient<IUsuarioRepository, UsuarioRepository>();

        // --- Servicios de aplicación ---
        builder.Services.AddTransient<IAuthService, AuthService>();
        builder.Services.AddTransient<IAuditoriaService, AuditoriaService>();
        builder.Services.AddTransient<IHabitacionService, HabitacionService>();
        builder.Services.AddTransient<IDashboardService, DashboardService>();

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

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        InicializarBaseDeDatos(app.Services, dbPath);

        return app;
    }

    /// <summary>
    /// Prepara la base de datos SQLite al arrancar la app.
    ///
    /// NOTA IMPORTANTE PARA MARCELO: mientras seguimos construyendo el sistema
    /// (y el modelo de datos sigue creciendo de sesión en sesión), esta función
    /// BORRA el archivo .db viejo y lo vuelve a crear con el esquema actualizado
    /// cada vez que abres la app. Esto es intencional: evita el error "no such
    /// table" cuando agregamos tablas nuevas, y siempre vas a tener datos de
    /// prueba frescos (usuarios + 36 habitaciones + catálogo del POS).
    ///
    /// Cuando el sistema esté terminado y listo para usarse con datos reales,
    /// quitamos este borrado automático y pasamos a Migrations formales de EF
    /// Core (que actualizan el esquema SIN perder datos).
    /// </summary>
    private static void InicializarBaseDeDatos(IServiceProvider services, string dbPath)
    {
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }

        using var db = services.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
    }
}
