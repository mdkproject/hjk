using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using JKalixto_System.Application.Services;
using JKalixto_System.Domain.Models;

namespace JKalixto_System.Tests;

/// <summary>
/// ADR ("Average Daily Rate") y RevPAR ("Revenue per Available Room") son los dos
/// KPIs que le faltaban al Dashboard frente a un PMS real (ver Servicios.cs,
/// DashboardService.ObtenerResumenAsync). Se calculan sobre TarifaNoche de las
/// habitaciones ocupadas, no sobre TotalAcumulado de la Estadia, así que estas
/// pruebas fijan tarifas conocidas para verificar la fórmula exacta.
/// </summary>
public class DashboardServiceTests
{
    [Fact]
    public async Task ObtenerResumenAsync_ConHabitacionesOcupadas_CalculaAdrYRevParCorrectamente()
    {
        using var bd = new BaseDeDatosDePrueba();

        var habitaciones = await bd.Contexto.Habitaciones.OrderBy(h => h.Id).ToListAsync();
        var totalHabitaciones = habitaciones.Count;

        var habitacion1 = habitaciones[0];
        habitacion1.Estado = EstadoHabitacion.Ocupada;
        habitacion1.TarifaNoche = 100m;

        var habitacion2 = habitaciones[1];
        habitacion2.Estado = EstadoHabitacion.Ocupada;
        habitacion2.TarifaNoche = 200m;

        await bd.Contexto.SaveChangesAsync();

        var servicio = new DashboardService(bd.Contexto);
        var resumen = await servicio.ObtenerResumenAsync();

        // ADR = suma de tarifas de las ocupadas / cantidad de ocupadas.
        Assert.Equal(150m, resumen.TarifaPromedioDiaria);

        // RevPAR = suma de tarifas de las ocupadas / TOTAL de habitaciones (no solo
        // las ocupadas) — por eso siempre es menor o igual al ADR.
        var revParEsperado = System.Math.Round(300m / totalHabitaciones, 2);
        Assert.Equal(revParEsperado, resumen.IngresoPorHabitacionDisponible);
        Assert.True(resumen.IngresoPorHabitacionDisponible <= resumen.TarifaPromedioDiaria);
    }

    [Fact]
    public async Task ObtenerResumenAsync_SinHabitacionesOcupadas_AdrYRevParSonCero()
    {
        using var bd = new BaseDeDatosDePrueba();
        var servicio = new DashboardService(bd.Contexto);

        var resumen = await servicio.ObtenerResumenAsync();

        Assert.Equal(0m, resumen.TarifaPromedioDiaria);
        Assert.Equal(0m, resumen.IngresoPorHabitacionDisponible);
    }
}
