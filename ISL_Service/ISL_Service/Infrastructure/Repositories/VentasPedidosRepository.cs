using System.Data;
using ISL_Service.Application.DTOs.VentasPedidos;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

public class VentasPedidosRepository : IVentasPedidosRepository
{
    private const string SpConsultaVentasPedidos = "sp_n_ConsultaVentasPedidos";
    private const string SpProcesarPedido = "sp_w_ProcesarPedido";
    private const string SpProcesarPedidoBatch = "sp_w_ProcesarPedidosBatch";
    private const string TvpPedidoIdsType = "dbo.WPedidoIdListType";

    private readonly IConfiguration _configuration;

    public VentasPedidosRepository(IConfiguration configuration)
    {
        _configuration = configuration;
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

    public async Task<List<VentaPedidoDto>> ConsultarPendientesAutorizarAsync(ConsultaVentasPedidosRequest request, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpConsultaVentasPedidos, conn)
        {
            CommandType = CommandType.StoredProcedure
        };

        cmd.Parameters.AddWithValue("@IDPedido", request.IDPedido);
        cmd.Parameters.AddWithValue("@IDVenta", request.IDVenta);
        cmd.Parameters.AddWithValue("@IDEmpresa", request.IDEmpresa);
        cmd.Parameters.AddWithValue("@IDCliente", request.IDCliente);
        cmd.Parameters.AddWithValue("@IDUsuario", request.IDUsuario);
        cmd.Parameters.AddWithValue("@IDAgente", request.IDAgente);
        cmd.Parameters.AddWithValue("@IDTipoDocumento", request.IDTipoDocumento);
        cmd.Parameters.AddWithValue("@IDStatusPedido", request.IDStatusPedido);
        cmd.Parameters.AddWithValue("@FolioInicial", request.FolioInicial ?? string.Empty);
        cmd.Parameters.AddWithValue("@FolioFinal", request.FolioFinal ?? string.Empty);
        cmd.Parameters.AddWithValue("@FechaInicial", request.FechaInicial ?? string.Empty);
        cmd.Parameters.AddWithValue("@FechaFinal", request.FechaFinal ?? string.Empty);
        cmd.Parameters.AddWithValue("@FechaCancelInicial", request.FechaCancelInicial ?? string.Empty);
        cmd.Parameters.AddWithValue("@FechaCancelFinal", request.FechaCancelFinal ?? string.Empty);
        cmd.Parameters.AddWithValue("@Formato", request.Formato);
        cmd.Parameters.AddWithValue("@IDUsuarioActual", request.IDUsuarioActual);

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd))
        {
            adapter.Fill(dt);
        }

        return Funciones.DataTableToList<VentaPedidoDto>(dt);
    }

    public async Task<AutorizarPedidosResponse> AutorizarPedidosAsync(
        AutorizarPedidosRequest request,
        int idUsuarioToken,
        string equipoToken,
        CancellationToken ct)
    {
        if (request == null || request.IdsPedido.Count == 0)
            return new AutorizarPedidosResponse(new List<PedidoResultadoDto>(), string.Empty);

        var idUsuarioProcesar = request.IdUsuarioProcesar
            ?? request.IdUsuarioAutorizar
            ?? request.IdUsuario
            ?? (idUsuarioToken > 0 ? idUsuarioToken : 0);
        var idUsuarioAutorizar = request.IdUsuarioAutorizar
            ?? request.IdUsuario
            ?? (idUsuarioToken > 0 ? idUsuarioToken : 0);

        var equipoProcesar = string.IsNullOrWhiteSpace(request.EquipoProcesar)
            ? (string.IsNullOrWhiteSpace(request.Equipo) ? equipoToken : request.Equipo!)
            : request.EquipoProcesar!;
        var equipoAutorizar = string.IsNullOrWhiteSpace(request.EquipoAutorizar)
            ? (string.IsNullOrWhiteSpace(request.Equipo) ? equipoToken : request.Equipo!)
            : request.EquipoAutorizar!;

        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        var idsPedido = request.IdsPedido
            .Where(x => x > 0)
            .Distinct()
            .ToList();
        if (idsPedido.Count == 0)
            return new AutorizarPedidosResponse(new List<PedidoResultadoDto>(), string.Empty);

        try
        {
            var batchResponse = await AutorizarPedidosBatchAsync(
                conn,
                idsPedido,
                request,
                idUsuarioProcesar,
                equipoProcesar,
                idUsuarioAutorizar,
                equipoAutorizar,
                ct);

            if (ShouldFallbackToPerPedidoByBatchContent(batchResponse))
            {
                return await AutorizarPedidosPerPedidoAsync(
                    conn,
                    idsPedido,
                    request,
                    idUsuarioProcesar,
                    equipoProcesar,
                    idUsuarioAutorizar,
                    equipoAutorizar,
                    ct);
            }

            return batchResponse;
        }
        catch (SqlException ex) when (IsBatchArtifactsMissing(ex))
        {
            // Fallback seguro web: si aun no instalaron scripts batch en DB, usa flujo por pedido web.
            return await AutorizarPedidosPerPedidoAsync(
                conn,
                idsPedido,
                request,
                idUsuarioProcesar,
                equipoProcesar,
                idUsuarioAutorizar,
                equipoAutorizar,
                ct);
        }
    }

    private static bool IsBatchArtifactsMissing(SqlException ex)
    {
        var msg = ex?.Message ?? string.Empty;
        return msg.Contains(SpProcesarPedidoBatch, StringComparison.OrdinalIgnoreCase)
               || msg.Contains(TvpPedidoIdsType, StringComparison.OrdinalIgnoreCase)
               || ex.Number is 208 or 2715 or 2812;
    }

    private static bool ShouldFallbackToPerPedidoByBatchContent(AutorizarPedidosResponse response)
    {
        if (response == null) return false;
        if (!string.IsNullOrWhiteSpace(response.IdsVenta)) return false;
        if (response.Pedidos == null || response.Pedidos.Count == 0) return false;

        // Error clÃ¡sico cuando el SP interno usa INSERT...EXEC y el batch intenta hacer otro INSERT...EXEC.
        return response.Pedidos.All(p =>
            p?.Result == -1
            && !string.IsNullOrWhiteSpace(p.Mensaje)
            && p.Mensaje.Contains("INSERT EXEC", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<AutorizarPedidosResponse> AutorizarPedidosBatchAsync(
        SqlConnection conn,
        List<int> idsPedido,
        AutorizarPedidosRequest request,
        int idUsuarioProcesar,
        string equipoProcesar,
        int idUsuarioAutorizar,
        string equipoAutorizar,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand(SpProcesarPedidoBatch, conn)
        {
            CommandType = CommandType.StoredProcedure
        };

        var tvp = BuildPedidoIdsTvp(idsPedido);
        var pIds = cmd.Parameters.AddWithValue("@IdsPedido", tvp);
        pIds.SqlDbType = SqlDbType.Structured;
        pIds.TypeName = TvpPedidoIdsType;

        AddAutorizarCommonParameters(
            cmd,
            request,
            idUsuarioProcesar,
            equipoProcesar,
            idUsuarioAutorizar,
            equipoAutorizar);

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd))
        {
            adapter.Fill(dt);
        }

        return MapAutorizarResponse(dt, idsPedido);
    }

    private async Task<AutorizarPedidosResponse> AutorizarPedidosPerPedidoAsync(
        SqlConnection conn,
        List<int> idsPedido,
        AutorizarPedidosRequest request,
        int idUsuarioProcesar,
        string equipoProcesar,
        int idUsuarioAutorizar,
        string equipoAutorizar,
        CancellationToken ct)
    {
        var rows = new DataTable();
        rows.Columns.Add("result", typeof(int));
        rows.Columns.Add("mensaje", typeof(string));
        rows.Columns.Add("IDPedido", typeof(int));
        rows.Columns.Add("Pedido", typeof(int));
        rows.Columns.Add("IDVenta", typeof(int));

        foreach (var idPedido in idsPedido)
        {
            ct.ThrowIfCancellationRequested();

            await using var cmd = new SqlCommand(SpProcesarPedido, conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@IDPedido", idPedido);
            AddAutorizarCommonParameters(
                cmd,
                request,
                idUsuarioProcesar,
                equipoProcesar,
                idUsuarioAutorizar,
                equipoAutorizar);

            var dt = new DataTable();
            using (var adapter = new SqlDataAdapter(cmd))
            {
                adapter.Fill(dt);
            }

            if (dt.Rows.Count == 0)
            {
                rows.Rows.Add(-1, "Sin respuesta del procedimiento.", idPedido, 0, 0);
                continue;
            }

            var first = dt.Rows[0];
            var result = dt.Columns.Contains("result") ? Convert.ToInt32(first["result"]) : -1;
            var mensaje = dt.Columns.Contains("mensaje") ? Convert.ToString(first["mensaje"]) : "Sin respuesta del procedimiento.";
            var pedido = dt.Columns.Contains("Pedido") ? Convert.ToInt32(first["Pedido"]) : 0;
            var idVenta = dt.Columns.Contains("IDVenta") ? Convert.ToInt32(first["IDVenta"]) : 0;
            rows.Rows.Add(result, mensaje ?? string.Empty, idPedido, pedido, idVenta);
        }

        return MapAutorizarResponse(rows, idsPedido);
    }

    private static void AddAutorizarCommonParameters(
        SqlCommand cmd,
        AutorizarPedidosRequest request,
        int idUsuarioProcesar,
        string equipoProcesar,
        int idUsuarioAutorizar,
        string equipoAutorizar)
    {
        cmd.Parameters.AddWithValue("@IDUsuarioProcesar", idUsuarioProcesar);
        cmd.Parameters.AddWithValue("@EquipoProcesar", equipoProcesar ?? string.Empty);
        cmd.Parameters.AddWithValue("@IDUsuarioAutorizar", idUsuarioAutorizar);
        cmd.Parameters.AddWithValue("@EquipoAutorizar", equipoAutorizar ?? string.Empty);
        cmd.Parameters.AddWithValue("@IDUsuarioRechazar", 0);
        cmd.Parameters.AddWithValue("@EquipoRechazar", string.Empty);

        cmd.Parameters.AddWithValue("@IDTipoPagoCS", request.IDTipoPagoCS ?? 0);
        cmd.Parameters.AddWithValue("@IDBancoTransferCS", request.IDBancoTransferCS ?? 0);
        cmd.Parameters.AddWithValue("@TransferenciaCS", request.TransferenciaCS ?? string.Empty);
        cmd.Parameters.AddWithValue("@IDBancoTarjetaCS", request.IDBancoTarjetaCS ?? 0);
        cmd.Parameters.AddWithValue("@TarjetaCS", request.TarjetaCS ?? string.Empty);
        cmd.Parameters.AddWithValue("@IDBancoDepositoEfeCS", request.IDBancoDepositoEfeCS ?? 0);
        cmd.Parameters.AddWithValue("@DepositoEfectivoNumeroCS", request.DepositoEfectivoNumeroCS ?? string.Empty);
        cmd.Parameters.AddWithValue("@MontoTotalCS", request.MontoTotalCS ?? 0m);
        cmd.Parameters.AddWithValue("@TipoTarjeta", request.TipoTarjeta ?? 0);
    }

    private static DataTable BuildPedidoIdsTvp(IEnumerable<int> idsPedido)
    {
        var tvp = new DataTable();
        tvp.Columns.Add("IDPedido", typeof(int));
        foreach (var id in idsPedido.Where(x => x > 0).Distinct())
            tvp.Rows.Add(id);
        return tvp;
    }

    private static AutorizarPedidosResponse MapAutorizarResponse(DataTable dt, IReadOnlyCollection<int> requestedIds)
    {
        var errors = new List<PedidoResultadoDto>();
        var idsVenta = new List<int>();
        var withResponse = new HashSet<int>();

        if (dt != null)
        {
            foreach (DataRow row in dt.Rows)
            {
                var result = dt.Columns.Contains("result") ? Convert.ToInt32(row["result"]) : -1;
                var mensaje = dt.Columns.Contains("mensaje") ? Convert.ToString(row["mensaje"]) : null;
                var idPedido = dt.Columns.Contains("IDPedido") ? Convert.ToInt32(row["IDPedido"]) : 0;
                var pedido = dt.Columns.Contains("Pedido") ? Convert.ToInt32(row["Pedido"]) : 0;
                var idVenta = dt.Columns.Contains("IDVenta") ? Convert.ToInt32(row["IDVenta"]) : 0;

                if (idPedido > 0) withResponse.Add(idPedido);
                if (idVenta > 0) idsVenta.Add(idVenta);

                if (result == -1)
                {
                    errors.Add(new PedidoResultadoDto
                    {
                        Result = result,
                        Mensaje = mensaje,
                        IDPedido = idPedido,
                        Pedido = pedido,
                        IDVenta = idVenta
                    });
                }
            }
        }

        foreach (var id in requestedIds)
        {
            if (withResponse.Contains(id)) continue;
            errors.Add(new PedidoResultadoDto
            {
                Result = -1,
                Mensaje = "Sin respuesta del procedimiento.",
                IDPedido = id,
                Pedido = 0,
                IDVenta = 0
            });
        }

        var idsVentaStr = idsVenta.Count > 0 ? string.Join("~", idsVenta.Distinct()) : string.Empty;
        return new AutorizarPedidosResponse(errors, idsVentaStr);
    }
}

