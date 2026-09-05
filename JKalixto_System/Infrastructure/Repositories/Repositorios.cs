using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using JKalixto_System.Domain.Models;
using JKalixto_System.Infrastructure.Data;

namespace JKalixto_System.Infrastructure.Repositories;

public interface IUsuarioRepository
{
    /// <summary>Busca un usuario ACTIVO por su username. Devuelve null si no existe o está dado de baja.</summary>
    Task<Usuario?> ObtenerPorUsernameAsync(string username);
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
}
