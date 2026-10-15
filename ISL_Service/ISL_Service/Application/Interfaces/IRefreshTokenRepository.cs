using ISL_Service.Application.Security;

namespace ISL_Service.Application.Interfaces;

// Identidad guardada junto al refresh token para poder re-emitir el access token
// sin re-login. Se guarda solo lo necesario para reconstruir el JWT.
public record RefreshTokenIdentity(
    Guid UserId,
    int LegacyUserId,
    string UsuarioNombre,
    string Rol,
    int EmpresaId,
    string CompanyKey,
    /*
      EL CANAL VIAJA CON EL REFRESH, NO SE VUELVE A PREGUNTAR.

      Si al renovar se tomara el canal de lo que diga el cliente, todo el
      diseno se cae: bastaria con pedir un refresh diciendo "web" para salir
      del canal movil y esquivar `app_movil.acceso`. Asi que el canal se guarda
      en `dbo.WRefreshToken.Canal` al emitirlo y el refresh solo lo ARRASTRA.
      El cuerpo del /api/auth/refresh no tiene ni donde declararlo.

      `Canal.NoDeclarado` son los renglones escritos antes de este cambio (el
      ALTER los deja en NULL); se leen como MOVIL, no como web, porque esta
      tabla la usa SOLO la app: "Almacen de refresh tokens (solo para la app
      movil)" (RefreshTokenRepository.cs) y el web nunca pide refresh
      (LoginRequest.IncluirRefresh). Leerlos como web les quitaria sus claves
      `app_movil.*` y dejaria sin pedidos a cada repartidor que tenga la app
      instalada, sin que nadie cambiara un permiso.
    */
    Canal CanalSesion = Canal.NoDeclarado);

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
