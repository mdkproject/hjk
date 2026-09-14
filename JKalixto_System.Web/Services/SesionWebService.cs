using Microsoft.EntityFrameworkCore;
using JKalixto_System.Application.Services;
using JKalixto_System.Domain.Models;
using JKalixto_System.Infrastructure.Data;

namespace JKalixto_System.Web.Services;

/// <summary>
/// PARCHE TEMPORAL — hasta que se construya una pantalla de login real para la
/// versión web (fuera de alcance de esta fase, que se enfoca en portar
/// funcionalidad de negocio, no autenticación).
///
/// Check-in, check-out, mantenimiento, etc. necesitan un "usuarioId" para dejar
/// registrado quién hizo cada acción (auditoría). En MAUI eso lo resuelve
/// ISessionService, fijado una sola vez al arrancar la app (ver
/// MauiProgram.ModoPruebaSinLogin). Acá, como todavía no hay pantalla de login,
/// se hace lo mismo pero por CIRCUITO (cada pestaña/usuario conectado): la
/// primera vez que una página pide el usuario actual, se busca el primer
/// usuario activo de la base y se fija para el resto de esa sesión de
/// navegador.
/// </summary>
public class SesionWebService
{
    private readonly ISessionService _sessionService;
    private readonly AppDbContext _db;

    public SesionWebService(ISessionService sessionService, AppDbContext db)
    {
        _sessionService = sessionService;
        _db = db;
    }

    public async Task<Usuario> ObtenerUsuarioActualAsync()
    {
        if (_sessionService.UsuarioActual is { } usuarioYaFijado)
        {
            return usuarioYaFijado;
        }

        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Activo)
            ?? throw new InvalidOperationException(
                "No hay ningún usuario activo en la base de datos — no se puede continuar sin al menos uno.");

        _sessionService.UsuarioActual = usuario;
        return usuario;
    }
}
