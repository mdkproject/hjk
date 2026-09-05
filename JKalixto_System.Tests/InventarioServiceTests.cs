using System;
using System.Threading.Tasks;
using Xunit;
using JKalixto_System.Application.Services;
using JKalixto_System.Domain.Models;

namespace JKalixto_System.Tests;

public class InventarioServiceTests
{
    private static InventarioService NuevoServicio(JKalixto_System.Infrastructure.Data.AppDbContext contexto)
        => new(contexto, new AuditoriaService(contexto, new SessionService()));

    [Fact]
    public async Task RegistrarMovimientoAsync_CantidadCeroONegativa_LanzaExcepcion()
    {
        using var bd = new BaseDeDatosDePrueba();
        var insumo = await bd.Contexto.Insumos.FindAsync(1);
        var servicio = NuevoServicio(bd.Contexto);

        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.RegistrarMovimientoAsync(new NuevoMovimientoInventarioDto
        {
            InsumoId = insumo!.Id,
            Tipo = TipoMovimientoInventario.Entrada,
            Cantidad = 0,
            Motivo = "Prueba",
            UsuarioId = 1
        }));
    }

    [Fact]
    public async Task RegistrarMovimientoAsync_SalidaMayorAlStockDisponible_LanzaExcepcionYNoModificaStock()
    {
        using var bd = new BaseDeDatosDePrueba();
        var insumo = await bd.Contexto.Insumos.FindAsync(1);
        var stockOriginal = insumo!.StockActual;
        var servicio = NuevoServicio(bd.Contexto);

        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.RegistrarMovimientoAsync(new NuevoMovimientoInventarioDto
        {
            InsumoId = insumo.Id,
            Tipo = TipoMovimientoInventario.Salida,
            Cantidad = stockOriginal + 1,
            Motivo = "Prueba",
            UsuarioId = 1
        }));

        var insumoActualizado = await bd.Contexto.Insumos.FindAsync(insumo.Id);
        Assert.Equal(stockOriginal, insumoActualizado!.StockActual);
    }

    [Fact]
    public async Task RegistrarMovimientoAsync_SalidaQueDejaStockExactoEnCero_SePermite()
    {
        using var bd = new BaseDeDatosDePrueba();
        var insumo = await bd.Contexto.Insumos.FindAsync(1);
        var stockOriginal = insumo!.StockActual;
        var servicio = NuevoServicio(bd.Contexto);

        await servicio.RegistrarMovimientoAsync(new NuevoMovimientoInventarioDto
        {
            InsumoId = insumo.Id,
            Tipo = TipoMovimientoInventario.Salida,
            Cantidad = stockOriginal,
            Motivo = "Prueba",
            UsuarioId = 1
        });

        var insumoActualizado = await bd.Contexto.Insumos.FindAsync(insumo.Id);
        Assert.Equal(0, insumoActualizado!.StockActual);
    }

    [Fact]
    public async Task RegistrarMovimientoAsync_Entrada_AumentaElStock()
    {
        using var bd = new BaseDeDatosDePrueba();
        var insumo = await bd.Contexto.Insumos.FindAsync(1);
        var stockOriginal = insumo!.StockActual;
        var servicio = NuevoServicio(bd.Contexto);

        await servicio.RegistrarMovimientoAsync(new NuevoMovimientoInventarioDto
        {
            InsumoId = insumo.Id,
            Tipo = TipoMovimientoInventario.Entrada,
            Cantidad = 10,
            Motivo = "Reposición",
            UsuarioId = 1
        });

        var insumoActualizado = await bd.Contexto.Insumos.FindAsync(insumo.Id);
        Assert.Equal(stockOriginal + 10, insumoActualizado!.StockActual);
    }
}
