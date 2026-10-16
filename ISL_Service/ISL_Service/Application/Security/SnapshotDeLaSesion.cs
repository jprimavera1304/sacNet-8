using System.Security.Claims;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Security;

/// <summary>
/// LO QUE PUEDE EL USUARIO DEL TOKEN, LEIDO UNA VEZ, FUERA DE UN CONTROLADOR.
/// </summary>
/// <remarks>
/// POR QUE EXISTE SI YA ESTA PermisoControllerBase.LeerPermisosAsync
/// -----------------------------------------------------------------
/// Porque ese esta dentro de una clase base de CONTROLADORES, y la capa de
/// conexion por centro la usan los REPOSITORIOS: no heredan de ningun
/// controlador y no tienen ClaimsPrincipal a mano por herencia.
///
/// Son los mismos cuatro pasos, en el mismo orden, y estan aqui como funcion
/// PURA (sin DI, sin estado) justamente para que haya UN solo sitio donde
/// cambiarlos:
///
///   1. Sin userId el token no dice quien es.
///   2. El SuperAdmin pasa (si no, el boton se ve encendido y la accion dice
///      que no — el error que ya se pago).
///   3. Instalacion sin modelo de permisos: pasa todo.
///   4. Las claves del telefono solo cuentan en un token del telefono
///      (CanalDeAcceso.Filtrar).
///
/// PENDIENTE DECLARADO: PermisoControllerBase.LeerPermisosAsync sigue teniendo
/// su propia copia de estos cuatro pasos. Deberia delegar aqui y quedarse en
/// una linea. NO se cambia en esta entrega porque esa clase base la comparten
/// todos los controladores y hay otros agentes trabajando en ella a la vez; un
/// candado no se toca para ahorrar quince lineas mientras tres ramas lo leen.
/// Queda anotado para el coordinador.
/// </remarks>
public static class SnapshotDeLaSesion
{
    /// <param name="TokenValido">false = el token no dice quien es.</param>
    /// <param name="TodoPermitido">SuperAdmin o instalacion sin modelo de permisos.</param>
    /// <param name="Permisos">El snapshot ya filtrado por canal.</param>
    public sealed record Resultado(
        bool TokenValido,
        bool TodoPermitido,
        IReadOnlyCollection<string> Permisos)
    {
        public static readonly Resultado SinToken = new(false, false, Array.Empty<string>());

        /// <summary>
        /// Si tiene ALGUNO de esos permisos. Misma regla que
        /// PermisoControllerBase.PermisosDelUsuario.Tiene: la clave MAS la
        /// entrada al modulo, y los separadores normalizados para que el front
        /// y la API opinen igual.
        /// </summary>
        public bool Tiene(params string[] permisos)
        {
            if (TodoPermitido) return true;
            if (!TokenValido) return false;

            return permisos.Any(p => TieneLaClave(p) && EntradasDeModulo.PuedeEntrar(p, TieneLaClave));
        }

        private bool TieneLaClave(string permiso) =>
            Permisos.Any(x =>
                string.Equals(x, permiso, StringComparison.OrdinalIgnoreCase)
                || string.Equals(SinSeparadores(x), SinSeparadores(permiso), StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Deja solo letras y numeros: "ventas.cancelar", "ventas_cancelar" y
        /// "Ventas Cancelar" son la misma cadena. Es lo mismo que hace `firma`
        /// en el front (nucleo/permisos.ts).
        /// </summary>
        private static string SinSeparadores(string? valor)
        {
            if (string.IsNullOrEmpty(valor)) return string.Empty;

            var sb = new System.Text.StringBuilder(valor.Length);
            foreach (var c in valor)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }
    }

    public static async Task<Resultado> LeerAsync(
        ClaimsPrincipal user,
        ICurrentUserAccessor acceso,
        IPermissionService permisos,
        CancellationToken ct)
    {
        var userId = acceso.GetUserId(user);
        if (userId is null) return Resultado.SinToken;

        if (CurrentUser.IsSuperAdmin(user))
            return new Resultado(true, true, Array.Empty<string>());

        var empresaId = acceso.GetCompanyId(user) ?? 0;
        var rol = acceso.GetRole(user);
        var snapshot = await permisos.GetPermissionsAsync(userId.Value, empresaId, rol, ct);

        if (!snapshot.PermissionsEnabled)
            return new Resultado(true, true, Array.Empty<string>());

        var delCanal = CanalDeAcceso.Filtrar(snapshot.Permissions, CanalDeAcceso.De(user));
        return new Resultado(true, false, delCanal);
    }
}
