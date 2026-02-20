using System.Security.Claims;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;

namespace ISL_Service.Infrastructure.Security;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionService _permissionService;

    public PermissionAuthorizationHandler(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
            return;

        var rolLegacy = context.User.FindFirstValue("rolLegacy")
            ?? context.User.FindFirstValue(ClaimTypes.Role)
            ?? string.Empty;

        if (string.Equals(rolLegacy, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
            return;
        }

        var sub = context.User.FindFirstValue("sub") ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var empresaIdRaw = context.User.FindFirstValue("empresaId");
        if (!Guid.TryParse(sub, out var userId) || !int.TryParse(empresaIdRaw, out var empresaId))
            return;

        var snapshot = await _permissionService.GetPermissionsAsync(
            userId,
            empresaId,
            rolLegacy,
            CancellationToken.None);

        if (snapshot.PermissionsEnabled)
        {
            if (snapshot.Permissions.Any(p => string.Equals(p, requirement.Permission, StringComparison.OrdinalIgnoreCase)))
                context.Succeed(requirement);
            else if (CanUseLegacyVerAsModuleView(snapshot.Permissions, requirement.Permission))
                context.Succeed(requirement);
            return;
        }

        if (_permissionService.IsAllowedByLegacy(rolLegacy, requirement.Permission))
            context.Succeed(requirement);
    }

    private static bool CanUseLegacyVerAsModuleView(IReadOnlyCollection<string> permissions, string requiredPermission)
    {
        var required = (requiredPermission ?? string.Empty).Trim().ToLowerInvariant();
        if (!required.EndsWith(".ver_modulo", StringComparison.Ordinal))
            return false;

        var legacyEquivalent = required[..^("_modulo".Length)];
        return permissions.Any(p => string.Equals((p ?? string.Empty).Trim(), legacyEquivalent, StringComparison.OrdinalIgnoreCase));
    }
}
