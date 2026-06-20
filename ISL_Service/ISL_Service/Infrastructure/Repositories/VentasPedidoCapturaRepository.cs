using System.Data;
using System.Globalization;
using ISL_Service.Application.DTOs.VentasPedidoCaptura;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

public class VentasPedidoCapturaRepository : IVentasPedidoCapturaRepository
{
    private const int CommandTimeoutSeconds = 500;
    private readonly IConfiguration _configuration;

    public VentasPedidoCapturaRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<PedidoBootstrapResponse> BootstrapAsync(int idUsuario, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var fecha = await ConsultarFechaOperacionAsync(conn, ct);
        return new PedidoBootstrapResponse
        {
            Empresas = await ConsultarEmpresasAsync(conn, ct),
            Almacenes = await ConsultarUsuarioAlmacenesAsync(conn, idUsuario, ct),
            Agentes = await ConsultarAgentesAsync(conn, ct),
            FechaOperacion = fecha,
            Pedido = await ConsultarPedidoInicialAsync(conn, ct)
        };
    }

    public async Task<PedidoSnapshotDto> ConsultarPedidoAsync(int idPedido, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        return await ConsultarPedidoAsync(conn, idPedido, ct);
    }

    public async Task<PedidoClienteContextResponse> BuscarClienteAsync(PedidoClienteBuscarRequest request, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var clientes = await ConsultarClientesAsync(conn, request, ct);
        var idCliente = request.IDCliente > 0
            ? request.IDCliente
            : ReadInt(clientes.Rows.Count > 0 ? clientes.Rows[0] : null, "IDCliente", "idCliente");

        var domicilios = new List<Dictionary<string, object?>>();
        var saldos = DataTableToRows(CrearSaldosClienteFallbackTable());

        if (idCliente > 0)
        {
            domicilios = DataTableToRows(await ConsultarDomiciliosAsync(conn, idCliente, request.IDEmpresaCS, ct));
            saldos = DataTableToRows(await ConsultarSaldosClienteWebAsync(conn, idCliente, ct));
        }

        return new PedidoClienteContextResponse
        {
            Clientes = DataTableToRows(clientes),
            Domicilios = domicilios,
            Saldos = saldos
        };
    }

    public async Task<PedidoRowsResponse> BuscarProductoAsync(PedidoProductoBuscarRequest request, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = CreateStoredProcedureCommand("sp_n_ConsultaAlmacenProducto", conn);
        cmd.Parameters.AddWithValue("@IDAlmacenProducto", 0);
        cmd.Parameters.AddWithValue("@IDAlmacen", request.IDAlmacen);
        cmd.Parameters.AddWithValue("@IDProducto", request.IDProducto);
        cmd.Parameters.AddWithValue("@IDGrupoCategoria", request.IDGrupoCategoria);
        cmd.Parameters.AddWithValue("@IDCategoria", 0);
        cmd.Parameters.AddWithValue("@Clave", request.Clave ?? string.Empty);
        cmd.Parameters.AddWithValue("@Descripcion", string.Empty);
        cmd.Parameters.AddWithValue("@Formato", 0);
        cmd.Parameters.AddWithValue("@Identico", 0);
        cmd.Parameters.AddWithValue("@IDEmpresaCS", request.IDEmpresaCS);

        return new PedidoRowsResponse { Rows = DataTableToRows(await ExecuteFirstTableAsync(cmd, ct)) };
    }

    public async Task<PedidoSnapshotDto> AgregarDetalleAsync(PedidoAgregarDetalleRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var result = await ExecuteDataSetWithFallbackAsync(
            conn,
            "sp_w_InsertarPedidoDetalle",
            "sp_n_InsertarPedidoDetalle",
            cmd => AddInsertarDetalleParams(cmd, request, idUsuario, equipo),
            ct);

        var idPedido = request.IDPedido;
        if (result.Tables.Count > 0 && result.Tables[0].Rows.Count > 0)
        {
            var row = result.Tables[0].Rows[0];
            var legacyResult = ReadInt(row, "result", "Result");
            if (legacyResult == 0)
            {
                var message = ReadString(row, "mensaje", "Mensaje");
                if (!string.IsNullOrWhiteSpace(message))
                    throw new ArgumentException(message.Replace("@@@@", Environment.NewLine + Environment.NewLine));
            }

            idPedido = ReadInt(row, "IDPedido", "idPedido");
        }

        return await ConsultarPedidoAsync(conn, idPedido, ct);
    }

    public async Task<PedidoSnapshotDto> EliminarDetalleAsync(PedidoEliminarDetalleRequest request, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var result = await ExecuteDataSetWithFallbackAsync(
            conn,
            "sp_w_EliminarPedidoDetalle",
            "sp_n_EliminarPedidoDetalle",
            cmd =>
            {
                cmd.Parameters.AddWithValue("@IDPedidoDetalle", request.IDPedidoDetalle);
                cmd.Parameters.AddWithValue("@Observaciones", request.Observaciones ?? string.Empty);
                cmd.Parameters.AddWithValue("@SinModificarObservaciones", request.SinModificarObservaciones);
            },
            ct);

        var idPedido = request.IDPedido;
        if (result.Tables.Count > 0 && result.Tables[0].Rows.Count > 0)
            idPedido = ReadInt(result.Tables[0].Rows[0], "IDPedido", "idPedido");

        return await ConsultarPedidoAsync(conn, idPedido, ct);
    }

    public async Task<PedidoSnapshotDto> GuardarAsync(PedidoGuardarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await ExecuteDataSetWithFallbackAsync(
            conn,
            "sp_w_ActualizarPedido",
            "sp_n_ActualizarPedido",
            cmd =>
            {
                cmd.Parameters.AddWithValue("@IDPedido", request.IDPedido);
                cmd.Parameters.AddWithValue("@IDDomicilio", request.IDDomicilio);
                cmd.Parameters.AddWithValue("@Productos", request.Productos);
                cmd.Parameters.AddWithValue("@TotalPagar", request.TotalPagar);
                cmd.Parameters.AddWithValue("@Observaciones", request.Observaciones ?? string.Empty);
                cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
                cmd.Parameters.AddWithValue("@IDAgenteAutoriza", request.IDAgenteAutoriza);
                cmd.Parameters.AddWithValue("@Equipo", equipo);
                cmd.Parameters.AddWithValue("@ServicioNombre", request.ServicioNombre ?? string.Empty);
                cmd.Parameters.AddWithValue("@ServicioDireccion1", request.ServicioDireccion1 ?? string.Empty);
                cmd.Parameters.AddWithValue("@ServicioDireccion2", request.ServicioDireccion2 ?? string.Empty);
                cmd.Parameters.AddWithValue("@ServicioReferencia", request.ServicioReferencia ?? string.Empty);
                cmd.Parameters.AddWithValue("@ServicioTelefono", request.ServicioTelefono ?? string.Empty);
                cmd.Parameters.AddWithValue("@SoloLogistica", request.SoloLogistica);
                cmd.Parameters.AddWithValue("@PedidoSinCascosCambio", request.PedidoSinCascosCambio);
            },
            ct);

        return await ConsultarPedidoAsync(conn, request.IDPedido, ct);
    }

    public async Task<PedidoRowsResponse> EliminarBorradorAsync(int idUsuario, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        var result = await ExecuteDataSetWithFallbackAsync(
            conn,
            "sp_w_EliminarPedidoBorrador",
            "sp_n_EliminarPedido",
            cmd => cmd.Parameters.AddWithValue("@IDUsuario", idUsuario),
            ct);

        return new PedidoRowsResponse
        {
            Rows = result.Tables.Count > 0 ? DataTableToRows(result.Tables[0]) : new()
        };
    }

    private async Task<PedidoSnapshotDto> ConsultarPedidoAsync(SqlConnection conn, int idPedido, CancellationToken ct)
    {
        var ds = await ExecuteDataSetWithFallbackAsync(
            conn,
            "sp_w_ConsultaPedido",
            "sp_n_ConsultaPedido",
            cmd =>
            {
                cmd.Parameters.AddWithValue("@IDPedido", idPedido);
                cmd.Parameters.AddWithValue("@SoloDetalles", 0);
                cmd.Parameters.AddWithValue("@SoloCargos", 0);
                cmd.Parameters.AddWithValue("@SoloCreditos", 0);
                cmd.Parameters.AddWithValue("@SoloTotales", 0);
            },
            ct);

        return new PedidoSnapshotDto
        {
            IDPedido = idPedido,
            Detalles = ds.Tables.Count > 0 ? DataTableToRows(ds.Tables[0]) : new(),
            CascosCargo = ds.Tables.Count > 1 ? DataTableToRows(ds.Tables[1]) : new(),
            CascosCredito = ds.Tables.Count > 2 ? DataTableToRows(ds.Tables[2]) : new(),
            Totales = ds.Tables.Count > 3 && ds.Tables[3].Rows.Count > 0
                ? DataRowToDictionary(ds.Tables[3].Rows[0])
                : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        };
    }

    private static async Task<PedidoSnapshotDto> ConsultarPedidoInicialAsync(SqlConnection conn, CancellationToken ct)
    {
        var pedido = await ConsultarPedidoInicialLegacyAsync(conn, ct);
        if (pedido.CascosCargo.Count > 0) return pedido;

        return new PedidoSnapshotDto
        {
            CascosCargo = await ConsultarCascosCatalogoAsync(conn, ct)
        };
    }

    private static async Task<PedidoSnapshotDto> ConsultarPedidoInicialLegacyAsync(SqlConnection conn, CancellationToken ct)
    {
        var ds = await ExecuteDataSetWithFallbackAsync(
            conn,
            "sp_w_ConsultaPedido",
            "sp_n_ConsultaPedido",
            cmd =>
            {
                cmd.Parameters.AddWithValue("@IDPedido", 0);
                cmd.Parameters.AddWithValue("@SoloDetalles", 0);
                cmd.Parameters.AddWithValue("@SoloCargos", 0);
                cmd.Parameters.AddWithValue("@SoloCreditos", 0);
                cmd.Parameters.AddWithValue("@SoloTotales", 0);
            },
            ct);

        return new PedidoSnapshotDto
        {
            Detalles = ds.Tables.Count > 0 ? DataTableToRows(ds.Tables[0]) : new(),
            CascosCargo = ds.Tables.Count > 1 ? DataTableToRows(ds.Tables[1]) : new(),
            CascosCredito = ds.Tables.Count > 2 ? DataTableToRows(ds.Tables[2]) : new(),
            Totales = ds.Tables.Count > 3 && ds.Tables[3].Rows.Count > 0
                ? DataRowToDictionary(ds.Tables[3].Rows[0])
                : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        };
    }

    private static async Task<List<Dictionary<string, object?>>> ConsultarCascosCatalogoAsync(SqlConnection conn, CancellationToken ct)
    {
        var porcentajeIva = await ConsultarPorcentajeIvaAsync(conn, ct);
        await using var cmd = CreateStoredProcedureCommand("sp_n_ConsultaTiposUsados", conn);
        cmd.Parameters.AddWithValue("@IDTipoUsado", 0);
        cmd.Parameters.AddWithValue("@IDStatus", 1);
        cmd.Parameters.AddWithValue("@TipoUsado", string.Empty);
        cmd.Parameters.AddWithValue("@Formato", 0);
        cmd.Parameters.AddWithValue("@Identico", 0);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return table.AsEnumerable()
            .Select(row => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["IDTipoUsado"] = ReadInt(row, "IDTipoUsado", "idTipoUsado"),
                ["TipoUsado"] = NormalizeTipoUsado(ReadString(row, "TipoUsadoCorto", "tipoUsadoCorto", "TipoUsado", "tipoUsado")),
                ["CostoVentaCargoConIva"] = ReadCostoCargoConIva(row, porcentajeIva),
                ["ClienteBonificacion"] = 0m,
                ["Cantidad"] = 0,
                ["ImporteClienteBonificacion"] = 0m,
                ["ImporteVentaCargoConIva"] = 0m,
                ["Orden"] = ReadInt(row, "Orden", "orden")
            })
            .Where(row => ReadIntFromDictionary(row, "IDTipoUsado") > 0 && !string.IsNullOrWhiteSpace(Convert.ToString(row["TipoUsado"], CultureInfo.InvariantCulture)))
            .OrderBy(row => ReadIntFromDictionary(row, "Orden"))
            .ToList();
    }

    private static async Task<decimal> ConsultarPorcentajeIvaAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = CreateStoredProcedureCommand("sp_n_ConsultaConstantes", conn);
        cmd.Parameters.AddWithValue("@ValidaActividad", 0);
        cmd.Parameters.AddWithValue("@IDUsuario", 0);
        cmd.Parameters.AddWithValue("@Equipo", string.Empty);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        if (table.Rows.Count == 0) return 0m;
        return ReadDecimal(table.Rows[0], "PorcentajeIVA", "porcentajeIVA", "IVA", "iva");
    }

    private static decimal ReadCostoCargoConIva(DataRow row, decimal porcentajeIva)
    {
        var costoConIva = ReadDecimal(row, "CostoVentaCargoConIva", "costoVentaCargoConIva", "VentaCostoCargoConIva", "ventaCostoCargoConIva", "CostoCargoConIva", "costoCargoConIva");
        if (costoConIva > 0m) return costoConIva;

        var costoSinIva = ReadDecimal(row, "CostoVentaCargo", "costoVentaCargo", "VentaCostoCargoSinIva", "ventaCostoCargoSinIva");
        if (costoSinIva <= 0m || porcentajeIva <= 0m) return costoSinIva;

        var ivaFactor = porcentajeIva > 1m ? porcentajeIva / 100m : porcentajeIva;
        return Math.Round(costoSinIva * (1m + ivaFactor), 2, MidpointRounding.AwayFromZero);
    }

    private static void AddInsertarDetalleParams(SqlCommand cmd, PedidoAgregarDetalleRequest request, int idUsuario, string equipo)
    {
        cmd.Parameters.AddWithValue("@IDPedido", request.IDPedido);
        cmd.Parameters.AddWithValue("@IDEmpresa", request.IDEmpresa);
        cmd.Parameters.AddWithValue("@IDCliente", request.IDCliente);
        cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
        cmd.Parameters.AddWithValue("@IDAgenteAutoriza", request.IDAgenteAutoriza);
        cmd.Parameters.AddWithValue("@IDDomicilio", request.IDDomicilio);
        cmd.Parameters.AddWithValue("@IDTipoDocumento", request.IDTipoDocumento);
        cmd.Parameters.AddWithValue("@Fecha", request.Fecha ?? string.Empty);
        cmd.Parameters.AddWithValue("@IDAlmacen", request.IDAlmacen);
        cmd.Parameters.AddWithValue("@IDProducto", request.IDProducto);
        cmd.Parameters.AddWithValue("@Cantidad", request.Cantidad);
        cmd.Parameters.AddWithValue("@SaldoVencido", request.SaldoVencido);
        cmd.Parameters.AddWithValue("@SaldoPendiente", request.SaldoPendiente);
        cmd.Parameters.AddWithValue("@Disponible", request.Disponible);
        cmd.Parameters.AddWithValue("@MaxDiasVencidos", request.MaxDiasVencidos);
        cmd.Parameters.AddWithValue("@MaxDiasVencidosAcumuladores", request.MaxDiasVencidosAcumuladores);
        cmd.Parameters.AddWithValue("@MaxDiasVencidosAceites", request.MaxDiasVencidosAceites);
        cmd.Parameters.AddWithValue("@MaxDiasVencidosCascos", request.MaxDiasVencidosCascos);
        cmd.Parameters.AddWithValue("@Observaciones", request.Observaciones ?? string.Empty);
        cmd.Parameters.AddWithValue("@ObservacionesDetalle", request.ObservacionesDetalle ?? string.Empty);
        cmd.Parameters.AddWithValue("@IDGarantia", 0);
        cmd.Parameters.AddWithValue("@AjustePorcentaje", 0);
        cmd.Parameters.AddWithValue("@AjusteMonto", 0);
        cmd.Parameters.AddWithValue("@AjusteTicket", string.Empty);
        cmd.Parameters.AddWithValue("@AjusteVigencia", string.Empty);
        cmd.Parameters.AddWithValue("@AjusteGarantia", string.Empty);
        cmd.Parameters.AddWithValue("@AjusteDiagnostico", string.Empty);
        cmd.Parameters.AddWithValue("@Factura", string.Empty);
        cmd.Parameters.AddWithValue("@FechaEmision", string.Empty);
        cmd.Parameters.AddWithValue("@FechaVencimiento", string.Empty);
        cmd.Parameters.AddWithValue("@IDProveedor", 0);
        cmd.Parameters.AddWithValue("@Equipo", equipo);
        cmd.Parameters.AddWithValue("@ServicioPrecioConIva", request.ServicioPrecioConIva);
        cmd.Parameters.AddWithValue("@ServicioNombre", request.ServicioNombre ?? string.Empty);
        cmd.Parameters.AddWithValue("@ServicioDireccion1", request.ServicioDireccion1 ?? string.Empty);
        cmd.Parameters.AddWithValue("@ServicioDireccion2", request.ServicioDireccion2 ?? string.Empty);
        cmd.Parameters.AddWithValue("@ServicioReferencia", request.ServicioReferencia ?? string.Empty);
        cmd.Parameters.AddWithValue("@ServicioTelefono", request.ServicioTelefono ?? string.Empty);
        cmd.Parameters.AddWithValue("@SoloAceites", request.SoloAceites);
        cmd.Parameters.AddWithValue("@SoloServicios", request.SoloServicios);
        cmd.Parameters.AddWithValue("@SoloLogistica", request.SoloLogistica);
        cmd.Parameters.AddWithValue("@Facturar", request.Facturar);
        cmd.Parameters.AddWithValue("@Simular", request.Simular);
        cmd.Parameters.AddWithValue("@IDDescuentoSimular", request.IDDescuentoSimular);
        cmd.Parameters.AddWithValue("@PedidoSinCascosCambio", request.PedidoSinCascosCambio);
        cmd.Parameters.AddWithValue("@Tipo", request.Tipo);
        cmd.Parameters.AddWithValue("@IDEmpresaCS", request.IDEmpresaCS);
        cmd.Parameters.AddWithValue("@FacturaMayorista", request.FacturaMayorista);
        cmd.Parameters.AddWithValue("@RemisionMayorista", request.RemisionMayorista);
        cmd.Parameters.AddWithValue("@DescuentoAdicional", request.DescuentoAdicional);
    }

    private async Task<DataTable> ConsultarClientesAsync(SqlConnection conn, PedidoClienteBuscarRequest request, CancellationToken ct)
    {
        await using var cmd = CreateStoredProcedureCommand("sp_n_ConsultaClientes", conn);
        cmd.Parameters.AddWithValue("@IDCliente", request.IDCliente);
        cmd.Parameters.AddWithValue("@IDCondicionesCredito", 0);
        cmd.Parameters.AddWithValue("@IDCondicionesCreditoAceites", 0);
        cmd.Parameters.AddWithValue("@IDCondicionesCreditoCascos", 0);
        cmd.Parameters.AddWithValue("@IDDescuento", 0);
        cmd.Parameters.AddWithValue("@IDAgente", 0);
        cmd.Parameters.AddWithValue("@IDStatus", 1);
        cmd.Parameters.AddWithValue("@Numero", request.Numero ?? string.Empty);
        cmd.Parameters.AddWithValue("@Nombre", string.Empty);
        cmd.Parameters.AddWithValue("@ApellidoPaterno", string.Empty);
        cmd.Parameters.AddWithValue("@ApellidoMaterno", string.Empty);
        cmd.Parameters.AddWithValue("@RFC", string.Empty);
        cmd.Parameters.AddWithValue("@IDTipoCliente", 0);
        cmd.Parameters.AddWithValue("@ConHuella", 0);
        cmd.Parameters.AddWithValue("@IDEmpresaCS", request.IDEmpresaCS);
        cmd.Parameters.AddWithValue("@EsMostrador", 0);
        cmd.Parameters.AddWithValue("@Referencia", string.Empty);
        cmd.Parameters.AddWithValue("@Formato", 0);
        cmd.Parameters.AddWithValue("@Identico", request.IDCliente > 0 || !string.IsNullOrWhiteSpace(request.Numero) ? 1 : 0);
        return await ExecuteFirstTableAsync(cmd, ct);
    }

    private static async Task<DataTable> ConsultarDomiciliosAsync(SqlConnection conn, int idCliente, int idEmpresaCs, CancellationToken ct)
    {
        await using var cmd = CreateStoredProcedureCommand("sp_n_ConsultaDomicilios", conn);
        cmd.Parameters.AddWithValue("@IDCliente", idCliente);
        cmd.Parameters.AddWithValue("@IDEmpresaCS", idEmpresaCs);
        return await ExecuteFirstTableAsync(cmd, ct);
    }

    private static DataTable CrearSaldosClienteFallbackTable()
    {
        var table = new DataTable();
        table.Columns.Add("saldoPendiente", typeof(decimal));
        table.Columns.Add("saldoVencido", typeof(decimal));
        table.Columns.Add("saldo", typeof(decimal));
        table.Columns.Add("diasVencimientoMostrar", typeof(string));
        table.Rows.Add(0m, 0m, 0m, "0/0/0");
        return table;
    }

    private async Task<DataTable> ConsultarSaldosClienteWebAsync(SqlConnection conn, int idCliente, CancellationToken ct)
    {
        var fecha = await ConsultarFechaOperacionAsync(conn, ct);
        var fechaOperacion = DateTime.TryParse(fecha.FechaOperacion, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.Date
            : DateTime.Today;
        var fechaFinal = fechaOperacion.AddDays(1).AddTicks(-1);

        const string sql = """
            DECLARE @Funcionalidad varchar(100) = '';
            SELECT TOP 1 @Funcionalidad = UPPER(ISNULL(Funcionalidad, '')) FROM Constantes;

            ;WITH Base AS
            (
                SELECT
                    V.IDVenta,
                    V.IDCliente,
                    V.IDTipoDocumento,
                    V.SoloAceites,
                    V.[Fecha de Vencimiento] AS FechaVencimiento,
                    V.FechaVencimientoAceites,
                    V.FechaVencimientoCascos,
                    V.[Fecha Pago] AS FechaPago,
                    CAST(ISNULL(V.Cargos, 0) AS decimal(18, 2)) AS Cargos,
                    CAST(ISNULL(V.Abonos, 0) AS decimal(18, 2)) AS Abonos,
                    CAST(ISNULL(V.Descuentos, 0) AS decimal(18, 2)) AS Descuentos,
                    CAST(ISNULL(V.Creditos, 0) AS decimal(18, 2)) AS Creditos,
                    CAST(ISNULL(V.[Iva Creditos], 0) AS decimal(18, 2)) AS IvaCreditos,
                    CAST(ISNULL(V.TotalCostoUsadoCargoConIva, 0) AS decimal(18, 2)) AS Cascos,
                    CAST(CASE WHEN @Funcionalidad = 'ZARA' THEN ISNULL(V.ImporteConIvaRnd, V.Importe) ELSE ISNULL(V.Importe, 0) END AS decimal(18, 2)) AS Importe,
                    CASE WHEN V.[Fecha Pago] IS NOT NULL THEN 0 ELSE DATEDIFF(day, V.[Fecha de Vencimiento], @FechaOperacion) END AS DiasVencimiento,
                    CASE WHEN V.[Fecha Pago] IS NOT NULL AND ISNULL(V.SoloAceites, 0) = 0 THEN 0 ELSE DATEDIFF(day, V.[Fecha de Vencimiento], @FechaOperacion) END AS DiasVencimientoAcumuladores,
                    CASE WHEN V.[Fecha Pago] IS NOT NULL AND ISNULL(V.SoloAceites, 0) = 1 THEN 0 ELSE DATEDIFF(day, V.FechaVencimientoAceites, @FechaOperacion) END AS DiasVencimientoAceites,
                    CASE WHEN V.[Fecha Pago] IS NOT NULL AND ISNULL(V.SoloAceites, 0) = 0 THEN 0 ELSE DATEDIFF(day, V.FechaVencimientoCascos, @FechaOperacion) END AS DiasVencimientoCascos
                FROM Ventas V
                WHERE V.IDCliente = @IDCliente
                  AND V.[Fecha Cancelacion] IS NULL
                  AND V.[Fecha Pago] IS NULL
                  AND V.[Fecha de Emision] BETWEEN @FechaInicial AND @FechaFinal
            ),
            PagosDiff AS
            (
                SELECT VP.IDVenta, CAST(SUM(ISNULL(VP.Abono, 0)) AS decimal(18, 2)) AS PagosDiffUsados
                FROM [Ventas Pagos Usados] VP
                INNER JOIN Base B ON B.IDVenta = VP.IDVenta
                WHERE VP.[Fecha Cancelacion] IS NULL
                GROUP BY VP.IDVenta
            ),
            Saldos AS
            (
                SELECT
                    B.*,
                    CAST(ISNULL(P.PagosDiffUsados, 0) AS decimal(18, 2)) AS PagosDiffUsados,
                    CAST(
                        CASE WHEN @Funcionalidad = 'ZARA' AND EXISTS (SELECT 1 FROM CentrosServicio CS WHERE CS.IDCliente = B.IDCliente)
                            THEN 0
                            ELSE B.Importe + B.Cargos - B.Descuentos - B.Abonos
                        END AS decimal(18, 2)
                    ) AS SaldoDinero,
                    CAST(
                        CASE WHEN @Funcionalidad = 'ZARA' AND EXISTS (SELECT 1 FROM CentrosServicio CS WHERE CS.IDCliente = B.IDCliente)
                            THEN 0
                            ELSE
                                CASE
                                    WHEN (
                                        CASE WHEN @Funcionalidad = 'ZARA'
                                            THEN CASE WHEN B.IDTipoDocumento = 6
                                                THEN B.Cascos - ISNULL(P.PagosDiffUsados, 0)
                                                ELSE B.Cascos - B.Creditos - B.IvaCreditos - ISNULL(P.PagosDiffUsados, 0)
                                            END
                                            ELSE B.Cascos - B.Creditos - B.IvaCreditos
                                        END
                                    ) < 0 THEN 0
                                    WHEN (
                                        CASE WHEN @Funcionalidad = 'ZARA'
                                            THEN CASE WHEN B.IDTipoDocumento = 6
                                                THEN B.Cascos - ISNULL(P.PagosDiffUsados, 0)
                                                ELSE B.Cascos - B.Creditos - B.IvaCreditos - ISNULL(P.PagosDiffUsados, 0)
                                            END
                                            ELSE B.Cascos - B.Creditos - B.IvaCreditos
                                        END
                                    ) BETWEEN 0.01 AND 0.05 THEN 0
                                    ELSE
                                        CASE WHEN @Funcionalidad = 'ZARA'
                                            THEN CASE WHEN B.IDTipoDocumento = 6
                                                THEN B.Cascos - ISNULL(P.PagosDiffUsados, 0)
                                                ELSE B.Cascos - B.Creditos - B.IvaCreditos - ISNULL(P.PagosDiffUsados, 0)
                                            END
                                            ELSE B.Cascos - B.Creditos - B.IvaCreditos
                                        END
                                END
                        END AS decimal(18, 2)
                    ) AS SaldoCascos
                FROM Base B
                LEFT JOIN PagosDiff P ON P.IDVenta = B.IDVenta
            ),
            Final AS
            (
                SELECT
                    *,
                    CAST(
                        CASE WHEN @Funcionalidad = 'ZARA'
                            THEN SaldoDinero + SaldoCascos
                            ELSE CASE WHEN IDTipoDocumento = 6 THEN SaldoDinero - Creditos - IvaCreditos ELSE SaldoDinero + SaldoCascos END
                        END AS decimal(18, 2)
                    ) AS SaldoTotal
                FROM Saldos
            )
            SELECT
                CAST(ISNULL(SUM(CASE WHEN DiasVencimiento > 0 THEN SaldoTotal ELSE 0 END), 0) AS decimal(18, 2)) AS saldoVencido,
                CAST(ISNULL(SUM(CASE WHEN DiasVencimiento <= 0 THEN SaldoTotal ELSE 0 END), 0) AS decimal(18, 2)) AS saldoPendiente,
                CAST(ISNULL(SUM(SaldoTotal), 0) AS decimal(18, 2)) AS saldo,
                CAST(ISNULL(MAX(CASE WHEN DiasVencimiento > 0 THEN DiasVencimiento ELSE 0 END), 0) AS int) AS maxDiasVencidos,
                CAST(ISNULL(MAX(CASE WHEN SoloAceites = 0 AND SaldoDinero > 0 AND DiasVencimientoAcumuladores > 0 THEN DiasVencimientoAcumuladores ELSE 0 END), 0) AS int) AS maxDiasVencidosAcumuladores,
                CAST(ISNULL(MAX(CASE WHEN SoloAceites = 0 AND SaldoCascos > 0 AND DiasVencimientoCascos > 0 THEN DiasVencimientoCascos ELSE 0 END), 0) AS int) AS maxDiasVencidosCascos,
                CAST(ISNULL(MAX(CASE WHEN SoloAceites = 1 AND DiasVencimientoAceites > 0 THEN DiasVencimientoAceites ELSE 0 END), 0) AS int) AS maxDiasVencidosAceites,
                CAST(
                    CASE WHEN @Funcionalidad = 'ZARA'
                        THEN CONCAT(
                            ISNULL(MAX(CASE WHEN SoloAceites = 0 AND SaldoDinero > 0 AND DiasVencimientoAcumuladores > 0 THEN DiasVencimientoAcumuladores ELSE 0 END), 0),
                            '/',
                            ISNULL(MAX(CASE WHEN SoloAceites = 0 AND SaldoCascos > 0 AND DiasVencimientoCascos > 0 THEN DiasVencimientoCascos ELSE 0 END), 0),
                            '/',
                            ISNULL(MAX(CASE WHEN SoloAceites = 1 AND DiasVencimientoAceites > 0 THEN DiasVencimientoAceites ELSE 0 END), 0)
                        )
                        ELSE CAST(ISNULL(MAX(CASE WHEN DiasVencimiento > 0 THEN DiasVencimiento ELSE 0 END), 0) AS varchar(20))
                    END AS varchar(100)
                ) AS diasVencimientoMostrar
            FROM Final;
            """;

        await using var cmd = new SqlCommand(sql, conn)
        {
            CommandType = CommandType.Text,
            CommandTimeout = CommandTimeoutSeconds
        };
        cmd.Parameters.Add("@IDCliente", SqlDbType.Int).Value = idCliente;
        cmd.Parameters.Add("@FechaOperacion", SqlDbType.DateTime).Value = fechaOperacion;
        cmd.Parameters.Add("@FechaFinal", SqlDbType.DateTime).Value = fechaFinal;
        cmd.Parameters.Add("@FechaInicial", SqlDbType.DateTime).Value = new DateTime(2023, 1, 1);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return table.Rows.Count > 0 ? table : CrearSaldosClienteFallbackTable();
    }

    private static async Task<List<PedidoCatalogoItemDto>> ConsultarUsuarioAlmacenesAsync(SqlConnection conn, int idUsuario, CancellationToken ct)
    {
        if (idUsuario <= 0) return await ConsultarAlmacenesAsync(conn, ct);

        await using var cmd = CreateStoredProcedureCommand("sp_n_ConsultaUsuariosAlmacenes", conn);
        cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
        var table = await ExecuteFirstTableAsync(cmd, ct);
        var rows = table.AsEnumerable()
            .Where(row => ReadBool(row, "Acceso", "acceso"))
            .Select(row => new PedidoCatalogoItemDto
            {
                Id = ReadInt(row, "IDAlmacen", "idAlmacen"),
                Nombre = ReadString(row, "Almacen", "almacen"),
                Clave = ReadString(row, "Almacen", "almacen")
            })
            .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Nombre))
            .OrderBy(x => x.Nombre)
            .ToList();

        return rows.Count > 0 ? rows : await ConsultarAlmacenesAsync(conn, ct);
    }

    private static async Task<List<PedidoCatalogoItemDto>> ConsultarEmpresasAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = CreateStoredProcedureCommand("sp_n_ConsultaEmpresas", conn);
        cmd.Parameters.AddWithValue("@IDEmpresa", 0);
        cmd.Parameters.AddWithValue("@IDStatus", 1);
        cmd.Parameters.AddWithValue("@RazonSocial", string.Empty);
        cmd.Parameters.AddWithValue("@Formato", 0);
        cmd.Parameters.AddWithValue("@Identico", 0);
        var table = await ExecuteFirstTableAsync(cmd, ct);
        return CatalogFromTable(table, "IDEmpresa", "NombreCorto", "RazonSocial");
    }

    private static async Task<List<PedidoCatalogoItemDto>> ConsultarAlmacenesAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = CreateStoredProcedureCommand("sp_n_ConsultaAlmacenes", conn);
        cmd.Parameters.AddWithValue("@IDAlmacen", 0);
        cmd.Parameters.AddWithValue("@IDStatus", 1);
        cmd.Parameters.AddWithValue("@Almacen", string.Empty);
        cmd.Parameters.AddWithValue("@Identico", 0);
        var table = await ExecuteFirstTableAsync(cmd, ct);
        return CatalogFromTable(table, "IDAlmacen", "Almacen", "Almacen");
    }

    private static async Task<List<PedidoCatalogoItemDto>> ConsultarAgentesAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = CreateStoredProcedureCommand("sp_n_ConsultaAgentes", conn);
        cmd.Parameters.AddWithValue("@IDAgente", 0);
        cmd.Parameters.AddWithValue("@IDStatus", 1);
        cmd.Parameters.AddWithValue("@NumeroAgente", 0);
        cmd.Parameters.AddWithValue("@NombreAgente", string.Empty);
        cmd.Parameters.AddWithValue("@Formato", 0);
        cmd.Parameters.AddWithValue("@Identico", 0);
        var table = await ExecuteFirstTableAsync(cmd, ct);
        return CatalogFromTable(table, "IDAgente", "NombreAgente", "NumeroAgente").OrderBy(x => x.Nombre).ToList();
    }

    private static async Task<PedidoFechaOperacionDto> ConsultarFechaOperacionAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = CreateStoredProcedureCommand("sp_n_ConsultaConstantes", conn);
        cmd.Parameters.AddWithValue("@ValidaActividad", 0);
        cmd.Parameters.AddWithValue("@IDUsuario", 0);
        cmd.Parameters.AddWithValue("@Equipo", string.Empty);
        var table = await ExecuteFirstTableAsync(cmd, ct);
        if (table.Rows.Count == 0) return new PedidoFechaOperacionDto();

        var row = table.Rows[0];
        var fecha = ReadDate(row, "FechaOperacion");
        return new PedidoFechaOperacionDto
        {
            FechaOperacion = fecha?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
            FechaOperacionFtm = ReadString(row, "FechaOperacionFtm"),
            FechaOperacionTexto = fecha.HasValue
                ? CultureInfo.GetCultureInfo("es-MX").TextInfo.ToTitleCase(fecha.Value.ToString("dddd dd / MMMM / yyyy", CultureInfo.GetCultureInfo("es-MX")))
                : ReadString(row, "FechaOperacionFtm")
        };
    }

    private static List<PedidoCatalogoItemDto> CatalogFromTable(DataTable table, string idColumn, string nameColumn, string fallbackNameColumn)
    {
        return table.AsEnumerable()
            .Select(row => new PedidoCatalogoItemDto
            {
                Id = ReadInt(row, idColumn),
                Nombre = ReadString(row, nameColumn, fallbackNameColumn),
                Clave = ReadString(row, fallbackNameColumn, nameColumn)
            })
            .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Nombre))
            .ToList();
    }

    private SqlConnection GetConnection()
    {
        var cs = _configuration.GetConnectionString("Main")
            ?? _configuration.GetConnectionString("Mac3")
            ?? _configuration.GetConnectionString("Local")
            ?? _configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("ConnectionString (Main/Mac3/Local/Default) no encontrada.");
        return new Mac3SqlServerConnector(cs).GetConnection;
    }

    private static SqlCommand CreateStoredProcedureCommand(string sp, SqlConnection conn)
    {
        return new SqlCommand(sp, conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };
    }

    private static async Task<DataSet> ExecuteDataSetWithFallbackAsync(
        SqlConnection conn,
        string webSp,
        string legacySp,
        Action<SqlCommand> addParams,
        CancellationToken ct)
    {
        try
        {
            await using var cmd = CreateStoredProcedureCommand(webSp, conn);
            addParams(cmd);
            return await ExecuteDataSetAsync(cmd, ct);
        }
        catch (SqlException ex) when (IsMissingStoredProcedure(ex, webSp))
        {
            await using var fallbackCmd = CreateStoredProcedureCommand(legacySp, conn);
            addParams(fallbackCmd);
            return await ExecuteDataSetAsync(fallbackCmd, ct);
        }
    }

    private static bool IsMissingStoredProcedure(SqlException ex, string sp)
    {
        return ex.Number == 2812 || ex.Number == 208 || (ex.Message ?? "").Contains(sp, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<DataTable> ExecuteFirstTableAsync(SqlCommand cmd, CancellationToken ct)
    {
        var ds = await ExecuteDataSetAsync(cmd, ct);
        return ds.Tables.Count > 0 ? ds.Tables[0] : new DataTable();
    }

    private static async Task<DataSet> ExecuteDataSetAsync(SqlCommand cmd, CancellationToken ct)
    {
        var ds = new DataSet();
        using var adapter = new SqlDataAdapter(cmd);
        await Task.Run(() => adapter.Fill(ds), ct);
        return ds;
    }

    private static List<Dictionary<string, object?>> DataTableToRows(DataTable table)
    {
        return table.AsEnumerable().Select(DataRowToDictionary).ToList();
    }

    private static Dictionary<string, object?> DataRowToDictionary(DataRow row)
    {
        var item = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (DataColumn column in row.Table.Columns)
            item[column.ColumnName] = row[column] == DBNull.Value ? null : row[column];
        return item;
    }

    private static int ReadInt(DataRow? row, params string[] names)
    {
        if (row == null) return 0;
        foreach (var name in names)
        {
            if (!row.Table.Columns.Contains(name)) continue;
            var value = row[name];
            if (value == DBNull.Value || value == null) continue;
            if (int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var parsed)) return parsed;
        }
        return 0;
    }

    private static string ReadString(DataRow row, params string[] names)
    {
        foreach (var name in names)
        {
            if (!row.Table.Columns.Contains(name)) continue;
            var value = row[name];
            if (value == DBNull.Value || value == null) continue;
            var text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }
        return string.Empty;
    }

    private static bool ReadBool(DataRow row, params string[] names)
    {
        foreach (var name in names)
        {
            if (!row.Table.Columns.Contains(name)) continue;
            var value = row[name];
            if (value == DBNull.Value || value == null) continue;
            if (value is bool b) return b;
            if (int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var i)) return i != 0;
            if (bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var parsed)) return parsed;
        }
        return false;
    }

    private static DateTime? ReadDate(DataRow row, params string[] names)
    {
        foreach (var name in names)
        {
            if (!row.Table.Columns.Contains(name)) continue;
            var value = row[name];
            if (value == DBNull.Value || value == null) continue;
            if (value is DateTime dt) return dt;
            if (DateTime.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var parsed)) return parsed;
        }
        return null;
    }

    private static decimal ReadDecimal(DataRow row, params string[] names)
    {
        foreach (var name in names)
        {
            if (!row.Table.Columns.Contains(name)) continue;
            var value = row[name];
            if (value == DBNull.Value || value == null) continue;
            if (value is decimal dec) return dec;
            if (decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        }
        return 0m;
    }

    private static int ReadIntFromDictionary(Dictionary<string, object?> row, string name)
    {
        return row.TryGetValue(name, out var value) && int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var parsed)
            ? parsed
            : 0;
    }

    private static string NormalizeTipoUsado(string value)
    {
        var text = value.Trim();
        if (text.StartsWith("GRUPO ", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(text["GRUPO ".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var group))
            return $"G{group}";

        if (text.Equals("MEDIO TANQUE", StringComparison.OrdinalIgnoreCase)) return "MT";
        return text;
    }
}
