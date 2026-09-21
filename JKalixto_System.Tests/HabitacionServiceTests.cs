using System;
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
    private const int IdUsuarioRecepcion = 5;   // seed: "recepcion", RolUsuario.Recepcionista
    private const int IdUsuarioGerencia = 1;    // seed: "gerencia.1", RolUsuario.Gerencia

    private static HabitacionService NuevoServicio(JKalixto_System.Infrastructure.Data.AppDbContext contexto, ISessionService? sessionService = null)
    {
        sessionService ??= new SessionService();
        return new(contexto, new AuditoriaService(contexto, sessionService), new ComprobanteNumeracionService(contexto), sessionService);
    }

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

        var servicio1 = new HabitacionService(contextoRecepcionista1, new AuditoriaService(contextoRecepcionista1, new SessionService()), new ComprobanteNumeracionService(contextoRecepcionista1), new SessionService());
        var servicio2 = new HabitacionService(contextoRecepcionista2, new AuditoriaService(contextoRecepcionista2, new SessionService()), new ComprobanteNumeracionService(contextoRecepcionista2), new SessionService());

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

    [Fact]
    public async Task CheckInAsync_FechaManualConRolRecepcionista_LanzaExcepcion()
    {
        using var bd = new BaseDeDatosDePrueba();
        var habitacion = await bd.Contexto.Habitaciones.FirstAsync(h => h.Estado == EstadoHabitacion.Disponible);
        var sesion = new SessionService { UsuarioActual = await bd.Contexto.Usuarios.FindAsync(IdUsuarioRecepcion) };
        var servicio = NuevoServicio(bd.Contexto, sesion);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => servicio.CheckInAsync(new NuevoCheckInDto
        {
            HabitacionId = habitacion.Id,
            NumeroDocumento = "12345678",
            NombreCompleto = "Juan Pérez",
            UsuarioId = IdUsuarioRecepcion,
            FechaCheckInManual = DateTime.Now.AddDays(-1)
        }));

        Assert.False(await bd.Contexto.Estadias.AnyAsync(e => e.HabitacionId == habitacion.Id));
    }

    [Fact]
    public async Task CheckInAsync_FechaManualEnElFuturoConRolGerencia_LanzaExcepcion()
    {
        using var bd = new BaseDeDatosDePrueba();
        var habitacion = await bd.Contexto.Habitaciones.FirstAsync(h => h.Estado == EstadoHabitacion.Disponible);
        var sesion = new SessionService { UsuarioActual = await bd.Contexto.Usuarios.FindAsync(IdUsuarioGerencia) };
        var servicio = NuevoServicio(bd.Contexto, sesion);

        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.CheckInAsync(new NuevoCheckInDto
        {
            HabitacionId = habitacion.Id,
            NumeroDocumento = "12345678",
            NombreCompleto = "Juan Pérez",
            UsuarioId = IdUsuarioGerencia,
            FechaCheckInManual = DateTime.Now.AddDays(1)
        }));
    }

    [Fact]
    public async Task CheckInAsync_FechaManualPasadaConRolGerencia_QuedaRegistradaConEsaFecha()
    {
        using var bd = new BaseDeDatosDePrueba();
        var habitacion = await bd.Contexto.Habitaciones.FirstAsync(h => h.Estado == EstadoHabitacion.Disponible);
        var sesion = new SessionService { UsuarioActual = await bd.Contexto.Usuarios.FindAsync(IdUsuarioGerencia) };
        var servicio = NuevoServicio(bd.Contexto, sesion);
        var fechaEsperada = DateTime.Now.AddDays(-3);

        await servicio.CheckInAsync(new NuevoCheckInDto
        {
            HabitacionId = habitacion.Id,
            NumeroDocumento = "12345678",
            NombreCompleto = "Juan Pérez",
            UsuarioId = IdUsuarioGerencia,
            FechaCheckInManual = fechaEsperada
        });

        var estadia = await bd.Contexto.Estadias.SingleAsync(e => e.HabitacionId == habitacion.Id);
        Assert.Equal(fechaEsperada, estadia.FechaCheckIn);
    }

    [Fact]
    public async Task CheckOutAsync_FechaManualConRolRecepcionista_LanzaExcepcionYNoCierraLaEstadia()
    {
        using var bd = new BaseDeDatosDePrueba();
        var servicio = NuevoServicio(bd.Contexto);
        var habitacion = await bd.Contexto.Habitaciones.FirstAsync(h => h.Estado == EstadoHabitacion.Disponible);
        await servicio.CheckInAsync(new NuevoCheckInDto { HabitacionId = habitacion.Id, NumeroDocumento = "12345678", NombreCompleto = "Juan Pérez", UsuarioId = IdUsuarioRecepcion });
        var estadiaId = (await bd.Contexto.Estadias.SingleAsync(e => e.HabitacionId == habitacion.Id)).Id;

        var sesionRecepcionista = new SessionService { UsuarioActual = await bd.Contexto.Usuarios.FindAsync(IdUsuarioRecepcion) };
        var servicioComoRecepcionista = NuevoServicio(bd.Contexto, sesionRecepcionista);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            servicioComoRecepcionista.CheckOutAsync(estadiaId, IdUsuarioRecepcion, MetodoPago.Efectivo, DateTime.Now));

        var estadia = await bd.Contexto.Estadias.FindAsync(estadiaId);
        Assert.Equal(EstadoEstadia.Activa, estadia!.Estado);
    }

    [Fact]
    public async Task CheckOutAsync_FechaManualAnteriorAlCheckIn_LanzaExcepcion()
    {
        using var bd = new BaseDeDatosDePrueba();
        var sesionGerencia = new SessionService { UsuarioActual = await bd.Contexto.Usuarios.FindAsync(IdUsuarioGerencia) };
        var servicio = NuevoServicio(bd.Contexto, sesionGerencia);
        var habitacion = await bd.Contexto.Habitaciones.FirstAsync(h => h.Estado == EstadoHabitacion.Disponible);

        await servicio.CheckInAsync(new NuevoCheckInDto { HabitacionId = habitacion.Id, NumeroDocumento = "12345678", NombreCompleto = "Juan Pérez", UsuarioId = IdUsuarioGerencia });
        var estadia = await bd.Contexto.Estadias.SingleAsync(e => e.HabitacionId == habitacion.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servicio.CheckOutAsync(estadia.Id, IdUsuarioGerencia, MetodoPago.Efectivo, estadia.FechaCheckIn.AddHours(-1)));
    }

    [Fact]
    public async Task CheckOutAsync_FechaManualConRolGerencia_QuedaRegistradaConEsaFecha()
    {
        using var bd = new BaseDeDatosDePrueba();
        var sesionGerencia = new SessionService { UsuarioActual = await bd.Contexto.Usuarios.FindAsync(IdUsuarioGerencia) };
        var servicio = NuevoServicio(bd.Contexto, sesionGerencia);
        var habitacion = await bd.Contexto.Habitaciones.FirstAsync(h => h.Estado == EstadoHabitacion.Disponible);

        await servicio.CheckInAsync(new NuevoCheckInDto { HabitacionId = habitacion.Id, NumeroDocumento = "12345678", NombreCompleto = "Juan Pérez", UsuarioId = IdUsuarioGerencia, FechaCheckInManual = DateTime.Now.AddDays(-2) });
        var estadia = await bd.Contexto.Estadias.SingleAsync(e => e.HabitacionId == habitacion.Id);
        var fechaCheckOutEsperada = DateTime.Now.AddDays(-1);

        await servicio.CheckOutAsync(estadia.Id, IdUsuarioGerencia, MetodoPago.Efectivo, fechaCheckOutEsperada);

        var estadiaActualizada = await bd.Contexto.Estadias.FindAsync(estadia.Id);
        Assert.Equal(fechaCheckOutEsperada, estadiaActualizada!.FechaCheckOut);
    }

    [Fact]
    public async Task EditarTarifaAsync_ComoRecepcionista_LanzaExcepcionYNoCambiaNada()
    {
        using var bd = new BaseDeDatosDePrueba();
        var habitacion = await bd.Contexto.Habitaciones.FirstAsync();
        var tarifaOriginal = habitacion.TarifaNoche;
        var sesion = new SessionService { UsuarioActual = await bd.Contexto.Usuarios.FindAsync(IdUsuarioRecepcion) };
        var servicio = NuevoServicio(bd.Contexto, sesion);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => servicio.EditarTarifaAsync(habitacion.Id, 999m, IdUsuarioRecepcion));

        var habitacionActualizada = await bd.Contexto.Habitaciones.FindAsync(habitacion.Id);
        Assert.Equal(tarifaOriginal, habitacionActualizada!.TarifaNoche);
    }

    [Fact]
    public async Task EditarTarifaAsync_ComoGerencia_ActualizaLaTarifa()
    {
        using var bd = new BaseDeDatosDePrueba();
        var habitacion = await bd.Contexto.Habitaciones.FirstAsync();
        var sesion = new SessionService { UsuarioActual = await bd.Contexto.Usuarios.FindAsync(IdUsuarioGerencia) };
        var servicio = NuevoServicio(bd.Contexto, sesion);

        await servicio.EditarTarifaAsync(habitacion.Id, 175.50m, IdUsuarioGerencia);

        var habitacionActualizada = await bd.Contexto.Habitaciones.FindAsync(habitacion.Id);
        Assert.Equal(175.50m, habitacionActualizada!.TarifaNoche);
    }

    [Fact]
    public async Task EditarTarifaAsync_TarifaCeroONegativa_LanzaExcepcion()
    {
        using var bd = new BaseDeDatosDePrueba();
        var habitacion = await bd.Contexto.Habitaciones.FirstAsync();
        var sesion = new SessionService { UsuarioActual = await bd.Contexto.Usuarios.FindAsync(IdUsuarioGerencia) };
        var servicio = NuevoServicio(bd.Contexto, sesion);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.EditarTarifaAsync(habitacion.Id, 0m, IdUsuarioGerencia));
    }

    [Fact]
    public async Task EditarTarifaAsync_NoAfectaElTotalDeUnaEstadiaYaEnCurso()
    {
        using var bd = new BaseDeDatosDePrueba();
        var sesion = new SessionService { UsuarioActual = await bd.Contexto.Usuarios.FindAsync(IdUsuarioGerencia) };
        var servicio = NuevoServicio(bd.Contexto, sesion);
        var habitacion = await bd.Contexto.Habitaciones.FirstAsync(h => h.Estado == EstadoHabitacion.Disponible);
        var tarifaAlHacerCheckIn = habitacion.TarifaNoche;

        await servicio.CheckInAsync(new NuevoCheckInDto { HabitacionId = habitacion.Id, NumeroDocumento = "12345678", NombreCompleto = "Juan Pérez", UsuarioId = IdUsuarioGerencia });
        var estadia = await bd.Contexto.Estadias.SingleAsync(e => e.HabitacionId == habitacion.Id);
        Assert.Equal(tarifaAlHacerCheckIn, estadia.TotalAcumulado);

        await servicio.EditarTarifaAsync(habitacion.Id, tarifaAlHacerCheckIn + 100m, IdUsuarioGerencia);

        var estadiaSinCambios = await bd.Contexto.Estadias.FindAsync(estadia.Id);
        Assert.Equal(tarifaAlHacerCheckIn, estadiaSinCambios!.TotalAcumulado);
    }
}
