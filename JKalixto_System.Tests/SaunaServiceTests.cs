using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using JKalixto_System.Application.Services;
using JKalixto_System.Domain.Models;

namespace JKalixto_System.Tests;

public class SaunaServiceTests
{
    private const int IdUsuarioRecepcion = 5;   // seed: "recepcion", RolUsuario.Recepcionista
    private const int IdUsuarioGerencia = 1;    // seed: "gerencia.1", RolUsuario.Gerencia

    private static SaunaService NuevoServicio(JKalixto_System.Infrastructure.Data.AppDbContext contexto, ISessionService? sessionService = null)
    {
        sessionService ??= new SessionService();
        return new(contexto, new AuditoriaService(contexto, sessionService), new ComprobanteNumeracionService(contexto), sessionService);
    }

    private static async Task<int> CrearEstadiaActivaAsync(JKalixto_System.Infrastructure.Data.AppDbContext contexto)
    {
        var habitacion = await contexto.Habitaciones.FirstAsync(h => h.Estado == EstadoHabitacion.Disponible);
        var habitacionService = new HabitacionService(contexto, new AuditoriaService(contexto, new SessionService()), new ComprobanteNumeracionService(contexto), new SessionService());
        await habitacionService.CheckInAsync(new NuevoCheckInDto
        {
            HabitacionId = habitacion.Id,
            NumeroDocumento = "12345678",
            NombreCompleto = "Huésped de Prueba",
            UsuarioId = 1
        });

        return (await contexto.Estadias.SingleAsync(e => e.HabitacionId == habitacion.Id)).Id;
    }

    [Fact]
    public async Task RegistrarVentaHotelAsync_CarritoVacio_LanzaExcepcion()
    {
        using var bd = new BaseDeDatosDePrueba();
        var estadiaId = await CrearEstadiaActivaAsync(bd.Contexto);
        var servicio = NuevoServicio(bd.Contexto);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.RegistrarVentaHotelAsync(estadiaId, new List<ItemCarritoDto>(), 1, cargarAHabitacion: true, metodoPago: null));
    }

    [Fact]
    public async Task RegistrarVentaHotelAsync_ItemConCantidadCero_LanzaExcepcionYNoCargaNadaALaHabitacion()
    {
        using var bd = new BaseDeDatosDePrueba();
        var estadiaId = await CrearEstadiaActivaAsync(bd.Contexto);
        var totalAntes = (await bd.Contexto.Estadias.FindAsync(estadiaId))!.TotalAcumulado;
        var servicio = NuevoServicio(bd.Contexto);

        var items = new List<ItemCarritoDto> { new() { Descripcion = "Gaseosa", Cantidad = 0, PrecioUnitario = 5m } };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.RegistrarVentaHotelAsync(estadiaId, items, 1, cargarAHabitacion: true, metodoPago: null));

        var estadia = await bd.Contexto.Estadias.FindAsync(estadiaId);
        Assert.Equal(totalAntes, estadia!.TotalAcumulado);
    }

    [Fact]
    public async Task RegistrarVentaHotelAsync_ItemConPrecioNegativo_LanzaExcepcion()
    {
        using var bd = new BaseDeDatosDePrueba();
        var estadiaId = await CrearEstadiaActivaAsync(bd.Contexto);
        var servicio = NuevoServicio(bd.Contexto);

        var items = new List<ItemCarritoDto> { new() { Descripcion = "Ajuste raro", Cantidad = 1, PrecioUnitario = -5m } };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.RegistrarVentaHotelAsync(estadiaId, items, 1, cargarAHabitacion: true, metodoPago: null));
    }

    [Fact]
    public async Task RegistrarVentaHotelAsync_CargaAHabitacion_IncrementaElTotalAcumuladoCorrectamente()
    {
        using var bd = new BaseDeDatosDePrueba();
        var estadiaId = await CrearEstadiaActivaAsync(bd.Contexto);
        var totalAntes = (await bd.Contexto.Estadias.FindAsync(estadiaId))!.TotalAcumulado;
        var servicio = NuevoServicio(bd.Contexto);

        var items = new List<ItemCarritoDto>
        {
            new() { Descripcion = "Gaseosa", Cantidad = 2, PrecioUnitario = 5m },
            new() { Descripcion = "Sánguche", Cantidad = 1, PrecioUnitario = 9m }
        };

        await servicio.RegistrarVentaHotelAsync(estadiaId, items, 1, cargarAHabitacion: true, metodoPago: null);

        var estadia = await bd.Contexto.Estadias.FindAsync(estadiaId);
        Assert.Equal(totalAntes + 19m, estadia!.TotalAcumulado);

        var venta = await bd.Contexto.VentasSauna.SingleAsync(v => v.EstadiaHotelDestinoId == estadiaId);
        Assert.Equal(19m, venta.Total);
        Assert.Equal(EstadoVenta.CargadaAHabitacion, venta.Estado);
    }

    [Fact]
    public async Task RegistrarVentaHotelAsync_EstadiaYaFinalizada_LanzaExcepcion()
    {
        using var bd = new BaseDeDatosDePrueba();
        var estadiaId = await CrearEstadiaActivaAsync(bd.Contexto);
        var estadia = await bd.Contexto.Estadias.FindAsync(estadiaId);
        estadia!.Estado = EstadoEstadia.Finalizada;
        await bd.Contexto.SaveChangesAsync();

        var servicio = NuevoServicio(bd.Contexto);
        var items = new List<ItemCarritoDto> { new() { Descripcion = "Gaseosa", Cantidad = 1, PrecioUnitario = 5m } };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.RegistrarVentaHotelAsync(estadiaId, items, 1, cargarAHabitacion: true, metodoPago: null));
    }

    [Fact]
    public async Task EditarPrecioProductoAsync_ComoRecepcionista_LanzaExcepcionYNoCambiaElPrecio()
    {
        using var bd = new BaseDeDatosDePrueba();
        var sesion = new SessionService { UsuarioActual = await bd.Contexto.Usuarios.FindAsync(IdUsuarioRecepcion) };
        var servicio = NuevoServicio(bd.Contexto, sesion);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => servicio.EditarPrecioProductoAsync(new EditarPrecioProductoDto
        {
            ProductoId = 5,
            Precio = 99m,
            PrecioAlquiler = 0m,
            PrecioVenta = 0m
        }, usuarioId: IdUsuarioRecepcion));

        var producto = await bd.Contexto.ProductosPOS.FindAsync(5);
        Assert.Equal(5m, producto!.Precio);
    }

    [Fact]
    public async Task EditarPrecioProductoAsync_ComoGerencia_ActualizaElPrecioSimple()
    {
        using var bd = new BaseDeDatosDePrueba();
        var sesion = new SessionService { UsuarioActual = await bd.Contexto.Usuarios.FindAsync(IdUsuarioGerencia) };
        var servicio = NuevoServicio(bd.Contexto, sesion);

        await servicio.EditarPrecioProductoAsync(new EditarPrecioProductoDto
        {
            ProductoId = 5,
            Precio = 7.50m,
            PrecioAlquiler = 0m,
            PrecioVenta = 0m
        }, usuarioId: IdUsuarioGerencia);

        var producto = await bd.Contexto.ProductosPOS.FindAsync(5);
        Assert.Equal(7.50m, producto!.Precio);
    }

    [Fact]
    public async Task EditarPrecioProductoAsync_ProductoDeAlquilerVenta_ActualizaAmbosPrecios()
    {
        using var bd = new BaseDeDatosDePrueba();
        var sesion = new SessionService { UsuarioActual = await bd.Contexto.Usuarios.FindAsync(IdUsuarioGerencia) };
        var servicio = NuevoServicio(bd.Contexto, sesion);

        await servicio.EditarPrecioProductoAsync(new EditarPrecioProductoDto
        {
            ProductoId = 1,
            Precio = 0m,
            PrecioAlquiler = 6m,
            PrecioVenta = 22m
        }, usuarioId: IdUsuarioGerencia);

        var producto = await bd.Contexto.ProductosPOS.FindAsync(1);
        Assert.Equal(6m, producto!.PrecioAlquiler);
        Assert.Equal(22m, producto.PrecioVenta);
    }

    [Fact]
    public async Task EditarPrecioProductoAsync_PrecioNegativo_LanzaExcepcion()
    {
        using var bd = new BaseDeDatosDePrueba();
        var sesion = new SessionService { UsuarioActual = await bd.Contexto.Usuarios.FindAsync(IdUsuarioGerencia) };
        var servicio = NuevoServicio(bd.Contexto, sesion);

        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.EditarPrecioProductoAsync(new EditarPrecioProductoDto
        {
            ProductoId = 5,
            Precio = -1m,
            PrecioAlquiler = 0m,
            PrecioVenta = 0m
        }, usuarioId: IdUsuarioGerencia));
    }
}
