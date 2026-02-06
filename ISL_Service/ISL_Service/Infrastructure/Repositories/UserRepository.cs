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
        // Para update necesitamos tracking, aquí puedes dejarlo sin tracking
        // pero en updates mejor buscar con tracking (lo hará el service con otro método si quieres).
        // Para mantener simple: dejamos tracking aquí, útil para updates.
        return _db.Usuarios
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public Task<bool> ExistsByUsuarioAsync(string usuario, CancellationToken ct)
    {
        return _db.Usuarios.AnyAsync(x => x.UsuarioNombre == usuario, ct);
    }

    public Task<List<Usuario>> ListAsync(int? empresaId, CancellationToken ct)
    {
        IQueryable<Usuario> q = _db.Usuarios.AsNoTracking();

        if (empresaId.HasValue)
            q = q.Where(x => x.EmpresaId == empresaId.Value);

        return q.OrderBy(x => x.UsuarioNombre).ToListAsync(ct);
    }

    public Task AddAsync(Usuario user, CancellationToken ct)
        => _db.Usuarios.AddAsync(user, ct).AsTask();

    public Task UpdateAsync(Usuario user, CancellationToken ct)
    {
        _db.Usuarios.Update(user);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct)
        => _db.SaveChangesAsync(ct);
}
