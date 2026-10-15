using System.Security.Claims;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Tests;

/// <summary>
/// Un guardia que no estorba, para las pruebas que no van del techo.
/// </summary>
/// <remarks>
/// UserAdminService pide IGuardiaDePermisos desde que el techo por rol existe
/// (ver <see cref="TechoDeRolAlAsignarTests"/>). Las pruebas que solo miran
/// otra cosa —que el alta no sincronice con legacy, que un usuario que no
/// existe de 404— no deben depender de esa decision, asi que reciben este.
///
/// NO ES UN AGUJERO: el techo se prueba contra el guardia DE VERDAD en su
/// propio archivo, y en el servidor solo se registra GuardiaDePermisos
/// (Program.cs). Este tipo no sale del proyecto de pruebas.
/// </remarks>
internal sealed class GuardiaQueDejaPasar : IGuardiaDePermisos
{
    public Task ExigirPuedeEditarRolAsync(ClaimsPrincipal llamante, int empresaId, string roleCode,
        IReadOnlyCollection<string> clavesPedidas, CancellationToken ct) => Task.CompletedTask;

    public Task ExigirPuedeEditarOverridesAsync(ClaimsPrincipal llamante, int empresaId, Guid userId,
        IReadOnlyCollection<string> allow, IReadOnlyCollection<string> deny, CancellationToken ct) => Task.CompletedTask;

    public Task ExigirPuedeAsignarRolAsync(ClaimsPrincipal llamante, int empresaId, string? rolActual,
        string rolNuevo, CancellationToken ct) => Task.CompletedTask;

    public Task ExigirPuedeCambiarEstatusDeModuloAsync(ClaimsPrincipal llamante, int empresaId,
        string moduleKey, int idStatus, CancellationToken ct) => Task.CompletedTask;
}
