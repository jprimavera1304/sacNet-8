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
        /*
          EL RECIBO DE NOMINA NO TIENE RENGLON Y NO LO NECESITA.

          `dbo.Reportes` no tiene el 1020 en ninguna de las dos bases, asi que
          preguntarselo a `sp_ConsultaReportes` solo puede devolver vacio. Su
          configuracion esta escrita aqui porque es FIJA: el nombre que imprime
          Mac31 y el procedimiento que de verdad trae los datos. No se le agrega
          el renglon a la tabla: esa tabla la lee tambien Mac31 y no estamos
          migrando a Mac31.

          Va antes de la consulta para no gastar un viaje a la base que se sabe
          que vuelve vacio.
        */
        if (idReporte == ReporteNominaRecibos)
            return ConfiguracionDeReporteSinRenglon(idReporte);

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

    /*
      LA CONFIGURACION DE LOS REPORTES QUE NO ESTAN EN `dbo.Reportes`.

      Hoy es uno solo, el recibo de nomina. El nombre es el que imprime la
      cabecera del papel y el procedimiento es el que trae los empleados con sus
      percepciones; el `Aspx` dice que armador lo pinta, igual que para los que
      si tienen renglon.
    */
    private static LegacyReportConfig ConfiguracionDeReporteSinRenglon(int idReporte)
    {
        if (idReporte != ReporteNominaRecibos)
            throw new InvalidOperationException("Reporte no encontrado en sp_ConsultaReportes.");

        return new LegacyReportConfig
        {
            NombreReporte = "RECIBOS DE NOMINA",
            StoredProcedure = "sp_n_ConsultaEmpleadosTipoSueldoRecibo",
            Aspx = "recibo_nomina",
            IDReporteMaster = 0
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

    /* ------------------------------------------------------------------ */
    /* LOS DATOS DE NOMINA QUE EL CAMINO GENERICO NO SABE PEDIR            */
    /* ------------------------------------------------------------------ */

    /*
      QUE PROCEDIMIENTO DE RECIBOS LE TOCA A ESTA EMPRESA.

      Legacy elige con `AppMac.Contains("MACT")`
      (EmpleadoTipoSueldoReciboRepository.cs:108-111). Aqui se reusa el `EsTauro`
      que ya tiene este repositorio, que mira `Funcionalidad`: es la otra cara de
      la misma moneda y las dos bases coinciden —MacZ dice AppMac "MacZ.exe" y
      Funcionalidad "ZARA"; Produccion_svr dice "MacT.exe" y "TAU"—.
    */
    private static string SpDeRecibos(bool esTauro)
    {
        return esTauro
            ? "sp_n_ConsultaEmpleadosTipoSueldoRecibo"
            : "sp_n_ConsultaEmpleadosTipoSueldoReciboZara";
    }

    /// <summary>
    /// Los recibos de un periodo. El procedimiento recibe DOS argumentos: el
    /// renglon de parametros y el periodo. Con el periodo en 0 el propio SP toma
    /// el que guarda el renglon (Param3).
    /// </summary>
    private static async Task<DataTable> ConsultarRecibosAsync(
        SqlConnection conn,
        string sp,
        int parametroId,
        int idPeriodoTipoSueldo,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand(sp, conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };
        cmd.Parameters.AddWithValue("@Params", parametroId.ToString(CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@IDPeriodoTipoSueldo", idPeriodoTipoSueldo);

        var ds = await FillDataSetAsync(cmd, ct);
        return ds.Tables.Count > 0 ? ds.Tables[0] : new DataTable();
    }

    /// <summary>Los dias del periodo, de donde salen los siete encabezados.</summary>
    private static async Task<DataTable> ConsultarDiasDelPeriodoAsync(
        SqlConnection conn,
        int idPeriodoTipoSueldo,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaPeriodosTipoSueldoDias", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };
        cmd.Parameters.AddWithValue("@IDPeriodoTipoSueldo", idPeriodoTipoSueldo);
        cmd.Parameters.AddWithValue("@IDPeriodicidad", 0);
        cmd.Parameters.AddWithValue("@IDTipoSueldo", 0);
        cmd.Parameters.AddWithValue("@IDPeriodoStatus", 0);

        var ds = await FillDataSetAsync(cmd, ct);
        return ds.Tables.Count > 0 ? ds.Tables[0] : new DataTable();
    }

    /// <summary>El renglon del periodo, de donde salen sus tags en la plantilla.</summary>
    private static async Task<DataTable> ConsultarPeriodoAsync(
        SqlConnection conn,
        int idPeriodoTipoSueldo,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaPeriodosTipoSueldo", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };
        cmd.Parameters.AddWithValue("@IDPeriodoTipoSueldo", idPeriodoTipoSueldo);
        cmd.Parameters.AddWithValue("@IDPeriodicidad", 0);
        cmd.Parameters.AddWithValue("@IDTipoSueldo", 0);
        cmd.Parameters.AddWithValue("@IDPeriodoStatus", 0);
        cmd.Parameters.AddWithValue("@Orden", 0);

        var ds = await FillDataSetAsync(cmd, ct);
        return ds.Tables.Count > 0 ? ds.Tables[0] : new DataTable();
    }

    /*
      LAS SIETE COLUMNAS DE DIAS POR EMPLEADO.

      Es el procedimiento que la nota decia que no existia. `@IDEmpleado = 0`
      trae todos y se filtra despues por empleado, que es lo que hace legacy
      (sp_n_ConsultaEmpleadosTipoSueldoRecibo lo llama asi, con su comentario
      "Traer las asistencias de todos los empleados y despues filtrar").
      `@IDStatus = 3` es TODOS, como en ReporteAsistenciaService.cs:109.
    */
    private static async Task<DataTable> ConsultarAsistenciasDelPeriodoAsync(
        SqlConnection conn,
        int idPeriodoTipoSueldo,
        CancellationToken ct)
    {
        /*
          VA POR EL GEMELO, IGUAL QUE LA REJILLA DE LA PANTALLA.

          sp_w_ConsultaEmpleadosPeriodoAsistencia es el cuerpo del sp_n_ de
          Zaragoza mas el ISNULL(@diaHorasLaboradas, 0) que le faltaba (el
          porque completo, en AsistenciasRepository.SpRejilla y en
          Querrys/43 Asistencias Zaragoza Totales En Null/).

          Importa que sea el MISMO que la pantalla: el papel de ASISTENCIAS y la
          rejilla de la pantalla salen de aqui, y si uno leyera el sp_n_ y la
          otra el gemelo, el mismo empleado tendria un total en la pantalla y
          otro en la hoja impresa. Eso es peor que el NULL original, porque no
          se ve.
        */
        await using var cmd = new SqlCommand("sp_w_ConsultaEmpleadosPeriodoAsistencia", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };
        cmd.Parameters.AddWithValue("@IDPeriodoTipoSueldo", idPeriodoTipoSueldo);
        cmd.Parameters.AddWithValue("@IDEmpleado", 0);
        cmd.Parameters.AddWithValue("@IDStatus", 3);

        var ds = await FillDataSetAsync(cmd, ct);
        return ds.Tables.Count > 0 ? ds.Tables[0] : new DataTable();
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

    /*
      EN CUANTOS JUEGOS DE PAGINAS SE PARTE EL REPORTE, COMO LO PARTE MAC31.

      Legacy no pregunta si el reporte "es por agente": RepFormatoNormalService y
      RepFormatoColumnas3Service agrupan SIEMPRE por la columna `Agente`
      (RepFormatoNormalService.cs:47, RepFormatoColumnas3Service.cs:58) y sacan un
      juego de paginas COMPLETO por cada valor distinto, con su propia numeracion
      de pagina y sus propios totales. Aqui se agrupaba solo cuando el front
      mandaba tipoReporte = "agente", y ademas por `IDAgente` si esa columna
      existia, que es OTRA columna: dos maneras de partir el papel distintas de
      la de Mac31.

      Medido en MacZ con marzo de 2026: los 17 procedimientos de ventas y
      cobranza que se generaron devuelven `Agente` = '0' en todas las filas
      cuando no se filtra por agente, asi que hoy sale UN solo juego y el papel
      no cambia. Lo que cambia es el dia que el SP devuelva varios agentes:
      entonces se parte como Mac31 y no como una regla nuestra.

      EL ORDEN DENTRO DEL GRUPO ES EL DEL PROCEDIMIENTO, y eso tambien es lo que
      hace legacy aunque use `DataTable.Select`. Ese Select NO conserva el orden
      de la tabla -construye un indice por la columna del filtro-, pero aqui el
      filtro es una IGUALDAD sobre `Agente`: todas las claves del resultado son
      iguales y el desempate de ADO.NET es la posicion original del renglon, o
      sea el orden del SP. Donde eso NO vale es en un filtro que casa valores
      DISTINTOS -el `NOT LIKE` sobre `FolioVentaFtm` de la hoja de cobro 1006, que
      por eso sale alfabetico-, y en este motor no hay ninguno. Comprobado
      renglon por renglon contra el `sp_n_` pelado en 7 reportes (1172, 21, 71,
      1360, 1465, 24 y 1185).

      El orden de los GRUPOS es el de primera aparicion, como el GroupBy de LINQ
      que usa legacy.
    */
    private static IEnumerable<DataRow[]> AgruparComoLegacy(DataTable table)
    {
        if (!table.Columns.Contains("Agente"))
        {
            /* Sin columna `Agente` legacy reventaria -`r["Agente"]` lanza y su
               catch devuelve el mensaje de la excepcion en vez del reporte-. No
               se porta el crash: se saca un solo juego de paginas, que es el
               papel que legacy habria sacado con la columna presente y un valor
               unico. Comprobado en MacZ: los 17 procedimientos de ventas y
               cobranza que se probaron SI la devuelven. */
            yield return table.AsEnumerable().ToArray();
            yield break;
        }

        var grupos = new List<string>();
        var porGrupo = new Dictionary<string, List<DataRow>>(StringComparer.Ordinal);

        foreach (var row in table.AsEnumerable())
        {
            var clave = FormatoFechaLegacy.Celda(row["Agente"]);
            if (!porGrupo.TryGetValue(clave, out var lista))
            {
                lista = new List<DataRow>();
                porGrupo.Add(clave, lista);
                grupos.Add(clave);
            }
            lista.Add(row);
        }

        foreach (var clave in grupos)
            yield return porGrupo[clave].ToArray();
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

        foreach (var rows in AgruparComoLegacy(table))
        {
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

        foreach (var rows in AgruparComoLegacy(table))
        {
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
                    /* El primer renglon del grupo, no el que abre la hoja: el
                       encabezado de 3 columnas lo llena legacy con
                       `dtDetalle.Rows[0]` igual que el del formato normal
                       (RepFormatoColumnas3Service, en su EncabezadoHtml). Este
                       formato no usa #topWM#, asi que no hace falta el segundo
                       renglon. */
                    htmlCuerpo += BuildColumnas3Header(cuerpoTemplate, logo, reporteNombre, numPagina, totalPaginas, table, rows[0], request);
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
            /* "----", como legacy: ver la nota de BuildEmptyHeader. */
            html = html.Replace("#fechaInicial#", "----");
            html = html.Replace("#fechaFinal#", "----");
            html = html.Replace("#ParametrosTexto#", "");
            html = html.Replace("#fechaImpresion#", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
            return html;
        }

        /* Mismo caso que en el formato normal: ver la nota de BuildFormatoNormalHeader. */
        return ReemplazarValoresDelEncabezado(table, row, html);
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
        /*
          EN EL PAPEL VACIO LAS FECHAS NO SE ESCRIBEN, Y ES A PROPOSITO.

          El encabezado sin datos de legacy (RepFormatoNormalService.cs:160-175)
          reemplaza a mano siete tags y manda TODO lo demas al barrido que los
          convierte en "----"; las lineas que pondrian las fechas estan ahi
          COMENTADAS. O sea que el "no se encontro informacion" de Mac31 dice
          "DEL: ---- AL ----" aunque el usuario si haya pedido un rango. Lo
          mismo en el de 3 columnas, que ahi si las pone a rayas de forma
          explicita (RepFormatoColumnas3Service.cs:170-171).

          Aqui se escribian las fechas pedidas. Se lee mejor, pero es un papel
          distinto del de Mac31 y lo que se pidio es que salga igual. Si algun
          dia se decide mejorarlo, se mejora en los dos sitios: estas cuatro
          lineas y las dos de BuildColumnas3Header.
        */
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

        /*
          #Agente# Y #Cliente# NO LLEVAN TRATO ESPECIAL EN EL CUERPO.

          Ni `EncabezadoHtml` de RepFormatoNormalService ni el de
          RepFormatoColumnas3Service saben nada de agentes ni de clientes en el
          camino CON datos: toman `dtDetalle.Rows[0]`, recorren las columnas y
          ya. Solo los ponen en blanco en el papel SIN informacion, que es el
          otro `if` de esta misma funcion.

          Aqui se llamaba a dos ayudantes que escribian "Agente: <b>…</b>" y
          "Cliente: <b>…</b>" -texto que Mac31 nunca imprime- o, cuando el
          reporte no era por agente, dejaban el tag en blanco pisando el valor
          de la columna. Lo que de verdad va en `#Agente#` es el valor de la
          columna `Agente`, que en este motor vale 0 y `FormateaNumero`
          convierte en cadena vacia; el nombre del agente que si se lee en el
          papel sale de `#NombreAgente#`, que es otra columna del `sp_n_`.
        */
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

    /*
      EL FORMATO DE LOS NUMEROS ES EL DE LEGACY, Y VIVE EN UN SOLO SITIO.

      `RemisionFormatoLegacy.FormateaNumero` ya es la copia literal de
      `Funciones.FormateaNumero` (Utils/Funciones.cs:231-276) y es la que imprime
      la remision. Esta era una SEGUNDA copia, y le quedaba una diferencia que
      cambia el papel: formateaba los `Double` -lo que ADO.NET hace de un `float`
      de SQL- como "###,##0.00". Legacy compara el NOMBRE del tipo y solo conoce
      "Int32", "Single" y "Decimal"; un "Double" cae en su `else` y sale TAL
      CUAL, sin separador de miles ni dos decimales. Donde Mac31 escribe
      "1234.5" aqui salia "1,234.50".

      Se delega para que no vuelva a haber dos verdades que se arreglan por
      separado. El try/catch es lo unico que se añade: legacy usa `int.Parse` y
      `decimal.Parse`, que LANZAN, y un listado de miles de renglones no puede
      convertirse en un 500 por una celda rara; con el valor crudo sale lo mismo
      que el `else` de legacy.
    */
    private static string FormatLegacyNumber(Type dataType, string value, bool esTotales)
    {
        try
        {
            return RemisionFormatoLegacy.FormateaNumero(dataType.Name, value, esTotales);
        }
        catch (FormatException)
        {
            return value;
        }
        catch (OverflowException)
        {
            return value;
        }
    }
}
