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

    [Fact]
    public async Task CrearInsumoAsync_DatosValidos_QuedaCreadoConElStockInicial()
    {
        using var bd = new BaseDeDatosDePrueba();
        var servicio = NuevoServicio(bd.Contexto);

        var id = await servicio.CrearInsumoAsync(new NuevoInsumoDto
        {
            Nombre = "Jabón artesanal",
            Categoria = CategoriaInsumo.HotelHabitaciones,
            UnidadMedida = "unidad",
            StockInicial = 50,
            StockMinimo = 10
        }, usuarioId: 1);

        var insumo = await bd.Contexto.Insumos.FindAsync(id);
        Assert.NotNull(insumo);
        Assert.Equal("Jabón artesanal", insumo!.Nombre);
        Assert.Equal(50, insumo.StockActual);
        Assert.Equal(10, insumo.StockMinimo);
        Assert.True(insumo.Activo);
    }

    [Fact]
    public async Task CrearInsumoAsync_NombreVacio_LanzaExcepcion()
    {
        using var bd = new BaseDeDatosDePrueba();
        var servicio = NuevoServicio(bd.Contexto);

        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.CrearInsumoAsync(new NuevoInsumoDto
        {
            Nombre = "   ",
            Categoria = CategoriaInsumo.Sauna,
            UnidadMedida = "litro",
            StockInicial = 5,
            StockMinimo = 1
        }, usuarioId: 1));
    }

    [Fact]
    public async Task EditarInsumoAsync_ActualizaNombreUnidadYMinimoSinTocarElStockActual()
    {
        using var bd = new BaseDeDatosDePrueba();
        var insumo = await bd.Contexto.Insumos.FindAsync(1);
        var stockOriginal = insumo!.StockActual;
        var servicio = NuevoServicio(bd.Contexto);

        await servicio.EditarInsumoAsync(new EditarInsumoDto
        {
            Id = insumo.Id,
            Nombre = "Nombre corregido",
            UnidadMedida = "caja",
            StockMinimo = 25
        }, usuarioId: 1);

        var insumoActualizado = await bd.Contexto.Insumos.FindAsync(insumo.Id);
        Assert.Equal("Nombre corregido", insumoActualizado!.Nombre);
        Assert.Equal("caja", insumoActualizado.UnidadMedida);
        Assert.Equal(25, insumoActualizado.StockMinimo);
        Assert.Equal(stockOriginal, insumoActualizado.StockActual);
    }

    [Fact]
    public async Task EditarInsumoAsync_InsumoQueNoExiste_LanzaExcepcion()
    {
        using var bd = new BaseDeDatosDePrueba();
        var servicio = NuevoServicio(bd.Contexto);

        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.EditarInsumoAsync(new EditarInsumoDto
        {
            Id = 999999,
            Nombre = "X",
            UnidadMedida = "unidad",
            StockMinimo = 1
        }, usuarioId: 1));
    }
}
