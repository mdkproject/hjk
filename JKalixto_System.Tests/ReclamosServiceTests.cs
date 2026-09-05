using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using JKalixto_System.Application.Services;
using JKalixto_System.Domain.Models;

namespace JKalixto_System.Tests;

/// <summary>Libro de Reclamaciones (Ley N° 29571). Lo importante acá no es la UI,
/// es que los campos obligatorios por ley se validen y que la respuesta del
/// establecimiento quede con su fecha, para poder demostrar que se respondió
/// dentro del plazo si INDECOPI lo pide.</summary>
public class ReclamosServiceTests
{
    private static NuevoReclamoDto DtoBase() => new()
    {
        NombreCompleto = "Ana Torres",
        Domicilio = "Av. Siempre Viva 123",
        TipoDocumento = TipoDocumento.DNI,
        NumeroDocumento = "45678912",
        BienContratado = "Hospedaje habitación 305",
        Tipo = TipoReclamoQueja.Reclamo,
        DetalleReclamo = "El aire acondicionado no funcionó durante toda la estadía."
    };

    [Fact]
    public async Task RegistrarAsync_DatosCompletos_CreaElReclamoComoPendiente()
    {
        using var bd = new BaseDeDatosDePrueba();
        var servicio = new ReclamosService(bd.Contexto, new AuditoriaService(bd.Contexto, new SessionService()));

        var id = await servicio.RegistrarAsync(DtoBase(), usuarioId: 1);

        var reclamo = await bd.Contexto.Reclamos.SingleAsync(r => r.Id == id);
        Assert.Equal(EstadoReclamo.Pendiente, reclamo.Estado);
        Assert.Equal(TipoReclamoQueja.Reclamo, reclamo.Tipo);
        Assert.Null(reclamo.RespuestaEstablecimiento);
    }

    [Theory]
    [InlineData("", "Av. Siempre Viva 123", "45678912")]
    [InlineData("Ana Torres", "", "45678912")]
    [InlineData("Ana Torres", "Av. Siempre Viva 123", "")]
    public async Task RegistrarAsync_SinDatosObligatoriosDelConsumidor_LanzaExcepcion(string nombre, string domicilio, string documento)
    {
        using var bd = new BaseDeDatosDePrueba();
        var servicio = new ReclamosService(bd.Contexto, new AuditoriaService(bd.Contexto, new SessionService()));

        var dto = DtoBase();
        dto.NombreCompleto = nombre;
        dto.Domicilio = domicilio;
        dto.NumeroDocumento = documento;

        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.RegistrarAsync(dto, usuarioId: 1));
    }

    [Fact]
    public async Task RegistrarAsync_SinDetalleDelReclamo_LanzaExcepcion()
    {
        using var bd = new BaseDeDatosDePrueba();
        var servicio = new ReclamosService(bd.Contexto, new AuditoriaService(bd.Contexto, new SessionService()));

        var dto = DtoBase();
        dto.DetalleReclamo = "   ";

        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.RegistrarAsync(dto, usuarioId: 1));
    }

    [Fact]
    public async Task ResponderAsync_ReclamoExistente_LoMarcaComoRespondidoConFecha()
    {
        using var bd = new BaseDeDatosDePrueba();
        var servicio = new ReclamosService(bd.Contexto, new AuditoriaService(bd.Contexto, new SessionService()));
        var id = await servicio.RegistrarAsync(DtoBase(), usuarioId: 1);

        await servicio.ResponderAsync(id, "Se revisó el equipo y se le dio un descuento del 20%.", usuarioId: 2);

        var reclamo = await bd.Contexto.Reclamos.SingleAsync(r => r.Id == id);
        Assert.Equal(EstadoReclamo.Respondido, reclamo.Estado);
        Assert.NotNull(reclamo.FechaRespuesta);
        Assert.Contains("20%", reclamo.RespuestaEstablecimiento);
    }

    [Fact]
    public async Task ResponderAsync_ReclamoQueNoExiste_LanzaExcepcion()
    {
        using var bd = new BaseDeDatosDePrueba();
        var servicio = new ReclamosService(bd.Contexto, new AuditoriaService(bd.Contexto, new SessionService()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.ResponderAsync(999, "Respuesta", usuarioId: 1));
    }

    [Fact]
    public async Task ObtenerTodosAsync_DevuelveLosMasRecientesPrimero()
    {
        using var bd = new BaseDeDatosDePrueba();
        var servicio = new ReclamosService(bd.Contexto, new AuditoriaService(bd.Contexto, new SessionService()));

        var dto1 = DtoBase();
        dto1.NombreCompleto = "Primero";
        await servicio.RegistrarAsync(dto1, usuarioId: 1);

        var dto2 = DtoBase();
        dto2.NombreCompleto = "Segundo";
        await servicio.RegistrarAsync(dto2, usuarioId: 1);

        var lista = await servicio.ObtenerTodosAsync();

        Assert.Equal(2, lista.Count);
        Assert.Equal("Segundo", lista.First().NombreCompleto);
    }
}
