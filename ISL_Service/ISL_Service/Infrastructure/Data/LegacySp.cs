using System.Data;
using System.Globalization;

namespace ISL_Service.Infrastructure.Data;

/*
  LO QUE HAY QUE HACERLE A UN PARAMETRO ANTES DE DARSELO A UN sp_n_ DE OBRA O AVIACION.

  Los procedimientos de estos dos modulos comparten cuatro manias y las cuatro
  se resuelven aqui una sola vez, porque repartirlas por veinte repositorios es
  como se pierden:

  1. LOS FILTROS DE TEXTO ENTRAN A UN EXEC(@Sql).
     sp_n_Consultar_MCContratos arma el WHERE concatenando y lo corre con
     EXEC(@Sql) (leido de sys.sql_modules de Produccion_svr, linea 147:
     "SELECT @SQL = @SQL + ' AND C.Fecha BETWEEN ''' + ... "). Pasar el filtro
     como SqlParameter NO protege: la inyeccion ocurre dentro del
     procedimiento, con el valor ya pegado al texto. De los filtros de mi lote
     Legacy solo saneaba el de gasolinas. Por eso Filtro() quita el apostrofo.

  2. LOS CAMPOS QUE SE GUARDAN NO SE SANEAN.
     sp_n_Insertar_MCPuesto hace un INSERT parametrizado de verdad
     (INSERT INTO MC_Puestos ... VALUES (..., @Puesto, ...)), asi que ahi el
     apostrofo es inofensivo. Legacy igual lo borraba
     (MCPuestoService.cs:63, `.Replace("'", "")`), de modo que un puesto
     llamado "ASESOR DE O'HIGGINS" se guardaba sin el apostrofo y nadie se
     enteraba. Texto() solo recorta: el dato se guarda como lo escribieron.

  3. LAS FECHAS VIAJAN COMO CADENA Y LEGACY LES VOLTEABA EL DIA Y EL MES.
     Varios de estos SP declaran @FechaInicial varchar(30) y lo concatenan al
     EXEC. Legacy leia Constantes.formatoFechaDB y, cuando valia "MM-dd-yyyy",
     volteaba la cadena a mano (Funciones.ConvertFtmDate: Substring(3,2) +
     Substring(0,2) + Substring(6,4) sobre una cadena que SUPONE en
     dd-MM-yyyy). Para leer esa constante llamaba a sp_n_ConsultaConstantes,
     que no consulta: cierra el dia del negocio (ver
     migracion/CUIDADO-consultas-que-escriben.md). Aqui la fecha llega tipada y
     se escribe en ISO con la T, que SQL Server interpreta igual con cualquier
     DATEFORMAT. Asi ninguna consulta de este lote necesita tocar Constantes y
     ninguna puede rodar la fecha de operacion.

  4. LAS LISTAS VIAJAN PEGADAS CON "~".
     Variables.DELIMITADOR_PARAM_TILDE_SP de Legacy. Lista() las arma aqui, con
     CultureInfo.InvariantCulture: con la cultura del servidor en espanol un
     12.5 se habria escrito "12,5" y el SP, que parte por "~" y luego por nada,
     habria leido un numero distinto.
*/
public static class LegacySp
{
    /// <summary>El separador de listas que esperan estos SP (Legacy: DELIMITADOR_PARAM_TILDE_SP).</summary>
    public const string Delimitador = "~";

    /// <summary>
    /// Un filtro de texto que el SP va a concatenar dentro de un EXEC(@Sql).
    /// Quita el apostrofo y recorta. Nulo o vacio se vuelve "", que es lo que
    /// estos SP entienden por "sin filtro" (comparan IF @X &lt;&gt; '').
    /// </summary>
    public static string Filtro(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return string.Empty;
        return valor.Replace("'", string.Empty).Trim();
    }

    /// <summary>
    /// Un texto que el SP va a GUARDAR con un INSERT/UPDATE parametrizado.
    /// Solo recorta: el apostrofo es parte del nombre de la gente.
    /// </summary>
    public static string Texto(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return string.Empty;
        return valor.Trim();
    }

    /// <summary>
    /// El principio del dia, en el formato que SQL Server lee igual con
    /// cualquier DATEFORMAT. Sin fecha devuelve "" (= sin filtro).
    /// </summary>
    public static string DesdeElDia(DateOnly? fecha)
        => fecha is null ? string.Empty : fecha.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T00:00:00";

    /// <summary>El final del dia (23:59:59). Sin fecha devuelve "".</summary>
    public static string HastaElDia(DateOnly? fecha)
        => fecha is null ? string.Empty : fecha.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T23:59:59";

    /// <summary>Un dia suelto a medianoche, para los SP que reciben una sola fecha de corte.</summary>
    public static string ElDia(DateOnly? fecha)
        => fecha is null ? string.Empty : fecha.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T00:00:00";

    /// <summary>Una fecha para un parametro datetime de verdad. Sin fecha, NULL.</summary>
    public static object FechaODbNull(DateOnly? fecha)
        => fecha is null ? DBNull.Value : fecha.Value.ToDateTime(TimeOnly.MinValue);

    /// <summary>Lista de enteros pegada con "~". Vacia o nula devuelve "".</summary>
    public static string Lista(IEnumerable<int>? valores)
    {
        if (valores is null) return string.Empty;
        return string.Join(Delimitador, valores.Select(v => v.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>
    /// Lista de decimales pegada con "~", con punto decimal invariante.
    /// </summary>
    public static string Lista(IEnumerable<decimal>? valores)
    {
        if (valores is null) return string.Empty;
        return string.Join(Delimitador, valores.Select(v => v.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>
    /// El renglon con el que estos SP contestan una escritura:
    /// SELECT &lt;id&gt; AS result, '' AS mensaje en el camino bueno y
    /// SELECT -1 AS result, 'ERROR' AS mensaje en el malo
    /// (leido de sp_n_Insertar_MCPuesto y sp_n_Actualizar_MCContratoGrupo).
    ///
    /// Si el SP no devuelve nada se contesta (0, "") y no se revienta: hay
    /// procedimientos de esta familia que no emiten el renglon cuando salen por
    /// un RETURN temprano, y un 500 ahi tapa la unica pista que queda.
    /// </summary>
    public static ResultadoSp LeerResultado(DataTable? dt)
    {
        if (dt is null || dt.Rows.Count == 0) return new ResultadoSp(0, string.Empty);

        var fila = dt.Rows[0];
        var result = 0;
        if (dt.Columns.Contains("result") && fila["result"] != DBNull.Value)
            int.TryParse(Convert.ToString(fila["result"], CultureInfo.InvariantCulture), out result);

        var mensaje = dt.Columns.Contains("mensaje") && fila["mensaje"] != DBNull.Value
            ? Convert.ToString(fila["mensaje"], CultureInfo.InvariantCulture) ?? string.Empty
            : string.Empty;

        return new ResultadoSp(result, mensaje.Trim());
    }

    /*
      POR QUE ESTAS LECTURAS DEVUELVEN EL RENGLON ENTERO Y NO UN DTO.

      De los seis procedimientos de consulta que trae mi lote, CUATRO no estan
      instalados en Produccion_svr (sp_n_Consultar_MCPartidas,
      ...MCContratosPartidas, ...MCContratosPeriodos, ...MCConvenios): su lista
      de columnas, que es el contrato, no se puede leer de esta base. Declarar
      un DTO a ojo publicaria campos que quiza no existen, y un campo que nunca
      trae dato es peor que un campo que no esta — el front le hace sitio en la
      pantalla y queda en blanco para siempre.

      Asi que se devuelve el renglon tal como lo manda el SP, en camelCase, con
      DataTableToRows (que respeta los NULL y no se come las columnas que el DTO
      no conocia). Cuando esas cuatro bases esten disponibles se puede tipar.

      Lo que si se quita, porque de eso si hay certeza:

      * Las columnas *Ftm. El SP manda cada numero y cada fecha DOS veces: el
        valor y su texto ya formateado (FechaInicio y FechaInicioFtm, con
        FORMAT(..., 'dd-MM-yyyy') dentro de la base). Eso lo armaba la base para
        las rejillas de WinForms; un front que recibe el valor lo formatea con
        la configuracion de quien mira la pantalla, y uno que recibe el texto ya
        no puede. Misma decision que la ola 3.
      * result y mensaje. Son el hueco de error de los SP de escritura; en una
        consulta salen 0/null siempre, y el sobre { ok, message } ya dice si la
        llamada salio.

      * LAS COLUMNAS QUE EL SP MANDA DOS VECES.
        sp_n_ConsultarGasolinas tiene esto en su SELECT (leido de la base):

            G.Fecha, FORMAT(...) AS FechaFtm, G.Equipo, Usu.Usuario, G.IDUsuario, G.Equipo,
            G.FechaModificacion, FORMAT(...) AS FechaModificacionFtm, G.IDUsuarioModificacion, G.FechaModificacion, ...
            G.FechaCancelacion, FORMAT(...) AS FechaCancelacionFtm, G.IDUsuarioCancelacion, G.FechaCancelacion, ...

        Equipo, FechaModificacion y FechaCancelacion salen DOS VECES cada una: un
        copiar y pegar dentro del procedimiento. Legacy no lo notaba porque su DTO
        declaraba cada nombre una sola vez y se quedaba con la primera; aqui el
        DataTable renombra la segunda a "Equipo1" y saldria al cliente un campo
        llamado `equipo1` que no significa nada.

        Se quita la copia, y solo cuando de verdad es una copia: el nombre es
        <algo><digito> y el valor de ese renglon es IDENTICO al de <algo>. Asi una
        columna que legitimamente se llame "Partida2" no se cae.
    */
    public static List<Dictionary<string, object?>> Renglones(DataTable dt)
    {
        var filas = Utils.Funciones.DataTableToRows(dt);
        foreach (var fila in filas)
        {
            var sobran = fila.Keys
                .Where(k => k.EndsWith("Ftm", StringComparison.Ordinal)
                            || string.Equals(k, "result", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(k, "mensaje", StringComparison.OrdinalIgnoreCase)
                            || EsCopiaDeOtraColumna(fila, k))
                .ToList();
            foreach (var k in sobran) fila.Remove(k);
        }
        return filas;
    }

    private static bool EsCopiaDeOtraColumna(Dictionary<string, object?> fila, string clave)
    {
        var i = clave.Length;
        while (i > 0 && char.IsDigit(clave[i - 1])) i--;
        if (i == clave.Length || i == 0) return false;   // no acaba en digito

        var original = clave[..i];
        if (!fila.TryGetValue(original, out var valorOriginal)) return false;

        var valorCopia = fila[clave];
        if (valorOriginal is null && valorCopia is null) return true;
        return valorOriginal is not null && valorOriginal.Equals(valorCopia);
    }
}

/// <summary>
/// Lo que contesta un SP de escritura de obra / aviacion.
/// Result: el id nuevo (altas) o 1 (modificaciones); -1 es error del SP.
/// </summary>
public sealed record ResultadoSp(int Result, string Mensaje);
