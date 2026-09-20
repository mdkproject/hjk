using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using JKalixto_System.Application.Services;
using JKalixto_System.Domain.Models;
using JKalixto_System.Infrastructure.Data;

namespace JKalixto_System.Web.Services;

// HERRAMIENTA DE DEMO -- solo corre en modo Desarrollo (ver el gate en
// Program.cs: app.Environment.IsDevelopment()), nunca en el servicio real de
// producción. Se deja instalada a propósito (el usuario pidió poder volver a
// generar un año de datos "pesados" cuando quiera) pero protegida para que
// nadie pueda dispararla por accidente sobre los datos reales del hotel --
// solo se puede correr apuntando a una copia de la base, arrancando el
// servidor manualmente con ASPNETCORE_ENVIRONMENT=Development.
//
// Simula ~365 días de operación con una pausa corta entre cada acción para
// poder verla pasar en vivo en el navegador (usa los mismos servicios de
// Application, así que dispara el mismo INotificadorCambios que ya refresca
// Recepción/Reservas/Calendario/Reportes solas). Lógica calcada de
// JKalixto_System.Tests/SimulacionOperativaTests.cs (ya probada), con dos
// diferencias a propósito:
//   1) diasASimular = 365 en vez de 45, y pausas más cortas -- para que un
//      año completo entre en unos pocos minutos en vez de horas.
//   2) Los montos y categorías de Gastos no son puramente al azar: están
//      calibrados con los montos y proporciones reales que el usuario
//      mostró en fotos de su informe mensual en Excel (sueldos, servicios
//      de agua/luz/cable/internet, mantenimiento, etc. -- ver
//      CategoriasGastoCalibradas más abajo). No es una copia exacta de esos
//      meses reales, pero hace que el año simulado tenga una "forma"
//      parecida a la de su negocio real en vez de números arbitrarios.
public static class SimulacionVisualTemporal
{
    /// <summary>Categoría, monto mínimo, monto máximo, peso relativo (no necesita
    /// sumar 100 -- se normaliza solo). Los rangos de monto y los pesos están
    /// calibrados a ojo con las fotos de reportes reales que compartió el
    /// usuario (ver conversación): sueldos y servicios son los egresos más
    /// grandes y frecuentes, impuestos son muchos pero chicos, etc.</summary>
    private static readonly (CategoriaMovimientoCaja Categoria, decimal Min, decimal Max, int Peso)[] CategoriasGastoCalibradas =
    {
        (CategoriaMovimientoCaja.Sueldos, 150m, 800m, 22),
        (CategoriaMovimientoCaja.Servicios, 30m, 1400m, 16),
        (CategoriaMovimientoCaja.Mantenimiento, 25m, 500m, 14),
        (CategoriaMovimientoCaja.Limpieza, 15m, 200m, 9),
        (CategoriaMovimientoCaja.Impuestos, 0.10m, 5m, 10),
        (CategoriaMovimientoCaja.Cafeteria, 1m, 20m, 7),
        (CategoriaMovimientoCaja.Lavanderia, 10m, 100m, 6),
        (CategoriaMovimientoCaja.Comisiones, 5m, 150m, 4),
        (CategoriaMovimientoCaja.Recepcion, 10m, 100m, 4),
        (CategoriaMovimientoCaja.Vitrina, 10m, 120m, 3),
        (CategoriaMovimientoCaja.Deposito, 200m, 3000m, 3),
        (CategoriaMovimientoCaja.Otros, 10m, 300m, 4),
    };

    private static readonly string[] DescripcionesPorCategoria_Sueldos = { "Pago de sueldo", "Adelanto de sueldo", "Pago sueldo apoyo" };
    private static readonly string[] DescripcionesPorCategoria_Servicios = { "Pago servicio de agua", "Pago servicio de electricidad", "Pago servicio de internet", "Pago servicio de cable", "Pago servicio de celular" };
    private static readonly string[] DescripcionesPorCategoria_Mantenimiento = { "Reparación menor", "Compra de insumos de mantenimiento", "Mantenimiento de habitación", "Reparación de electrodoméstico" };
    private static readonly string[] DescripcionesPorCategoria_Limpieza = { "Compra de insumos de limpieza", "Pago de lavado de sábanas" };
    private static readonly string[] DescripcionesPorCategoria_Impuestos = { "Impuesto ITF", "Impuesto de transferencia" };
    private static readonly string[] DescripcionesPorCategoria_Cafeteria = { "Compra de pan", "Compra de gaseosas y snacks", "Compra de fruta" };
    private static readonly string[] DescripcionesPorCategoria_Lavanderia = { "Pago de lavandería", "Transporte de sábanas y toallas" };
    private static readonly string[] DescripcionesPorCategoria_Comisiones = { "Comisión de plataforma de reservas", "Comisión bancaria" };
    private static readonly string[] DescripcionesPorCategoria_Recepcion = { "Gasto de recepción", "Compra de papel bond y útiles" };
    private static readonly string[] DescripcionesPorCategoria_Vitrina = { "Venta de vitrina", "Reposición de vitrina" };
    private static readonly string[] DescripcionesPorCategoria_Deposito = { "Depósito a cuenta bancaria", "Retiro para depósito" };
    private static readonly string[] DescripcionesPorCategoria_Otros = { "Gasto varios", "Ajuste de caja" };

    private static string[] DescripcionesPara(CategoriaMovimientoCaja categoria) => categoria switch
    {
        CategoriaMovimientoCaja.Sueldos => DescripcionesPorCategoria_Sueldos,
        CategoriaMovimientoCaja.Servicios => DescripcionesPorCategoria_Servicios,
        CategoriaMovimientoCaja.Mantenimiento => DescripcionesPorCategoria_Mantenimiento,
        CategoriaMovimientoCaja.Limpieza => DescripcionesPorCategoria_Limpieza,
        CategoriaMovimientoCaja.Impuestos => DescripcionesPorCategoria_Impuestos,
        CategoriaMovimientoCaja.Cafeteria => DescripcionesPorCategoria_Cafeteria,
        CategoriaMovimientoCaja.Lavanderia => DescripcionesPorCategoria_Lavanderia,
        CategoriaMovimientoCaja.Comisiones => DescripcionesPorCategoria_Comisiones,
        CategoriaMovimientoCaja.Recepcion => DescripcionesPorCategoria_Recepcion,
        CategoriaMovimientoCaja.Vitrina => DescripcionesPorCategoria_Vitrina,
        CategoriaMovimientoCaja.Deposito => DescripcionesPorCategoria_Deposito,
        _ => DescripcionesPorCategoria_Otros
    };

    private static (CategoriaMovimientoCaja categoria, decimal monto, string descripcion) GastoAlAzar(Random random)
    {
        var pesoTotal = CategoriasGastoCalibradas.Sum(c => c.Peso);
        var punto = random.Next(pesoTotal);
        var acumulado = 0;
        foreach (var c in CategoriasGastoCalibradas)
        {
            acumulado += c.Peso;
            if (punto < acumulado)
            {
                var monto = Math.Round((decimal)(random.NextDouble() * (double)(c.Max - c.Min) + (double)c.Min), 2);
                var descripciones = DescripcionesPara(c.Categoria);
                return (c.Categoria, monto, descripciones[random.Next(descripciones.Length)]);
            }
        }
        return (CategoriaMovimientoCaja.Otros, 20m, "Gasto varios");
    }

    public static async Task EjecutarAsync(IServiceScopeFactory scopeFactory)
    {
        using var scope = scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var contexto = sp.GetRequiredService<AppDbContext>();
        var habitacionesSvc = sp.GetRequiredService<IHabitacionService>();
        var reservasSvc = sp.GetRequiredService<IReservaService>();
        var saunaSvc = sp.GetRequiredService<ISaunaService>();
        var gastosSvc = sp.GetRequiredService<IGastosService>();
        var inventarioSvc = sp.GetRequiredService<IInventarioService>();
        var cierreCajaSvc = sp.GetRequiredService<ICierreCajaService>();
        var reclamosSvc = sp.GetRequiredService<IReclamosService>();
        var notificador = sp.GetRequiredService<INotificadorCambios>();

        var random = new Random();
        var usuarios = await contexto.Usuarios.Where(u => u.Activo).Select(u => u.Id).ToListAsync();
        var catalogo = await saunaSvc.ObtenerCatalogoAsync();
        int UsuarioAlAzar() => usuarios[random.Next(usuarios.Count)];
        Task Pausa() => Task.Delay(random.Next(12, 30));

        const int diasASimular = 365;
        Console.WriteLine($"[demo-visual] Arrancando simulación de {diasASimular} días...");

        try
        {
            for (var dia = 1; dia <= diasASimular; dia++)
            {
                if (dia % 10 == 0)
                {
                    Console.WriteLine($"[demo-visual] Día {dia}/{diasASimular}");
                }

                var disponibles = (await habitacionesSvc.ObtenerTodasAsync())
                    .Where(h => h.Estado == EstadoHabitacion.Disponible)
                    .OrderBy(_ => random.Next())
                    .Take(random.Next(1, 5))
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
                    if (random.Next(3) == 0) dto.Acompanantes.Add(DatosPruebaSeeder.NombreCompletoAleatorio());

                    await habitacionesSvc.CheckInAsync(dto);
                    notificador.Avisar("habitaciones");
                    await Pausa();
                }

                var activas = await contexto.Estadias.Where(e => e.Estado == EstadoEstadia.Activa).Select(e => e.Id).ToListAsync();
                var aCheckOut = activas.OrderBy(_ => random.Next()).Take((int)Math.Ceiling(activas.Count * 0.3)).ToList();
                foreach (var estadiaId in aCheckOut)
                {
                    await habitacionesSvc.CheckOutAsync(estadiaId, UsuarioAlAzar(), (MetodoPago)random.Next(5));
                    notificador.Avisar("habitaciones");
                    await Pausa();
                }

                var habitacionesEnLimpieza = (await habitacionesSvc.ObtenerTodasAsync())
                    .Where(h => h.Estado == EstadoHabitacion.LimpiezaSalida).ToList();
                foreach (var hab in habitacionesEnLimpieza)
                {
                    if (random.Next(10) < 8)
                    {
                        await habitacionesSvc.FinalizarLimpiezaAsync(hab.HabitacionId, UsuarioAlAzar());
                        notificador.Avisar("habitaciones");
                        await Pausa();
                    }
                }

                for (var r = 0; r < random.Next(0, 3); r++)
                {
                    var inicio = DateTime.Today.AddDays(random.Next(1, 46));
                    var fin = inicio.AddDays(random.Next(1, 6));
                    var disponiblesParaReserva = await reservasSvc.ObtenerHabitacionesDisponiblesAsync(inicio, fin);
                    if (disponiblesParaReserva.Count == 0) continue;
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
                    notificador.Avisar("reservas");
                    await Pausa();
                }

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
                            notificador.Avisar("habitaciones");
                            notificador.Avisar("reservas");
                            await Pausa();
                        }
                        catch (InvalidOperationException) { }
                    }
                }

                if (dia % 7 == 0)
                {
                    var confirmadas = (await reservasSvc.ObtenerProximasAsync()).Where(r => r.Estado == EstadoReserva.Confirmada).ToList();
                    if (confirmadas.Count > 0)
                    {
                        var aCancelar = confirmadas[random.Next(confirmadas.Count)];
                        await reservasSvc.CancelarReservaAsync(aCancelar.ReservaId, UsuarioAlAzar());
                        notificador.Avisar("reservas");
                        await Pausa();
                    }
                }

                var huespedesActivos = await saunaSvc.BuscarHuespedesActivosAsync();
                for (var s = 0; s < random.Next(2, 6); s++)
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
                    notificador.Avisar("sauna");
                    await Pausa();

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
                            items.Add(new ItemCarritoDto { ProductoId = producto.ProductoId, Descripcion = producto.Nombre, Cantidad = 1, PrecioUnitario = precio });
                        }
                        var cargarAHabitacion = esHuesped && random.Next(2) == 0;
                        await saunaSvc.RegistrarVentaAsync(clienteSaunaId, items, UsuarioAlAzar(), cargarAHabitacion, cargarAHabitacion ? null : (MetodoPago)random.Next(5));
                        notificador.Avisar("sauna");
                        if (cargarAHabitacion) notificador.Avisar("habitaciones");
                        await Pausa();
                    }
                }

                var clientesActivos = await saunaSvc.ObtenerClientesActivosAsync();
                var aFinalizar = clientesActivos.OrderBy(_ => random.Next()).Take(clientesActivos.Count / 2).ToList();
                foreach (var c in aFinalizar)
                {
                    await saunaSvc.FinalizarSesionAsync(c.ClienteSaunaId, UsuarioAlAzar());
                    notificador.Avisar("sauna");
                    await Pausa();
                }

                if (random.Next(2) == 0 && catalogo.Count > 0)
                {
                    var estadiasActivasParaCafeteria = await contexto.Estadias.Where(e => e.Estado == EstadoEstadia.Activa).Select(e => e.Id).ToListAsync();
                    if (estadiasActivasParaCafeteria.Count > 0)
                    {
                        var estadiaId = estadiasActivasParaCafeteria[random.Next(estadiasActivasParaCafeteria.Count)];
                        var productoCafeteria = catalogo[random.Next(catalogo.Count)];
                        var cargarAHabitacion = random.Next(2) == 0;
                        await saunaSvc.RegistrarVentaHotelAsync(estadiaId,
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
                        notificador.Avisar("sauna");
                        if (cargarAHabitacion) notificador.Avisar("habitaciones");
                        await Pausa();
                    }
                }

                // --- GASTOS: montos y categorías calibrados con los reportes reales (ver arriba) ---
                for (var g = 0; g < random.Next(2, 5); g++)
                {
                    var (categoria, monto, descripcion) = GastoAlAzar(random);
                    await gastosSvc.RegistrarMovimientoAsync(new NuevoMovimientoCajaDto
                    {
                        Direccion = categoria == CategoriaMovimientoCaja.Deposito && random.Next(2) == 0
                            ? DireccionMovimiento.Ingreso
                            : DireccionMovimiento.Salida,
                        Categoria = categoria,
                        Descripcion = descripcion,
                        Monto = monto,
                        OrigenCaja = (OrigenCajaChica)random.Next(2),
                        MetodoPago = (MetodoPago)random.Next(5),
                        UsuarioId = UsuarioAlAzar()
                    });
                    notificador.Avisar("gastos");
                    await Pausa();
                }

                var insumos = await inventarioSvc.ObtenerInsumosAsync();
                for (var m = 0; m < random.Next(1, 3); m++)
                {
                    if (insumos.Count == 0) break;
                    var insumo = insumos[random.Next(insumos.Count)];
                    var esEntrada = random.Next(3) > 0;
                    var cantidad = esEntrada ? random.Next(5, 30) : random.Next(1, Math.Max(2, insumo.StockActual + 5));
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
                        notificador.Avisar("almacen");
                        await Pausa();
                    }
                    catch (InvalidOperationException) { }
                }

                if (dia % 6 == 0)
                {
                    var candidatas = (await habitacionesSvc.ObtenerTodasAsync()).Where(h => h.Estado == EstadoHabitacion.Disponible).ToList();
                    if (candidatas.Count > 0)
                    {
                        var hab = candidatas[random.Next(candidatas.Count)];
                        await habitacionesSvc.IniciarMantenimientoAsync(hab.HabitacionId, "Revisión preventiva de rutina", UsuarioAlAzar());
                        notificador.Avisar("habitaciones");
                        await Pausa();
                    }
                }
                var enMantenimiento = (await habitacionesSvc.ObtenerTodasAsync()).Where(h => h.Estado == EstadoHabitacion.Mantenimiento).ToList();
                foreach (var hab in enMantenimiento)
                {
                    if (random.Next(2) == 0)
                    {
                        await habitacionesSvc.FinalizarMantenimientoAsync(hab.HabitacionId, UsuarioAlAzar());
                        notificador.Avisar("habitaciones");
                        await Pausa();
                    }
                }

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
                        DetalleReclamo = "Reclamo generado por la simulación visual de prueba."
                    }, UsuarioAlAzar());
                    notificador.Avisar("reclamos");
                    await Pausa();
                }
                var pendientes = await contexto.Reclamos.Where(r => r.Estado == EstadoReclamo.Pendiente).Select(r => r.Id).ToListAsync();
                foreach (var reclamoId in pendientes)
                {
                    if (random.Next(2) == 0)
                    {
                        await reclamosSvc.ResponderAsync(reclamoId, "Se atendió el caso y se ofreció una disculpa formal al cliente.", UsuarioAlAzar());
                        notificador.Avisar("reclamos");
                        await Pausa();
                    }
                }

                if (dia % 3 == 0)
                {
                    var abiertosAntesDeCierre = await saunaSvc.ObtenerClientesActivosAsync();
                    foreach (var c in abiertosAntesDeCierre)
                    {
                        await saunaSvc.FinalizarSesionAsync(c.ClienteSaunaId, UsuarioAlAzar());
                        notificador.Avisar("sauna");
                        await Pausa();
                    }
                    await cierreCajaSvc.CerrarCajaAsync((TurnoCaja)random.Next(3), UsuarioAlAzar());
                    notificador.Avisar("caja");
                    await Pausa();
                }

                await Task.Delay(random.Next(40, 90));
            }

            Console.WriteLine("[demo-visual] Simulación terminada.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[demo-visual] ERROR: {ex}");
        }
    }
}
