using System.Threading.Tasks;
using Xunit;
using JKalixto_System.Application.Services;
using JKalixto_System.Infrastructure.Repositories;

namespace JKalixto_System.Tests;

public class AuthServiceTests
{
    private static AuthService NuevoServicio(JKalixto_System.Infrastructure.Data.AppDbContext contexto)
        => new(new UsuarioRepository(contexto));

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
}
