using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using JKalixto_System.Application.Services;
using JKalixto_System.Domain.Models;
using JKalixto_System.Infrastructure.Data;

namespace JKalixto_System.Web.Services;

/// <summary>
/// Resuelve el <see cref="Usuario"/> real detrás de la cookie de login (ver el
/// endpoint POST /login en Program.cs) y lo deja fijado en
/// <see cref="ISessionService.UsuarioActual"/> para el resto del circuito —
/// necesario porque check-in, check-out, mantenimiento, etc. necesitan un
/// "usuarioId" para dejar registrado quién hizo cada acción, y porque
/// <c>AuditoriaService</c> (capa compartida con MAUI) lee
/// <c>ISessionService.UsuarioActual?.Rol</c> directamente para su propio
/// chequeo de permiso — por eso este servicio SIGUE poblando esa propiedad,
/// aunque ahora el origen del dato ya no es "el primer usuario activo" sino la
/// sesión real autenticada.
/// </summary>
public class SesionWebService
{
    private readonly ISessionService _sessionService;
    private readonly AppDbContext _db;
    private readonly AuthenticationStateProvider _authStateProvider;

    public SesionWebService(ISessionService sessionService, AppDbContext db, AuthenticationStateProvider authStateProvider)
    {
        _sessionService = sessionService;
        _db = db;
        _authStateProvider = authStateProvider;
    }

    public async Task<Usuario> ObtenerUsuarioActualAsync()
    {
        if (_sessionService.UsuarioActual is { } usuarioYaFijado)
        {
            return usuarioYaFijado;
        }

        var estadoAuth = await _authStateProvider.GetAuthenticationStateAsync();
        var username = estadoAuth.User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new InvalidOperationException("No hay una sesión iniciada.");
        }

        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Username == username && u.Activo)
            ?? throw new InvalidOperationException($"El usuario '{username}' no existe o está inactivo.");

        _sessionService.UsuarioActual = usuario;
        return usuario;
    }
}
