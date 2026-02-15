using ISL_Service.Application.Models;
using ISL_Service.Domain.Entities;

namespace ISL_Service.Application.Interfaces;

public interface IUserRepository
{
    Task<Usuario?> GetByUsuarioAsync(string usuario, CancellationToken ct);
    Task<Usuario?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<WebLoginFallbackResult?> LoginWithFallbackAsync(string usuario, string contrasenaPlano, string contrasenaHashWeb, CancellationToken ct);

    Task<bool> ExistsByUsuarioAsync(string usuario, CancellationToken ct);

    // empresaId se ignora: en este modelo por base EmpresaId siempre es 1.
    Task<List<Usuario>> ListAsync(int? empresaId, CancellationToken ct);

    Task AddAsync(Usuario user, CancellationToken ct);
    Task UpdateAsync(Usuario user, CancellationToken ct);
    Task<Usuario> UpsertWebAndLegacyAsync(
        string usuario,
        string contrasenaPlano,
        string contrasenaHashWeb,
        string nombre,
        string rol,
        bool debeCambiarContrasena,
        int estado,
        CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
