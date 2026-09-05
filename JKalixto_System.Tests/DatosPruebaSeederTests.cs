using System.Linq;
using Xunit;
using JKalixto_System.Domain.Models;
using JKalixto_System.Infrastructure.Data;

namespace JKalixto_System.Tests;

/// <summary>
/// Cubre exactamente el tipo de bug que motivó esta prueba: DatosPruebaSeeder lee
/// Habitaciones/Estadias para MODIFICARLAS (Estado, TotalAcumulado) usando el mismo
/// AppDbContext que el resto de la app — que tiene NoTracking por defecto (ver
/// MauiProgram.cs). Sin ".AsTracking()" en esas lecturas puntuales, las Estadias sí
/// se creaban, pero el cambio de Habitacion.Estado a Ocupada/Limpieza/Mantenimiento
/// se perdía en silencio: todas las habitaciones se seguían viendo "Disponible"
/// aunque hubiera huéspedes cargados.
/// </summary>
public class DatosPruebaSeederTests
{
    [Fact]
    public void Sembrar_CreaHuespedes_YSusHabitacionesQuedanRealmenteOcupadas()
    {
        using var bd = new BaseDeDatosDePrueba();
        var usuarioId = bd.Contexto.Usuarios.First().Id;

        DatosPruebaSeeder.Sembrar(bd.Contexto, usuarioId);

        // Releer desde una conexión nueva: si el cambio no se guardó de verdad,
        // esto lo detecta aunque el contexto original tuviera el dato "en memoria".
        using var verificacion = bd.NuevoContexto();

        var estadiasActivas = verificacion.Estadias.Where(e => e.Estado == EstadoEstadia.Activa).ToList();
        Assert.Equal(5, estadiasActivas.Count);

        foreach (var estadia in estadiasActivas)
        {
            var habitacion = verificacion.Habitaciones.Single(h => h.Id == estadia.HabitacionId);
            Assert.Equal(EstadoHabitacion.Ocupada, habitacion.Estado);
        }
    }

    [Fact]
    public void Sembrar_DejaAlgunasHabitacionesEnLimpiezaYMantenimiento()
    {
        using var bd = new BaseDeDatosDePrueba();
        var usuarioId = bd.Contexto.Usuarios.First().Id;

        DatosPruebaSeeder.Sembrar(bd.Contexto, usuarioId);

        using var verificacion = bd.NuevoContexto();

        Assert.Equal(3, verificacion.Habitaciones.Count(h => h.Estado == EstadoHabitacion.LimpiezaSalida));

        var enMantenimiento = verificacion.Habitaciones.Where(h => h.Estado == EstadoHabitacion.Mantenimiento).ToList();
        Assert.Equal(2, enMantenimiento.Count);
        Assert.All(enMantenimiento, h => Assert.False(string.IsNullOrWhiteSpace(h.MotivoMantenimiento)));
    }

    [Fact]
    public void Sembrar_LaVentaCargadaAHabitacion_SumaRealmenteAlTotalAcumulado()
    {
        using var bd = new BaseDeDatosDePrueba();
        var usuarioId = bd.Contexto.Usuarios.First().Id;

        DatosPruebaSeeder.Sembrar(bd.Contexto, usuarioId);

        using var verificacion = bd.NuevoContexto();

        var ventaCargada = verificacion.VentasSauna.SingleOrDefault(v => v.Estado == EstadoVenta.CargadaAHabitacion);
        Assert.NotNull(ventaCargada);

        var estadia = verificacion.Estadias.Single(e => e.Id == ventaCargada!.EstadiaHotelDestinoId);
        var habitacion = verificacion.Habitaciones.Single(h => h.Id == estadia.HabitacionId);

        // TotalAcumulado debe ser al menos la tarifa de la 1ra noche + el total de esa
        // venta — si el ".AsTracking()" faltara acá, TotalAcumulado se quedaría solo
        // en la tarifa de la noche, sin sumar el consumo cargado.
        Assert.True(estadia.TotalAcumulado >= habitacion.TarifaNoche + ventaCargada!.Total);
    }

    [Fact]
    public void Sembrar_LlamadoDosVeces_NoDuplicaLosDatos()
    {
        using var bd = new BaseDeDatosDePrueba();
        var usuarioId = bd.Contexto.Usuarios.First().Id;

        DatosPruebaSeeder.Sembrar(bd.Contexto, usuarioId);
        DatosPruebaSeeder.Sembrar(bd.Contexto, usuarioId);

        Assert.Equal(5, bd.Contexto.Estadias.Count());
        Assert.Equal(5, bd.Contexto.ClientesSauna.Count());
    }
}
