using ISL_Service.Domain.Entities;

namespace ISL_Service.Application.Interfaces;

public interface IUserRepository
{
    Task<Usuario?> GetByUsuarioAsync(string usuario, CancellationToken ct);
    Task<Usuario?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<bool> ExistsByUsuarioAsync(string usuario, CancellationToken ct);

    // empresaId null => todos (solo lo usará SuperAdmin)
    Task<List<Usuario>> ListAsync(int? empresaId, CancellationToken ct);

    Task AddAsync(Usuario user, CancellationToken ct);
    Task UpdateAsync(Usuario user, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
