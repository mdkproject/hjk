using System;
using System.Collections.Generic;
using System.Linq;
using JKalixto_System.Domain.Models;

namespace JKalixto_System.Infrastructure.Data;

/// <summary>
/// Genera datos de prueba ALEATORIOS Y FICTICIOS (huéspedes de hotel, clientes de
/// sauna, consumos de POS, reservas a futuro) para no tener que probar el sistema
/// con una base de datos vacía. Los nombres, DNI y celulares son inventados al azar
/// — no corresponden a personas reales.
///
/// Todo es SÍNCRONO a propósito: esto se llama desde MauiProgram.cs, dentro de
/// InicializarBaseDeDatos, que corre antes de que exista cualquier pantalla y que
/// ya es síncrona (usa EnsureCreated(), no EnsureCreatedAsync()). Mezclar async
/// ahí adentro podría colgar el arranque de la app, así que se evita del todo.
///
/// Se activa/desactiva desde MauiProgram.cs (ver GenerarDatosDePrueba). Como la BD
/// se recrea en cada arranque (modo desarrollo), esto corre una vez por sesión y
/// genera combinaciones distintas cada vez.
/// </summary>
public static class DatosPruebaSeeder
{
    private static readonly Random _random = new();

    private static readonly string[] NombresMujer =
    {
        "María", "Rosa", "Carmen", "Ana", "Luz", "Milagros", "Katherine", "Yesenia",
        "Diana", "Gabriela", "Fiorella", "Andrea", "Pamela", "Karen", "Xiomara"
    };

    private static readonly string[] NombresHombre =
    {
        "Carlos", "José", "Luis", "Jorge", "Miguel", "Renato", "Fernando", "Diego",
        "Alberto", "Ricardo", "Eduardo", "Julio", "Manuel", "Víctor", "Rodrigo"
    };

    private static readonly string[] Apellidos =
    {
        "Quispe", "Mamani", "Flores", "Huamán", "Condori", "Gonzales", "Rodríguez",
        "Torres", "Vargas", "Chávez", "Ramos", "Salas", "Chuquimia", "Apaza",
        "Cárdenas", "Medina", "Paredes", "Zapata", "Ochoa", "Delgado"
    };

    public static string NombreCompletoAleatorio()
    {
        var esMujer = _random.Next(2) == 0;
        var nombre = esMujer
            ? NombresMujer[_random.Next(NombresMujer.Length)]
            : NombresHombre[_random.Next(NombresHombre.Length)];
        var apellidoPaterno = Apellidos[_random.Next(Apellidos.Length)];
        var apellidoMaterno = Apellidos[_random.Next(Apellidos.Length)];
        return $"{nombre} {apellidoPaterno} {apellidoMaterno}";
    }

    /// <summary>DNI peruano: 8 dígitos.</summary>
    public static string DniAleatorio() => _random.Next(10_000_000, 80_000_000).ToString();

    /// <summary>Celular peruano: 9 dígitos, empieza con 9.</summary>
    public static string CelularAleatorio() => "9" + _random.Next(10_000_000, 99_999_999);

    public static void Sembrar(AppDbContext db, int usuarioId)
    {
        // Por si esto se corre más de una vez sin borrar la BD antes: no duplica nada.
        if (db.Estadias.Any() || db.ClientesSauna.Any())
        {
            return;
        }

        var estadiasCreadas = SembrarHuespedesHotel(db, usuarioId);
        SembrarEstadosVariosDeHabitaciones(db);
        var clientesCreados = SembrarClientesSauna(db, estadiasCreadas);
        SembrarConsumos(db, usuarioId, clientesCreados);
        SembrarReservas(db, usuarioId);
        SembrarGastos(db, usuarioId);
    }

    /// <summary>Entre 4 y 6 movimientos de Caja Chica (Gastos) de ejemplo, con montos y
    /// categorías al azar, repartidos entre Hotel y Sauna — para poder probar el módulo
    /// de Gastos sin arrancar con la lista vacía. Mismo criterio que el resto del
    /// seeder: aleatorio y ficticio, se regenera en cada arranque.</summary>
    private static void SembrarGastos(AppDbContext db, int usuarioId)
    {
        var motivosGastosDiarios = new[]
        {
            "Compra de insumos de limpieza",
            "Pedido de gaseosas y snacks",
            "Reparación menor de grifería",
            "Compra de útiles de oficina",
            "Combustible para movilidad"
        };

        var categorias = new[]
        {
            CategoriaMovimientoCaja.GastosDiarios,
            CategoriaMovimientoCaja.PagoPersonal,
            CategoriaMovimientoCaja.ConsumoPersonal,
            CategoriaMovimientoCaja.AjusteCaja
        };

        var cantidadMovimientos = _random.Next(4, 7);
        for (var i = 0; i < cantidadMovimientos; i++)
        {
            var categoria = categorias[_random.Next(categorias.Length)];
            var esPagoOConsumoPersonal = categoria is CategoriaMovimientoCaja.PagoPersonal or CategoriaMovimientoCaja.ConsumoPersonal;

            // Ajuste de Caja normalmente es Salida (faltante), pero 1 de cada 4 es
            // Ingreso (sobrante) — el resto de categorías siempre es Salida de dinero.
            var direccion = categoria == CategoriaMovimientoCaja.AjusteCaja && _random.Next(4) == 0
                ? DireccionMovimiento.Ingreso
                : DireccionMovimiento.Salida;

            var descripcion = categoria switch
            {
                CategoriaMovimientoCaja.PagoPersonal => "Adelanto de sueldo",
                CategoriaMovimientoCaja.ConsumoPersonal => "Almuerzo del personal",
                CategoriaMovimientoCaja.AjusteCaja => direccion == DireccionMovimiento.Ingreso
                    ? "Ajuste — sobrante de caja"
                    : "Ajuste — faltante de caja",
                _ => motivosGastosDiarios[_random.Next(motivosGastosDiarios.Length)]
            };

            db.MovimientosCaja.Add(new MovimientoCaja
            {
                FechaHora = DateTime.Now.AddHours(-_random.Next(1, 20)),
                Direccion = direccion,
                Categoria = categoria,
                Descripcion = descripcion,
                PersonalRelacionado = esPagoOConsumoPersonal ? NombreCompletoAleatorio() : null,
                Monto = esPagoOConsumoPersonal ? _random.Next(20, 80) : _random.Next(10, 150),
                OrigenCaja = _random.Next(2) == 0 ? OrigenCajaChica.Hotel : OrigenCajaChica.Sauna,
                UsuarioId = usuarioId
            });
        }

        db.SaveChanges();
    }

    /// <summary>Deja algunas habitaciones adicionales en Limpieza y Mantenimiento (además
    /// de las que quedaron Ocupadas en SembrarHuespedesHotel), para poder ver y probar
    /// visualmente los 4 estados posibles de una habitación desde el primer arranque.</summary>
    private static void SembrarEstadosVariosDeHabitaciones(AppDbContext db)
    {
        var disponibles = db.Habitaciones
            .Where(h => h.Estado == EstadoHabitacion.Disponible)
            .ToList()
            .OrderBy(_ => _random.Next())
            .ToList();

        var paraLimpieza = disponibles.Take(3).ToList();
        var paraMantenimiento = disponibles.Skip(3).Take(2).ToList();

        var motivosMantenimiento = new[]
        {
            "Aire acondicionado no enfría",
            "Fuga de agua en el baño",
            "Cambio de colchón pendiente"
        };

        foreach (var habitacion in paraLimpieza)
        {
            habitacion.Estado = EstadoHabitacion.LimpiezaSalida;
        }

        foreach (var habitacion in paraMantenimiento)
        {
            habitacion.Estado = EstadoHabitacion.Mantenimiento;
            habitacion.MotivoMantenimiento = motivosMantenimiento[_random.Next(motivosMantenimiento.Length)];
            habitacion.FechaInicioMantenimiento = DateTime.Now.AddHours(-_random.Next(1, 20));
        }

        db.SaveChanges();
    }

    /// <summary>5 huéspedes con Check-in ya hecho, en habitaciones elegidas al azar, con horas
    /// de ingreso escalonadas (entre 1 y 30 horas atrás) para que los totales no sean todos iguales.</summary>
    private static List<Estadia> SembrarHuespedesHotel(AppDbContext db, int usuarioId)
    {
        var disponibles = db.Habitaciones
            .Where(h => h.Estado == EstadoHabitacion.Disponible)
            .ToList();

        // Mezcla en memoria (no se puede pedirle a SQLite un ORDER BY con Guid.NewGuid() de forma segura).
        var elegidas = disponibles.OrderBy(_ => _random.Next()).Take(5).ToList();
        var estadias = new List<Estadia>();

        foreach (var habitacion in elegidas)
        {
            var horasAtras = _random.Next(1, 31);
            var conAcompanante = _random.Next(3) == 0; // 1 de cada 3, aprox.

            var estadia = new Estadia
            {
                HabitacionId = habitacion.Id,
                TipoDocumento = TipoDocumento.DNI,
                NumeroDocumento = DniAleatorio(),
                NombreCompleto = NombreCompletoAleatorio(),
                Celular = CelularAleatorio(),
                FechaCheckIn = DateTime.Now.AddHours(-horasAtras),
                Estado = EstadoEstadia.Activa,
                TipoComprobante = TipoComprobante.Boleta,
                AccesoSaunaIncluido = true,
                TotalAcumulado = habitacion.TarifaNoche,
                UsuarioCheckInId = usuarioId
            };

            if (conAcompanante)
            {
                estadia.Acompanantes.Add(new Acompanante { NombreCompleto = NombreCompletoAleatorio() });
            }

            habitacion.Estado = EstadoHabitacion.Ocupada;
            db.Estadias.Add(estadia);
            estadias.Add(estadia);
        }

        db.SaveChanges();
        return estadias;
    }

    /// <summary>5 clientes de sauna. Los primeros 2 quedan vinculados a huéspedes del hotel
    /// recién creados (entrada gratis), el resto son clientes externos.</summary>
    private static List<ClienteSauna> SembrarClientesSauna(AppDbContext db, List<Estadia> estadiasHotel)
    {
        var clientes = new List<ClienteSauna>();

        for (var i = 0; i < 5; i++)
        {
            var esHuespedHotel = i < 2 && i < estadiasHotel.Count;
            var estadiaVinculada = esHuespedHotel ? estadiasHotel[i] : null;
            var horasAtras = _random.Next(0, 6);

            var cliente = new ClienteSauna
            {
                TipoDocumento = TipoDocumento.DNI,
                NumeroDocumento = estadiaVinculada?.NumeroDocumento ?? DniAleatorio(),
                NombreCompleto = estadiaVinculada?.NombreCompleto ?? NombreCompletoAleatorio(),
                NumeroCandado = (i + 1).ToString("00"),
                Seccion = _random.Next(2) == 0 ? SeccionSauna.Damas : SeccionSauna.General,
                FechaIngreso = DateTime.Now.AddHours(-horasAtras),
                Estado = EstadoClienteSauna.Activo,
                EsHuespedHotel = esHuespedHotel,
                EstadiaHotelId = estadiaVinculada?.Id
            };

            db.ClientesSauna.Add(cliente);
            clientes.Add(cliente);
        }

        db.SaveChanges();
        return clientes;
    }

    /// <summary>Un par de ventas de POS por cliente (bebida/comida/alquiler), tomadas del
    /// catálogo real. Al primer cliente vinculado a un huésped se le carga a la habitación,
    /// para que se pueda ver ese caso funcionando de una.</summary>
    private static void SembrarConsumos(AppDbContext db, int usuarioId, List<ClienteSauna> clientes)
    {
        var catalogo = db.ProductosPOS.Where(p => p.Activo).ToList();
        if (catalogo.Count == 0)
        {
            return;
        }

        var primerCargoAHabitacionHecho = false;

        foreach (var cliente in clientes)
        {
            var cantidadItems = _random.Next(1, 4);
            var itemsElegidos = catalogo.OrderBy(_ => _random.Next()).Take(cantidadItems).ToList();

            var esCargoAHabitacion = cliente.EsHuespedHotel && cliente.EstadiaHotelId.HasValue && !primerCargoAHabitacionHecho;

            var venta = new VentaSauna
            {
                ClienteSaunaId = cliente.Id,
                Fecha = cliente.FechaIngreso.AddMinutes(_random.Next(10, 90)),
                Estado = esCargoAHabitacion ? EstadoVenta.CargadaAHabitacion : EstadoVenta.Pagada,
                EstadiaHotelDestinoId = esCargoAHabitacion ? cliente.EstadiaHotelId : null,
                UsuarioId = usuarioId
            };

            var total = 0m;
            foreach (var producto in itemsElegidos)
            {
                var cantidad = _random.Next(1, 3);

                string descripcion;
                decimal precioUnitario;
                if (producto.EsAlquilerVenta)
                {
                    var esAlquiler = _random.Next(2) == 0;
                    descripcion = esAlquiler ? $"{producto.Nombre} (Alquiler)" : $"{producto.Nombre} (Venta)";
                    precioUnitario = esAlquiler ? producto.PrecioAlquiler : producto.PrecioVenta;
                }
                else
                {
                    descripcion = producto.Nombre;
                    precioUnitario = producto.Precio;
                }

                var subtotal = precioUnitario * cantidad;
                total += subtotal;

                venta.Detalles.Add(new DetalleVenta
                {
                    ProductoId = producto.Id,
                    Descripcion = descripcion,
                    Cantidad = cantidad,
                    PrecioUnitario = precioUnitario,
                    Subtotal = subtotal
                });
            }
            venta.Total = total;

            db.VentasSauna.Add(venta);

            if (esCargoAHabitacion)
            {
                var estadia = db.Estadias.FirstOrDefault(e => e.Id == cliente.EstadiaHotelId!.Value);
                if (estadia is not null)
                {
                    estadia.TotalAcumulado += total;
                }
                primerCargoAHabitacionHecho = true;
            }
        }

        db.SaveChanges();
    }

    /// <summary>2 reservas a futuro, en habitaciones que quedaron libres (no las que se
    /// usaron para los Check-in de arriba), para poder probar el módulo de Reservas.</summary>
    private static void SembrarReservas(AppDbContext db, int usuarioId)
    {
        var libres = db.Habitaciones
            .Where(h => h.Estado == EstadoHabitacion.Disponible)
            .ToList();

        var elegidas = libres.OrderBy(_ => _random.Next()).Take(2).ToList();

        var diasDesdeHoy = 3;
        foreach (var habitacion in elegidas)
        {
            var fechaInicio = DateTime.Now.Date.AddDays(diasDesdeHoy);
            var fechaFin = fechaInicio.AddDays(_random.Next(1, 4));

            db.Reservas.Add(new Reserva
            {
                HabitacionId = habitacion.Id,
                TipoDocumento = TipoDocumento.DNI,
                NumeroDocumento = DniAleatorio(),
                NombreCompleto = NombreCompletoAleatorio(),
                Celular = CelularAleatorio(),
                FechaInicio = fechaInicio,
                FechaFin = fechaFin,
                Estado = EstadoReserva.Confirmada,
                Observaciones = "Reserva de prueba generada automáticamente.",
                FechaCreacion = DateTime.Now,
                UsuarioCreacionId = usuarioId
            });

            diasDesdeHoy += 5;
        }

        db.SaveChanges();
    }
}
