namespace ISL_Service.Application.Security;

/// <summary>
/// LA ENTRADA AL MODULO: la mitad del permiso que el backend no comprobaba.
/// </summary>
/// <remarks>
/// LA REGLA
/// --------
/// Acceso = entrada al modulo + permiso de la operacion. Tener
/// "compras.cancelar" sin "compras.ver_modulo" NO alcanza: la entrada es la
/// llave de la puerta y el permiso fino es la del cajon.
///
/// POR QUE HACE FALTA ESTE ARCHIVO
/// -------------------------------
/// Se midio: de los 93 controladores, NINGUNO comprobaba un ".ver_modulo".
/// La capa de entrada existia solo en el front —cada tarjeta del inicio se
/// enciende con su ".ver_modulo"— asi que quien conociera la URL entraba a un
/// modulo que no tenia concedido, siempre que tuviera suelto cualquier permiso
/// fino de adentro. El front escondia la puerta; no la cerraba.
///
/// POR QUE UNA TABLA A MANO Y NO "el primer segmento de la clave"
/// --------------------------------------------------------------
/// Porque NO coinciden, y eso fue la sorpresa de la auditoria. El ".ver_modulo"
/// del web es POR PANTALLA, no por modulo, y varias pantallas comparten el
/// prefijo de sus permisos finos mientras su entrada se llama de otra forma:
///
///   inventario.ajustes.*    -> inventario_ajustes.ver_modulo
///   inventario.transito.*   -> inventario_transito.ver_modulo
///   facturacion.*           -> facturacion.ver_modulo  (y existe tambien
///                              facturas.ver_modulo, duplicada, ver abajo)
///
/// Derivar la entrada del prefijo habria pedido "inventario.ver_modulo", que
/// no existe en el catalogo: todo el modulo de inventario habria contestado
/// 403 a todo el mundo. Una tabla explicita se puede auditar renglon por
/// renglon; una regla implicita no.
///
/// LO QUE NO SE PUERTEA, Y POR QUE
/// -------------------------------
/// * app_movil.*  — el canal movil no entra en esta entrega. Sus claves no
///   tienen ".ver_modulo" (el telefono no tiene tarjetas de inicio), asi que
///   quedan fuera por omision Y por decision.
/// * catalogos.* y constantes.* — NO son una pantalla, son el diccionario que
///   leen las demas. "catalogos.ver" lo exigen ~40 endpoints de lectura que
///   consumen Compras, Ventas, Garantias y Cobranza para llenar sus combos.
///   Exigir "catalogos.ver_modulo" para entrar a ellos dejaria sin combos a
///   quien no administra catalogos, que es casi todo el mundo.
/// * descuentos.* — tiene dos candidatas ("liquidaciones.ver_modulo", que esta
///   declarada dentro del modulo "descuentos", y ninguna llamada
///   "descuentos.ver_modulo") y tres pantallas distintas colgando del mismo
///   prefijo. Sin poder decir cual gobierna cual, se deja fuera: equivocarse
///   aqui apaga un modulo entero.
/// * cobranza.* — no tiene NINGUN ".ver_modulo" en el catalogo. No se inventa
///   uno: una clave que no esta sembrada no se puede conceder, y el modulo
///   quedaria muerto para todos menos el SuperAdmin.
/// * Las de administracion del propio modelo de permisos (permisosweb.*,
///   permisos_roles.*, permisos_usuarios.*, permisos_modulos.*) y las de
///   plomeria (seguridad.*, sincronizacion.*, huellas.*, empresas.*): no son
///   modulos de negocio y gatearlas puede dejar sin arreglo una instalacion
///   mal configurada, que es el peor momento para cerrar la puerta.
///
/// LAS DOS LLAVES DUPLICADAS QUE NO SE TOCAN
/// -----------------------------------------
/// El catalogo trae "facturas.ver_modulo" junto a "facturacion.ver_modulo", y
/// "cambios_vigencia.ver_modulo" junto a "cambiosvigencia.ver_modulo": la misma
/// pantalla con dos llaves de entrada, de dos siembras distintas. Aqui se
/// aceptan LAS DOS (basta cualquiera) en vez de elegir una: borrar la que
/// sobra es una migracion de datos, y mientras no se haga, quien tenga la
/// "mala" seguiria entrando por el front y se estrellaria aqui.
/// </remarks>
public static class EntradasDeModulo
{
    /// <summary>El sufijo que distingue una llave de entrada.</summary>
    public const string SufijoEntrada = ".ver_modulo";

    /// <summary>
    /// Prefijo de las claves finas -> entradas que lo abren. Se evalua del
    /// prefijo MAS LARGO al mas corto, para que "inventario.ajustes." gane
    /// sobre un hipotetico "inventario.".
    /// </summary>
    private static readonly (string Prefijo, string[] Entradas)[] Tabla =
    {
        // Las cuatro familias que ya lee Mac31.
        ("ventas.",             new[] { "ventas.ver_modulo" }),
        ("empleados.",          new[] { "empleados.ver_modulo" }),
        ("prestamos.",          new[] { "prestamos.ver_modulo" }),
        ("reportes.",           new[] { "reportes.ver_modulo" }),

        // Las que este cambio puentea desde Mac31.
        ("compras.",            new[] { "compras.ver_modulo" }),
        ("garantias.",          new[] { "garantias.ver_modulo" }),
        ("cortes.",             new[] { "cortes.ver_modulo" }),
        ("cambiosvigencia.",    new[] { "cambiosvigencia.ver_modulo", "cambios_vigencia.ver_modulo" }),
        ("inventario.ajustes.", new[] { "inventario_ajustes.ver_modulo" }),
        ("inventario.transito.",new[] { "inventario_transito.ver_modulo" }),

        // Las que ya tenian entrada en el web y nadie comprobaba.
        ("facturacion.",        new[] { "facturacion.ver_modulo", "facturas.ver_modulo" }),
        ("cobros.",             new[] { "cobros.ver_modulo", "dinero_usados.ver_modulo" }),
        ("cobranzas.",          new[] { "cobranzas.ver_modulo" }),
        ("almacen_cascos.",     new[] { "almacen_cascos.ver_modulo" }),
        ("cascos_cambio.",      new[] { "cascos_cambio.ver_modulo" }),
        ("gasolinas.",          new[] { "gasolinas.ver_modulo" }),
        ("transferencias.",     new[] { "transferencias.ver_modulo" }),
        ("recordatorios.",      new[] { "recordatorios.ver_modulo" }),
        ("productos.",          new[] { "productos.ver_modulo" }),
        ("clientes.",           new[] { "clientes.ver_modulo" }),
        ("usuarios.",           new[] { "usuarios.ver_modulo" }),
        ("categorias.",         new[] { "categorias.ver_modulo" }),
        ("cheques.",            new[] { "cheques.ver_modulo" }),
        ("tarimas.",            new[] { "tarimas.ver_modulo" }),
        ("nomina.",             new[] { "nomina.ver_modulo" }),
        ("proyectos.",          new[] { "proyectos.ver_modulo" }),
        ("servicios.",          new[] { "servicios.ver_modulo" }),
        ("obra.",               new[] { "obra.ver_modulo" }),
        ("aviacion.",           new[] { "aviacion.ver_modulo" }),
        ("dashboardventas.",    new[] { "dashboardventas.ver_modulo" }),
        ("profesores.",         new[] { "profesores.ver_modulo" }),
        ("equipos.",            new[] { "equipos.ver_modulo" }),
        ("jornadas.",           new[] { "jornadas.ver_modulo" }),
        ("inscripciones.",      new[] { "inscripciones.ver_modulo" }),
        ("torneos.",            new[] { "torneos.ver_modulo" }),
        ("temporadas.",         new[] { "temporadas.ver_modulo" }),
        ("configuracionroltorneo.", new[] { "configuracionroltorneo.ver_modulo" }),
        ("generacionroltorneo.",new[] { "generacionroltorneo.ver_modulo" })
    };

    private static readonly (string Prefijo, string[] Entradas)[] TablaOrdenada =
        Tabla.OrderByDescending(x => x.Prefijo.Length).ToArray();

    /// <summary>
    /// Las entradas que abren la puerta de <paramref name="clave"/>, o vacio si
    /// esa clave no vive detras de ninguna puerta.
    /// </summary>
    /// <remarks>
    /// Devuelve vacio —o sea, deja pasar— en tres casos a proposito:
    /// la clave ES una entrada (pedirle su propia entrada es un ciclo), la
    /// clave es del canal movil, o su familia no esta en la tabla de arriba.
    /// "No esta en la tabla" significa "no se pudo comprobar", y lo que no se
    /// comprobo no se cierra.
    /// </remarks>
    public static IReadOnlyList<string> Requeridas(string? clave)
    {
        if (string.IsNullOrWhiteSpace(clave))
            return Array.Empty<string>();

        var k = clave.Trim();

        // Una entrada no se pide a si misma.
        if (k.EndsWith(SufijoEntrada, StringComparison.OrdinalIgnoreCase))
            return Array.Empty<string>();

        // El canal movil queda fuera de esta entrega.
        if (k.StartsWith("app_movil.", StringComparison.OrdinalIgnoreCase))
            return Array.Empty<string>();

        foreach (var (prefijo, entradas) in TablaOrdenada)
        {
            if (k.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase))
                return entradas;
        }

        return Array.Empty<string>();
    }

    /// <summary>
    /// Si la puerta del modulo de <paramref name="clave"/> esta abierta para
    /// alguien que tiene los permisos que contesta <paramref name="tiene"/>.
    /// </summary>
    /// <remarks>
    /// LA EQUIVALENCIA ".ver" vale como ".ver_modulo" SE RESPETA.
    ///
    /// No se invento aqui: ya vivia en PermissionAuthorizationHandler
    /// (CanUseLegacyVerAsModuleView) porque en los modulos que gobierna Mac31
    /// el ".ver_modulo" no existe alla y mucha gente tiene el ".ver" y nada
    /// mas. Si esta comprobacion no la respetara, las dos mitades de la API
    /// opinarian distinto sobre la misma persona: el endpoint con
    /// [Authorize(Policy)] la dejaria pasar y el que usa PermisoControllerBase
    /// le diria que no. Eso es peor que cualquiera de las dos reglas.
    ///
    /// Lo que SI se cierra —y es todo el punto— es el caso de quien tiene un
    /// permiso fino suelto y NI la entrada NI el ".ver": antes entraba por
    /// URL a un modulo donde el front no le ensenaba ni la tarjeta.
    /// </remarks>
    public static bool PuedeEntrar(string clave, Func<string, bool> tiene)
    {
        ArgumentNullException.ThrowIfNull(tiene);

        var entradas = Requeridas(clave);
        if (entradas.Count == 0) return true;

        foreach (var entrada in entradas)
        {
            if (tiene(entrada)) return true;

            if (entrada.EndsWith(SufijoEntrada, StringComparison.OrdinalIgnoreCase))
            {
                // "compras.ver_modulo" -> "compras.ver"
                var equivalente = entrada[..^"_modulo".Length];
                if (tiene(equivalente)) return true;
            }
        }

        return false;
    }
}
