using System.Reflection;
using ISL_Service.Application.Centros;
using ISL_Service.Application.Security;
using ISL_Service.Infrastructure.Security;

namespace ISL_Service.Tests;

/// <summary>
/// QUE NADIE PIERDA EL ACCESO QUE YA TENIA AL SUBIR LA FAMILIA NUEVA DE CENTROS.
/// </summary>
/// <remarks>
/// LO QUE SE PIDIO, TEXTUAL: "ningun empleado que actualmente tenga acceso
/// autorizado debe perderlo por la actualizacion" y "no elimines los permisos
/// actuales ni obligues a configurarlos nuevamente".
///
/// LA COMPATIBILIDAD SE SOSTIENE EN TRES COSAS, Y LAS TRES SON DE DISTINTO DUENO
/// -----------------------------------------------------------------------------
///   1. Que la clave VIEJA valga al preguntar. Lo hace
///      <see cref="CapacidadesDeCentros"/> (capa de centros).
///   2. Que la clave vieja NO SE BORRE del juego de permisos. Lo hace
///      PermissionService con CentrosPermissionPrefix (permisos).
///   3. Que las dos familias ESTEN en el catalogo, o no se pueden ni ver ni
///      repartir. Lo hace EnsureCentrosPermissionsSyncedAsync (permisos).
///
/// Las tres se prueban aqui juntas a proposito: cada una por separado se ve
/// bien, y basta que falle UNA para que a alguien se le apague el selector sin
/// un error en el log. Dos de las tres viven en archivos de dueños distintos, y
/// esta clase es el unico sitio donde se comprueba que casan.
///
/// CADA PRUEBA SE ROMPIO A PROPOSITO antes de darla por buena: se quita o se
/// invierte la linea que vigila y se comprueba que FALLA. En el comentario de
/// cada una esta que se rompio y como fallo. Una prueba que pasa con el codigo
/// roto no vigila nada.
/// </remarks>
public class CompatibilidadDeCentrosTests
{
    private const int Centro = 2004;   // ORIENTE, en las dos empresas

    /// <summary>
    /// Lo que puede alguien con ese juego de permisos. Es la MISMA clase que usa
    /// el backend al contestar, no una copia de la regla: una copia acabaria
    /// opinando distinto y las dos se verian verdes.
    /// </summary>
    private static CapacidadesDeCentros Puede(params string[] permisos)
    {
        var juego = permisos.ToList();
        return new CapacidadesDeCentros(
            todoPermitido: false,
            permisos: juego,
            tiene: clave => juego.Contains(clave, StringComparer.OrdinalIgnoreCase));
    }

    /* ================================================================== */
    /* 1. LA EQUIVALENCIA: QUIEN HOY PUEDE, MAÑANA PUEDE                  */
    /* ================================================================== */

    /// <summary>
    /// EL FALLO QUE ESTA PRUEBA EXISTE PARA CAZAR, Y QUE YA PASO DE VERDAD.
    ///
    /// `ClavesDeCentros.CambiarCompatible` no es un literal: vale
    /// `PermisosDeCentros.Cambiar`. Al integrar las dos familias se intento
    /// meterlas en la MISMA clase, con `Cambiar` pasando a significar la nueva y
    /// `CambiarLegado` la vieja. Resultado: `CambiarCompatible` se quedo
    /// apuntando a `centros.cambiar` —la clave nueva, que nadie tiene— el codigo
    /// siguio compilando, las pruebas de la capa de centros siguieron verdes, y
    /// la compatibilidad dejo de existir en silencio.
    ///
    /// ROTA A PROPOSITO: con esa version, esta prueba falla diciendo
    /// "la clave compatible dejo de ser la vieja". Es la unica que lo caza, y por
    /// eso compara contra el TEXTO y no contra otra constante: comparar dos
    /// constantes que cambian juntas no prueba nada.
    /// </summary>
    [Fact]
    public void La_Clave_Compatible_Es_La_Vieja()
    {
        Assert.Equal("reportes.centros.cambiar", ClavesDeCentros.CambiarCompatible);
        Assert.Equal($"reportes.centros.{Centro}.ver", ClavesDeCentros.VerCompatible(Centro));

        Assert.Equal("centros.cambiar", ClavesDeCentros.Cambiar);
        Assert.Equal($"centros.{Centro}.ver", ClavesDeCentros.Ver(Centro));

        Assert.NotEqual(ClavesDeCentros.Cambiar, ClavesDeCentros.CambiarCompatible);
    }

    /// <summary>
    /// ROTA A PROPOSITO: quitando `|| _tiene(ClavesDeCentros.CambiarCompatible)`
    /// de CapacidadesDeCentros.PuedeCambiar, esta prueba falla.
    /// </summary>
    [Fact]
    public void El_Selector_Viejo_Sigue_Abriendo_El_Selector()
    {
        Assert.True(Puede(PermisosDeCentros.Cambiar).PuedeCambiar);
    }

    /// <summary>
    /// ROTA A PROPOSITO: quitando `|| _tiene(ClavesDeCentros.VerCompatible(numero))`
    /// de PuedeVer, esta prueba falla.
    /// </summary>
    [Fact]
    public void El_Permiso_Viejo_De_Un_Centro_Sigue_Abriendo_Ese_Centro()
    {
        Assert.True(Puede(PermisosDeCentros.Ver(Centro)).PuedeVer(Centro));
    }

    /// <summary>
    /// LOS DOCE DE ZARAGOZA, DE GOLPE, CON SOLO LAS CLAVES VIEJAS. Es el caso
    /// real: 2001..2012 repartidos a mano, y ninguno se puede quedar fuera.
    /// </summary>
    [Fact]
    public void Los_Doce_Centros_De_Zaragoza_Siguen_Abriendo()
    {
        var numeros = Enumerable.Range(2001, 12).ToArray();
        var viejas = new[] { PermisosDeCentros.Cambiar }
            .Concat(numeros.Select(PermisosDeCentros.Ver))
            .ToArray();

        var puede = Puede(viejas);

        Assert.True(puede.PuedeCambiar, "se queda sin selector");
        var fuera = numeros.Where(n => !puede.PuedeVer(n)).ToList();
        Assert.True(fuera.Count == 0,
            "estos centros los tiene hoy y se quedarian sin acceso: " + string.Join(", ", fuera));
    }

    /// <summary>
    /// LO QUE NO SE TENIA NO SE REGALA. La compatibilidad traduce, no reparte.
    /// </summary>
    [Fact]
    public void No_Se_Abre_Un_Centro_Que_Nadie_Le_Dio()
    {
        var puede = Puede(PermisosDeCentros.Cambiar, PermisosDeCentros.Ver(2004));

        Assert.False(puede.PuedeVer(2007));
    }

    /// <summary>
    /// SIN EL SELECTOR VIEJO NO HAY SELECTOR NUEVO: la compatibilidad no abre
    /// puertas que hoy esten cerradas.
    /// </summary>
    [Fact]
    public void Sin_Ninguna_Clave_De_Selector_No_Se_Puede_Cambiar()
    {
        Assert.False(Puede("reportes.ver_modulo").PuedeCambiar);
    }

    /* ================================================================== */
    /* 2. LA MATRIZ: NADIE SE QUEDA FUERA EL DIA DEL DESPLIEGUE           */
    /* ================================================================== */

    /// <summary>
    /// QUIEN HOY TRABAJA EN LA MATRIZ SIGUE TRABAJANDO, SIN QUE NADIE LE
    /// CONFIGURE NADA.
    ///
    /// Hoy la matriz no la gobierna ninguna clave de centros: nadie tiene
    /// `centros.matriz.*` porque no existian. Si la matriz empezara a exigirlas,
    /// el dia del despliegue la aplicacion se queda en blanco PARA TODO EL MUNDO
    /// —y no por un permiso que alguien decidiera, sino por una regla nueva—.
    ///
    /// ROTA A PROPOSITO: cambiando `return !TieneAlgunaClaveDeMatriz;` por
    /// `return false;` en CapacidadesDeCentros, las dos aserciones de aqui
    /// fallan. Es el apagon general, y lo caza esta prueba.
    ///
    /// Se prueba con la lista mas pobre que hay a proposito: si el acceso a la
    /// matriz dependiera de tener algo —cualquier cosa—, habria alguien en algun
    /// rol raro que no lo tendria, y a esa persona el sistema entero dejaria de
    /// contestarle.
    /// </summary>
    [Fact]
    public void Quien_Hoy_Usa_La_Matriz_Sigue_Usandola()
    {
        var puede = Puede("ventas.ver_modulo");

        Assert.True(puede.PuedeVerLaMatriz, "se queda sin poder consultar la matriz");
        Assert.True(puede.PuedeOperarEnLaMatriz, "se queda sin poder guardar en la matriz");
    }

    /// <summary>
    /// Y TAMBIEN QUIEN NO TIENE ABSOLUTAMENTE NADA. El juego de permisos vacio
    /// pasa de verdad: un usuario nuevo, o una lectura en la que todavia no
    /// entro ninguna familia. Lo que decide si puede hacer algo son sus permisos
    /// finos; la matriz no puede ser el candado que lo deje fuera antes.
    /// </summary>
    [Fact]
    public void Un_Juego_De_Permisos_Vacio_Sigue_Viendo_La_Matriz()
    {
        var puede = Puede();

        Assert.True(puede.PuedeVerLaMatriz);
        Assert.True(puede.PuedeOperarEnLaMatriz);
    }

    /// <summary>
    /// PERO EL INTERRUPTOR SE PUEDE ENCENDER. El dia que alguien le configura UNA
    /// clave de matriz, esa clave gobierna: con solo `matriz.ver`, consulta y no
    /// guarda.
    ///
    /// Es la otra mitad y hay que probarla, porque es lo que distingue "la matriz
    /// se rige por permiso" de "la matriz esta abierta para siempre". Tambien es
    /// la razon por la que la siembra NO concede estas dos claves a nadie: si se
    /// repartieran solas, este interruptor no se podria volver a usar.
    ///
    /// ROTA A PROPOSITO: cambiando `return !TieneAlgunaClaveDeMatriz;` por
    /// `return true;`, esta prueba falla —la matriz quedaria abierta aunque
    /// alguien la hubiera restringido a proposito— mientras las dos de arriba
    /// siguen pasando. Esa es la pareja: una caza el apagon, la otra caza que el
    /// candado no sirva.
    /// </summary>
    [Fact]
    public void Configurarle_Una_Clave_De_Matriz_La_Pone_A_Gobernar()
    {
        var soloConsulta = Puede(ClavesDeCentros.MatrizVer);

        Assert.True(soloConsulta.PuedeVerLaMatriz);
        Assert.False(soloConsulta.PuedeOperarEnLaMatriz,
            "le configuraron solo consulta en la matriz y aun asi puede guardar");
    }

    /* ================================================================== */
    /* 3. `operar` NO SE DEDUCE DE `ver`. NUNCA.                          */
    /* ================================================================== */

    /// <summary>
    /// LA PRUEBA QUE VIGILA QUE NADIE "AYUDE" AL USUARIO.
    ///
    /// ROTA A PROPOSITO AL REVES: se le agrego a
    /// CapacidadesDeCentros.PuedeOperar un `|| PuedeVer(numero)` —que es
    /// exactamente el atajo que alguien escribiria para que "funcione"— y esta
    /// prueba fallo con "se regalo el permiso de ESCRIBIR". Despues se quito.
    ///
    /// Si esta prueba pasa con ese atajo puesto, no sirve y hay que arreglarla,
    /// no borrarla: lo que estaria mal es el candado, no el aviso.
    /// </summary>
    [Fact]
    public void Ver_Un_Centro_No_Concede_Escribir_En_El()
    {
        var puede = Puede(
            PermisosDeCentros.Cambiar,
            PermisosDeCentros.Ver(Centro),
            ClavesDeCentros.Ver(Centro));

        Assert.True(puede.PuedeVer(Centro));
        Assert.False(puede.PuedeOperar(Centro),
            "se regalo el permiso de ESCRIBIR en el centro 2004 a quien solo puede consultarlo");
    }

    /// <summary>
    /// Y LA FAMILIA VIEJA TAMPOCO ABRE `operar`. No existe clave vieja de
    /// escribir -alla los centros solo servian para sacar reportes-, asi que no
    /// hay nada de donde heredar. Aceptar la vieja aqui seria deducir `operar` de
    /// `ver` por la puerta de atras.
    ///
    /// ROTA A PROPOSITO: agregando `|| _tiene(ClavesDeCentros.VerCompatible(numero))`
    /// a PuedeOperar, esta prueba falla.
    /// </summary>
    [Fact]
    public void El_Permiso_Viejo_No_Abre_Escribir_En_El_Centro()
    {
        var puede = Puede(PermisosDeCentros.Cambiar, PermisosDeCentros.Ver(Centro));

        Assert.False(puede.PuedeOperar(Centro));
    }

    /// <summary>
    /// NINGUNA COMBINACION DE CLAVES VIEJAS PRODUCE UN `operar`. Es la version
    /// general: las de arriba vigilan un centro, esta vigila la regla con los
    /// doce de Zaragoza puestos.
    /// </summary>
    [Fact]
    public void Con_Todo_Lo_Viejo_Puesto_No_Se_Puede_Escribir_En_Ningun_Centro()
    {
        var numeros = Enumerable.Range(2001, 12).ToArray();
        var viejas = new[] { PermisosDeCentros.Cambiar }
            .Concat(numeros.Select(PermisosDeCentros.Ver))
            .ToArray();

        var puede = Puede(viejas);
        var regalados = numeros.Where(puede.PuedeOperar).ToList();

        Assert.True(regalados.Count == 0,
            "se puede escribir en estos centros sin que nadie lo haya concedido: "
            + string.Join(", ", regalados));
    }

    /// <summary>
    /// CONCEDIDO A MANO SI FUNCIONA. Sin esta, el candado de arriba podria estar
    /// cerrando la puerta para siempre y nadie se enteraria.
    /// </summary>
    [Fact]
    public void Operar_Concedido_A_Mano_Abre()
    {
        var puede = Puede(
            ClavesDeCentros.Cambiar,
            ClavesDeCentros.Ver(Centro),
            ClavesDeCentros.Operar(Centro));

        Assert.True(puede.PuedeVer(Centro));
        Assert.True(puede.PuedeOperar(Centro));
    }

    /* ================================================================== */
    /* 4. EL CANDADO QUE, SI SE SUELTA, APAGA EL SELECTOR PARA TODOS      */
    /* ================================================================== */

    /// <summary>
    /// QUE LAS CLAVES VIEJAS NO LAS BORRE EL PUENTE DE REPORTES.
    ///
    /// `reportes.centros.*` empieza con "reportes." pero NO existe en Mac31. El
    /// snapshot borra todo lo que empieza con "reportes." y no esta exento, y lo
    /// vuelve a calcular desde los procesos de Mac31; sin la exencion, las claves
    /// viejas de centros desaparecen en la primera lectura y el selector se apaga
    /// para todo el mundo, sin un error en el log. Toda la compatibilidad de
    /// arriba da igual si la clave ya no esta en el juego de permisos.
    ///
    /// ROTA A PROPOSITO: se puso
    /// `CentrosPermissionPrefix = ClavesDeCentros.Prefijo` —el de la familia
    /// nueva, que es el despiste natural ahora que hay dos— y esta prueba fallo
    /// con "reportes.centros.2004.ver caeria en el reemplazo por lo de Mac31".
    ///
    /// Se llega por reflexion porque el metodo es privado y estatico, y porque es
    /// justo esa decision lo que hay que vigilar: contra la base, este fallo solo
    /// se ve cuando ya le paso a alguien.
    /// </summary>
    [Fact]
    public void Las_Claves_Viejas_De_Centros_No_Las_Borra_El_Puente_De_Reportes()
    {
        var metodo = typeof(PermissionService).GetMethod(
            "EsReporteQueMandaLegacy",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(metodo);

        bool MandaLegacy(string clave) => (bool)metodo!.Invoke(null, new object[] { clave })!;

        Assert.False(MandaLegacy(PermisosDeCentros.Cambiar),
            "reportes.centros.cambiar caeria en el reemplazo por lo de Mac31 y se borraria del snapshot");
        Assert.False(MandaLegacy(PermisosDeCentros.Ver(Centro)),
            "reportes.centros.2004.ver caeria en el reemplazo por lo de Mac31 y se borraria del snapshot");

        // Y la red sigue puesta para lo que SI manda Mac31, que es el otro lado
        // del mismo candado: aflojarlo de mas dejaria pasar reportes heredados.
        Assert.True(MandaLegacy("reportes.ventas.diario"),
            "un reporte normal dejo de regirse por Mac31");
    }

    /// <summary>
    /// Y QUE LOS DOS PREFIJOS NO SE CONFUNDAN. El viejo tiene que seguir siendo
    /// exactamente "reportes.centros." y el nuevo "centros.": el primero es el
    /// que se exime del reemplazo de reportes y el segundo el que decide la
    /// tarjeta de la pantalla de permisos.
    /// </summary>
    [Fact]
    public void Los_Dos_Prefijos_Son_Los_Que_Se_Esperan()
    {
        Assert.Equal("reportes.centros.", PermisosDeCentros.Prefijo);
        Assert.Equal("centros.", ClavesDeCentros.Prefijo);
        Assert.StartsWith("reportes.", PermisosDeCentros.Prefijo, StringComparison.Ordinal);
        Assert.False(ClavesDeCentros.Prefijo.StartsWith("reportes.", StringComparison.Ordinal));
    }

    /* ================================================================== */
    /* 5. QUE LA PANTALLA DEL WEB LAS PUEDA REPARTIR                      */
    /* ================================================================== */

    /// <summary>
    /// QUE LAS CLAVES DE CENTROS NO SE ESCONDAN EN LA PANTALLA DE PERMISOS.
    ///
    /// El catalogo del web solo ofrece lo que "protege algo", y eso se calcula
    /// por reflexion sobre el ensamblado. `centros.2004.ver` NO esta como literal
    /// en ningun sitio: se compone en tiempo de ejecucion con el Numero que esta
    /// en la base. Sin el renglon de centros en ClavesQueProtegenAlgo, las 24
    /// casillas de los doce centros de Zaragoza —incluidas las de ESCRIBIR, que
    /// son las que mas importa poder revisar— se esconderian y no habria forma de
    /// repartirlas.
    ///
    /// ROTA A PROPOSITO: se quito `EsDeCentros(k)` de
    /// ClavesQueProtegenAlgo.Protege y esta prueba fallo nombrando las claves
    /// escondidas.
    /// </summary>
    [Fact]
    public void La_Pantalla_De_Permisos_Ofrece_Las_Claves_De_Centros()
    {
        var deben = new[]
        {
            ClavesDeCentros.Cambiar,
            ClavesDeCentros.MatrizVer,
            ClavesDeCentros.MatrizOperar,
            ClavesDeCentros.Ver(Centro),
            ClavesDeCentros.Operar(Centro),
            PermisosDeCentros.Cambiar,
            PermisosDeCentros.Ver(Centro)
        };

        var escondidas = deben.Where(k => !ClavesQueProtegenAlgo.Protege(k)).ToList();

        Assert.True(escondidas.Count == 0,
            "la pantalla de Accesos y permisos esconderia estas claves y nadie podria repartirlas: "
            + string.Join(", ", escondidas));
    }

    /// <summary>
    /// Y QUE NO SE GATEEN DETRAS DE UNA PUERTA QUE NO EXISTE. La familia nueva no
    /// tiene `centros.ver_modulo` —Centros no es una pantalla del inicio, es el
    /// contexto en el que se abren las demas—, asi que si alguien la metiera en
    /// EntradasDeModulo, todas sus claves pedirian una entrada que nadie sembro y
    /// contestarian 403 a todo el mundo menos al SuperAdmin. Es el fallo que esta
    /// casa ya se encontro tres veces.
    /// </summary>
    [Fact]
    public void Las_Claves_Nuevas_No_Piden_Una_Entrada_Que_No_Existe()
    {
        foreach (var clave in new[]
                 {
                     ClavesDeCentros.Cambiar,
                     ClavesDeCentros.MatrizVer,
                     ClavesDeCentros.MatrizOperar,
                     ClavesDeCentros.Ver(Centro),
                     ClavesDeCentros.Operar(Centro)
                 })
        {
            var entradas = EntradasDeModulo.Requeridas(clave);
            var existen = entradas.All(e =>
                EntradasDeModulo.TodasLasEntradas.Contains(e, StringComparer.OrdinalIgnoreCase));

            Assert.True(entradas.Count == 0 || existen,
                $"{clave} pide la entrada {string.Join("/", entradas)}, que no esta declarada");
        }
    }

    /* ================================================================== */
    /* 6. RECONOCER LAS CLAVES                                            */
    /* ================================================================== */

    [Theory]
    [InlineData("centros.cambiar", true)]
    [InlineData("centros.2004.operar", true)]
    [InlineData("centros.matriz.ver", true)]
    [InlineData("reportes.centros.2004.ver", true)]
    [InlineData("reportes.centros.cambiar", true)]
    [InlineData("clientes.centros.editar", false)]   // existe y NO es de centros
    [InlineData("cortes.cs.ver", false)]
    [InlineData("centroscostos.ver", false)]         // empieza parecido y no lo es
    [InlineData("", false)]
    public void Se_Reconoce_Lo_Que_Es_De_Centros_Y_Lo_Que_No(string clave, bool esperado)
    {
        Assert.Equal(esperado, PermisosDeCentros.EsDeCentros(clave));
    }
}
