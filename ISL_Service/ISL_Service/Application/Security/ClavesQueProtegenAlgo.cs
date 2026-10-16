using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Microsoft.AspNetCore.Authorization;

namespace ISL_Service.Application.Security;

/// <summary>
/// QUE CLAVES DEL CATALOGO DECIDEN DE VERDAD ALGO EN ESTE SERVIDOR.
/// </summary>
/// <remarks>
/// EL PROBLEMA QUE RESUELVE
/// ------------------------
/// El catalogo de WPermiso tiene mas claves que comprobaciones. Medido el
/// 2026-10-12 en MacZ: 606 claves guardadas y 138 que no aparecian ni una vez
/// en el codigo del servidor. Una clave asi se puede marcar y desmarcar en la
/// pantalla de permisos y NO PASA NADA: el administrador cree que concedio o
/// retiro algo y no concedio ni retiro nada. Eso es peor que no tenerla, porque
/// miente.
///
/// POR QUE POR REFLEXION Y NO CON UNA LISTA ESCRITA A MANO
/// -------------------------------------------------------
/// Una lista escrita se desincroniza el primer dia que alguien agrega un
/// endpoint, y el error seria silencioso en la direccion mala: la clave nueva
/// se veria como "no protege nada" y la pantalla la esconderia, dejando sin
/// administrar un permiso que si decide. Aqui se lee el ensamblado, que es la
/// misma verdad que ejecuta la autorizacion.
///
/// LAS TRES FUENTES, Y POR QUE HACEN FALTA LAS TRES
/// ------------------------------------------------
///   1. [Authorize(Policy = "perm:clave")] en clases y metodos. Es el camino
///      corto y hay 91 claves asi.
///   2. Los arreglos de claves de los controladores (PermisosVer, Crear,
///      PermisoCobroEditar...). El camino largo —ExigirPermisoAsync— recibe el
///      arreglo, no un literal, asi que sin leer los campos se perderian 468
///      llamadas. Se toman solo los campos estaticos de tipo string[] cuyos
///      valores TIENEN FORMA DE CLAVE, para no arrastrar listas de columnas.
///   3. Las entradas de modulo de <see cref="EntradasDeModulo"/>. No estan en
///      ningun atributo porque las exige el propio manejador de autorizacion
///      para cualquier clave de su modulo, asi que decidian sin aparecer.
///
/// LO QUE NO ESTA AQUI, A PROPOSITO
/// --------------------------------
/// Las claves de reportes (reportes.*) NO se enumeran: nacen del arbol de
/// reportes de cada empresa en tiempo de ejecucion, asi que la lista correcta
/// depende de la base y no del ensamblado. Quien use esta clase tiene que
/// sumarle el catalogo de reportes de la empresa —por eso
/// <see cref="EsDeReportes"/> esta publico— o tratara 48 claves vivas de MacZ
/// como inertes.
/// </remarks>
public static class ClavesQueProtegenAlgo
{
    private const string Prefijo = "perm:";

    private static readonly Lazy<HashSet<string>> _claves = new(Calcular, isThreadSafe: true);

    /// <summary>
    /// Todas las claves que este ensamblado llega a comprobar, menos las de
    /// reportes (ver el remark de la clase).
    /// </summary>
    public static IReadOnlySet<string> Todas => _claves.Value;

    /// <summary>
    /// Si esa clave decide algo. Las de reportes contestan true sin mirar la
    /// lista: su comprobacion existe (ExigirPermisoReporteAsync) y cual aplica
    /// lo dice el arbol de la empresa, que aqui no se puede leer.
    /// </summary>
    public static bool Protege(string? clave)
    {
        var k = (clave ?? string.Empty).Trim();
        if (k.Length == 0) return false;
        return EsDeReportes(k) || EsDeCentros(k) || EsPuertaDeModulo(k) || _claves.Value.Contains(k);
    }

    /// <summary>
    /// Las claves "modulo.ver_modulo" deciden si el mosaico de ese modulo sale
    /// en el Inicio, y eso cuenta como decidir.
    /// </summary>
    /// <remarks>
    /// NO SE PODIAN ENUMERAR, Y POR ESO SE RECONOCEN POR LA FORMA.
    ///
    /// ModulosController no las nombra: compone la clave en tiempo de
    /// ejecucion —CapabilityVer = $"{moduleKey}.ver_modulo"— y filtra
    /// `modules.Where(m => permissionSet.Contains(m.CapabilityVer))`, asi que
    /// la lista correcta es la de WModulo de CADA empresa y no la del
    /// ensamblado. Enumerar solo las 40 y tantas de EntradasDeModulo dejaba
    /// fuera a todas las demas —inicio, checador, concentrados,
    /// liquidaciones, proveedores...— que si gobiernan su tarjeta.
    ///
    /// Que entre alguna de un modulo que ya no existe es el error barato: se
    /// ofreceria como administrable una casilla que no apaga nada. Al reves
    /// —esconder la puerta de un modulo vivo— deja el modulo inalcanzable y sin
    /// forma de arreglarlo desde la pantalla.
    /// </remarks>
    public static bool EsPuertaDeModulo(string? clave) =>
        (clave ?? string.Empty).TrimEnd().EndsWith(".ver_modulo", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Las claves de los centros de servicio, de las dos familias.
    /// </summary>
    /// <remarks>
    /// MISMO MOTIVO QUE REPORTES, Y HABIA QUE DECIRLO O LA PANTALLA LAS ESCONDE.
    ///
    /// `centros.2004.ver` no aparece como literal en ningun sitio del
    /// ensamblado: se COMPONE en tiempo de ejecucion con el Numero que esta en
    /// dbo.CentrosServicio de cada empresa (PermisosDeCentros.Ver). La reflexion
    /// no puede verla, asi que sin este renglon las 24 claves de los doce centros
    /// de Zaragoza se reportarian como "no protege nada" y el catalogo las
    /// esconderia — justo las casillas que hay que repartir, y encima el permiso
    /// de ESCRIBIR en un centro, que es el que mas importa revisar.
    ///
    /// Su comprobacion existe y es GuardiaDeCentros (PuedeVer / PuedeOperar /
    /// PuedeCambiar); cual clave aplica lo dice el catalogo de centros de la
    /// empresa, que aqui no se puede leer.
    /// </remarks>
    public static bool EsDeCentros(string? clave) => PermisosDeCentros.EsDeCentros(clave);

    /// <summary>
    /// Las claves del arbol de reportes, que se autorizan por otra via.
    /// </summary>
    public static bool EsDeReportes(string? clave) =>
        (clave ?? string.Empty).TrimStart().StartsWith("reportes.", StringComparison.OrdinalIgnoreCase);

    private static HashSet<string> Calcular()
    {
        var claves = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ensamblado = typeof(ClavesQueProtegenAlgo).Assembly;

        Type[] tipos;
        try
        {
            tipos = ensamblado.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Si un tipo no carga se trabaja con los que si. Quedarse sin lista
            // seria lo peor: haria que la pantalla escondiera todo.
            tipos = ex.Types.Where(t => t is not null).Cast<Type>().ToArray();
        }

        foreach (var tipo in tipos)
        {
            // 1. Las politicas de los atributos, de la clase y de cada metodo.
            foreach (var attr in tipo.GetCustomAttributes<AuthorizeAttribute>(inherit: true))
                foreach (var clave in DeLaPolitica(attr.Policy)) Agregar(claves, clave);

            foreach (var metodo in tipo.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                foreach (var attr in metodo.GetCustomAttributes<AuthorizeAttribute>(inherit: true))
                    foreach (var clave in DeLaPolitica(attr.Policy)) Agregar(claves, clave);
            }

            // 2. Los arreglos de claves que recibe ExigirPermisoAsync.
            foreach (var campo in tipo.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                var esTexto = campo.FieldType == typeof(string);
                if (!esTexto && campo.FieldType != typeof(string[])) continue;

                object? valor;
                try
                {
                    valor = campo.GetValue(null);
                }
                catch
                {
                    // Un inicializador estatico que revienta no debe tumbar el
                    // calculo: se salta ese campo y se sigue.
                    continue;
                }

                // Los dos sabores conviven en los controladores: arreglo cuando
                // el endpoint acepta varias claves (PermisosVer) y const string
                // cuando es una sola (PermisoVerPassword). Dejar fuera el
                // segundo hacia que claves tan vivas como
                // "usuarios.password.ver" se vieran como inertes.
                if (valor is string uno) Agregar(claves, TieneFormaDeClave(uno) ? uno : null);
                else if (valor is string[] varias)
                    foreach (var v in varias) Agregar(claves, TieneFormaDeClave(v) ? v : null);
            }
        }

        // 3. Las entradas de modulo.
        foreach (var entrada in EntradasDeModulo.TodasLasEntradas) Agregar(claves, entrada);

        // 4. Los literales que los controladores usan DENTRO de sus metodos.
        foreach (var literal in LiteralesDeLosControladores(ensamblado)) Agregar(claves, literal);

        return claves;
    }

    /// <summary>
    /// Las cadenas con forma de clave que aparecen en el cuerpo de los metodos
    /// de los controladores, leidas del IL.
    /// </summary>
    /// <remarks>
    /// POR QUE HAY QUE BAJAR AL IL, Y POR QUE SOLO EN LOS CONTROLADORES
    /// ----------------------------------------------------------------
    /// Muchas comprobaciones no pasan por un campo ni por un atributo:
    /// `ExigirPermisoAsync(new[] { "constantes.ver" }, ct)` escribe el arreglo
    /// ahi mismo. La reflexion no ve dentro de un metodo, asi que
    /// "constantes.ver" se reportaba como clave que no protege nada aunque
    /// `GET /api/constantes` la exija. Los literales de un metodo si estan en
    /// el ensamblado: cada uno es un `ldstr` con un token al monton de cadenas.
    ///
    /// Se limita a los controladores a proposito. Si se leyeran los literales
    /// de TODO el ensamblado entrarian las semillas del catalogo —que viven
    /// como literales en PermissionService.cs— y entonces cualquier clave
    /// sembrada se veria protegida por el hecho de existir, que es exactamente
    /// la pregunta que esta clase intenta contestar.
    ///
    /// El recorrido del IL es deliberadamente simple: busca el opcode de
    /// `ldstr` y resuelve su token. Puede colar algun byte que no era una
    /// instruccion; no importa, porque lo que sale se cruza despues contra el
    /// catalogo y un texto que no sea una clave de WPermiso no cambia nada. El
    /// error caro seria el contrario: perder una clave que si se exige.
    /// </remarks>
    private static IEnumerable<string> LiteralesDeLosControladores(Assembly ensamblado)
    {
        var resultado = new List<string>();
        var ruta = ensamblado.Location;
        if (string.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta)) return resultado;

        try
        {
            using var stream = File.OpenRead(ruta);
            using var pe = new PEReader(stream);
            if (!pe.HasMetadata) return resultado;
            var md = pe.GetMetadataReader();

            foreach (var th in md.TypeDefinitions)
            {
                var tipo = md.GetTypeDefinition(th);
                if (!EsDeLosControladores(md, tipo)) continue;

                foreach (var mh in tipo.GetMethods())
                {
                    var metodo = md.GetMethodDefinition(mh);
                    if (metodo.RelativeVirtualAddress == 0) continue;

                    byte[] il;
                    try { il = pe.GetMethodBody(metodo.RelativeVirtualAddress).GetILBytes() ?? Array.Empty<byte>(); }
                    catch { continue; }

                    for (var i = 0; i + 4 < il.Length; i++)
                    {
                        if (il[i] != 0x72) continue; // ldstr
                        var token = il[i + 1] | (il[i + 2] << 8) | (il[i + 3] << 16) | (il[i + 4] << 24);
                        if ((token & 0xFF000000) != 0x70000000) continue;

                        try
                        {
                            var texto = md.GetUserString(MetadataTokens.UserStringHandle(token));
                            if (TieneFormaDeClave(texto)) resultado.Add(texto.Trim());
                        }
                        catch
                        {
                            // Token que no era un ldstr de verdad: se ignora.
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
            // Si el ensamblado no se puede leer se trabaja con las otras tres
            // fuentes. Quedarse sin lista haria que la pantalla escondiera
            // permisos vivos, que es el peor resultado posible.
        }

        return resultado;
    }

    /// <summary>
    /// Si ese tipo pertenece a los controladores, subiendo por quien lo
    /// contiene.
    /// </summary>
    /// <remarks>
    /// HACE FALTA SUBIR PORQUE LOS METODOS ASYNC NO GUARDAN SU CUERPO EN LA
    /// CLASE QUE LOS DECLARA.
    ///
    /// El compilador lo mueve a un tipo ANIDADO —la maquina de estados— y un
    /// tipo anidado NO tiene espacio de nombres propio en los metadatos: el
    /// suyo viene de quien lo contiene. Mirando solo el espacio del tipo se
    /// saltaban todos los endpoints async, que son casi todos, y por eso
    /// "constantes.ver" seguia reportandose como clave que no protege nada
    /// aunque GET /api/constantes la exija con un arreglo escrito en linea.
    /// </remarks>
    private static bool EsDeLosControladores(MetadataReader md, TypeDefinition tipo)
    {
        for (var i = 0; i < 8; i++)
        {
            var espacio = md.GetString(tipo.Namespace);
            if (espacio.StartsWith("ISL_Service.Controllers", StringComparison.Ordinal)) return true;

            var padre = tipo.GetDeclaringType();
            if (padre.IsNil) return false;
            tipo = md.GetTypeDefinition(padre);
        }
        return false;
    }

    private static void Agregar(HashSet<string> destino, string? clave)
    {
        if (!string.IsNullOrWhiteSpace(clave)) destino.Add(clave.Trim());
    }

    /// <summary>
    /// Las claves de una politica. "perm:a" da una; "perm:a|b" da las dos.
    /// </summary>
    /// <remarks>
    /// EL PIPE HAY QUE PARTIRLO IGUAL QUE LO PARTE QUIEN AUTORIZA.
    ///
    /// PermissionPolicyProvider hace Split('|') y exige "a O b" (un mismo
    /// endpoint sirve a la oficina y al repartidor, con catalogos distintos).
    /// Sin partirlo aqui, lo que entraba al conjunto era la cadena completa
    /// —"ventas.pedidos.crear|app_movil.pedidos"— y las dos claves de verdad
    /// quedaban fuera. Eso hacia que "app_movil.pedidos", que es la llave del
    /// repartidor y la exigen once endpoints, se reportara como clave que no
    /// protege nada. Una pantalla que esconde lo que no protege nada habria
    /// escondido justo esa.
    /// </remarks>
    private static IEnumerable<string> DeLaPolitica(string? politica)
    {
        var p = (politica ?? string.Empty).Trim();
        if (!p.StartsWith(Prefijo, StringComparison.OrdinalIgnoreCase))
            return Array.Empty<string>();

        // "modulo:<clave>" NO ES UNA CLAVE DE PERMISO. Es la pregunta "puede
        // abrir la pantalla de ese modulo", que PermissionAuthorizationHandler
        // contesta con la entrada del modulo mas algun permiso de trabajo suyo.
        // No existe en WPermiso y meterla aqui la reportaria como una clave que
        // nadie tiene.
        return p[Prefijo.Length..]
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !x.StartsWith("modulo:", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Forma de clave: minusculas, al menos un punto, y nada de rutas ni SQL.
    /// </summary>
    /// <remarks>
    /// Es el filtro que impide que un campo string[] de nombres de columna o de
    /// extensiones de archivo entre como si fuera un permiso. No pretende ser
    /// exacto: lo que entra de mas solo se compara contra el catalogo, asi que
    /// un texto que no sea una clave de WPermiso no cambia nada.
    /// </remarks>
    private static bool TieneFormaDeClave(string? v)
    {
        var s = (v ?? string.Empty).Trim();
        if (s.Length < 3 || !s.Contains('.')) return false;
        if (s.Contains(' ') || s.Contains('/') || s.Contains('\\')) return false;
        return s.All(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-');
    }
}
