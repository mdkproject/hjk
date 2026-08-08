using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using JKalixto_System.Domain.Models;
using JKalixto_System.Infrastructure.Data;
using JKalixto_System.Infrastructure.Repositories;

namespace JKalixto_System.Application.Services;

/// <summary>
/// Resultado de un intento de inicio de sesión. Se usa "Exito" en vez de excepciones
/// para que el ViewModel pueda mostrar un mensaje claro al recepcionista/gerente.
/// </summary>
public class ResultadoLogin
{
    public bool Exito { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public Usuario? Usuario { get; set; }
}

public interface IAuthService
{
    /// <summary>Valida usuario y contraseña contra la base de datos.</summary>
    Task<ResultadoLogin> IniciarSesionAsync(string username, string password);
}

/// <summary>
/// Implementación del login. Compara la contraseña ingresada contra el hash
/// guardado en BD usando BCrypt (nunca se compara texto plano contra texto plano).
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUsuarioRepository _usuarioRepository;

    public AuthService(IUsuarioRepository usuarioRepository)
    {
        _usuarioRepository = usuarioRepository;
    }

    public async Task<ResultadoLogin> IniciarSesionAsync(string username, string password)
    {
        var usuario = await _usuarioRepository.ObtenerPorUsernameAsync(username);

        if (usuario is null)
        {
            return new ResultadoLogin
            {
                Exito = false,
                Mensaje = "Usuario o contraseña incorrectos."
            };
        }

        bool passwordValida;
        try
        {
            passwordValida = BCrypt.Net.BCrypt.Verify(password, usuario.PasswordHash);
        }
        catch
        {
            // Un hash corrupto o mal formado nunca debe tumbar la app: se trata como password inválida.
            passwordValida = false;
        }

        if (!passwordValida)
        {
            return new ResultadoLogin
            {
                Exito = false,
                Mensaje = "Usuario o contraseña incorrectos."
            };
        }

        return new ResultadoLogin
        {
            Exito = true,
            Mensaje = "Bienvenido",
            Usuario = usuario
        };
    }
}

/// <summary>
/// Guarda quién es el usuario que inició sesión mientras la app está abierta.
/// Se registra como Singleton en MauiProgram.cs: existe UNA sola instancia
/// compartida por todas las páginas mientras la app está corriendo.
/// </summary>
public interface ISessionService
{
    Usuario? UsuarioActual { get; set; }
    bool HaySesionActiva { get; }
    void CerrarSesion();
}

public class SessionService : ISessionService
{
    public Usuario? UsuarioActual { get; set; }
    public bool HaySesionActiva => UsuarioActual is not null;

    public void CerrarSesion()
    {
        UsuarioActual = null;
    }
}

// ============================================================
// AUDITORÍA
// ============================================================

public interface IAuditoriaService
{
    /// <summary>Registra una acción crítica en el log inmutable. Nunca debe lanzar una excepción que tumbe la operación principal.</summary>
    Task RegistrarAsync(string tipoAccion, string descripcion, int usuarioId, string entidadAfectada, int? entidadId);

    Task<List<LogAuditoria>> ObtenerRecientesAsync(int cantidad);
}

public class AuditoriaService : IAuditoriaService
{
    private readonly AppDbContext _context;

    public AuditoriaService(AppDbContext context)
    {
        _context = context;
    }

    public async Task RegistrarAsync(string tipoAccion, string descripcion, int usuarioId, string entidadAfectada, int? entidadId)
    {
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId);

        _context.LogsAuditoria.Add(new LogAuditoria
        {
            Timestamp = DateTime.Now,
            TipoAccion = tipoAccion,
            Descripcion = descripcion,
            UsuarioId = usuarioId,
            UsuarioNombre = usuario?.NombreCompleto ?? "Desconocido",
            EntidadAfectada = entidadAfectada,
            EntidadId = entidadId
        });

        await _context.SaveChangesAsync();
    }

    public async Task<List<LogAuditoria>> ObtenerRecientesAsync(int cantidad)
    {
        return await _context.LogsAuditoria
            .OrderByDescending(l => l.Timestamp)
            .Take(cantidad)
            .ToListAsync();
    }
}

// ============================================================
// MÓDULO HOTEL — DTOs
// ============================================================

/// <summary>Proyección lista-para-mostrar de una habitación en la grilla de Recepción.</summary>
public class HabitacionCardDto
{
    public int HabitacionId { get; set; }
    public int Numero { get; set; }
    public int Piso { get; set; }
    public TipoHabitacion Tipo { get; set; }
    public EstadoHabitacion Estado { get; set; }
    public decimal TarifaNoche { get; set; }
    public string? MotivoMantenimiento { get; set; }

    // Solo tienen valor si Estado == Ocupada
    public int? EstadiaId { get; set; }
    public string? NombreHuesped { get; set; }
    public decimal? TotalAcumulado { get; set; }

    public Color ColorEstado => Estado switch
    {
        EstadoHabitacion.Disponible => Color.FromArgb("#22C55E"),
        EstadoHabitacion.Ocupada => Color.FromArgb("#3B82F6"),
        EstadoHabitacion.LimpiezaSalida => Color.FromArgb("#F59E0B"),
        EstadoHabitacion.Mantenimiento => Color.FromArgb("#EF4444"),
        _ => Colors.Gray
    };

    public string EtiquetaEstado => Estado switch
    {
        EstadoHabitacion.Disponible => "Disponible",
        EstadoHabitacion.Ocupada => "Ocupada",
        EstadoHabitacion.LimpiezaSalida => "Limpieza",
        EstadoHabitacion.Mantenimiento => "Mantenimiento",
        _ => Estado.ToString()
    };

    public string EtiquetaTipo => Tipo.ToString();

    /// <summary>Evita tener que usar un converter en XAML solo para saber si mostrar los datos del huésped.</summary>
    public bool TieneHuesped => Estado == EstadoHabitacion.Ocupada;

    /// <summary>
    /// Igual que ColorEstado pero como Brush. Border.Stroke es de tipo Brush (no Color),
    /// así que se expone esta versión aparte para el borde de la tarjeta — evita depender
    /// de una conversión implícita Color→Brush en el binding.
    /// </summary>
    public Brush BrushEstado => new SolidColorBrush(ColorEstado);
}

/// <summary>Datos que llegan desde CheckInPage para crear una nueva Estadia.</summary>
public class NuevoCheckInDto
{
    public int HabitacionId { get; set; }
    public string DNI { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string Celular { get; set; } = string.Empty;
    public TipoComprobante TipoComprobante { get; set; } = TipoComprobante.Boleta;
    public string? RUC { get; set; }
    public string? RazonSocial { get; set; }
    public string? CorreoFacturacion { get; set; }
    public List<string> Acompanantes { get; set; } = new();
    public int UsuarioId { get; set; }
}

// ============================================================
// MÓDULO HOTEL — Servicio
// ============================================================

public interface IHabitacionService
{
    Task<List<HabitacionCardDto>> ObtenerPorPisoAsync(int piso);
    Task CheckInAsync(NuevoCheckInDto dto);
    Task CheckOutAsync(int estadiaId, int usuarioId);
    Task IniciarMantenimientoAsync(int habitacionId, string motivo, int usuarioId);
    Task FinalizarMantenimientoAsync(int habitacionId, int usuarioId);
    Task FinalizarLimpiezaAsync(int habitacionId);
    Task RegistrarLimpiezaIntermediaAsync(int habitacionId, int usuarioId);
}

/// <summary>
/// Toda la lógica del ciclo de vida de una habitación: Check-in, Check-out,
/// mantenimiento y limpieza. Usa AppDbContext directamente (no un Repositorio
/// intermedio) para garantizar que los cambios a Habitacion y Estadia se
/// guarden juntos, en la misma operación — así se evita un bug sutil de EF
/// Core que aparece si cada Repositorio tuviera su propia conexión separada.
/// </summary>
public class HabitacionService : IHabitacionService
{
    private readonly AppDbContext _context;
    private readonly IAuditoriaService _auditoriaService;

    public HabitacionService(AppDbContext context, IAuditoriaService auditoriaService)
    {
        _context = context;
        _auditoriaService = auditoriaService;
    }

    public async Task<List<HabitacionCardDto>> ObtenerPorPisoAsync(int piso)
    {
        var habitaciones = await _context.Habitaciones
            .Where(h => h.Piso == piso)
            .OrderBy(h => h.Numero)
            .ToListAsync();

        // Se traen TODAS las estadías activas (nunca son más de 36) y se
        // cruzan en memoria — así evitamos depender de que EF traduzca
        // correctamente un join más complejo a SQL.
        var estadiasActivas = await _context.Estadias
            .Where(e => e.Estado == EstadoEstadia.Activa)
            .ToListAsync();

        var resultado = new List<HabitacionCardDto>();
        foreach (var h in habitaciones)
        {
            var estadia = estadiasActivas.FirstOrDefault(e => e.HabitacionId == h.Id);

            resultado.Add(new HabitacionCardDto
            {
                HabitacionId = h.Id,
                Numero = h.Numero,
                Piso = h.Piso,
                Tipo = h.Tipo,
                Estado = h.Estado,
                TarifaNoche = h.TarifaNoche,
                MotivoMantenimiento = h.MotivoMantenimiento,
                EstadiaId = estadia?.Id,
                NombreHuesped = estadia?.NombreCompleto,
                TotalAcumulado = estadia?.TotalAcumulado
            });
        }

        return resultado;
    }

    public async Task CheckInAsync(NuevoCheckInDto dto)
    {
        var habitacion = await _context.Habitaciones.FirstOrDefaultAsync(h => h.Id == dto.HabitacionId);
        if (habitacion is null)
        {
            throw new InvalidOperationException("La habitación no existe.");
        }
        if (habitacion.Estado != EstadoHabitacion.Disponible)
        {
            throw new InvalidOperationException("Esta habitación ya no está Disponible. Actualiza la pantalla e intenta de nuevo.");
        }

        var estadia = new Estadia
        {
            HabitacionId = habitacion.Id,
            DNI = dto.DNI,
            NombreCompleto = dto.NombreCompleto,
            Celular = dto.Celular,
            FechaCheckIn = DateTime.Now,
            Estado = EstadoEstadia.Activa,
            TipoComprobante = dto.TipoComprobante,
            RUC = dto.RUC,
            RazonSocial = dto.RazonSocial,
            CorreoFacturacion = dto.CorreoFacturacion,
            AccesoSaunaIncluido = true,
            TotalAcumulado = habitacion.TarifaNoche,
            UsuarioCheckInId = dto.UsuarioId
        };

        foreach (var nombreAcompanante in dto.Acompanantes)
        {
            estadia.Acompanantes.Add(new Acompanante { NombreCompleto = nombreAcompanante });
        }

        habitacion.Estado = EstadoHabitacion.Ocupada;
        _context.Estadias.Add(estadia);

        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync(
            "CHECK_IN",
            $"Check-in de {dto.NombreCompleto} (DNI {dto.DNI}) en habitación {habitacion.Numero}.",
            dto.UsuarioId, "Estadia", estadia.Id);
    }

    public async Task CheckOutAsync(int estadiaId, int usuarioId)
    {
        var estadia = await _context.Estadias
            .Include(e => e.Habitacion)
            .FirstOrDefaultAsync(e => e.Id == estadiaId);

        if (estadia is null)
        {
            throw new InvalidOperationException("La estadía no existe.");
        }
        if (estadia.Estado != EstadoEstadia.Activa)
        {
            throw new InvalidOperationException("Esta estadía ya fue cerrada.");
        }

        estadia.Estado = EstadoEstadia.Finalizada;
        estadia.FechaCheckOut = DateTime.Now;
        estadia.UsuarioCheckOutId = usuarioId;

        if (estadia.Habitacion is not null)
        {
            estadia.Habitacion.Estado = EstadoHabitacion.LimpiezaSalida;
        }

        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync(
            "CHECKOUT",
            $"Check-out de {estadia.NombreCompleto}, habitación {estadia.Habitacion?.Numero}. Total: S/ {estadia.TotalAcumulado:0.00}.",
            usuarioId, "Estadia", estadia.Id);
    }

    public async Task IniciarMantenimientoAsync(int habitacionId, string motivo, int usuarioId)
    {
        // REGLA ANTI-FRAUDE: sin motivo, bloqueo total.
        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new InvalidOperationException("El motivo de mantenimiento es obligatorio.");
        }

        var habitacion = await _context.Habitaciones.FirstOrDefaultAsync(h => h.Id == habitacionId);
        if (habitacion is null)
        {
            throw new InvalidOperationException("La habitación no existe.");
        }
        if (habitacion.Estado != EstadoHabitacion.Disponible)
        {
            throw new InvalidOperationException("Solo se puede enviar a Mantenimiento una habitación Disponible.");
        }

        habitacion.Estado = EstadoHabitacion.Mantenimiento;
        habitacion.MotivoMantenimiento = motivo.Trim();
        habitacion.FechaInicioMantenimiento = DateTime.Now;

        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync(
            "MANTENIMIENTO_INICIO",
            $"Habitación {habitacion.Numero} pasó a Mantenimiento. Motivo: {habitacion.MotivoMantenimiento}",
            usuarioId, "Habitacion", habitacion.Id);
    }

    public async Task FinalizarMantenimientoAsync(int habitacionId, int usuarioId)
    {
        var habitacion = await _context.Habitaciones.FirstOrDefaultAsync(h => h.Id == habitacionId);
        if (habitacion is null)
        {
            throw new InvalidOperationException("La habitación no existe.");
        }

        var motivoAnterior = habitacion.MotivoMantenimiento;
        habitacion.Estado = EstadoHabitacion.Disponible;
        habitacion.MotivoMantenimiento = null;
        habitacion.FechaInicioMantenimiento = null;

        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync(
            "MANTENIMIENTO_FIN",
            $"Habitación {habitacion.Numero} volvió a Disponible. (Motivo que tuvo: {motivoAnterior})",
            usuarioId, "Habitacion", habitacion.Id);
    }

    public async Task FinalizarLimpiezaAsync(int habitacionId)
    {
        var habitacion = await _context.Habitaciones.FirstOrDefaultAsync(h => h.Id == habitacionId);
        if (habitacion is null)
        {
            throw new InvalidOperationException("La habitación no existe.");
        }
        if (habitacion.Estado != EstadoHabitacion.LimpiezaSalida)
        {
            throw new InvalidOperationException("La habitación no está en Limpieza.");
        }

        habitacion.Estado = EstadoHabitacion.Disponible;
        await _context.SaveChangesAsync();
    }

    public async Task RegistrarLimpiezaIntermediaAsync(int habitacionId, int usuarioId)
    {
        var habitacion = await _context.Habitaciones.FirstOrDefaultAsync(h => h.Id == habitacionId);
        if (habitacion is null)
        {
            return;
        }

        // No cambia de estado — solo queda constancia en el log (ver máquina de estados del documento maestro).
        await _auditoriaService.RegistrarAsync(
            "SOLICITUD_LIMPIEZA",
            $"Se solicitó limpieza intermedia para la habitación {habitacion.Numero} (sin cambio de estado).",
            usuarioId, "Habitacion", habitacion.Id);
    }
}

// ============================================================
// DASHBOARD — DTOs + Servicio
// ============================================================

public class EstadoHabitacionResumenDto
{
    public EstadoHabitacion Estado { get; set; }
    public int Cantidad { get; set; }

    /// <summary>Ancho ya calculado en pixeles para dibujar la barra proporcional (ver DashboardService).</summary>
    public double AnchoBarra { get; set; }
    public Color ColorBarra { get; set; } = Colors.Gray;
    public string Etiqueta { get; set; } = string.Empty;
}

public class ResumenDashboardDto
{
    public decimal IngresosHotelHoy { get; set; }
    public decimal IngresosSaunaHoy { get; set; }
    public decimal IngresosTotalHoy => IngresosHotelHoy + IngresosSaunaHoy;

    /// <summary>Porcentaje 0-100.</summary>
    public double TasaOcupacion { get; set; }
    public int ClientesSaunaHoy { get; set; }
    public int AlertasActivas { get; set; }

    public List<EstadoHabitacionResumenDto> ResumenEstados { get; set; } = new();
    public List<LogAuditoria> UltimosEventos { get; set; } = new();
}

public interface IDashboardService
{
    Task<ResumenDashboardDto> ObtenerResumenAsync();
}

/// <summary>
/// Calcula los KPIs del Dashboard gerencial. Todas las sumas se hacen EN MEMORIA
/// (después de traer los datos con ToListAsync) en vez de usar Sum() directo en
/// la consulta — así evitamos depender de que el proveedor de SQLite traduzca
/// bien las sumas de columnas decimal a SQL.
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly AppDbContext _context;

    public DashboardService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ResumenDashboardDto> ObtenerResumenAsync()
    {
        var hoy = DateTime.Now.Date;

        var habitaciones = await _context.Habitaciones.ToListAsync();

        var estadiasHoy = await _context.Estadias
            .Where(e => e.FechaCheckIn.Date == hoy || (e.FechaCheckOut != null && e.FechaCheckOut.Value.Date == hoy))
            .ToListAsync();

        var clientesSaunaHoy = await _context.ClientesSauna
            .Where(c => c.FechaIngreso.Date == hoy)
            .ToListAsync();

        var ventasSaunaHoy = await _context.VentasSauna
            .Where(v => v.Fecha.Date == hoy && v.Estado != EstadoVenta.Anulada)
            .ToListAsync();

        var ultimosEventos = await _context.LogsAuditoria
            .OrderByDescending(l => l.Timestamp)
            .Take(8)
            .ToListAsync();

        decimal ingresosHotel = estadiasHoy.Sum(e => e.TotalAcumulado);
        decimal ingresosSauna = ventasSaunaHoy.Sum(v => v.Total);

        int ocupadas = habitaciones.Count(h => h.Estado == EstadoHabitacion.Ocupada);
        double tasaOcupacion = habitaciones.Count > 0 ? ocupadas * 100.0 / habitaciones.Count : 0;

        int alertas = habitaciones.Count(h =>
            h.Estado == EstadoHabitacion.Mantenimiento &&
            h.FechaInicioMantenimiento.HasValue &&
            (DateTime.Now - h.FechaInicioMantenimiento.Value).TotalHours > 4);

        return new ResumenDashboardDto
        {
            IngresosHotelHoy = ingresosHotel,
            IngresosSaunaHoy = ingresosSauna,
            TasaOcupacion = Math.Round(tasaOcupacion, 1),
            ClientesSaunaHoy = clientesSaunaHoy.Count,
            AlertasActivas = alertas,
            ResumenEstados = CalcularResumenEstados(habitaciones),
            UltimosEventos = ultimosEventos
        };
    }

    private static List<EstadoHabitacionResumenDto> CalcularResumenEstados(List<Habitacion> habitaciones)
    {
        const double anchoTotalBarra = 320;
        int total = habitaciones.Count == 0 ? 1 : habitaciones.Count;

        var estadosOrdenados = new[]
        {
            EstadoHabitacion.Disponible,
            EstadoHabitacion.Ocupada,
            EstadoHabitacion.LimpiezaSalida,
            EstadoHabitacion.Mantenimiento
        };

        var resultado = new List<EstadoHabitacionResumenDto>();
        foreach (var estado in estadosOrdenados)
        {
            int cantidad = habitaciones.Count(h => h.Estado == estado);
            double ancho = cantidad == 0 ? 0 : Math.Max(cantidad * anchoTotalBarra / total, 4);

            resultado.Add(new EstadoHabitacionResumenDto
            {
                Estado = estado,
                Cantidad = cantidad,
                AnchoBarra = ancho,
                ColorBarra = ColorParaEstado(estado),
                Etiqueta = EtiquetaParaEstado(estado)
            });
        }

        return resultado;
    }

    private static Color ColorParaEstado(EstadoHabitacion estado) => estado switch
    {
        EstadoHabitacion.Disponible => Color.FromArgb("#22C55E"),
        EstadoHabitacion.Ocupada => Color.FromArgb("#3B82F6"),
        EstadoHabitacion.LimpiezaSalida => Color.FromArgb("#F59E0B"),
        EstadoHabitacion.Mantenimiento => Color.FromArgb("#EF4444"),
        _ => Colors.Gray
    };

    private static string EtiquetaParaEstado(EstadoHabitacion estado) => estado switch
    {
        EstadoHabitacion.Disponible => "Disponible",
        EstadoHabitacion.Ocupada => "Ocupada",
        EstadoHabitacion.LimpiezaSalida => "Limpieza",
        EstadoHabitacion.Mantenimiento => "Mantenimiento",
        _ => estado.ToString()
    };
}
