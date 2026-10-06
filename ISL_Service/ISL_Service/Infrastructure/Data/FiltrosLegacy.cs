using System.Globalization;

namespace ISL_Service.Infrastructure.Data;

/// <summary>
/// Las tres traducciones que hay que hacerle a un filtro antes de bajarlo a un
/// `sp_n_` de legacy: la fecha, el folio y las listas de ids.
/// </summary>
/// <remarks>
/// Vive aparte y es publica para poder PROBARLA. Son funciones puras y son el
/// sitio exacto donde una migracion descuidada pierde datos sin que salga un
/// error: una fecha mal volteada no truena, devuelve otros renglones.
/// </remarks>
public static class FiltrosLegacy
{
    /*
      LA FECHA QUE ENTIENDE EL PROCEDIMIENTO, SIN EL TRUCO DE LOS SUBSTRING

      Legacy hace esto (Utils/Funciones.cs:137-146 de MacServicios2):

          if (formatoFechaDB == "MM-dd-yyyy")
              return fecha.Substring(3,2) + "-" + fecha.Substring(0,2) + "-" + fecha.Substring(6,4);
          return fecha;

      O sea: lee `formatoFechaDB` de `sp_n_ConsultaConstantes` y, si la base
      espera el mes primero, voltea los dos primeros grupos a fuerza de
      posiciones fijas. Eso obliga a que la pantalla mande SIEMPRE "dd-MM-yyyy"
      con dos digitos: con "1-2-2026" el Substring(3,2) se lleva "2-" y la fecha
      sale corrupta sin que nadie se entere, y con menos de 10 caracteres
      revienta con IndexOutOfRange.

      Aqui se normaliza a **ISO `yyyy-MM-dd HH:mm:ss`**, que SQL Server
      interpreta igual sea cual sea el DATEFORMAT de la sesion o el
      `formatoFechaDB` de Constantes. Por eso este backend NO tiene que leer
      Constantes para filtrar por fecha — que de paso es lo que lo mantiene de
      solo lectura, porque `sp_n_ConsultaConstantes` hace el cierre del dia
      (migracion/CUIDADO-consultas-que-escriben.md).

      LAS HORAS SI SE COPIAN DE LEGACY: " 00:00:00" al inicio del rango y
      " 23:59:59" al final. Sin ellas un rango "hoy a hoy" pierde todo lo
      capturado despues de medianoche.
    */
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

    /*
      EL FOLIO VACIO ES "0", NO "".

      Legacy lo consigue de una forma que cuesta reconocer: le pega un "0" por
      delante al folio (`FolioInicialCobro = "0" + FolioInicialCobro`,
      ConsultaCobrosService.cs:45), asi que "" se vuelve "0" y "123" se vuelve
      "0123". El parametro del procedimiento es numerico, asi que el cero de
      adelante no cambia nada y lo unico que de verdad importa es que el vacio
      llegue como cero.

      Aqui se hace explicito. De paso se arregla un efecto raro de legacy: en
      cobros el campo arranca en "0" por el constructor del DTO y luego le pega
      otro, asi que sin filtro manda "00".
    */
    public static string Folio(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? "0" : valor.Trim();

    /// <summary>
    /// Une ids para los parametros `@IDs...`.
    /// </summary>
    /// <remarks>
    /// El separador por omision es la VIRGULILLA porque es el de toda la familia
    /// legacy (`Globales.DELIMITADOR_PARAM_TILDE_SP` en Mac31) y varios
    /// procedimientos lo traen escrito a mano dentro (`SET @Delimitador = '~'`).
    /// Con coma y DOS O MAS ids el filtro no encuentra nada; con UNO funciona
    /// igual, asi que el error no se nota hasta que alguien marca dos casillas.
    /// Se quitan los no positivos y los repetidos: un 0 en la lista la convierte
    /// en un filtro que no empareja con nada y la consulta sale vacia sin decir
    /// por que.
    /// </remarks>
    public static string Ids(IEnumerable<int>? valores, string? crudo = null, string separador = "~")
    {
        if (!string.IsNullOrWhiteSpace(crudo)) return crudo.Trim();
        if (valores == null) return string.Empty;
        return string.Join(separador, valores.Where(x => x > 0).Distinct());
    }

    /// <summary>Texto para un parametro varchar: nunca null, siempre recortado.</summary>
    public static string Texto(string? valor) => (valor ?? string.Empty).Trim();

    /*
      EL APOSTROFO SE QUITA, Y NO POR GUSTO

      Los `sp_n_Consultar*` arman su WHERE concatenando y lo corren con
      `EXEC(@Sql)`. Pasar el filtro como SqlParameter NO protege, porque el
      procedimiento lo concatena adentro. Legacy ya lo quitaba en garantias
      (ConsultaGarantiasService.cs:69) y se le olvidaba en otras pantallas; aqui
      se quita en todo campo de texto libre que acabe dentro de un EXEC.
    */
    public static string TextoSinApostrofo(string? valor)
        => Texto(valor).Replace("'", string.Empty);
}
