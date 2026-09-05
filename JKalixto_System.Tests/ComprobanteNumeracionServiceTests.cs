using System.Threading.Tasks;
using Xunit;
using JKalixto_System.Application.Services;
using JKalixto_System.Domain.Models;

namespace JKalixto_System.Tests;

/// <summary>
/// La numeración de comprobantes es la parte de "facturación SUNAT" que se puede
/// resolver sin contratar un PSE/OSE: que el correlativo nunca se repita ni salte.
/// Estas pruebas verifican justo eso, y que Boleta y Factura llevan series (y
/// contadores) independientes entre sí.
/// </summary>
public class ComprobanteNumeracionServiceTests
{
    [Fact]
    public async Task ObtenerSiguienteNumeroAsync_PrimeraVez_EmpiezaEnUno()
    {
        using var bd = new BaseDeDatosDePrueba();
        var servicio = new ComprobanteNumeracionService(bd.Contexto);

        var numero = await servicio.ObtenerSiguienteNumeroAsync(TipoComprobante.Boleta);

        Assert.Equal("B001-00000001", numero);
    }

    [Fact]
    public async Task ObtenerSiguienteNumeroAsync_LlamadasSucesivas_IncrementaSinRepetir()
    {
        using var bd = new BaseDeDatosDePrueba();
        var servicio = new ComprobanteNumeracionService(bd.Contexto);

        var primero = await servicio.ObtenerSiguienteNumeroAsync(TipoComprobante.Boleta);
        var segundo = await servicio.ObtenerSiguienteNumeroAsync(TipoComprobante.Boleta);
        var tercero = await servicio.ObtenerSiguienteNumeroAsync(TipoComprobante.Boleta);

        Assert.Equal("B001-00000001", primero);
        Assert.Equal("B001-00000002", segundo);
        Assert.Equal("B001-00000003", tercero);
    }

    [Fact]
    public async Task ObtenerSiguienteNumeroAsync_BoletaYFactura_LlevanContadoresIndependientes()
    {
        using var bd = new BaseDeDatosDePrueba();
        var servicio = new ComprobanteNumeracionService(bd.Contexto);

        var boleta1 = await servicio.ObtenerSiguienteNumeroAsync(TipoComprobante.Boleta);
        var factura1 = await servicio.ObtenerSiguienteNumeroAsync(TipoComprobante.Factura);
        var boleta2 = await servicio.ObtenerSiguienteNumeroAsync(TipoComprobante.Boleta);

        Assert.Equal("B001-00000001", boleta1);
        Assert.Equal("F001-00000001", factura1);
        Assert.Equal("B001-00000002", boleta2);
    }

    [Fact]
    public async Task ObtenerSiguienteNumeroAsync_DesdeDosConexionesCasiSimultaneas_NuncaRepiteElNumero()
    {
        using var bd = new BaseDeDatosDePrueba();

        // Dos "cajas" distintas (dos AppDbContext, como en la app real) generando un
        // comprobante casi al mismo tiempo — el caso que la transacción del servicio
        // tiene que blindar.
        await using var contexto1 = bd.NuevoContexto();
        await using var contexto2 = bd.NuevoContexto();
        var servicio1 = new ComprobanteNumeracionService(contexto1);
        var servicio2 = new ComprobanteNumeracionService(contexto2);

        var tarea1 = Task.Run(() => servicio1.ObtenerSiguienteNumeroAsync(TipoComprobante.Boleta));
        var tarea2 = Task.Run(() => servicio2.ObtenerSiguienteNumeroAsync(TipoComprobante.Boleta));

        var numeros = await Task.WhenAll(tarea1, tarea2);

        Assert.NotEqual(numeros[0], numeros[1]);
    }
}
