using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using JKalixto_System.Domain.Models;
using JKalixto_System.Infrastructure.Data;
using JKalixto_System.Infrastructure.Repositories;

namespace JKalixto_System.Application.Services;

/// <summary>
/// Traduce un EstadoHabitacion a la CLAVE (string) del color de tema asociado — NO al
/// color en sí, para que esta capa (Application) no dependa de ningún tipo de UI (MAUI,
/// Blazor, etc.) y pueda vivir en una librería compartida entre la app de escritorio y
/// el futuro proyecto web. Las claves son las mismas que usa TemaService como llaves de
/// Application.Current.Resources en MAUI. Compartida entre HabitacionCardDto y
/// DashboardService para no repetir el mismo switch dos veces.
/// </summary>
internal static class ClaveDeColorEstado
{
    public static string ParaEstadoHabitacion(EstadoHabitacion estado) => estado switch
    {
        EstadoHabitacion.Disponible => "ColorDisponible",
        EstadoHabitacion.Ocupada => "ColorOcupada",
        EstadoHabitacion.LimpiezaSalida => "ColorLimpieza",
        EstadoHabitacion.Mantenimiento => "ColorMantenimiento",
        _ => string.Empty
    };
}

/// <summary>Un solo lugar para el texto de cada categoría de movimiento de caja —
/// lo usan tanto MovimientoCajaCardDto (pantalla de Gastos) como el Informe
/// Mensual, para no repetir el mismo switch dos veces y arriesgar que queden
/// desincronizados.</summary>
internal static class EtiquetaCategoriaMovimiento
{
    public static string Etiqueta(CategoriaMovimientoCaja categoria) => categoria switch
    {
        CategoriaMovimientoCaja.PagoPersonal => "Pago del Personal",
        CategoriaMovimientoCaja.GastosDiarios => "Gastos diarios",
        CategoriaMovimientoCaja.AjusteCaja => "Ajuste de Caja",
        CategoriaMovimientoCaja.ConsumoPersonal => "Consumo de Personal",
        CategoriaMovimientoCaja.Cafeteria => "Cafetería",
        CategoriaMovimientoCaja.Mantenimiento => "Mantenimiento",
        CategoriaMovimientoCaja.Servicios => "Servicios",
        CategoriaMovimientoCaja.Sueldos => "Sueldos",
        CategoriaMovimientoCaja.Limpieza => "Limpieza",
        CategoriaMovimientoCaja.Lavanderia => "Lavandería",
        CategoriaMovimientoCaja.Recepcion => "Recepción",
        CategoriaMovimientoCaja.Vitrina => "Vitrina",
        CategoriaMovimientoCaja.Impuestos => "Impuestos",
        CategoriaMovimientoCaja.Comisiones => "Comisiones",
        CategoriaMovimientoCaja.Deposito => "Depósito",
        CategoriaMovimientoCaja.Otros => "Otros",
        _ => categoria.ToString()
    };
}

/// <summary>
/// Un solo lugar para la regla de "contraseña válida" — usado por el cambio de
/// contraseña propio (Program.cs), la creación de usuarios y el reseteo de
/// contraseña (UsuarioAdminService), para que las 3 pantallas exijan exactamente lo
/// mismo. Mínimo 8 caracteres + al menos una letra y un número (no exige mayúsculas
/// ni símbolos a propósito: personal de hotel sin mucha costumbre con sistemas, una
/// regla más estricta termina en contraseñas anotadas en un papel al lado de la PC,
/// que es peor que la que se busca evitar).
/// </summary>
public static class PoliticaPassword
{
    public static string? Validar(string password)
    {
        if (password.Length < 8)
        {
            return "La contraseña debe tener al menos 8 caracteres.";
        }
        if (!password.Any(char.IsLetter))
        {
            return "La contraseña debe incluir al menos una letra.";
        }
        if (!password.Any(char.IsDigit))
        {
            return "La contraseña debe incluir al menos un número.";
        }
        return null;
    }
}

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
///
/// Además bloquea la cuenta temporalmente después de varios intentos fallidos
/// seguidos (fuerza bruta) y deja constancia en auditoría de cada intento sobre
/// una cuenta real — agregado en la jornada de seguridad.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IAuditoriaService _auditoriaService;

    private const int MaxIntentosFallidos = 5;
    private static readonly TimeSpan DuracionBloqueo = TimeSpan.FromMinutes(15);

    public AuthService(IUsuarioRepository usuarioRepository, IAuditoriaService auditoriaService)
    {
        _usuarioRepository = usuarioRepository;
        _auditoriaService = auditoriaService;
    }

    public async Task<ResultadoLogin> IniciarSesionAsync(string username, string password)
    {
        var usuario = await _usuarioRepository.ObtenerPorUsernameAsync(username);

        if (usuario is null)
        {
            // No hay auditoría para este caso a propósito: LogAuditoria.UsuarioId tiene
            // una clave foránea real a Usuario (ver AppDbContext), y un username que no
            // existe no tiene ningún Id válido al cual atribuirle el intento.
            return new ResultadoLogin
            {
                Exito = false,
                Mensaje = "Usuario o contraseña incorrectos."
            };
        }

        if (usuario.BloqueadoHasta.HasValue && usuario.BloqueadoHasta.Value > DateTime.Now)
        {
            var minutosRestantes = Math.Max(1, (int)Math.Ceiling((usuario.BloqueadoHasta.Value - DateTime.Now).TotalMinutes));
            await _auditoriaService.RegistrarAsync(
                "LOGIN_BLOQUEADO",
                $"Intento de inicio de sesión de '{usuario.Username}' mientras la cuenta estaba bloqueada temporalmente por intentos fallidos.",
                usuario.Id, "Usuario", usuario.Id);
            return new ResultadoLogin
            {
                Exito = false,
                Mensaje = $"Cuenta bloqueada temporalmente por demasiados intentos fallidos. Probá de nuevo en {minutosRestantes} minuto(s)."
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
            usuario.IntentosFallidos++;
            var mensaje = "Usuario o contraseña incorrectos.";

            if (usuario.IntentosFallidos >= MaxIntentosFallidos)
            {
                usuario.BloqueadoHasta = DateTime.Now.Add(DuracionBloqueo);
                mensaje = $"Demasiados intentos fallidos. La cuenta quedó bloqueada por {(int)DuracionBloqueo.TotalMinutes} minutos.";
            }

            await _usuarioRepository.ActualizarAsync(usuario);
            await _auditoriaService.RegistrarAsync(
                "LOGIN_FALLIDO",
                $"Contraseña incorrecta para '{usuario.Username}' (intento {usuario.IntentosFallidos} de {MaxIntentosFallidos}).",
                usuario.Id, "Usuario", usuario.Id);

            return new ResultadoLogin { Exito = false, Mensaje = mensaje };
        }

        // Login correcto: resetea el contador de intentos y cualquier bloqueo vigente.
        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;
        await _usuarioRepository.ActualizarAsync(usuario);

        await _auditoriaService.RegistrarAsync(
            "LOGIN_EXITOSO",
            $"Inicio de sesión de '{usuario.Username}'.",
            usuario.Id, "Usuario", usuario.Id);

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
// GESTIÓN DE USUARIOS (implementación pendiente #3 de la jornada de
// seguridad: antes solo se podían crear/editar usuarios a mano en la base).
// ============================================================

public class UsuarioListaDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public RolUsuario Rol { get; set; }
    public bool Activo { get; set; }
    public bool DebeCambiarPassword { get; set; }
    public bool Bloqueado { get; set; }
    public DateTime FechaCreacion { get; set; }
}

public class NuevoUsuarioDto
{
    public string Username { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public RolUsuario Rol { get; set; }
    public string PasswordInicial { get; set; } = string.Empty;
}

public class EditarUsuarioDto
{
    public int Id { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public RolUsuario Rol { get; set; }
    public bool Activo { get; set; }
}

public interface IUsuarioAdminService
{
    Task<List<UsuarioListaDto>> ObtenerTodosAsync();
    Task<int> CrearAsync(NuevoUsuarioDto dto, int usuarioQueCreaId);
    Task ActualizarAsync(EditarUsuarioDto dto, int usuarioQueEditaId);

    /// <summary>Gerencia/Desarrollador resetean la clave de alguien que la
    /// olvidó — la cuenta queda obligada a elegir una nueva en el próximo login
    /// (igual que las 3 cuentas sembradas) y se invalidan sus sesiones activas
    /// en cualquier dispositivo (SecurityStamp nuevo).</summary>
    Task ResetearPasswordAsync(int usuarioId, string passwordTemporal, int usuarioQueReseteaId);
}

/// <summary>
/// CRUD de usuarios para la pantalla de administración — solo Gerencia/
/// Desarrollador pueden usarlo (mismo criterio que ObtenerRecientesAsync en
/// AuditoriaService: el chequeo de rol vive en el servicio, no solo en la UI,
/// para que nadie lo salte llamando al método directo).
/// </summary>
public class UsuarioAdminService : IUsuarioAdminService
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IAuditoriaService _auditoriaService;
    private readonly ISessionService _sessionService;

    public UsuarioAdminService(IUsuarioRepository usuarioRepository, IAuditoriaService auditoriaService, ISessionService sessionService)
    {
        _usuarioRepository = usuarioRepository;
        _auditoriaService = auditoriaService;
        _sessionService = sessionService;
    }

    private void ExigirPermiso()
    {
        var rol = _sessionService.UsuarioActual?.Rol;
        if (rol != RolUsuario.Gerencia && rol != RolUsuario.Desarrollador)
        {
            throw new UnauthorizedAccessException("No tenés permiso para administrar usuarios.");
        }
    }

    public async Task<List<UsuarioListaDto>> ObtenerTodosAsync()
    {
        ExigirPermiso();
        var usuarios = await _usuarioRepository.ObtenerTodosAsync();
        return usuarios.Select(u => new UsuarioListaDto
        {
            Id = u.Id,
            Username = u.Username,
            NombreCompleto = u.NombreCompleto,
            Rol = u.Rol,
            Activo = u.Activo,
            DebeCambiarPassword = u.DebeCambiarPassword,
            Bloqueado = u.BloqueadoHasta.HasValue && u.BloqueadoHasta.Value > DateTime.Now,
            FechaCreacion = u.FechaCreacion
        }).ToList();
    }

    public async Task<int> CrearAsync(NuevoUsuarioDto dto, int usuarioQueCreaId)
    {
        ExigirPermiso();

        if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.NombreCompleto))
        {
            throw new InvalidOperationException("Usuario y nombre completo son obligatorios.");
        }
        if (PoliticaPassword.Validar(dto.PasswordInicial) is { } errorPassword)
        {
            throw new InvalidOperationException(errorPassword);
        }
        if (await _usuarioRepository.ExisteUsernameAsync(dto.Username))
        {
            throw new InvalidOperationException($"Ya existe un usuario con el nombre '{dto.Username}'.");
        }

        var usuario = new Usuario
        {
            Username = dto.Username.Trim(),
            NombreCompleto = dto.NombreCompleto.Trim(),
            Rol = dto.Rol,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.PasswordInicial),
            Activo = true,
            FechaCreacion = DateTime.Now,
            // Un admin eligió esta contraseña por la persona -- se la hace elegir
            // una propia apenas entre, mismo criterio que las cuentas sembradas.
            DebeCambiarPassword = true,
            SecurityStamp = Guid.NewGuid().ToString("N")
        };

        var id = await _usuarioRepository.CrearAsync(usuario);

        await _auditoriaService.RegistrarAsync(
            "USUARIO_CREADO",
            $"Se creó el usuario '{usuario.Username}' ({usuario.NombreCompleto}, rol {usuario.Rol}).",
            usuarioQueCreaId, "Usuario", id);

        return id;
    }

    public async Task ActualizarAsync(EditarUsuarioDto dto, int usuarioQueEditaId)
    {
        ExigirPermiso();

        var usuario = await _usuarioRepository.ObtenerPorIdAsync(dto.Id)
            ?? throw new InvalidOperationException("El usuario no existe.");

        if (string.IsNullOrWhiteSpace(dto.NombreCompleto))
        {
            throw new InvalidOperationException("El nombre completo es obligatorio.");
        }

        usuario.NombreCompleto = dto.NombreCompleto.Trim();
        usuario.Rol = dto.Rol;
        usuario.Activo = dto.Activo;
        await _usuarioRepository.ActualizarAsync(usuario);

        await _auditoriaService.RegistrarAsync(
            "USUARIO_EDITADO",
            $"Se editó el usuario '{usuario.Username}' (rol {usuario.Rol}, {(usuario.Activo ? "activo" : "desactivado")}).",
            usuarioQueEditaId, "Usuario", usuario.Id);
    }

    public async Task ResetearPasswordAsync(int usuarioId, string passwordTemporal, int usuarioQueReseteaId)
    {
        ExigirPermiso();

        if (PoliticaPassword.Validar(passwordTemporal) is { } errorPassword)
        {
            throw new InvalidOperationException(errorPassword);
        }

        var usuario = await _usuarioRepository.ObtenerPorIdAsync(usuarioId)
            ?? throw new InvalidOperationException("El usuario no existe.");

        usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(passwordTemporal);
        usuario.DebeCambiarPassword = true;
        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;
        usuario.SecurityStamp = Guid.NewGuid().ToString("N");
        await _usuarioRepository.ActualizarAsync(usuario);

        await _auditoriaService.RegistrarAsync(
            "USUARIO_PASSWORD_RESETEADA",
            $"Se reseteó la contraseña de '{usuario.Username}' — va a tener que elegir una nueva en el próximo login.",
            usuarioQueReseteaId, "Usuario", usuario.Id);
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

    /// <summary>
    /// Clave del color de tema asociado al estado (ej. "ColorDisponible"), NO un Color
    /// de MAUI — esta capa (Application) tiene que poder compilar sin MAUI para poder
    /// vivir en una librería compartida con el futuro proyecto web. Cada UI (MAUI hoy,
    /// Blazor Server más adelante) traduce esta clave a su propio tipo de color; en MAUI
    /// eso lo hace Presentation/Converters/ClaveColorConverters.cs.
    /// </summary>
    public string ClaveColorEstado => ClaveDeColorEstado.ParaEstadoHabitacion(Estado);

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

    /// <summary>Propiedades de conveniencia para que las tarjetas de Recepción/Reservas
    /// puedan mostrar más información sin necesitar converters en XAML.</summary>
    public bool EsDisponible => Estado == EstadoHabitacion.Disponible;
    public bool EsMantenimiento => Estado == EstadoHabitacion.Mantenimiento;

    public string TarifaTexto => $"S/ {TarifaNoche:0.00} / noche";
    public string FechaCheckInTexto => FechaCheckInHuesped?.ToString("dd/MM HH:mm") ?? "-";

}

/// <summary>Datos que llegan desde CheckInPage para crear una nueva Estadia.</summary>
public class NuevoCheckInDto
{
    public int HabitacionId { get; set; }
    public TipoDocumento TipoDocumento { get; set; } = TipoDocumento.DNI;
    public string NumeroDocumento { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string Celular { get; set; } = string.Empty;

    // --- Registro de Huéspedes MINCETUR (ver Estadia) ---
    public DateTime? FechaNacimiento { get; set; }
    public SexoHuesped? Sexo { get; set; }
    public string Nacionalidad { get; set; } = "Peruana";
    public string? LugarResidencia { get; set; }
    public MotivoViaje? MotivoViaje { get; set; }

    public TipoComprobante TipoComprobante { get; set; } = TipoComprobante.Boleta;
    public string? RUC { get; set; }
    public string? RazonSocial { get; set; }
    public string? CorreoFacturacion { get; set; }
    public List<string> Acompanantes { get; set; } = new();
    public string? Observaciones { get; set; }
    public int UsuarioId { get; set; }

    /// <summary>Fecha/hora de Check-in a registrar en vez de "ahora" — para corregir un
    /// huésped que ya estaba alojado y recién se está cargando en el sistema, o un
    /// error de tipeo en la hora. Null = usa DateTime.Now (caso normal). Restringido a
    /// Gerencia/Desarrollador (ver HabitacionService.CheckInAsync): permitir que
    /// cualquiera "mueva" cuándo empezó una estadía afecta a qué día se le atribuye el
    /// ingreso en Informe Mensual y Reportes.</summary>
    public DateTime? FechaCheckInManual { get; set; }
}

/// <summary>Corrección de los datos de identidad de un huésped ya con Check-in hecho
/// — ver HabitacionService.EditarDatosHuespedAsync. A propósito NO incluye
/// facturación (RUC/RazonSocial/TipoComprobante) ni acompañantes: cambiar cómo se
/// factura después de hecho el Check-in es un caso distinto, más delicado, que no
/// se resuelve con un simple typo-fix.</summary>
public class EditarDatosHuespedDto
{
    public int EstadiaId { get; set; }
    public TipoDocumento TipoDocumento { get; set; } = TipoDocumento.DNI;
    public string NumeroDocumento { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string Celular { get; set; } = string.Empty;
}

// ============================================================
// MÓDULO HOTEL — Servicio
// ============================================================

public interface IHabitacionService
{
    Task<List<HabitacionCardDto>> ObtenerPorPisoAsync(int piso);
    Task<List<HabitacionCardDto>> ObtenerTodasAsync();
    Task CheckInAsync(NuevoCheckInDto dto);

    /// <summary>fechaCheckOutManual: igual que NuevoCheckInDto.FechaCheckInManual, pero
    /// para el cierre de la estadía — null usa DateTime.Now (caso normal), y fijar un
    /// valor está restringido a Gerencia/Desarrollador por el mismo motivo.</summary>
    Task CheckOutAsync(int estadiaId, int usuarioId, MetodoPago metodoPago, DateTime? fechaCheckOutManual = null);
    Task IniciarMantenimientoAsync(int habitacionId, string motivo, int usuarioId);
    Task FinalizarMantenimientoAsync(int habitacionId, int usuarioId);
    Task FinalizarLimpiezaAsync(int habitacionId, int usuarioId);
    Task RegistrarLimpiezaIntermediaAsync(int habitacionId, int usuarioId);

    /// <summary>Cambia la tarifa por noche de una habitación de acá en adelante — NO
    /// afecta el TotalAcumulado de estadías ya en curso (esas ya cobraron su tarifa al
    /// hacer Check-in). Restringido a Gerencia/Desarrollador.</summary>
    Task EditarTarifaAsync(int habitacionId, decimal nuevaTarifa, int usuarioId);

    /// <summary>Corrige documento/nombre/celular de una estadía Activa — para el typo
    /// más común del día a día (un número de documento mal tipeado en el apuro de un
    /// Check-in). A diferencia de EditarTarifaAsync, NO está restringido a Gerencia:
    /// es una corrección de datos, no algo que cambie cuánto se cobra, y cualquiera
    /// que hizo el Check-in original debería poder arreglar su propio error sin
    /// depender de un superior. Solo se puede usar mientras la estadía sigue Activa
    /// (no después del Check-out, que ya cerró el registro).</summary>
    Task EditarDatosHuespedAsync(EditarDatosHuespedDto dto, int usuarioId);

    /// <summary>Datos de una Estadia ya cerrada (Check-out hecho), listos para
    /// imprimir el comprobante. Null si la estadía no existe o todavía está Activa
    /// (el total y el número de comprobante recién quedan definitivos al Check-out).</summary>
    Task<EstadiaReciboDto?> ObtenerReciboEstadiaAsync(int estadiaId);
}

/// <summary>
/// Toda la lógica del ciclo de vida de una habitación: Check-in, Check-out,
/// mantenimiento y limpieza. Usa AppDbContext directamente (no un Repositorio
/// intermedio) para garantizar que los cambios a Habitacion y Estadia se
/// guarden juntos, en la misma operación — así se evita un bug sutil de EF
/// Core que aparece si cada Repositorio tuviera su propia conexión separada.
/// </summary>
// ============================================================
// FACTURACIÓN — numeración de comprobantes
// ============================================================

public interface IComprobanteNumeracionService
{
    /// <summary>Genera el siguiente número correlativo para ese tipo de comprobante,
    /// con el formato SUNAT "SERIE-00000001". Esto NO emite el comprobante
    /// electrónico ante SUNAT (eso requiere contratar un PSE/OSE) — solo asegura que
    /// el número nunca se repita ni salte, para poder imprimirlo ya mismo en un
    /// comprobante manual mientras esa integración no exista.</summary>
    Task<string> ObtenerSiguienteNumeroAsync(TipoComprobante tipo);
}

public class ComprobanteNumeracionService : IComprobanteNumeracionService
{
    private readonly AppDbContext _context;

    public ComprobanteNumeracionService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>Serie fija por tipo — un solo punto de emisión (el POS del hotel),
    /// como la gran mayoría de negocios chicos en Perú. Si el día de mañana hay más
    /// de un punto de venta, esto pasaría a depender de cuál terminal emite.</summary>
    private static string SerieParaTipo(TipoComprobante tipo) => tipo == TipoComprobante.Factura ? "F001" : "B001";

    public async Task<string> ObtenerSiguienteNumeroAsync(TipoComprobante tipo)
    {
        var serie = SerieParaTipo(tipo);

        // Transacción propia: dos ventas casi simultáneas no pueden terminar con el
        // mismo número — SUNAT exige correlativos únicos y sin repetirse. El índice
        // único en (Tipo, Serie) es la segunda barrera por si dos "Add" chocaran.
        await using var transaccion = await _context.Database.BeginTransactionAsync();

        var contador = await _context.NumeracionesComprobante
            .AsTracking()
            .FirstOrDefaultAsync(n => n.Tipo == tipo && n.Serie == serie);

        if (contador is null)
        {
            contador = new NumeracionComprobante { Tipo = tipo, Serie = serie, UltimoCorrelativo = 0 };
            _context.NumeracionesComprobante.Add(contador);
        }

        contador.UltimoCorrelativo++;
        await _context.SaveChangesAsync();
        await transaccion.CommitAsync();

        return $"{serie}-{contador.UltimoCorrelativo:00000000}";
    }
}

public class HabitacionService : IHabitacionService
{
    private readonly AppDbContext _context;
    private readonly IAuditoriaService _auditoriaService;
    private readonly IComprobanteNumeracionService _comprobanteNumeracionService;
    private readonly ISessionService _sessionService;

    public HabitacionService(AppDbContext context, IAuditoriaService auditoriaService, IComprobanteNumeracionService comprobanteNumeracionService, ISessionService sessionService)
    {
        _context = context;
        _auditoriaService = auditoriaService;
        _comprobanteNumeracionService = comprobanteNumeracionService;
        _sessionService = sessionService;
    }

    /// <summary>Repite el mismo chequeo de rol que ya usan GastosService/AuditoriaService
    /// para sus acciones sensibles — protege tanto "mover" la fecha real de un
    /// Check-in/Check-out (cambia a qué día se le atribuye un ingreso en los reportes)
    /// como editar la tarifa de una habitación (cambia cuánto se cobra a partir de
    /// ahora).</summary>
    private void ExigirRolGerencial(string accion)
    {
        var rol = _sessionService.UsuarioActual?.Rol;
        if (rol != RolUsuario.Gerencia && rol != RolUsuario.Desarrollador)
        {
            throw new UnauthorizedAccessException($"Solo Gerencia/Desarrollador puede {accion}.");
        }
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
        // AsTracking(): esta lectura SE MODIFICA (habitacion.Estado = Ocupada, más
        // abajo) y se guarda. Además es la que necesita el ConcurrencyToken de
        // Habitacion.Estado para detectar dos Check-in simultáneos — sin seguimiento,
        // EF Core no tiene el valor original con el cual comparar y el cambio ni
        // siquiera llegaría a guardarse.
        var habitacion = await _context.Habitaciones.AsTracking().FirstOrDefaultAsync(h => h.Id == dto.HabitacionId);
        if (habitacion is null)
        {
            throw new InvalidOperationException("La habitación no existe.");
        }
        if (habitacion.Estado != EstadoHabitacion.Disponible)
        {
            throw new InvalidOperationException("Esta habitación ya no está Disponible. Actualiza la pantalla e intenta de nuevo.");
        }

        var fechaCheckIn = DateTime.Now;
        if (dto.FechaCheckInManual is { } fechaManual)
        {
            ExigirRolGerencial("fijar una fecha de Check-in distinta a la actual");
            if (fechaManual > DateTime.Now)
            {
                throw new InvalidOperationException("La fecha de Check-in no puede ser en el futuro.");
            }
            fechaCheckIn = fechaManual;
        }

        var estadia = new Estadia
        {
            HabitacionId = habitacion.Id,
            TipoDocumento = dto.TipoDocumento,
            NumeroDocumento = dto.NumeroDocumento,
            NombreCompleto = dto.NombreCompleto,
            Celular = dto.Celular,
            FechaNacimiento = dto.FechaNacimiento,
            Sexo = dto.Sexo,
            Nacionalidad = string.IsNullOrWhiteSpace(dto.Nacionalidad) ? "Peruana" : dto.Nacionalidad,
            LugarResidencia = dto.LugarResidencia,
            MotivoViaje = dto.MotivoViaje,
            FechaCheckIn = fechaCheckIn,
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

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // Otra operación (otro Check-in, un Mantenimiento, etc.) cambió el
            // estado de esta habitación entre que la leímos y que guardamos —
            // ver el ConcurrencyToken en AppDbContext. Sin este catch, dos
            // Check-in casi simultáneos podían dejar la misma habitación
            // asignada a dos huéspedes.
            throw new InvalidOperationException("Esta habitación acaba de cambiar de estado (probablemente otro Check-in la tomó primero). Actualiza la pantalla e intenta de nuevo.");
        }

        await _auditoriaService.RegistrarAsync(
            "CHECK_IN",
            $"Check-in de {dto.NombreCompleto} ({dto.TipoDocumento} {dto.NumeroDocumento}) en habitación {habitacion.Numero}.",
            dto.UsuarioId, "Estadia", estadia.Id);
    }

    public async Task CheckOutAsync(int estadiaId, int usuarioId, MetodoPago metodoPago, DateTime? fechaCheckOutManual = null)
    {
        // AsTracking(): se modifican tanto la Estadia (Estado, TotalAcumulado) como
        // su Habitacion (Estado = LimpiezaSalida) y ambas se guardan.
        var estadia = await _context.Estadias
            .Include(e => e.Habitacion)
            .AsTracking()
            .FirstOrDefaultAsync(e => e.Id == estadiaId);

        if (estadia is null)
        {
            throw new InvalidOperationException("La estadía no existe.");
        }
        if (estadia.Estado != EstadoEstadia.Activa)
        {
            throw new InvalidOperationException("Esta estadía ya fue cerrada.");
        }

        var fechaCheckOut = DateTime.Now;
        if (fechaCheckOutManual is { } fechaManual)
        {
            ExigirRolGerencial("fijar una fecha de Check-out distinta a la actual");
            if (fechaManual > DateTime.Now)
            {
                throw new InvalidOperationException("La fecha de Check-out no puede ser en el futuro.");
            }
            if (fechaManual < estadia.FechaCheckIn)
            {
                throw new InvalidOperationException("La fecha de Check-out no puede ser anterior a la de Check-in.");
            }
            fechaCheckOut = fechaManual;
        }

        estadia.Estado = EstadoEstadia.Finalizada;
        estadia.FechaCheckOut = fechaCheckOut;
        estadia.UsuarioCheckOutId = usuarioId;
        estadia.MetodoPago = metodoPago;
        estadia.NumeroComprobante = await _comprobanteNumeracionService.ObtenerSiguienteNumeroAsync(estadia.TipoComprobante);

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

            // Abre el registro histórico de limpieza (ver RegistroLimpieza) — se
            // cierra en FinalizarLimpiezaAsync. Sirve para reportar frecuencia de
            // limpieza por habitación; no afecta el Calendario, que solo mira
            // Habitacion.Estado para HOY.
            _context.RegistrosLimpieza.Add(new RegistroLimpieza
            {
                HabitacionId = estadia.Habitacion.Id,
                FechaInicio = DateTime.Now,
                UsuarioInicioId = usuarioId
            });
        }

        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync(
            "CHECKOUT",
            $"Check-out de {estadia.NombreCompleto}, habitación {estadia.Habitacion?.Numero}. Total: S/ {estadia.TotalAcumulado:0.00}. Pagado con {metodoPago}. Comprobante {estadia.NumeroComprobante}.",
            usuarioId, "Estadia", estadia.Id);
    }

    public async Task IniciarMantenimientoAsync(int habitacionId, string motivo, int usuarioId)
    {
        // REGLA ANTI-FRAUDE: sin motivo, bloqueo total.
        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new InvalidOperationException("El motivo de mantenimiento es obligatorio.");
        }

        // AsTracking(): se modifica (Estado, MotivoMantenimiento, fecha) y se guarda.
        var habitacion = await _context.Habitaciones.AsTracking().FirstOrDefaultAsync(h => h.Id == habitacionId);
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
        // AsTracking(): se modifica (Estado, MotivoMantenimiento) y se guarda.
        var habitacion = await _context.Habitaciones.AsTracking().FirstOrDefaultAsync(h => h.Id == habitacionId);
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

    public async Task FinalizarLimpiezaAsync(int habitacionId, int usuarioId)
    {
        // AsTracking(): se modifica (Estado = Disponible) y se guarda.
        var habitacion = await _context.Habitaciones.AsTracking().FirstOrDefaultAsync(h => h.Id == habitacionId);
        if (habitacion is null)
        {
            throw new InvalidOperationException("La habitación no existe.");
        }
        if (habitacion.Estado != EstadoHabitacion.LimpiezaSalida)
        {
            throw new InvalidOperationException("La habitación no está en Limpieza.");
        }

        habitacion.Estado = EstadoHabitacion.Disponible;

        // Cierra el registro histórico que abrió CheckOutAsync. AsTracking()
        // porque se modifica (FechaFin, UsuarioFinId) y se guarda junto con la
        // habitación en el mismo SaveChangesAsync.
        var registroAbierto = await _context.RegistrosLimpieza
            .AsTracking()
            .Where(r => r.HabitacionId == habitacionId && r.FechaFin == null)
            .OrderByDescending(r => r.FechaInicio)
            .FirstOrDefaultAsync();
        if (registroAbierto is not null)
        {
            registroAbierto.FechaFin = DateTime.Now;
            registroAbierto.UsuarioFinId = usuarioId;
        }

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

    public async Task EditarTarifaAsync(int habitacionId, decimal nuevaTarifa, int usuarioId)
    {
        ExigirRolGerencial("editar la tarifa de una habitación");

        if (nuevaTarifa <= 0)
        {
            throw new InvalidOperationException("La tarifa debe ser mayor a cero.");
        }

        // AsTracking(): se modifica (TarifaNoche) y se guarda. No toca Estado (el
        // ConcurrencyToken es sobre esa propiedad, no sobre esta) así que no hay riesgo
        // de chocar con un Check-in concurrente por este cambio.
        var habitacion = await _context.Habitaciones.AsTracking().FirstOrDefaultAsync(h => h.Id == habitacionId);
        if (habitacion is null)
        {
            throw new InvalidOperationException("La habitación no existe.");
        }

        var tarifaAnterior = habitacion.TarifaNoche;
        habitacion.TarifaNoche = nuevaTarifa;
        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync(
            "HABITACION_TARIFA_EDITADA",
            $"Tarifa de la habitación {habitacion.Numero} cambió de S/ {tarifaAnterior:0.00} a S/ {nuevaTarifa:0.00}.",
            usuarioId, "Habitacion", habitacion.Id);
    }

    public async Task EditarDatosHuespedAsync(EditarDatosHuespedDto dto, int usuarioId)
    {
        if (string.IsNullOrWhiteSpace(dto.NumeroDocumento) || string.IsNullOrWhiteSpace(dto.NombreCompleto) || string.IsNullOrWhiteSpace(dto.Celular))
        {
            throw new InvalidOperationException("El número de documento, nombre completo y celular son obligatorios.");
        }

        // AsTracking(): se modifica (TipoDocumento/NumeroDocumento/NombreCompleto/
        // Celular) y se guarda.
        var estadia = await _context.Estadias.AsTracking().FirstOrDefaultAsync(e => e.Id == dto.EstadiaId);
        if (estadia is null)
        {
            throw new InvalidOperationException("La estadía no existe.");
        }
        if (estadia.Estado != EstadoEstadia.Activa)
        {
            throw new InvalidOperationException("Solo se pueden corregir los datos de una estadía Activa (no después del Check-out).");
        }

        var nombreAnterior = estadia.NombreCompleto;
        estadia.TipoDocumento = dto.TipoDocumento;
        estadia.NumeroDocumento = dto.NumeroDocumento.Trim();
        estadia.NombreCompleto = dto.NombreCompleto.Trim();
        estadia.Celular = dto.Celular.Trim();
        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync(
            "ESTADIA_DATOS_CORREGIDOS",
            $"Se corrigieron los datos del huésped \"{nombreAnterior}\" → \"{estadia.NombreCompleto}\" ({estadia.TipoDocumento} {estadia.NumeroDocumento}) en la habitación.",
            usuarioId, "Estadia", estadia.Id);
    }

    public async Task<EstadiaReciboDto?> ObtenerReciboEstadiaAsync(int estadiaId)
    {
        var estadia = await _context.Estadias
            .Include(e => e.Habitacion)
            .Include(e => e.Acompanantes)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == estadiaId);

        if (estadia is null || estadia.Estado != EstadoEstadia.Finalizada || estadia.FechaCheckOut is null)
        {
            return null;
        }

        var noches = Math.Max(1, (estadia.FechaCheckOut.Value.Date - estadia.FechaCheckIn.Date).Days);

        return new EstadiaReciboDto
        {
            EstadiaId = estadia.Id,
            NumeroComprobante = estadia.NumeroComprobante ?? "—",
            TipoComprobante = estadia.TipoComprobante,
            FechaCheckIn = estadia.FechaCheckIn,
            FechaCheckOut = estadia.FechaCheckOut.Value,
            Noches = noches,
            NumeroHabitacion = estadia.Habitacion?.Numero ?? 0,
            TipoHabitacion = estadia.Habitacion?.Tipo ?? TipoHabitacion.Simple,
            NombreHuesped = estadia.NombreCompleto,
            TipoDocumento = estadia.TipoDocumento,
            NumeroDocumento = estadia.NumeroDocumento,
            RUC = estadia.RUC,
            RazonSocial = estadia.RazonSocial,
            MetodoPago = estadia.MetodoPago,
            Total = estadia.TotalAcumulado,
            Acompanantes = estadia.Acompanantes.Select(a => a.NombreCompleto).ToList()
        };
    }
}

/// <summary>Datos listos para imprimir el comprobante de una Estadia ya cerrada —
/// ver IHabitacionService.ObtenerReciboEstadiaAsync.</summary>
public class EstadiaReciboDto
{
    public int EstadiaId { get; set; }
    public string NumeroComprobante { get; set; } = string.Empty;
    public TipoComprobante TipoComprobante { get; set; }
    public DateTime FechaCheckIn { get; set; }
    public DateTime FechaCheckOut { get; set; }
    public int Noches { get; set; }
    public int NumeroHabitacion { get; set; }
    public TipoHabitacion TipoHabitacion { get; set; }
    public string NombreHuesped { get; set; } = string.Empty;
    public TipoDocumento TipoDocumento { get; set; }
    public string NumeroDocumento { get; set; } = string.Empty;
    public string? RUC { get; set; }
    public string? RazonSocial { get; set; }
    public MetodoPago? MetodoPago { get; set; }
    public decimal Total { get; set; }
    public List<string> Acompanantes { get; set; } = new();
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

    /// <summary>Clave del color de tema (ej. "ColorDisponible"), no un Color de MAUI — ver
    /// el comentario de HabitacionCardDto.ClaveColorEstado más arriba.</summary>
    public string ClaveColorBarra { get; set; } = string.Empty;
    public string Etiqueta { get; set; } = string.Empty;
}

public class ResumenDashboardDto
{
    public decimal IngresosHotelHoy { get; set; }
    public decimal IngresosSaunaHoy { get; set; }
    public decimal IngresosTotalHoy => IngresosHotelHoy + IngresosSaunaHoy;

    /// <summary>Porcentaje 0-100.</summary>
    public double TasaOcupacion { get; set; }

    /// <summary>ADR ("Average Daily Rate"): tarifa promedio de las habitaciones
    /// ocupadas ahora mismo. Es el KPI estándar de la industria hotelera para saber
    /// a qué precio promedio se está vendiendo, sin importar cuántas se vendieron.</summary>
    public decimal TarifaPromedioDiaria { get; set; }

    /// <summary>RevPAR ("Revenue per Available Room"): ADR × Ocupación. A diferencia
    /// del ADR, sí castiga tener habitaciones vacías — es el número que mejor resume
    /// "qué tan bien está rindiendo el hotel hoy" en una sola cifra.</summary>
    public decimal IngresoPorHabitacionDisponible { get; set; }

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

        // ADR/RevPAR se calculan sobre la TARIFA de la habitación (un valor estable
        // y conocido), no sobre TotalAcumulado de la Estadia (que mezcla noches +
        // consumos de POS y no sirve para medir "a qué precio se vendió la noche").
        decimal ingresoTarifasOcupadas = habitaciones.Where(h => h.Estado == EstadoHabitacion.Ocupada).Sum(h => h.TarifaNoche);
        decimal adr = ocupadas > 0 ? ingresoTarifasOcupadas / ocupadas : 0m;
        decimal revPar = habitaciones.Count > 0 ? ingresoTarifasOcupadas / habitaciones.Count : 0m;

        int alertas = habitaciones.Count(h =>
            h.Estado == EstadoHabitacion.Mantenimiento &&
            h.FechaInicioMantenimiento.HasValue &&
            (DateTime.Now - h.FechaInicioMantenimiento.Value).TotalHours > 4);

        return new ResumenDashboardDto
        {
            IngresosHotelHoy = ingresosHotel,
            IngresosSaunaHoy = ingresosSauna,
            TasaOcupacion = Math.Round(tasaOcupacion, 1),
            TarifaPromedioDiaria = Math.Round(adr, 2),
            IngresoPorHabitacionDisponible = Math.Round(revPar, 2),
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
                ClaveColorBarra = ClaveDeColorEstado.ParaEstadoHabitacion(estado),
                Etiqueta = EtiquetaParaEstado(estado)
            });
        }

        return resultado;
    }

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
// INFORME MENSUAL — DTOs + Servicio
// ============================================================
// Replica el informe mensual en Excel que el negocio ya llevaba a mano
// (ver conversación de referencia): resumen Ingreso/Egreso/Saldo del mes,
// desglose por método de pago y por categoría (con el detalle de
// movimientos debajo de cada categoría), el libro diario completo del
// mes, y la evolución mes a mes para los gráficos anuales.

public class MontoPorMetodoDto
{
    public string Etiqueta { get; set; } = string.Empty;
    public decimal Monto { get; set; }
}

/// <summary>Un renglón del libro diario — igual a la columna "Concepto" del Excel:
/// mezcla check-outs de Hotel, ventas de Sauna/Cafetería cobradas al contado, y
/// movimientos de caja manuales, todo en una sola línea de tiempo.</summary>
public class MovimientoLibroDiarioDto
{
    public DateTime Fecha { get; set; }
    public string Concepto { get; set; } = string.Empty;
    public decimal? Ingreso { get; set; }
    public decimal? Salida { get; set; }
    public string Medio { get; set; } = string.Empty;
    public string Responsable { get; set; } = string.Empty;
}

public class MontoPorCategoriaDto
{
    public string Etiqueta { get; set; } = string.Empty;
    public decimal Monto { get; set; }

    /// <summary>Los movimientos que componen este total — para poder expandir una
    /// categoría (ej. "Servicios") y ver el detalle, igual que el Excel mostraba
    /// Agua/Electricidad/Cable/etc. debajo del total de Servicios.</summary>
    public List<MovimientoLibroDiarioDto> Detalle { get; set; } = new();
}

public class InformeMensualDto
{
    public int Anio { get; set; }
    public int Mes { get; set; }
    public string NombreMes { get; set; } = string.Empty;

    public decimal IngresoTotal { get; set; }
    public decimal EgresoTotal { get; set; }
    public decimal Saldo => IngresoTotal - EgresoTotal;

    /// <summary>Saldo acumulado de TODA la historia hasta antes de este mes — igual
    /// a "SALDO ANTERIOR" del Excel.</summary>
    public decimal SaldoAnterior { get; set; }

    public List<MontoPorMetodoDto> IngresosPorMetodo { get; set; } = new();
    public List<MontoPorCategoriaDto> IngresosPorCategoria { get; set; } = new();

    public List<MontoPorMetodoDto> EgresosPorMetodo { get; set; } = new();
    public List<MontoPorCategoriaDto> EgresosPorCategoria { get; set; } = new();

    public List<MovimientoLibroDiarioDto> LibroDiario { get; set; } = new();
}

/// <summary>Un punto de la serie mensual — para los 2 gráficos "anuales" del Excel
/// (barras Ingreso/Egreso por mes, y tendencia del Saldo neto).</summary>
public class EvolucionMesDto
{
    public int Anio { get; set; }
    public int Mes { get; set; }
    public string Etiqueta { get; set; } = string.Empty;
    public decimal Ingreso { get; set; }
    public decimal Egreso { get; set; }
    public decimal Saldo => Ingreso - Egreso;
}

public interface IInformeMensualService
{
    Task<InformeMensualDto> ObtenerInformeMensualAsync(int anio, int mes);

    /// <summary>Mismo informe que ObtenerInformeMensualAsync, pero para un rango de
    /// fechas elegido a mano en vez de un mes calendario completo — por ejemplo, para
    /// cerrar solo la primera quincena o revisar una semana puntual. "hastaInclusive"
    /// es el último día que SÍ entra en el informe (a diferencia del "finExclusivo"
    /// interno, para que quien llama no tenga que acordarse de sumar un día).</summary>
    Task<InformeMensualDto> ObtenerInformePorRangoAsync(DateTime desde, DateTime hastaInclusive);

    /// <summary>Los últimos "cantidadMeses" meses, terminando en el mes actual.</summary>
    Task<List<EvolucionMesDto>> ObtenerEvolucionAsync(int cantidadMeses);
}

public class InformeMensualService : IInformeMensualService
{
    private readonly AppDbContext _context;

    private static readonly string[] NombresMeses =
    {
        "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
        "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"
    };

    public InformeMensualService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<InformeMensualDto> ObtenerInformeMensualAsync(int anio, int mes)
    {
        var inicio = new DateTime(anio, mes, 1);
        var finExclusivo = inicio.AddMonths(1);

        var informe = await ConstruirInformeAsync(inicio, finExclusivo);
        informe.Anio = anio;
        informe.Mes = mes;
        informe.NombreMes = $"{NombresMeses[mes - 1]} {anio}";
        return informe;
    }

    public async Task<InformeMensualDto> ObtenerInformePorRangoAsync(DateTime desde, DateTime hastaInclusive)
    {
        var inicio = desde.Date;
        var finExclusivo = hastaInclusive.Date.AddDays(1);
        if (finExclusivo <= inicio)
        {
            throw new InvalidOperationException("La fecha 'hasta' debe ser igual o posterior a la fecha 'desde'.");
        }

        var informe = await ConstruirInformeAsync(inicio, finExclusivo);
        informe.NombreMes = inicio == hastaInclusive.Date
            ? inicio.ToString("dd/MM/yyyy")
            : $"{inicio:dd/MM/yyyy} — {hastaInclusive:dd/MM/yyyy}";
        return informe;
    }

    /// <summary>Núcleo compartido por ObtenerInformeMensualAsync y
    /// ObtenerInformePorRangoAsync — arma todo el informe (totales, desglose por
    /// método/categoría, libro diario) para el rango [inicio, finExclusivo). No fija
    /// Anio/Mes/NombreMes: eso es responsabilidad de cada método público, porque solo
    /// tiene sentido real para un mes calendario completo.</summary>
    private async Task<InformeMensualDto> ConstruirInformeAsync(DateTime inicio, DateTime finExclusivo)
    {
        var (ingresoAnterior, egresoAnterior) = await CalcularTotalesAsync(DateTime.MinValue, inicio);

        // --- Ingresos "reales" del período: cuándo se cobró la plata de verdad. ---
        // Check-out (no Check-in): recién ahí se cobra el total real de la estadía
        // (noches + consumos cargados a la habitación). Venta de Sauna/Cafetería
        // SOLO si se pagó al contado (Pagada) — la que se cargó a la habitación ya
        // está adentro del total de la estadía, contarla de nuevo sería duplicar.
        var estadias = await _context.Estadias
            .Include(e => e.Habitacion)
            .Where(e => e.FechaCheckOut != null && e.FechaCheckOut >= inicio && e.FechaCheckOut < finExclusivo)
            .ToListAsync();

        var ventas = await _context.VentasSauna
            .Where(v => v.Estado == EstadoVenta.Pagada && v.Fecha >= inicio && v.Fecha < finExclusivo)
            .ToListAsync();

        var clienteIds = ventas.Where(v => v.ClienteSaunaId != null).Select(v => v.ClienteSaunaId!.Value).Distinct().ToList();
        var clientes = await _context.ClientesSauna.Where(c => clienteIds.Contains(c.Id)).ToListAsync();
        var estadiaVentaIds = ventas.Where(v => v.ClienteSaunaId == null && v.EstadiaHotelDestinoId != null)
            .Select(v => v.EstadiaHotelDestinoId!.Value).Distinct().ToList();
        var estadiasDeVenta = await _context.Estadias.Where(e => estadiaVentaIds.Contains(e.Id)).ToListAsync();

        var movimientos = await _context.MovimientosCaja
            .Where(m => m.FechaHora >= inicio && m.FechaHora < finExclusivo)
            .ToListAsync();
        var usuarioIds = movimientos.Select(m => m.UsuarioId).Distinct().ToList();
        var usuarios = await _context.Usuarios.Where(u => usuarioIds.Contains(u.Id)).ToListAsync();
        string NombreUsuario(int id) => usuarios.FirstOrDefault(u => u.Id == id)?.NombreCompleto ?? "—";

        // --- Libro diario: las 3 fuentes, mezcladas y ordenadas por fecha ---
        var libro = new List<MovimientoLibroDiarioDto>();

        foreach (var e in estadias)
        {
            libro.Add(new MovimientoLibroDiarioDto
            {
                Fecha = e.FechaCheckOut!.Value,
                Concepto = $"Check-out {e.NombreCompleto} — Hab. {e.Habitacion?.Numero}",
                Ingreso = e.TotalAcumulado,
                Medio = e.MetodoPago.HasValue ? EtiquetaMetodo(e.MetodoPago.Value) : "—",
                Responsable = "—"
            });
        }

        foreach (var v in ventas)
        {
            var nombre = v.ClienteSaunaId is { } clienteId
                ? clientes.FirstOrDefault(c => c.Id == clienteId)?.NombreCompleto ?? "—"
                : estadiasDeVenta.FirstOrDefault(e => e.Id == v.EstadiaHotelDestinoId)?.NombreCompleto ?? "—";

            libro.Add(new MovimientoLibroDiarioDto
            {
                Fecha = v.Fecha,
                Concepto = $"Venta POS — {nombre}",
                Ingreso = v.Total,
                Medio = v.MetodoPago.HasValue ? EtiquetaMetodo(v.MetodoPago.Value) : "—",
                Responsable = "—"
            });
        }

        foreach (var m in movimientos)
        {
            libro.Add(new MovimientoLibroDiarioDto
            {
                Fecha = m.FechaHora,
                Concepto = m.Descripcion,
                Ingreso = m.Direccion == DireccionMovimiento.Ingreso ? m.Monto : null,
                Salida = m.Direccion == DireccionMovimiento.Salida ? m.Monto : null,
                Medio = EtiquetaMetodo(m.MetodoPago),
                Responsable = !string.IsNullOrWhiteSpace(m.PersonalRelacionado) ? m.PersonalRelacionado : NombreUsuario(m.UsuarioId)
            });
        }

        libro = libro.OrderBy(l => l.Fecha).ToList();

        var detalleCheckOuts = libro.Where(l => l.Concepto.StartsWith("Check-out")).ToList();
        var detalleVentas = libro.Where(l => l.Concepto.StartsWith("Venta POS")).ToList();

        decimal ingresoHabitacion = estadias.Sum(e => e.TotalAcumulado);
        decimal ingresoVentas = ventas.Sum(v => v.Total);
        decimal ingresoMovimientos = movimientos.Where(m => m.Direccion == DireccionMovimiento.Ingreso).Sum(m => m.Monto);
        decimal egresoTotal = movimientos.Where(m => m.Direccion == DireccionMovimiento.Salida).Sum(m => m.Monto);

        var ingresosPorCategoria = new List<MontoPorCategoriaDto>();
        if (ingresoHabitacion > 0)
        {
            ingresosPorCategoria.Add(new MontoPorCategoriaDto { Etiqueta = "Habitación", Monto = ingresoHabitacion, Detalle = detalleCheckOuts });
        }
        if (ingresoVentas > 0)
        {
            ingresosPorCategoria.Add(new MontoPorCategoriaDto { Etiqueta = "Otras ventas", Monto = ingresoVentas, Detalle = detalleVentas });
        }
        ingresosPorCategoria.AddRange(
            movimientos.Where(m => m.Direccion == DireccionMovimiento.Ingreso)
                .GroupBy(m => m.Categoria)
                .Select(g => new MontoPorCategoriaDto
                {
                    Etiqueta = EtiquetaCategoriaMovimiento.Etiqueta(g.Key),
                    Monto = g.Sum(m => m.Monto),
                    Detalle = g.Select(m => new MovimientoLibroDiarioDto
                    {
                        Fecha = m.FechaHora,
                        Concepto = m.Descripcion,
                        Ingreso = m.Monto,
                        Medio = EtiquetaMetodo(m.MetodoPago),
                        Responsable = !string.IsNullOrWhiteSpace(m.PersonalRelacionado) ? m.PersonalRelacionado : NombreUsuario(m.UsuarioId)
                    }).ToList()
                }));

        var egresosPorCategoria = movimientos
            .Where(m => m.Direccion == DireccionMovimiento.Salida)
            .GroupBy(m => m.Categoria)
            .Select(g => new MontoPorCategoriaDto
            {
                Etiqueta = EtiquetaCategoriaMovimiento.Etiqueta(g.Key),
                Monto = g.Sum(m => m.Monto),
                Detalle = g.Select(m => new MovimientoLibroDiarioDto
                {
                    Fecha = m.FechaHora,
                    Concepto = m.Descripcion,
                    Salida = m.Monto,
                    Medio = EtiquetaMetodo(m.MetodoPago),
                    Responsable = !string.IsNullOrWhiteSpace(m.PersonalRelacionado) ? m.PersonalRelacionado : NombreUsuario(m.UsuarioId)
                }).ToList()
            })
            .OrderByDescending(c => c.Monto)
            .ToList();

        List<MontoPorMetodoDto> AgruparPorMetodo(IEnumerable<(MetodoPago metodo, decimal monto)> items) =>
            items.GroupBy(i => i.metodo)
                .Select(g => new MontoPorMetodoDto { Etiqueta = EtiquetaMetodo(g.Key), Monto = g.Sum(i => i.monto) })
                .OrderByDescending(m => m.Monto)
                .ToList();

        var ingresosPorMetodo = AgruparPorMetodo(
            estadias.Where(e => e.MetodoPago.HasValue).Select(e => (e.MetodoPago!.Value, e.TotalAcumulado))
                .Concat(ventas.Where(v => v.MetodoPago.HasValue).Select(v => (v.MetodoPago!.Value, v.Total)))
                .Concat(movimientos.Where(m => m.Direccion == DireccionMovimiento.Ingreso).Select(m => (m.MetodoPago, m.Monto))));

        var egresosPorMetodo = AgruparPorMetodo(
            movimientos.Where(m => m.Direccion == DireccionMovimiento.Salida).Select(m => (m.MetodoPago, m.Monto)));

        return new InformeMensualDto
        {
            IngresoTotal = ingresoHabitacion + ingresoVentas + ingresoMovimientos,
            EgresoTotal = egresoTotal,
            SaldoAnterior = ingresoAnterior - egresoAnterior,
            IngresosPorMetodo = ingresosPorMetodo,
            IngresosPorCategoria = ingresosPorCategoria.OrderByDescending(c => c.Monto).ToList(),
            EgresosPorMetodo = egresosPorMetodo,
            EgresosPorCategoria = egresosPorCategoria,
            LibroDiario = libro
        };
    }

    public async Task<List<EvolucionMesDto>> ObtenerEvolucionAsync(int cantidadMeses)
    {
        var resultado = new List<EvolucionMesDto>();
        var mesActual = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

        for (var i = cantidadMeses - 1; i >= 0; i--)
        {
            var mes = mesActual.AddMonths(-i);
            var (ingreso, egreso) = await CalcularTotalesAsync(mes, mes.AddMonths(1));
            resultado.Add(new EvolucionMesDto
            {
                Anio = mes.Year,
                Mes = mes.Month,
                Etiqueta = $"{NombresMeses[mes.Month - 1][..3].ToLower()}-{mes:yy}",
                Ingreso = ingreso,
                Egreso = egreso
            });
        }

        return resultado;
    }

    /// <summary>Mismo criterio de ingreso/egreso "real" que ObtenerInformeMensualAsync,
    /// pero sumado directo en SQL (sin traer entidades a memoria) — se usa tanto para
    /// el Saldo Anterior como para cada punto de ObtenerEvolucionAsync.</summary>
    private async Task<(decimal ingreso, decimal egreso)> CalcularTotalesAsync(DateTime desde, DateTime hastaExclusivo)
    {
        var ingresoHabitacion = await _context.Estadias
            .Where(e => e.FechaCheckOut != null && e.FechaCheckOut >= desde && e.FechaCheckOut < hastaExclusivo)
            .SumAsync(e => (decimal?)e.TotalAcumulado) ?? 0m;

        var ingresoVentas = await _context.VentasSauna
            .Where(v => v.Estado == EstadoVenta.Pagada && v.Fecha >= desde && v.Fecha < hastaExclusivo)
            .SumAsync(v => (decimal?)v.Total) ?? 0m;

        var ingresoMovimientos = await _context.MovimientosCaja
            .Where(m => m.Direccion == DireccionMovimiento.Ingreso && m.FechaHora >= desde && m.FechaHora < hastaExclusivo)
            .SumAsync(m => (decimal?)m.Monto) ?? 0m;

        var egreso = await _context.MovimientosCaja
            .Where(m => m.Direccion == DireccionMovimiento.Salida && m.FechaHora >= desde && m.FechaHora < hastaExclusivo)
            .SumAsync(m => (decimal?)m.Monto) ?? 0m;

        return (ingresoHabitacion + ingresoVentas + ingresoMovimientos, egreso);
    }

    private static string EtiquetaMetodo(MetodoPago metodo) => metodo switch
    {
        MetodoPago.Efectivo => "Efectivo",
        MetodoPago.Tarjeta => "Tarjeta",
        MetodoPago.Yape => "Yape",
        MetodoPago.Plin => "Plin",
        MetodoPago.Transferencia => "Transferencia",
        _ => metodo.ToString()
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
    public MetodoPago MetodoPago { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;

    public string HoraTexto => FechaHora.ToString("HH:mm:ss");

    public string EtiquetaMetodoPago => MetodoPago switch
    {
        MetodoPago.Efectivo => "Efectivo",
        MetodoPago.Tarjeta => "Tarjeta",
        MetodoPago.Yape => "Yape",
        MetodoPago.Plin => "Plin",
        MetodoPago.Transferencia => "Transferencia",
        _ => MetodoPago.ToString()
    };

    public string EtiquetaDireccion => Direccion == DireccionMovimiento.Ingreso ? "Ingreso de dinero" : "Salida de dinero";

    public string EtiquetaCategoria => EtiquetaCategoriaMovimiento.Etiqueta(Categoria);

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
    public MetodoPago MetodoPago { get; set; } = MetodoPago.Efectivo;
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
    private readonly ISessionService _sessionService;

    public decimal MontoBaseCajaChicaHotel => 250m;
    public decimal MontoBaseCajaChicaSauna => 150m;

    public GastosService(AppDbContext context, IAuditoriaService auditoriaService, ISessionService sessionService)
    {
        _context = context;
        _auditoriaService = auditoriaService;
        _sessionService = sessionService;
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
            MetodoPago = m.MetodoPago,
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
            MetodoPago = dto.MetodoPago,
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
        // Borrar un movimiento de caja es irreversible y no queda ningún registro
        // "antes/después" salvo el texto libre de auditoría — por eso, igual que
        // AuditoriaService.ObtenerRecientesAsync, la regla de rol se repite acá a
        // nivel de servicio y no solo escondiendo el botón en la UI.
        var rol = _sessionService.UsuarioActual?.Rol;
        if (rol != RolUsuario.Gerencia && rol != RolUsuario.Desarrollador)
        {
            throw new UnauthorizedAccessException("No tienes permiso para eliminar movimientos de caja.");
        }

        // AsTracking(): se va a eliminar (Remove) a continuación.
        var movimiento = await _context.MovimientosCaja.AsTracking().FirstOrDefaultAsync(m => m.Id == movimientoId);
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

/// <summary>Cambios permitidos sobre una reserva ya creada — a propósito NO incluye
/// documento/nombre/facturación (eso es la identidad del huésped, no algo que
/// debería cambiar con un "editar"; si está mal, conviene cancelar y crear una
/// nueva). Cubre el pedido más común en la práctica: mover las fechas porque el
/// huésped llamó a cambiar su estadía, o corregir el celular/una observación.</summary>
public class ModificarReservaDto
{
    public int ReservaId { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public string Celular { get; set; } = string.Empty;
    public string? Observaciones { get; set; }
}

public interface IReservaService
{
    Task<List<ReservaCardDto>> ObtenerProximasAsync();
    Task<List<HabitacionDisponibleDto>> ObtenerHabitacionesDisponiblesAsync(DateTime fechaInicio, DateTime fechaFin);
    Task<int> CrearReservaAsync(NuevaReservaDto dto);
    Task ModificarReservaAsync(ModificarReservaDto dto, int usuarioId);
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

    public async Task ModificarReservaAsync(ModificarReservaDto dto, int usuarioId)
    {
        // AsTracking(): se modifica (FechaInicio/FechaFin/Celular/Observaciones) y se guarda.
        var reserva = await _context.Reservas.AsTracking().FirstOrDefaultAsync(r => r.Id == dto.ReservaId);
        if (reserva is null)
        {
            throw new InvalidOperationException("La reserva no existe.");
        }
        if (reserva.Estado != EstadoReserva.Confirmada)
        {
            throw new InvalidOperationException("Solo se pueden modificar reservas Confirmadas (no canceladas ni ya convertidas en Check-in).");
        }

        var fechaInicio = dto.FechaInicio.Date;
        var fechaFin = dto.FechaFin.Date;

        if (fechaFin <= fechaInicio)
        {
            throw new InvalidOperationException("La fecha de salida debe ser posterior a la fecha de entrada.");
        }
        if (fechaInicio < DateTime.Now.Date)
        {
            throw new InvalidOperationException("No se puede mover la reserva a una fecha que ya pasó.");
        }

        // Mismo chequeo de superposición que CrearReservaAsync, excluyendo esta misma
        // reserva (si no, siempre "chocaría" contra sus propias fechas viejas).
        var hayConflicto = await _context.Reservas.AnyAsync(r =>
            r.Id != dto.ReservaId &&
            r.HabitacionId == reserva.HabitacionId &&
            r.Estado == EstadoReserva.Confirmada &&
            r.FechaInicio < fechaFin &&
            fechaInicio < r.FechaFin);

        if (hayConflicto)
        {
            throw new InvalidOperationException("Esa habitación ya tiene otra reserva confirmada que se cruza con esas fechas.");
        }

        var fechaInicioAnterior = reserva.FechaInicio;
        var fechaFinAnterior = reserva.FechaFin;

        reserva.FechaInicio = fechaInicio;
        reserva.FechaFin = fechaFin;
        reserva.Celular = dto.Celular;
        reserva.Observaciones = dto.Observaciones;
        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync(
            "RESERVA_MODIFICADA",
            $"Reserva de {reserva.NombreCompleto} modificada: {fechaInicioAnterior:dd/MM/yyyy}–{fechaFinAnterior:dd/MM/yyyy} pasó a {fechaInicio:dd/MM/yyyy}–{fechaFin:dd/MM/yyyy}.",
            usuarioId, "Reserva", reserva.Id);
    }

    public async Task CancelarReservaAsync(int reservaId, int usuarioId)
    {
        // AsTracking(): se modifica (Estado = Cancelada) y se guarda.
        var reserva = await _context.Reservas.Include(r => r.Habitacion).AsTracking().FirstOrDefaultAsync(r => r.Id == reservaId);
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
        // AsTracking(): se modifica (Estado = CheckInRealizado) y se guarda.
        var reserva = await _context.Reservas.Include(r => r.Acompanantes).AsTracking().FirstOrDefaultAsync(r => r.Id == reservaId);
        if (reserva is null)
        {
            throw new InvalidOperationException("La reserva no existe.");
        }

        if (reserva.Estado != EstadoReserva.Confirmada)
        {
            throw new InvalidOperationException("Esta reserva ya no está confirmada.");
        }

        // AsTracking(): se modifica (Estado = Ocupada) y se guarda; también es la que
        // necesita el ConcurrencyToken de Habitacion.Estado (ver CheckInAsync).
        var habitacion = await _context.Habitaciones.AsTracking().FirstOrDefaultAsync(h => h.Id == reserva.HabitacionId);
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
        try
        {
            await using (var transaccion = await _context.Database.BeginTransactionAsync())
            {
                await _context.SaveChangesAsync();

                reserva.EstadiaId = estadia.Id;
                await _context.SaveChangesAsync();

                await transaccion.CommitAsync();
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            // Mismo caso que en HabitacionService.CheckInAsync: la habitación cambió
            // de estado entre que la leímos y que confirmamos el check-in de la reserva.
            throw new InvalidOperationException("Esta habitación acaba de cambiar de estado (probablemente otra operación la tomó primero). Actualiza la pantalla e intenta de nuevo.");
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
    Mantenimiento,
    Limpieza
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
    public decimal TarifaNoche { get; set; }

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
                TarifaNoche = habitacion.TarifaNoche,
                EstadoActual = habitacion.Estado
            };

            var estadiasHabitacion = estadias.Where(e => e.HabitacionId == habitacion.Id).ToList();
            var reservasHabitacion = reservas.Where(r => r.HabitacionId == habitacion.Id).ToList();

            for (var dia = 1; dia <= cantidadDias; dia++)
            {
                var fecha = new DateTime(anio, mes, dia);
                var esHoy = fecha == hoy;

                // 1) Mantenimiento y Limpieza: solo se pueden saber para HOY (son estados
                //    "actuales" de la habitación, no hay historial por fecha para pintar
                //    días pasados o futuros — ver RegistroLimpieza para el historial real,
                //    que existe para reportes de frecuencia, no para esta grilla).
                if (esHoy && habitacion.Estado == EstadoHabitacion.Mantenimiento)
                {
                    columna.Celdas.Add(new CeldaCalendarioDto { Dia = dia, Estado = EstadoCeldaCalendario.Mantenimiento, EsHoy = true });
                    continue;
                }

                if (esHoy && habitacion.Estado == EstadoHabitacion.LimpiezaSalida)
                {
                    columna.Celdas.Add(new CeldaCalendarioDto { Dia = dia, Estado = EstadoCeldaCalendario.Limpieza, EsHoy = true });
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

/// <summary>Cambio de precio sobre un producto del catálogo del POS — a propósito
/// solo toca precio(s), no nombre/categoría/ícono (eso es catálogo, no algo que
/// cambie con la frecuencia con la que sube un precio). Restringido a
/// Gerencia/Desarrollador, igual que el resto de acciones que afectan cuánto se
/// le cobra a un cliente.</summary>
public class EditarPrecioProductoDto
{
    public int ProductoId { get; set; }
    public decimal Precio { get; set; }
    public decimal PrecioAlquiler { get; set; }
    public decimal PrecioVenta { get; set; }
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

/// <summary>Alta de un artículo nuevo en el Almacén — hasta ahora la lista de
/// insumos era fija (la que trajo el sembrado inicial), sin forma de agregar un
/// producto nuevo que el hotel empiece a comprar sin editar la base a mano.</summary>
public class NuevoInsumoDto
{
    public string Nombre { get; set; } = string.Empty;
    public CategoriaInsumo Categoria { get; set; }
    public string UnidadMedida { get; set; } = string.Empty;
    public int StockInicial { get; set; }
    public int StockMinimo { get; set; }
}

/// <summary>Corrección de un insumo ya existente — a propósito NO incluye
/// StockActual (eso se mueve solo con RegistrarMovimientoAsync, que además deja
/// registro en MovimientoInventario; cambiarlo acá directamente perdería esa
/// trazabilidad).</summary>
public class EditarInsumoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;
    public int StockMinimo { get; set; }
}

public interface IInventarioService
{
    Task<List<InsumoCardDto>> ObtenerInsumosAsync();
    Task RegistrarMovimientoAsync(NuevoMovimientoInventarioDto dto);
    Task<int> CrearInsumoAsync(NuevoInsumoDto dto, int usuarioId);
    Task EditarInsumoAsync(EditarInsumoDto dto, int usuarioId);
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

        // AsTracking(): se modifica (StockActual) y se guarda.
        var insumo = await _context.Insumos.AsTracking().FirstOrDefaultAsync(i => i.Id == dto.InsumoId);
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

    public async Task<int> CrearInsumoAsync(NuevoInsumoDto dto, int usuarioId)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
        {
            throw new InvalidOperationException("El nombre del insumo es obligatorio.");
        }
        if (string.IsNullOrWhiteSpace(dto.UnidadMedida))
        {
            throw new InvalidOperationException("La unidad de medida es obligatoria.");
        }
        if (dto.StockInicial < 0 || dto.StockMinimo < 0)
        {
            throw new InvalidOperationException("El stock inicial y el mínimo no pueden ser negativos.");
        }

        var insumo = new Insumo
        {
            Nombre = dto.Nombre.Trim(),
            Categoria = dto.Categoria,
            UnidadMedida = dto.UnidadMedida.Trim(),
            StockActual = dto.StockInicial,
            StockMinimo = dto.StockMinimo,
            Activo = true
        };

        _context.Insumos.Add(insumo);
        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync(
            "INSUMO_CREADO",
            $"Nuevo insumo '{insumo.Nombre}' ({insumo.UnidadMedida}), stock inicial {insumo.StockActual}, mínimo {insumo.StockMinimo}.",
            usuarioId, "Insumo", insumo.Id);

        return insumo.Id;
    }

    public async Task EditarInsumoAsync(EditarInsumoDto dto, int usuarioId)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
        {
            throw new InvalidOperationException("El nombre del insumo es obligatorio.");
        }
        if (string.IsNullOrWhiteSpace(dto.UnidadMedida))
        {
            throw new InvalidOperationException("La unidad de medida es obligatoria.");
        }
        if (dto.StockMinimo < 0)
        {
            throw new InvalidOperationException("El stock mínimo no puede ser negativo.");
        }

        // AsTracking(): se modifica (Nombre/UnidadMedida/StockMinimo) y se guarda.
        var insumo = await _context.Insumos.AsTracking().FirstOrDefaultAsync(i => i.Id == dto.Id);
        if (insumo is null)
        {
            throw new InvalidOperationException("El insumo no existe.");
        }

        insumo.Nombre = dto.Nombre.Trim();
        insumo.UnidadMedida = dto.UnidadMedida.Trim();
        insumo.StockMinimo = dto.StockMinimo;
        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync(
            "INSUMO_EDITADO",
            $"Insumo '{insumo.Nombre}' editado — mínimo actualizado a {insumo.StockMinimo} {insumo.UnidadMedida}.",
            usuarioId, "Insumo", insumo.Id);
    }
}

public interface ISaunaService
{
    Task<List<ProductoCatalogoDto>> ObtenerCatalogoAsync();
    Task EditarPrecioProductoAsync(EditarPrecioProductoDto dto, int usuarioId);
    Task<List<ClienteSaunaCardDto>> ObtenerClientesActivosAsync();
    Task<int> RegistrarClienteAsync(NuevoClienteSaunaDto dto, int usuarioId);
    Task<List<HabitacionCardDto>> BuscarHuespedesActivosAsync();
    /// <summary>metodoPago es obligatorio cuando cargarAHabitacion es false (se está
    /// cobrando ahora mismo); se ignora cuando es true (se cobra recién al Check-out).
    /// Devuelve el Id de la VentaSauna creada, para poder abrir su comprobante
    /// imprimible (ver ObtenerReciboVentaAsync) cuando se cobró en el momento.</summary>
    Task<int> RegistrarVentaAsync(int clienteSaunaId, List<ItemCarritoDto> items, int usuarioId, bool cargarAHabitacion, MetodoPago? metodoPago);

    /// <summary>Venta de Cafetería/servicios DIRECTA a un huésped de hotel, sin pasar
    /// por un registro de ClienteSauna — para el caso de un huésped que solo quiere
    /// un café o un servicio adicional, sin haber ido al Sauna. Ver CafeteriaPage.
    /// Devuelve el Id de la VentaSauna creada, igual que RegistrarVentaAsync.</summary>
    Task<int> RegistrarVentaHotelAsync(int estadiaId, List<ItemCarritoDto> items, int usuarioId, bool cargarAHabitacion, MetodoPago? metodoPago);

    Task FinalizarSesionAsync(int clienteSaunaId, int usuarioId);

    /// <summary>Datos de una VentaSauna ya cobrada (no cargada a habitación), listos
    /// para imprimir el comprobante. Null si la venta no existe o todavía está
    /// CargadaAHabitacion (esas se imprimen recién al Check-out, con el total final).</summary>
    Task<VentaReciboDto?> ObtenerReciboVentaAsync(int ventaId);
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
    private readonly IComprobanteNumeracionService _comprobanteNumeracionService;
    private readonly ISessionService _sessionService;

    public SaunaService(AppDbContext context, IAuditoriaService auditoriaService, IComprobanteNumeracionService comprobanteNumeracionService, ISessionService sessionService)
    {
        _context = context;
        _auditoriaService = auditoriaService;
        _comprobanteNumeracionService = comprobanteNumeracionService;
        _sessionService = sessionService;
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

    public async Task EditarPrecioProductoAsync(EditarPrecioProductoDto dto, int usuarioId)
    {
        var rol = _sessionService.UsuarioActual?.Rol;
        if (rol != RolUsuario.Gerencia && rol != RolUsuario.Desarrollador)
        {
            throw new UnauthorizedAccessException("No tienes permiso para cambiar precios del catálogo.");
        }

        if (dto.Precio < 0 || dto.PrecioAlquiler < 0 || dto.PrecioVenta < 0)
        {
            throw new InvalidOperationException("Los precios no pueden ser negativos.");
        }

        // AsTracking(): se modifica (Precio/PrecioAlquiler/PrecioVenta) y se guarda.
        var producto = await _context.ProductosPOS.AsTracking().FirstOrDefaultAsync(p => p.Id == dto.ProductoId);
        if (producto is null)
        {
            throw new InvalidOperationException("El producto no existe.");
        }

        var precioAnteriorTexto = producto.EsAlquilerVenta
            ? $"Alquiler S/ {producto.PrecioAlquiler:0.00} / Venta S/ {producto.PrecioVenta:0.00}"
            : $"S/ {producto.Precio:0.00}";

        producto.Precio = dto.Precio;
        producto.PrecioAlquiler = dto.PrecioAlquiler;
        producto.PrecioVenta = dto.PrecioVenta;
        await _context.SaveChangesAsync();

        var precioNuevoTexto = producto.EsAlquilerVenta
            ? $"Alquiler S/ {producto.PrecioAlquiler:0.00} / Venta S/ {producto.PrecioVenta:0.00}"
            : $"S/ {producto.Precio:0.00}";

        await _auditoriaService.RegistrarAsync(
            "PRODUCTO_PRECIO_EDITADO",
            $"Precio de '{producto.Nombre}' cambió de {precioAnteriorTexto} a {precioNuevoTexto}.",
            usuarioId, "ProductoPOS", producto.Id);
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

    /// <summary>Valida el carrito antes de registrar cualquier venta: no vacío, y ningún
    /// ítem con cantidad o precio que pudiera dejar un total negativo o en cero
    /// disfrazado de venta real (afecta directamente el dinero cargado a la
    /// habitación o cobrado en caja).</summary>
    private static void ValidarCarrito(List<ItemCarritoDto> items)
    {
        if (items.Count == 0)
        {
            throw new InvalidOperationException("El carrito está vacío.");
        }

        foreach (var item in items)
        {
            if (item.Cantidad <= 0)
            {
                throw new InvalidOperationException($"La cantidad de \"{item.Descripcion}\" debe ser mayor a cero.");
            }
            if (item.PrecioUnitario < 0)
            {
                throw new InvalidOperationException($"El precio de \"{item.Descripcion}\" no puede ser negativo.");
            }
        }
    }

    public async Task<int> RegistrarVentaAsync(int clienteSaunaId, List<ItemCarritoDto> items, int usuarioId, bool cargarAHabitacion, MetodoPago? metodoPago)
    {
        ValidarCarrito(items);

        if (!cargarAHabitacion && metodoPago is null)
        {
            throw new InvalidOperationException("Indica el método de pago para cobrar esta venta.");
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

            // AsTracking(): se modifica (TotalAcumulado) y se guarda si cargarAHabitacion.
            estadia = await _context.Estadias
                .Include(e => e.Habitacion)
                .AsTracking()
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
            Total = items.Sum(i => i.Subtotal),
            MetodoPago = cargarAHabitacion ? null : metodoPago
        };

        if (!cargarAHabitacion)
        {
            venta.NumeroComprobante = await _comprobanteNumeracionService.ObtenerSiguienteNumeroAsync(cliente.TipoComprobante);
        }

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

        return venta.Id;
    }

    public async Task<int> RegistrarVentaHotelAsync(int estadiaId, List<ItemCarritoDto> items, int usuarioId, bool cargarAHabitacion, MetodoPago? metodoPago)
    {
        ValidarCarrito(items);

        if (!cargarAHabitacion && metodoPago is null)
        {
            throw new InvalidOperationException("Indica el método de pago para cobrar esta venta.");
        }

        // AsTracking(): se modifica (TotalAcumulado) y se guarda si cargarAHabitacion.
        var estadia = await _context.Estadias
            .Include(e => e.Habitacion)
            .AsTracking()
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
            Total = items.Sum(i => i.Subtotal),
            MetodoPago = cargarAHabitacion ? null : metodoPago
        };

        if (!cargarAHabitacion)
        {
            venta.NumeroComprobante = await _comprobanteNumeracionService.ObtenerSiguienteNumeroAsync(estadia.TipoComprobante);
        }

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

        return venta.Id;
    }

    public async Task FinalizarSesionAsync(int clienteSaunaId, int usuarioId)
    {
        // AsTracking(): se modifica (Estado, FechaSalida) y se guarda.
        var cliente = await _context.ClientesSauna.AsTracking().FirstOrDefaultAsync(c => c.Id == clienteSaunaId);
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

    public async Task<VentaReciboDto?> ObtenerReciboVentaAsync(int ventaId)
    {
        var venta = await _context.VentasSauna
            .Include(v => v.Detalles)
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == ventaId);

        if (venta is null || venta.Estado != EstadoVenta.Pagada)
        {
            return null;
        }

        string nombreCliente;
        TipoDocumento tipoDocumento;
        string numeroDocumento;
        TipoComprobante tipoComprobante;
        string? ruc = null;
        string? razonSocial = null;
        int? numeroHabitacion = null;

        if (venta.ClienteSaunaId is { } clienteSaunaId)
        {
            var cliente = await _context.ClientesSauna.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clienteSaunaId);
            nombreCliente = cliente?.NombreCompleto ?? "—";
            tipoDocumento = cliente?.TipoDocumento ?? TipoDocumento.DNI;
            numeroDocumento = cliente?.NumeroDocumento ?? "—";
            tipoComprobante = cliente?.TipoComprobante ?? TipoComprobante.Boleta;
            ruc = cliente?.RUC;
            razonSocial = cliente?.RazonSocial;
        }
        else if (venta.EstadiaHotelDestinoId is { } estadiaId)
        {
            var estadia = await _context.Estadias.Include(e => e.Habitacion).AsNoTracking().FirstOrDefaultAsync(e => e.Id == estadiaId);
            nombreCliente = estadia?.NombreCompleto ?? "—";
            tipoDocumento = estadia?.TipoDocumento ?? TipoDocumento.DNI;
            numeroDocumento = estadia?.NumeroDocumento ?? "—";
            tipoComprobante = estadia?.TipoComprobante ?? TipoComprobante.Boleta;
            ruc = estadia?.RUC;
            razonSocial = estadia?.RazonSocial;
            numeroHabitacion = estadia?.Habitacion?.Numero;
        }
        else
        {
            nombreCliente = "—";
            tipoDocumento = TipoDocumento.DNI;
            numeroDocumento = "—";
            tipoComprobante = TipoComprobante.Boleta;
        }

        return new VentaReciboDto
        {
            VentaId = venta.Id,
            NumeroComprobante = venta.NumeroComprobante ?? "—",
            TipoComprobante = tipoComprobante,
            Fecha = venta.Fecha,
            NombreCliente = nombreCliente,
            TipoDocumento = tipoDocumento,
            NumeroDocumento = numeroDocumento,
            RUC = ruc,
            RazonSocial = razonSocial,
            NumeroHabitacion = numeroHabitacion,
            MetodoPago = venta.MetodoPago,
            Total = venta.Total,
            Items = venta.Detalles.Select(d => new ItemReciboDto
            {
                Descripcion = d.Descripcion,
                Cantidad = d.Cantidad,
                PrecioUnitario = d.PrecioUnitario,
                Subtotal = d.Subtotal
            }).ToList()
        };
    }
}

/// <summary>Un renglón de comprobante (hotel o sauna/cafetería) — ver EstadiaReciboDto
/// y VentaReciboDto.</summary>
public class ItemReciboDto
{
    public string Descripcion { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }
}

/// <summary>Datos listos para imprimir el comprobante de una venta de Sauna o
/// Cafetería ya cobrada — ver ISaunaService.ObtenerReciboVentaAsync.</summary>
public class VentaReciboDto
{
    public int VentaId { get; set; }
    public string NumeroComprobante { get; set; } = string.Empty;
    public TipoComprobante TipoComprobante { get; set; }
    public DateTime Fecha { get; set; }
    public string NombreCliente { get; set; } = string.Empty;
    public TipoDocumento TipoDocumento { get; set; }
    public string NumeroDocumento { get; set; } = string.Empty;
    public string? RUC { get; set; }
    public string? RazonSocial { get; set; }
    public int? NumeroHabitacion { get; set; }
    public MetodoPago? MetodoPago { get; set; }
    public decimal Total { get; set; }
    public List<ItemReciboDto> Items { get; set; } = new();
}

// ============================================================
// REGISTRO DE HUÉSPEDES (MINCETUR) — DTO + Servicio
// Reglamento de Establecimientos de Hospedaje: D.S. N° 001-2015-MINCETUR,
// modificado por D.S. N° 005-2021-MINCETUR.
// ============================================================

/// <summary>Una fila del Registro de Huéspedes exigido por MINCETUR — junta los
/// campos que ya se piden en el Check-in (Estadia) en el formato/columnas que pide
/// la norma. Incluye estadías históricas, no solo las activas: el registro es un
/// libro acumulativo, no una foto del momento.</summary>
public class RegistroHuespedDto
{
    public int EstadiaId { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public DateTime? FechaNacimiento { get; set; }
    public SexoHuesped? Sexo { get; set; }
    public string Nacionalidad { get; set; } = string.Empty;
    public string? LugarResidencia { get; set; }
    public TipoDocumento TipoDocumento { get; set; }
    public string NumeroDocumento { get; set; } = string.Empty;
    public MotivoViaje? MotivoViaje { get; set; }
    public DateTime FechaIngreso { get; set; }
    public DateTime? FechaSalida { get; set; }
    public int NumeroHabitacion { get; set; }
    public decimal Tarifa { get; set; }

    public string EtiquetaTipoDocumento => TipoDocumento switch
    {
        TipoDocumento.Pasaporte => "Pasaporte",
        TipoDocumento.CarneExtranjeria => "Carné Ext.",
        _ => "DNI"
    };

    public string EtiquetaSexo => Sexo switch
    {
        SexoHuesped.Masculino => "M",
        SexoHuesped.Femenino => "F",
        _ => "—"
    };

    // Con tipo completo (no solo "MotivoViaje.Turismo"): la propiedad de arriba se
    // llama igual que el enum, así que el nombre corto queda tapado por la propiedad.
    public string EtiquetaMotivoViaje => MotivoViaje switch
    {
        JKalixto_System.Domain.Models.MotivoViaje.Turismo => "Turismo",
        JKalixto_System.Domain.Models.MotivoViaje.Negocios => "Negocios",
        JKalixto_System.Domain.Models.MotivoViaje.Otro => "Otro",
        _ => "—"
    };

    public string FechaNacimientoTexto => FechaNacimiento?.ToString("dd/MM/yyyy") ?? "—";
    public string FechaIngresoTexto => FechaIngreso.ToString("dd/MM/yyyy HH:mm");
    public string FechaSalidaTexto => FechaSalida?.ToString("dd/MM/yyyy HH:mm") ?? "En curso";
}

public interface IRegistroHuespedesService
{
    Task<List<RegistroHuespedDto>> ObtenerRegistroAsync();
}

public class RegistroHuespedesService : IRegistroHuespedesService
{
    private readonly AppDbContext _context;

    public RegistroHuespedesService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<RegistroHuespedDto>> ObtenerRegistroAsync()
    {
        var estadias = await _context.Estadias
            .Include(e => e.Habitacion)
            .OrderByDescending(e => e.FechaCheckIn)
            .ToListAsync();

        return estadias.Select(e => new RegistroHuespedDto
        {
            EstadiaId = e.Id,
            NombreCompleto = e.NombreCompleto,
            FechaNacimiento = e.FechaNacimiento,
            Sexo = e.Sexo,
            Nacionalidad = e.Nacionalidad,
            LugarResidencia = e.LugarResidencia,
            TipoDocumento = e.TipoDocumento,
            NumeroDocumento = e.NumeroDocumento,
            MotivoViaje = e.MotivoViaje,
            FechaIngreso = e.FechaCheckIn,
            FechaSalida = e.FechaCheckOut,
            NumeroHabitacion = e.Habitacion?.Numero ?? 0,
            Tarifa = e.Habitacion?.TarifaNoche ?? 0
        }).ToList();
    }
}

// ============================================================
// LIBRO DE RECLAMACIONES — DTOs + Servicio
// (Ley N° 29571 — Código de Protección y Defensa del Consumidor —
// D.S. N° 011-2011-PCM y D.S. N° 042-2011-PCM)
// ============================================================

public class NuevoReclamoDto
{
    public string NombreCompleto { get; set; } = string.Empty;
    public string Domicilio { get; set; } = string.Empty;
    public TipoDocumento TipoDocumento { get; set; } = TipoDocumento.DNI;
    public string NumeroDocumento { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public bool EsMenorDeEdad { get; set; }
    public string? NombreApoderado { get; set; }
    public string? DocumentoApoderado { get; set; }
    public string BienContratado { get; set; } = string.Empty;
    public decimal? MontoReclamado { get; set; }
    public TipoReclamoQueja Tipo { get; set; }
    public string DetalleReclamo { get; set; } = string.Empty;
    public string? PedidoConsumidor { get; set; }
}

public class ReclamoCardDto
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string NumeroDocumento { get; set; } = string.Empty;
    public string BienContratado { get; set; } = string.Empty;
    public decimal? MontoReclamado { get; set; }
    public TipoReclamoQueja Tipo { get; set; }
    public string DetalleReclamo { get; set; } = string.Empty;
    public string? PedidoConsumidor { get; set; }
    public EstadoReclamo Estado { get; set; }
    public string? RespuestaEstablecimiento { get; set; }
    public DateTime? FechaRespuesta { get; set; }

    public string FechaTexto => Fecha.ToString("dd/MM/yyyy HH:mm");
    public string EtiquetaTipo => Tipo == TipoReclamoQueja.Reclamo ? "Reclamo" : "Queja";
    public string EtiquetaEstado => Estado == EstadoReclamo.Pendiente ? "Pendiente" : "Respondido";

    /// <summary>Plazo legal para responder: 15 días HÁBILES desde la fecha de
    /// presentación (Ley N° 29571). No descuenta feriados —solo fines de semana—
    /// así que es una referencia, no un cálculo oficial exacto.</summary>
    public DateTime FechaLimiteRespuesta => SumarDiasHabiles(Fecha, 15);

    public bool PlazoVencido => Estado == EstadoReclamo.Pendiente && DateTime.Now > FechaLimiteRespuesta;

    private static DateTime SumarDiasHabiles(DateTime fecha, int diasHabiles)
    {
        var resultado = fecha;
        var contados = 0;
        while (contados < diasHabiles)
        {
            resultado = resultado.AddDays(1);
            if (resultado.DayOfWeek != DayOfWeek.Saturday && resultado.DayOfWeek != DayOfWeek.Sunday)
            {
                contados++;
            }
        }
        return resultado;
    }
}

public interface IReclamosService
{
    Task<int> RegistrarAsync(NuevoReclamoDto dto, int usuarioId);
    Task<List<ReclamoCardDto>> ObtenerTodosAsync();
    Task ResponderAsync(int reclamoId, string respuesta, int usuarioId);
}

/// <summary>Registro de reclamos/quejas del Libro de Reclamaciones. El personal lo
/// llena a pedido del consumidor en el momento (esta app es de uso interno, no un
/// kiosco de autoatención) — sigue siendo válido: la norma exige que el
/// establecimiento LO PONGA A DISPOSICIÓN, no que el consumidor lo opere él mismo.</summary>
public class ReclamosService : IReclamosService
{
    private readonly AppDbContext _context;
    private readonly IAuditoriaService _auditoriaService;

    public ReclamosService(AppDbContext context, IAuditoriaService auditoriaService)
    {
        _context = context;
        _auditoriaService = auditoriaService;
    }

    public async Task<int> RegistrarAsync(NuevoReclamoDto dto, int usuarioId)
    {
        if (string.IsNullOrWhiteSpace(dto.NombreCompleto) || string.IsNullOrWhiteSpace(dto.NumeroDocumento) || string.IsNullOrWhiteSpace(dto.Domicilio))
        {
            throw new InvalidOperationException("Nombre, documento y domicilio del consumidor son obligatorios.");
        }
        if (string.IsNullOrWhiteSpace(dto.BienContratado))
        {
            throw new InvalidOperationException("Indica qué producto o servicio contrató el consumidor.");
        }
        if (string.IsNullOrWhiteSpace(dto.DetalleReclamo))
        {
            throw new InvalidOperationException("El detalle del reclamo o queja es obligatorio.");
        }

        var reclamo = new Reclamo
        {
            Fecha = DateTime.Now,
            NombreCompleto = dto.NombreCompleto.Trim(),
            Domicilio = dto.Domicilio.Trim(),
            TipoDocumento = dto.TipoDocumento,
            NumeroDocumento = dto.NumeroDocumento.Trim(),
            Telefono = dto.Telefono,
            Email = dto.Email,
            EsMenorDeEdad = dto.EsMenorDeEdad,
            NombreApoderado = dto.EsMenorDeEdad ? dto.NombreApoderado : null,
            DocumentoApoderado = dto.EsMenorDeEdad ? dto.DocumentoApoderado : null,
            BienContratado = dto.BienContratado.Trim(),
            MontoReclamado = dto.MontoReclamado,
            Tipo = dto.Tipo,
            DetalleReclamo = dto.DetalleReclamo.Trim(),
            PedidoConsumidor = dto.PedidoConsumidor,
            Estado = EstadoReclamo.Pendiente,
            UsuarioRegistroId = usuarioId
        };

        _context.Reclamos.Add(reclamo);
        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync(
            reclamo.Tipo == TipoReclamoQueja.Reclamo ? "RECLAMO_REGISTRADO" : "QUEJA_REGISTRADA",
            $"{(reclamo.Tipo == TipoReclamoQueja.Reclamo ? "Reclamo" : "Queja")} de {reclamo.NombreCompleto} por \"{reclamo.BienContratado}\".",
            usuarioId, "Reclamo", reclamo.Id);

        return reclamo.Id;
    }

    public async Task<List<ReclamoCardDto>> ObtenerTodosAsync()
    {
        var reclamos = await _context.Reclamos.OrderByDescending(r => r.Fecha).ToListAsync();

        return reclamos.Select(r => new ReclamoCardDto
        {
            Id = r.Id,
            Fecha = r.Fecha,
            NombreCompleto = r.NombreCompleto,
            NumeroDocumento = r.NumeroDocumento,
            BienContratado = r.BienContratado,
            MontoReclamado = r.MontoReclamado,
            Tipo = r.Tipo,
            DetalleReclamo = r.DetalleReclamo,
            PedidoConsumidor = r.PedidoConsumidor,
            Estado = r.Estado,
            RespuestaEstablecimiento = r.RespuestaEstablecimiento,
            FechaRespuesta = r.FechaRespuesta
        }).ToList();
    }

    public async Task ResponderAsync(int reclamoId, string respuesta, int usuarioId)
    {
        if (string.IsNullOrWhiteSpace(respuesta))
        {
            throw new InvalidOperationException("La respuesta no puede estar vacía.");
        }

        // AsTracking(): se modifica (RespuestaEstablecimiento, Estado) y se guarda.
        var reclamo = await _context.Reclamos.AsTracking().FirstOrDefaultAsync(r => r.Id == reclamoId);
        if (reclamo is null)
        {
            throw new InvalidOperationException("El reclamo no existe.");
        }

        reclamo.RespuestaEstablecimiento = respuesta.Trim();
        reclamo.FechaRespuesta = DateTime.Now;
        reclamo.Estado = EstadoReclamo.Respondido;

        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync(
            "RECLAMO_RESPONDIDO",
            $"Se respondió el {(reclamo.Tipo == TipoReclamoQueja.Reclamo ? "reclamo" : "queja")} de {reclamo.NombreCompleto}.",
            usuarioId, "Reclamo", reclamo.Id);
    }
}
