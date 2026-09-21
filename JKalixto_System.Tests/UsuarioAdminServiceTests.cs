using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using JKalixto_System.Application.Services;
using JKalixto_System.Domain.Models;
using JKalixto_System.Infrastructure.Repositories;

namespace JKalixto_System.Tests;

public class UsuarioAdminServiceTests
{
    private static (UsuarioAdminService servicio, ISessionService sesion) NuevoServicio(JKalixto_System.Infrastructure.Data.AppDbContext contexto, RolUsuario rolDeQuienOpera)
    {
        var sesion = new SessionService
        {
            UsuarioActual = new Usuario { Id = 999, Username = "quien-opera", NombreCompleto = "Quien Opera", Rol = rolDeQuienOpera, Activo = true }
        };
        var auditoria = new AuditoriaService(contexto, sesion);
        var repo = new UsuarioRepository(contexto);
        return (new UsuarioAdminService(repo, auditoria, sesion), sesion);
    }

    [Fact]
    public async Task CrearAsync_ComoGerencia_CreaElUsuarioConDebeCambiarPasswordEnTrue()
    {
        using var bd = new BaseDeDatosDePrueba();
        var (servicio, _) = NuevoServicio(bd.Contexto, RolUsuario.Gerencia);

        var id = await servicio.CrearAsync(new NuevoUsuarioDto
        {
            Username = "prueba.nueva",
            NombreCompleto = "Prueba Nueva",
            Rol = RolUsuario.Recepcionista,
            PasswordInicial = "clave123"
        }, usuarioQueCreaId: 1);

        var lista = await servicio.ObtenerTodosAsync();
        var creado = lista.First(u => u.Id == id);

        Assert.Equal("prueba.nueva", creado.Username);
        Assert.True(creado.DebeCambiarPassword);
        Assert.True(creado.Activo);
    }

    [Fact]
    public async Task CrearAsync_ComoRecepcionista_TiraExcepcionDePermiso()
    {
        using var bd = new BaseDeDatosDePrueba();
        var (servicio, _) = NuevoServicio(bd.Contexto, RolUsuario.Recepcionista);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => servicio.CrearAsync(new NuevoUsuarioDto
        {
            Username = "no.deberia.crearse",
            NombreCompleto = "No Debería Crearse",
            Rol = RolUsuario.Recepcionista,
            PasswordInicial = "clave123"
        }, usuarioQueCreaId: 1));
    }

    [Fact]
    public async Task CrearAsync_UsernameYaExistente_TiraExcepcion()
    {
        using var bd = new BaseDeDatosDePrueba();
        var (servicio, _) = NuevoServicio(bd.Contexto, RolUsuario.Gerencia);

        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.CrearAsync(new NuevoUsuarioDto
        {
            Username = "recepcion", // ya existe, sembrado por BaseDeDatosDePrueba
            NombreCompleto = "Duplicado",
            Rol = RolUsuario.Recepcionista,
            PasswordInicial = "clave123"
        }, usuarioQueCreaId: 1));
    }

    [Fact]
    public async Task ResetearPasswordAsync_FuerzaDebeCambiarPasswordYCambiaElSecurityStamp()
    {
        using var bd = new BaseDeDatosDePrueba();
        var (servicio, _) = NuevoServicio(bd.Contexto, RolUsuario.Gerencia);

        var antes = (await servicio.ObtenerTodosAsync()).First(u => u.Username == "recepcion");

        await servicio.ResetearPasswordAsync(antes.Id, "nueva-clave-temporal", usuarioQueReseteaId: 1);

        var despues = (await servicio.ObtenerTodosAsync()).First(u => u.Username == "recepcion");
        Assert.True(despues.DebeCambiarPassword);
    }

    [Fact]
    public async Task ActualizarAsync_DesactivarUsuario_QuedaInactivoEnLaLista()
    {
        using var bd = new BaseDeDatosDePrueba();
        var (servicio, _) = NuevoServicio(bd.Contexto, RolUsuario.Gerencia);

        var objetivo = (await servicio.ObtenerTodosAsync()).First(u => u.Username == "recepcion");

        await servicio.ActualizarAsync(new EditarUsuarioDto
        {
            Id = objetivo.Id,
            NombreCompleto = objetivo.NombreCompleto,
            Rol = objetivo.Rol,
            Activo = false
        }, usuarioQueEditaId: 1);

        var despues = (await servicio.ObtenerTodosAsync()).First(u => u.Id == objetivo.Id);
        Assert.False(despues.Activo);
    }
}
