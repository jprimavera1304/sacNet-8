using Microsoft.AspNetCore.Authorization;

namespace ISL_Service.Application.Security;

/// <summary>
/// Permiso(s) que exige un endpoint. Si trae mas de uno, basta con tener
/// CUALQUIERA (OR), no todos.
/// </summary>
/// <remarks>
/// El caso que lo motiva: la captura de pedidos la usan dos superficies con
/// catalogos de permiso distintos. Oficina entra desde el web con
/// `ventas.pedidos.crear`; el repartidor entra desde el movil con
/// `app_movil.pedidos`. Los dos llaman a los MISMOS endpoints, asi que exigir
/// solo uno deja fuera al otro.
/// </remarks>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permission)
        : this(new[] { permission })
    {
    }

    public PermissionRequirement(IReadOnlyList<string> permissions)
    {
        Permissions = permissions;
    }

    /// <summary>Permisos aceptados. Con tener uno alcanza.</summary>
    public IReadOnlyList<string> Permissions { get; }

    /// <summary>El primero. Para mensajes y compatibilidad.</summary>
    public string Permission => Permissions.Count > 0 ? Permissions[0] : string.Empty;
}
