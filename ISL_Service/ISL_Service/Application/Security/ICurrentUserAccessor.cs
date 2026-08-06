using System.Security.Claims;

namespace ISL_Service.Application.Security;

/// <summary>
/// Lee del token quien esta haciendo la peticion.
///
/// Venia del paquete Shared.Backend.Core (repo shared-back), publicado en
/// GitHub Packages. Se trajo aqui tal cual para que este backend no dependa de
/// ese feed: bajarlo requeria un token que caduca, y el dia que caduco tumbo el
/// build sin que el codigo tuviera nada malo.
///
/// De todo aquel paquete (CORS, Swagger, JWT, politicas de permisos) esto era
/// lo unico que se usaba; lo demas ya estaba resuelto con codigo propio.
/// </summary>
public interface ICurrentUserAccessor
{
    Guid? GetUserId(ClaimsPrincipal user);
    int GetLegacyUserId(ClaimsPrincipal user);
    string GetUsername(ClaimsPrincipal user, string fallback = "Sistema");
    int? GetCompanyId(ClaimsPrincipal user);
    string? GetCompanyKey(ClaimsPrincipal user);
    string GetRole(ClaimsPrincipal user);
}
