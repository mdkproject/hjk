using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using JKalixto_System.Application.Services;
using JKalixto_System.Domain.Models;

namespace JKalixto_System.Tests;

public class ReservaServiceTests
{
    private static ReservaService NuevoServicio(JKalixto_System.Infrastructure.Data.AppDbContext contexto)
        => new(contexto, new AuditoriaService(contexto, new SessionService()));

    private static NuevaReservaDto DtoBase(int habitacionId, DateTime inicio, DateTime fin) => new()
    {
        HabitacionId = habitacionId,
        NumeroDocumento = "12345678",
        NombreCompleto = "Cliente de Prueba",
        Celular = "999999999",
        FechaInicio = inicio,
        FechaFin = fin,
        UsuarioId = 1
    };

    [Fact]
    public async Task CrearReservaAsync_FechaFinAntesQueInicio_LanzaExcepcion()
    {
        using var bd = new BaseDeDatosDePrueba();
        var habitacionId = (await bd.Contexto.Habitaciones.FirstAsync()).Id;
        var servicio = NuevoServicio(bd.Contexto);

        var manana = DateTime.Now.Date.AddDays(1);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.CrearReservaAsync(DtoBase(habitacionId, manana, manana)));
    }

    [Fact]
    public async Task CrearReservaAsync_FechaEnElPasado_LanzaExcepcion()
    {
        using var bd = new BaseDeDatosDePrueba();
        var habitacionId = (await bd.Contexto.Habitaciones.FirstAsync()).Id;
        var servicio = NuevoServicio(bd.Contexto);

        var ayer = DateTime.Now.Date.AddDays(-1);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.CrearReservaAsync(DtoBase(habitacionId, ayer, ayer.AddDays(2))));
    }

    [Fact]
    public async Task CrearReservaAsync_FechasQueSeCruzanConOtraReservaConfirmada_LanzaExcepcion()
    {
        using var bd = new BaseDeDatosDePrueba();
        var habitacionId = (await bd.Contexto.Habitaciones.FirstAsync()).Id;
        var servicio = NuevoServicio(bd.Contexto);

        var inicio = DateTime.Now.Date.AddDays(5);
        var fin = DateTime.Now.Date.AddDays(8);
        await servicio.CrearReservaAsync(DtoBase(habitacionId, inicio, fin));

        // Se cruza a la mitad del rango ya reservado.
        var inicioSolapado = inicio.AddDays(1);
        var finSolapado = fin.AddDays(1);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.CrearReservaAsync(DtoBase(habitacionId, inicioSolapado, finSolapado)));
    }

    [Fact]
    public async Task CrearReservaAsync_FechasQueNoSeCruzan_PermiteAmbasReservas()
    {
        using var bd = new BaseDeDatosDePrueba();
        var habitacionId = (await bd.Contexto.Habitaciones.FirstAsync()).Id;
        var servicio = NuevoServicio(bd.Contexto);

        var inicio1 = DateTime.Now.Date.AddDays(5);
        var fin1 = DateTime.Now.Date.AddDays(8);
        await servicio.CrearReservaAsync(DtoBase(habitacionId, inicio1, fin1));

        // Empieza justo el mismo día que termina la primera — no se solapan.
        var inicio2 = fin1;
        var fin2 = fin1.AddDays(3);
        var idSegunda = await servicio.CrearReservaAsync(DtoBase(habitacionId, inicio2, fin2));

        Assert.True(idSegunda > 0);
        Assert.Equal(2, await bd.Contexto.Reservas.CountAsync(r => r.HabitacionId == habitacionId));
    }

    [Fact]
    public async Task ConvertirEnCheckInAsync_HabitacionYaOcupadaPorOtroLado_LanzaExcepcion()
    {
        using var bd = new BaseDeDatosDePrueba();
        var habitacion = await bd.Contexto.Habitaciones.FirstAsync(h => h.Estado == EstadoHabitacion.Disponible);
        var servicioReserva = NuevoServicio(bd.Contexto);

        var inicio = DateTime.Now.Date;
        var reservaId = await servicioReserva.CrearReservaAsync(DtoBase(habitacion.Id, inicio, inicio.AddDays(2)));

        // Mientras tanto, otra persona hizo Check-in directo en esa misma habitación
        // (por ejemplo, un huésped walk-in) antes de que se procese la reserva.
        var servicioHabitacion = new HabitacionService(bd.Contexto, new AuditoriaService(bd.Contexto, new SessionService()), new ComprobanteNumeracionService(bd.Contexto));
        await servicioHabitacion.CheckInAsync(new NuevoCheckInDto
        {
            HabitacionId = habitacion.Id,
            NumeroDocumento = "000",
            NombreCompleto = "Huésped Walk-in",
            UsuarioId = 1
        });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicioReserva.ConvertirEnCheckInAsync(reservaId, 1));
    }
}
