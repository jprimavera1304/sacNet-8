using System.Data;
using ISL_Service.Application.DTOs.Reportes;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Infrastructure.Reports;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

public partial class ReportesVentasRepository : IReportesVentasRepository
{
    private readonly IConfiguration _configuration;
    private readonly ICentrosServicioRepository _centros;

    public ReportesVentasRepository(IConfiguration configuration, ICentrosServicioRepository centros)
    {
        _configuration = configuration;
        _centros = centros;
    }

    /*
      DE UN IDCentro AL NOMBRE DE SU BASE.

      Devuelve "" para la principal, que es lo que quiere decir "sin centro".
      Si el Id no existe o esta inactivo tambien devuelve "" — no se inventa una
      base ni se truena: se contesta con los datos de la matriz, que es el
      comportamiento de siempre.

      Es el UNICO camino del cliente a un nombre de base: llega un numero, sale
      un nombre de la tabla. Nunca al reves.
    */
    private async Task<string> BaseDeCentroAsync(int idCentro, CancellationToken ct)
    {
        if (idCentro <= 0) return "";
        var centro = await _centros.BuscarAsync(idCentro, ct);
        return centro?.Base ?? "";
    }

    public async Task<ReportesVentasCatalogosResponse> ConsultarCatalogosAcumuladoresProductosAsync(
        int? idGrupoCategoria,
        IReadOnlyCollection<int>? idCategorias,
        CancellationToken ct = default)
    {
        var grupoCategoria = idGrupoCategoria.GetValueOrDefault(GrupoCategoriaAcumuladores);
        if (grupoCategoria <= 0) grupoCategoria = GrupoCategoriaAcumuladores;

        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var constants = await ConsultarConstantesAsync(conn, ct);
        var empresas = await ConsultarEmpresasAsync(conn, ct);
        var almacenes = await ConsultarAlmacenesAsync(conn, ct);
        var agentes = await ConsultarAgentesAsync(conn, ct);
        var proveedores = await ConsultarProveedoresAsync(conn, ct);
        var categorias = await ConsultarGruposCategoriasAsync(conn, ct);
        var subcategorias = await ConsultarSubcategoriasAsync(conn, grupoCategoria, ct);
        var selectedCategorias = NormalizeCatalogIds(idCategorias);
        if (selectedCategorias.Count == 0)
            selectedCategorias = subcategorias.Select(x => x.IDCategoria).Distinct().ToList();
        var marcas = await ConsultarMarcasPorCategoriasAsync(conn, grupoCategoria, selectedCategorias, ct);

        return new ReportesVentasCatalogosResponse
        {
            FechaOperacion = constants.FechaOperacion,
            Empresas = empresas,
            Almacenes = almacenes,
            Agentes = agentes,
            Categorias = categorias,
            Subcategorias = subcategorias,
            Marcas = marcas,
            Documentos = BuildDocumentos(),
            Proveedores = proveedores
        };
    }

    public async Task<ReportesVentasCatalogosResponse> ConsultarCatalogosRemisionesAsync(CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var constants = await ConsultarConstantesAsync(conn, ct);
        var empresas = await ConsultarEmpresasAsync(conn, ct);
        var agentes = await ConsultarAgentesAsync(conn, ct);
        var usuarios = await ConsultarUsuariosAsync(conn, ct);
        var repartidores = await ConsultarRepartidoresAsync(conn, ct);
        var proveedores = await ConsultarProveedoresAsync(conn, ct);
        var autos = await ConsultarAutosAsync(conn, ct);
        var tiposGasto = await ConsultarTiposGastoAsync(conn, ct);

        return new ReportesVentasCatalogosResponse
        {
            FechaOperacion = constants.FechaOperacion,
            Empresas = empresas,
            Agentes = agentes,
            Usuarios = usuarios,
            Repartidores = repartidores,
            Autos = autos,
            TiposGasto = tiposGasto,
            Documentos = BuildDocumentosConTodos(),
            StatusFolios = BuildStatusFolios(),
            Proveedores = proveedores
        };
    }

    public async Task<List<ReportesVentasClienteItem>> ConsultarClientesAsync(
        int? numero,
        int? idCliente,
        CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        return await ConsultarClientesAsync(conn, numero, idCliente, ct);
    }

    public async Task<ReportesVentasGenerateResponse> GenerarAcumuladoresProductosAsync(
        ReportesVentasAcumuladoresProductosRequest request,
        CancellationToken ct = default)
    {
        var idReporte = ResolveReporte(request);

        await using var conn = GetConnection(await BaseDeCentroAsync(request.IdCentro, ct));
        await conn.OpenAsync(ct);

        var legacyParams = await BuildLegacyParamsAsync(conn, idReporte, request, ct);
        var parametroId = await GuardarParametrosAsync(conn, idReporte, legacyParams, ct);
        var config = await ConsultarConfiguracionAsync(conn, idReporte, ct);

        return new ReportesVentasGenerateResponse
        {
            IDReporte = idReporte,
            NombreReporte = config.NombreReporte,
            StoredProcedure = config.StoredProcedure,
            ParametrosLegacy = parametroId.ToString(),
            Salida = (request.Salida ?? "pantalla").Trim().ToLowerInvariant()
        };
    }

    public async Task<ReportesVentasGenerateResponse> GenerarRemisionesAsync(
        ReportesVentasRemisionesRequest request,
        CancellationToken ct = default)
    {
        var idReporte = ResolveReporteRemisiones(request);

        await using var conn = GetConnection(await BaseDeCentroAsync(request.IdCentro, ct));
        await conn.OpenAsync(ct);

        var legacyParams = await BuildRemisionesLegacyParamsAsync(conn, request, ct);
        var parametroId = await GuardarParametrosAsync(conn, idReporte, legacyParams, ct);
        var config = await ConsultarConfiguracionAsync(conn, idReporte, ct);

        return new ReportesVentasGenerateResponse
        {
            IDReporte = idReporte,
            NombreReporte = config.NombreReporte,
            StoredProcedure = config.StoredProcedure,
            ParametrosLegacy = parametroId.ToString(),
            Salida = (request.Salida ?? "pantalla").Trim().ToLowerInvariant()
        };
    }

    public async Task<ReportesVentasGenerateResponse> GenerarFoliosAsync(
        ReportesVentasFoliosRequest request,
        CancellationToken ct = default)
    {
        var idReporte = ResolveReporteFolios(request);

        await using var conn = GetConnection(await BaseDeCentroAsync(request.IdCentro, ct));
        await conn.OpenAsync(ct);

        var legacyParams = await BuildDocumentosVentaLegacyParamsAsync(conn, idReporte, request, ct);
        var parametroId = await GuardarParametrosAsync(conn, idReporte, legacyParams, ct);
        var config = await ConsultarConfiguracionAsync(conn, idReporte, ct);

        return new ReportesVentasGenerateResponse
        {
            IDReporte = idReporte,
            NombreReporte = config.NombreReporte,
            StoredProcedure = config.StoredProcedure,
            ParametrosLegacy = parametroId.ToString(),
            Salida = (request.Salida ?? "pantalla").Trim().ToLowerInvariant()
        };
    }

    public async Task<ReportesVentasGenerateResponse> GenerarFacturasAsync(
        ReportesVentasFacturasRequest request,
        CancellationToken ct = default)
    {
        var idReporte = ResolveReporteFacturas(request);

        await using var conn = GetConnection(await BaseDeCentroAsync(request.IdCentro, ct));
        await conn.OpenAsync(ct);

        var legacyParams = await BuildDocumentosVentaLegacyParamsAsync(
            conn,
            idReporte,
            request,
            ct,
            formato: 0,
            tipo: ResolveTipoFactura(request.TipoFactura));
        var parametroId = await GuardarParametrosAsync(conn, idReporte, legacyParams, ct);
        var config = await ConsultarConfiguracionAsync(conn, idReporte, ct);

        return new ReportesVentasGenerateResponse
        {
            IDReporte = idReporte,
            NombreReporte = config.NombreReporte,
            StoredProcedure = config.StoredProcedure,
            ParametrosLegacy = parametroId.ToString(),
            Salida = (request.Salida ?? "pantalla").Trim().ToLowerInvariant()
        };
    }

    public async Task<ReportesVentasGenerateResponse> GenerarConcentradosAsync(
        ReportesVentasConcentradosRequest request,
        CancellationToken ct = default)
    {
        var idReporte = ResolveReporteConcentrados(request);

        await using var conn = GetConnection(await BaseDeCentroAsync(request.IdCentro, ct));
        await conn.OpenAsync(ct);

        var legacyParams = BuildConcentradosLegacyParams(request);
        var parametroId = await GuardarParametrosAsync(conn, idReporte, legacyParams, ct);
        var config = await ConsultarConfiguracionAsync(conn, idReporte, ct);

        return new ReportesVentasGenerateResponse
        {
            IDReporte = idReporte,
            NombreReporte = config.NombreReporte,
            StoredProcedure = config.StoredProcedure,
            ParametrosLegacy = parametroId.ToString(),
            Salida = (request.Salida ?? "pantalla").Trim().ToLowerInvariant()
        };
    }

    public async Task<ReportesVentasGenerateResponse> GenerarCobranzaAsync(
        ReportesVentasCobranzaRequest request,
        CancellationToken ct = default)
    {
        var idReporte = ResolveReporteCobranza(request);

        await using var conn = GetConnection(await BaseDeCentroAsync(request.IdCentro, ct));
        await conn.OpenAsync(ct);

        var legacyParams = await BuildCobranzaLegacyParamsAsync(conn, request, ct);
        var parametroId = await GuardarParametrosAsync(conn, idReporte, legacyParams, ct);
        var config = await ConsultarConfiguracionAsync(conn, idReporte, ct);

        return new ReportesVentasGenerateResponse
        {
            IDReporte = idReporte,
            NombreReporte = config.NombreReporte,
            StoredProcedure = config.StoredProcedure,
            ParametrosLegacy = parametroId.ToString(),
            Salida = (request.Salida ?? "pantalla").Trim().ToLowerInvariant()
        };
    }

    public async Task<ReportesVentasGenerateResponse> GenerarLegacyVentasAsync(
        ReportesVentasLegacyRequest request,
        CancellationToken ct = default)
    {
        var idReporte = ResolveReporteLegacyVentas(request);

        await using var conn = GetConnection(await BaseDeCentroAsync(request.IdCentro, ct));
        await conn.OpenAsync(ct);

        var legacyParams = await BuildLegacyVentasParamsAsync(conn, idReporte, request, ct);
        var parametroId = await GuardarParametrosAsync(conn, idReporte, legacyParams, ct);
        var config = await ConsultarConfiguracionAsync(conn, idReporte, ct);

        return new ReportesVentasGenerateResponse
        {
            IDReporte = idReporte,
            NombreReporte = config.NombreReporte,
            StoredProcedure = config.StoredProcedure,
            ParametrosLegacy = parametroId.ToString(),
            Salida = (request.Salida ?? "pantalla").Trim().ToLowerInvariant()
        };
    }

    public async Task<ReportesVentasPreviewResponse> ConsultarAcumuladoresProductosAsync(
        ReportesVentasAcumuladoresProductosRequest request,
        CancellationToken ct = default)
    {
        var idReporte = ResolveReporte(request);

        await using var conn = GetConnection(await BaseDeCentroAsync(request.IdCentro, ct));
        await conn.OpenAsync(ct);

        var legacyParams = await BuildLegacyParamsAsync(conn, idReporte, request, ct);
        var parametroId = await GuardarParametrosAsync(conn, idReporte, legacyParams, ct);
        return await ConsultarAcumuladoresProductosPorParametrosAsync(conn, parametroId, request, ct);
    }

    public async Task<ReportesVentasPreviewResponse> ConsultarAcumuladoresProductosPorParametrosAsync(
        int parametrosLegacy,
        CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var legacy = await ConsultarParametrosAsync(conn, parametrosLegacy, ct);
        return await ConsultarAcumuladoresProductosPorParametrosAsync(conn, parametrosLegacy, legacy.Request, ct);
    }

    public async Task<ReportesVentasFileResponse> GenerarAcumuladoresProductosExcelPorParametrosAsync(
        int parametrosLegacy,
        CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var legacy = await ConsultarParametrosAsync(conn, parametrosLegacy, ct);
        if (ResolvePresentacion(legacy.Request.Salida) != 3)
            throw new InvalidOperationException("El psp no corresponde a una salida Excel.");

        var idReporte = legacy.IDReporte;
        var config = await ConsultarConfiguracionAsync(conn, idReporte, ct);
        var data = await ExecuteLegacySpAsync(conn, config.StoredProcedure, parametrosLegacy.ToString(), ct);
        var source = data.Tables.Count > 0 ? data.Tables[0] : new DataTable();
        var errorParams = source.Columns.Contains("ErrorParams") && source.Rows.Count > 0
            ? Convert.ToString(source.Rows[0]["ErrorParams"]) ?? "Error de parametros legacy."
            : "";
        if (!string.IsNullOrWhiteSpace(errorParams))
            throw new InvalidOperationException(errorParams);

        var excelTable = BuildLegacyExcelTable(idReporte, source);
        return LegacyPlainXlsxReportRenderer.Render(excelTable, config.NombreReporte, legacy.Request);
    }

    public async Task<ReportesVentasPreviewResponse> ConsultarReporteVentasPorParametrosAsync(
        int parametrosLegacy,
        int idCentro = 0,
        CancellationToken ct = default)
    {
        var reporte = await ArmarReportePorParametrosAsync(parametrosLegacy, idCentro, ct);

        /*
          Y aqui se le pone de donde salio. Va en UN solo lugar y no en las doce
          ramas de abajo: si se sellara en cada una, la que se agregue manana
          saldria sin marca y nadie lo notaria hasta que dos PDF identicos
          trajeran numeros distintos.
        */
        var centro = idCentro > 0 ? await _centros.BuscarAsync(idCentro, ct) : null;
        if (centro is not null)
        {
            reporte.NombreReporte = SelloDeCentro.Nombre(reporte.NombreReporte, centro.Nombre);
            reporte.Html = SelloDeCentro.Html(reporte.Html, centro.Nombre);
        }
        return reporte;
    }

    private async Task<ReportesVentasPreviewResponse> ArmarReportePorParametrosAsync(
        int parametrosLegacy,
        int idCentro,
        CancellationToken ct)
    {
        /* Los parametros se guardaron en la base del centro, asi que hay que
           leerlos de ahi: el reporte entero vive en una sola base. */
        await using var conn = GetConnection(await BaseDeCentroAsync(idCentro, ct));
        await conn.OpenAsync(ct);

        var row = await ConsultarParametrosRowAsync(conn, parametrosLegacy, ct);
        var idReporte = ReadInt(row, "IDReporte");
        if (IsAcumuladoresProductosReporte(idReporte))
        {
            var legacy = BuildAcumuladoresProductosStoredParams(row);
            return await ConsultarAcumuladoresProductosPorParametrosAsync(conn, parametrosLegacy, legacy.Request, ct);
        }
        if (IsRemisionesReporte(idReporte))
        {
            var request = BuildRemisionesStoredRequest(row);
            return await ConsultarReportePorParametrosAsync(conn, parametrosLegacy, idReporte, request, ct);
        }
        if (IsFoliosReporte(idReporte))
        {
            var request = BuildRemisionesStoredRequest(row);
            return await ConsultarReportePorParametrosAsync(conn, parametrosLegacy, idReporte, request, ct);
        }
        if (IsFacturasReporte(idReporte))
        {
            var request = BuildRemisionesStoredRequest(row);
            return await ConsultarReportePorParametrosAsync(conn, parametrosLegacy, idReporte, request, ct);
        }
        if (IsConcentradosReporte(idReporte))
        {
            var request = BuildConcentradosStoredRequest(row);
            return await ConsultarReportePorParametrosAsync(conn, parametrosLegacy, idReporte, request, ct);
        }
        if (IsCobranzaReporte(idReporte))
        {
            var request = BuildCobranzaStoredRequest(row);
            return await ConsultarReportePorParametrosAsync(conn, parametrosLegacy, idReporte, request, ct);
        }
        if (IsGenericVentasReporte(idReporte))
        {
            var request = BuildGenericVentasStoredRequest(row);
            return await ConsultarReportePorParametrosAsync(conn, parametrosLegacy, idReporte, request, ct);
        }

        throw new InvalidOperationException("El psp no corresponde a un reporte soportado.");
    }

    public async Task<ReportesVentasFileResponse> GenerarReporteVentasExcelPorParametrosAsync(
        int parametrosLegacy,
        int idCentro = 0,
        CancellationToken ct = default)
    {
        await using var conn = GetConnection(await BaseDeCentroAsync(idCentro, ct));
        await conn.OpenAsync(ct);

        var row = await ConsultarParametrosRowAsync(conn, parametrosLegacy, ct);
        var idReporte = ReadInt(row, "IDReporte");
        var salida = ResolveSalida(ReadString(row, "Param10"));
        if (IsAcumuladoresProductosReporte(idReporte))
            salida = ResolveSalida(ReadString(row, "Param12"));
        if (IsConcentradosReporte(idReporte))
            salida = ResolveSalida(ReadString(row, "Param5"));
        if (IsGenericVentasReporte(idReporte))
            salida = ResolveGenericVentasSalida(idReporte, row);

        if (salida != "excel")
            throw new InvalidOperationException("El psp no corresponde a una salida Excel.");

        if (!IsAcumuladoresProductosReporte(idReporte) && !IsRemisionesReporte(idReporte) && !IsFoliosReporte(idReporte) && !IsFacturasReporte(idReporte) && !IsConcentradosReporte(idReporte) && !IsCobranzaReporte(idReporte) && !IsGenericVentasReporte(idReporte))
            throw new InvalidOperationException("El psp no corresponde a un reporte soportado.");

        var request = BuildStoredExcelRequest(idReporte, row);
        var config = await ConsultarConfiguracionAsync(conn, idReporte, ct);
        var data = await ExecuteLegacySpAsync(conn, config.StoredProcedure, parametrosLegacy.ToString(), ct);
        var source = data.Tables.Count > 0 ? data.Tables[0] : new DataTable();
        var errorParams = source.Columns.Contains("ErrorParams") && source.Rows.Count > 0
            ? Convert.ToString(source.Rows[0]["ErrorParams"]) ?? "Error de parametros legacy."
            : "";
        if (!string.IsNullOrWhiteSpace(errorParams))
            throw new InvalidOperationException(errorParams);

        var excelTable = BuildLegacyExcelTable(idReporte, source);
        return LegacyPlainXlsxReportRenderer.Render(excelTable, config.NombreReporte, request);
    }

    private static ReportesVentasAcumuladoresProductosRequest BuildStoredExcelRequest(int idReporte, DataRow row)
    {
        if (IsAcumuladoresProductosReporte(idReporte))
            return BuildAcumuladoresProductosStoredParams(row).Request;
        if (IsRemisionesReporte(idReporte) || IsFoliosReporte(idReporte) || IsFacturasReporte(idReporte))
            return BuildRemisionesStoredRequest(row);
        if (IsConcentradosReporte(idReporte))
            return BuildConcentradosStoredRequest(row);
        if (IsCobranzaReporte(idReporte))
            return BuildCobranzaStoredRequest(row);
        if (IsGenericVentasReporte(idReporte))
            return BuildGenericVentasStoredRequest(row);

        return new ReportesVentasAcumuladoresProductosRequest();
    }

    private static async Task<ReportesVentasPreviewResponse> ConsultarAcumuladoresProductosPorParametrosAsync(
        SqlConnection conn,
        int parametroId,
        ReportesVentasAcumuladoresProductosRequest request,
        CancellationToken ct)
    {
        var idReporte = ResolveReporte(request);
        return await ConsultarReportePorParametrosAsync(conn, parametroId, idReporte, request, ct);
    }

    private static async Task<ReportesVentasPreviewResponse> ConsultarReportePorParametrosAsync(
        SqlConnection conn,
        int parametroId,
        int idReporte,
        ReportesVentasAcumuladoresProductosRequest request,
        CancellationToken ct)
    {
        var config = await ConsultarConfiguracionAsync(conn, idReporte, ct);
        var data = await ExecuteLegacySpAsync(conn, config.StoredProcedure, parametroId.ToString(), ct);

        var table = data.Tables.Count > 0 ? data.Tables[0] : new DataTable();
        var errorParams = table.Columns.Contains("ErrorParams") && table.Rows.Count > 0
            ? Convert.ToString(table.Rows[0]["ErrorParams"]) ?? "Error de parametros legacy."
            : "";

        var reportHtml = "";
        var template = new LegacyTemplate();
        if (ResolvePresentacion(request.Salida) == 0)
        {
            var constants = await ConsultarConstantesAsync(conn, ct);
            var logo = ResolveLogoForHtml(constants.Logo);
            var templateReportId = config.IDReporteMaster > 0 ? config.IDReporteMaster : idReporte;
            var templates = await ConsultarTemplatesAsync(conn, templateReportId, ct);
            template = BuildTemplateInfo(templates);
            if (!string.IsNullOrWhiteSpace(errorParams))
            {
                reportHtml = GenerateSinInformacionHtml(logo, templates, config.NombreReporte, request, errorParams);
            }
            else if (idReporte == ReporteListasPreciosZaragoza)
            {
                reportHtml = GenerateListasPreciosZaragozaHtml(logo, config.NombreReporte, data, templates, request);
            }
            else if (string.Equals(config.Aspx, "3columnas", StringComparison.OrdinalIgnoreCase))
            {
                reportHtml = GenerateFormatoColumnas3Html(logo, config.NombreReporte, data, templates, template, request);
            }
            else
            {
                reportHtml = GenerateFormatoNormalHtml(logo, constants.LogoWatermark, config.NombreReporte, data, templates, template, request);
            }
        }

        if (!string.IsNullOrWhiteSpace(errorParams) && string.IsNullOrWhiteSpace(reportHtml))
            throw new InvalidOperationException(errorParams);

        return new ReportesVentasPreviewResponse
        {
            IDReporte = idReporte,
            NombreReporte = config.NombreReporte,
            StoredProcedure = config.StoredProcedure,
            ParametrosLegacy = parametroId.ToString(),
            Html = reportHtml,
            Orientacion = template.Orientacion,
            Ancho = template.Ancho,
            Alto = template.Alto,
            MarginTop = template.MarginTop,
            MarginBottom = template.MarginBottom,
            MarginLeft = template.MarginLeft,
            MarginRight = template.MarginRight,
            Columns = BuildColumns(table),
            Rows = BuildRows(table),
            Total = table.Rows.Count
        };
    }

    /*
      DE QUE BASE SALEN LOS DATOS DE ESTE REPORTE.

      Sin centro, la de siempre. Con centro, la suya: mismo servidor, mismo
      usuario, solo cambia el catalogo (ver BaseDelCentro). El sistema es
      identico en la matriz y en cada centro; lo unico que cambia es a quien se
      le pregunta.

      `baseDelCentro` NUNCA viene del navegador. El front manda un IDCentro y
      quien resuelve el nombre es CentrosServicioRepository, contra la tabla, en
      el servidor.

      Solo lo usan los REPORTES. Los demas modulos siguen con su base y no se
      enteran de que esto existe.
    */
    private SqlConnection GetConnection(string? baseDelCentro = null)
    {
        var cs = _configuration.GetConnectionString("Main")
            ?? _configuration.GetConnectionString("Mac3")
            ?? _configuration.GetConnectionString("Local")
            ?? _configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("ConnectionString (Main/Mac3/Local/Default) no encontrada.");

        var connector = new Mac3SqlServerConnector(BaseDelCentro.Apuntar(cs, baseDelCentro));
        return connector.GetConnection;
    }
}
