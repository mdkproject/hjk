using Microsoft.EntityFrameworkCore;
using JKalixto_System.Domain.Models;
using JKalixto_System.Infrastructure.Data;

namespace JKalixto_System.Web.Services;

public class SaludSistemaDto
{
    public string RutaBaseDeDatos { get; set; } = "";
    public long TamanoBaseDeDatosBytes { get; set; }
    public long EspacioLibreDiscoBytes { get; set; }
    public long EspacioTotalDiscoBytes { get; set; }

    public string CarpetaRespaldoPrincipal { get; set; } = "";
    public DateTime? UltimoRespaldoPrincipal { get; set; }
    public int CantidadRespaldosPrincipal { get; set; }

    public bool RespaldoSecundarioConfigurado { get; set; }
    public string? CarpetaRespaldoSecundario { get; set; }
    public DateTime? UltimoRespaldoSecundario { get; set; }
    public int CantidadRespaldosSecundario { get; set; }

    public TimeSpan TiempoActivo { get; set; }
    public string Entorno { get; set; } = "";

    public bool HayMigracionesPendientes { get; set; }
    public List<string> MigracionesPendientes { get; set; } = new();

    public int TotalUsuariosActivos { get; set; }
    public int TotalHabitaciones { get; set; }
    public int TotalReservasConfirmadas { get; set; }
}

/// <summary>
/// Panel de Salud del Sistema — reúne en un solo lugar las señales que hoy
/// solo se podían ver revisando archivos/carpetas a mano por PowerShell
/// (tamaño de la base, si el respaldo automático realmente corrió, espacio
/// libre en disco, si hay una migración de esquema sin aplicar). Sin esto,
/// un problema como "el disco se quedó sin espacio" o "el respaldo dejó de
/// correr hace 3 días" solo se nota cuando ya causó un problema real.
///
/// Vive en el proyecto Web (no en Application) a propósito: todo lo que
/// expone es específico del servidor que aloja la app (rutas de archivo,
/// disco, tiempo de actividad del proceso) — no aplica a la versión de
/// escritorio MAUI y no tiene sentido compartirlo con ella.
/// </summary>
public class SaludSistemaService
{
    private static readonly DateTime _horaInicioProceso = DateTime.Now;

    private readonly AppDbContext _context;
    private readonly IConfiguration _config;
    private readonly IHostEnvironment _entorno;

    public SaludSistemaService(AppDbContext context, IConfiguration config, IHostEnvironment entorno)
    {
        _context = context;
        _config = config;
        _entorno = entorno;
    }

    public async Task<SaludSistemaDto> ObtenerAsync()
    {
        var rutaDb = _context.Database.GetDbConnection().DataSource;

        var dto = new SaludSistemaDto
        {
            RutaBaseDeDatos = rutaDb,
            TamanoBaseDeDatosBytes = File.Exists(rutaDb) ? new FileInfo(rutaDb).Length : 0,
            Entorno = _entorno.EnvironmentName,
            TiempoActivo = DateTime.Now - _horaInicioProceso,
        };

        try
        {
            var raiz = Path.GetPathRoot(Path.GetFullPath(rutaDb));
            if (!string.IsNullOrEmpty(raiz))
            {
                var unidad = new DriveInfo(raiz);
                dto.EspacioLibreDiscoBytes = unidad.AvailableFreeSpace;
                dto.EspacioTotalDiscoBytes = unidad.TotalSize;
            }
        }
        catch
        {
            // Ruta de red o disco no accesible por DriveInfo -- el resto del
            // panel sigue siendo útil aunque no se pueda leer el espacio libre.
        }

        dto.CarpetaRespaldoPrincipal = ResolverCarpetaPrincipal();
        (dto.UltimoRespaldoPrincipal, dto.CantidadRespaldosPrincipal) = InspeccionarCarpeta(dto.CarpetaRespaldoPrincipal);

        var carpetaSecundaria = _config["RespaldoBaseDeDatos:CarpetaSecundaria"];
        dto.RespaldoSecundarioConfigurado = !string.IsNullOrWhiteSpace(carpetaSecundaria);
        if (dto.RespaldoSecundarioConfigurado)
        {
            dto.CarpetaRespaldoSecundario = carpetaSecundaria;
            (dto.UltimoRespaldoSecundario, dto.CantidadRespaldosSecundario) = InspeccionarCarpeta(carpetaSecundaria!);
        }

        dto.MigracionesPendientes = (await _context.Database.GetPendingMigrationsAsync()).ToList();
        dto.HayMigracionesPendientes = dto.MigracionesPendientes.Count > 0;

        dto.TotalUsuariosActivos = await _context.Usuarios.CountAsync(u => u.Activo);
        dto.TotalHabitaciones = await _context.Habitaciones.CountAsync();
        dto.TotalReservasConfirmadas = await _context.Reservas.CountAsync(r => r.Estado == EstadoReserva.Confirmada);

        return dto;
    }

    private string ResolverCarpetaPrincipal()
    {
        var configurada = _config["RespaldoBaseDeDatos:Carpeta"];
        return string.IsNullOrWhiteSpace(configurada)
            ? Path.Combine(_entorno.ContentRootPath, "Data", "Backups")
            : configurada;
    }

    private static (DateTime? ultimo, int cantidad) InspeccionarCarpeta(string carpeta)
    {
        if (!Directory.Exists(carpeta))
        {
            return (null, 0);
        }

        var archivos = new DirectoryInfo(carpeta).GetFiles("jkalixto-*.db");
        if (archivos.Length == 0)
        {
            return (null, 0);
        }

        var ultimo = archivos.OrderByDescending(f => f.CreationTimeUtc).First();
        return (ultimo.CreationTimeUtc.ToLocalTime(), archivos.Length);
    }
}
