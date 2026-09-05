using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using JKalixto_System.Application.Services;
using JKalixto_System.Domain.Models;

namespace JKalixto_System.Tests;

public class GastosServiceTests
{
    private const int IdUsuarioRecepcion = 5;   // seed: "recepcion", RolUsuario.Recepcionista
    private const int IdUsuarioGerencia = 1;    // seed: "gerencia.1", RolUsuario.Gerencia

    private static GastosService NuevoServicio(JKalixto_System.Infrastructure.Data.AppDbContext contexto, ISessionService sessionService)
        => new(contexto, new AuditoriaService(contexto, sessionService), sessionService);

    private static NuevoMovimientoCajaDto DtoBase(decimal monto) => new()
    {
        Direccion = DireccionMovimiento.Salida,
        Categoria = CategoriaMovimientoCaja.GastosDiarios,
        Descripcion = "Compra de insumos de limpieza",
        Monto = monto,
        OrigenCaja = OrigenCajaChica.Hotel,
        UsuarioId = IdUsuarioRecepcion
    };

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task RegistrarMovimientoAsync_MontoCeroONegativo_LanzaExcepcion(decimal monto)
    {
        using var bd = new BaseDeDatosDePrueba();
        var servicio = NuevoServicio(bd.Contexto, new SessionService());

        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.RegistrarMovimientoAsync(DtoBase(monto)));
    }

    [Fact]
    public async Task RegistrarMovimientoAsync_DescripcionVacia_LanzaExcepcion()
    {
        using var bd = new BaseDeDatosDePrueba();
        var servicio = NuevoServicio(bd.Contexto, new SessionService());
        var dto = DtoBase(50m);
        dto.Descripcion = "   ";

        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.RegistrarMovimientoAsync(dto));
    }

    [Fact]
    public async Task RegistrarMovimientoAsync_DatosValidos_QuedaRegistradoConSuLogDeAuditoria()
    {
        using var bd = new BaseDeDatosDePrueba();
        var servicio = NuevoServicio(bd.Contexto, new SessionService());

        var id = await servicio.RegistrarMovimientoAsync(DtoBase(75m));

        var movimiento = await bd.Contexto.MovimientosCaja.FindAsync(id);
        Assert.NotNull(movimiento);
        Assert.Equal(75m, movimiento!.Monto);

        var hayLog = await bd.Contexto.LogsAuditoria.AnyAsync(l => l.TipoAccion == "MOVIMIENTO_CAJA" && l.EntidadId == id);
        Assert.True(hayLog, "El movimiento de caja debe quedar registrado en el log de auditoría.");
    }

    [Fact]
    public async Task EliminarMovimientoAsync_UsuarioRecepcionista_NoTienePermisoYNoBorraNada()
    {
        using var bd = new BaseDeDatosDePrueba();
        var sessionAlRegistrar = new SessionService();
        var servicioParaRegistrar = NuevoServicio(bd.Contexto, sessionAlRegistrar);
        var id = await servicioParaRegistrar.RegistrarMovimientoAsync(DtoBase(50m));

        var sesionRecepcionista = new SessionService { UsuarioActual = await bd.Contexto.Usuarios.FindAsync(IdUsuarioRecepcion) };
        var servicio = NuevoServicio(bd.Contexto, sesionRecepcionista);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => servicio.EliminarMovimientoAsync(id, IdUsuarioRecepcion));

        Assert.NotNull(await bd.Contexto.MovimientosCaja.FindAsync(id));
    }

    [Fact]
    public async Task EliminarMovimientoAsync_UsuarioGerencia_EliminaCorrectamente()
    {
        using var bd = new BaseDeDatosDePrueba();
        var servicioParaRegistrar = NuevoServicio(bd.Contexto, new SessionService());
        var id = await servicioParaRegistrar.RegistrarMovimientoAsync(DtoBase(50m));

        var sesionGerencia = new SessionService { UsuarioActual = await bd.Contexto.Usuarios.FindAsync(IdUsuarioGerencia) };
        var servicio = NuevoServicio(bd.Contexto, sesionGerencia);

        await servicio.EliminarMovimientoAsync(id, IdUsuarioGerencia);

        Assert.Null(await bd.Contexto.MovimientosCaja.FindAsync(id));
    }
}
