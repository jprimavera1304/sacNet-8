namespace ISL_Service.Application.Interfaces;

// Identidad guardada junto al refresh token para poder re-emitir el access token
// sin re-login. Se guarda solo lo necesario para reconstruir el JWT.
public record RefreshTokenIdentity(
    Guid UserId,
    int LegacyUserId,
    string UsuarioNombre,
    string Rol,
    int EmpresaId,
    string CompanyKey);

public interface IRefreshTokenRepository
{
    // Crea la tabla WRefreshToken si no existe (auto-despliegue, aditivo).
    Task EnsureSchemaAsync(CancellationToken ct);

    // Guarda un refresh token (solo su hash) con su identidad y expiracion.
    Task CreateAsync(RefreshTokenIdentity identity, byte[] tokenHash, DateTime expiraEnUtc, CancellationToken ct);

    // Devuelve la identidad si el token es valido (existe, no revocado, no expirado); si no, null.
    Task<RefreshTokenIdentity?> ValidateAsync(byte[] tokenHash, CancellationToken ct);

    // Revoca (marca) un refresh token por su hash.
    Task RevokeAsync(byte[] tokenHash, CancellationToken ct);
}
