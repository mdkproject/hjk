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
    private static SaunaService NuevoServicio(JKalixto_System.Infrastructure.Data.AppDbContext contexto)
        => new(contexto, new AuditoriaService(contexto, new SessionService()));

    private static async Task<int> CrearEstadiaActivaAsync(JKalixto_System.Infrastructure.Data.AppDbContext contexto)
    {
        var habitacion = await contexto.Habitaciones.FirstAsync(h => h.Estado == EstadoHabitacion.Disponible);
        var habitacionService = new HabitacionService(contexto, new AuditoriaService(contexto, new SessionService()));
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
            () => servicio.RegistrarVentaHotelAsync(estadiaId, new List<ItemCarritoDto>(), 1, cargarAHabitacion: true));
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
            () => servicio.RegistrarVentaHotelAsync(estadiaId, items, 1, cargarAHabitacion: true));

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
            () => servicio.RegistrarVentaHotelAsync(estadiaId, items, 1, cargarAHabitacion: true));
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

        await servicio.RegistrarVentaHotelAsync(estadiaId, items, 1, cargarAHabitacion: true);

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
            () => servicio.RegistrarVentaHotelAsync(estadiaId, items, 1, cargarAHabitacion: true));
    }
}
