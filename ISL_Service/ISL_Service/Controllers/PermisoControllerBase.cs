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
    /// Lo que puede el usuario del token, leido UNA sola vez.
    ///
    /// Existe porque una pantalla puede tener que decidir sobre decenas de
    /// permisos en la misma peticion — el menu de reportes son 53 — y llamar a
    /// ExigirPermisoAsync 53 veces vuelve a preguntar lo mismo 53 veces.
    /// </summary>
    /// <param name="TokenValido">false = el token no dice quien es.</param>
    /// <param name="TodoPermitido">
    /// SuperAdmin, o instalacion sin modelo de permisos: pasa todo y
    /// <see cref="Permisos"/> viene vacio a proposito.
    /// </param>
    protected sealed record PermisosDelUsuario(
        bool TokenValido,
        bool TodoPermitido,
        IReadOnlyCollection<string> Permisos)
    {
        /// <summary>Si tiene ALGUNO de esos permisos.</summary>
        /// <remarks>
        /// ADEMAS del permiso fino se exige la ENTRADA al modulo.
        ///
        /// La regla es "entrada al modulo + permiso de la operacion": sin la
        /// entrada no se puede, aunque tenga los especificos. Hasta ahora esa
        /// mitad vivia SOLO en el front (cada tarjeta del inicio se enciende
        /// con su ".ver_modulo") y el backend no la miraba, asi que quien
        /// conociera la URL entraba a un modulo que no tenia concedido.
        ///
        /// Se comprueba POR CLAVE y no una sola vez al principio porque un
        /// endpoint acepta varias claves de modulos distintos (la del web y la
        /// del movil): cada una trae su propia puerta, y la del movil no tiene.
        /// Ver <see cref="EntradasDeModulo"/> para que familias se gatean y
        /// cuales no.
        /// </remarks>
        public bool Tiene(params string[] permisos)
        {
            if (TodoPermitido) return true;
            if (!TokenValido) return false;

            return permisos.Any(p => TieneLaClave(p) && TieneLaEntradaDe(p));
        }

        private bool TieneLaClave(string permiso) =>
            Permisos.Any(x =>
                string.Equals(x, permiso, StringComparison.OrdinalIgnoreCase)
                || string.Equals(SinSeparadores(x), SinSeparadores(permiso), StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Si la puerta del modulo esta abierta para esa clave. Sin puerta
        /// declarada pasa: lo que no se pudo comprobar no se cierra.
        /// </summary>
        private bool TieneLaEntradaDe(string permiso) =>
            EntradasDeModulo.PuedeEntrar(permiso, TieneLaClave);
    }

    /// <summary>
    /// Lee el snapshot de permisos del usuario del token.
    /// </summary>
    protected async Task<PermisosDelUsuario> LeerPermisosAsync(CancellationToken ct)
    {
        var userId = CurrentUser.GetUserId(User);
        if (userId is null)
            return new PermisosDelUsuario(false, false, Array.Empty<string>());

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
            return new PermisosDelUsuario(true, true, Array.Empty<string>());

        var empresaId = CurrentUser.GetCompanyId(User) ?? 0;
        var rol = CurrentUser.GetRole(User);
        var snapshot = await PermissionService.GetPermissionsAsync(userId.Value, empresaId, rol, ct);

        // Instalacion sin modelo de permisos: no hay nada contra que comparar y
        // negar dejaria muertas bases que hoy funcionan. Pasa, igual que el
        // resto de la API.
        if (!snapshot.PermissionsEnabled)
            return new PermisosDelUsuario(true, true, Array.Empty<string>());

        /*
          LAS CLAVES DEL TELEFONO SOLO CUENTAN EN UN TOKEN DEL TELEFONO.

          Este es el SEGUNDO camino que decide lo mismo (el otro es
          PermissionAuthorizationHandler). Se filtra en los dos porque es la
          misma leccion que ya esta escrita arriba en esta clase: el dia que
          haya que arreglar algo del modelo de permisos hay que acordarse de
          todos los lugares, y "acordarse" no es un plan. Si solo se filtrara en
          el handler, los controladores que heredan de aqui -Empleados,
          Asistencias, Prestamos, Clientes, Cortes...- seguirian aceptando una
          clave `app_movil.*` presentada desde un token de canal web.

          Ver CanalDeAcceso: filtrar aqui NO le quita nada a quien entra por el
          web de verdad (medido en las dos bases), y es lo que hace que
          declararse "web" para esquivar `app_movil.acceso` cueste en vez de
          regalar.
        */
        var permisosDelCanal = CanalDeAcceso.Filtrar(snapshot.Permissions, CanalDeAcceso.De(User));

        return new PermisosDelUsuario(true, false, permisosDelCanal);
    }

    /// <summary>
    /// Para la LECTURA PRINCIPAL de un modulo: la lista que se ve nada mas
    /// abrirlo. Pasa con la clave de lectura, y tambien con la entrada al
    /// modulo mas cualquier permiso de trabajo suyo.
    /// </summary>
    /// <remarks>
    /// POR QUE NO BASTA LA CLAVE DE LECTURA, Y DE DONDE SALE ESTO.
    ///
    /// En Mac31 el permiso NO decide si la pantalla se abre: decide si el BOTON
    /// se puede pulsar. Quien tiene NUEVO y no tiene VER entra igual a CONSULTA
    /// DE PROVEEDORES, ve la rejilla, y el boton VER le sale en gris. El web
    /// usaba esa misma clave ("proveedores.ver") como candado de LA PANTALLA
    /// ENTERA, asi que la misma persona se quedaba fuera.
    ///
    /// Medido el 2026-10-12 contra las dos bases, pasaba en siete sitios:
    /// proveedores (XOCHITL y JAZMIN, Tauro), productos (NAOLANY), usuarios
    /// (JANET, y QA_AMBOS en Zaragoza), catalogos y obra (DEMO en Zaragoza) y
    /// permisos_modulos (QA_PERMISOS). A todas la tarjeta se les dibujaba en el
    /// Inicio y la pantalla las rebotaba, que es el peor de los dos errores.
    ///
    /// LO QUE ESTO **NO** HACE: no concede la clave de lectura. Quien entre asi
    /// sigue sin poder abrir el detalle ni pulsar lo que no tenga; cada accion
    /// conserva su propio candado, igual que los botones grises de Mac31.
    ///
    /// Y NO ABRE LA PUERTA A QUIEN NO TRABAJA AHI: se exigen LAS DOS cosas, la
    /// entrada del modulo y algun permiso suyo. Con solo la entrada entraria
    /// DEMO, que tiene las 34 puertas de plataforma y ni un permiso de Mac31;
    /// es la misma medicion que obligo a poner dos condiciones en la tarjeta
    /// del Inicio (Web/QR_Web/docs2/src/nucleo/entradaDeModulo.ts).
    /// </remarks>
    /// <param name="modulo">La clave del modulo, sin sufijo: "proveedores".</param>
    /// <param name="clavesDeLectura">Las que bastan por si solas.</param>
    /// <param name="familias">
    /// Los prefijos de los permisos de trabajo del modulo. Si no se pasa
    /// ninguno se usa "<paramref name="modulo"/>.". Hace falta pasarlo cuando
    /// la familia no se llama como el modulo.
    /// </param>
    protected async Task<IActionResult?> ExigirLecturaDeModuloAsync(
        string modulo,
        string[] clavesDeLectura,
        CancellationToken ct,
        params string[] familias)
    {
        var puede = await LeerPermisosAsync(ct);
        if (!puede.TokenValido)
            return Unauthorized(new { ok = false, message = "Token invalido." });
        if (puede.TodoPermitido) return null;
        if (puede.Tiene(clavesDeLectura)) return null;

        var entrada = $"{modulo}.ver_modulo";
        if (puede.Tiene(entrada))
        {
            var prefijos = familias is { Length: > 0 } ? familias : new[] { $"{modulo}." };
            var trabaja = puede.Permisos.Any(p =>
                !p.EndsWith(".ver_modulo", StringComparison.OrdinalIgnoreCase)
                && prefijos.Any(pre => p.StartsWith(pre, StringComparison.OrdinalIgnoreCase)));
            if (trabaja) return null;
        }

        return StatusCode(StatusCodes.Status403Forbidden, new
        {
            ok = false,
            message = $"No tienes permiso ({string.Join(" o ", clavesDeLectura)})."
        });
    }

    /// <summary>
    /// Devuelve el 403 (o 401) a regresar si al usuario le faltan TODOS los
    /// permisos de la lista, o null si puede pasar.
    /// </summary>
    protected async Task<IActionResult?> ExigirPermisoAsync(string[] permisos, CancellationToken ct)
    {
        var puede = await LeerPermisosAsync(ct);
        if (!puede.TokenValido)
            return Unauthorized(new { ok = false, message = "Token invalido." });

        if (puede.Tiene(permisos))
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
        // Pasa por LeerPermisosAsync y por el mismo Tiene que usa
        // ExigirPermisoAsync. Antes tenia su propia comparacion, y por eso NO
        // reconocia al SuperAdmin ni normalizaba separadores: el mismo usuario
        // podia pasar el candado del endpoint y aun asi ver el DTO con los
        // sueldos borrados. Dos formas de responder la misma pregunta siempre
        // acaban respondiendo distinto.
        var puede = await LeerPermisosAsync(ct);
        if (!puede.TokenValido) return false;

        return puede.Tiene(permisos);
    }

    /// <summary>
    /// Quien y desde donde. El cliente NUNCA los manda: si viajaran en el body,
    /// cualquiera podria firmar un movimiento a nombre de otro.
    /// </summary>
    protected int IdUsuarioLegacy() => CurrentUser.GetLegacyUserId(User);

    protected string Equipo() => CurrentUser.GetUsername(User, Environment.MachineName);
}
