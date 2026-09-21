using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using JKalixto_System.Domain.Models;
using JKalixto_System.Infrastructure.Data;

namespace JKalixto_System.Infrastructure.Repositories;

public interface IUsuarioRepository
{
    /// <summary>Busca un usuario ACTIVO por su username. Devuelve null si no existe o está dado de baja.</summary>
    Task<Usuario?> ObtenerPorUsernameAsync(string username);

    /// <summary>Busca por Id sin filtrar por Activo — para pantallas de administración
    /// donde también hay que poder ver/reactivar un usuario dado de baja.</summary>
    Task<Usuario?> ObtenerPorIdAsync(int id);

    /// <summary>Todos los usuarios, activos e inactivos, para la pantalla de gestión.</summary>
    Task<List<Usuario>> ObtenerTodosAsync();

    /// <summary>Guarda cambios sobre un Usuario ya existente (ej. intentos fallidos,
    /// bloqueo temporal, cambio de contraseña, edición desde la pantalla de gestión).</summary>
    Task ActualizarAsync(Usuario usuario);

    /// <summary>Alta de un usuario nuevo. Devuelve el Id asignado.</summary>
    Task<int> CrearAsync(Usuario usuario);

    /// <summary>True si ya existe otro usuario (opcionalmente excluyendo excluirId, para
    /// permitir guardar una edición sin chocar contra el propio registro) con ese
    /// Username — evita duplicados al crear o renombrar un usuario.</summary>
    Task<bool> ExisteUsernameAsync(string username, int? excluirId = null);
}

public class UsuarioRepository : IUsuarioRepository
{
    private readonly AppDbContext _context;

    public UsuarioRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Usuario?> ObtenerPorUsernameAsync(string username)
    {
        return await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Username == username && u.Activo);
    }

    public async Task<Usuario?> ObtenerPorIdAsync(int id)
    {
        return await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<List<Usuario>> ObtenerTodosAsync()
    {
        return await _context.Usuarios
            .OrderBy(u => u.NombreCompleto)
            .ToListAsync();
    }

    public async Task ActualizarAsync(Usuario usuario)
    {
        // No usa _context.Usuarios.Update(usuario) directo: si YA hay otra
        // instancia de este mismo Usuario rastreada en este contexto (ej. dos
        // intentos de login seguidos dentro del mismo DbContext, como pasa en los
        // tests), Update() intenta rastrear una segunda instancia con la misma
        // clave y EF Core tira InvalidOperationException ("cannot be tracked
        // because another instance..."). Acá se detecta ese caso y se copian los
        // valores nuevos sobre la instancia YA rastreada en vez de intentar
        // adjuntar una segunda.
        var yaRastreado = _context.ChangeTracker.Entries<Usuario>()
            .FirstOrDefault(e => e.Entity.Id == usuario.Id);

        if (yaRastreado is not null)
        {
            yaRastreado.CurrentValues.SetValues(usuario);
        }
        else
        {
            _context.Usuarios.Attach(usuario);
            _context.Entry(usuario).State = EntityState.Modified;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<int> CrearAsync(Usuario usuario)
    {
        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();
        return usuario.Id;
    }

    public async Task<bool> ExisteUsernameAsync(string username, int? excluirId = null)
    {
        return await _context.Usuarios
            .AnyAsync(u => u.Username == username && (excluirId == null || u.Id != excluirId));
    }
}
