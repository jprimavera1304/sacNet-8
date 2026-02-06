using Microsoft.EntityFrameworkCore;
using ISL_Service.Application.Interfaces;
using ISL_Service.Domain.Entities;
using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Usuario?> GetByUsuarioAsync(string usuario, CancellationToken ct)
    {
        return _db.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UsuarioNombre == usuario, ct);
    }

    public Task<Usuario?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return _db.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }
}
