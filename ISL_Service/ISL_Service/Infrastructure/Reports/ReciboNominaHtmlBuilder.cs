using System.Data;
using System.Globalization;

namespace ISL_Service.Infrastructure.Reports;

/*
  ══════════════════════════════════════════════════════════════════════
  RECIBOS DE NOMINA (el boton F6 de ASISTENCIAS Y RECIBOS)
  ══════════════════════════════════════════════════════════════════════

  ESTE ES EL OTRO QUE LA NOTA DABA POR INALCANZABLE.

  Lo que decia era verdad: el id 1020 NO TIENE RENGLON en `dbo.Reportes` —
  comprobado otra vez en MacZ y en Produccion_svr: de 1020, 1024, 1025, 1026 y
  1027 la tabla solo devuelve los cuatro ultimos—, asi que el motor generico de
  reportes, que arranca leyendo `spV2` de esa tabla, no lo puede alcanzar.

  La conclusion que se saco de ahi —"el recibo sigue saliendo de MacReportes"— es
  la que estaba mal. El recibo NO lo arma MacReportes: MacReportes solo pide un
  HTML ya hecho y lo pasa por HtmlToPdf
  (MacReportes2/.../EmpleadosTipoSueldoRecibosController.cs:69-76). Quien lo arma
  es MacServicios2/.../EmpleadoTipoSueldoReciboService.findAsync, y todo lo que
  usa esta aqui:

    · Template_Html, IDTipoTemplateHtml = 1020, con sus cuatro filas
      (`cuerpo_nomina`, `detalle_nomina`, `cuerpo_comision`, `detalle_comision`).
      Las cuatro existen en las dos bases.
    · sp_n_ConsultaEmpleadosTipoSueldoRecibo (Tauro) /
      sp_n_ConsultaEmpleadosTipoSueldoReciboZara (Zaragoza), con el renglon de
      parametros y el IDPeriodoTipoSueldo.
    · sp_n_ConsultaPeriodosTipoSueldo para el periodo.

  Asi que el recibo se arma aqui, con las mismas plantillas y la misma base. No
  hace falta que el id tenga renglon en `dbo.Reportes` porque este camino no pasa
  por `spV2`: se le dice que procedimiento es.

  CUANTOS RECIBOS CABEN EN UNA HOJA LO DECIDE LA PLANTILLA, NO NOSOTROS.

  Legacy tiene dos armadores y es por esto: `cuerpo_nomina` de Produccion_svr
  tiene cuatro huecos (#Detalle1#..#Detalle4#) y el de MacZ solo uno —los otros
  tres estan comentados dentro del propio HTML—. Con cuatro huecos legacy avanza
  de cuatro en cuatro (GeneraHtmlTauro); con uno, de uno en uno
  (GeneraHtmlZara), porque si avanzara de cuatro perderia tres empleados por
  hoja sin avisar. Aqui se cuentan los huecos de la plantilla en vez de
  preguntar que empresa es: es la misma decision, pero leida de donde de verdad
  viene.

  EL PASE DE NOMINA Y EL PASE DE COMISION.

  Legacy pide el recibo DOS veces: con el periodo pedido (nomina) y con
  periodo + 1 (comision, que es el periodo gemelo de tipo de sueldo comision), y
  concatena los dos papeles. El pase de nomina legacy lo encierra en
  `if (AppMac.Contains("MACT"))`, o sea que en Zaragoza no sale; aqui se respeta
  ese condicional porque lo que decide no es un gusto nuestro, es que papel se
  entrega hoy en cada empresa.
*/
internal static class ReciboNominaHtmlBuilder
{
    /// <summary>
    /// Arma el HTML de los recibos de nomina. <paramref name="esTauro"/> decide
    /// si sale tambien el pase de nomina, igual que el AppMac de legacy.
    /// </summary>
    internal static string Construir(
        string logo,
        DataTable templates,
        DataTable periodoNomina,
        DataTable recibosNomina,
        DataTable periodoComision,
        DataTable recibosComision,
        bool esTauro)
    {
        var cuerpoNomina = LeerPlantilla(templates, "cuerpo_nomina");
        var detalleNomina = LeerPlantilla(templates, "detalle_nomina");
        var detalleComision = LeerPlantilla(templates, "detalle_comision");

        var html = "";

        if (esTauro && recibosNomina.Rows.Count > 0)
            html += ArmarPase(cuerpoNomina, detalleNomina, periodoNomina, recibosNomina);

        if (recibosComision.Rows.Count > 0)
            html += ArmarPase(cuerpoNomina, detalleComision, periodoComision, recibosComision);

        if (html == "")
        {
            /* El mismo aviso que da legacy cuando no hay nada que imprimir, con
               su mismo colspan (findAsync:216-222). No sale una hoja en blanco. */
            return cuerpoNomina
                .Replace("#Detalle1#", "<tr><td colspan='10'><center><h1>No se encontro la información del recibo.</h1></center></td></tr>")
                .Replace("#Detalle2#", "")
                .Replace("#Detalle3#", "")
                .Replace("#Detalle4#", "")
                .Replace("#Logo#", logo);
        }

        return html.Replace("#Logo#", logo);
    }

    private static string ArmarPase(
        string cuerpoTemplate,
        string detalleTemplate,
        DataTable periodo,
        DataTable empleados)
    {
        /*
          LOS TAGS DEL PERIODO SE RESUELVEN UNA SOLA VEZ, SOBRE LA PLANTILLA DE
          DETALLE, antes de recorrer empleados. Es lo que hace legacy
          (GeneraHtmlTauro/Zara, primer foreach) y de ahi salen
          #FechaInicialComFtm#, #NumeroDias#, #numeroSemana# y los demas.
        */
        if (periodo.Rows.Count > 0)
            detalleTemplate = ReemplazarFila(periodo, periodo.Rows[0], detalleTemplate);

        var huecos = ContarHuecos(cuerpoTemplate);
        var html = "";

        for (var i = 0; i < empleados.Rows.Count; i += huecos)
        {
            var hoja = cuerpoTemplate;

            for (var hueco = 1; hueco <= 4; hueco++)
            {
                var indice = i + hueco - 1;
                var detalle = (hueco <= huecos && indice < empleados.Rows.Count)
                    ? ReemplazarFila(empleados, empleados.Rows[indice], detalleTemplate)
                    : "";
                hoja = hoja.Replace("#Detalle" + hueco + "#", detalle);
            }

            html += hoja;
            html += "<p style='page-break-before: always'></p>";
        }

        return html;
    }

    /*
      CUANTOS #DetalleN# ACTIVOS TIENE EL CUERPO.

      Se cuentan solo los que estan FUERA de un comentario HTML: el
      `cuerpo_nomina` de MacZ trae los huecos 2, 3 y 4 escritos pero comentados,
      y contarlos haria exactamente el fallo que GeneraHtmlZara evita —tres
      empleados por hoja que no se imprimen—.
    */
    private static int ContarHuecos(string cuerpo)
    {
        var sinComentarios = QuitarComentarios(cuerpo);
        var huecos = 0;
        for (var i = 1; i <= 4; i++)
        {
            if (sinComentarios.Contains("#Detalle" + i + "#", StringComparison.Ordinal))
                huecos = i;
        }

        return Math.Max(1, huecos);
    }

    private static string QuitarComentarios(string html)
    {
        var resultado = new System.Text.StringBuilder(html.Length);
        var i = 0;
        while (i < html.Length)
        {
            var abre = html.IndexOf("<!--", i, StringComparison.Ordinal);
            if (abre < 0)
            {
                resultado.Append(html, i, html.Length - i);
                break;
            }

            resultado.Append(html, i, abre - i);
            var cierra = html.IndexOf("-->", abre, StringComparison.Ordinal);
            if (cierra < 0) break;
            i = cierra + 3;
        }

        return resultado.ToString();
    }

    private static string ReemplazarFila(DataTable tabla, DataRow fila, string html)
    {
        foreach (DataColumn columna in tabla.Columns)
        {
            var tag = "#" + columna.ColumnName + "#";
            var valor = FormatoFechaLegacy.Celda(fila[columna.ColumnName]);
            if (valor != "")
                valor = FormateaNumero(columna.DataType, valor);

            html = html.Replace(tag, valor);
            html = html.Replace(tag.ToLowerInvariant(), valor);
        }

        return html;
    }

    private static string FormateaNumero(Type tipo, string valor)
    {
        if (tipo == typeof(decimal) || tipo == typeof(double) || tipo == typeof(float))
        {
            return decimal.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out var numero)
                ? numero.ToString("###,##0.00", CultureInfo.InvariantCulture)
                : valor;
        }

        return valor;
    }

    private static string LeerPlantilla(DataTable templates, string descripcion)
    {
        var filas = templates.Select($"descripcion = '{descripcion.Replace("'", "''")}'");
        if (filas.Length == 0)
            throw new InvalidOperationException($"Template HTML '{descripcion}' no encontrado para el recibo de nomina.");
        return Convert.ToString(filas[0]["html"]) ?? "";
    }
}
