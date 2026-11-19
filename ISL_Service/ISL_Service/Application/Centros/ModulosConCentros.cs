namespace ISL_Service.Application.Centros;

/// <summary>
/// QUE MODULOS SABEN DE CENTROS. Declaracion explicita, una linea por modulo.
/// </summary>
/// <remarks>
/// POR QUE UNA LISTA Y NO "EL QUE NO FALLE"
/// ----------------------------------------
/// Porque casi todos los modulos FUNCIONARIAN con la base cambiada, y
/// contestarian datos del centro con la mitad de la pantalla pensada para la
/// matriz: folios que se piden a un consecutivo que no es el suyo, almacenes
/// que no existen alla, un corte de caja que suma dos negocios. Lo malo no es
/// que reviente, es que NO reviente.
///
/// Y POR QUE EL QUE NO ESTA DECLARADO DEVUELVE 409 Y NO DATOS DE LA MATRIZ
/// ----------------------------------------------------------------------
/// Porque ensenar la matriz cuando el usuario escogio EJE 6 es ensenarle un
/// sitio diciendole que es otro. Una pantalla vacia se entiende; una pantalla
/// llena de los numeros equivocados se cree. El 409 dice la verdad y es
/// accionable: "este modulo todavia no sabe de centros, vuelve a la matriz".
///
/// ANADIR UN MODULO ES UNA LINEA AQUI. Nada mas. El modulo no tiene que
/// enterarse de permisos ni de nombres de bases: pide la conexion a
/// <see cref="Application.Interfaces.IConexionDelCentro"/> con su clave y la
/// recibe ya apuntada, o recibe el 409.
///
/// LAS OPERACIONES QUE ESCRIBEN SE NOMBRAN UNA POR UNA. No se deduce ninguna:
/// escribir en un centro desde una sola instalacion es la parte que puede
/// estropear datos ajenos, asi que no se concede por descuido. Un modulo
/// declarado sin operaciones de escritura es de SOLO LECTURA fuera de la
/// matriz, por mas permisos que tenga quien lo pida.
/// </remarks>
public static class ModulosConCentros
{
    /*
      EL REGISTRO. Una linea por modulo:

        ["mi_modulo"] = ModuloConCentros.SoloLectura("mi_modulo"),
        ["otro"]      = ModuloConCentros.ConEscritura("otro", "guardar", "cancelar"),

      Y si un modulo NO esta aqui, con X-Centro != 0 contesta 409. Esa es la
      omision, y es a proposito.
    */
    private static readonly Dictionary<string, ModuloConCentros> Registro =
        new(StringComparer.OrdinalIgnoreCase)
        {
            /* Reportes es el unico que hoy sabe leer de otra base: es lo que ya
               hace ReportesVentasRepository.GetConnection(baseDelCentro). No
               escribe nada fuera de la matriz — un reporte solo lee. */
            ["reportes"] = ModuloConCentros.SoloLectura("reportes"),

            /*  LOS CUATRO QUE ENTRARON EL 19-NOV-2026, TODOS DE SOLO LECTURA.
                ---------------------------------------------------------------
                Se midio cuanto hay en cada sitio antes de declararlos, porque
                un modulo no merece selector de centro si alla no hay nada que
                ensenar. En produccion de Zaragoza:

                  modulo    | matriz (MacZ) | los trece centros
                  ----------|---------------|-------------------
                  cortes    |             0 | 2,163  <- TODO esta alla
                  facturas  |        41,059 | 6,799
                  ajustes   |        15,612 | 1,501
                  transito  |        97,195 | 27,066

                Y los nueve sp_n_ que estos cuatro llaman estan en LAS TRECE
                bases: Mac31 las instalo asi. No hizo falta ningun script.

                Cortes es el caso que lo justifica solo: su tabla de la matriz
                tiene CERO filas en Zaragoza y los ~2,163 retiros viven en
                [Corte CS] dentro de cada base de centro. Sin esta linea el
                modulo esta condenado a salir vacio para siempre.

                ALMACEN DE CASCOS entro despues y por otra puerta: ver su
                propia linea mas abajo.

                POR QUE LOS CUATRO SON SoloLectura
                ----------------------------------
                Fue lo que se pidio: "asi como en reportes". Y reportes es de
                solo lectura porque un reporte solo lee.

                Las escrituras de estos cuatro —cancelar una factura, capturar
                o cancelar un ajuste, procesar una remision— NO se declaran, asi
                que con un centro puesto la capa contesta "esta accion solo se
                puede hacer en la matriz" (ConexionDelCentro.cs:144) y en la
                matriz siguen funcionando byte por byte como hoy. Eso es
                deliberado: escribir en la base de un centro desde una sola
                instalacion es la parte que puede estropear datos ajenos, y
                cada operacion se concede por su nombre cuando alguien decida
                que toca. Lo que hoy se gana es poder MIRAR el centro, que es
                justo lo que no se podia.  */
            ["cortes"]               = ModuloConCentros.SoloLectura("cortes"),
            ["facturacion"]          = ModuloConCentros.SoloLectura("facturacion"),
            ["inventario.ajustes"]   = ModuloConCentros.SoloLectura("inventario.ajustes"),
            ["inventario.transito"]  = ModuloConCentros.SoloLectura("inventario.transito"),

            /*  EL CATALOGO QUE LOS ACOMPANA, Y SOLO ESE.
                ---------------------------------------------------------------
                Los almacenes son de la base en la que viven. La pantalla de
                ajustes manda `idAlmacen` al servidor para filtrar, y ese id se
                resuelve DENTRO de la base que se esta consultando: con los
                ajustes saliendo de un centro y el catalogo saliendo de la
                matriz, el filtro buscaria un numero que alla significa otra
                cosa, o nada.

                La clave es `catalogos.almacenes` y no `catalogos` a proposito:
                declarar el modulo entero arrastraria los otros veintitantos
                catalogos —agentes, tipos de gasto, dias festivos— que NO
                cambian de significado por centro, y los arrastraria en las
                veinte pantallas que los piden. Un catalogo, una linea.  */
            ["catalogos.almacenes"]  = ModuloConCentros.SoloLectura("catalogos.almacenes"),

            /*  ALMACEN DE CASCOS, Y ES EL CASO RARO DEL REGISTRO.
                ---------------------------------------------------------------
                Primero se dejo FUERA, y por una medicion equivocada: se busco
                `WMovimientoCasco` en las trece bases, no estaba, y se concluyo
                que alla no habia nada que ensenar. La tabla que no existe alla
                no es donde estan los datos.

                Lo que de verdad hay, medido en produccion el 19-nov-2026:

                  UsadosMovimientos (lo que lee Mac31) | MacZ: 0 | centros: 7,818
                  WMovimientoCasco  (lo que lee la app)| MacZ: 2 | centros: no existe

                O sea el mismo reparto que cortes -todo en los centros, cero en
                la matriz- solo que en la tabla de LEGACY. Las tablas W son
                nuevas, se hicieron para la app y solo viven en la matriz.

                Por eso este modulo no lee lo mismo en los dos sitios: en la
                matriz, sus propias tablas; en un centro,
                `sp_n_ConsultarUsadosMovimientos`, que es el procedimiento de
                la pantalla de Mac31. Esta declarado SIN escrituras y eso aqui
                pesa mas que en los otros: escribir en la tabla nueva de un
                centro crearia una segunda verdad al lado de la de legacy, y
                nadie sabria cual de las dos listas es la buena.  */
            ["almacen.cascos"]       = ModuloConCentros.SoloLectura("almacen.cascos"),
        };

    /// <summary>Los modulos declarados, para diagnostico y para las pruebas.</summary>
    public static IReadOnlyCollection<ModuloConCentros> Todos => Registro.Values.ToList();

    /// <summary>La declaracion de ese modulo, o null si no esta declarado.</summary>
    public static ModuloConCentros? Buscar(string? modulo)
    {
        var clave = (modulo ?? "").Trim();
        if (clave.Length == 0) return null;
        return Registro.TryGetValue(clave, out var encontrado) ? encontrado : null;
    }

    /// <summary>Si ese modulo puede trabajar fuera de la matriz.</summary>
    public static bool SoportaCentros(string? modulo) => Buscar(modulo)?.SoportaCentros == true;

    /// <summary>Si esa operacion de ese modulo puede ESCRIBIR fuera de la matriz.</summary>
    public static bool PuedeEscribirFueraDeLaMatriz(string? modulo, string? operacion)
        => Buscar(modulo)?.Escribe(operacion) == true;
}

/// <summary>Lo que un modulo declara sobre los centros.</summary>
/// <param name="Clave">La clave del modulo: "reportes", "ventas"...</param>
/// <param name="SoportaCentros">
/// false = ni leyendo. Se deja poner explicitamente para poder declarar
/// "este modulo lo revisamos y NO" sin que parezca un olvido.
/// </param>
/// <param name="OperacionesQueEscriben">
/// Las operaciones que admiten ESCRITURA fuera de la matriz. Lo que no esta
/// aqui es solo lectura.
/// </param>
public sealed record ModuloConCentros(
    string Clave,
    bool SoportaCentros,
    IReadOnlySet<string> OperacionesQueEscriben)
{
    public static ModuloConCentros SoloLectura(string clave) =>
        new(clave, true, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    public static ModuloConCentros ConEscritura(string clave, params string[] operaciones) =>
        new(clave, true, new HashSet<string>(operaciones, StringComparer.OrdinalIgnoreCase));

    /// <summary>Declarado y revisado: este modulo NO trabaja fuera de la matriz.</summary>
    public static ModuloConCentros No(string clave) =>
        new(clave, false, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    public bool Escribe(string? operacion)
    {
        if (!SoportaCentros) return false;
        var nombre = (operacion ?? "").Trim();
        return nombre.Length > 0 && OperacionesQueEscriben.Contains(nombre);
    }
}
