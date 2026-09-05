using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using JKalixto_System.Application.Services;
using JKalixto_System.Domain.Models;

namespace JKalixto_System.Tests;

/// <summary>
/// Reglas de negocio de Check-in/Check-out. El caso más importante acá es
/// "doble Check-in en la misma habitación" — el riesgo real de plata/operación
/// que motivó agregar un ConcurrencyToken en Habitacion.Estado
/// (ver AppDbContext.cs) y el catch en HabitacionService.CheckInAsync.
/// </summary>
public class HabitacionServiceTests
{
    private static HabitacionService NuevoServicio(JKalixto_System.Infrastructure.Data.AppDbContext contexto)
        => new(contexto, new AuditoriaService(contexto, new SessionService()), new ComprobanteNumeracionService(contexto));

    [Fact]
    public async Task CheckInAsync_HabitacionDisponible_CreaEstadiaYOcupaHabitacion()
    {
        using var bd = new BaseDeDatosDePrueba();
        var habitacion = await bd.Contexto.Habitaciones.FirstAsync(h => h.Estado == EstadoHabitacion.Disponible);
        var servicio = NuevoServicio(bd.Contexto);

        await servicio.CheckInAsync(new NuevoCheckInDto
        {
            HabitacionId = habitacion.Id,
            NumeroDocumento = "12345678",
            NombreCompleto = "Juan Pérez",
            Celular = "999999999",
            UsuarioId = 1
        });

        var habitacionActualizada = await bd.Contexto.Habitaciones.FindAsync(habitacion.Id);
        Assert.Equal(EstadoHabitacion.Ocupada, habitacionActualizada!.Estado);

        var estadia = await bd.Contexto.Estadias.SingleAsync(e => e.HabitacionId == habitacion.Id);
        Assert.Equal(EstadoEstadia.Activa, estadia.Estado);
        Assert.Equal(habitacion.TarifaNoche, estadia.TotalAcumulado);
    }

    [Fact]
    public async Task CheckInAsync_HabitacionQueNoExiste_LanzaExcepcion()
    {
        using var bd = new BaseDeDatosDePrueba();
        var servicio = NuevoServicio(bd.Contexto);

        await Assert.ThrowsAsync<System.InvalidOperationException>(() => servicio.CheckInAsync(new NuevoCheckInDto
        {
            HabitacionId = 999999,
            NumeroDocumento = "12345678",
            NombreCompleto = "Juan Pérez",
            UsuarioId = 1
        }));
    }

    [Fact]
    public async Task CheckInAsync_HabitacionYaOcupada_LanzaExcepcion()
    {
        using var bd = new BaseDeDatosDePrueba();
        // AsTracking(): esta prueba modifica la habitación directamente (arreglo del
        // escenario) y la guarda — BaseDeDatosDePrueba usa NoTracking por defecto.
        var habitacion = await bd.Contexto.Habitaciones.AsTracking().FirstAsync(h => h.Estado == EstadoHabitacion.Disponible);
        habitacion.Estado = EstadoHabitacion.Ocupada;
        await bd.Contexto.SaveChangesAsync();

        var servicio = NuevoServicio(bd.Contexto);

        await Assert.ThrowsAsync<System.InvalidOperationException>(() => servicio.CheckInAsync(new NuevoCheckInDto
        {
            HabitacionId = habitacion.Id,
            NumeroDocumento = "12345678",
            NombreCompleto = "Juan Pérez",
            UsuarioId = 1
        }));
    }

    /// <summary>
    /// EL CASO CRÍTICO: simula dos recepcionistas (dos AppDbContext separados, como
    /// en la app real) que leen la MISMA habitación "Disponible" casi al mismo
    /// tiempo e intentan hacer Check-in los dos. Sin el ConcurrencyToken agregado en
    /// Habitacion.Estado, ambos guardaban con éxito y la habitación quedaba
    /// asignada a dos huéspedes distintos al mismo tiempo. Con el fix, el segundo
    /// debe fallar con un mensaje claro en vez de duplicar la ocupación.
    /// </summary>
    [Fact]
    public async Task CheckInAsync_DosCheckInSimultaneosMismaHabitacion_SoloUnoTieneExito()
    {
        using var bd = new BaseDeDatosDePrueba();
        var habitacionId = (await bd.Contexto.Habitaciones.FirstAsync(h => h.Estado == EstadoHabitacion.Disponible)).Id;

        // Dos "terminales" distintas, cada una con su propio AppDbContext apuntando
        // al mismo archivo .db físico — así se comporta la app real (Transient).
        await using var contextoRecepcionista1 = bd.NuevoContexto();
        await using var contextoRecepcionista2 = bd.NuevoContexto();

        var servicio1 = new HabitacionService(contextoRecepcionista1, new AuditoriaService(contextoRecepcionista1, new SessionService()), new ComprobanteNumeracionService(contextoRecepcionista1));
        var servicio2 = new HabitacionService(contextoRecepcionista2, new AuditoriaService(contextoRecepcionista2, new SessionService()), new ComprobanteNumeracionService(contextoRecepcionista2));

        // Task.Run fuerza que las dos llamadas corran en hilos del pool distintos,
        // como pasaría de verdad con dos terminales — si simplemente se llamara
        // "await" una tras otra (o incluso sin await de por medio), el driver de
        // SQLite suele ejecutar todo de forma sincrónica dentro del mismo hilo y
        // nunca se llegaría a solapar la lectura de ambas.
        var tarea1 = Task.Run(() => servicio1.CheckInAsync(new NuevoCheckInDto { HabitacionId = habitacionId, NumeroDocumento = "111", NombreCompleto = "Huésped Uno", UsuarioId = 1 }));
        var tarea2 = Task.Run(() => servicio2.CheckInAsync(new NuevoCheckInDto { HabitacionId = habitacionId, NumeroDocumento = "222", NombreCompleto = "Huésped Dos", UsuarioId = 1 }));

        var resultados = await Task.WhenAll(
            tarea1.ContinueWith(t => t.Exception is null),
            tarea2.ContinueWith(t => t.Exception is null));

        Assert.Equal(1, resultados.Count(exito => exito));

        using var verificacion = bd.NuevoContexto();
        var estadiasActivas = await verificacion.Estadias
            .Where(e => e.HabitacionId == habitacionId && e.Estado == EstadoEstadia.Activa)
            .CountAsync();

        Assert.Equal(1, estadiasActivas);
    }
}
