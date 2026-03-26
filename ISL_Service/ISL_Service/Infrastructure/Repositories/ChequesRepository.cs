using System.Data;
using ISL_Service.Application.DTOs.Cheques;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

public class ChequesRepository : IChequesRepository
{
    private readonly IConfiguration _configuration;

    private const string SpConsultarCheques = "dbo.sp_w_ConsultarCheques";
    private const string SpConsultarChequeDetalle = "dbo.sp_w_ConsultarChequeDetalle";
    private const string SpInsertarCheque = "dbo.sp_w_InsertarCheque";
    private const string SpActualizarCheque = "dbo.sp_w_ActualizarCheque";
    private const string SpCambiarEstatusCheque = "dbo.sp_w_CambiarEstatusCheque";

    public ChequesRepository(IConfiguration configuration)
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

    public async Task<List<ChequeDto>> ConsultarAsync(
        string? texto,
        int? idCliente,
        int? idBanco,
        byte? estatusCheque,
        byte? idStatus,
        DateTime? fechaChequeInicio,
        DateTime? fechaChequeFin,
        CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpConsultarCheques, conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@Texto", string.IsNullOrWhiteSpace(texto) ? DBNull.Value : texto.Trim());
        cmd.Parameters.AddWithValue("@IDCliente", (object?)idCliente ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@IDBanco", (object?)idBanco ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@EstatusCheque", (object?)estatusCheque ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@IDStatus", (object?)idStatus ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@FechaChequeInicio", (object?)fechaChequeInicio ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@FechaChequeFin", (object?)fechaChequeFin ?? DBNull.Value);

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd))
        {
            adapter.Fill(dt);
        }

        return Funciones.DataTableToList<ChequeDto>(dt);
    }

    public async Task<ChequeDetalleDto?> ConsultarDetalleAsync(Guid chequeId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpConsultarChequeDetalle, conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@ChequeId", chequeId);

        using var adapter = new SqlDataAdapter(cmd);
        var ds = new DataSet();
        adapter.Fill(ds);

        var cabecera = ds.Tables.Count > 0 ? ds.Tables[0] : null;
        var historial = ds.Tables.Count > 1 ? ds.Tables[1] : null;

        var detail = cabecera is null
            ? null
            : Funciones.DataTableToList<ChequeDetalleDto>(cabecera).FirstOrDefault();

        if (detail is null)
            return null;

        detail.Historial = historial is null
            ? new List<ChequeHistorialDto>()
            : Funciones.DataTableToList<ChequeHistorialDto>(historial);

        return detail;
    }

    public async Task<ChequeDetalleDto?> InsertarAsync(CreateChequeRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpInsertarCheque, conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@IDCliente", request.IDCliente);
        cmd.Parameters.AddWithValue("@IDBanco", request.IDBanco);
        cmd.Parameters.AddWithValue("@NumeroCheque", request.NumeroCheque.Trim());
        cmd.Parameters.AddWithValue("@Monto", request.Monto);
        cmd.Parameters.AddWithValue("@FechaCheque", request.FechaCheque.Date);
        cmd.Parameters.AddWithValue("@Observaciones", string.IsNullOrWhiteSpace(request.Observaciones) ? DBNull.Value : request.Observaciones.Trim());
        cmd.Parameters.AddWithValue("@ResponsableCobroId", (object?)request.ResponsableCobroId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        Guid createdId;
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            var dt = new DataTable();
            dt.Load(reader);
            createdId = Funciones.DataTableToList<ChequeDetalleDto>(dt).FirstOrDefault()?.Id ?? Guid.Empty;
        }

        if (createdId == Guid.Empty)
            return null;

        return await ConsultarDetalleAsync(createdId, ct);
    }

    public async Task<ChequeDetalleDto?> ActualizarAsync(Guid chequeId, UpdateChequeRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpActualizarCheque, conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@ChequeId", chequeId);
        cmd.Parameters.AddWithValue("@IDCliente", request.IDCliente);
        cmd.Parameters.AddWithValue("@IDBanco", request.IDBanco);
        cmd.Parameters.AddWithValue("@NumeroCheque", request.NumeroCheque.Trim());
        cmd.Parameters.AddWithValue("@Monto", request.Monto);
        cmd.Parameters.AddWithValue("@FechaCheque", request.FechaCheque.Date);
        cmd.Parameters.AddWithValue("@Observaciones", string.IsNullOrWhiteSpace(request.Observaciones) ? DBNull.Value : request.Observaciones.Trim());
        cmd.Parameters.AddWithValue("@ResponsableCobroId", (object?)request.ResponsableCobroId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        await cmd.ExecuteNonQueryAsync(ct);
        return await ConsultarDetalleAsync(chequeId, ct);
    }

    public async Task<ChequeDetalleDto?> CambiarEstatusAsync(
        Guid chequeId,
        byte estatusChequeNuevo,
        string? motivo,
        string? observaciones,
        DateTime? fechaMovimiento,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpCambiarEstatusCheque, conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@ChequeId", chequeId);
        cmd.Parameters.AddWithValue("@EstatusChequeNuevo", estatusChequeNuevo);
        cmd.Parameters.AddWithValue("@Motivo", string.IsNullOrWhiteSpace(motivo) ? DBNull.Value : motivo.Trim());
        cmd.Parameters.AddWithValue("@Observaciones", string.IsNullOrWhiteSpace(observaciones) ? DBNull.Value : observaciones.Trim());
        cmd.Parameters.AddWithValue("@FechaMovimiento", (object?)fechaMovimiento ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        await cmd.ExecuteNonQueryAsync(ct);
        return await ConsultarDetalleAsync(chequeId, ct);
    }
}
