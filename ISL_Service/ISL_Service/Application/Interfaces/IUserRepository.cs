using ISL_Service.Domain.Entities;

namespace ISL_Service.Application.Interfaces;

public interface IUserRepository
{
    Task<Usuario?> GetByUsuarioAsync(string usuario, CancellationToken ct);
    Task<Usuario?> GetByIdAsync(Guid id, CancellationToken ct);
}
