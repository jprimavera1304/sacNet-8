using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using ISL_Service.Application.DTOs.Reportes;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Reportes;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Infrastructure.Reports;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

public partial class ReportesVentasRepository
{
    private static async Task<LegacyReportConfig> ConsultarConfiguracionAsync(
        SqlConnection conn,
        int idReporte,
        CancellationToken ct)
    {
        var ds = await ExecuteLegacySpAsync(conn, "sp_ConsultaReportes", idReporte.ToString(), ct);
        if (ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
            throw new InvalidOperationException("Reporte no encontrado en sp_ConsultaReportes.");

        var row = ds.Tables[0].Rows[0];
        var nombre = Convert.ToString(row["NombreReporte"]) ?? "";
        var sp = Convert.ToString(row["SPV2"]) ?? "";
        if (string.IsNullOrWhiteSpace(sp))
            throw new InvalidOperationException("El reporte no tiene SPV2 configurado.");

        /*
          Algunos `sp_n_` de reporte estan ROTOS EN LA BASE y no sacan el papel
          en ninguna empresa —el de Autos pide una columna que no existe—, asi
          que tampoco sale desde Mac31. Cuando hay un gemelo `sp_w_` con el
          defecto corregido, se usa el gemelo.

          No se cambia el `spV2` de la fila de `Reportes` a proposito: esa fila
          la lee tambien Mac31, y cambiarla le cambia el comportamiento a un
          programa que no estamos migrando. Ver ProcedimientosRotosEnLaBase.
        */
        sp = ProcedimientosRotosEnLaBase.Bueno(sp);

        return new LegacyReportConfig
        {
            NombreReporte = nombre,
            StoredProcedure = sp,
            Aspx = Convert.ToString(row["aspx"]) ?? "",
            IDReporteMaster = ReadInt(row, "IDReporteMaster")
        };
    }

    private static async Task<LegacyConstants> ConsultarConstantesAsync(
        SqlConnection conn,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_w_ConsultaConstantesSoloLectura", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };
        cmd.Parameters.AddWithValue("@IDUsuario", 0);

        var ds = await FillDataSetAsync(cmd, ct);
        if (ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
            return new LegacyConstants();

        var row = ds.Tables[0].Rows[0];
        var pathImagenes = Convert.ToString(row["PathImagenes"]) ?? "";
        return new LegacyConstants
        {
            FechaOperacion = DateTime.TryParse(ReadString(row, "FechaOperacion"), out var fechaOperacion)
                ? fechaOperacion.ToString("yyyy-MM-dd")
                : DateTime.Today.ToString("yyyy-MM-dd"),
            Logo = pathImagenes + (Convert.ToString(row["LogoMacReportes"]) ?? ""),
            LogoWatermark = pathImagenes + (Convert.ToString(row["LogoMacWM"]) ?? ""),
            IDDescuentoCompra = ReadInt(row, "IDDescuentoCompra"),
            IDDescuentoListaPrecios = ReadInt(row, "IDDescuentoListaPrecios"),
            IDEmpresaCS = ReadInt(row, "IDEmpresaCS"),
            Funcionalidad = ReadString(row, "Funcionalidad"),
            EsCentroServicio = ReadInt(row, "EsCentroServicio")
        };
    }

    private static string ResolveLogoForHtml(string logo)
    {
        var trimmed = (logo ?? "").Trim();
        if (trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        if (!string.IsNullOrWhiteSpace(trimmed))
        {
            var localLogo = TryImageFileToDataUri(trimmed);
            if (!string.IsNullOrWhiteSpace(localLogo))
                return localLogo;
        }

        return FallbackLogoDataUri.Value;
    }

    private static string? TryImageFileToDataUri(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            var extension = Path.GetExtension(path).ToLowerInvariant();
            var mimeType = extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".svg" => "image/svg+xml",
                ".webp" => "image/webp",
                _ => "image/png"
            };
            return $"data:{mimeType};base64,{Convert.ToBase64String(File.ReadAllBytes(path))}";
        }
        catch
        {
            return null;
        }
    }

    private static async Task<DataTable> ConsultarTemplatesAsync(
        SqlConnection conn,
        int idTipoTemplateHtml,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaTemplateHtml", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };
        cmd.Parameters.AddWithValue("@IDTemplateHtml", 0);
        cmd.Parameters.AddWithValue("@IDTipoTemplateHtml", idTipoTemplateHtml);
        cmd.Parameters.AddWithValue("@IDAplicacion", 1);
        cmd.Parameters.AddWithValue("@IDStatus", 1);

        var ds = await FillDataSetAsync(cmd, ct);
        if (ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
            throw new InvalidOperationException("No se encontraron templates HTML para el reporte.");

        return ds.Tables[0];
    }

    private static LegacyTemplate BuildTemplateInfo(DataTable templates)
    {
        var row = templates.Rows[0];
        return new LegacyTemplate
        {
            Orientacion = Convert.ToString(row["orientacion"]) ?? "Landscape",
            Ancho = ReadInt(row, "ancho"),
            Alto = ReadInt(row, "alto"),
            MarginTop = ReadInt(row, "marginTop"),
            MarginBottom = ReadInt(row, "marginBottom"),
            MarginLeft = ReadInt(row, "marginLeft"),
            MarginRight = ReadInt(row, "marginRight"),
            MaxLineas = Math.Max(1, ReadInt(row, "maxLineas"))
        };
    }

    private static string GenerateFormatoNormalHtml(
        string logo,
        string logoWatermark,
        string reporteNombre,
        DataSet data,
        DataTable templates,
        LegacyTemplate template,
        ReportesVentasAcumuladoresProductosRequest request)
    {
        var table = data.Tables.Count > 0 ? data.Tables[0] : new DataTable();
        if (table.Rows.Count == 0)
            return GenerateEmptyFormatoNormalHtml(logo, templates, reporteNombre, request);

        var cuerpoTemplate = LeerCuerpoDeLaPlantilla(templates);
        var detalleTemplate = ReadTemplateHtml(templates, "detalle");
        var totalesTemplate = ReadTemplateHtml(templates, "totales");
        var htmlFinal = "";

        var groupColumn = ResolveAgentGroupColumn(table, request);
        var groups = groupColumn is null
            ? new[] { "" }
            : table.AsEnumerable()
                .Select(row => Convert.ToString(row[groupColumn]) ?? "")
                .Distinct()
                .ToArray();

        foreach (var group in groups)
        {
            var rows = groupColumn is null
                ? table.AsEnumerable().ToArray()
                : table.AsEnumerable()
                    .Where(row => string.Equals(Convert.ToString(row[groupColumn]) ?? "", group, StringComparison.Ordinal))
                    .ToArray();
            if (rows.Length == 0) continue;

            var totalPaginas = rows.Length / template.MaxLineas;
            if (rows.Length % template.MaxLineas != 0) totalPaginas++;

            var numPagina = 1;
            var lineas = 0;
            var numRegistro = 0;
            var htmlCuerpo = "";
            var htmlDetalle = "";
            var registroTotales = false;

            foreach (var row in rows)
            {
                numRegistro++;

                if (lineas == 0)
                {
                    /*
                      EL ENCABEZADO SE LLENA SIEMPRE CON EL PRIMER RENGLON, NO CON
                      EL QUE ABRE LA HOJA.

                      Legacy toma `DataRow dr = dtDetalle.Rows[0]` UNA vez, fuera
                      de cualquier cuenta de paginas, y con ese renglon resuelve
                      todos los tags del cuerpo (RepFormatoNormalService.cs, dentro
                      de EncabezadoHtml). Nosotros le pasabamos `row`, el renglon
                      con el que empieza la hoja: en la hoja 1 da igual, y de la
                      hoja 2 en adelante el encabezado salia con los datos de otro
                      registro. En el 1260 (maxLineas 2) y en el 1345 (23) eso pasa
                      enseguida.

                      El unico tag que legacy SI saca del renglon en curso es
                      #topWM#, la altura de la marca de agua, y por eso van dos
                      renglones a la funcion y no uno.
                    */
                    htmlCuerpo = "<p style='page-break-before: always'></p>";
                    htmlCuerpo += BuildFormatoNormalHeader(cuerpoTemplate, logo, reporteNombre, numPagina, totalPaginas, table, rows[0], row, request);
                }

                htmlDetalle += ReplaceValues(table, row, detalleTemplate);
                lineas++;

                if (lineas >= template.MaxLineas)
                {
                    if (numRegistro == rows.Length)
                    {
                        htmlCuerpo = htmlCuerpo.Replace("#Totales#", ReplaceValues(table, rows[0], totalesTemplate, true));
                        registroTotales = true;
                    }

                    lineas = 0;
                    numPagina++;
                    htmlCuerpo = htmlCuerpo.Replace("#Detalle#", htmlDetalle);
                    htmlCuerpo = htmlCuerpo.Replace("#Totales#", "");
                    htmlFinal += htmlCuerpo;
                    htmlDetalle = "";
                }
            }

            if (!registroTotales)
            {
                htmlCuerpo = htmlCuerpo.Replace("#Detalle#", htmlDetalle);
                htmlCuerpo = htmlCuerpo.Replace("#Totales#", ReplaceValues(table, rows[0], totalesTemplate, true));
                htmlFinal += htmlCuerpo;
            }

            htmlFinal += "<p style='page-break-before: always'></p>";
            htmlFinal = htmlFinal.Replace("#LogoWM#", logoWatermark);
        }

        return htmlFinal;
    }

    private static string GenerateFormatoColumnas3Html(
        string logo,
        string reporteNombre,
        DataSet data,
        DataTable templates,
        LegacyTemplate template,
        ReportesVentasAcumuladoresProductosRequest request)
    {
        var table = data.Tables.Count > 0 ? data.Tables[0] : new DataTable();
        if (table.Rows.Count == 0)
            return GenerateEmptyColumnas3Html(logo, templates, reporteNombre, request);

        var cuerpoTemplate = LeerCuerpoDeLaPlantilla(templates);
        var detalle1Template = ReadTemplateHtml(templates, "detalle1");
        var detalle2Template = ReadTemplateHtml(templates, "detalle2");
        var detalle3Template = ReadTemplateHtml(templates, "detalle3");
        var totalesTemplate = ReadTemplateHtml(templates, "totales");
        var htmlFinal = "";

        var groupColumn = ResolveAgentGroupColumn(table, request);
        var groups = groupColumn is null
            ? new[] { "" }
            : table.AsEnumerable()
                .Select(row => Convert.ToString(row[groupColumn]) ?? "")
                .Distinct()
                .ToArray();

        foreach (var group in groups)
        {
            var rows = groupColumn is null
                ? table.AsEnumerable().ToArray()
                : table.AsEnumerable()
                    .Where(row => string.Equals(Convert.ToString(row[groupColumn]) ?? "", group, StringComparison.Ordinal))
                    .ToArray();
            if (rows.Length == 0) continue;

            var totalPaginas = rows.Length / template.MaxLineas;
            if (rows.Length % template.MaxLineas != 0) totalPaginas++;

            var numPagina = 1;
            var lineas = 0;
            var numRegistro = 0;
            var htmlCuerpo = "";
            var htmlDetalle1 = "";
            var htmlDetalle2 = "";
            var htmlDetalle3 = "";
            var registroTotales = false;

            foreach (var row in rows)
            {
                numRegistro++;

                if (lineas == 0)
                {
                    htmlCuerpo = "<p style='page-break-before: always'></p>";
                    htmlCuerpo += BuildColumnas3Header(cuerpoTemplate, logo, reporteNombre, numPagina, totalPaginas, table, row, request);
                }

                htmlDetalle1 += ReplaceValues(table, row, detalle1Template);
                htmlDetalle2 += ReplaceValues(table, row, detalle2Template);
                htmlDetalle3 += ReplaceValues(table, row, detalle3Template);
                lineas++;

                if (lineas >= template.MaxLineas)
                {
                    if (numRegistro == rows.Length)
                    {
                        htmlCuerpo += ReplaceValues(table, rows[0], totalesTemplate, true);
                        registroTotales = true;
                    }

                    lineas = 0;
                    numPagina++;
                    htmlCuerpo = htmlCuerpo.Replace("#Detalle1#", htmlDetalle1);
                    htmlCuerpo = htmlCuerpo.Replace("#Detalle2#", htmlDetalle2);
                    htmlCuerpo = htmlCuerpo.Replace("#Detalle3#", htmlDetalle3);
                    htmlFinal += htmlCuerpo;
                    htmlDetalle1 = "";
                    htmlDetalle2 = "";
                    htmlDetalle3 = "";
                }
            }

            if (!registroTotales)
            {
                htmlCuerpo += ReplaceValues(table, rows[0], totalesTemplate, true);
                htmlCuerpo = htmlCuerpo.Replace("#Detalle1#", htmlDetalle1);
                htmlCuerpo = htmlCuerpo.Replace("#Detalle2#", htmlDetalle2);
                htmlCuerpo = htmlCuerpo.Replace("#Detalle3#", htmlDetalle3);
                htmlFinal += htmlCuerpo;
            }

            htmlFinal += "<p style='page-break-before: always'></p>";
        }

        return htmlFinal;
    }

    private static string GenerateListasPreciosZaragozaHtml(
        string logo,
        string reporteNombre,
        DataSet data,
        DataTable templates,
        ReportesVentasAcumuladoresProductosRequest request)
    {
        var table = data.Tables.Count > 0 ? data.Tables[0] : new DataTable();
        if (table.Rows.Count == 0)
            return GenerateEmptyFormatoNormalHtml(logo, templates, reporteNombre, request);

        var cuerpoTemplate = LeerCuerpoDeLaPlantilla(templates);
        var detalleTemplate = ReadTemplateHtml(templates, "detalle");
        var detalleMotoTemplate = ReadTemplateHtml(templates, "detalleMoto");

        var html = ReplaceValues(table, table.Rows[0], cuerpoTemplate);
        html = html.Replace("#Logo#", logo);
        html = html.Replace("#Titulo#", reporteNombre);

        var ordenes = new (int Orden, string Tag, string Template)[]
        {
            (1, "#DetalleLTH#", detalleTemplate),
            (2, "#DetalleAME#", detalleTemplate),
            (3, "#DetalleFP#", detalleTemplate),
            (4, "#DetalleDiener#", detalleTemplate),
            (5, "#DetalleOptima#", detalleTemplate),
            (6, "#DetalleAGM#", detalleTemplate),
            (20, "#DetalleEvolutionAux#", detalleTemplate),
            (21, "#DetalleProtect#", detalleTemplate),
            (7, "#DetalleHeavyDuty#", detalleTemplate),
            (8, "#DetalleEspeciales#", detalleTemplate),
            (9, "#DetalleCale#", detalleTemplate),
            (10, "#DetalleHiTec#", detalleTemplate),
            (11, "#DetalleDynamo#", detalleTemplate),
            (100, "#DetalleCascos#", detalleTemplate),
            (15, "#DetalleMotoLTH#", detalleMotoTemplate),
            (16, "#DetalleMotoAME#", detalleMotoTemplate)
        };

        foreach (var item in ordenes)
            html = html.Replace(item.Tag, BuildListasPreciosDetalle(table, item.Orden, item.Template));

        return ReplaceRemainingTags(html) + "<p style='page-break-before: always'></p>";
    }

    private static string BuildListasPreciosDetalle(DataTable table, int orden, string detalleTemplate)
    {
        var rows = table.AsEnumerable()
            .Where(row => ReadInt(row, "Orden") == orden)
            .ToArray();

        if (rows.Length == 0)
            return "";

        return string.Concat(rows.Select(row => ReplaceValues(table, row, detalleTemplate)));
    }

    private static string GenerateEmptyColumnas3Html(
        string logo,
        DataTable templates,
        string reporteNombre,
        ReportesVentasAcumuladoresProductosRequest request)
    {
        var html = BuildColumnas3Header(LeerCuerpoDeLaPlantilla(templates), logo, reporteNombre, 1, 1, null, null, request);
        var mensaje = "<tr><td colspan='16' align='center'><br /><br /> <u> <h1>No se encontro información</h1> </u></td></tr>";
        html = html.Replace("#Detalle1#", mensaje);
        html = html.Replace("#Detalle2#", "");
        html = html.Replace("#Detalle3#", "");
        html = html.Replace("#Totales#", "");
        return ReplaceRemainingTags(html);
    }

    private static string BuildColumnas3Header(
        string cuerpoTemplate,
        string logo,
        string reporteNombre,
        int numPagina,
        int totalPaginas,
        DataTable? table,
        DataRow? row,
        ReportesVentasAcumuladoresProductosRequest request)
    {
        var html = cuerpoTemplate;
        html = html.Replace("#Logo#", logo);
        html = html.Replace("#Titulo#", reporteNombre);
        html = html.Replace("#NumPagina#", numPagina.ToString());
        html = html.Replace("#TotalPaginas#", totalPaginas.ToString());

        if (table is null || row is null)
        {
            html = html.Replace("#almacen#", "----");
            html = html.Replace("#categorias#", "----");
            html = html.Replace("#Agente#", "");
            html = html.Replace("#agente#", "");
            html = html.Replace("#Cliente#", "");
            html = html.Replace("#cliente#", "");
            html = html.Replace("#fechaInicial#", request.FechaInicial.ToString("dd/MM/yyyy"));
            html = html.Replace("#fechaFinal#", request.FechaFinal.ToString("dd/MM/yyyy"));
            html = html.Replace("#ParametrosTexto#", "");
            html = html.Replace("#fechaImpresion#", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
            return html;
        }

        html = ReplaceAgentHeader(html, table, row, request);
        html = ReplaceClientHeader(html, table, row);
        return ReplaceValues(table, row, html);
    }

    private static string GenerateEmptyFormatoNormalHtml(
        string logo,
        DataTable templates,
        string reporteNombre,
        ReportesVentasAcumuladoresProductosRequest request)
    {
        var html = BuildEmptyHeader(LeerCuerpoDeLaPlantilla(templates), logo, reporteNombre, request);
        var mensaje = "<tr><td colspan='16' align='center'><br /><br /> <u> <h1>No se encontro información</h1> </u></td></tr>";
        html = html.Replace("#Detalle1#", mensaje);
        html = html.Replace("#Detalle#", mensaje);
        html = html.Replace("#Totales#", "");
        return ReplaceRemainingTags(html);
    }

    private static string GenerateSinInformacionHtml(
        string logo,
        DataTable templates,
        string reporteNombre,
        ReportesVentasAcumuladoresProductosRequest request,
        string mensaje)
    {
        var html = BuildEmptyHeader(LeerCuerpoDeLaPlantilla(templates), logo, reporteNombre, request);
        var detail = "<tr><td colspan='16' align='center'><br /><br /> <u> <h1>" + mensaje + "</h1> </u></td></tr>";
        html = html.Replace("#Detalle1#", detail);
        html = html.Replace("#Detalle#", detail);
        html = html.Replace("#Detalle2#", "");
        html = html.Replace("#Detalle3#", "");
        html = html.Replace("#Totales#", "");
        return ReplaceRemainingTags(html);
    }

    private static string BuildEmptyHeader(
        string cuerpoTemplate,
        string logo,
        string reporteNombre,
        ReportesVentasAcumuladoresProductosRequest request)
    {
        var html = cuerpoTemplate;
        html = html.Replace("#Logo#", logo);
        html = html.Replace("#Titulo#", reporteNombre);
        html = html.Replace("#NumPagina#", "1");
        html = html.Replace("#TotalPaginas#", "1");
        html = html.Replace("#Agente#", "");
        html = html.Replace("#agente#", "");
        html = html.Replace("#Cliente#", "");
        html = html.Replace("#cliente#", "");
        html = html.Replace("#Totales#", "");
        html = html.Replace("#FormatoTexto#", "");
        html = html.Replace("#ParametrosTexto#", "");
        html = html.Replace("#fechaInicial#", request.FechaInicial.ToString("dd/MM/yyyy"));
        html = html.Replace("#fechaFinal#", request.FechaFinal.ToString("dd/MM/yyyy"));
        html = html.Replace("#FechaInicial#", request.FechaInicial.ToString("dd/MM/yyyy"));
        html = html.Replace("#FechaFinal#", request.FechaFinal.ToString("dd/MM/yyyy"));
        html = html.Replace("#fechaImpresion#", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
        return html;
    }

    /// <param name="filaDeLosValores">
    /// El PRIMER renglon del grupo: de ahi salen todos los tags del cuerpo, como
    /// en legacy. Null solo en el papel sin informacion.
    /// </param>
    /// <param name="filaEnCurso">
    /// El renglon con el que empieza esta hoja. Legacy lo usa unicamente para
    /// #topWM#.
    /// </param>
    private static string BuildFormatoNormalHeader(
        string cuerpoTemplate,
        string logo,
        string reporteNombre,
        int numPagina,
        int totalPaginas,
        DataTable? table,
        DataRow? filaDeLosValores,
        DataRow? filaEnCurso,
        ReportesVentasAcumuladoresProductosRequest request)
    {
        var html = cuerpoTemplate;
        html = html.Replace("#Logo#", logo);
        html = html.Replace("#Titulo#", reporteNombre);
        html = html.Replace("#NumPagina#", numPagina.ToString());
        html = html.Replace("#TotalPaginas#", totalPaginas.ToString());

        if (table is null || filaDeLosValores is null)
        {
            html = html.Replace("#Agente#", "");
            html = html.Replace("#agente#", "");
            html = html.Replace("#Cliente#", "");
            html = html.Replace("#cliente#", "");
            html = html.Replace("#Totales#", "");
            html = html.Replace("#FormatoTexto#", "");
            html = html.Replace("#ParametrosTexto#", "");
            html = html.Replace("#fechaImpresion#", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
            return ReplaceRemainingTags(html);
        }

        if (table.Columns.Contains("topWM") && filaEnCurso is not null)
            html = html.Replace("#topWM#", FormatoFechaLegacy.Celda(filaEnCurso["topWM"]));

        html = ReplaceAgentHeader(html, table, filaDeLosValores, request);
        html = ReplaceClientHeader(html, table, filaDeLosValores);
        return ReemplazarValoresDelEncabezado(table, filaDeLosValores, html);
    }

    /*
      EL ENCABEZADO NO REEMPLAZA IGUAL QUE EL DETALLE, Y LA DIFERENCIA SE VE.

      Legacy tiene DOS rutinas de sustitucion y no son la misma:

        * el detalle y los totales pasan por `Funciones.ReemplazaValores`, que
          SALTA el formateo cuando la celda viene vacia y ademas repite el
          reemplazo con el tag en minusculas;
        * el cuerpo lo arma `EncabezadoHtml`, que llama a `FormateaNumero`
          SIEMPRE -tambien con la celda vacia- y NO prueba el tag en minusculas.

      Esa llamada sin guarda es la que hace que un entero nulo salga "0" y un
      decimal nulo "0.00" en el encabezado, mientras en el detalle los mismos
      nulos salen en blanco. Parece un descuido suyo, pero es lo que esta
      impreso en el papel que la gente firma, asi que se copia tal cual.
    */
    private static string ReemplazarValoresDelEncabezado(DataTable table, DataRow row, string html)
    {
        foreach (DataColumn column in table.Columns)
        {
            var valor = FormatLegacyNumber(column.DataType, FormatoFechaLegacy.Celda(row[column.ColumnName]), false);
            html = html.Replace("#" + column.ColumnName + "#", valor);
        }

        return html;
    }

    /*
      EL CUERPO SE LLAMA "cuerpo_n" EN CASI TODOS Y "cuerpo" EN DOS.

      `Template_Html` guarda tres piezas por reporte -el cuerpo, el detalle y los
      totales- y el cuerpo se llama `cuerpo_n` en toda la tabla menos en dos
      filas. Contado en MacZ:

          1024  cuerpo | detalle | totales
          1025  cuerpo | detalle | totales
          1190  cuerpo_n | detalle | totales      <- como todos los demas

      Los dos de la excepcion son los reportes de asistencias que salen de
      `ReporteEmpleadoAsistenciaRecibos`, y es justo por eso que Legacy los trata
      aparte en ReporteV2Service.cs:860 en vez de mandarlos por el camino comun.

      Pidiendo solo `cuerpo_n` el motor moria con "Template HTML 'cuerpo_n' no
      encontrado", que es un 500 mudo y suena a que falta una fila cuando lo que
      cambia es el nombre. Se busca el alternativo y SOLO ese: si no esta ninguno
      de los dos, se sigue fallando con el mensaje de siempre, porque entonces el
      reporte de verdad no tiene plantilla y callarlo imprimiria un papel en
      blanco.
    */
    private static string LeerCuerpoDeLaPlantilla(DataTable templates)
    {
        var rows = templates.Select("descripcion = 'cuerpo_n'");
        if (rows.Length > 0)
            return Convert.ToString(rows[0]["html"]) ?? "";

        return ReadTemplateHtml(templates, "cuerpo");
    }

    private static string ReadTemplateHtml(DataTable templates, string descripcion)
    {
        var rows = templates.Select($"descripcion = '{descripcion.Replace("'", "''")}'");
        if (rows.Length == 0)
            throw new InvalidOperationException($"Template HTML '{descripcion}' no encontrado.");
        return Convert.ToString(rows[0]["html"]) ?? "";
    }

    private static string ReplaceValues(DataTable table, DataRow row, string html, bool esTotales = false)
    {
        foreach (DataColumn column in table.Columns)
        {
            var tag = "#" + column.ColumnName + "#";
            /* Una celda de fecha sale como la escribe Mac31 ("05:49:14 p. m."),
               no como la escribe .NET 8. Ver FormatoFechaLegacy. */
            var value = FormatoFechaLegacy.Celda(row[column.ColumnName]);
            if (value != "")
                value = FormatLegacyNumber(column.DataType, value, esTotales);

            html = html.Replace(tag, value);
            html = html.Replace(tag.ToLowerInvariant(), value);
        }

        return html;
    }

    /*
      UNA IMAGEN QUE NO SE PUEDE CARGAR SE QUITA DEL HTML.

      wkhtmltopdf 0.12.4 sale con codigo 1 ("network error:
      ContentNotFoundError") cuando un <img> apunta a algo que no existe, y
      entonces NO HAY DOCUMENTO. Las banderas --load-error-handling ignore y
      --load-media-error-handling ignore no cubren este caso (comprobado con el
      exe empaquetado), asi que la unica defensa es no escribir el <img>.

      Pasa de verdad, con dos causas distintas:
      * plantillas con tags de imagen que el motor no conoce: el cuerpo del 1250
        pide #LogoCdMx#, que ReplaceRemainingTags convierte en "----" y
        wkhtmltopdf intenta abrir como archivo;
      * plantillas que apuntan a archivos de la maquina de legacy: el mismo 1250
        pide C:\Mac21\imagenes\GobiernoCDMX.png, que no esta ni empaquetado ni en
        este servidor.

      Se quita solo el <img> que no se puede cargar: el resto del papel sale
      igual. Lo que no se toca es data: ni http(s) — esos los resuelve el
      navegador interno, y si fallan ya solo dejan el hueco.
    */
    private static string SanearImagenesNoCargables(string html)
    {
        if (string.IsNullOrWhiteSpace(html) || html.IndexOf("<img", StringComparison.OrdinalIgnoreCase) < 0)
            return html;

        return Regex.Replace(
            html,
            @"<img\b[^>]*?>",
            match => ImagenSePuedeCargar(match.Value) ? match.Value : "",
            RegexOptions.IgnoreCase);
    }

    private static bool ImagenSePuedeCargar(string imgTag)
    {
        var src = Regex.Match(imgTag, @"src\s*=\s*(?:""(?<v>[^""]*)""|'(?<v>[^']*)'|(?<v>[^\s>]+))", RegexOptions.IgnoreCase)
            .Groups["v"].Value.Trim();

        if (string.IsNullOrEmpty(src))
            return false;
        if (src.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            || src.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || src.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return true;
        // Un tag sin resolver ("#LogoCdMx#") o ya reemplazado por el relleno de
        // ReplaceRemainingTags ("----") no es una ruta: no hay nada que cargar.
        if (src.StartsWith('#') || src.Trim('-').Length == 0)
            return false;

        try
        {
            return File.Exists(src);
        }
        catch
        {
            return false;
        }
    }

    private static string ReplaceRemainingTags(string html)
    {
        html = html.Replace("#ccc", "@@@@");
        while (true)
        {
            var start = html.IndexOf('#');
            if (start == -1) break;
            var end = html.IndexOf('#', start + 1);
            if (end == -1) break;
            var tag = html.Substring(start, end - start + 1);
            html = html.Replace(tag, "----");
        }
        return html.Replace("@@@@", "#ccc");
    }

    private static string FormatLegacyNumber(Type dataType, string value, bool esTotales)
    {
        if (dataType == typeof(int) && value == "") return "0";
        if (dataType == typeof(decimal) && value == "") return "0.00";
        if (value == "-99999") return "0";
        if (value == "-99999.00") return "0.00";
        if (value == "0") return esTotales ? value : "";
        if (value is "0.00" or "0.0000") return esTotales ? value : "";

        var cultura = FormatoFechaLegacy.Cultura;

        if (dataType == typeof(int) && int.TryParse(value, NumberStyles.Any, cultura, out var intValue))
            return intValue.ToString("###,##0", cultura);
        if ((dataType == typeof(float) || dataType == typeof(double))
            && double.TryParse(value, NumberStyles.Any, cultura, out var doubleValue))
            return doubleValue.ToString("###,##0.00", cultura);
        /*
          EL DECIMAL SE TRUNCA A DOS, NO SE REDONDEA.

          Legacy hace `TruncateDecimal(valor, 2)` y luego lo formatea
          (Funciones.cs). Aqui habia un caso aparte para cuando el truncado daba
          cero, y en ese caso formateaba el valor ORIGINAL, que redondea: 0.009
          salia "0.01" donde el papel de Mac31 dice "0.00". Se deja el truncado
          a secas, que es lo que hace legacy para todos los valores.
        */
        if (dataType == typeof(decimal) && decimal.TryParse(value, NumberStyles.Any, cultura, out var decimalValue))
            return (Math.Truncate(decimalValue * 100m) / 100m).ToString("###,##0.00", cultura);

        return value;
    }
}
