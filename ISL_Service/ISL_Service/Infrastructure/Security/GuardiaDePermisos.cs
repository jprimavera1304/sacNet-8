using System.Security.Claims;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Models;
using ISL_Service.Application.Security;

namespace ISL_Service.Infrastructure.Security;

/// <summary>
/// LAS CUATRO REGLAS QUE EVITAN QUE EL MODELO DE PERMISOS SE USE CONTRA SI MISMO.
/// </summary>
/// <remarks>
/// EL AGUJERO QUE CIERRA, MEDIDO
/// -----------------------------
/// Ver <see cref="LlavesDeGobierno"/> para las cuatro peticiones con curl que
/// se hicieron ANTES de escribir esto y que contestaron 200: concederse llaves
/// propias, degradar el rol de sistema ADMIN, firmarse ajustes individuales y
/// apagar la pantalla de permisos sin vuelta posible. Los cuatro frenos
/// existian, pero en el front.
///
/// LAS REGLAS
/// ----------
/// 1. TECHO DEL ADMINISTRADOR. Nadie concede ni retira una clave que el mismo
///    no tenga. Es una sola regla y de ella sale gratis lo demas: nadie puede
///    darse lo que no tiene (ni a su rol ni a si mismo, porque para concederla
///    tendria que tenerla ya), y nadie puede tocar un rol que maneja llaves
///    por encima de las suyas.
///
///    Se mira lo que se CONCEDE y lo que se RETIRA, no la lista entera: el
///    endpoint recibe el juego completo de claves del rol, asi que si solo se
///    mirara "¿tiene todo lo que manda?" un administrador no podria dejar un
///    rol como estaba. Y se mira tambien lo que se RETIRA porque quitar una
///    clave que no puedes devolver es tan grave como concederla: seria la
///    forma de desarmar a quien esta por encima.
///
/// 2. LOS ROLES ADMINISTRATIVOS DE LA INSTALACION. Un rol de sistema
///    (WRol.EsSistema = 1) que hoy tiene alguna llave de gobierno solo lo
///    edita quien tiene TODAS las llaves de gobierno. La regla 1 ya impide
///    degradarlo a medias; esta impide los juegos de suma cero — dos cuentas
///    con la mitad de las llaves cada una turnandose para recortar al otro — y
///    deja claro en una linea quien manda sobre ADMIN y SUPER_ADMIN.
///
/// 3. NUNCA EL ULTIMO ACCESO ADMINISTRATIVO. Ningun cambio —de rol, de ajustes
///    individuales o de estatus de modulo— se guarda si despues NADIE podria
///    conceder permisos, o NADIE podria volver a prender un modulo. Se
///    pregunta simulando el cambio, no guardandolo (ver
///    <see cref="SimulacionDePermisos"/>).
///
/// 4. EL MENSAJE DICE QUE HACER. Un 403 que solo dice "no tienes permiso"
///    convierte un error de configuracion en una llamada de telefono. Los de
///    aqui nombran las claves que faltan, o a quien hay que darle la llave
///    antes de reintentar.
///
/// LO QUE NO SE HIZO, Y POR QUE NO
/// -------------------------------
/// NO HAY PUERTA TRASERA. Se descarto a proposito el patron de Mac31 —"si el
/// equipo se llama X", "si el IDUsuario es 3"— que es justo lo que esta
/// migracion esta dejando atras: no se puede auditar, no se puede revocar y el
/// dia que se filtra no hay forma de cerrarlo. La recuperacion es la regla 3:
/// el sistema SE NIEGA a quedarse sin administradores, asi que nunca hace
/// falta una llave maestra para volver a entrar.
///
/// El rol legacy "SuperAdmin" si cuenta como administrador en la regla 3, pero
/// eso no es una puerta nueva: PermissionAuthorizationHandler ya lo deja pasar
/// antes de mirar el snapshot, y es una cuenta nominal que se ve en la tabla
/// UsuarioWeb. No contarlo haria que el guardia negara cambios inofensivos en
/// las bases que si tienen uno (MacZ tiene exactamente uno).
/// </remarks>
public sealed class GuardiaDePermisos : IGuardiaDePermisos
{
    private readonly IPermissionService _permisos;
    private readonly ICurrentUserAccessor _quienEs;
    private readonly ILogger<GuardiaDePermisos> _log;

    public GuardiaDePermisos(
        IPermissionService permisos,
        ICurrentUserAccessor quienEs,
        ILogger<GuardiaDePermisos> log)
    {
        _permisos = permisos;
        _quienEs = quienEs;
        _log = log;
    }

    public async Task ExigirPuedeEditarRolAsync(
        ClaimsPrincipal llamante,
        int empresaId,
        string roleCode,
        IReadOnlyCollection<string> clavesPedidas,
        CancellationToken ct)
    {
        var codigo = (roleCode ?? string.Empty).Trim();
        var pedidas = Normalizar(clavesPedidas);
        var actuales = new HashSet<string>(
            await _permisos.GetRolePermissionKeysAsync(empresaId, codigo, ct),
            StringComparer.OrdinalIgnoreCase);

        var techo = await TechoDelLlamanteAsync(llamante, empresaId, ct);

        if (techo is not null)
        {
            // Regla 1. Lo que cambia de verdad: lo que entra y lo que sale.
            var concedidas = pedidas.Except(actuales, StringComparer.OrdinalIgnoreCase).ToList();
            var retiradas = actuales.Except(pedidas, StringComparer.OrdinalIgnoreCase).ToList();

            var faltanParaConceder = concedidas.Where(c => !techo.Contains(c)).OrderBy(x => x).ToList();
            if (faltanParaConceder.Count > 0)
            {
                throw new EscaladaDePermisosException(
                    $"No puedes conceder permisos que tu no tienes. Te faltan: {Lista(faltanParaConceder)}. " +
                    "Pide a quien si los tenga que haga el cambio, o que te los conceda a ti primero.");
            }

            var faltanParaRetirar = retiradas.Where(c => !techo.Contains(c)).OrderBy(x => x).ToList();
            if (faltanParaRetirar.Count > 0)
            {
                throw new EscaladaDePermisosException(
                    $"No puedes retirar permisos que tu no tienes, porque no podrias devolverlos. " +
                    $"Esas claves son: {Lista(faltanParaRetirar)}. " +
                    "Si el cambio es correcto, lo tiene que hacer quien administre esas claves.");
            }

            // Regla 2. Los roles que vienen con la instalacion.
            if (actuales.Overlaps(LlavesDeGobierno.Todas) && await _permisos.EsRolDeSistemaAsync(empresaId, codigo, ct))
            {
                var faltanDeGobierno = LlavesDeGobierno.Todas
                    .Where(c => !techo.Contains(c)).OrderBy(x => x).ToList();
                if (faltanDeGobierno.Count > 0)
                {
                    throw new EscaladaDePermisosException(
                        $"'{codigo}' es un rol administrativo de la instalacion. Para editarlo hacen falta TODAS " +
                        $"las claves de administracion de permisos, y te faltan: {Lista(faltanDeGobierno)}. " +
                        "Crea un rol propio para lo que necesites en vez de recortar este.");
                }
            }
        }

        // Regla 3, para todos, SuperAdmin incluido: si existe un SuperAdmin vivo
        // la comprobacion pasa sola porque cuenta como administrador.
        await ExigirQueQuedeAdministracionAsync(
            empresaId,
            new SimulacionDePermisos(RolCodigoReemplazado: codigo, ClavesDelRol: pedidas),
            $"cambiar los permisos del rol '{codigo}'",
            ct);
    }

    public async Task ExigirPuedeEditarOverridesAsync(
        ClaimsPrincipal llamante,
        int empresaId,
        Guid userId,
        IReadOnlyCollection<string> allow,
        IReadOnlyCollection<string> deny,
        CancellationToken ct)
    {
        var allowPedido = Normalizar(allow);
        var denyPedido = Normalizar(deny);
        var (allowActual, denyActual) = await _permisos.GetUserOverrideKeysAsync(empresaId, userId, ct);

        var techo = await TechoDelLlamanteAsync(llamante, empresaId, ct);
        if (techo is not null)
        {
            /*
              AQUI CUENTA CUALQUIER MOVIMIENTO, EN LOS DOS SENTIDOS.

              Las cuatro operaciones posibles sobre un ajuste individual
              cambian lo que puede alguien, asi que las cuatro tienen el mismo
              techo:

                * poner un allow   -> le concedes la clave
                * quitar un allow  -> se la quitas
                * poner un deny    -> se la quitas (aunque su rol la tenga)
                * quitar un deny   -> se la devuelves

              Por eso se compara la diferencia simetrica de los dos conjuntos y
              no solo lo que se agrega: sin el deny en la cuenta, cualquiera con
              "permisosweb.overrides.editar" podria desarmar al administrador de
              verdad poniendole un deny sobre una llave que el no tiene.
            */
            var tocadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            tocadas.UnionWith(allowPedido.Except(allowActual, StringComparer.OrdinalIgnoreCase));
            tocadas.UnionWith(allowActual.Except(allowPedido, StringComparer.OrdinalIgnoreCase));
            tocadas.UnionWith(denyPedido.Except(denyActual, StringComparer.OrdinalIgnoreCase));
            tocadas.UnionWith(denyActual.Except(denyPedido, StringComparer.OrdinalIgnoreCase));

            var faltan = tocadas.Where(c => !techo.Contains(c)).OrderBy(x => x).ToList();
            if (faltan.Count > 0)
            {
                throw new EscaladaDePermisosException(
                    $"No puedes conceder ni quitar permisos que tu no tienes. Te faltan: {Lista(faltan)}. " +
                    "Pide a quien si los tenga que haga el cambio, o que te los conceda a ti primero.");
            }
        }

        await ExigirQueQuedeAdministracionAsync(
            empresaId,
            new SimulacionDePermisos(
                UsuarioReemplazado: userId,
                AllowDelUsuario: allowPedido,
                DenyDelUsuario: denyPedido),
            "cambiar los permisos individuales de ese usuario",
            ct);
    }

    public async Task ExigirPuedeAsignarRolAsync(
        ClaimsPrincipal llamante,
        int empresaId,
        string? rolActual,
        string rolNuevo,
        CancellationToken ct)
    {
        var nuevo = (rolNuevo ?? string.Empty).Trim();
        var actual = (rolActual ?? string.Empty).Trim();
        if (nuevo.Length == 0) return;

        /*
          SI EL ROL NO CAMBIA, NO HAY NADA QUE AUTORIZAR.

          Y no es un atajo cosmetico: los PUT de usuarios mandan el renglon
          completo, asi que editar el NOMBRE de alguien vuelve a mandar su rol.
          Sin esta salida, un administrador que no tenga todas las claves de
          ADMIN no podria corregirle una letra al nombre de un usuario ADMIN.
        */
        if (MismoRol(actual, nuevo)) return;

        var techo = await TechoDelLlamanteAsync(llamante, empresaId, ct);
        if (techo is null) return;

        var clavesNuevo = new HashSet<string>(
            await _permisos.GetRolePermissionKeysAsync(empresaId, nuevo, ct),
            StringComparer.OrdinalIgnoreCase);
        var clavesActual = actual.Length == 0
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(
                await _permisos.GetRolePermissionKeysAsync(empresaId, actual, ct),
                StringComparer.OrdinalIgnoreCase);

        var concedidas = clavesNuevo.Except(clavesActual, StringComparer.OrdinalIgnoreCase)
            .Where(c => !techo.Contains(c)).OrderBy(x => x).ToList();
        if (concedidas.Count > 0)
        {
            throw new EscaladaDePermisosException(
                $"No puedes asignar el rol '{nuevo}' porque trae permisos que tu no tienes. " +
                $"Te faltan: {Lista(concedidas)}. " +
                "Pide a quien si los tenga que haga el cambio, o que te los conceda a ti primero.");
        }

        var retiradas = clavesActual.Except(clavesNuevo, StringComparer.OrdinalIgnoreCase)
            .Where(c => !techo.Contains(c)).OrderBy(x => x).ToList();
        if (retiradas.Count > 0)
        {
            throw new EscaladaDePermisosException(
                $"No puedes quitarle el rol '{actual}' porque trae permisos que tu no tienes y no " +
                $"podrias devolverlos. Esas claves son: {Lista(retiradas)}. " +
                "Si el cambio es correcto, lo tiene que hacer quien administre esas claves.");
        }
    }

    public async Task ExigirPuedeCambiarEstatusDeModuloAsync(
        ClaimsPrincipal llamante,
        int empresaId,
        string moduleKey,
        int idStatus,
        CancellationToken ct)
    {
        // Prender un modulo solo puede DEVOLVER permisos. Nunca se bloquea:
        // bloquearlo seria quitar justo el camino de vuelta de la regla 3.
        if (idStatus != 2) return;

        var clave = (moduleKey ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(clave)) return;

        /*
          REGLA 3b: NADIE APAGA LA PUERTA POR LA QUE TIENE QUE VOLVER A ENTRAR.

          POR QUE HACE FALTA, ADEMAS DE LA REGLA 3
          ----------------------------------------
          La regla 3 pregunta "¿queda ALGUIEN?" y en MacZ la respuesta es
          siempre si, porque hay una cuenta con rol legacy SuperAdmin y esa
          salta toda la comprobacion. Se midio: con solo la regla 3 puesta, el
          usuario de prueba QAESCALADA seguia consiguiendo 200 en los tres
          apagados —permisos_roles, permisosweb y permisos_modulos— y despues
          su propio /api/me no traia NINGUNA clave "permisos*". O sea: la
          regla 3 era correcta y aun asi el destrozo pasaba, porque "alguien"
          era una cuenta ajena cuya contrasena quien apago no tiene.

          LA REGLA, DICHA EN UNA LINEA: no se puede hacer una operacion que te
          quite a ti mismo la capacidad de deshacerla. Quien apaga
          "permisos_modulos" se queda sin "permisos_modulos.activar" —las
          claves de un modulo apagado no cuentan, ver
          LoadEffectivePermissionsAsync— y "activar" es la unica puerta que
          vuelve a prender un modulo: el apagado seria definitivo para el.

          NO ES LA REGLA DEL FRONT DISFRAZADA. nucleo/permisosWeb.ts tiene una
          lista fija de "modulos del sistema" que no se pueden apagar; esto no
          mira la lista de nadie, mira si el llamante conserva la capacidad.
          Un modulo nuevo que manana sirva la pantalla de permisos queda
          protegido sin que haya que acordarse de anotarlo, y un modulo de la
          lista que no afecte a la administracion se puede apagar sin pelear.

          AL SuperAdmin NO LE APLICA, y no es un favor: su capacidad no sale
          del modelo de permisos (PermissionAuthorizationHandler lo deja pasar
          antes de mirar el snapshot), asi que apagar el modulo no se la quita
          y la premisa de la regla —"te quedarias sin poder deshacerlo"— es
          falsa para el. Por eso TechoDelLlamanteAsync devuelve null y aqui se
          sale: no hay nada que comprobar, no hay nada que permitir de mas.
        */
        var techo = await TechoDelLlamanteAsync(llamante, empresaId, ct);
        if (techo is not null)
        {
            var despues = techo
                .Where(c => !string.Equals(LlavesDeGobierno.ModuloDe(c), clave, StringComparison.OrdinalIgnoreCase))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (techo.Overlaps(LlavesDeGobierno.Reactivar) && !despues.Overlaps(LlavesDeGobierno.Reactivar))
            {
                throw new EscaladaDePermisosException(
                    $"No puedes apagar el modulo '{clave}': con el se apaga tu propia clave " +
                    $"'{string.Join("', '", LlavesDeGobierno.Reactivar)}', que es la UNICA con la que se vuelve a prender un modulo. " +
                    "Quedaria apagado para siempre. Si de verdad hay que apagarlo, que lo haga una cuenta con rol SuperAdmin, " +
                    "o concede antes esa clave a un rol de un modulo que no vayas a apagar.");
            }

            if (techo.Overlaps(LlavesDeGobierno.Conceder) && !despues.Overlaps(LlavesDeGobierno.Conceder))
            {
                throw new EscaladaDePermisosException(
                    $"No puedes apagar el modulo '{clave}': con el se apaga tu propia clave para conceder permisos " +
                    $"('{string.Join("' o '", LlavesDeGobierno.Conceder)}'), asi que no podrias devolver lo que este apagado se lleva. " +
                    "Si de verdad hay que apagarlo, que lo haga una cuenta con rol SuperAdmin, o concede antes esa clave a un rol " +
                    "de un modulo que no vayas a apagar.");
            }
        }

        await ExigirQueQuedeAdministracionAsync(
            empresaId,
            new SimulacionDePermisos(ModuloApagado: clave),
            $"apagar el modulo '{clave}'",
            ct);
    }

    /// <summary>
    /// Lo que el llamante puede de verdad: su rol MAS sus allow, MENOS sus
    /// deny, MENOS los modulos apagados — o sea el snapshot, el mismo que usa
    /// la autorizacion. Null significa "no se aplica el techo".
    /// </summary>
    /// <remarks>
    /// SE DECIDE CONTRA EL SNAPSHOT, NO CONTRA EL TOKEN NI CONTRA EL CUERPO.
    ///
    /// El token solo trae el rol legacy; el cuerpo de la peticion no trae nada
    /// de esto y si lo trajera no se mirarian. Se pregunta a
    /// GetPermissionsAsync, que es exactamente lo que contesta /api/me y lo que
    /// usa PermissionAuthorizationHandler: si el guardia usara otra cuenta, un
    /// mismo usuario podria pasar el candado del endpoint y chocar con el
    /// guardia, o al contrario.
    ///
    /// Hay DOS casos en los que se devuelve null —no hay techo— y los dos son
    /// deliberados:
    ///
    ///   * EL LLAMANTE ES SuperAdmin. Ya pasa toda la autorizacion sin mirar el
    ///     snapshot, asi que su snapshot puede venir vacio y aplicarle un techo
    ///     con el le impediria TODO. La regla 3 le sigue aplicando.
    ///   * LA INSTALACION NO TIENE EL MODELO DE CAPACIDADES
    ///     (PermissionsEnabled = false: faltan las tablas, la bandera de la
    ///     empresa esta en 0, o revento el calculo del snapshot). Ahi no hay
    ///     permisos efectivos contra los que medir y un techo sacado de un
    ///     snapshot vacio dejaria la pantalla de permisos inservible — que es
    ///     el peor momento, porque es cuando hay que arreglar la instalacion.
    ///     No abre nada: en ese modo manda LegacyPolicyMap, que ya reserva
    ///     permisosweb.roles.editar / overrides.editar / permisos_modulos.*
    ///     para SuperAdmin, asi que a estos endpoints no llega nadie mas.
    /// </remarks>
    private async Task<HashSet<string>?> TechoDelLlamanteAsync(
        ClaimsPrincipal llamante,
        int empresaId,
        CancellationToken ct)
    {
        if (CurrentUser.IsSuperAdmin(llamante))
            return null;

        var userId = _quienEs.GetUserId(llamante);
        if (userId is null)
        {
            // Sin sub no se sabe quien es. No se deja pasar a ciegas.
            throw new EscaladaDePermisosException(
                "El token no dice quien eres, asi que no se puede comprobar hasta donde puedes administrar. Vuelve a entrar.");
        }

        var rol = _quienEs.GetRole(llamante);
        var snapshot = await _permisos.GetPermissionsAsync(userId.Value, empresaId, rol, ct);
        if (!snapshot.PermissionsEnabled)
        {
            _log.LogWarning(
                "Guardia de permisos sin techo: la empresa {EmpresaId} contesta PermissionsEnabled=false para {UserId}. " +
                "Manda LegacyPolicyMap, que reserva estos endpoints a SuperAdmin.",
                empresaId, userId);
            return null;
        }

        return new HashSet<string>(snapshot.Permissions, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Regla 3: niega el cambio si despues no quedaria NADIE capaz de conceder
    /// permisos, o nadie capaz de volver a prender un modulo.
    /// </summary>
    private async Task ExigirQueQuedeAdministracionAsync(
        int empresaId,
        SimulacionDePermisos simulacion,
        string queSeIntentaba,
        CancellationToken ct)
    {
        var restante = await _permisos.GetAdministracionRestanteAsync(empresaId, simulacion, ct);

        // Lo que no se pudo comprobar no se cierra: una base sin el modelo de
        // capacidades no tiene nada que repartir y negar ahi la dejaria muerta.
        if (restante.NoSePudoComprobar) return;

        if (restante.PuedenConceder.Count == 0)
        {
            throw new EscaladaDePermisosException(
                $"No se puede {queSeIntentaba}: despues del cambio NINGUN usuario activo de esta empresa podria " +
                "volver a conceder permisos, y no hay forma de deshacerlo por la aplicacion. " +
                $"Antes de reintentarlo, concede '{string.Join("' o '", LlavesDeGobierno.Conceder)}' a otro usuario activo " +
                "(o deja activa una cuenta con rol SuperAdmin) y vuelve a intentarlo.");
        }

        if (restante.PuedenReactivarModulos.Count == 0)
        {
            throw new EscaladaDePermisosException(
                $"No se puede {queSeIntentaba}: despues del cambio NINGUN usuario activo de esta empresa podria " +
                "volver a prender un modulo apagado, asi que la pantalla de permisos quedaria cerrada para siempre. " +
                $"Antes de reintentarlo, concede '{string.Join("', '", LlavesDeGobierno.Reactivar)}' a otro usuario activo " +
                "(o deja activa una cuenta con rol SuperAdmin) y vuelve a intentarlo.");
        }
    }

    /// <summary>
    /// Si los dos textos nombran el mismo rol. "SUPER_ADMIN", "SuperAdmin" y
    /// "super admin" son el mismo, porque la columna UsuarioWeb.Rol guarda la
    /// forma bonita y el catalogo WRol.Codigo la forma con guion bajo.
    /// </summary>
    private static bool MismoRol(string a, string b)
    {
        static string Plano(string x) => new string(
            (x ?? string.Empty).Trim().ToUpperInvariant()
                .Where(char.IsLetterOrDigit).ToArray());
        return Plano(a).Length > 0 && Plano(a) == Plano(b);
    }

    /// <summary>
    /// Las claves para el mensaje: como mucho diez, y el resto contado.
    /// </summary>
    /// <remarks>
    /// NO ES COSMETICA. Se midio: el ataque de poner un "deny" en blanco sobre
    /// los ajustes individuales del administrador DEMO retira 486 claves de
    /// golpe —el PUT reemplaza la lista entera, asi que mandar allow:[] es
    /// retirarle todo— y el 403 salia con las 486 nombradas, 12 KB de texto en
    /// una sola linea. Un mensaje que no se puede leer no dice nada, y este
    /// tiene un trabajo: explicar que hacer. Diez claves alcanzan para
    /// reconocer el patron; el total dice el tamano del destrozo que se evito.
    /// </remarks>
    private static string Lista(IEnumerable<string> claves)
    {
        var todas = claves.ToList();
        if (todas.Count <= 10) return string.Join(", ", todas);
        return string.Join(", ", todas.Take(10)) + $" (y {todas.Count - 10} mas, {todas.Count} en total)";
    }

    private static HashSet<string> Normalizar(IReadOnlyCollection<string>? claves) =>
        (claves ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
