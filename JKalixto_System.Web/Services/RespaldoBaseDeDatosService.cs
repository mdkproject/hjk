using Microsoft.EntityFrameworkCore;
using JKalixto_System.Infrastructure.Data;

namespace JKalixto_System.Web.Services;

/// <summary>
/// Respaldo periódico de la base de datos — sin esto, un disco dañado o un
/// borrado por error pierde huéspedes, reservas y caja sin posibilidad de
/// recuperación.
///
/// Usa "VACUUM INTO", el mecanismo que el propio SQLite ofrece para hacer una
/// copia consistente de un archivo que sigue recibiendo escrituras (a
/// diferencia de copiar el archivo .db a mano con File.Copy, que puede capturar
/// una foto a medio escribir y quedar corrupta si justo en ese instante alguien
/// está haciendo un check-in o una venta).
///
/// Configurable en appsettings.json bajo "RespaldoBaseDeDatos" — si no se
/// configura nada, usa los valores por defecto de acá abajo.
///
/// Además de la carpeta principal, admite una "CarpetaSecundaria" opcional —
/// pensada para apuntarla a una carpeta sincronizada por un cliente de nube ya
/// instalado en la PC (OneDrive, Google Drive, Dropbox, etc.): este servicio
/// solo necesita escribir un archivo en una carpeta local, y es ese otro
/// programa el que se encarga de subirlo — sin que este sistema tenga que
/// manejar ninguna credencial de un servicio externo. Sin esa segunda copia,
/// un robo o incendio en el hotel se lleva la base Y todos los respaldos
/// juntos, porque hoy viven en el mismo disco físico.
/// </summary>
public class RespaldoBaseDeDatosService : BackgroundService
{
    private readonly IServiceProvider _servicios;
    private readonly IConfiguration _config;
    private readonly IHostEnvironment _entorno;
    private readonly ILogger<RespaldoBaseDeDatosService> _logger;

    public RespaldoBaseDeDatosService(
        IServiceProvider servicios,
        IConfiguration config,
        IHostEnvironment entorno,
        ILogger<RespaldoBaseDeDatosService> logger)
    {
        _servicios = servicios;
        _config = config;
        _entorno = entorno;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = TimeSpan.FromHours(_config.GetValue("RespaldoBaseDeDatos:IntervaloHoras", 6));
        var retener = _config.GetValue("RespaldoBaseDeDatos:RetenerCantidad", 30);
        var carpetas = ResolverCarpetasDestino();

        // Primer respaldo apenas arranca — un hotel recién instalado no debería
        // depender de esperar 6 horas para tener la primera copia.
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var carpeta in carpetas)
            {
                try
                {
                    await HacerRespaldoAsync(carpeta, retener, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "No se pudo completar el respaldo periódico de la base de datos en {Carpeta}.", carpeta);
                }
            }

            try
            {
                await Task.Delay(intervalo, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // El servidor se está apagando — nada que hacer, se sale del bucle.
            }
        }
    }

    private List<string> ResolverCarpetasDestino()
    {
        var principal = _config["RespaldoBaseDeDatos:Carpeta"];
        var carpetas = new List<string>
        {
            string.IsNullOrWhiteSpace(principal)
                ? Path.Combine(_entorno.ContentRootPath, "Data", "Backups")
                : principal
        };

        var secundaria = _config["RespaldoBaseDeDatos:CarpetaSecundaria"];
        if (!string.IsNullOrWhiteSpace(secundaria))
        {
            carpetas.Add(secundaria);
        }

        return carpetas;
    }

    private async Task HacerRespaldoAsync(string carpeta, int retener, CancellationToken token)
    {
        Directory.CreateDirectory(carpeta);

        using var scope = _servicios.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var nombreArchivo = $"jkalixto-{DateTime.Now:yyyyMMdd-HHmmss}.db";
        var destino = Path.Combine(carpeta, nombreArchivo);

        // Parametrizado (no interpolación directa en el SQL) aunque la ruta la
        // arma el propio servidor, no un usuario — buena práctica de todas formas.
        await contexto.Database.ExecuteSqlRawAsync("VACUUM INTO {0}", new object[] { destino });

        _logger.LogInformation("Respaldo de base de datos creado: {Destino}", destino);

        BorrarRespaldosViejos(carpeta, retener);
    }

    private void BorrarRespaldosViejos(string carpeta, int retener)
    {
        var respaldos = new DirectoryInfo(carpeta)
            .GetFiles("jkalixto-*.db")
            .OrderByDescending(f => f.CreationTimeUtc)
            .Skip(retener);

        foreach (var archivo in respaldos)
        {
            try
            {
                archivo.Delete();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo borrar el respaldo viejo {Archivo}.", archivo.FullName);
            }
        }
    }
}
