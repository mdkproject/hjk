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

/// <summary>Cómo pagó el cliente — igual que en cualquier POS peruano real (Yape y
/// Plin son, junto con efectivo, los medios más usados en el día a día).</summary>
public enum MetodoPago
{
    Efectivo,
    Tarjeta,
    Yape,
    Plin,
    Transferencia
}

/// <summary>Para el Registro de Huéspedes exigido por MINCETUR (D.S. N° 001-2015-MINCETUR,
/// modificado por D.S. N° 005-2021-MINCETUR) — ver Estadia.MotivoViaje.</summary>
public enum MotivoViaje
{
    Turismo,
    Negocios,
    Otro
}

/// <summary>Sexo del huésped — campo obligatorio del Registro de Huéspedes MINCETUR,
/// no una clasificación general del sistema.</summary>
public enum SexoHuesped
{
    Masculino,
    Femenino
}

public enum TipoComprobante
{
    Boleta,
    Factura
}

/// <summary>Los 3 tipos de documento de identidad que acepta el sistema para registrar
/// a un huésped o cliente (antes solo se aceptaba DNI).</summary>
public enum TipoDocumento
{
    DNI,
    Pasaporte,
    CarneExtranjeria
}

public enum EstadoReserva
{
    Confirmada,
    CheckInRealizado,
    Cancelada
}

/// <summary>
/// Una reserva a futuro para una habitación (rango de fechas), hecha ANTES de que el
/// huésped llegue físicamente. Cuando el huésped llega, la reserva se "convierte" en
/// una Estadia real (Check-in) — ver ReservaService.ConvertirEnCheckInAsync.
/// No confundir con Estadia: Estadia es la ocupación real y actual de la habitación;
/// Reserva es solo una promesa de ocupación futura.
/// </summary>
public class Reserva
{
    public int Id { get; set; }

    public int HabitacionId { get; set; }
    public Habitacion? Habitacion { get; set; }

    public TipoDocumento TipoDocumento { get; set; } = TipoDocumento.DNI;
    public string NumeroDocumento { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string Celular { get; set; } = string.Empty;

    /// <summary>Solo importa la fecha (sin hora) — el día en que empieza la reserva.</summary>
    public DateTime FechaInicio { get; set; }

    /// <summary>Solo importa la fecha (sin hora) — el día en que termina la reserva.</summary>
    public DateTime FechaFin { get; set; }

    public EstadoReserva Estado { get; set; } = EstadoReserva.Confirmada;

    public string? Observaciones { get; set; }

    /// <summary>Cuándo y quién registró la reserva (no cuándo empieza la estadía).</summary>
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    public int UsuarioCreacionId { get; set; }

    /// <summary>Se llena solo cuando Estado pasa a CheckInRealizado.</summary>
    public int? EstadiaId { get; set; }

    // --- Facturación (mismos datos que se piden en el Check-in) ---
    public TipoComprobante TipoComprobante { get; set; } = TipoComprobante.Boleta;
    public string? RUC { get; set; }
    public string? RazonSocial { get; set; }
    public string? CorreoFacturacion { get; set; }

    public List<AcompananteReserva> Acompanantes { get; set; } = new();
}

/// <summary>Acompañante declarado al momento de reservar — igual que Acompanante, pero
/// para una Reserva a futuro en vez de una Estadia ya en curso (ver ConvertirEnCheckInAsync,
/// que copia estos acompañantes a la Estadia real cuando el huésped hace Check-in).</summary>
public class AcompananteReserva
{
    public int Id { get; set; }
    public int ReservaId { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
}

/// <summary>Una estadía = un Check-in hasta su Check-out en una habitación del hotel.</summary>
public class Estadia
{
    public int Id { get; set; }

    public int HabitacionId { get; set; }
    public Habitacion? Habitacion { get; set; }

    // --- Huésped principal (obligatorio) ---
    public TipoDocumento TipoDocumento { get; set; } = TipoDocumento.DNI;
    public string NumeroDocumento { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string Celular { get; set; } = string.Empty;

    // --- Registro de Huéspedes (MINCETUR): campos que exige el Reglamento de
    // Establecimientos de Hospedaje además de los de arriba. Nacionalidad por
    // defecto "Peruana" porque es el caso más común y evita fricción en cada
    // Check-in; el resto queda nullable porque no todo Check-in viejo los tiene. ---
    public DateTime? FechaNacimiento { get; set; }
    public SexoHuesped? Sexo { get; set; }
    public string Nacionalidad { get; set; } = "Peruana";
    public string? LugarResidencia { get; set; }
    public MotivoViaje? MotivoViaje { get; set; }

    public DateTime FechaCheckIn { get; set; }
    public DateTime? FechaCheckOut { get; set; }
    public EstadoEstadia Estado { get; set; } = EstadoEstadia.Activa;

    // --- Facturación ---
    public TipoComprobante TipoComprobante { get; set; } = TipoComprobante.Boleta;
    public string? RUC { get; set; }
    public string? RazonSocial { get; set; }
    public string? CorreoFacturacion { get; set; }

    /// <summary>Serie y correlativo del comprobante (ej: "B001-00000123"), asignado al
    /// Check-out — ver IComprobanteNumeracionService. Es la numeración correlativa que
    /// exige SUNAT para cualquier boleta/factura; NO reemplaza la emisión electrónica
    /// real ante SUNAT (eso requiere contratar un PSE/OSE), pero deja el número listo
    /// para imprimir en un comprobante manual mientras esa integración no exista.</summary>
    public string? NumeroComprobante { get; set; }

    /// <summary>Cómo se cobró el total al hacer Check-out. Nulo mientras la estadía
    /// sigue activa (todavía no se cobró nada).</summary>
    public MetodoPago? MetodoPago { get; set; }

    /// <summary>Se activa siempre al hacer Check-in: da acceso gratuito al Sauna.</summary>
    public bool AccesoSaunaIncluido { get; set; } = true;

    /// <summary>Tarifa de la(s) noche(s) + cargos de sauna (Charge to Room) + penalidades.</summary>
    public decimal TotalAcumulado { get; set; }

    public int UsuarioCheckInId { get; set; }
    public int? UsuarioCheckOutId { get; set; }

    /// <summary>Nota u observación opcional del huésped o de recepción (ej: hora estimada
    /// de llegada, pedido especial) — mismo campo que ya existía en Reserva.</summary>
    public string? Observaciones { get; set; }

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

    public TipoDocumento TipoDocumento { get; set; } = TipoDocumento.DNI;
    public string NumeroDocumento { get; set; } = string.Empty;
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
    Manual,

    /// <summary>Servicios adicionales de Hotel que se cobran vía el mismo POS de
    /// Cafetería (planchado, lavandería, frazada/toalla extra) — ver CafeteriaPage.</summary>
    ServicioHotel
}

/// <summary>Catálogo de artículos que aparecen como botones en el POS.</summary>
public class ProductoPOS
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Precio único — solo se usa si EsAlquilerVenta es false.</summary>
    public decimal Precio { get; set; }

    public CategoriaProducto Categoria { get; set; }
    public string Icono { get; set; } = "🛒";
    public bool RequiereDevolucion { get; set; }
    public bool Activo { get; set; } = true;

    /// <summary>Si es true, este ítem tiene DOS precios (alquiler y venta) en vez de
    /// uno solo — el POS le pregunta al usuario cuál de los dos quiere antes de
    /// agregarlo al carrito. Ej: Toalla, Sandalias, Shorts.</summary>
    public bool EsAlquilerVenta { get; set; }
    public decimal PrecioAlquiler { get; set; }
    public decimal PrecioVenta { get; set; }
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

    /// <summary>Nulo cuando la venta es un cargo DIRECTO a una habitación desde
    /// Cafetería (sin que el huésped haya pasado por el Sauna) — ver
    /// EstadiaHotelDestinoId, que en ese caso es el verdadero destino de la venta.</summary>
    public int? ClienteSaunaId { get; set; }

    public DateTime Fecha { get; set; }
    public decimal Total { get; set; }
    public EstadoVenta Estado { get; set; } = EstadoVenta.Pendiente;
    public int? EstadiaHotelDestinoId { get; set; }
    public int UsuarioId { get; set; }
    public List<DetalleVenta> Detalles { get; set; } = new();

    /// <summary>Nulo cuando la venta se cargó a la habitación (todavía no se cobró
    /// directamente) — se llena solo cuando se cobra en el momento.</summary>
    public MetodoPago? MetodoPago { get; set; }

    /// <summary>Serie y correlativo del comprobante — ver Estadia.NumeroComprobante.</summary>
    public string? NumeroComprobante { get; set; }
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

public enum DireccionMovimiento
{
    Ingreso,
    Salida
}

/// <summary>Categorías de movimiento de caja, basadas en cómo se usa en la operación
/// diaria real (adelantos al personal, gastos del día a día, ajustes manuales).</summary>
public enum CategoriaMovimientoCaja
{
    PagoPersonal,
    GastosDiarios,
    AjusteCaja,
    ConsumoPersonal
}

public enum OrigenCajaChica
{
    Hotel,
    Sauna
}

/// <summary>
/// Cualquier movimiento de dinero que NO sea una venta directa de habitación o del
/// POS de sauna (esas ya se cuentan solas vía Estadia/VentaSauna). Por ejemplo:
/// adelantos al personal, gastos operativos del día, o ajustes manuales de caja.
/// Cada movimiento pertenece a la Caja Chica del Hotel o a la del Sauna, para poder
/// separar el dinero de cada área.
/// </summary>
public class MovimientoCaja
{
    public int Id { get; set; }
    public DateTime FechaHora { get; set; } = DateTime.Now;
    public DireccionMovimiento Direccion { get; set; }
    public CategoriaMovimientoCaja Categoria { get; set; }
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>Opcional — a qué persona del personal se refiere (ej: un adelanto).</summary>
    public string? PersonalRelacionado { get; set; }

    public decimal Monto { get; set; }
    public OrigenCajaChica OrigenCaja { get; set; }
    public MetodoPago MetodoPago { get; set; } = MetodoPago.Efectivo;
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

// ============================================================
// MÓDULO FACTURACIÓN — numeración de comprobantes
// ============================================================

/// <summary>
/// Contador de serie-correlativo por Serie+Tipo de comprobante (ej: la fila
/// "B001"/Boleta guarda el último correlativo usado, 123, y el próximo
/// comprobante recibe "B001-00000124"). Es la parte de la numeración SUNAT que
/// SÍ se puede resolver localmente (que nunca se repita ni se salte un número);
/// la emisión electrónica real ante SUNAT es un paso aparte que requiere
/// contratar un PSE/OSE — ver IComprobanteNumeracionService.
/// </summary>
public class NumeracionComprobante
{
    public int Id { get; set; }
    public TipoComprobante Tipo { get; set; }
    public string Serie { get; set; } = string.Empty;
    public int UltimoCorrelativo { get; set; }
}

// ============================================================
// MÓDULO ATENCIÓN AL CLIENTE — Libro de Reclamaciones
// (Ley N° 29571, D.S. N° 011-2011-PCM y D.S. N° 042-2011-PCM: todo
// establecimiento comercial en Perú debe tener uno, físico o virtual)
// ============================================================

/// <summary>"Reclamo" = disconformidad con el producto/servicio en sí;
/// "Queja" = malestar con la atención recibida — distinción exigida por la norma.</summary>
public enum TipoReclamoQueja
{
    Reclamo,
    Queja
}

public enum EstadoReclamo
{
    Pendiente,
    Respondido
}

/// <summary>
/// Una entrada del Libro de Reclamaciones. El personal la registra en el momento
/// (ver ReclamosService) a pedido del consumidor — el establecimiento tiene 15
/// días hábiles (por ley) para responder. Campos según la Guía Informativa de
/// INDECOPI y los D.S. 011-2011-PCM / 042-2011-PCM.
/// </summary>
public class Reclamo
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;

    // --- Datos del consumidor (obligatorios) ---
    public string NombreCompleto { get; set; } = string.Empty;
    public string Domicilio { get; set; } = string.Empty;
    public TipoDocumento TipoDocumento { get; set; } = TipoDocumento.DNI;
    public string NumeroDocumento { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Email { get; set; }

    // --- Si el consumidor es menor de edad: datos del padre/madre/apoderado ---
    public bool EsMenorDeEdad { get; set; }
    public string? NombreApoderado { get; set; }
    public string? DocumentoApoderado { get; set; }

    // --- El reclamo/queja en sí ---
    /// <summary>Qué se contrató (ej: "Hospedaje habitación 305", "Consumo de Cafetería").</summary>
    public string BienContratado { get; set; } = string.Empty;
    public decimal? MontoReclamado { get; set; }
    public TipoReclamoQueja Tipo { get; set; }
    public string DetalleReclamo { get; set; } = string.Empty;
    public string? PedidoConsumidor { get; set; }

    // --- Respuesta del establecimiento (dentro de 15 días hábiles, por ley) ---
    public EstadoReclamo Estado { get; set; } = EstadoReclamo.Pendiente;
    public string? RespuestaEstablecimiento { get; set; }
    public DateTime? FechaRespuesta { get; set; }

    public int UsuarioRegistroId { get; set; }
}

// ============================================================
// MÓDULO ALMACÉN / INVENTARIO
// ============================================================

public enum CategoriaInsumo
{
    HotelHabitaciones,
    HotelCocina,
    Sauna
}

/// <summary>Un artículo de stock del almacén (blancos de habitación, insumos de cocina,
/// insumos de sauna) — distinto de ProductoPOS, que es lo que se LE VENDE al cliente.
/// Un Insumo es lo que el hotel consume/usa internamente y hay que reponer.</summary>
public class Insumo
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public CategoriaInsumo Categoria { get; set; }

    /// <summary>Ej: "unidad", "docena", "litro", "caja", "par".</summary>
    public string UnidadMedida { get; set; } = string.Empty;

    public int StockActual { get; set; }

    /// <summary>Debajo de este número, el Almacén lo marca como stock bajo (alerta visual).</summary>
    public int StockMinimo { get; set; }

    public bool Activo { get; set; } = true;
}

public enum TipoMovimientoInventario
{
    Entrada,
    Salida
}

/// <summary>Historial de reposición (Entrada) o consumo/baja (Salida) de un Insumo.</summary>
public class MovimientoInventario
{
    public int Id { get; set; }
    public int InsumoId { get; set; }
    public TipoMovimientoInventario Tipo { get; set; }
    public int Cantidad { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public DateTime FechaHora { get; set; } = DateTime.Now;
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
