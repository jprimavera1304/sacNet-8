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
    private const string SpProcesarPedido = "sp_n_ProcesarPedido";

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

        var results = new List<PedidoResultadoDto>();
        var idsVenta = new List<int>();

        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        foreach (var idPedido in request.IdsPedido)
        {
            if (idPedido <= 0)
                continue;

            await using var cmd = new SqlCommand(SpProcesarPedido, conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@IDPedido", idPedido);
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

            var dt = new DataTable();
            using (var adapter = new SqlDataAdapter(cmd))
            {
                adapter.Fill(dt);
            }

            if (dt.Rows.Count == 0)
            {
                results.Add(new PedidoResultadoDto
                {
                    Result = -1,
                    Mensaje = "Sin respuesta del procedimiento.",
                    IDPedido = idPedido,
                    Pedido = 0,
                    IDVenta = 0
                });
                continue;
            }

            var row = dt.Rows[0];
            var result = row.Table.Columns.Contains("result") ? Convert.ToInt32(row["result"]) : -1;
            var mensaje = row.Table.Columns.Contains("mensaje") ? Convert.ToString(row["mensaje"]) : null;
            var pedido = row.Table.Columns.Contains("Pedido") ? Convert.ToInt32(row["Pedido"]) : 0;
            var idVenta = row.Table.Columns.Contains("IDVenta") ? Convert.ToInt32(row["IDVenta"]) : 0;

            if (idVenta > 0)
                idsVenta.Add(idVenta);

            if (result == -1)
            {
                results.Add(new PedidoResultadoDto
                {
                    Result = result,
                    Mensaje = mensaje,
                    IDPedido = idPedido,
                    Pedido = pedido,
                    IDVenta = idVenta
                });
            }
        }

        var idsVentaStr = idsVenta.Count > 0 ? string.Join("~", idsVenta.Distinct()) : string.Empty;
        return new AutorizarPedidosResponse(results, idsVentaStr);
    }
}
