namespace ISL_Service.Application.Security;

/// <summary>
/// LAS LLAVES QUE REPARTEN LAS DEMAS LLAVES.
/// </summary>
/// <remarks>
/// QUE ES ESTO
/// -----------
/// El catalogo de permisos tiene 491 claves en MacZ y todas valen lo mismo
/// para el modelo: una fila en WPermiso. Pero nueve de ellas no son un permiso
/// de negocio, son el PODER DE REPARTIR permisos: con
/// "permisosweb.roles.editar" se le puede dar cualquier clave del catalogo a
/// cualquier rol, y con "permisos_modulos.activar" se puede apagar el modulo
/// que sirve la pantalla desde la que se reparten.
///
/// Mientras esa diferencia no estuviera escrita en ningun sitio, el backend no
/// podia distinguir "editar el rol de los repartidores" de "editar el rol que
/// administra los permisos", y por eso las dos cosas costaban lo mismo.
///
/// LO QUE SE MIDIO ANTES DE ESCRIBIR ESTE ARCHIVO
/// ----------------------------------------------
/// Con un usuario de prueba (QAESCALADA, rol QA_ESCALA, 11 claves, todas de
/// administracion de permisos y NINGUNA de negocio) contra el backend local
/// (127.0.0.1:5041, base MacZ):
///
///   1. PUT /api/permisos-web/roles/QA_ESCALA/permissions con su propia lista
///      mas "empleados.sueldo.ver", "usuarios.crear", "usuarios.password.ver"
///      y "permisosweb.catalogo.editar" -> 200 {"ok":true}. El siguiente
///      /api/me del MISMO usuario devolvia 17 claves en vez de 11. O sea: se
///      concedio a si mismo cuatro llaves que no tenia, una de ellas la de los
///      sueldos.
///   2. PUT /api/permisos-web/roles/ADMIN/permissions con ["permisos_modulos.ver"]
///      -> 200. El rol de sistema ADMIN paso de 17 claves a 1.
///   3. PUT /api/permisos-web/usuarios/{suGuid}/overrides con
///      allow:["usuarios.editar","usuarios.password.reset"] -> 200. Se firmo a
///      si mismo dos permisos que su rol no tiene.
///   4. PUT /api/permisos-web/modulos/permisos_roles/status {idStatus:2} -> 200,
///      y despues el mismo PUT sobre "permisos_modulos" -> 200. A partir de ahi
///      su propio /api/me ya no traia NINGUNA clave "permisos*" (las claves de
///      un modulo apagado no cuentan, ver LoadEffectivePermissionsAsync) y el
///      intento de volver a prenderlo contestaba 403. Sin vuelta por API.
///
/// Los cuatro frenos que faltaban vivian en el front
/// (ModalPermisosDeRol.tsx y nucleo/permisosWeb.ts), o sea que con curl no
/// existian.
///
/// POR QUE UNA LISTA A MANO Y NO "lo que empiece por permisos"
/// -----------------------------------------------------------
/// Porque el prefijo no separa leer de escribir. "permisos_modulos.ver" y
/// "permisos_roles.ver_modulo" son de solo mirar: quien solo las tiene no
/// puede hacer dano y no tiene por que quedar atrapado en las reglas de abajo.
/// Las que importan son las de ESCRIBIR, y hay que nombrarlas una por una.
/// Una lista explicita tambien se puede auditar renglon por renglon; una regla
/// implicita no.
/// </remarks>
public static class LlavesDeGobierno
{
    /// <summary>
    /// Las claves de ESCRITURA del propio modelo de permisos. Tener cualquiera
    /// de estas significa poder cambiar lo que puede alguien mas.
    /// </summary>
    public static readonly IReadOnlySet<string> Todas =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "permisosweb.roles.editar",
            "permisosweb.overrides.editar",
            "permisosweb.catalogo.editar",
            "permisos_roles.crear",
            "permisos_roles.editar",
            "permisos_modulos.crear",
            "permisos_modulos.editar",
            "permisos_modulos.activar",
            "permisos_usuarios.editar"
        };

    /// <summary>
    /// Las dos claves con las que se DESHACE cualquier desastre de permisos:
    /// una concede (volver a dar la clave perdida a un rol o a una persona) y
    /// la otra reactiva (volver a prender el modulo apagado).
    /// </summary>
    /// <remarks>
    /// POR QUE ESTAS DOS Y NO LAS NUEVE
    /// --------------------------------
    /// La pregunta que hay que poder contestar antes de cada cambio es "¿queda
    /// alguien que pueda deshacer esto?", y deshacer solo necesita dos cosas:
    ///
    ///   * CONCEDER — "permisosweb.roles.editar" (PUT roles/{code}/permissions)
    ///     o "permisosweb.overrides.editar" (PUT usuarios/{id}/overrides).
    ///     Cualquiera de las dos sirve: con la segunda se le devuelve a una
    ///     persona la clave que perdio sin tocar su rol.
    ///   * REACTIVAR — "permisos_modulos.activar" (PUT modulos/{clave}/status).
    ///     Esta no tiene sustituto: es la unica puerta que vuelve a prender un
    ///     modulo, y es justo la que el ataque 4 de arriba cerraba.
    ///
    /// Exigir las nueve seria mas estricto y ademas FALSO: una instalacion que
    /// reparte el trabajo entre dos personas (una toca roles, otra modulos)
    /// quedaria bloqueada para siempre aunque entre las dos pudieran deshacer
    /// cualquier cosa. Por eso se comprueban por SEPARADO, no juntas: basta
    /// que de cada capacidad quede alguien, no que quede alguien con las dos.
    /// </remarks>
    public static readonly IReadOnlySet<string> Conceder =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "permisosweb.roles.editar",
            "permisosweb.overrides.editar"
        };

    /// <summary>La unica clave que vuelve a prender un modulo apagado.</summary>
    public static readonly IReadOnlySet<string> Reactivar =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "permisos_modulos.activar"
        };

    /// <summary>
    /// El rol legacy que salta TODA la comprobacion de permisos
    /// (PermissionAuthorizationHandler y PermisoControllerBase lo dejan pasar
    /// antes de mirar el snapshot).
    ///
    /// No es una puerta que se abra aqui: ya existia y es la que de verdad
    /// garantiza que una instalacion se puede rescatar. Se nombra para poder
    /// CONTARLO como administrador en las reglas de abajo, porque si no las
    /// reglas negarian cambios perfectamente seguros en una base que si tiene
    /// un SuperAdmin vivo.
    /// </summary>
    public const string RolQueSaltaTodo = "SuperAdmin";

    /// <summary>Si la clave es una de las nueve de escritura del modelo.</summary>
    public static bool EsDeGobierno(string? clave) =>
        !string.IsNullOrWhiteSpace(clave) && Todas.Contains(clave.Trim());

    /// <summary>
    /// El modulo al que pertenece una clave: lo que va ANTES DEL PRIMER PUNTO.
    ///
    /// Es la MISMA regla que usa PermissionService.EsDeModuloApagado, y tiene
    /// que seguir siendo la misma: si aqui se partiera distinto, la simulacion
    /// de "que pasaria si apago este modulo" diria una cosa y el calculo real
    /// de permisos otra, y el guardia dejaria pasar justo el cambio que
    /// pretende impedir.
    /// </summary>
    public static string ModuloDe(string clave)
    {
        if (string.IsNullOrWhiteSpace(clave)) return string.Empty;
        var punto = clave.IndexOf('.');
        return punto <= 0 ? string.Empty : clave[..punto];
    }
}

/// <summary>
/// El cambio de permisos se nego porque dejaria a quien lo pide con mas poder
/// del que administra, o a la instalacion sin nadie capaz de administrar.
/// Siempre sale como 403 con el mensaje tal cual: el mensaje dice QUE falta y
/// QUE hacer, y es lo unico que va a leer quien se tope con el.
/// </summary>
public sealed class EscaladaDePermisosException : Exception
{
    public EscaladaDePermisosException(string message) : base(message) { }
}
