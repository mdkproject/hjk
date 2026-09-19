using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;
using JKalixto_System.Application.Services;
using JKalixto_System.Domain.Models;
using JKalixto_System.Infrastructure.Data;

namespace JKalixto_System.Tests;

/// <summary>
/// Simulación acelerada de operación diaria (timelapse): recorre N "días"
/// simulados haciendo una mezcla realista de check-ins, check-outs, reservas,
/// clientes de sauna, ventas, gastos, movimientos de inventario, mantenimiento
/// y reclamos — como uno o dos meses de trabajo del hotel, corridos en
/// segundos. El objetivo NO es probar una función puntual (para eso están los
/// demás archivos de test) sino encontrar problemas que solo aparecen con
/// volumen y variedad acumulada: habitaciones que quedan trabadas, stock que
/// se va negativo, un cierre de caja que debería rechazarse y no lo hace, etc.
///
/// LIMITACIÓN CONOCIDA Y A PROPÓSITO: Check-in/Check-out usan DateTime.Now
/// internamente (no reciben una fecha como parámetro) — no hay forma de
/// "viajar en el tiempo" sin cambiar esas firmas, así que todas las Estadias
/// de esta simulación quedan con la fecha real de hoy. Lo que sí avanza con
/// fechas reales distintas son las Reservas (si aceptan FechaInicio/FechaFin
/// explícitas). Lo que se acelera acá es el VOLUMEN y la VARIEDAD de
/// operaciones acumuladas, no el calendario real.
/// </summary>
public class SimulacionOperativaTests
{
    private readonly ITestOutputHelper _output;

    public SimulacionOperativaTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task SimulacionDeUnMesDeOperacion_NoRompeInvariantesDelSistema()
    {
        using var bd = new BaseDeDatosDePrueba();
        var contexto = bd.Contexto;

        var sesion = new SessionService();
        var auditoria = new AuditoriaService(contexto, sesion);
        var comprobantes = new ComprobanteNumeracionService(contexto);
        var habitacionesSvc = new HabitacionService(contexto, auditoria, comprobantes);
        var reservasSvc = new ReservaService(contexto, auditoria);
        var saunaSvc = new SaunaService(contexto, auditoria, comprobantes);
        var gastosSvc = new GastosService(contexto, auditoria, sesion);
        var inventarioSvc = new InventarioService(contexto, auditoria);
        var cierreCajaSvc = new CierreCajaService(contexto, auditoria, gastosSvc);
        var reclamosSvc = new ReclamosService(contexto, auditoria);

        // Semilla fija: la simulación es aleatoria PERO reproducible — si algo
        // falla, se puede correr de nuevo y va a fallar exactamente igual.
        var random = new Random(20260101);

        var usuarios = await contexto.Usuarios.Where(u => u.Activo).Select(u => u.Id).ToListAsync();
        var catalogo = await saunaSvc.ObtenerCatalogoAsync();
        int UsuarioAlAzar() => usuarios[random.Next(usuarios.Count)];

        var stats = new Dictionary<string, int>();
        void Contar(string clave, int cantidad = 1) => stats[clave] = stats.GetValueOrDefault(clave) + cantidad;
        var hallazgos = new List<string>();

        const int diasASimular = 45;

        for (var dia = 1; dia <= diasASimular; dia++)
        {
            // ------------------------------------------------------------
            // 1) CHECK-INS — 1 a 3 por día, en habitaciones Disponibles
            // ------------------------------------------------------------
            var disponibles = (await habitacionesSvc.ObtenerTodasAsync())
                .Where(h => h.Estado == EstadoHabitacion.Disponible)
                .OrderBy(_ => random.Next())
                .Take(random.Next(1, 4))
                .ToList();

            foreach (var hab in disponibles)
            {
                var esFactura = random.Next(4) == 0;
                var dto = new NuevoCheckInDto
                {
                    HabitacionId = hab.HabitacionId,
                    TipoDocumento = TipoDocumento.DNI,
                    NumeroDocumento = DatosPruebaSeeder.DniAleatorio(),
                    NombreCompleto = DatosPruebaSeeder.NombreCompletoAleatorio(),
                    Celular = DatosPruebaSeeder.CelularAleatorio(),
                    Nacionalidad = "Peruana",
                    MotivoViaje = (MotivoViaje)random.Next(3),
                    TipoComprobante = esFactura ? TipoComprobante.Factura : TipoComprobante.Boleta,
                    RUC = esFactura ? "20123456789" : null,
                    RazonSocial = esFactura ? "Empresa de Prueba SAC" : null,
                    CorreoFacturacion = esFactura ? "facturacion@prueba.com" : null,
                    UsuarioId = UsuarioAlAzar()
                };
                if (random.Next(3) == 0)
                {
                    dto.Acompanantes.Add(DatosPruebaSeeder.NombreCompletoAleatorio());
                }

                await habitacionesSvc.CheckInAsync(dto);
                Contar("CheckIn");
            }

            // ------------------------------------------------------------
            // 2) CHECK-OUTS — ~30% de las estadías activas, cada día
            // ------------------------------------------------------------
            var activas = await contexto.Estadias
                .Where(e => e.Estado == EstadoEstadia.Activa)
                .Select(e => e.Id)
                .ToListAsync();
            var aCheckOut = activas.OrderBy(_ => random.Next()).Take((int)Math.Ceiling(activas.Count * 0.3)).ToList();

            foreach (var estadiaId in aCheckOut)
            {
                var metodo = (MetodoPago)random.Next(5);
                await habitacionesSvc.CheckOutAsync(estadiaId, UsuarioAlAzar(), metodo);
                Contar("CheckOut");
            }

            // Barrido diario de limpieza: el personal revisa TODAS las habitaciones
            // que estén en Limpieza en este momento, no solo las que salieron hoy —
            // si no, las de días anteriores quedan olvidadas para siempre (nunca
            // vuelven a Disponible) y el hotel se queda sin habitaciones vendibles
            // en poco tiempo. La mayoría se termina el mismo día (turnover rápido),
            // pero no el 100% — queda variación realista.
            var habitacionesEnLimpieza = (await habitacionesSvc.ObtenerTodasAsync())
                .Where(h => h.Estado == EstadoHabitacion.LimpiezaSalida)
                .ToList();
            foreach (var hab in habitacionesEnLimpieza)
            {
                if (random.Next(10) < 8) // 80% de las habitaciones sucias se terminan cada día
                {
                    await habitacionesSvc.FinalizarLimpiezaAsync(hab.HabitacionId, UsuarioAlAzar());
                    Contar("FinalizarLimpieza");
                }
            }

            // ------------------------------------------------------------
            // 3) RESERVAS A FUTURO — 0 a 2 por día, fechas reales repartidas
            //    en los próximos 1 a 45 días
            // ------------------------------------------------------------
            for (var r = 0; r < random.Next(0, 3); r++)
            {
                var inicio = DateTime.Today.AddDays(random.Next(1, 46));
                var fin = inicio.AddDays(random.Next(1, 6));
                var disponiblesParaReserva = await reservasSvc.ObtenerHabitacionesDisponiblesAsync(inicio, fin);
                if (disponiblesParaReserva.Count == 0)
                {
                    continue;
                }

                var habReserva = disponiblesParaReserva[random.Next(disponiblesParaReserva.Count)];
                await reservasSvc.CrearReservaAsync(new NuevaReservaDto
                {
                    HabitacionId = habReserva.HabitacionId,
                    TipoDocumento = TipoDocumento.DNI,
                    NumeroDocumento = DatosPruebaSeeder.DniAleatorio(),
                    NombreCompleto = DatosPruebaSeeder.NombreCompletoAleatorio(),
                    Celular = DatosPruebaSeeder.CelularAleatorio(),
                    FechaInicio = inicio,
                    FechaFin = fin,
                    UsuarioId = UsuarioAlAzar()
                });
                Contar("ReservaCreada");
            }

            // Cada ~5 días: convertir la reserva más próxima en check-in (huésped que llegó).
            if (dia % 5 == 0)
            {
                var proximas = await reservasSvc.ObtenerProximasAsync();
                var masProxima = proximas
                    .Where(r => r.Estado == EstadoReserva.Confirmada && r.FechaInicio.Date <= DateTime.Today.AddDays(3))
                    .OrderBy(r => r.FechaInicio)
                    .FirstOrDefault();
                if (masProxima is not null)
                {
                    try
                    {
                        await reservasSvc.ConvertirEnCheckInAsync(masProxima.ReservaId, UsuarioAlAzar());
                        Contar("ReservaConvertidaEnCheckIn");
                    }
                    catch (InvalidOperationException ex)
                    {
                        // Esperable: la habitación de la reserva puede estar Ocupada por
                        // otra estadía todavía (la reserva no se auto-sincroniza con el
                        // estado real minuto a minuto). Se cuenta, no es un hallazgo.
                        Contar("ReservaConversionRechazada");
                        _output.WriteLine($"[día {dia}] Conversión de reserva rechazada (esperable): {ex.Message}");
                    }
                }
            }

            // Cada ~7 días: cancelar alguna reserva futura al azar.
            if (dia % 7 == 0)
            {
                var confirmadas = (await reservasSvc.ObtenerProximasAsync())
                    .Where(r => r.Estado == EstadoReserva.Confirmada)
                    .ToList();
                if (confirmadas.Count > 0)
                {
                    var aCancelar = confirmadas[random.Next(confirmadas.Count)];
                    await reservasSvc.CancelarReservaAsync(aCancelar.ReservaId, UsuarioAlAzar());
                    Contar("ReservaCancelada");
                }
            }

            // ------------------------------------------------------------
            // 4) SAUNA — 1 a 3 clientes nuevos por día (algunos huéspedes del hotel)
            // ------------------------------------------------------------
            var huespedesActivos = await saunaSvc.BuscarHuespedesActivosAsync();
            for (var s = 0; s < random.Next(1, 4); s++)
            {
                var esHuesped = huespedesActivos.Count > 0 && random.Next(2) == 0;
                var huespedElegido = esHuesped ? huespedesActivos[random.Next(huespedesActivos.Count)] : null;

                var clienteSaunaId = await saunaSvc.RegistrarClienteAsync(new NuevoClienteSaunaDto
                {
                    TipoDocumento = TipoDocumento.DNI,
                    NumeroDocumento = huespedElegido?.NumeroDocumentoHuesped ?? DatosPruebaSeeder.DniAleatorio(),
                    NombreCompleto = huespedElegido?.NombreHuesped ?? DatosPruebaSeeder.NombreCompletoAleatorio(),
                    NumeroCandado = random.Next(1, 200).ToString(),
                    Seccion = (SeccionSauna)random.Next(2),
                    EsHuespedHotel = esHuesped,
                    EstadiaHotelId = esHuesped ? huespedElegido!.EstadiaId : null
                }, UsuarioAlAzar());
                Contar("ClienteSaunaRegistrado");

                // Venta de 1 a 2 ítems del catálogo para este mismo cliente.
                if (catalogo.Count > 0)
                {
                    var items = new List<ItemCarritoDto>();
                    var cantidadItems = random.Next(1, 3);
                    for (var it = 0; it < cantidadItems; it++)
                    {
                        var producto = catalogo[random.Next(catalogo.Count)];
                        var precio = producto.EsAlquilerVenta
                            ? (random.Next(2) == 0 ? producto.PrecioAlquiler : producto.PrecioVenta)
                            : producto.Precio;
                        items.Add(new ItemCarritoDto
                        {
                            ProductoId = producto.ProductoId,
                            Descripcion = producto.Nombre,
                            Cantidad = 1,
                            PrecioUnitario = precio
                        });
                    }

                    var cargarAHabitacion = esHuesped && random.Next(2) == 0;
                    await saunaSvc.RegistrarVentaAsync(
                        clienteSaunaId, items, UsuarioAlAzar(), cargarAHabitacion,
                        cargarAHabitacion ? null : (MetodoPago)random.Next(5));
                    Contar("VentaSaunaRegistrada");
                }
            }

            // Finalizar ~50% de las sesiones de sauna activas (clientes que se van).
            var clientesActivos = await saunaSvc.ObtenerClientesActivosAsync();
            var aFinalizar = clientesActivos.OrderBy(_ => random.Next()).Take(clientesActivos.Count / 2).ToList();
            foreach (var c in aFinalizar)
            {
                await saunaSvc.FinalizarSesionAsync(c.ClienteSaunaId, UsuarioAlAzar());
                Contar("SesionSaunaFinalizada");
            }

            // ------------------------------------------------------------
            // 5) CAFETERÍA — venta directa a un huésped de hotel al azar (sin pasar por Sauna)
            // ------------------------------------------------------------
            if (random.Next(2) == 0 && catalogo.Count > 0)
            {
                var estadiasActivasParaCafeteria = await contexto.Estadias
                    .Where(e => e.Estado == EstadoEstadia.Activa)
                    .Select(e => e.Id)
                    .ToListAsync();
                if (estadiasActivasParaCafeteria.Count > 0)
                {
                    var estadiaId = estadiasActivasParaCafeteria[random.Next(estadiasActivasParaCafeteria.Count)];
                    var productoCafeteria = catalogo[random.Next(catalogo.Count)];
                    var cargarAHabitacion = random.Next(2) == 0;
                    await saunaSvc.RegistrarVentaHotelAsync(
                        estadiaId,
                        new List<ItemCarritoDto>
                        {
                            new()
                            {
                                ProductoId = productoCafeteria.ProductoId,
                                Descripcion = productoCafeteria.Nombre,
                                Cantidad = 1,
                                PrecioUnitario = productoCafeteria.EsAlquilerVenta ? productoCafeteria.PrecioVenta : productoCafeteria.Precio
                            }
                        },
                        UsuarioAlAzar(), cargarAHabitacion, cargarAHabitacion ? null : (MetodoPago)random.Next(5));
                    Contar("VentaCafeteriaHotel");
                }
            }

            // ------------------------------------------------------------
            // 6) GASTOS / MOVIMIENTOS DE CAJA — 1 a 2 por día
            // ------------------------------------------------------------
            var motivosGasto = new[]
            {
                "Compra de insumos de limpieza", "Pedido de gaseosas y snacks",
                "Reparación menor", "Combustible para movilidad", "Adelanto de sueldo"
            };
            for (var g = 0; g < random.Next(1, 3); g++)
            {
                await gastosSvc.RegistrarMovimientoAsync(new NuevoMovimientoCajaDto
                {
                    Direccion = (DireccionMovimiento)random.Next(2),
                    Categoria = (CategoriaMovimientoCaja)random.Next(4),
                    Descripcion = motivosGasto[random.Next(motivosGasto.Length)],
                    Monto = Math.Round((decimal)(random.NextDouble() * 180 + 5), 2),
                    OrigenCaja = (OrigenCajaChica)random.Next(2),
                    MetodoPago = (MetodoPago)random.Next(5),
                    UsuarioId = UsuarioAlAzar()
                });
                Contar("MovimientoCajaRegistrado");
            }

            // ------------------------------------------------------------
            // 7) ALMACÉN / INVENTARIO — reponer o consumir insumos
            // ------------------------------------------------------------
            var insumos = await inventarioSvc.ObtenerInsumosAsync();
            for (var m = 0; m < random.Next(1, 3); m++)
            {
                if (insumos.Count == 0)
                {
                    break;
                }

                var insumo = insumos[random.Next(insumos.Count)];
                var esEntrada = random.Next(3) > 0; // 2/3 entrada, 1/3 salida — más se repone que se consume
                var cantidad = esEntrada
                    ? random.Next(5, 30)
                    : random.Next(1, Math.Max(2, insumo.StockActual + 5)); // a veces se pasa a propósito

                try
                {
                    await inventarioSvc.RegistrarMovimientoAsync(new NuevoMovimientoInventarioDto
                    {
                        InsumoId = insumo.Id,
                        Tipo = esEntrada ? TipoMovimientoInventario.Entrada : TipoMovimientoInventario.Salida,
                        Cantidad = cantidad,
                        Motivo = esEntrada ? "Reposición de stock" : "Consumo operativo",
                        UsuarioId = UsuarioAlAzar()
                    });
                    Contar(esEntrada ? "InventarioEntrada" : "InventarioSalida");
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("stock suficiente"))
                {
                    // Esperable a propósito (ver comentario arriba): confirma que el
                    // sistema SÍ frena una salida que dejaría stock negativo.
                    Contar("InventarioSalidaRechazadaPorStock");
                }
            }

            // ------------------------------------------------------------
            // 8) MANTENIMIENTO — cada ~6 días, una habitación Disponible al azar
            // ------------------------------------------------------------
            if (dia % 6 == 0)
            {
                var candidatas = (await habitacionesSvc.ObtenerTodasAsync())
                    .Where(h => h.Estado == EstadoHabitacion.Disponible)
                    .ToList();
                if (candidatas.Count > 0)
                {
                    var hab = candidatas[random.Next(candidatas.Count)];
                    await habitacionesSvc.IniciarMantenimientoAsync(hab.HabitacionId, "Revisión preventiva de rutina", UsuarioAlAzar());
                    Contar("MantenimientoIniciado");
                }
            }

            // Finalizar mantenimientos abiertos hace unos días (mitad de las veces).
            var enMantenimiento = (await habitacionesSvc.ObtenerTodasAsync())
                .Where(h => h.Estado == EstadoHabitacion.Mantenimiento)
                .ToList();
            foreach (var hab in enMantenimiento)
            {
                if (random.Next(2) == 0)
                {
                    await habitacionesSvc.FinalizarMantenimientoAsync(hab.HabitacionId, UsuarioAlAzar());
                    Contar("MantenimientoFinalizado");
                }
            }

            // ------------------------------------------------------------
            // 9) RECLAMOS — cada ~4 días se registra uno; los pendientes se
            //    responden con ~50% de probabilidad cada día
            // ------------------------------------------------------------
            if (dia % 4 == 0)
            {
                await reclamosSvc.RegistrarAsync(new NuevoReclamoDto
                {
                    NombreCompleto = DatosPruebaSeeder.NombreCompletoAleatorio(),
                    Domicilio = "Av. Simulación 123",
                    TipoDocumento = TipoDocumento.DNI,
                    NumeroDocumento = DatosPruebaSeeder.DniAleatorio(),
                    Telefono = DatosPruebaSeeder.CelularAleatorio(),
                    BienContratado = random.Next(2) == 0 ? "Hospedaje" : "Consumo de Sauna",
                    Tipo = (TipoReclamoQueja)random.Next(2),
                    DetalleReclamo = "Reclamo generado por la simulación automatizada de prueba."
                }, UsuarioAlAzar());
                Contar("ReclamoRegistrado");
            }

            var pendientes = await contexto.Reclamos.Where(r => r.Estado == EstadoReclamo.Pendiente).Select(r => r.Id).ToListAsync();
            foreach (var reclamoId in pendientes)
            {
                if (random.Next(2) == 0)
                {
                    await reclamosSvc.ResponderAsync(reclamoId, "Se atendió el caso y se ofreció una disculpa formal al cliente.", UsuarioAlAzar());
                    Contar("ReclamoRespondido");
                }
            }

            // ------------------------------------------------------------
            // 10) CIERRE DE CAJA — cada ~3 días, cerrando primero cualquier
            //     sesión de sauna que haya quedado abierta (la regla de
            //     negocio real: no se puede cerrar con sesiones activas)
            // ------------------------------------------------------------
            if (dia % 3 == 0)
            {
                var abiertosAntesDeCierre = await saunaSvc.ObtenerClientesActivosAsync();
                foreach (var c in abiertosAntesDeCierre)
                {
                    await saunaSvc.FinalizarSesionAsync(c.ClienteSaunaId, UsuarioAlAzar());
                    Contar("SesionSaunaFinalizada");
                }

                await cierreCajaSvc.CerrarCajaAsync((TurnoCaja)random.Next(3), UsuarioAlAzar());
                Contar("CierreCajaRealizado");
            }

            // ------------------------------------------------------------
            // INVARIANTES A VERIFICAR CADA DÍA — si algo de esto falla,
            // es un hallazgo real, no una regla de negocio esperada.
            // ------------------------------------------------------------
            var stockNegativo = await contexto.Insumos.Where(i => i.StockActual < 0).ToListAsync();
            if (stockNegativo.Count > 0)
            {
                hallazgos.Add($"[día {dia}] Stock negativo en: {string.Join(", ", stockNegativo.Select(i => $"{i.Nombre}={i.StockActual}"))}");
            }

            var habitacionesDuplicadasOcupadas = await contexto.Estadias
                .Where(e => e.Estado == EstadoEstadia.Activa)
                .GroupBy(e => e.HabitacionId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToListAsync();
            if (habitacionesDuplicadasOcupadas.Count > 0)
            {
                hallazgos.Add($"[día {dia}] Habitación(es) con más de una Estadia Activa a la vez: {string.Join(", ", habitacionesDuplicadasOcupadas)}");
            }
        }

        // ------------------------------------------------------------
        // REPORTE FINAL
        // ------------------------------------------------------------
        _output.WriteLine("");
        _output.WriteLine($"=== Simulación de {diasASimular} días — resumen de operaciones ===");
        foreach (var kv in stats.OrderByDescending(k => k.Value))
        {
            _output.WriteLine($"  {kv.Key}: {kv.Value}");
        }

        var habitacionesFinal = await habitacionesSvc.ObtenerTodasAsync();
        _output.WriteLine("");
        _output.WriteLine("=== Estado final de habitaciones ===");
        foreach (var grupo in habitacionesFinal.GroupBy(h => h.Estado).OrderBy(g => g.Key))
        {
            _output.WriteLine($"  {grupo.Key}: {grupo.Count()}");
        }

        var insumosFinal = await inventarioSvc.ObtenerInsumosAsync();
        _output.WriteLine("");
        _output.WriteLine("=== Stock final de insumos (los con stock bajo) ===");
        foreach (var i in insumosFinal.Where(i => i.StockBajo))
        {
            _output.WriteLine($"  {i.Nombre}: {i.StockActual}/{i.StockMinimo} ({i.EtiquetaCategoria})");
        }

        _output.WriteLine("");
        _output.WriteLine($"=== Hallazgos: {hallazgos.Count} ===");
        foreach (var h in hallazgos)
        {
            _output.WriteLine($"  {h}");
        }

        Assert.Empty(hallazgos);
    }
}
