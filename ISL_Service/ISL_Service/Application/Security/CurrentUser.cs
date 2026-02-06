using System.Security.Claims;

namespace ISL_Service.Application.Security;

public static class CurrentUser
{
    public static Guid GetUserId(ClaimsPrincipal user)
    {
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(id))
            throw new UnauthorizedAccessException("Token inválido: falta sub/nameid.");

        return Guid.Parse(id);
    }

    public static string GetRol(ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.Role) ?? user.FindFirstValue("rol") ?? "";

    public static int GetEmpresaId(ClaimsPrincipal user)
    {
        var val = user.FindFirstValue("empresaId");
        if (string.IsNullOrWhiteSpace(val))
            return 0;

        return int.Parse(val);
    }

    public static bool IsSuperAdmin(ClaimsPrincipal user)
        => string.Equals(GetRol(user), "SuperAdmin", StringComparison.OrdinalIgnoreCase)
           || GetEmpresaId(user) == 5;
}
