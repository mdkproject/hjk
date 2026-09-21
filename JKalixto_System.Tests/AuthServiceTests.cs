using System;
using System.Threading.Tasks;
using Xunit;
using JKalixto_System.Application.Services;
using JKalixto_System.Infrastructure.Repositories;

namespace JKalixto_System.Tests;

public class AuthServiceTests
{
    private static AuthService NuevoServicio(JKalixto_System.Infrastructure.Data.AppDbContext contexto)
        => new(new UsuarioRepository(contexto), new AuditoriaService(contexto, new SessionService()));

    [Fact]
    public async Task IniciarSesionAsync_PasswordCorrecta_DevuelveExito()
    {
        using var bd = new BaseDeDatosDePrueba();
        var servicio = NuevoServicio(bd.Contexto);

        var resultado = await servicio.IniciarSesionAsync("recepcion", "1234");

        Assert.True(resultado.Exito);
        Assert.NotNull(resultado.Usuario);
    }

    [Fact]
    public async Task IniciarSesionAsync_PasswordIncorrecta_DevuelveFalloSinRevelarSiElUsuarioExiste()
    {
        using var bd = new BaseDeDatosDePrueba();
        var servicio = NuevoServicio(bd.Contexto);

        var resultado = await servicio.IniciarSesionAsync("recepcion", "clave-incorrecta");

        Assert.False(resultado.Exito);
        Assert.Null(resultado.Usuario);
    }

    [Fact]
    public async Task IniciarSesionAsync_UsuarioQueNoExiste_DevuelveFallo()
    {
        using var bd = new BaseDeDatosDePrueba();
        var servicio = NuevoServicio(bd.Contexto);

        var resultado = await servicio.IniciarSesionAsync("no-existe", "1234");

        Assert.False(resultado.Exito);
    }

    [Fact]
    public async Task IniciarSesionAsync_CincoIntentosFallidosSeguidos_BloqueaLaCuentaAunqueLaSextaTengaLaPasswordCorrecta()
    {
        using var bd = new BaseDeDatosDePrueba();
        var servicio = NuevoServicio(bd.Contexto);

        for (var i = 0; i < 5; i++)
        {
            var intento = await servicio.IniciarSesionAsync("recepcion", "clave-incorrecta");
            Assert.False(intento.Exito);
        }

        var conPasswordCorrecta = await servicio.IniciarSesionAsync("recepcion", "1234");

        Assert.False(conPasswordCorrecta.Exito);
        Assert.Contains("bloqueada", conPasswordCorrecta.Mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IniciarSesionAsync_LoginExitoso_ReseteaLosIntentosFallidosPrevios()
    {
        using var bd = new BaseDeDatosDePrueba();
        var servicio = NuevoServicio(bd.Contexto);

        await servicio.IniciarSesionAsync("recepcion", "clave-incorrecta");
        await servicio.IniciarSesionAsync("recepcion", "clave-incorrecta");
        var exitoso = await servicio.IniciarSesionAsync("recepcion", "1234");

        Assert.True(exitoso.Exito);
        Assert.Equal(0, exitoso.Usuario!.IntentosFallidos);
        Assert.Null(exitoso.Usuario.BloqueadoHasta);
    }
}
