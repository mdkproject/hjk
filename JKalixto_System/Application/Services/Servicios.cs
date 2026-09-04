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
    private readonly ISessionService _sessionService;

    public AuditoriaService(AppDbContext context, ISessionService sessionService)
    {
        _context = context;
        _sessionService = sessionService;
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
        // El menú solo muestra "Auditoría" a Gerencia/Desarrollador (ver
        // AppShell.xaml.cs), pero esa es solo una ayuda visual — acá se repite la
        // misma regla a nivel de servicio para que nadie pueda leer los logs
        // simplemente llamando al método, sin pasar por esa pantalla.
        var rol = _sessionService.UsuarioActual?.Rol;
        if (rol != RolUsuario.Gerencia && rol != RolUsuario.Desarrollador)
        {
            throw new UnauthorizedAccessException("No tienes permiso para ver el registro de auditoría.");
        }

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
    public TipoDocumento? TipoDocumentoHuesped { get; set; }
    public string? NumeroDocumentoHuesped { get; set; }
    public string? CelularHuesped { get; set; }
    public DateTime? FechaCheckInHuesped { get; set; }
    public List<string> AcompanantesHuesped { get; set; } = new();

    public string EtiquetaTipoDocumentoHuesped => TipoDocumentoHuesped switch
    {
        TipoDocumento.Pasaporte => "Pasaporte",
        TipoDocumento.CarneExtranjeria => "Carné Ext.",
        _ => "DNI"
    };

    public string AcompanantesTexto => AcompanantesHuesped.Count == 0
        ? "Sin acompañantes"
        : string.Join(", ", AcompanantesHuesped);

    public Color ColorEstado => Estado switch
    {
        EstadoHabitacion.Disponible => (Color)Microsoft.Maui.Controls.Application.Current!.Resources["ColorDisponible"],
        EstadoHabitacion.Ocupada => (Color)Microsoft.Maui.Controls.Application.Current!.Resources["ColorOcupada"],
        EstadoHabitacion.LimpiezaSalida => (Color)Microsoft.Maui.Controls.Application.Current!.Resources["ColorLimpieza"],
        EstadoHabitacion.Mantenimiento => (Color)Microsoft.Maui.Controls.Application.Current!.Resources["ColorMantenimiento"],
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
    public TipoDocumento TipoDocumento { get; set; } = TipoDocumento.DNI;
    public string NumeroDocumento { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string Celular { get; set; } = string.Empty;
    public TipoComprobante TipoComprobante { get; set; } = TipoComprobante.Boleta;
    public string? RUC { get; set; }
    public string? RazonSocial { get; set; }
    public string? CorreoFacturacion { get; set; }
    public List<string> Acompanantes { get; set; } = new();
    public string? Observaciones { get; set; }
    public int UsuarioId { get; set; }
}

// ============================================================
// MÓDULO HOTEL — Servicio
// ============================================================

public interface IHabitacionService
{
    Task<List<HabitacionCardDto>> ObtenerPorPisoAsync(int piso);
    Task<List<HabitacionCardDto>> ObtenerTodasAsync();
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

        return await ConstruirTarjetasAsync(habitaciones);
    }

    public async Task<List<HabitacionCardDto>> ObtenerTodasAsync()
    {
        var habitaciones = await _context.Habitaciones
            .OrderBy(h => h.Piso)
            .ThenBy(h => h.Numero)
            .ToListAsync();

        return await ConstruirTarjetasAsync(habitaciones);
    }

    /// <summary>
    /// Arma la lista de tarjetas para mostrar en Recepción, cruzando en memoria con las
    /// estadías activas y sus acompañantes (nunca son más de 36 habitaciones, así que
    /// traer todo y cruzar en memoria es más simple y seguro que depender de que EF
    /// traduzca un join más complejo a SQL).
    /// </summary>
    private async Task<List<HabitacionCardDto>> ConstruirTarjetasAsync(List<Habitacion> habitaciones)
    {
        var estadiasActivas = await _context.Estadias
            .Where(e => e.Estado == EstadoEstadia.Activa)
            .Include(e => e.Acompanantes)
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
                TotalAcumulado = estadia?.TotalAcumulado,
                TipoDocumentoHuesped = estadia?.TipoDocumento,
                NumeroDocumentoHuesped = estadia?.NumeroDocumento,
                CelularHuesped = estadia?.Celular,
                FechaCheckInHuesped = estadia?.FechaCheckIn,
                AcompanantesHuesped = estadia?.Acompanantes.Select(a => a.NombreCompleto).ToList() ?? new List<string>()
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
            TipoDocumento = dto.TipoDocumento,
            NumeroDocumento = dto.NumeroDocumento,
            NombreCompleto = dto.NombreCompleto,
            Celular = dto.Celular,
            FechaCheckIn = DateTime.Now,
            Estado = EstadoEstadia.Activa,
            TipoComprobante = dto.TipoComprobante,
            RUC = dto.RUC,
            RazonSocial = dto.RazonSocial,
            CorreoFacturacion = dto.CorreoFacturacion,
            Observaciones = dto.Observaciones,
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
            $"Check-in de {dto.NombreCompleto} ({dto.TipoDocumento} {dto.NumeroDocumento}) en habitación {habitacion.Numero}.",
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
            // CheckInAsync ya cobró la 1ra noche (TotalAcumulado = TarifaNoche). Acá se
            // cobra solo la DIFERENCIA por noches adicionales reales — nunca se
            // recalcula todo desde cero, porque TotalAcumulado también acumula
            // consumos de Sauna/Cafetería cargados a la habitación (ver
            // RegistrarVentaAsync/RegistrarVentaHotelAsync) y esos ya están sumados
            // correctamente ahí. Antes de este fix, una estadía de varias noches se
            // cobraba como si fuera una sola.
            var noches = Math.Max(1, (estadia.FechaCheckOut.Value.Date - estadia.FechaCheckIn.Date).Days);
            var nochesAdicionales = noches - 1;
            if (nochesAdicionales > 0)
            {
                estadia.TotalAcumulado += nochesAdicionales * estadia.Habitacion.TarifaNoche;
            }

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
        EstadoHabitacion.Disponible => (Color)Microsoft.Maui.Controls.Application.Current!.Resources["ColorDisponible"],
        EstadoHabitacion.Ocupada => (Color)Microsoft.Maui.Controls.Application.Current!.Resources["ColorOcupada"],
        EstadoHabitacion.LimpiezaSalida => (Color)Microsoft.Maui.Controls.Application.Current!.Resources["ColorLimpieza"],
        EstadoHabitacion.Mantenimiento => (Color)Microsoft.Maui.Controls.Application.Current!.Resources["ColorMantenimiento"],
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

// ============================================================
// MÓDULO CLIENTES (vista unificada Hotel + Sauna) — DTOs + Servicio
// ============================================================

/// <summary>Una fila de la lista de Clientes: puede representar un huésped del hotel
/// o un cliente de sauna, con el mismo "molde" para poder mostrarlos juntos.</summary>
public class ClienteUnificadoDto
{
    public string Origen { get; set; } = string.Empty; // "Hotel" o "Sauna"
    public int? EstadiaId { get; set; }
    public int? ClienteSaunaId { get; set; }
    public string NombreCliente { get; set; } = string.Empty;
    public string UbicacionTexto { get; set; } = string.Empty; // "Hab. 305" o "Candado 04"
    public DateTime HoraRegistro { get; set; }
    public DateTime? HoraSalida { get; set; }
    public string EstadoTexto { get; set; } = string.Empty;
    public decimal Total { get; set; }

    public string HoraRegistroTexto => HoraRegistro.ToString("HH:mm");
    public string HoraSalidaTexto => HoraSalida.HasValue ? HoraSalida.Value.ToString("HH:mm") : "-";
    public string FechaTexto => HoraRegistro.ToString("dd/MM/yyyy");
}

public class ConsumoItemDto
{
    public string Descripcion { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal Subtotal { get; set; }
    public DateTime Hora { get; set; }

    public string HoraTexto => Hora.ToString("HH:mm");
}

/// <summary>El panel de detalle a la derecha, cuando se selecciona un cliente de la lista.</summary>
public class DetalleClienteDto
{
    public string Origen { get; set; } = string.Empty;
    public string NombreCliente { get; set; } = string.Empty;
    public string EtiquetaTipoDocumento { get; set; } = string.Empty;
    public string NumeroDocumento { get; set; } = string.Empty;
    public string? Celular { get; set; }
    public string UbicacionTexto { get; set; } = string.Empty;
    public DateTime HoraRegistro { get; set; }
    public string EstadoTexto { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public List<ConsumoItemDto> Consumos { get; set; } = new();

    /// <summary>Si es un cliente de sauna activo, sirve para poder ir directo a
    /// "Vender productos (POS)" desde acá y agregarle más consumo.</summary>
    public int? ClienteSaunaIdParaVenta { get; set; }
    public string? NombreParaVenta { get; set; }
    public bool EsHuespedHotelParaVenta { get; set; }

    /// <summary>Si es un huésped de hotel con estadía activa, permite cargar un
    /// consumo DIRECTO a su habitación desde acá (sin pasar por un registro de
    /// ClienteSauna) — mismo mecanismo que usa Cafetería. Ver SaunaVentaViewModel.EstadiaIdDirecta.</summary>
    public int? EstadiaIdParaVenta { get; set; }
    public int? NumeroHabitacionParaVenta { get; set; }

    /// <summary>True cuando la estadía/sesión sigue activa — habilita el botón "Pagar".</summary>
    public bool EstaActivo { get; set; }

    public bool PuedeAgregarConsumo => ClienteSaunaIdParaVenta.HasValue || EstadiaIdParaVenta.HasValue;
    public bool PuedePagar => EstaActivo;

    public string HoraRegistroTexto => HoraRegistro.ToString("dd/MM/yyyy HH:mm");
}

public interface IClientesService
{
    Task<List<ClienteUnificadoDto>> ObtenerClientesHotelAsync();
    Task<List<ClienteUnificadoDto>> ObtenerClientesSaunaAsync();
    Task<DetalleClienteDto?> ObtenerDetalleHotelAsync(int estadiaId);
    Task<DetalleClienteDto?> ObtenerDetalleSaunaAsync(int clienteSaunaId);
}

/// <summary>
/// Junta Estadias (hotel) y ClientesSauna en una sola vista tipo "Clientes", como
/// en el sistema de referencia. No reemplaza a HabitacionService/SaunaService (las
/// acciones de negocio siguen viviendo ahí) — esto es solo una vista de consulta.
/// </summary>
public class ClientesService : IClientesService
{
    private readonly AppDbContext _context;

    public ClientesService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<ClienteUnificadoDto>> ObtenerClientesHotelAsync()
    {
        var hoy = DateTime.Now.Date;

        var estadias = await _context.Estadias
            .Include(e => e.Habitacion)
            .Where(e => e.Estado == EstadoEstadia.Activa || e.FechaCheckIn.Date == hoy ||
                        (e.FechaCheckOut != null && e.FechaCheckOut.Value.Date == hoy))
            .OrderByDescending(e => e.FechaCheckIn)
            .ToListAsync();

        return estadias.Select(e => new ClienteUnificadoDto
        {
            Origen = "Hotel",
            EstadiaId = e.Id,
            NombreCliente = e.NombreCompleto,
            UbicacionTexto = e.Habitacion is not null ? $"Hab. {e.Habitacion.Numero}" : "-",
            HoraRegistro = e.FechaCheckIn,
            HoraSalida = e.FechaCheckOut,
            EstadoTexto = e.Estado == EstadoEstadia.Activa ? "OCUPADA" : "CHECK-OUT",
            Total = e.TotalAcumulado
        }).ToList();
    }

    public async Task<List<ClienteUnificadoDto>> ObtenerClientesSaunaAsync()
    {
        var hoy = DateTime.Now.Date;

        var clientes = await _context.ClientesSauna
            .Where(c => c.Estado == EstadoClienteSauna.Activo || c.FechaIngreso.Date == hoy)
            .OrderByDescending(c => c.FechaIngreso)
            .ToListAsync();

        var ids = clientes.Select(c => c.Id).ToList();
        var totalesPorCliente = await _context.VentasSauna
            .Where(v => v.ClienteSaunaId.HasValue && ids.Contains(v.ClienteSaunaId.Value) && v.Estado != EstadoVenta.Anulada)
            .GroupBy(v => v.ClienteSaunaId)
            .Select(g => new { ClienteSaunaId = g.Key, Total = g.Sum(v => v.Total) })
            .ToListAsync();

        return clientes.Select(c => new ClienteUnificadoDto
        {
            Origen = "Sauna",
            ClienteSaunaId = c.Id,
            NombreCliente = c.NombreCompleto,
            UbicacionTexto = $"Candado {c.NumeroCandado}",
            HoraRegistro = c.FechaIngreso,
            HoraSalida = c.FechaSalida,
            EstadoTexto = c.Estado == EstadoClienteSauna.Activo ? "ACTIVO" : "FINALIZADO",
            Total = totalesPorCliente.FirstOrDefault(t => t.ClienteSaunaId == c.Id)?.Total ?? 0m
        }).ToList();
    }

    public async Task<DetalleClienteDto?> ObtenerDetalleHotelAsync(int estadiaId)
    {
        var e = await _context.Estadias
            .Include(x => x.Habitacion)
            .FirstOrDefaultAsync(x => x.Id == estadiaId);

        if (e is null)
        {
            return null;
        }

        var etiquetaDoc = e.TipoDocumento switch
        {
            TipoDocumento.Pasaporte => "Pasaporte",
            TipoDocumento.CarneExtranjeria => "Carné Ext.",
            _ => "DNI"
        };

        var consumos = new List<ConsumoItemDto>
        {
            new ConsumoItemDto
            {
                Descripcion = $"Hospedaje — {e.Habitacion?.Tipo}",
                Cantidad = 1,
                Subtotal = e.Habitacion?.TarifaNoche ?? 0m,
                Hora = e.FechaCheckIn
            }
        };

        var cargosSauna = await _context.VentasSauna
            .Where(v => v.EstadiaHotelDestinoId == e.Id && v.Estado != EstadoVenta.Anulada)
            .Include(v => v.Detalles)
            .ToListAsync();

        foreach (var venta in cargosSauna)
        {
            foreach (var detalle in venta.Detalles)
            {
                consumos.Add(new ConsumoItemDto
                {
                    Descripcion = $"{detalle.Descripcion} (cargado a la habitación)",
                    Cantidad = detalle.Cantidad,
                    Subtotal = detalle.Subtotal,
                    Hora = venta.Fecha
                });
            }
        }

        var estaActivaHotel = e.Estado == EstadoEstadia.Activa;

        return new DetalleClienteDto
        {
            Origen = "Hotel",
            NombreCliente = e.NombreCompleto,
            EtiquetaTipoDocumento = etiquetaDoc,
            NumeroDocumento = e.NumeroDocumento,
            Celular = e.Celular,
            UbicacionTexto = e.Habitacion is not null ? $"Hab. {e.Habitacion.Numero}" : "-",
            HoraRegistro = e.FechaCheckIn,
            EstadoTexto = estaActivaHotel ? "OCUPADA" : "CHECK-OUT",
            Total = e.TotalAcumulado,
            Consumos = consumos.OrderBy(c => c.Hora).ToList(),
            EstadiaIdParaVenta = estaActivaHotel ? e.Id : null,
            NumeroHabitacionParaVenta = e.Habitacion?.Numero,
            NombreParaVenta = e.NombreCompleto,
            EstaActivo = estaActivaHotel
        };
    }

    public async Task<DetalleClienteDto?> ObtenerDetalleSaunaAsync(int clienteSaunaId)
    {
        var c = await _context.ClientesSauna.FirstOrDefaultAsync(x => x.Id == clienteSaunaId);
        if (c is null)
        {
            return null;
        }

        var etiquetaDoc = c.TipoDocumento switch
        {
            TipoDocumento.Pasaporte => "Pasaporte",
            TipoDocumento.CarneExtranjeria => "Carné Ext.",
            _ => "DNI"
        };

        var ventas = await _context.VentasSauna
            .Where(v => v.ClienteSaunaId == c.Id && v.Estado != EstadoVenta.Anulada)
            .Include(v => v.Detalles)
            .ToListAsync();

        var consumos = new List<ConsumoItemDto>();
        foreach (var venta in ventas)
        {
            foreach (var detalle in venta.Detalles)
            {
                consumos.Add(new ConsumoItemDto
                {
                    Descripcion = detalle.Descripcion,
                    Cantidad = detalle.Cantidad,
                    Subtotal = detalle.Subtotal,
                    Hora = venta.Fecha
                });
            }
        }

        var estaActivoSauna = c.Estado == EstadoClienteSauna.Activo;

        return new DetalleClienteDto
        {
            Origen = "Sauna",
            NombreCliente = c.NombreCompleto,
            EtiquetaTipoDocumento = etiquetaDoc,
            NumeroDocumento = c.NumeroDocumento,
            Celular = null,
            UbicacionTexto = $"Candado {c.NumeroCandado} — {c.Seccion}",
            HoraRegistro = c.FechaIngreso,
            EstadoTexto = estaActivoSauna ? "ACTIVO" : "FINALIZADO",
            Total = consumos.Sum(x => x.Subtotal),
            Consumos = consumos.OrderBy(x => x.Hora).ToList(),
            ClienteSaunaIdParaVenta = estaActivoSauna ? c.Id : null,
            NombreParaVenta = c.NombreCompleto,
            EsHuespedHotelParaVenta = c.EsHuespedHotel,
            EstaActivo = estaActivoSauna
        };
    }
}

// ============================================================
// MÓDULO GASTOS / CAJA CHICA — DTOs + Servicio
// ============================================================

public class MovimientoCajaCardDto
{
    public int Id { get; set; }
    public DateTime FechaHora { get; set; }
    public DireccionMovimiento Direccion { get; set; }
    public CategoriaMovimientoCaja Categoria { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public string? PersonalRelacionado { get; set; }
    public decimal Monto { get; set; }
    public OrigenCajaChica OrigenCaja { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;

    public string HoraTexto => FechaHora.ToString("HH:mm:ss");

    public string EtiquetaDireccion => Direccion == DireccionMovimiento.Ingreso ? "Ingreso de dinero" : "Salida de dinero";

    public string EtiquetaCategoria => Categoria switch
    {
        CategoriaMovimientoCaja.PagoPersonal => "Pago del Personal",
        CategoriaMovimientoCaja.GastosDiarios => "Gastos diarios",
        CategoriaMovimientoCaja.AjusteCaja => "Ajuste de Caja",
        CategoriaMovimientoCaja.ConsumoPersonal => "Consumo de Personal",
        _ => Categoria.ToString()
    };

    /// <summary>Ej: "(Salida de dinero / Pago del Personal)" — mismo estilo que pediste.</summary>
    public string TipoCompletoTexto => $"({EtiquetaDireccion} / {EtiquetaCategoria})";

    public string MontoTexto => Direccion == DireccionMovimiento.Salida ? $"- S/ {Monto:0.00}" : $"+ S/ {Monto:0.00}";
}

public class NuevoMovimientoCajaDto
{
    public DireccionMovimiento Direccion { get; set; }
    public CategoriaMovimientoCaja Categoria { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public string? PersonalRelacionado { get; set; }
    public decimal Monto { get; set; }
    public OrigenCajaChica OrigenCaja { get; set; }
    public int UsuarioId { get; set; }
}

/// <summary>El estado de la Caja Chica de cada área: el monto base que siempre
/// debería estar disponible, más/menos los movimientos del día.</summary>
public class CajaChicaResumenDto
{
    public decimal MontoBaseHotel { get; set; }
    public decimal MovimientosHotel { get; set; } // positivo = neto a favor, negativo = neto en contra
    public decimal MontoBaseSauna { get; set; }
    public decimal MovimientosSauna { get; set; }

    public decimal MontoEsperadoHotel => MontoBaseHotel + MovimientosHotel;
    public decimal MontoEsperadoSauna => MontoBaseSauna + MovimientosSauna;
}

public interface IGastosService
{
    /// <summary>Monto base fijo de Caja Chica del Hotel (S/ 250 según el documento maestro).</summary>
    decimal MontoBaseCajaChicaHotel { get; }

    /// <summary>Monto base fijo de Caja Chica del Sauna (S/ 150 según el documento maestro).</summary>
    decimal MontoBaseCajaChicaSauna { get; }

    Task<List<MovimientoCajaCardDto>> ObtenerDelDiaAsync(DateTime fecha);
    Task<CajaChicaResumenDto> ObtenerResumenCajaChicaAsync(DateTime fecha);
    Task<int> RegistrarMovimientoAsync(NuevoMovimientoCajaDto dto);
    Task EliminarMovimientoAsync(int movimientoId, int usuarioId);
}

public class GastosService : IGastosService
{
    private readonly AppDbContext _context;
    private readonly IAuditoriaService _auditoriaService;

    public decimal MontoBaseCajaChicaHotel => 250m;
    public decimal MontoBaseCajaChicaSauna => 150m;

    public GastosService(AppDbContext context, IAuditoriaService auditoriaService)
    {
        _context = context;
        _auditoriaService = auditoriaService;
    }

    public async Task<List<MovimientoCajaCardDto>> ObtenerDelDiaAsync(DateTime fecha)
    {
        var dia = fecha.Date;

        var movimientos = await _context.MovimientosCaja
            .Where(m => m.FechaHora.Date == dia)
            .OrderByDescending(m => m.FechaHora)
            .ToListAsync();

        var usuarioIds = movimientos.Select(m => m.UsuarioId).Distinct().ToList();
        var usuarios = await _context.Usuarios
            .Where(u => usuarioIds.Contains(u.Id))
            .ToListAsync();

        return movimientos.Select(m => new MovimientoCajaCardDto
        {
            Id = m.Id,
            FechaHora = m.FechaHora,
            Direccion = m.Direccion,
            Categoria = m.Categoria,
            Descripcion = m.Descripcion,
            PersonalRelacionado = m.PersonalRelacionado,
            Monto = m.Monto,
            OrigenCaja = m.OrigenCaja,
            UsuarioNombre = usuarios.FirstOrDefault(u => u.Id == m.UsuarioId)?.NombreCompleto ?? "—"
        }).ToList();
    }

    public async Task<CajaChicaResumenDto> ObtenerResumenCajaChicaAsync(DateTime fecha)
    {
        var dia = fecha.Date;

        var movimientos = await _context.MovimientosCaja
            .Where(m => m.FechaHora.Date == dia)
            .ToListAsync();

        decimal Neto(OrigenCajaChica origen) => movimientos
            .Where(m => m.OrigenCaja == origen)
            .Sum(m => m.Direccion == DireccionMovimiento.Ingreso ? m.Monto : -m.Monto);

        return new CajaChicaResumenDto
        {
            MontoBaseHotel = MontoBaseCajaChicaHotel,
            MovimientosHotel = Neto(OrigenCajaChica.Hotel),
            MontoBaseSauna = MontoBaseCajaChicaSauna,
            MovimientosSauna = Neto(OrigenCajaChica.Sauna)
        };
    }

    public async Task<int> RegistrarMovimientoAsync(NuevoMovimientoCajaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Descripcion))
        {
            throw new InvalidOperationException("La descripción es obligatoria.");
        }

        if (dto.Monto <= 0)
        {
            throw new InvalidOperationException("El monto debe ser mayor a cero.");
        }

        var movimiento = new MovimientoCaja
        {
            FechaHora = DateTime.Now,
            Direccion = dto.Direccion,
            Categoria = dto.Categoria,
            Descripcion = dto.Descripcion,
            PersonalRelacionado = dto.PersonalRelacionado,
            Monto = dto.Monto,
            OrigenCaja = dto.OrigenCaja,
            UsuarioId = dto.UsuarioId
        };

        _context.MovimientosCaja.Add(movimiento);
        await _context.SaveChangesAsync();

        var direccionTexto = dto.Direccion == DireccionMovimiento.Ingreso ? "Ingreso" : "Salida";
        await _auditoriaService.RegistrarAsync(
            "MOVIMIENTO_CAJA",
            $"{direccionTexto} de caja ({dto.OrigenCaja}) — {dto.Descripcion}: S/ {dto.Monto:0.00}. Hora: {movimiento.FechaHora:dd/MM/yyyy HH:mm:ss}.",
            dto.UsuarioId, "MovimientoCaja", movimiento.Id);

        return movimiento.Id;
    }

    public async Task EliminarMovimientoAsync(int movimientoId, int usuarioId)
    {
        var movimiento = await _context.MovimientosCaja.FirstOrDefaultAsync(m => m.Id == movimientoId);
        if (movimiento is null)
        {
            throw new InvalidOperationException("El movimiento no existe.");
        }

        _context.MovimientosCaja.Remove(movimiento);
        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync(
            "MOVIMIENTO_CAJA_ELIMINADO",
            $"Se eliminó el movimiento \"{movimiento.Descripcion}\" (S/ {movimiento.Monto:0.00}).",
            usuarioId, "MovimientoCaja", movimientoId);
    }
}

// ============================================================
// MÓDULO RESERVAS — DTOs + Servicio
// ============================================================

/// <summary>Habitación libre para reservar en un rango de fechas (distinto de
/// HabitacionCardDto, que describe el estado de HOY, no de una fecha futura).</summary>
public class HabitacionDisponibleDto
{
    public int HabitacionId { get; set; }
    public int Numero { get; set; }
    public int Piso { get; set; }
    public TipoHabitacion Tipo { get; set; }
    public decimal TarifaNoche { get; set; }
    public string EtiquetaTipo => Tipo.ToString();
}

public class ReservaCardDto
{
    public int ReservaId { get; set; }
    public int HabitacionId { get; set; }
    public int NumeroHabitacion { get; set; }
    public string EtiquetaTipoHabitacion { get; set; } = string.Empty;
    public string NombreCliente { get; set; } = string.Empty;
    public TipoDocumento TipoDocumento { get; set; }
    public string NumeroDocumento { get; set; } = string.Empty;
    public string Celular { get; set; } = string.Empty;
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public string? Observaciones { get; set; }
    public EstadoReserva Estado { get; set; }
    public DateTime FechaCreacion { get; set; }

    public string EtiquetaTipoDocumento => TipoDocumento switch
    {
        TipoDocumento.Pasaporte => "Pasaporte",
        TipoDocumento.CarneExtranjeria => "Carné Ext.",
        _ => "DNI"
    };

    public string RangoFechasTexto => $"{FechaInicio:dd/MM/yyyy} — {FechaFin:dd/MM/yyyy}";

    public int Noches => Math.Max(1, (FechaFin - FechaInicio).Days);

    public string EtiquetaEstado => Estado switch
    {
        EstadoReserva.Confirmada => "Confirmada",
        EstadoReserva.CheckInRealizado => "Check-in realizado",
        EstadoReserva.Cancelada => "Cancelada",
        _ => Estado.ToString()
    };

    public string FechaCreacionTexto => FechaCreacion.ToString("dd/MM/yyyy HH:mm");

    public bool PuedeConvertirACheckIn => Estado == EstadoReserva.Confirmada && FechaInicio.Date <= DateTime.Now.Date;
    public bool PuedeCancelar => Estado == EstadoReserva.Confirmada;
}

public class NuevaReservaDto
{
    public int HabitacionId { get; set; }
    public TipoDocumento TipoDocumento { get; set; } = TipoDocumento.DNI;
    public string NumeroDocumento { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string Celular { get; set; } = string.Empty;
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public string? Observaciones { get; set; }
    public TipoComprobante TipoComprobante { get; set; } = TipoComprobante.Boleta;
    public string? RUC { get; set; }
    public string? RazonSocial { get; set; }
    public string? CorreoFacturacion { get; set; }
    public List<string> Acompanantes { get; set; } = new();
    public int UsuarioId { get; set; }
}

public interface IReservaService
{
    Task<List<ReservaCardDto>> ObtenerProximasAsync();
    Task<List<HabitacionDisponibleDto>> ObtenerHabitacionesDisponiblesAsync(DateTime fechaInicio, DateTime fechaFin);
    Task<int> CrearReservaAsync(NuevaReservaDto dto);
    Task CancelarReservaAsync(int reservaId, int usuarioId);
    Task ConvertirEnCheckInAsync(int reservaId, int usuarioId);
}

/// <summary>
/// Reservas a futuro. Usa AppDbContext directamente (no un Repositorio intermedio),
/// mismo motivo que HabitacionService: para que Reserva + Estadia + Habitacion se
/// guarden en una sola operación cuando una reserva se convierte en Check-in.
/// </summary>
public class ReservaService : IReservaService
{
    private readonly AppDbContext _context;
    private readonly IAuditoriaService _auditoriaService;

    public ReservaService(AppDbContext context, IAuditoriaService auditoriaService)
    {
        _context = context;
        _auditoriaService = auditoriaService;
    }

    public async Task<List<ReservaCardDto>> ObtenerProximasAsync()
    {
        var reservas = await _context.Reservas
            .Include(r => r.Habitacion)
            .Where(r => r.Estado != EstadoReserva.Cancelada)
            .OrderBy(r => r.FechaInicio)
            .ToListAsync();

        return reservas.Select(r => new ReservaCardDto
        {
            ReservaId = r.Id,
            HabitacionId = r.HabitacionId,
            NumeroHabitacion = r.Habitacion?.Numero ?? 0,
            EtiquetaTipoHabitacion = r.Habitacion?.Tipo.ToString() ?? string.Empty,
            NombreCliente = r.NombreCompleto,
            TipoDocumento = r.TipoDocumento,
            NumeroDocumento = r.NumeroDocumento,
            Celular = r.Celular,
            FechaInicio = r.FechaInicio,
            FechaFin = r.FechaFin,
            Observaciones = r.Observaciones,
            Estado = r.Estado,
            FechaCreacion = r.FechaCreacion
        }).ToList();
    }

    public async Task<List<HabitacionDisponibleDto>> ObtenerHabitacionesDisponiblesAsync(DateTime fechaInicio, DateTime fechaFin)
    {
        var todas = await _context.Habitaciones
            .OrderBy(h => h.Piso).ThenBy(h => h.Numero)
            .ToListAsync();

        // Una habitación NO está disponible en el rango pedido si hay alguna reserva
        // Confirmada de esa habitación cuyo rango se cruza con el pedido.
        var idsOcupadosEnRango = await _context.Reservas
            .Where(r => r.Estado == EstadoReserva.Confirmada && r.FechaInicio < fechaFin && fechaInicio < r.FechaFin)
            .Select(r => r.HabitacionId)
            .ToListAsync();

        return todas
            .Where(h => !idsOcupadosEnRango.Contains(h.Id))
            .Select(h => new HabitacionDisponibleDto
            {
                HabitacionId = h.Id,
                Numero = h.Numero,
                Piso = h.Piso,
                Tipo = h.Tipo,
                TarifaNoche = h.TarifaNoche
            })
            .ToList();
    }

    public async Task<int> CrearReservaAsync(NuevaReservaDto dto)
    {
        var fechaInicio = dto.FechaInicio.Date;
        var fechaFin = dto.FechaFin.Date;

        if (fechaFin <= fechaInicio)
        {
            throw new InvalidOperationException("La fecha de salida debe ser posterior a la fecha de entrada.");
        }

        if (fechaInicio < DateTime.Now.Date)
        {
            throw new InvalidOperationException("No se puede reservar para una fecha que ya pasó.");
        }

        var hayConflicto = await _context.Reservas.AnyAsync(r =>
            r.HabitacionId == dto.HabitacionId &&
            r.Estado == EstadoReserva.Confirmada &&
            r.FechaInicio < fechaFin &&
            fechaInicio < r.FechaFin);

        if (hayConflicto)
        {
            throw new InvalidOperationException("Esa habitación ya tiene una reserva confirmada que se cruza con esas fechas.");
        }

        var habitacion = await _context.Habitaciones.FirstOrDefaultAsync(h => h.Id == dto.HabitacionId);
        if (habitacion is null)
        {
            throw new InvalidOperationException("La habitación elegida no existe.");
        }

        var reserva = new Reserva
        {
            HabitacionId = dto.HabitacionId,
            TipoDocumento = dto.TipoDocumento,
            NumeroDocumento = dto.NumeroDocumento,
            NombreCompleto = dto.NombreCompleto,
            Celular = dto.Celular,
            FechaInicio = fechaInicio,
            FechaFin = fechaFin,
            Observaciones = dto.Observaciones,
            Estado = EstadoReserva.Confirmada,
            FechaCreacion = DateTime.Now,
            UsuarioCreacionId = dto.UsuarioId,
            TipoComprobante = dto.TipoComprobante,
            RUC = dto.RUC,
            RazonSocial = dto.RazonSocial,
            CorreoFacturacion = dto.CorreoFacturacion
        };

        reserva.Acompanantes = dto.Acompanantes
            .Select(nombre => new AcompananteReserva { NombreCompleto = nombre })
            .ToList();

        _context.Reservas.Add(reserva);
        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync(
            "RESERVA_CREADA",
            $"Reserva de {dto.NombreCompleto} para la habitación {habitacion.Numero}, del {fechaInicio:dd/MM/yyyy} al {fechaFin:dd/MM/yyyy}. Registrada el {reserva.FechaCreacion:dd/MM/yyyy HH:mm}.",
            dto.UsuarioId, "Reserva", reserva.Id);

        return reserva.Id;
    }

    public async Task CancelarReservaAsync(int reservaId, int usuarioId)
    {
        var reserva = await _context.Reservas.Include(r => r.Habitacion).FirstOrDefaultAsync(r => r.Id == reservaId);
        if (reserva is null)
        {
            throw new InvalidOperationException("La reserva no existe.");
        }

        reserva.Estado = EstadoReserva.Cancelada;
        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync(
            "RESERVA_CANCELADA",
            $"Se canceló la reserva de {reserva.NombreCompleto} (habitación {reserva.Habitacion?.Numero}, {reserva.FechaInicio:dd/MM/yyyy} al {reserva.FechaFin:dd/MM/yyyy}).",
            usuarioId, "Reserva", reserva.Id);
    }

    public async Task ConvertirEnCheckInAsync(int reservaId, int usuarioId)
    {
        var reserva = await _context.Reservas.Include(r => r.Acompanantes).FirstOrDefaultAsync(r => r.Id == reservaId);
        if (reserva is null)
        {
            throw new InvalidOperationException("La reserva no existe.");
        }

        if (reserva.Estado != EstadoReserva.Confirmada)
        {
            throw new InvalidOperationException("Esta reserva ya no está confirmada.");
        }

        var habitacion = await _context.Habitaciones.FirstOrDefaultAsync(h => h.Id == reserva.HabitacionId);
        if (habitacion is null)
        {
            throw new InvalidOperationException("La habitación de esta reserva ya no existe.");
        }

        if (habitacion.Estado != EstadoHabitacion.Disponible)
        {
            throw new InvalidOperationException(
                $"La habitación {habitacion.Numero} no está Disponible en este momento (estado actual: {habitacion.Estado}). Resuelve eso primero desde Recepción.");
        }

        var estadia = new Estadia
        {
            HabitacionId = habitacion.Id,
            TipoDocumento = reserva.TipoDocumento,
            NumeroDocumento = reserva.NumeroDocumento,
            NombreCompleto = reserva.NombreCompleto,
            Celular = reserva.Celular,
            FechaCheckIn = DateTime.Now,
            Estado = EstadoEstadia.Activa,
            TipoComprobante = reserva.TipoComprobante,
            RUC = reserva.RUC,
            RazonSocial = reserva.RazonSocial,
            CorreoFacturacion = reserva.CorreoFacturacion,
            AccesoSaunaIncluido = true,
            TotalAcumulado = habitacion.TarifaNoche,
            UsuarioCheckInId = usuarioId,
            Acompanantes = reserva.Acompanantes
                .Select(a => new Acompanante { NombreCompleto = a.NombreCompleto })
                .ToList()
        };

        habitacion.Estado = EstadoHabitacion.Ocupada;
        reserva.Estado = EstadoReserva.CheckInRealizado;
        _context.Estadias.Add(estadia);

        // Transacción explícita: hacen falta DOS SaveChangesAsync (el primero genera
        // el Id de la Estadia nueva, que recién ahí se puede guardar en
        // reserva.EstadiaId). Sin envolver ambos en la misma transacción, una falla
        // justo entre los dos dejaría el check-in hecho pero la Reserva sin el
        // vínculo a su Estadia — huérfana para siempre.
        await using (var transaccion = await _context.Database.BeginTransactionAsync())
        {
            await _context.SaveChangesAsync();

            reserva.EstadiaId = estadia.Id;
            await _context.SaveChangesAsync();

            await transaccion.CommitAsync();
        }

        await _auditoriaService.RegistrarAsync(
            "CHECK_IN",
            $"Check-in de {reserva.NombreCompleto} a partir de una reserva, en habitación {habitacion.Numero}. Hora de registro: {DateTime.Now:dd/MM/yyyy HH:mm}.",
            usuarioId, "Estadia", estadia.Id);
    }
}

// ============================================================
// MÓDULO CALENDARIO DE HABITACIONES — DTOs + Servicio
// ============================================================

public enum EstadoCeldaCalendario
{
    Disponible,
    Ocupada,
    Reservada,
    Mantenimiento
}

public class CeldaCalendarioDto
{
    public int Dia { get; set; }
    public EstadoCeldaCalendario Estado { get; set; }
    public string? NombreCliente { get; set; }

    public bool EsHoy { get; set; }
}

public class ColumnaHabitacionCalendarioDto
{
    public int HabitacionId { get; set; }
    public int Numero { get; set; }
    public int Piso { get; set; }
    public TipoHabitacion Tipo { get; set; }

    /// <summary>Estado actual (de hoy) de la habitación — no es por día, es el mismo
    /// dato que usa Registro Hotel. Sirve para el filtro de estado del Calendario.</summary>
    public EstadoHabitacion EstadoActual { get; set; }
    public List<CeldaCalendarioDto> Celdas { get; set; } = new();

    public string EtiquetaTipo => Tipo.ToString();
}

public class CalendarioMensualDto
{
    public int Anio { get; set; }
    public int Mes { get; set; }
    public string NombreMesTexto { get; set; } = string.Empty;
    public List<int> Dias { get; set; } = new();
    public List<ColumnaHabitacionCalendarioDto> Columnas { get; set; } = new();
}

public interface ICalendarioService
{
    Task<CalendarioMensualDto> ObtenerCalendarioMensualAsync(int anio, int mes);
}

/// <summary>
/// Arma la grilla del calendario (habitaciones × días del mes). Cruza en memoria las
/// Estadias (ocupación real) y las Reservas confirmadas (ocupación futura prometida)
/// contra cada día — no hace una consulta por celda, trae todo una sola vez y calcula
/// en memoria, para que 36 habitaciones × ~31 días no golpeen la base de datos 1000+
/// veces.
///
/// LIMITACIÓN CONOCIDA: una Estadia activa (sin Check-out todavía) no tiene una fecha
/// de salida "esperada" en el sistema — solo sabemos cuándo entró. Por eso, para los
/// días FUTUROS de una estadía todavía activa, esta grilla no puede saber si van a
/// seguir ocupando la habitación o no, y simplemente no la marca como ocupada más
/// allá de hoy. Esto es honesto: es mejor no mostrar un dato que no tenemos, a
/// inventar una fecha de salida que no existe.
/// </summary>
public class CalendarioService : ICalendarioService
{
    private readonly AppDbContext _context;

    private static readonly string[] Meses =
        { "", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio",
          "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };

    public CalendarioService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CalendarioMensualDto> ObtenerCalendarioMensualAsync(int anio, int mes)
    {
        var primerDia = new DateTime(anio, mes, 1);
        var cantidadDias = DateTime.DaysInMonth(anio, mes);
        var ultimoDia = new DateTime(anio, mes, cantidadDias);
        var hoy = DateTime.Now.Date;

        var habitaciones = await _context.Habitaciones
            .OrderBy(h => h.Piso).ThenBy(h => h.Numero)
            .ToListAsync();

        // Estadias que se cruzan con el mes visible (activas o con checkout dentro del mes,
        // o que empezaron dentro del mes).
        var estadias = await _context.Estadias
            .Where(e => e.FechaCheckIn.Date <= ultimoDia &&
                        (e.Estado == EstadoEstadia.Activa ||
                         (e.FechaCheckOut != null && e.FechaCheckOut.Value.Date >= primerDia)))
            .ToListAsync();

        // Reservas Confirmadas que se cruzan con el mes visible.
        var reservas = await _context.Reservas
            .Where(r => r.Estado == EstadoReserva.Confirmada && r.FechaInicio <= ultimoDia && r.FechaFin >= primerDia)
            .ToListAsync();

        var columnas = new List<ColumnaHabitacionCalendarioDto>();

        foreach (var habitacion in habitaciones)
        {
            var columna = new ColumnaHabitacionCalendarioDto
            {
                HabitacionId = habitacion.Id,
                Numero = habitacion.Numero,
                Piso = habitacion.Piso,
                Tipo = habitacion.Tipo,
                EstadoActual = habitacion.Estado
            };

            var estadiasHabitacion = estadias.Where(e => e.HabitacionId == habitacion.Id).ToList();
            var reservasHabitacion = reservas.Where(r => r.HabitacionId == habitacion.Id).ToList();

            for (var dia = 1; dia <= cantidadDias; dia++)
            {
                var fecha = new DateTime(anio, mes, dia);
                var esHoy = fecha == hoy;

                // 1) Mantenimiento: solo se puede saber para HOY (es el único estado
                //    "actual" que tenemos, no hay historial de mantenimiento por fecha).
                if (esHoy && habitacion.Estado == EstadoHabitacion.Mantenimiento)
                {
                    columna.Celdas.Add(new CeldaCalendarioDto { Dia = dia, Estado = EstadoCeldaCalendario.Mantenimiento, EsHoy = true });
                    continue;
                }

                // 2) Ocupada: una Estadia real cubre este día.
                //    - Si la Estadia ya hizo Check-out, el rango es [CheckIn, CheckOut].
                //    - Si sigue Activa, el rango es [CheckIn, HOY] — no sabemos más allá.
                var estadiaDelDia = estadiasHabitacion.FirstOrDefault(e =>
                {
                    var inicio = e.FechaCheckIn.Date;
                    var fin = e.FechaCheckOut?.Date ?? (e.Estado == EstadoEstadia.Activa ? hoy : inicio);
                    return fecha >= inicio && fecha <= fin;
                });

                if (estadiaDelDia is not null)
                {
                    columna.Celdas.Add(new CeldaCalendarioDto
                    {
                        Dia = dia,
                        Estado = EstadoCeldaCalendario.Ocupada,
                        NombreCliente = estadiaDelDia.NombreCompleto,
                        EsHoy = esHoy
                    });
                    continue;
                }

                // 3) Reservada: una Reserva confirmada cubre este día (y no hay ocupación real).
                var reservaDelDia = reservasHabitacion.FirstOrDefault(r => fecha >= r.FechaInicio.Date && fecha < r.FechaFin.Date);
                if (reservaDelDia is not null)
                {
                    columna.Celdas.Add(new CeldaCalendarioDto
                    {
                        Dia = dia,
                        Estado = EstadoCeldaCalendario.Reservada,
                        NombreCliente = reservaDelDia.NombreCompleto,
                        EsHoy = esHoy
                    });
                    continue;
                }

                // 4) Nada de lo anterior: Disponible.
                columna.Celdas.Add(new CeldaCalendarioDto { Dia = dia, Estado = EstadoCeldaCalendario.Disponible, EsHoy = esHoy });
            }

            columnas.Add(columna);
        }

        return new CalendarioMensualDto
        {
            Anio = anio,
            Mes = mes,
            NombreMesTexto = $"{Meses[mes]} {anio}",
            Dias = Enumerable.Range(1, cantidadDias).ToList(),
            Columnas = columnas
        };
    }
}

// ============================================================
// MÓDULO SAUNA + POS — DTOs
// ============================================================

public class ClienteSaunaCardDto
{
    public int ClienteSaunaId { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string NumeroCandado { get; set; } = string.Empty;
    public SeccionSauna Seccion { get; set; }
    public bool EsHuespedHotel { get; set; }
    public decimal TotalConsumo { get; set; }
    public DateTime FechaIngreso { get; set; }

    public string EtiquetaSeccion => Seccion == SeccionSauna.Damas ? "Damas" : "General";
}

public class NuevoClienteSaunaDto
{
    public TipoDocumento TipoDocumento { get; set; } = TipoDocumento.DNI;
    public string NumeroDocumento { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string NumeroCandado { get; set; } = string.Empty;
    public SeccionSauna Seccion { get; set; }
    public string? Observacion { get; set; }
    public bool EsHuespedHotel { get; set; }
    public int? EstadiaHotelId { get; set; }
}

public class ProductoCatalogoDto
{
    public int ProductoId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public CategoriaProducto Categoria { get; set; }
    public string Icono { get; set; } = "🛒";
    public bool EsAlquilerVenta { get; set; }
    public decimal PrecioAlquiler { get; set; }
    public decimal PrecioVenta { get; set; }

    public string PrecioTexto => EsAlquilerVenta
        ? $"S/ {PrecioAlquiler:0.00} / {PrecioVenta:0.00}"
        : $"S/ {Precio:0.00}";
}

public class ItemCarritoDto
{
    public int? ProductoId { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal => Cantidad * PrecioUnitario;
}

// ============================================================
// MÓDULO SAUNA + POS — Servicio
// ============================================================

// ============================================================
// CIERRE DE CAJA — DTOs + Servicio
// ============================================================

public class ResumenCierreCajaDto
{
    public decimal TotalHotel { get; set; }
    public decimal TotalSauna { get; set; }
    public decimal TotalGeneral => TotalHotel + TotalSauna;
    public int ClientesSaunaAbiertos { get; set; }
    public List<string> NombresClientesAbiertos { get; set; } = new();
    public bool PuedeCerrar => ClientesSaunaAbiertos == 0;

    // Caja Chica del día (ver módulo de Gastos) — separada por Hotel y Sauna.
    public decimal MontoBaseCajaChicaHotel { get; set; }
    public decimal MovimientosCajaChicaHotel { get; set; }
    public decimal MontoBaseCajaChicaSauna { get; set; }
    public decimal MovimientosCajaChicaSauna { get; set; }

    public decimal MontoEsperadoCajaChicaHotel => MontoBaseCajaChicaHotel + MovimientosCajaChicaHotel;
    public decimal MontoEsperadoCajaChicaSauna => MontoBaseCajaChicaSauna + MovimientosCajaChicaSauna;
}

public interface ICierreCajaService
{
    Task<ResumenCierreCajaDto> ObtenerResumenDelDiaAsync();
    Task CerrarCajaAsync(TurnoCaja turno, int usuarioId);
}

/// <summary>
/// Cierre de caja del día. Regla de negocio clave: NO se puede cerrar si hay algún
/// cliente de Sauna con sesión todavía abierta (Estado == Activo) el día de hoy.
/// </summary>
public class CierreCajaService : ICierreCajaService
{
    private readonly AppDbContext _context;
    private readonly IAuditoriaService _auditoriaService;
    private readonly IGastosService _gastosService;

    public CierreCajaService(AppDbContext context, IAuditoriaService auditoriaService, IGastosService gastosService)
    {
        _context = context;
        _auditoriaService = auditoriaService;
        _gastosService = gastosService;
    }

    public async Task<ResumenCierreCajaDto> ObtenerResumenDelDiaAsync()
    {
        var hoy = DateTime.Now.Date;

        var estadiasHoy = await _context.Estadias
            .Where(e => e.FechaCheckIn.Date == hoy || (e.FechaCheckOut != null && e.FechaCheckOut.Value.Date == hoy))
            .ToListAsync();

        var ventasHoy = await _context.VentasSauna
            .Where(v => v.Fecha.Date == hoy && v.Estado != EstadoVenta.Anulada)
            .ToListAsync();

        var clientesAbiertosHoy = await _context.ClientesSauna
            .Where(c => c.Estado == EstadoClienteSauna.Activo && c.FechaIngreso.Date == hoy)
            .ToListAsync();

        var cajaChica = await _gastosService.ObtenerResumenCajaChicaAsync(hoy);

        return new ResumenCierreCajaDto
        {
            TotalHotel = estadiasHoy.Sum(e => e.TotalAcumulado),
            TotalSauna = ventasHoy.Sum(v => v.Total),
            ClientesSaunaAbiertos = clientesAbiertosHoy.Count,
            NombresClientesAbiertos = clientesAbiertosHoy.Select(c => c.NombreCompleto).ToList(),
            MontoBaseCajaChicaHotel = cajaChica.MontoBaseHotel,
            MovimientosCajaChicaHotel = cajaChica.MovimientosHotel,
            MontoBaseCajaChicaSauna = cajaChica.MontoBaseSauna,
            MovimientosCajaChicaSauna = cajaChica.MovimientosSauna
        };
    }

    public async Task CerrarCajaAsync(TurnoCaja turno, int usuarioId)
    {
        var resumen = await ObtenerResumenDelDiaAsync();

        if (!resumen.PuedeCerrar)
        {
            throw new InvalidOperationException(
                $"No se puede cerrar caja: hay {resumen.ClientesSaunaAbiertos} cliente(s) de Sauna con sesión abierta. Finalízalas primero.");
        }

        var cierre = new CierreCaja
        {
            Fecha = DateTime.Now.Date,
            Turno = turno,
            TotalHotel = resumen.TotalHotel,
            TotalSauna = resumen.TotalSauna,
            FechaCierre = DateTime.Now,
            UsuarioId = usuarioId
        };

        _context.CierresCaja.Add(cierre);
        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync(
            "CIERRE_CAJA",
            $"Cierre de caja ({turno}). Total Hotel: S/ {resumen.TotalHotel:0.00}, Total Sauna: S/ {resumen.TotalSauna:0.00}.",
            usuarioId, "CierreCaja", cierre.Id);
    }
}

// ============================================================
// MÓDULO ALMACÉN / INVENTARIO — DTOs + Servicio
// ============================================================

public class InsumoCardDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public CategoriaInsumo Categoria { get; set; }
    public string UnidadMedida { get; set; } = string.Empty;
    public int StockActual { get; set; }
    public int StockMinimo { get; set; }

    public string EtiquetaCategoria => Categoria switch
    {
        CategoriaInsumo.HotelHabitaciones => "Hotel — Habitaciones",
        CategoriaInsumo.HotelCocina => "Hotel — Cocina",
        CategoriaInsumo.Sauna => "Sauna",
        _ => Categoria.ToString()
    };

    /// <summary>True cuando el stock actual ya llegó (o bajó) del mínimo — dispara el
    /// resaltado visual de alerta en la tarjeta.</summary>
    public bool StockBajo => StockActual <= StockMinimo;
}

public class NuevoMovimientoInventarioDto
{
    public int InsumoId { get; set; }
    public TipoMovimientoInventario Tipo { get; set; }
    public int Cantidad { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public int UsuarioId { get; set; }
}

public interface IInventarioService
{
    Task<List<InsumoCardDto>> ObtenerInsumosAsync();
    Task RegistrarMovimientoAsync(NuevoMovimientoInventarioDto dto);
}

/// <summary>
/// Stock de blancos de habitación, insumos de cocina e insumos de sauna. Usa
/// AppDbContext directamente (mismo motivo que HabitacionService/SaunaService): el
/// movimiento y la actualización del stock del Insumo se guardan juntos, en una sola
/// operación atómica.
/// </summary>
public class InventarioService : IInventarioService
{
    private readonly AppDbContext _context;
    private readonly IAuditoriaService _auditoriaService;

    public InventarioService(AppDbContext context, IAuditoriaService auditoriaService)
    {
        _context = context;
        _auditoriaService = auditoriaService;
    }

    public async Task<List<InsumoCardDto>> ObtenerInsumosAsync()
    {
        var insumos = await _context.Insumos
            .Where(i => i.Activo)
            .OrderBy(i => i.Categoria)
            .ThenBy(i => i.Nombre)
            .ToListAsync();

        return insumos.Select(i => new InsumoCardDto
        {
            Id = i.Id,
            Nombre = i.Nombre,
            Categoria = i.Categoria,
            UnidadMedida = i.UnidadMedida,
            StockActual = i.StockActual,
            StockMinimo = i.StockMinimo
        }).ToList();
    }

    public async Task RegistrarMovimientoAsync(NuevoMovimientoInventarioDto dto)
    {
        if (dto.Cantidad <= 0)
        {
            throw new InvalidOperationException("La cantidad debe ser mayor a cero.");
        }

        var insumo = await _context.Insumos.FirstOrDefaultAsync(i => i.Id == dto.InsumoId);
        if (insumo is null)
        {
            throw new InvalidOperationException("El insumo no existe.");
        }

        if (dto.Tipo == TipoMovimientoInventario.Salida && insumo.StockActual < dto.Cantidad)
        {
            throw new InvalidOperationException(
                $"No hay stock suficiente de {insumo.Nombre} (disponible: {insumo.StockActual} {insumo.UnidadMedida}).");
        }

        insumo.StockActual += dto.Tipo == TipoMovimientoInventario.Entrada ? dto.Cantidad : -dto.Cantidad;

        _context.MovimientosInventario.Add(new MovimientoInventario
        {
            InsumoId = dto.InsumoId,
            Tipo = dto.Tipo,
            Cantidad = dto.Cantidad,
            Motivo = dto.Motivo,
            FechaHora = DateTime.Now,
            UsuarioId = dto.UsuarioId
        });

        await _context.SaveChangesAsync();

        var etiquetaTipo = dto.Tipo == TipoMovimientoInventario.Entrada ? "Ingreso" : "Salida";
        await _auditoriaService.RegistrarAsync(
            dto.Tipo == TipoMovimientoInventario.Entrada ? "INVENTARIO_ENTRADA" : "INVENTARIO_SALIDA",
            $"{etiquetaTipo} de {dto.Cantidad} {insumo.UnidadMedida} de {insumo.Nombre}. Motivo: {dto.Motivo}. Stock resultante: {insumo.StockActual} {insumo.UnidadMedida}.",
            dto.UsuarioId, "Insumo", insumo.Id);
    }
}

public interface ISaunaService
{
    Task<List<ProductoCatalogoDto>> ObtenerCatalogoAsync();
    Task<List<ClienteSaunaCardDto>> ObtenerClientesActivosAsync();
    Task<int> RegistrarClienteAsync(NuevoClienteSaunaDto dto, int usuarioId);
    Task<List<HabitacionCardDto>> BuscarHuespedesActivosAsync();
    Task RegistrarVentaAsync(int clienteSaunaId, List<ItemCarritoDto> items, int usuarioId, bool cargarAHabitacion);

    /// <summary>Venta de Cafetería/servicios DIRECTA a un huésped de hotel, sin pasar
    /// por un registro de ClienteSauna — para el caso de un huésped que solo quiere
    /// un café o un servicio adicional, sin haber ido al Sauna. Ver CafeteriaPage.</summary>
    Task RegistrarVentaHotelAsync(int estadiaId, List<ItemCarritoDto> items, int usuarioId, bool cargarAHabitacion);

    Task FinalizarSesionAsync(int clienteSaunaId, int usuarioId);
}

/// <summary>
/// Registro de clientes del Sauna y ventas del POS (toallas, cafetería, etc.).
/// Igual que HabitacionService, usa AppDbContext directamente para que las
/// operaciones que tocan varias tablas (venta + detalles) se guarden juntas.
/// </summary>
public class SaunaService : ISaunaService
{
    private readonly AppDbContext _context;
    private readonly IAuditoriaService _auditoriaService;

    public SaunaService(AppDbContext context, IAuditoriaService auditoriaService)
    {
        _context = context;
        _auditoriaService = auditoriaService;
    }

    public async Task<List<ProductoCatalogoDto>> ObtenerCatalogoAsync()
    {
        var productos = await _context.ProductosPOS
            .Where(p => p.Activo)
            .OrderBy(p => p.Categoria)
            .ThenBy(p => p.Nombre)
            .ToListAsync();

        return productos.Select(p => new ProductoCatalogoDto
        {
            ProductoId = p.Id,
            Nombre = p.Nombre,
            Precio = p.Precio,
            Categoria = p.Categoria,
            Icono = p.Icono,
            EsAlquilerVenta = p.EsAlquilerVenta,
            PrecioAlquiler = p.PrecioAlquiler,
            PrecioVenta = p.PrecioVenta
        }).ToList();
    }

    public async Task<List<ClienteSaunaCardDto>> ObtenerClientesActivosAsync()
    {
        var clientes = await _context.ClientesSauna
            .Where(c => c.Estado == EstadoClienteSauna.Activo)
            .OrderByDescending(c => c.FechaIngreso)
            .ToListAsync();

        var ventas = await _context.VentasSauna
            .Where(v => v.Estado != EstadoVenta.Anulada)
            .ToListAsync();

        return clientes.Select(c => new ClienteSaunaCardDto
        {
            ClienteSaunaId = c.Id,
            NombreCompleto = c.NombreCompleto,
            NumeroCandado = c.NumeroCandado,
            Seccion = c.Seccion,
            EsHuespedHotel = c.EsHuespedHotel,
            FechaIngreso = c.FechaIngreso,
            TotalConsumo = ventas.Where(v => v.ClienteSaunaId == c.Id).Sum(v => v.Total)
        }).ToList();
    }

    public async Task<int> RegistrarClienteAsync(NuevoClienteSaunaDto dto, int usuarioId)
    {
        if (string.IsNullOrWhiteSpace(dto.NumeroDocumento) || string.IsNullOrWhiteSpace(dto.NombreCompleto) || string.IsNullOrWhiteSpace(dto.NumeroCandado))
        {
            throw new InvalidOperationException("El número de documento, nombre y número de candado son obligatorios.");
        }

        var cliente = new ClienteSauna
        {
            TipoDocumento = dto.TipoDocumento,
            NumeroDocumento = dto.NumeroDocumento,
            NombreCompleto = dto.NombreCompleto,
            NumeroCandado = dto.NumeroCandado,
            Seccion = dto.Seccion,
            Observacion = dto.Observacion,
            FechaIngreso = DateTime.Now,
            Estado = EstadoClienteSauna.Activo,
            EsHuespedHotel = dto.EsHuespedHotel,
            EstadiaHotelId = dto.EsHuespedHotel ? dto.EstadiaHotelId : null
        };

        _context.ClientesSauna.Add(cliente);
        await _context.SaveChangesAsync();

        var descripcionIngreso = cliente.EsHuespedHotel
            ? $"{cliente.NombreCompleto} ingresó al Sauna (Huésped Hotel — entrada gratuita)."
            : $"{cliente.NombreCompleto} ingresó al Sauna.";

        await _auditoriaService.RegistrarAsync("INGRESO_SAUNA", descripcionIngreso, usuarioId, "ClienteSauna", cliente.Id);

        return cliente.Id;
    }

    public async Task<List<HabitacionCardDto>> BuscarHuespedesActivosAsync()
    {
        var estadias = await _context.Estadias
            .Include(e => e.Habitacion)
            .Where(e => e.Estado == EstadoEstadia.Activa)
            .OrderBy(e => e.Habitacion!.Numero)
            .ToListAsync();

        return estadias.Select(e => new HabitacionCardDto
        {
            EstadiaId = e.Id,
            HabitacionId = e.HabitacionId,
            Numero = e.Habitacion?.Numero ?? 0,
            NombreHuesped = e.NombreCompleto,
            Estado = EstadoHabitacion.Ocupada
        }).ToList();
    }

    public async Task RegistrarVentaAsync(int clienteSaunaId, List<ItemCarritoDto> items, int usuarioId, bool cargarAHabitacion)
    {
        if (items.Count == 0)
        {
            throw new InvalidOperationException("El carrito está vacío.");
        }

        var cliente = await _context.ClientesSauna.FirstOrDefaultAsync(c => c.Id == clienteSaunaId);
        if (cliente is null)
        {
            throw new InvalidOperationException("El cliente no existe.");
        }

        Estadia? estadia = null;
        if (cargarAHabitacion)
        {
            if (!cliente.EsHuespedHotel || cliente.EstadiaHotelId is null)
            {
                throw new InvalidOperationException("Este cliente no está vinculado a una habitación del hotel.");
            }

            estadia = await _context.Estadias
                .Include(e => e.Habitacion)
                .FirstOrDefaultAsync(e => e.Id == cliente.EstadiaHotelId.Value && e.Estado == EstadoEstadia.Activa);

            if (estadia is null)
            {
                throw new InvalidOperationException("La estadía de ese huésped ya no está activa. No se puede cargar a la habitación.");
            }
        }

        var venta = new VentaSauna
        {
            ClienteSaunaId = clienteSaunaId,
            Fecha = DateTime.Now,
            Estado = cargarAHabitacion ? EstadoVenta.CargadaAHabitacion : EstadoVenta.Pagada,
            EstadiaHotelDestinoId = estadia?.Id,
            UsuarioId = usuarioId,
            Total = items.Sum(i => i.Subtotal)
        };

        foreach (var item in items)
        {
            venta.Detalles.Add(new DetalleVenta
            {
                ProductoId = item.ProductoId,
                Descripcion = item.Descripcion,
                Cantidad = item.Cantidad,
                PrecioUnitario = item.PrecioUnitario,
                Subtotal = item.Subtotal
            });
        }

        _context.VentasSauna.Add(venta);

        if (estadia is not null)
        {
            estadia.TotalAcumulado += venta.Total;
        }

        await _context.SaveChangesAsync();

        if (cargarAHabitacion && estadia is not null)
        {
            await _auditoriaService.RegistrarAsync(
                "CARGO_HABITACION",
                $"Consumo de Sauna de {cliente.NombreCompleto} (S/ {venta.Total:0.00}) cargado a la habitación {estadia.Habitacion?.Numero}.",
                usuarioId, "VentaSauna", venta.Id);
        }
        else
        {
            await _auditoriaService.RegistrarAsync(
                "VENTA_POS",
                $"Venta POS de S/ {venta.Total:0.00} a {cliente.NombreCompleto} ({items.Count} ítem(s)).",
                usuarioId, "VentaSauna", venta.Id);
        }
    }

    public async Task RegistrarVentaHotelAsync(int estadiaId, List<ItemCarritoDto> items, int usuarioId, bool cargarAHabitacion)
    {
        if (items.Count == 0)
        {
            throw new InvalidOperationException("El carrito está vacío.");
        }

        var estadia = await _context.Estadias
            .Include(e => e.Habitacion)
            .FirstOrDefaultAsync(e => e.Id == estadiaId && e.Estado == EstadoEstadia.Activa);

        if (estadia is null)
        {
            throw new InvalidOperationException("La estadía no existe o ya no está activa.");
        }

        var venta = new VentaSauna
        {
            ClienteSaunaId = null,
            Fecha = DateTime.Now,
            Estado = cargarAHabitacion ? EstadoVenta.CargadaAHabitacion : EstadoVenta.Pagada,
            EstadiaHotelDestinoId = estadia.Id,
            UsuarioId = usuarioId,
            Total = items.Sum(i => i.Subtotal)
        };

        foreach (var item in items)
        {
            venta.Detalles.Add(new DetalleVenta
            {
                ProductoId = item.ProductoId,
                Descripcion = item.Descripcion,
                Cantidad = item.Cantidad,
                PrecioUnitario = item.PrecioUnitario,
                Subtotal = item.Subtotal
            });
        }

        _context.VentasSauna.Add(venta);

        if (cargarAHabitacion)
        {
            estadia.TotalAcumulado += venta.Total;
        }

        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync(
            cargarAHabitacion ? "CARGO_HABITACION" : "VENTA_POS",
            cargarAHabitacion
                ? $"Consumo de Cafetería de {estadia.NombreCompleto} (S/ {venta.Total:0.00}) cargado a la habitación {estadia.Habitacion?.Numero}."
                : $"Venta de Cafetería de S/ {venta.Total:0.00} a {estadia.NombreCompleto} (habitación {estadia.Habitacion?.Numero}), cobrada directamente.",
            usuarioId, "VentaSauna", venta.Id);
    }

    public async Task FinalizarSesionAsync(int clienteSaunaId, int usuarioId)
    {
        var cliente = await _context.ClientesSauna.FirstOrDefaultAsync(c => c.Id == clienteSaunaId);
        if (cliente is null)
        {
            throw new InvalidOperationException("El cliente no existe.");
        }

        cliente.Estado = EstadoClienteSauna.Finalizado;
        cliente.FechaSalida = DateTime.Now;

        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync(
            "SALIDA_SAUNA",
            $"{cliente.NombreCompleto} finalizó su sesión de Sauna.",
            usuarioId, "ClienteSauna", cliente.Id);
    }
}
