using System.Reflection;
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
/// Las pruebas de aqui son las que convierten eso en algo comprobable. CADA UNA
/// SE ROMPIO A PROPOSITO antes de darla por buena —se quita la linea de
/// compatibilidad que vigila y se comprueba que la prueba FALLA—, porque una
/// prueba que pasa con el codigo roto no vigila nada. En el comentario de cada
/// bloque esta QUE se rompio y COMO fallo.
///
/// La mitad de las pruebas mira el otro lado: que la compatibilidad NO REGALE
/// `operar`. Esa es la decision de negocio que no se ablanda, y es tan
/// importante que no falte acceso como que no sobre.
/// </remarks>
public class CompatibilidadDeCentrosTests
{
    private const int Centro = 2004;   // ORIENTE, en las dos empresas

    /* ================================================================== */
    /* 1. LA EQUIVALENCIA: QUIEN HOY PUEDE, MAÑANA PUEDE                  */
    /* ================================================================== */

    /// <summary>
    /// ROTA A PROPOSITO: quitando el bloque `if (tiene.Contains(CambiarLegado))`
    /// de CompatibilidadDeCentros, esta prueba falla con
    /// "no se tradujo el selector".
    /// </summary>
    [Fact]
    public void El_Selector_Viejo_Vale_Como_El_Nuevo()
    {
        var permisos = new List<string> { PermisosDeCentros.CambiarLegado };

        CompatibilidadDeCentros.Aplicar(permisos);

        Assert.Contains(PermisosDeCentros.Cambiar, permisos);

        // Y el guardia, que es quien pregunta de verdad, abre.
        Assert.True(GuardiaDeCentros.PuedeCambiar(permisos.Contains));
    }

    /// <summary>
    /// ROTA A PROPOSITO: quitando el bucle que recorre las claves viejas de ver,
    /// esta prueba falla con "no se tradujo el centro 2004".
    /// </summary>
    [Fact]
    public void El_Permiso_Viejo_De_Un_Centro_Vale_Como_El_Nuevo()
    {
        var permisos = new List<string>
        {
            PermisosDeCentros.CambiarLegado,
            PermisosDeCentros.VerLegado(Centro)
        };

        CompatibilidadDeCentros.Aplicar(permisos);

        Assert.Contains(PermisosDeCentros.Ver(Centro), permisos);
        Assert.True(GuardiaDeCentros.PuedeVer(Centro, permisos.Contains));
    }

    /// <summary>
    /// LOS DOCE DE ZARAGOZA, DE GOLPE. Es el caso real: 2001..2012 repartidos a
    /// mano, y ninguno se puede quedar fuera.
    /// </summary>
    /// <remarks>
    /// MIRA LA CLAVE TRADUCIDA Y NO AL GUARDIA, Y ES A PROPOSITO. Se descubrio
    /// rompiendo: al quitar la traduccion, una version de esta prueba que
    /// preguntaba por GuardiaDeCentros SEGUIA PASANDO, porque el guardia acepta
    /// ademas la clave vieja. Esa red esta puesta a proposito —ver
    /// GuardiaDeCentros— pero convierte a esta prueba en un adorno: taparia
    /// justo el fallo que tiene que cazar. Quien vigila el acceso de punta a
    /// punta es El_Dia_Del_Despliegue_Nadie_Cambia_De_Sitio; esta vigila la
    /// traduccion.
    /// </remarks>
    [Fact]
    public void Los_Doce_Centros_De_Zaragoza_Se_Traducen_Todos()
    {
        var numeros = Enumerable.Range(2001, 12).ToArray();

        var permisos = new List<string> { PermisosDeCentros.CambiarLegado };
        permisos.AddRange(numeros.Select(PermisosDeCentros.VerLegado));

        CompatibilidadDeCentros.Aplicar(permisos);

        var sinTraducir = numeros
            .Where(n => !permisos.Contains(PermisosDeCentros.Ver(n), StringComparer.OrdinalIgnoreCase))
            .ToList();

        Assert.True(sinTraducir.Count == 0,
            "estos centros los tiene hoy y su clave nueva no se genero: "
            + string.Join(", ", sinTraducir));
    }

    /// <summary>
    /// LO QUE NO SE TENIA NO SE REGALA. La equivalencia traduce, no reparte: el
    /// centro 2007 que nunca estuvo concedido sigue sin estarlo.
    /// </summary>
    [Fact]
    public void No_Se_Traduce_Un_Centro_Que_Nadie_Le_Dio()
    {
        var permisos = new List<string>
        {
            PermisosDeCentros.CambiarLegado,
            PermisosDeCentros.VerLegado(2004)
        };

        CompatibilidadDeCentros.Aplicar(permisos);

        Assert.DoesNotContain(PermisosDeCentros.Ver(2007), permisos);
        Assert.False(GuardiaDeCentros.PuedeVer(2007, permisos.Contains));
    }

    /// <summary>
    /// SIN EL SELECTOR VIEJO NO HAY SELECTOR NUEVO. Quien hoy no puede cambiar de
    /// centro tampoco podra mañana: la compatibilidad no abre puertas nuevas.
    /// </summary>
    [Fact]
    public void Sin_El_Selector_Viejo_No_Se_Concede_El_Nuevo()
    {
        var permisos = new List<string> { "reportes.ver_modulo" };

        CompatibilidadDeCentros.Aplicar(permisos);

        Assert.DoesNotContain(PermisosDeCentros.Cambiar, permisos);
        Assert.False(GuardiaDeCentros.PuedeCambiar(permisos.Contains));
    }

    /* ================================================================== */
    /* 2. LA MATRIZ: NADIE SE QUEDA FUERA EL DIA DEL DESPLIEGUE           */
    /* ================================================================== */

    /// <summary>
    /// ROTA A PROPOSITO: quitando los dos `Agregar(Matriz...)` de
    /// CompatibilidadDeCentros, esta prueba falla con "se quedaria fuera de la
    /// matriz".
    ///
    /// POR QUE SE PRUEBA CON LA LISTA MAS POBRE QUE HAY. Si la concesion de la
    /// matriz dependiera de tener algo —cualquier cosa—, el dia del despliegue
    /// habria alguien, en algun rol raro, que no lo tendria, y a esa persona el
    /// sistema entero dejaria de contestarle. Que funcione con un solo permiso
    /// cualquiera es exactamente lo que hay que vigilar.
    /// </summary>
    [Fact]
    public void Quien_Hoy_Entra_A_La_Matriz_Sigue_Entrando()
    {
        var permisos = new List<string> { "ventas.ver_modulo" };

        CompatibilidadDeCentros.Aplicar(permisos);

        Assert.Contains(PermisosDeCentros.MatrizVer, permisos);
        Assert.True(GuardiaDeCentros.PuedeVer(GuardiaDeCentros.Matriz, permisos.Contains));
    }

    /// <summary>
    /// Y SIGUE PUDIENDO GUARDAR. La otra mitad, y la que de olvidarse deja a la
    /// empresa entera sin poder escribir nada: hoy todo el que guarda, guarda en
    /// la matriz.
    ///
    /// ROTA A PROPOSITO: quitando `Agregar(MatrizOperar)`, falla con "no podria
    /// guardar en la matriz".
    /// </summary>
    [Fact]
    public void Quien_Hoy_Guarda_En_La_Matriz_Sigue_Guardando()
    {
        var permisos = new List<string> { "ventas.pedidos.crear" };

        CompatibilidadDeCentros.Aplicar(permisos);

        Assert.Contains(PermisosDeCentros.MatrizOperar, permisos);
        Assert.True(GuardiaDeCentros.PuedeOperar(GuardiaDeCentros.Matriz, permisos.Contains));
    }

    /// <summary>
    /// LO QUE SE NIEGA A MANO SE RESPETA. Sin esto, apagarle la matriz a alguien
    /// a proposito seria imposible y la pantalla de permisos mentiria.
    /// </summary>
    [Fact]
    public void Una_Negacion_Explicita_De_La_Matriz_Manda()
    {
        var permisos = new List<string> { "ventas.ver_modulo" };
        var negadas = new[] { PermisosDeCentros.MatrizVer };

        CompatibilidadDeCentros.Aplicar(permisos, negadas);

        Assert.DoesNotContain(PermisosDeCentros.MatrizVer, permisos);
        // La de operar no se negó, asi que esa si queda.
        Assert.Contains(PermisosDeCentros.MatrizOperar, permisos);
    }

    /// <summary>
    /// Y tambien se respeta sobre una clave traducida: negar `centros.2004.ver`
    /// gana sobre tener el permiso viejo de ese centro.
    /// </summary>
    [Fact]
    public void Una_Negacion_Explicita_De_Un_Centro_Manda()
    {
        var permisos = new List<string>
        {
            PermisosDeCentros.CambiarLegado,
            PermisosDeCentros.VerLegado(Centro)
        };

        CompatibilidadDeCentros.Aplicar(permisos, new[] { PermisosDeCentros.Ver(Centro) });

        Assert.DoesNotContain(PermisosDeCentros.Ver(Centro), permisos);
    }

    /* ================================================================== */
    /* 3. `operar` NO SE DEDUCE DE `ver`. NUNCA.                          */
    /* ================================================================== */

    /// <summary>
    /// LA PRUEBA QUE VIGILA QUE NADIE "AYUDE" AL USUARIO.
    ///
    /// ROTA A PROPOSITO AL REVES: se agrego a CompatibilidadDeCentros un
    /// `Agregar(PermisosDeCentros.Operar(numero))` dentro del bucle de
    /// traduccion —que es exactamente el atajo que alguien escribiria para que
    /// "funcione"— y esta prueba fallo con "se regalo el permiso de ESCRIBIR en
    /// el centro 2004". Despues se quito.
    ///
    /// Si esta prueba pasa con ese atajo puesto, no sirve y hay que arreglarla,
    /// no borrarla: lo que estaria mal es el candado, no el aviso.
    /// </summary>
    [Fact]
    public void Ver_Un_Centro_No_Concede_Escribir_En_El()
    {
        var permisos = new List<string>
        {
            PermisosDeCentros.CambiarLegado,
            PermisosDeCentros.VerLegado(Centro),
            PermisosDeCentros.Ver(Centro)
        };

        CompatibilidadDeCentros.Aplicar(permisos);

        Assert.DoesNotContain(PermisosDeCentros.Operar(Centro), permisos);
        Assert.False(
            GuardiaDeCentros.PuedeOperar(Centro, permisos.Contains),
            "se regalo el permiso de ESCRIBIR en el centro 2004 a quien solo puede consultarlo");
    }

    /// <summary>
    /// LA COMPATIBILIDAD NO PUEDE PRODUCIR NINGUN `operar` DE CENTRO, con
    /// cualquier combinacion de claves viejas. Es la version general de la de
    /// arriba: la de arriba vigila un centro, esta vigila la regla.
    /// </summary>
    [Fact]
    public void La_Compatibilidad_No_Produce_Ningun_Operar_De_Centro()
    {
        var todoLoViejo = new List<string> { PermisosDeCentros.CambiarLegado };
        todoLoViejo.AddRange(Enumerable.Range(2001, 12).Select(PermisosDeCentros.VerLegado));

        var agregadas = CompatibilidadDeCentros.ClavesQueSeAgregan(todoLoViejo);

        var regalados = agregadas
            .Where(k => k.EndsWith(".operar", StringComparison.OrdinalIgnoreCase))
            .Where(k => !string.Equals(k, PermisosDeCentros.MatrizOperar, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(regalados.Count == 0,
            "la compatibilidad esta repartiendo permisos de ESCRIBIR en centros: "
            + string.Join(", ", regalados));
    }

    /// <summary>
    /// Y escribir en un centro NO lo abre la familia vieja. No hay clave vieja de
    /// escribir —alla los centros solo servian para sacar reportes—, asi que no
    /// hay nada de donde heredar.
    /// </summary>
    [Fact]
    public void El_Permiso_Viejo_No_Abre_Escribir_En_El_Centro()
    {
        var permisos = new List<string>
        {
            PermisosDeCentros.CambiarLegado,
            PermisosDeCentros.VerLegado(Centro)
        };

        CompatibilidadDeCentros.Aplicar(permisos);

        Assert.False(GuardiaDeCentros.PuedeOperar(Centro, permisos.Contains));
    }

    /// <summary>
    /// CONCEDIDO A MANO SI FUNCIONA: lo que se reparte desde el web se respeta
    /// tal cual. Sin esta, el candado de arriba podria estar cerrando la puerta
    /// para siempre y nadie se enteraria.
    /// </summary>
    [Fact]
    public void Operar_Concedido_A_Mano_Abre()
    {
        var permisos = new List<string>
        {
            PermisosDeCentros.Cambiar,
            PermisosDeCentros.Ver(Centro),
            PermisosDeCentros.Operar(Centro)
        };

        CompatibilidadDeCentros.Aplicar(permisos);

        Assert.True(GuardiaDeCentros.PuedeOperar(Centro, permisos.Contains));
        Assert.True(GuardiaDeCentros.PuedeVer(Centro, permisos.Contains));
    }

    /* ================================================================== */
    /* 4. LA COMPATIBILIDAD SOLO SUMA                                     */
    /* ================================================================== */

    /// <summary>
    /// NO BORRA NADA. Ni las claves viejas —que siguen en el catalogo y hay que
    /// poder quitarlas desde la pantalla— ni nada de lo demas.
    /// </summary>
    [Fact]
    public void No_Quita_Ninguna_Clave()
    {
        var antes = new List<string>
        {
            "reportes.ver_modulo",
            PermisosDeCentros.CambiarLegado,
            PermisosDeCentros.VerLegado(Centro),
            "ventas.facturar"
        };
        var copia = antes.ToList();

        CompatibilidadDeCentros.Aplicar(antes);

        foreach (var clave in copia)
            Assert.Contains(clave, antes);
    }

    /// <summary>
    /// CORRERLA DOS VECES NO DUPLICA. El snapshot se arma mas de una vez por
    /// sesion y una lista con la misma clave repetida se ve igual de bien hasta
    /// que alguien la cuenta.
    /// </summary>
    [Fact]
    public void Aplicarla_Dos_Veces_No_Agrega_Nada_La_Segunda()
    {
        var permisos = new List<string>
        {
            PermisosDeCentros.CambiarLegado,
            PermisosDeCentros.VerLegado(Centro)
        };

        var primera = CompatibilidadDeCentros.Aplicar(permisos);
        var segunda = CompatibilidadDeCentros.Aplicar(permisos);

        Assert.True(primera > 0, "la primera pasada no agrego nada");
        Assert.Equal(0, segunda);
        Assert.Equal(permisos.Count, permisos.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /* ================================================================== */
    /* 5. EL CANDADO QUE, SI SE SUELTA, APAGA EL SELECTOR PARA TODOS      */
    /* ================================================================== */

    /// <summary>
    /// QUE LAS CLAVES VIEJAS SIGAN EXENTAS DEL REEMPLAZO POR LO DE MAC31.
    ///
    /// `reportes.centros.*` empieza con "reportes." pero NO existe en Mac31. El
    /// snapshot borra todo lo que empieza con "reportes." y no esta exento, y lo
    /// vuelve a calcular desde los procesos de Mac31; sin la exencion, las claves
    /// viejas de centros desaparecen en la primera lectura y el selector se apaga
    /// para todo el mundo, sin un error en el log.
    ///
    /// ROTA A PROPOSITO: se puso
    /// `CentrosPermissionPrefix = PermisosDeCentros.Prefijo` —el nuevo, que es el
    /// despiste natural ahora que hay dos— y esta prueba fallo con
    /// "reportes.centros.2004.ver caeria en el reemplazo por lo de Mac31".
    ///
    /// Se llega por reflexion porque el metodo es privado y estatico, y porque lo
    /// que hay que vigilar es justo esa decision y no un camino que pase por la
    /// base: contra la base, este fallo solo se ve cuando ya le paso a alguien.
    /// </summary>
    [Fact]
    public void Las_Claves_Viejas_De_Centros_No_Las_Borra_El_Puente_De_Reportes()
    {
        var metodo = typeof(PermissionService).GetMethod(
            "EsReporteQueMandaLegacy",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(metodo);

        bool MandaLegacy(string clave) => (bool)metodo!.Invoke(null, new object[] { clave })!;

        Assert.False(MandaLegacy(PermisosDeCentros.CambiarLegado),
            "reportes.centros.cambiar caeria en el reemplazo por lo de Mac31 y se borraria del snapshot");
        Assert.False(MandaLegacy(PermisosDeCentros.VerLegado(Centro)),
            "reportes.centros.2004.ver caeria en el reemplazo por lo de Mac31 y se borraria del snapshot");

        // Y la red sigue puesta para lo que SI manda Mac31, que es el otro lado
        // del mismo candado: aflojarlo de mas dejaria pasar reportes heredados.
        Assert.True(MandaLegacy("reportes.ventas.diario"),
            "un reporte normal dejo de regirse por Mac31");
    }

    /// <summary>
    /// Y QUE LOS DOS PREFIJOS NO SE CONFUNDAN. El viejo tiene que seguir siendo
    /// exactamente "reportes.centros." y el nuevo "centros.": el primero se
    /// compara contra el reemplazo de reportes y el segundo contra el modulo de
    /// la pantalla de permisos.
    /// </summary>
    [Fact]
    public void Los_Dos_Prefijos_Son_Los_Que_Se_Esperan()
    {
        Assert.Equal("reportes.centros.", PermisosDeCentros.PrefijoLegado);
        Assert.Equal("centros.", PermisosDeCentros.Prefijo);
        Assert.StartsWith("reportes.", PermisosDeCentros.PrefijoLegado, StringComparison.Ordinal);
        Assert.False(PermisosDeCentros.Prefijo.StartsWith("reportes.", StringComparison.Ordinal));
    }

    /* ================================================================== */
    /* 6. QUE LA PANTALLA DEL WEB LAS PUEDA REPARTIR                      */
    /* ================================================================== */

    /// <summary>
    /// QUE LAS CLAVES NUEVAS NO SE ESCONDAN EN LA PANTALLA DE PERMISOS.
    ///
    /// El catalogo del web solo ofrece lo que "protege algo", y eso se calcula
    /// por reflexion sobre el ensamblado. `centros.2004.ver` NO esta como literal
    /// en ningun sitio: se compone en tiempo de ejecucion con el Numero que esta
    /// en la base. Sin el renglon de centros en ClavesQueProtegenAlgo, las 24
    /// casillas de los doce centros de Zaragoza —incluidas las de ESCRIBIR— se
    /// esconderian y no habria forma de repartirlas.
    ///
    /// ROTA A PROPOSITO: se quito `EsDeCentros(k)` de
    /// ClavesQueProtegenAlgo.Protege y esta prueba fallo nombrando las cinco
    /// claves escondidas.
    /// </summary>
    [Fact]
    public void La_Pantalla_De_Permisos_Ofrece_Las_Claves_Nuevas()
    {
        var deben = new[]
        {
            PermisosDeCentros.Cambiar,
            PermisosDeCentros.MatrizVer,
            PermisosDeCentros.MatrizOperar,
            PermisosDeCentros.Ver(Centro),
            PermisosDeCentros.Operar(Centro),
            PermisosDeCentros.CambiarLegado,
            PermisosDeCentros.VerLegado(Centro)
        };

        var escondidas = deben.Where(k => !ClavesQueProtegenAlgo.Protege(k)).ToList();

        Assert.True(escondidas.Count == 0,
            "la pantalla de Accesos y permisos esconderia estas claves y nadie podria repartirlas: "
            + string.Join(", ", escondidas));
    }

    /// <summary>
    /// Y QUE NO SE GATEEN DETRAS DE UNA PUERTA QUE NO EXISTE. La familia nueva no
    /// tiene `centros.ver_modulo` —Centros no es una pantalla del inicio—, asi
    /// que si alguien la metiera en EntradasDeModulo, todas sus claves pedirian
    /// una entrada que nadie sembro y contestarian 403 a todo el mundo menos al
    /// SuperAdmin. Es el fallo que esta casa ya se encontro tres veces.
    /// </summary>
    [Fact]
    public void Las_Claves_Nuevas_No_Piden_Una_Entrada_Que_No_Existe()
    {
        foreach (var clave in new[]
                 {
                     PermisosDeCentros.Cambiar,
                     PermisosDeCentros.MatrizVer,
                     PermisosDeCentros.MatrizOperar,
                     PermisosDeCentros.Ver(Centro),
                     PermisosDeCentros.Operar(Centro)
                 })
        {
            var entradas = EntradasDeModulo.Requeridas(clave);
            var existen = entradas.All(e => EntradasDeModulo.TodasLasEntradas.Contains(e, StringComparer.OrdinalIgnoreCase));

            Assert.True(entradas.Count == 0 || existen,
                $"{clave} pide la entrada {string.Join("/", entradas)}, que no esta declarada");
        }
    }

    /* ================================================================== */
    /* 7. RECONOCER LAS CLAVES                                            */
    /* ================================================================== */

    [Theory]
    [InlineData("centros.cambiar", null)]
    [InlineData("centros.matriz.ver", null)]          // la matriz no tiene Numero
    [InlineData("centros.matriz.operar", null)]
    [InlineData("centros.2004.operar", null)]         // no es clave de VER
    [InlineData("centros.2004.ver", 2004)]
    [InlineData("reportes.centros.2004.ver", 2004)]
    [InlineData("reportes.centros.cambiar", null)]
    [InlineData("ventas.facturar", null)]
    [InlineData("centros.abc.ver", null)]
    [InlineData("", null)]
    public void El_Numero_De_La_Clave_Se_Lee_Bien(string clave, int? esperado)
    {
        Assert.Equal(esperado, PermisosDeCentros.NumeroDeClaveDeVer(clave));
    }

    [Theory]
    [InlineData("centros.cambiar", true)]
    [InlineData("centros.2004.operar", true)]
    [InlineData("reportes.centros.2004.ver", true)]
    [InlineData("clientes.centros.editar", false)]   // existe y NO es de centros
    [InlineData("cortes.cs.ver", false)]
    [InlineData("centroscostos.ver", false)]         // empieza parecido y no lo es
    public void Se_Reconoce_Lo_Que_Es_De_Centros_Y_Lo_Que_No(string clave, bool esperado)
    {
        Assert.Equal(esperado, PermisosDeCentros.EsDeCentros(clave));
    }
}
