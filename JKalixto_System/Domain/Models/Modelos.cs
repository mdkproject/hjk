using System;
using System.Collections.Generic;

namespace JKalixto_System.Domain.Models;

/// <summary>
/// Roles del sistema. Definen qué puede ver y hacer cada usuario.
/// Ver tabla de permisos en el documento maestro (sección "Seguridad y Roles").
/// </summary>
public enum RolUsuario
{
    Gerencia,
    Recepcionista,
    Desarrollador
}

/// <summary>
/// Usuario que puede iniciar sesión en el sistema (gerentes, recepcionistas, etc).
/// Esta es la primera entidad del dominio. En las próximas sesiones se agregarán aquí
/// Habitacion, Huesped, ClienteSauna, ProductoPOS, LogAuditoria, etc.
/// </summary>
public class Usuario
{
    public int Id { get; set; }

    /// <summary>Nombre de usuario para el login. Ej: "lilian.chacon"</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Contraseña ya encriptada con BCrypt. NUNCA se guarda en texto plano.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string NombreCompleto { get; set; } = string.Empty;

    public RolUsuario Rol { get; set; }

    /// <summary>Si es false, el usuario no puede iniciar sesión (baja lógica, no se borra).</summary>
    public bool Activo { get; set; } = true;

    public DateTime FechaCreacion { get; set; }
}

// ============================================================
// MÓDULO HOTEL
// ============================================================

public enum TipoHabitacion
{
    Simple,
    Matrimonial,
    Doble,
    Familiar,
    Suite
}

public enum EstadoHabitacion
{
    Disponible,
    Ocupada,
    LimpiezaSalida,
    Mantenimiento
}

/// <summary>Una de las 36 habitaciones físicas del hotel.</summary>
public class Habitacion
{
    public int Id { get; set; }

    /// <summary>Ej: 101, 205, 408.</summary>
    public int Numero { get; set; }

    public int Piso { get; set; }

    public TipoHabitacion Tipo { get; set; }

    public EstadoHabitacion Estado { get; set; } = EstadoHabitacion.Disponible;

    public decimal TarifaNoche { get; set; }

    public int CapacidadMax { get; set; }

    /// <summary>Obligatorio mientras Estado == Mantenimiento (regla anti-fraude).</summary>
    public string? MotivoMantenimiento { get; set; }

    public DateTime? FechaInicioMantenimiento { get; set; }
}

public enum EstadoEstadia
{
    Activa,
    Finalizada
}

public enum TipoComprobante
{
    Boleta,
    Factura
}

/// <summary>Una estadía = un Check-in hasta su Check-out en una habitación del hotel.</summary>
public class Estadia
{
    public int Id { get; set; }

    public int HabitacionId { get; set; }
    public Habitacion? Habitacion { get; set; }

    // --- Huésped principal (obligatorio) ---
    public string DNI { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string Celular { get; set; } = string.Empty;

    public DateTime FechaCheckIn { get; set; }
    public DateTime? FechaCheckOut { get; set; }
    public EstadoEstadia Estado { get; set; } = EstadoEstadia.Activa;

    // --- Facturación ---
    public TipoComprobante TipoComprobante { get; set; } = TipoComprobante.Boleta;
    public string? RUC { get; set; }
    public string? RazonSocial { get; set; }
    public string? CorreoFacturacion { get; set; }

    /// <summary>Se activa siempre al hacer Check-in: da acceso gratuito al Sauna.</summary>
    public bool AccesoSaunaIncluido { get; set; } = true;

    /// <summary>Tarifa de la(s) noche(s) + cargos de sauna (Charge to Room) + penalidades.</summary>
    public decimal TotalAcumulado { get; set; }

    public int UsuarioCheckInId { get; set; }
    public int? UsuarioCheckOutId { get; set; }

    public List<Acompanante> Acompanantes { get; set; } = new();
}

/// <summary>Acompañante del huésped principal (solo nombre, es opcional).</summary>
public class Acompanante
{
    public int Id { get; set; }
    public int EstadiaId { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
}

// ============================================================
// MÓDULO SAUNA Y POS
// (modelos listos desde ahora; la pantalla se construye en el siguiente bloque)
// ============================================================

public enum SeccionSauna
{
    Damas,
    General
}

public enum EstadoClienteSauna
{
    Activo,
    Finalizado
}

public class ClienteSauna
{
    public int Id { get; set; }

    public string DNI { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string NumeroCandado { get; set; } = string.Empty;
    public SeccionSauna Seccion { get; set; }
    public string? Observacion { get; set; }

    public DateTime FechaIngreso { get; set; }
    public DateTime? FechaSalida { get; set; }
    public EstadoClienteSauna Estado { get; set; } = EstadoClienteSauna.Activo;

    /// <summary>True si entró gratis por ser huésped del hotel (ver EstadiaHotelId).</summary>
    public bool EsHuespedHotel { get; set; }
    public int? EstadiaHotelId { get; set; }

    public TipoComprobante TipoComprobante { get; set; } = TipoComprobante.Boleta;
    public string? RUC { get; set; }
    public string? RazonSocial { get; set; }
}

public enum CategoriaProducto
{
    AlquilerSauna,
    VentaSauna,
    BebidaCafeteria,
    AlimentoCafeteria,
    Manual
}

/// <summary>Catálogo de artículos que aparecen como botones en el POS.</summary>
public class ProductoPOS
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public CategoriaProducto Categoria { get; set; }
    public string Icono { get; set; } = "🛒";
    public bool RequiereDevolucion { get; set; }
    public bool Activo { get; set; } = true;
}

public enum EstadoVenta
{
    Pendiente,
    Pagada,
    CargadaAHabitacion,
    Anulada
}

public class VentaSauna
{
    public int Id { get; set; }
    public int ClienteSaunaId { get; set; }
    public DateTime Fecha { get; set; }
    public decimal Total { get; set; }
    public EstadoVenta Estado { get; set; } = EstadoVenta.Pendiente;
    public int? EstadiaHotelDestinoId { get; set; }
    public int UsuarioId { get; set; }
    public List<DetalleVenta> Detalles { get; set; } = new();
}

public class DetalleVenta
{
    public int Id { get; set; }
    public int VentaSaunaId { get; set; }
    public int? ProductoId { get; set; }

    /// <summary>Nombre del producto o descripción manual ("Ítem manual").</summary>
    public string Descripcion { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }
}

public class Penalidad
{
    public int Id { get; set; }
    public decimal Monto { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public int? ClienteSaunaId { get; set; }
    public int? EstadiaHotelId { get; set; }
    public int UsuarioId { get; set; }
}

public enum TurnoCaja
{
    Manana,
    Tarde,
    Noche
}

public class CierreCaja
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public TurnoCaja Turno { get; set; }
    public decimal TotalHotel { get; set; }
    public decimal TotalSauna { get; set; }
    public DateTime FechaCierre { get; set; }
    public int UsuarioId { get; set; }
}

/// <summary>Registro inmutable de auditoría. Ver lista de TipoAccion en el documento maestro.</summary>
public class LogAuditoria
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string TipoAccion { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public int UsuarioId { get; set; }

    /// <summary>Nombre del usuario al momento de la acción (guardado aparte para que el log nunca cambie si el usuario se edita después).</summary>
    public string UsuarioNombre { get; set; } = string.Empty;
    public string EntidadAfectada { get; set; } = string.Empty;
    public int? EntidadId { get; set; }
}
