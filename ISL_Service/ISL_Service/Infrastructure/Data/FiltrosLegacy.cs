using System.Globalization;

namespace ISL_Service.Infrastructure.Data;

/*
  LOS FILTROS QUE LEGACY ARMABA A MANO, EN UN SOLO LUGAR.

  Cada pantalla de MacServicios2 repetia estas cuatro cosas antes de llamar al
  procedimiento, y cada una las repetia un poco distinto:

    1) La FECHA. Legacy leia `formatoFechaDB` de Constantes y volteaba la cadena
       con Substring de posiciones fijas (Funciones.ConvertFtmDate), para luego
       pegarle " 00:00:00" o " 23:59:59". Dos problemas: la lectura de
       Constantes es la que DISPARA EL CIERRE DEL DIA
       (sp_n_ConsultaConstantes borra y rehace AlmacenProductoHist y adelanta
       Constantes.FechaOperacion), asi que una consulta inofensiva movia la
       fecha de operacion del negocio; y la conversion depende de que el
       DATEFORMAT de la sesion coincida con lo que diga esa columna.
       Aqui se manda SIEMPRE "yyyy-MM-dd HH:mm:ss", que SQL Server interpreta
       igual con cualquier DATEFORMAT. Con eso ya no hace falta leer Constantes
       para una consulta, y por lo tanto no se dispara el cierre del dia.

    2) Las LISTAS DE IDS. Los sp_n_ reciben varchar(4000) con los ids separados
       por VIRGULILLA ("~") y los parten con fn_DevuelveCadenaEnTabla. Legacy
       hacia string.Join("~", lista) sobre una lista que a veces llegaba null, y
       string.Join sobre null lanza ArgumentNullException: el detalle de
       descuentos de legacy contesta 400 con el stack por esto.

    3) El separador es SIEMPRE la virgulilla, nunca la coma. Se verifico leyendo
       el cuerpo de los procedimientos en la base. Equivocarse aqui no da error:
       da cero renglones procesados en silencio.

    4) El APOSTROFO. Legacy hace Replace("'","") en los campos de texto libre
       antes de pasarlos al procedimiento. Aqui todo va parametrizado, asi que
       no haria falta por seguridad — pero se mantiene a proposito: el dato que
       queda GUARDADO en la base tiene que ser el mismo que guardaba Mac31, y
       si aqui se guardara el apostrofo el renglon nuevo no seria igual al
       viejo. Es fidelidad de datos, no defensa contra inyeccion.

  Es publico para poder probarlo sin base.
*/
public static class FiltrosLegacy
{
    /// <summary>
    /// EL separador de las listas de los sp_n_: la virgulilla.
    ///
    /// Es uno y es siempre el mismo, y se comprobo leyendo el cuerpo de los
    /// procedimientos en la base, no suponiendolo: todos declaran
    /// `DECLARE @Delimitador varchar(1)` y luego `SELECT @Delimitador = '~'`
    /// antes de partir la cadena con fn_DevuelveCadenaEnTabla. Los comentarios
    /// de ejemplo de sus cabeceras lo repiten: '15692~7546~7547'.
    ///
    /// Importa decirlo aqui porque la tentacion natural es la coma, y una lista
    /// con comas no falla: llega como UN solo valor que no es numero, y el
    /// procedimiento se queda sin renglones que procesar sin dar ningun error.
    /// </summary>
    public const string SeparadorIds = "~";

    /// <summary>
    /// El mismo separador, con el nombre que usan las listas de usados. Se
    /// conserva como alias porque en esa familia son DOCE listas paralelas y
    /// leer `IdsUsados` dice mas que `Ids` sobre lo que se esta pegando.
    /// </summary>
    public const string SeparadorUsados = SeparadorIds;

    /// <summary>
    /// La fecha como la entiende SQL Server sin depender del DATEFORMAT.
    /// Cadena vacia cuando no hay fecha: asi el procedimiento la ve como "sin
    /// filtro", igual que cuando legacy mandaba "".
    /// </summary>
    public static string Fecha(DateTime? valor)
        => valor is null ? string.Empty : valor.Value.ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>
    /// La fecha SIN hora, "yyyy-MM-dd".
    ///
    /// HACE FALTA, Y DESCUBRIRLO COSTO UN CASO ROJO.
    ///
    /// Hay sp_n_ que COMPLETAN la fecha ellos mismos. El de autorizar un ajuste de
    /// descuento hace, literalmente:
    ///
    ///     -- Agregar la hora
    ///     select @Fecha = @Fecha + ' ' + SUBSTRING(CAST(CONVERT(TIME, GETDATE()) as varchar(20)), 1, 8)
    ///
    /// Si se le manda "2026-10-06 00:00:00", el procedimiento lo deja en
    /// "2026-10-06 00:00:00 23:23:32", que no es una fecha. Y como corre con
    /// SET ANSI_WARNINGS OFF, NO revienta: el UPDATE guarda NULL en la columna de
    /// fecha y contesta result = 1. O sea que el ajuste queda autorizado sin fecha
    /// de autorizacion, y la pantalla dice que todo bien.
    ///
    /// Se comprobo ejecutando el procedimiento con las dos formas:
    ///   @Fecha = '2026-10-06 00:00:00'  ->  [Fecha Autorizacion] = NULL
    ///   @Fecha = '10-06-2026'           ->  [Fecha Autorizacion] = 2026-10-06 23:23:32
    ///
    /// Asi que en esos parametros va la fecha sola y la hora la pone el
    /// procedimiento. Los demas reciben la fecha completa y la usan tal cual.
    /// </summary>
    public static string SoloFecha(DateTime? valor)
        => valor is null ? string.Empty : valor.Value.ToString("yyyy-MM-dd");

    /// <summary>
    /// El inicio de un rango: la fecha a las 00:00:00. Es lo que legacy pegaba
    /// a mano despues de voltear la cadena.
    /// </summary>
    public static string FechaDesde(DateTime? valor)
        => valor is null ? string.Empty : valor.Value.Date.ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>
    /// El fin de un rango: la fecha a las 23:59:59. Sin esto, "hasta el 5" deja
    /// fuera todo lo que se capturo el 5 despues de medianoche.
    /// </summary>
    public static string FechaHasta(DateTime? valor)
        => valor is null
            ? string.Empty
            : valor.Value.Date.AddDays(1).AddSeconds(-1).ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>
    /// Une una lista de ids con virgulilla. Null y lista vacia dan cadena vacia, que
    /// es lo que los procedimientos leen como "todos".
    /// </summary>

    /// <summary>
    /// El mismo pegado, con el nombre que usan las listas de la familia de usados.
    /// </summary>
    public static string IdsUsados(IEnumerable<int>? valores)
        => Unir(valores, SeparadorUsados);

    /// <summary>
    /// Une una lista de importes o cantidades con virgulilla, con punto decimal
    /// e invariante de cultura. Con la cultura del servidor en espanol, un
    /// 12,50 con coma partiria la cadena en dos y el procedimiento leeria dos
    /// renglones donde habia uno.
    /// </summary>
    public static string ImportesUsados(IEnumerable<decimal>? valores)
    {
        if (valores is null) return string.Empty;
        return string.Join(
            SeparadorUsados,
            valores.Select(x => x.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)));
    }

    /// <summary>
    /// Une una lista de textos con virgulilla, quitando el apostrofo de cada
    /// uno. Los codigos de producto del inventario de pedidos viajan asi.
    /// </summary>
    public static string TextosUsados(IEnumerable<string?>? valores)
    {
        if (valores is null) return string.Empty;
        return string.Join(SeparadorUsados, valores.Select(x => Texto(x)));
    }

    /// <summary>
    /// Une una lista de textos con virgulilla, quitando el apostrofo de cada uno.
    /// </summary>
    public static string Textos(IEnumerable<string?>? valores)
    {
        if (valores is null) return string.Empty;
        return string.Join(SeparadorIds, valores.Select(x => Texto(x)));
    }

    /// <summary>
    /// El texto libre tal como lo guardaba Mac31: sin apostrofos y recortado.
    /// Null se vuelve cadena vacia porque los parametros varchar de los sp_n_
    /// no estan preparados para NULL (muchos lo concatenan sin ISNULL y la
    /// condicion entera se vuelve NULL, que no es false: es "ningun renglon").
    /// </summary>
    public static string Texto(string? valor)
        => string.IsNullOrEmpty(valor) ? string.Empty : valor.Replace("'", string.Empty).Trim();

    /// <summary>
    /// El texto recortado a lo que cabe en el parametro. El truncado lo hacia
    /// SQL Server en silencio al asignar a un varchar(n) mas corto; hacerlo
    /// aqui es lo mismo, pero visible.
    /// </summary>
    public static string Texto(string? valor, int largoMaximo)
    {
        var limpio = Texto(valor);
        return limpio.Length <= largoMaximo ? limpio : limpio[..largoMaximo];
    }

    /// <summary>
    /// Limpia una lista de ids: quita los nulos, los ceros y los negativos, y
    /// no repite. Es lo que la pantalla queria decir cuando mandaba
    /// [0, 123, 123, -1].
    /// </summary>
    public static List<int> LimpiarIds(IEnumerable<int>? valores)
    {
        if (valores is null) return new List<int>();
        return valores.Where(x => x > 0).Distinct().ToList();
    }

    /// <summary>
    /// Acota la presentacion al enumPresentacion de Mac31 (0, 1 y 2).
    /// Cualquier otro numero baja al procedimiento como una rama que nadie
    /// escribio.
    /// </summary>
    public static int Presentacion(int? valor)
        => valor is >= 0 and <= 2 ? valor.Value : 0;

    private static string Unir(IEnumerable<int>? valores, string separador)
    {
        if (valores is null) return string.Empty;
        return string.Join(separador, valores);
    }

    /*
      LO QUE TRAIA LA OLA 3 Y ESTA RAMA NO.

      Dos agentes le agregaron metodos a la misma clase a la vez y cada uno
      resolvio lo suyo. Se conservan los de los dos: el compilador dijo
      exactamente cuales faltaban.

      `Fecha` queda con sus DOS sobrecargas -una recibe el texto que manda el
      cliente y la otra un DateTime ya tipado- porque conviven y las dos se
      usan. De `Ids` se queda la de tres parametros, que lleva valores por
      omision y cubre tambien la forma corta; tener las dos seria ambiguo.
    */

    /// <summary>La fecha como la esperan los `sp_n_`, normalizada a ISO.</summary>
    /// <remarks>
    /// Legacy la volteaba con posiciones fijas segun `formatoFechaDB` y la
    /// corrompia sin avisar si no traia 10 caracteres. Aqui se normaliza a ISO,
    /// que SQL Server interpreta igual sea cual sea el DATEFORMAT de la sesion.
    /// Por eso este backend NO tiene que leer Constantes para filtrar por fecha,
    /// que es lo que lo mantiene de solo lectura: `sp_n_ConsultaConstantes` hace
    /// el cierre del dia.
    ///
    /// Las horas SI se copian de legacy: 00:00:00 al inicio del rango y
    /// 23:59:59 al final. Sin ellas un rango "hoy a hoy" pierde todo lo
    /// capturado despues de medianoche.
    /// </remarks>
    public static string Fecha(string? valor, bool finDelDia)
    {
        if (string.IsNullOrWhiteSpace(valor)) return string.Empty;

        var texto = valor.Trim();
        var sinEspacios = texto.Contains('/') ? texto.Replace(" ", "") : texto;
        var formatos = new[]
        {
            "yyyy-MM-dd",
            "yyyy-MM-dd HH:mm:ss",
            "dd/MM/yyyy",
            "d/M/yyyy",
            "dd-MM-yyyy",
            "d-M-yyyy",
            "MM-dd-yyyy",
            "M-d-yyyy"
        };

        if (!DateTime.TryParseExact(sinEspacios, formatos, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
            && !DateTime.TryParse(texto, CultureInfo.GetCultureInfo("es-MX"), DateTimeStyles.None, out fecha))
        {
            /*
              No se entendio: se pasa tal cual y que el procedimiento decida.
              Tragarsela y mandar "" convertiria un filtro escrito mal en una
              consulta SIN filtro, que es peor que un error: el usuario creeria
              estar viendo febrero y estaria viendo todo.
            */
            return texto;
        }

        var normalizada = finDelDia ? fecha.Date.AddDays(1).AddSeconds(-1) : fecha.Date;
        return normalizada.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    /// <summary>Une ids para los parametros `@IDs...`.</summary>
    /// <remarks>
    /// El separador por omision es la VIRGULILLA porque es el de toda la
    /// familia legacy y varios procedimientos lo traen escrito a mano dentro.
    /// Con coma y DOS O MAS ids el filtro no encuentra nada; con UNO funciona
    /// igual, asi que el error no se nota hasta que alguien marca dos casillas.
    /// Se quitan los no positivos y los repetidos: un 0 en la lista la
    /// convierte en un filtro que no empareja con nada.
    /// </remarks>
    public static string Ids(IEnumerable<int>? valores, string? crudo = null, string separador = "~")
    {
        if (!string.IsNullOrWhiteSpace(crudo)) return crudo.Trim();
        if (valores == null) return string.Empty;
        return string.Join(separador, valores.Where(x => x > 0).Distinct());
    }

    /// <summary>El folio vacio viaja como "0", que es lo que espera el procedimiento.</summary>
    public static string Folio(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? "0" : valor.Trim();

    /// <summary>Texto libre sin apostrofos.</summary>
    /// <remarks>
    /// Los `sp_n_Consultar*` arman su WHERE concatenando y lo corren con
    /// `EXEC(@Sql)`: pasar el filtro como SqlParameter NO protege, porque el
    /// procedimiento lo concatena adentro.
    /// </remarks>
    public static string TextoSinApostrofo(string? valor)
        => Texto(valor).Replace("'", string.Empty);
}
