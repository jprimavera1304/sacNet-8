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

        // Se va DIRECTO al flujo por pedido. El batch no funciona y no puede
        // funcionar: hace "INSERT INTO #tmp EXEC sp_w_ProcesarPedido", y ese
        // procedimiento termina llamando procedimientos de legacy que a su vez
        // hacen INSERT...EXEC. SQL Server no permite anidarlos, asi que el
        // batch contesta siempre "An INSERT EXEC statement cannot be nested."
        // (medido contra la base: uno por uno autoriza y genera venta; el mismo
        // pedido por batch falla).
        //
        // Antes se intentaba el batch y se caia al flujo por pedido SOLO si el
        // mensaje de error traia el texto "INSERT EXEC". Eso hacia que autorizar
        // dependiera de reconocer una frase de SQL Server: basta con que cambie
        // la redaccion o el idioma del servidor para que el rescate no entre y
        // la autorizacion se pierda sin explicacion.
        //
        // Ir por pedido tambien da mejor informacion: se sabe cual si y cual no,
        // en vez de perder el lote entero por uno.
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

