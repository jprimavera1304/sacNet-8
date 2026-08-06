using System.Security.Claims;
using ISL_Service.Application.Security;

namespace ISL_Service.Infrastructure.Security;

/// <summary>
/// Saca del token quien esta haciendo la peticion.
///
/// Copiado TAL CUAL de Shared.Backend.Core (repo shared-back), sin cambiarle
/// una linea, para que el comportamiento sea identico al que corria en
/// produccion antes de soltar la dependencia. Los nombres de los claims tienen
/// que coincidir con los que emite JwtTokenGenerator: si aqui se cambiara uno,
/// la gente dejaria de poder entrar y no se veria hasta produccion.
/// </summary>
public sealed class CurrentUserAccessor : ICurrentUserAccessor
{
    public Guid? GetUserId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var value) ? value : null;
    }

    public int GetLegacyUserId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue("idUsuario");
        return int.TryParse(raw, out var value) && value > 0 ? value : 0;
    }

    public string GetUsername(ClaimsPrincipal user, string fallback = "Sistema")
    {
        var value = user.FindFirstValue("username")
            ?? user.FindFirstValue("preferred_username")
            ?? user.FindFirstValue("name")
            ?? user.FindFirstValue("unique_name")
            ?? user.Identity?.Name;
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    public int? GetCompanyId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue("empresaId") ?? user.FindFirstValue("companyId");
        return int.TryParse(raw, out var value) ? value : null;
    }

    public string? GetCompanyKey(ClaimsPrincipal user)
    {
        var key = user.FindFirstValue("companyKey")?.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(key) ? null : key;
    }

    public string GetRole(ClaimsPrincipal user)
    {
        return (user.FindFirstValue("rolLegacy")
            ?? user.FindFirstValue(ClaimTypes.Role)
            ?? string.Empty).Trim();
    }
}
