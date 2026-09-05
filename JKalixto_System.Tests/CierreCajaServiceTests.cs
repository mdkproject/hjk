using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using JKalixto_System.Application.Services;
using JKalixto_System.Domain.Models;

namespace JKalixto_System.Tests;

public class CierreCajaServiceTests
{
    private static CierreCajaService NuevoServicio(JKalixto_System.Infrastructure.Data.AppDbContext contexto)
    {
        var sessionService = new SessionService();
        var auditoria = new AuditoriaService(contexto, sessionService);
        var gastos = new GastosService(contexto, auditoria, sessionService);
        return new CierreCajaService(contexto, auditoria, gastos);
    }

    [Fact]
    public async Task CerrarCajaAsync_ConClienteDeSaunaConSesionAbierta_LanzaExcepcionYNoCreaElCierre()
    {
        using var bd = new BaseDeDatosDePrueba();
        bd.Contexto.ClientesSauna.Add(new ClienteSauna
        {
            NumeroDocumento = "12345678",
            NombreCompleto = "Cliente con sesión abierta",
            NumeroCandado = "C1",
            Seccion = SeccionSauna.General,
            FechaIngreso = DateTime.Now,
            Estado = EstadoClienteSauna.Activo
        });
        await bd.Contexto.SaveChangesAsync();

        var servicio = NuevoServicio(bd.Contexto);

        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.CerrarCajaAsync(TurnoCaja.Manana, 1));
        Assert.Equal(0, await bd.Contexto.CierresCaja.CountAsync());
    }

    [Fact]
    public async Task CerrarCajaAsync_SinClientesDeSaunaAbiertos_CierraCorrectamente()
    {
        using var bd = new BaseDeDatosDePrueba();
        var servicio = NuevoServicio(bd.Contexto);

        await servicio.CerrarCajaAsync(TurnoCaja.Manana, 1);

        Assert.Equal(1, await bd.Contexto.CierresCaja.CountAsync());
    }
}
