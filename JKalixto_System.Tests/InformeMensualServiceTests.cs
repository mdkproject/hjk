using System;
using System.Threading.Tasks;
using Xunit;
using JKalixto_System.Application.Services;
using JKalixto_System.Domain.Models;
using JKalixto_System.Infrastructure.Data;

namespace JKalixto_System.Tests;

public class InformeMensualServiceTests
{
    private static InformeMensualService NuevoServicio(AppDbContext contexto) => new(contexto);

    private static MovimientoCaja Movimiento(DateTime fecha, decimal monto, string descripcion) => new()
    {
        FechaHora = fecha,
        Direccion = DireccionMovimiento.Ingreso,
        Categoria = CategoriaMovimientoCaja.GastosDiarios,
        Descripcion = descripcion,
        Monto = monto,
        OrigenCaja = OrigenCajaChica.Hotel,
        MetodoPago = MetodoPago.Efectivo,
        UsuarioId = 1
    };

    [Fact]
    public async Task ObtenerInformePorRangoAsync_SumaSoloMovimientosDentroDelRango()
    {
        using var bd = new BaseDeDatosDePrueba();
        var contexto = bd.Contexto;

        contexto.MovimientosCaja.Add(Movimiento(new DateTime(2026, 3, 10), 100m, "Dentro del rango"));
        contexto.MovimientosCaja.Add(Movimiento(new DateTime(2026, 3, 20), 999m, "Fuera del rango"));
        await contexto.SaveChangesAsync();

        var servicio = NuevoServicio(contexto);
        var informe = await servicio.ObtenerInformePorRangoAsync(new DateTime(2026, 3, 1), new DateTime(2026, 3, 15));

        Assert.Equal(100m, informe.IngresoTotal);
    }

    [Fact]
    public async Task ObtenerInformePorRangoAsync_UnSoloDia_IncluyeEseDiaCompletoHastaLaMedianoche()
    {
        using var bd = new BaseDeDatosDePrueba();
        var contexto = bd.Contexto;

        contexto.MovimientosCaja.Add(Movimiento(new DateTime(2026, 3, 10, 23, 30, 0), 55m, "Tarde en el día"));
        await contexto.SaveChangesAsync();

        var servicio = NuevoServicio(contexto);
        var informe = await servicio.ObtenerInformePorRangoAsync(new DateTime(2026, 3, 10), new DateTime(2026, 3, 10));

        Assert.Equal(55m, informe.IngresoTotal);
    }

    [Fact]
    public async Task ObtenerInformePorRangoAsync_HastaAnteriorADesde_LanzaExcepcion()
    {
        using var bd = new BaseDeDatosDePrueba();
        var servicio = NuevoServicio(bd.Contexto);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servicio.ObtenerInformePorRangoAsync(new DateTime(2026, 3, 15), new DateTime(2026, 3, 1)));
    }

    [Fact]
    public async Task ObtenerInformePorRangoAsync_SaldoAnteriorAcumulaTodoAntesDelInicio()
    {
        using var bd = new BaseDeDatosDePrueba();
        var contexto = bd.Contexto;

        contexto.MovimientosCaja.Add(Movimiento(new DateTime(2026, 2, 1), 200m, "Antes del rango"));
        contexto.MovimientosCaja.Add(Movimiento(new DateTime(2026, 3, 10), 50m, "Dentro del rango"));
        await contexto.SaveChangesAsync();

        var servicio = NuevoServicio(contexto);
        var informe = await servicio.ObtenerInformePorRangoAsync(new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));

        Assert.Equal(200m, informe.SaldoAnterior);
        Assert.Equal(50m, informe.IngresoTotal);
    }
}
