using System.Data;
using ISL_Service.Application.DTOs.Reportes;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

public class ReportesVentasRepository : IReportesVentasRepository
{
    private readonly IConfiguration _configuration;

    private const int ReporteVentaAcumuladores = 10;
    private const int ReporteVentaProductos = 15;
    private const int GrupoCategoriaAcumuladores = 1;
    private const int ProductoServicioDomicilio = 11807;
    private const string FallbackLogoPath = "Assets/Logos/Logo_Zaragoza.png";
    private static readonly Lazy<string> FallbackLogoDataUri = new(() => TryImageFileToDataUri(Path.Combine(AppContext.BaseDirectory, FallbackLogoPath)) ?? "");

    public ReportesVentasRepository(IConfiguration configuration)
    {
        _configuration = configuration;
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
            Documentos = BuildDocumentos()
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
        var legacyParams = BuildLegacyParams(idReporte, request);

        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

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
        var legacyParams = BuildLegacyParams(idReporte, request);

        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

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

    private static async Task<ReportesVentasPreviewResponse> ConsultarAcumuladoresProductosPorParametrosAsync(
        SqlConnection conn,
        int parametroId,
        ReportesVentasAcumuladoresProductosRequest request,
        CancellationToken ct)
    {
        var idReporte = ResolveReporte(request);
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
            reportHtml = !string.IsNullOrWhiteSpace(errorParams)
                ? GenerateSinInformacionHtml(logo, templates, config.NombreReporte, request, errorParams)
                : string.Equals(config.Aspx, "3columnas", StringComparison.OrdinalIgnoreCase)
                    ? GenerateFormatoColumnas3Html(logo, config.NombreReporte, data, templates, template, request)
                    : GenerateFormatoNormalHtml(logo, constants.LogoWatermark, config.NombreReporte, data, templates, template, request);
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

    private SqlConnection GetConnection()
    {
        var cs = _configuration.GetConnectionString("Main")
            ?? _configuration.GetConnectionString("Mac3")
            ?? _configuration.GetConnectionString("Local")
            ?? _configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("ConnectionString (Main/Mac3/Local/Default) no encontrada.");

        var connector = new Mac3SqlServerConnector(cs);
        return connector.GetConnection;
    }

    private static async Task<List<ReportesVentasCatalogoItem>> ConsultarEmpresasAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaEmpresas", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };
        cmd.Parameters.AddWithValue("@IDEmpresa", 0);
        cmd.Parameters.AddWithValue("@IDStatus", 1);
        cmd.Parameters.AddWithValue("@RazonSocial", "");
        cmd.Parameters.AddWithValue("@Formato", 0);
        cmd.Parameters.AddWithValue("@Identico", 0);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return table.AsEnumerable()
            .Select(row => new ReportesVentasCatalogoItem
            {
                Id = ReadInt(row, "IDEmpresa"),
                Nombre = ReadString(row, "NombreCorto", "RazonSocial", "Empresa"),
                Clave = ReadString(row, "Clave", "NombreCorto")
            })
            .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Nombre))
            .ToList();
    }

    private static async Task<List<ReportesVentasCatalogoItem>> ConsultarAlmacenesAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaAlmacenes", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };
        cmd.Parameters.AddWithValue("@IDAlmacen", 0);
        cmd.Parameters.AddWithValue("@IDStatus", 1);
        cmd.Parameters.AddWithValue("@Almacen", "");
        cmd.Parameters.AddWithValue("@Identico", 0);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return table.AsEnumerable()
            .Select(row => new ReportesVentasCatalogoItem
            {
                Id = ReadInt(row, "IDAlmacen"),
                Nombre = ReadString(row, "Almacen"),
                Clave = ReadString(row, "Almacen")
            })
            .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Nombre))
            .ToList();
    }

    private static async Task<List<ReportesVentasCatalogoItem>> ConsultarAgentesAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaAgentes", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };
        cmd.Parameters.AddWithValue("@IDAgente", 0);
        cmd.Parameters.AddWithValue("@IDStatus", 1);
        cmd.Parameters.AddWithValue("@NumeroAgente", 0);
        cmd.Parameters.AddWithValue("@NombreAgente", "");
        cmd.Parameters.AddWithValue("@Formato", 0);
        cmd.Parameters.AddWithValue("@Identico", 0);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return table.AsEnumerable()
            .Select(row => new ReportesVentasCatalogoItem
            {
                Id = ReadInt(row, "IDAgente"),
                Nombre = ReadString(row, "NombreAgente"),
                Clave = ReadString(row, "NumeroAgente")
            })
            .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Nombre))
            .OrderBy(x => x.Nombre)
            .ToList();
    }

    private static async Task<List<ReportesVentasClienteItem>> ConsultarClientesAsync(
        SqlConnection conn,
        int? numero,
        int? idCliente,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaClientes", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };

        cmd.Parameters.AddWithValue("@IDCliente", idCliente.GetValueOrDefault());
        cmd.Parameters.AddWithValue("@IDCondicionesCredito", 0);
        cmd.Parameters.AddWithValue("@IDCondicionesCreditoAceites", 0);
        cmd.Parameters.AddWithValue("@IDCondicionesCreditoCascos", 0);
        cmd.Parameters.AddWithValue("@IDDescuento", 0);
        cmd.Parameters.AddWithValue("@IDAgente", 0);
        cmd.Parameters.AddWithValue("@IDStatus", 0);
        cmd.Parameters.AddWithValue("@Numero", numero.GetValueOrDefault() > 0 ? numero.GetValueOrDefault().ToString() : "");
        cmd.Parameters.AddWithValue("@Nombre", "");
        cmd.Parameters.AddWithValue("@ApellidoPaterno", "");
        cmd.Parameters.AddWithValue("@ApellidoMaterno", "");
        cmd.Parameters.AddWithValue("@RFC", "");
        cmd.Parameters.AddWithValue("@IDTipoCliente", 0);
        cmd.Parameters.AddWithValue("@ConHuella", 0);
        cmd.Parameters.AddWithValue("@IDEmpresaCS", 1);
        cmd.Parameters.AddWithValue("@EsMostrador", 0);
        cmd.Parameters.AddWithValue("@Referencia", "");
        cmd.Parameters.AddWithValue("@Formato", 0);
        cmd.Parameters.AddWithValue("@Identico", 0);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return table.AsEnumerable()
            .Select(row => new ReportesVentasClienteItem
            {
                IDCliente = ReadInt(row, "IDCliente"),
                Numero = ReadInt(row, "Numero"),
                NombreCliente = ReadString(row, "NombreCliente"),
                Nombre = ReadString(row, "Nombre")
            })
            .Where(x => x.IDCliente > 0 && x.Numero > 0)
            .ToList();
    }

    private static async Task<List<ReportesVentasCatalogoItem>> ConsultarGruposCategoriasAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaGrupoCategorias", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };
        cmd.Parameters.AddWithValue("@IDsGrupoCategoria", "");
        cmd.Parameters.AddWithValue("@IDStatus", 1);
        cmd.Parameters.AddWithValue("@GrupoCategoria", "");
        cmd.Parameters.AddWithValue("@Identico", 0);
        cmd.Parameters.AddWithValue("@Formato", 0);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return table.AsEnumerable()
            .Select(row => new ReportesVentasCatalogoItem
            {
                Id = ReadInt(row, "IDGrupoCategoria"),
                Nombre = ReadString(row, "GrupoCategoria"),
                Clave = ReadString(row, "GrupoCategoria")
            })
            .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Nombre))
            .ToList();
    }

    private static async Task<List<ReportesVentasProductoItem>> ConsultarSubcategoriasAsync(
        SqlConnection conn,
        int idGrupoCategoria,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaCategorias", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };
        cmd.Parameters.AddWithValue("@IDsCategoria", "");
        cmd.Parameters.AddWithValue("@IDsGrupoCategoria", idGrupoCategoria.ToString());
        cmd.Parameters.AddWithValue("@IDStatus", 1);
        cmd.Parameters.AddWithValue("@Categoria", "");
        cmd.Parameters.AddWithValue("@Identico", 0);
        cmd.Parameters.AddWithValue("@Formato", 0);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return table.AsEnumerable()
            .Select(row => new ReportesVentasProductoItem
            {
                IDGrupoCategoria = ReadInt(row, "IDGrupoCategoria"),
                GrupoCategoria = ReadString(row, "GrupoCategoria"),
                IDCategoria = ReadInt(row, "IDCategoria"),
                Categoria = ReadString(row, "Categoria")
            })
            .Where(x => x.IDGrupoCategoria > 0 && x.IDCategoria > 0 && !string.IsNullOrWhiteSpace(x.Categoria))
            .ToList();
    }

    private static async Task<List<ReportesVentasProductoItem>> ConsultarMarcasPorCategoriasAsync(
        SqlConnection conn,
        int idGrupoCategoria,
        IReadOnlyCollection<int> idCategorias,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaGrupoCategoriasMarcas", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };
        cmd.Parameters.AddWithValue("@IDsMarca", "");
        cmd.Parameters.AddWithValue("@IDsCategoria", JoinIds(idCategorias));
        cmd.Parameters.AddWithValue("@IDsGrupoCategoria", idGrupoCategoria.ToString());
        cmd.Parameters.AddWithValue("@IDStatus", 1);
        cmd.Parameters.AddWithValue("@Marca", "");
        cmd.Parameters.AddWithValue("@Identico", 0);
        cmd.Parameters.AddWithValue("@Formato", 0);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return table.AsEnumerable()
            .Select(row => new ReportesVentasProductoItem
            {
                IDGrupoCategoria = ReadInt(row, "IDGrupoCategoria"),
                GrupoCategoria = ReadString(row, "GrupoCategoria"),
                IDCategoria = ReadInt(row, "IDCategoria"),
                Categoria = ReadString(row, "Categoria"),
                IDMarca = ReadInt(row, "IDMarca"),
                Marca = ReadString(row, "Marca")
            })
            .Where(x => x.IDGrupoCategoria > 0 && x.IDCategoria > 0 && x.IDMarca > 0 && !string.IsNullOrWhiteSpace(x.Marca))
            .ToList();
    }

    private static List<ReportesVentasCatalogoItem> BuildDocumentos()
    {
        return new List<ReportesVentasCatalogoItem>
        {
            new() { Id = 10, Nombre = "VENTAS", Clave = "ventas" },
            new() { Id = 1, Nombre = "MAYORISTAS", Clave = "mayoristas" },
            new() { Id = 2, Nombre = "AJUSTES", Clave = "ajustes" },
            new() { Id = 3, Nombre = "LOCALES", Clave = "locales" },
            new() { Id = 4, Nombre = "DEVOLUCIONES", Clave = "devoluciones" }
        };
    }

    private static List<int> NormalizeCatalogIds(IEnumerable<int>? ids)
    {
        return (ids ?? Enumerable.Empty<int>())
            .Where(x => x > 0)
            .Distinct()
            .ToList();
    }

    private static int ResolveReporte(ReportesVentasAcumuladoresProductosRequest request)
    {
        if (request.IDGrupoCategoria == GrupoCategoriaAcumuladores)
            return ReporteVentaAcumuladores;

        var categoria = (request.Categoria ?? "").Trim().ToLowerInvariant();
        return categoria == "acumuladores" || categoria == "acumulador"
            ? ReporteVentaAcumuladores
            : ReporteVentaProductos;
    }

    private static LegacyReportParams BuildLegacyParams(int idReporte, ReportesVentasAcumuladoresProductosRequest request)
    {
        var idGrupoCategoria = request.IDGrupoCategoria > 0 ? request.IDGrupoCategoria : GrupoCategoriaAcumuladores;
        var idCliente = ResolveCliente(request);

        return new LegacyReportParams
        {
            Param1 = "WEB[0",
            Param2 = JoinIds(request.IDEmpresas),
            Param3 = idGrupoCategoria.ToString(),
            Param4 = JoinIds(request.IDSubcategorias),
            Param5 = JoinIds(request.IDMarcas),
            Param6 = JoinIds(request.IDAlmacenes),
            Param7 = ResolveTipoDocumento(request.Documento).ToString(),
            Param8 = idCliente.ToString(),
            Param9 = JoinIds(request.IDAgentes),
            Param10 = request.FechaInicial.ToString("yyyy-MM-dd"),
            Param11 = request.FechaFinal.ToString("yyyy-MM-dd"),
            Param12 = ResolvePresentacion(request.Salida).ToString(),
            Param13 = idReporte == ReporteVentaAcumuladores && request.SoloServiciosDomicilio
                ? ProductoServicioDomicilio.ToString()
                : ""
        };
    }

    private static int ResolveCliente(ReportesVentasAcumuladoresProductosRequest request)
    {
        if ((request.Salida ?? "").Trim().Equals("excel", StringComparison.OrdinalIgnoreCase))
            return -1;

        if ((request.TipoReporte ?? "").Trim().Equals("cliente", StringComparison.OrdinalIgnoreCase))
            return (request.IDClientes ?? new List<int>()).FirstOrDefault();

        return 0;
    }

    private static bool IsAgentReport(ReportesVentasAcumuladoresProductosRequest request)
    {
        return (request.TipoReporte ?? "").Trim().Equals("agente", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveAgentGroupColumn(DataTable table, ReportesVentasAcumuladoresProductosRequest request)
    {
        if (!IsAgentReport(request))
            return null;

        if (table.Columns.Contains("IDAgente"))
            return "IDAgente";
        if (table.Columns.Contains("Agente"))
            return "Agente";

        return null;
    }

    private static string ReplaceAgentHeader(string html, DataTable? table, DataRow? row, ReportesVentasAcumuladoresProductosRequest request)
    {
        if (!IsAgentReport(request) || table is null || row is null)
        {
            html = html.Replace("#Agente#", "");
            html = html.Replace("#agente#", "");
            return html;
        }

        var idAgente = ReadString(row, "IDAgente");
        var nombreAgente = ReadString(row, "Nombre Agente", "NombreAgente");
        var agente = ReadString(row, "Agente");
        var textoAgente = "";

        if (!string.IsNullOrWhiteSpace(idAgente) && idAgente != "0")
            textoAgente = string.IsNullOrWhiteSpace(nombreAgente) ? idAgente : $"{idAgente} - {nombreAgente}";
        else if (!string.IsNullOrWhiteSpace(agente) && agente != "0")
            textoAgente = agente;

        var value = string.IsNullOrWhiteSpace(textoAgente) ? "" : $"Agente: <b>{textoAgente}</b>";
        html = html.Replace("#Agente#", value);
        html = html.Replace("#agente#", value);
        return html;
    }

    private static string ReplaceClientHeader(string html, DataTable? table, DataRow? row)
    {
        if (table is null || row is null)
        {
            html = html.Replace("#Cliente#", "");
            html = html.Replace("#cliente#", "");
            return html;
        }

        var idCliente = ReadString(row, "idCliente", "IDCliente");
        var cliente = ReadString(row, "cliente", "Cliente");
        var hasCliente = int.TryParse(idCliente, out var id) && id != 0 && !string.IsNullOrWhiteSpace(cliente);
        var value = hasCliente ? $"Cliente: <b>{cliente}</b>" : "";
        html = html.Replace("#Cliente#", value);
        html = html.Replace("#cliente#", value);
        return html;
    }

    private static int ResolvePresentacion(string? salida)
    {
        return (salida ?? "").Trim().Equals("excel", StringComparison.OrdinalIgnoreCase) ? 3 : 0;
    }

    private static int ResolveTipoDocumento(string? documento)
    {
        return (documento ?? "ventas").Trim().ToLowerInvariant() switch
        {
            "mayoristas" => 1,
            "locales" => 3,
            "ajustes" => 2,
            "devoluciones" => 4,
            _ => 10
        };
    }

    private static string ResolveDocumento(string? tipoDocumento)
    {
        return (tipoDocumento ?? "").Trim() switch
        {
            "1" => "mayoristas",
            "2" => "ajustes",
            "3" => "locales",
            "4" => "devoluciones",
            _ => "ventas"
        };
    }

    private static string ResolveSalida(string? presentacion)
    {
        return (presentacion ?? "").Trim() == "3" ? "excel" : "pantalla";
    }

    private static string JoinIds(IEnumerable<int>? ids)
    {
        var clean = (ids ?? Enumerable.Empty<int>())
            .Where(x => x > 0)
            .Distinct()
            .Select(x => x.ToString())
            .ToList();

        return clean.Count == 0 ? "" : string.Join("~", clean);
    }

    private static async Task<int> GuardarParametrosAsync(
        SqlConnection conn,
        int idReporte,
        LegacyReportParams legacyParams,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ActualizarParametro", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };

        cmd.Parameters.AddWithValue("@NombreEquipo", "WEB");
        cmd.Parameters.AddWithValue("@IDUsuario", 0);
        cmd.Parameters.AddWithValue("@IDReporte", idReporte);
        cmd.Parameters.AddWithValue("@Param1", legacyParams.Param1);
        cmd.Parameters.AddWithValue("@Param2", legacyParams.Param2);
        cmd.Parameters.AddWithValue("@Param3", legacyParams.Param3);
        cmd.Parameters.AddWithValue("@Param4", legacyParams.Param4);
        cmd.Parameters.AddWithValue("@Param5", legacyParams.Param5);
        cmd.Parameters.AddWithValue("@Param6", legacyParams.Param6);
        cmd.Parameters.AddWithValue("@Param7", legacyParams.Param7);
        cmd.Parameters.AddWithValue("@Param8", legacyParams.Param8);
        cmd.Parameters.AddWithValue("@Param9", legacyParams.Param9);
        cmd.Parameters.AddWithValue("@Param10", legacyParams.Param10);
        cmd.Parameters.AddWithValue("@Param11", legacyParams.Param11);
        cmd.Parameters.AddWithValue("@Param12", legacyParams.Param12);
        cmd.Parameters.AddWithValue("@Param13", legacyParams.Param13);
        cmd.Parameters.AddWithValue("@Param14", "");
        cmd.Parameters.AddWithValue("@Param15", "");
        cmd.Parameters.AddWithValue("@Param16", "");
        cmd.Parameters.AddWithValue("@Param17", "");
        cmd.Parameters.AddWithValue("@Param18", "");
        cmd.Parameters.AddWithValue("@Param19", "");
        cmd.Parameters.AddWithValue("@Param20", "");

        var ds = await FillDataSetAsync(cmd, ct);
        if (ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
            throw new InvalidOperationException("sp_n_ActualizarParametro no regreso ID de parametros.");

        var row = ds.Tables[0].Rows[0];
        if (ds.Tables[0].Columns.Contains("ID") && int.TryParse(Convert.ToString(row["ID"]), out var id) && id > 0)
            return id;

        throw new InvalidOperationException("No se pudo leer el ID de parametros legacy.");
    }

    private static async Task<LegacyStoredParams> ConsultarParametrosAsync(
        SqlConnection conn,
        int parametroId,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaParametros", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };
        cmd.Parameters.AddWithValue("@ID", parametroId);

        var ds = await FillDataSetAsync(cmd, ct);
        if (ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
            throw new InvalidOperationException("No se encontraron parametros legacy para el psp.");

        var row = ds.Tables[0].Rows[0];
        var idReporte = ReadInt(row, "IDReporte");
        if (idReporte != ReporteVentaAcumuladores && idReporte != ReporteVentaProductos)
            throw new InvalidOperationException("El psp no corresponde a Acumuladores y Productos.");

        return new LegacyStoredParams
        {
            IDReporte = idReporte,
            Request = new ReportesVentasAcumuladoresProductosRequest
            {
                Categoria = idReporte == ReporteVentaAcumuladores ? "acumuladores" : "productos",
                IDGrupoCategoria = ReadInt(row, "Param3"),
                TipoReporte = ResolveTipoReporteFromParams(row),
                Documento = ResolveDocumento(ReadString(row, "Param7")),
                FechaInicial = ReadLegacyDate(row, "Param10"),
                FechaFinal = ReadLegacyDate(row, "Param11"),
                Salida = ResolveSalida(ReadString(row, "Param12")),
                SoloServiciosDomicilio = ReadString(row, "Param13") == ProductoServicioDomicilio.ToString(),
                IDEmpresas = SplitLegacyIds(ReadString(row, "Param2")),
                IDAlmacenes = SplitLegacyIds(ReadString(row, "Param6")),
                IDSubcategorias = SplitLegacyIds(ReadString(row, "Param4")),
                IDMarcas = SplitLegacyIds(ReadString(row, "Param5")),
                IDAgentes = SplitLegacyIds(ReadString(row, "Param9")),
                IDClientes = SplitLegacyIds(ReadString(row, "Param8"))
            }
        };
    }

    private static string ResolveTipoReporteFromParams(DataRow row)
    {
        var clientes = SplitLegacyIds(ReadString(row, "Param8"));
        if (clientes.Any(id => id > 0))
            return "cliente";

        var agentes = SplitLegacyIds(ReadString(row, "Param9"));
        if (agentes.Any(id => id > 0))
            return "agente";

        return "empresa";
    }

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
        await using var cmd = new SqlCommand("sp_n_ConsultaConstantes", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };
        cmd.Parameters.AddWithValue("@ValidaActividad", 0);
        cmd.Parameters.AddWithValue("@IDUsuario", 0);
        cmd.Parameters.AddWithValue("@Equipo", "");

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
            LogoWatermark = pathImagenes + (Convert.ToString(row["LogoMacWM"]) ?? "")
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

        var cuerpoTemplate = ReadTemplateHtml(templates, "cuerpo_n");
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
                    htmlCuerpo = "<p style='page-break-before: always'></p>";
                    htmlCuerpo += BuildFormatoNormalHeader(cuerpoTemplate, logo, reporteNombre, numPagina, totalPaginas, table, row, numRegistro, request);
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

        var cuerpoTemplate = ReadTemplateHtml(templates, "cuerpo_n");
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

    private static string GenerateEmptyColumnas3Html(
        string logo,
        DataTable templates,
        string reporteNombre,
        ReportesVentasAcumuladoresProductosRequest request)
    {
        var html = BuildColumnas3Header(ReadTemplateHtml(templates, "cuerpo_n"), logo, reporteNombre, 1, 1, null, null, request);
        var mensaje = "<tr><td colspan='16' align='center'><br /><br /> <u> <h1>No se encontro informacion</h1> </u></td></tr>";
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
        var html = BuildEmptyHeader(ReadTemplateHtml(templates, "cuerpo_n"), logo, reporteNombre, request);
        var mensaje = "<tr><td colspan='16' align='center'><br /><br /> <u> <h1>No se encontro informacion</h1> </u></td></tr>";
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
        var html = BuildEmptyHeader(ReadTemplateHtml(templates, "cuerpo_n"), logo, reporteNombre, request);
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

    private static string BuildFormatoNormalHeader(
        string cuerpoTemplate,
        string logo,
        string reporteNombre,
        int numPagina,
        int totalPaginas,
        DataTable? table,
        DataRow? row,
        int numRegistro,
        ReportesVentasAcumuladoresProductosRequest request)
    {
        var html = cuerpoTemplate;
        html = html.Replace("#Logo#", logo);
        html = html.Replace("#Titulo#", reporteNombre);
        html = html.Replace("#NumPagina#", numPagina.ToString());
        html = html.Replace("#TotalPaginas#", totalPaginas.ToString());

        if (table is null || row is null)
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

        if (table.Columns.Contains("topWM") && numRegistro > 0)
            html = html.Replace("#topWM#", Convert.ToString(row["topWM"]) ?? "");

        html = ReplaceAgentHeader(html, table, row, request);
        html = ReplaceClientHeader(html, table, row);
        return ReplaceValues(table, row, html);
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
            var value = Convert.ToString(row[column.ColumnName]) ?? "";
            if (value != "")
                value = FormatLegacyNumber(column.DataType, value, esTotales);

            html = html.Replace(tag, value);
            html = html.Replace(tag.ToLowerInvariant(), value);
        }

        return html;
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

        if (dataType == typeof(int) && int.TryParse(value, out var intValue))
            return intValue.ToString("###,##0");
        if ((dataType == typeof(float) || dataType == typeof(double)) && double.TryParse(value, out var doubleValue))
            return doubleValue.ToString("###,##0.00");
        if (dataType == typeof(decimal) && decimal.TryParse(value, out var decimalValue))
            return Math.Truncate(decimalValue * 100m / 1m) / 100m == 0
                ? decimalValue.ToString("###,##0.00")
                : (Math.Truncate(decimalValue * 100m) / 100m).ToString("###,##0.00");

        return value;
    }

    private static int ReadInt(DataRow row, string column)
    {
        return row.Table.Columns.Contains(column) && int.TryParse(Convert.ToString(row[column]), out var value)
            ? value
            : 0;
    }

    private static string ReadString(DataRow row, string column)
    {
        return row.Table.Columns.Contains(column) ? Convert.ToString(row[column]) ?? "" : "";
    }

    private static string ReadString(DataRow row, params string[] columns)
    {
        foreach (var column in columns)
        {
            var value = ReadString(row, column);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return "";
    }

    private static DateTime ReadLegacyDate(DataRow row, string column)
    {
        var value = ReadString(row, column).Replace("@", " ");
        return DateTime.TryParse(value, out var date) ? date : DateTime.Today;
    }

    private static List<int> SplitLegacyIds(string value)
    {
        return (value ?? "")
            .Split('~', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => int.TryParse(item, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
    }

    private static async Task<DataSet> ExecuteLegacySpAsync(
        SqlConnection conn,
        string spNames,
        string spParams,
        CancellationToken ct)
    {
        var ds = new DataSet();
        var parameters = (spParams ?? "").Split('|');

        for (var paramIndex = 0; paramIndex < parameters.Length; paramIndex++)
        {
            parameters[paramIndex] = parameters[paramIndex].Replace("~", "|");
        }

        foreach (var spName in (spNames ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            await using var deriveCmd = new SqlCommand(spName, conn)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 500
            };
            SqlCommandBuilder.DeriveParameters(deriveCmd);

            await using var cmd = new SqlCommand(spName, conn)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 500
            };

            var valueIndex = 0;
            foreach (SqlParameter parameter in deriveCmd.Parameters)
            {
                if (parameter.ParameterName == "@RETURN_VALUE")
                    continue;

                var rawValue = valueIndex < parameters.Length ? parameters[valueIndex] : "";
                var value = rawValue.Replace("|", "~");
                if (rawValue.Contains("@@"))
                    value = rawValue.Replace("@@", "|");
                else if (rawValue.Contains("["))
                    value = rawValue.Replace("[", "~");

                cmd.Parameters.Add(parameter.ParameterName, parameter.SqlDbType).Value = value;
                valueIndex++;
            }

            var table = new DataTable(spName);
            using var adapter = new SqlDataAdapter(cmd);
            adapter.Fill(table);
            ds.Tables.Add(table);
        }

        await Task.CompletedTask;
        return ds;
    }

    private static async Task<DataSet> FillDataSetAsync(SqlCommand cmd, CancellationToken ct)
    {
        var ds = new DataSet();
        using var adapter = new SqlDataAdapter(cmd);
        adapter.Fill(ds);
        await Task.CompletedTask;
        return ds;
    }

    private static async Task<DataTable> ExecuteFirstTableAsync(SqlCommand cmd, CancellationToken ct)
    {
        var ds = await FillDataSetAsync(cmd, ct);
        return ds.Tables.Count > 0 ? ds.Tables[0] : new DataTable();
    }

    private static List<ReportesVentasColumnDto> BuildColumns(DataTable table)
    {
        return table.Columns
            .Cast<DataColumn>()
            .Select(column => new ReportesVentasColumnDto
            {
                Key = column.ColumnName,
                Label = SplitLabel(column.ColumnName),
                Type = ResolveColumnType(column.DataType)
            })
            .ToList();
    }

    private static List<Dictionary<string, object?>> BuildRows(DataTable table)
    {
        return table.Rows
            .Cast<DataRow>()
            .Select(row => table.Columns
                .Cast<DataColumn>()
                .ToDictionary(
                    column => column.ColumnName,
                    column => row[column] == DBNull.Value ? null : row[column]))
            .ToList();
    }

    private static string SplitLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var chars = new List<char> { value[0] };
        for (var i = 1; i < value.Length; i++)
        {
            var current = value[i];
            var previous = value[i - 1];
            if (char.IsUpper(current) && !char.IsWhiteSpace(previous) && !char.IsUpper(previous))
                chars.Add(' ');
            chars.Add(current);
        }
        return new string(chars.ToArray());
    }

    private static string ResolveColumnType(Type type)
    {
        if (type == typeof(decimal) || type == typeof(double) || type == typeof(float))
            return "number";
        if (type == typeof(int) || type == typeof(long) || type == typeof(short))
            return "number";
        if (type == typeof(DateTime))
            return "date";
        return "text";
    }

    private sealed class LegacyReportParams
    {
        public string Param1 { get; set; } = "";
        public string Param2 { get; set; } = "";
        public string Param3 { get; set; } = "";
        public string Param4 { get; set; } = "";
        public string Param5 { get; set; } = "";
        public string Param6 { get; set; } = "";
        public string Param7 { get; set; } = "";
        public string Param8 { get; set; } = "";
        public string Param9 { get; set; } = "";
        public string Param10 { get; set; } = "";
        public string Param11 { get; set; } = "";
        public string Param12 { get; set; } = "";
        public string Param13 { get; set; } = "";
    }

    private sealed class LegacyStoredParams
    {
        public int IDReporte { get; set; }
        public ReportesVentasAcumuladoresProductosRequest Request { get; set; } = new();
    }

    private sealed class LegacyReportConfig
    {
        public string NombreReporte { get; set; } = "";
        public string StoredProcedure { get; set; } = "";
        public string Aspx { get; set; } = "";
        public int IDReporteMaster { get; set; }
    }

    private sealed class LegacyConstants
    {
        public string FechaOperacion { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");
        public string Logo { get; set; } = "";
        public string LogoWatermark { get; set; } = "";
    }

    private sealed class LegacyTemplate
    {
        public string Orientacion { get; set; } = "Landscape";
        public int Ancho { get; set; }
        public int Alto { get; set; }
        public int MarginTop { get; set; }
        public int MarginBottom { get; set; }
        public int MarginLeft { get; set; }
        public int MarginRight { get; set; }
        public int MaxLineas { get; set; } = 40;
    }
}
