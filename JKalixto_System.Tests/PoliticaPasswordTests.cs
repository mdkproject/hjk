using Xunit;
using JKalixto_System.Application.Services;

namespace JKalixto_System.Tests;

public class PoliticaPasswordTests
{
    [Theory]
    [InlineData("corta1")]      // menos de 8 caracteres
    [InlineData("sololetras")]  // sin número
    [InlineData("12345678")]    // sin letra
    public void Validar_PasswordDebil_DevuelveError(string password)
    {
        Assert.NotNull(PoliticaPassword.Validar(password));
    }

    [Theory]
    [InlineData("clave1234")]
    [InlineData("Recepcion2026")]
    public void Validar_PasswordValida_DevuelveNull(string password)
    {
        Assert.Null(PoliticaPassword.Validar(password));
    }
}
