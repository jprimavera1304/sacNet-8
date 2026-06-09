using System.Data;
using ISL_Service.Application.DTOs.Reportes;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Infrastructure.Reports;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

public partial class ReportesVentasRepository
{
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

    private static int ResolveReporteRemisiones(ReportesVentasRemisionesRequest request)
    {
        return ResolveStatusFolio(request.EstatusFolio) == 4
            ? ReporteVentasRemisionesSaldoFavor
            : ReporteVentasRemisiones;
    }

    private static int ResolveReporteFolios(ReportesVentasFoliosRequest request)
    {
        if (request.FoliosDineroCascos)
            return ReporteVentasFoliosDineroUsados;

        return ResolveStatusFolio(request.EstatusFolio) == 4
            ? ReporteVentasRemisionesSaldoFavor
            : ReporteVentasFoliosGlobal;
    }

    private static bool IsAcumuladoresProductosReporte(int idReporte)
    {
        return idReporte == ReporteVentaAcumuladores || idReporte == ReporteVentaProductos;
    }

    private static bool IsRemisionesReporte(int idReporte)
    {
        return idReporte == ReporteVentasRemisiones
            || idReporte == ReporteVentasRemisionesSaldoFavor
            || idReporte == ReporteVentasRemisionesImpuestos;
    }

    private static bool IsFoliosReporte(int idReporte)
    {
        return idReporte == ReporteVentasFoliosGlobal
            || idReporte == ReporteVentasFoliosDineroUsados;
    }

    private static async Task<LegacyReportParams> BuildLegacyParamsAsync(
        SqlConnection conn,
        int idReporte,
        ReportesVentasAcumuladoresProductosRequest request,
        CancellationToken ct)
    {
        var idGrupoCategoria = request.IDGrupoCategoria > 0 ? request.IDGrupoCategoria : GrupoCategoriaAcumuladores;
        var idCliente = await ResolveClienteAsync(conn, request, ct);

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

    private static async Task<LegacyReportParams> BuildRemisionesLegacyParamsAsync(
        SqlConnection conn,
        ReportesVentasRemisionesRequest request,
        CancellationToken ct)
    {
        var idReporte = ResolveReporteRemisiones(request);
        return await BuildDocumentosVentaLegacyParamsAsync(conn, idReporte, request, ct);
    }

    private static async Task<LegacyReportParams> BuildDocumentosVentaLegacyParamsAsync(
        SqlConnection conn,
        int idReporte,
        ReportesVentasRemisionesRequest request,
        CancellationToken ct)
    {
        var idCliente = await ResolveClienteLegacyAsync(conn, request, ct);
        return new LegacyReportParams
        {
            Param1 = "WEB[0",
            Param2 = JoinIds(request.IDEmpresas),
            Param3 = JoinIds(request.IDAlmacenes),
            Param4 = ResolveTipoDocumento(request.Documento).ToString(),
            Param5 = idCliente.ToString(),
            Param6 = JoinIds(request.IDAgentes),
            Param7 = ResolveStatusFolio(request.EstatusFolio).ToString(),
            Param8 = request.FechaInicial.ToString("yyyy-MM-dd"),
            Param9 = request.FechaFinal.ToString("yyyy-MM-dd"),
            Param10 = ResolvePresentacion(request.Salida).ToString(),
            Param11 = JoinIds(request.IDUsuarios),
            Param12 = "1",
            Param13 = "0",
            Param14 = idReporte == ReporteVentasRemisiones && request.SoloServiciosDomicilio
                ? ProductoServicioDomicilio.ToString()
                : ""
        };
    }

    private static int ResolveStatusFolio(string? estatusFolio)
    {
        return (estatusFolio ?? "todos").Trim().ToLowerInvariant() switch
        {
            "vigentes" => 1,
            "cancelados" or "canceladas" => 2,
            "saldo_favor" or "saldo favor" => 4,
            _ => 0
        };
    }

    private static async Task<int> ResolveClienteAsync(
        SqlConnection conn,
        ReportesVentasAcumuladoresProductosRequest request,
        CancellationToken ct)
    {
        if ((request.Salida ?? "").Trim().Equals("excel", StringComparison.OrdinalIgnoreCase))
            return -1;

        if ((request.TipoReporte ?? "").Trim().Equals("cliente", StringComparison.OrdinalIgnoreCase))
        {
            var candidate = (request.IDClientes ?? new List<int>()).FirstOrDefault(id => id > 0);
            if (candidate <= 0) return 0;

            var byId = await ConsultarClientesAsync(conn, null, candidate, ct);
            if (byId.Any(cliente => cliente.IDCliente == candidate))
                return candidate;

            var byNumero = await ConsultarClientesAsync(conn, candidate, null, ct);
            return byNumero.FirstOrDefault()?.IDCliente ?? candidate;
        }

        return 0;
    }

    private static async Task<int> ResolveClienteLegacyAsync(
        SqlConnection conn,
        ReportesVentasAcumuladoresProductosRequest request,
        CancellationToken ct)
    {
        if (!(request.TipoReporte ?? "").Trim().Equals("cliente", StringComparison.OrdinalIgnoreCase))
            return 0;

        var candidate = (request.IDClientes ?? new List<int>()).FirstOrDefault(id => id > 0);
        if (candidate <= 0) return 0;

        var byId = await ConsultarClientesAsync(conn, null, candidate, ct);
        if (byId.Any(cliente => cliente.IDCliente == candidate))
            return candidate;

        var byNumero = await ConsultarClientesAsync(conn, candidate, null, ct);
        return byNumero.FirstOrDefault()?.IDCliente ?? candidate;
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
            "todos" => 0,
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
        cmd.Parameters.AddWithValue("@Param14", legacyParams.Param14);
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

    private static async Task<DataRow> ConsultarParametrosRowAsync(
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

        return ds.Tables[0].Rows[0];
    }

    private static async Task<LegacyStoredParams> ConsultarParametrosAsync(
        SqlConnection conn,
        int parametroId,
        CancellationToken ct)
    {
        var row = await ConsultarParametrosRowAsync(conn, parametroId, ct);
        var idReporte = ReadInt(row, "IDReporte");
        if (!IsAcumuladoresProductosReporte(idReporte))
            throw new InvalidOperationException("El psp no corresponde a Acumuladores y Productos.");

        return BuildAcumuladoresProductosStoredParams(row);
    }

    private static LegacyStoredParams BuildAcumuladoresProductosStoredParams(DataRow row)
    {
        var idReporte = ReadInt(row, "IDReporte");
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

    private static ReportesVentasRemisionesRequest BuildRemisionesStoredRequest(DataRow row)
    {
        return new ReportesVentasRemisionesRequest
        {
            TipoReporte = ResolveTipoReporteFromRemisionesParams(row),
            Documento = ResolveDocumento(ReadString(row, "Param4")),
            EstatusFolio = ResolveStatusFolioKey(ReadString(row, "Param7")),
            FechaInicial = ReadLegacyDate(row, "Param8"),
            FechaFinal = ReadLegacyDate(row, "Param9"),
            Salida = ResolveSalida(ReadString(row, "Param10")),
            SoloServiciosDomicilio = ReadString(row, "Param14") == ProductoServicioDomicilio.ToString(),
            IDEmpresas = SplitLegacyIds(ReadString(row, "Param2")),
            IDAlmacenes = SplitLegacyIds(ReadString(row, "Param3")),
            IDUsuarios = SplitLegacyIds(ReadString(row, "Param11")),
            IDAgentes = SplitLegacyIds(ReadString(row, "Param6")),
            IDClientes = SplitLegacyIds(ReadString(row, "Param5"))
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

    private static string ResolveTipoReporteFromRemisionesParams(DataRow row)
    {
        var clientes = SplitLegacyIds(ReadString(row, "Param5"));
        if (clientes.Any(id => id > 0))
            return "cliente";

        var agentes = SplitLegacyIds(ReadString(row, "Param6"));
        if (agentes.Any(id => id > 0))
            return "agente";

        return "empresa";
    }

    private static string ResolveStatusFolioKey(string? statusFolio)
    {
        return (statusFolio ?? "").Trim() switch
        {
            "1" => "vigentes",
            "2" => "cancelados",
            "4" => "saldo_favor",
            _ => "todos"
        };
    }
}
