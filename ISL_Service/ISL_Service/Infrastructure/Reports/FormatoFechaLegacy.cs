using System.Globalization;

namespace ISL_Service.Infrastructure.Reports;

/*
  ══════════════════════════════════════════════════════════════════════
  LA FECHA Y LA HORA COMO LAS ESCRIBE MAC31
  ══════════════════════════════════════════════════════════════════════

  Esto lo comparten TODOS los reportes portados de legacy. Si un papel tiene
  que salir igual al de Mac31, la fecha se formatea aqui y no a mano.

  ──────────────────────────────────────────────────────────────────────
  POR QUE HACIA FALTA
  ──────────────────────────────────────────────────────────────────────

  Legacy no formatea: escribe `fila["FechaCobro"].ToString()` y deja que la
  cultura del servidor decida. El servidor de Mac31 es .NET Framework sobre
  Windows, que para es-MX usa las tablas NLS del sistema, y esas dicen:

      fecha corta   dd/MM/yyyy
      hora larga    hh:mm:ss tt      (DOCE horas, con cero delante)
      AM / PM       "a. m." / "p. m." (con espacio entre letras y punto en cada una)

  de donde sale, literal, el `04/05/2026 05:49:14 p. m.` de la hoja de cobro.

  Nosotros corremos .NET 8, que ya no usa NLS sino ICU/CLDR. Y el CLDR de
  es-MX define la hora en VEINTICUATRO horas. El mismo `ToString()`, con la
  misma cultura nominal, imprimia `04/05/2026 17:49:14`. No era un error del
  armador: era el mismo codigo dando otro resultado porque debajo cambio la
  tabla de culturas.

  Por eso no basta con pedir `CultureInfo.GetCultureInfo("es-MX")`: hay que
  FIJAR los patrones. Una cultura congelada aqui tambien nos protege de que la
  maquina donde corre el servicio tenga otra configuracion regional, que es lo
  que de verdad decidia el formato en legacy.

  ──────────────────────────────────────────────────────────────────────
  QUE NO ES
  ──────────────────────────────────────────────────────────────────────

  No sirve para las columnas que la base ya devuelve formateadas (las `*Ftm`:
  `FechaEmisionFtm`, `FechaCobroFtm`…). Esas son varchar armados dentro del
  `sp_n_` y hay que imprimirlas tal cual: meterlas por aqui no haria nada, y
  reformatearlas a mano las separaria de lo que imprime Mac31.
*/
internal static class FormatoFechaLegacy
{
    /*
      La cultura de los reportes. Es es-MX con los patrones de fecha y hora
      forzados a los de .NET Framework/NLS, y se usa tambien para los numeros
      (separador de miles y punto decimal), que ahi ICU y NLS coinciden.
    */
    internal static readonly CultureInfo Cultura = ConstruirCultura();

    private static CultureInfo ConstruirCultura()
    {
        var cultura = (CultureInfo)CultureInfo.GetCultureInfo("es-MX").Clone();
        var f = cultura.DateTimeFormat;

        f.ShortDatePattern = "dd/MM/yyyy";
        f.LongDatePattern = "dddd, d' de 'MMMM' de 'yyyy";
        f.ShortTimePattern = "hh:mm tt";
        f.LongTimePattern = "hh:mm:ss tt";
        /* El patron general (el de `ToString()` a secas, que es lo que usa
           legacy) se arma solo con la fecha corta + la hora larga, pero se deja
           escrito para que se lea de un golpe que esto da `dd/MM/yyyy hh:mm:ss tt`. */
        f.FullDateTimePattern = "dddd, d' de 'MMMM' de 'yyyy hh:mm:ss tt";
        f.AMDesignator = "a. m.";
        f.PMDesignator = "p. m.";

        return CultureInfo.ReadOnly(cultura);
    }

    /* `04/05/2026 05:49:14 p. m.` — lo que legacy obtiene de un `DateTime.ToString()`. */
    internal static string FechaHora(DateTime valor)
    {
        return valor.ToString("G", Cultura);
    }

    /*
      Lo que legacy hace con una celda cualquiera: `fila[columna].ToString()`.

      Si la celda es un DateTime sale con el formato de arriba; si es texto o
      numero sale tal cual, y el nulo sale vacio. Tenerlo en un solo sitio evita
      que cada armador tenga que acordarse de mirar el tipo de la columna.
    */
    internal static string Celda(object? valor)
    {
        if (valor is null || valor == DBNull.Value)
            return "";

        return valor is DateTime fecha
            ? FechaHora(fecha)
            : Convert.ToString(valor, Cultura) ?? "";
    }
}
