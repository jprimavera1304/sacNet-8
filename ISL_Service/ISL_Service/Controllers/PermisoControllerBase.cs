using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// El candado de permisos que comparten Empleados, Asistencias y Prestamos.
///
/// POR QUE UNA CLASE BASE Y NO COPIAR EL METODO TRES VECES
/// -------------------------------------------------------
/// Es exactamente el mismo ExigirPermisoAsync que ya viven copiado en
/// ChecadorController y VentasPedidosController. Copiarlo tres veces mas
/// significa que el dia que haya que arreglar algo del modelo de permisos —
/// por ejemplo, dejar de dejar pasar cuando PermissionsEnabled es false — hay
/// que acordarse de cinco lugares. En un candado, "acordarse" no es un plan.
/// Los dos controladores viejos se dejan como estan: cambiarlos no es parte de
/// esta entrega y ya funcionan.
///
/// POR QUE NO [Authorize(Policy = ...)]
/// ------------------------------------
/// Porque el modelo de permisos es POR EMPRESA y vive en la base: una politica
/// estatica no sabe si el tenant siquiera lo tiene prendido.
///
/// POR QUE ACEPTA VARIOS PERMISOS
/// ------------------------------
/// Un endpoint sirve a los dos canales, web y movil, y cada canal tiene su
/// propia clave (empleados.ver / app_movil.empleados.ver). Basta con tener la
/// de su canal: quien no la tiene, en su pantalla ni siquiera ve la opcion.
/// Son permisos separados a proposito — el telefono se presta y la computadora
/// de la oficina no.
/// </summary>
public abstract class PermisoControllerBase : ControllerBase
{
    protected readonly ICurrentUserAccessor CurrentUser;
    protected readonly IPermissionService PermissionService;

    protected PermisoControllerBase(ICurrentUserAccessor currentUser, IPermissionService permissionService)
    {
        CurrentUser = currentUser;
        PermissionService = permissionService;
    }

    /// <summary>
    /// Devuelve el 403 (o 401) a regresar si al usuario le faltan TODOS los
    /// permisos de la lista, o null si puede pasar.
    /// </summary>
    protected async Task<IActionResult?> ExigirPermisoAsync(string[] permisos, CancellationToken ct)
    {
        var userId = CurrentUser.GetUserId(User);
        if (userId is null)
            return Unauthorized(new { ok = false, message = "Token invalido." });

        // EL SUPERADMIN PASA.
        //
        // Faltaba, y el sintoma era el peor posible: el boton se veia encendido y
        // la accion contestaba "no tienes permiso". El front deja pasar al
        // superadmin (nucleo/permisos.ts, `puede`) y aqui no, asi que las dos
        // mitades de la aplicacion opinaban distinto sobre la misma persona.
        //
        // Un boton encendido que al pulsarlo dice que no se puede es peor que un
        // boton apagado: te hace dudar de tus permisos en vez de del programa.
        if (Application.Security.CurrentUser.IsSuperAdmin(User))
            return null;

        var empresaId = CurrentUser.GetCompanyId(User) ?? 0;
        var rol = CurrentUser.GetRole(User);
        var snapshot = await PermissionService.GetPermissionsAsync(userId.Value, empresaId, rol, ct);

        // Instalacion sin modelo de permisos: no hay nada contra que comparar y
        // negar dejaria muertas bases que hoy funcionan. Pasa, igual que el
        // resto de la API.
        if (!snapshot.PermissionsEnabled)
            return null;

        // Se compara TAMBIEN sin separadores, igual que el front.
        //
        // El catalogo de permisos se escribio a mano a lo largo de los años y
        // conviven "ventas.cancelar", "ventas_cancelar" y "Ventas Cancelar" para
        // la misma cosa. El front ya resolvia eso normalizando ("firma") y aqui
        // se comparaba exacto: un permiso guardado con guion bajo encendia el
        // boton y despues rebotaba en la API.
        //
        // No afloja la seguridad: quita los separadores, no los nombres. Sigue
        // haciendo falta tener el permiso.
        var tiene = snapshot.Permissions.Any(
            x => permisos.Any(p =>
                string.Equals(x, p, StringComparison.OrdinalIgnoreCase)
                || string.Equals(SinSeparadores(x), SinSeparadores(p), StringComparison.OrdinalIgnoreCase)));
        if (tiene)
            return null;

        return StatusCode(StatusCodes.Status403Forbidden, new { ok = false, message = "No tienes permiso para esta accion." });
    }

    /// <summary>
    /// Deja solo letras y numeros: "ventas.cancelar", "ventas_cancelar" y
    /// "Ventas Cancelar" terminan siendo la misma cadena. Es exactamente lo que
    /// hace `firma` en el front (nucleo/permisos.ts) — las dos mitades tienen
    /// que opinar igual o el boton dice una cosa y la API otra.
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

    /// <summary>
    /// Si el usuario tiene ALGUNO de esos permisos. A diferencia de
    /// ExigirPermisoAsync no bloquea: se usa para decidir si el DTO sale
    /// completo o con los campos de dinero borrados.
    /// </summary>
    protected async Task<bool> TienePermisoAsync(string[] permisos, CancellationToken ct)
    {
        var userId = CurrentUser.GetUserId(User);
        if (userId is null) return false;

        var empresaId = CurrentUser.GetCompanyId(User) ?? 0;
        var rol = CurrentUser.GetRole(User);
        var snapshot = await PermissionService.GetPermissionsAsync(userId.Value, empresaId, rol, ct);

        // Sin modelo de permisos se muestra todo, por la misma razon de arriba:
        // esas bases funcionan hoy sin este control y apagarles los sueldos
        // seria romperlas.
        if (!snapshot.PermissionsEnabled) return true;

        return snapshot.Permissions.Any(
            x => permisos.Any(p => string.Equals(x, p, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Quien y desde donde. El cliente NUNCA los manda: si viajaran en el body,
    /// cualquiera podria firmar un movimiento a nombre de otro.
    /// </summary>
    protected int IdUsuarioLegacy() => CurrentUser.GetLegacyUserId(User);

    protected string Equipo() => CurrentUser.GetUsername(User, Environment.MachineName);
}
