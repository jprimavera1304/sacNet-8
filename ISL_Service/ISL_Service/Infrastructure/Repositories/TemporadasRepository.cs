using System.Data;
using ISL_Service.Application.DTOs.Temporadas;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

public class TemporadasRepository : ITemporadasRepository
{
    private readonly IConfiguration _configuration;

    private const string SpConsultarTemporadas = "dbo.sp_w_ConsultarTemporadas";
    private const string SpCerrarTemporadasVencidas = "dbo.sp_w_CerrarTemporadasVencidas";
    private const string SpInsertarTemporada = "dbo.sp_w_InsertarTemporada";
    private const string SpActualizarTemporada = "dbo.sp_w_ActualizarTemporada";
    private const string SpCancelarTemporada = "dbo.sp_w_CancelarTemporada";
    private const string SpReactivarTemporada = "dbo.sp_w_ReactivarTemporada";

    private const string SpConsultarTorneos = "dbo.sp_w_ConsultarTorneos";
    private const string SpCerrarTorneosVencidos = "dbo.sp_w_CerrarTorneosVencidos";
    private const string SpInsertarTorneo = "dbo.sp_w_InsertarTorneo";
    private const string SpActualizarTorneo = "dbo.sp_w_ActualizarTorneo";
    private const string SpCancelarTorneo = "dbo.sp_w_CancelarTorneo";
    private const string SpActivarTorneo = "dbo.sp_w_ActivarTorneo";
    private const string SpCerrarTorneo = "dbo.sp_w_CerrarTorneo";
    private const string SpReactivarTorneo = "dbo.sp_w_ReactivarTorneo";

    public TemporadasRepository(IConfiguration configuration)
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

    public async Task<List<TemporadaDto>> ConsultarTemporadasAsync(byte? estado, string? texto, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpConsultarTemporadas, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@Estado", (object?)estado ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Texto", string.IsNullOrWhiteSpace(texto) ? DBNull.Value : texto.Trim());

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd))
        {
            adapter.Fill(dt);
        }

        return Funciones.DataTableToList<TemporadaDto>(dt);
    }

    public async Task<List<TemporadaDto>> ConsultarTemporadasListadoAsync(byte? estado, string? texto, Guid usuarioSistemaId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            var resolvedUsuarioSistemaId = ResolveUsuarioSistemaId(usuarioSistemaId);

            await using (var closeCmd = new SqlCommand(SpCerrarTemporadasVencidas, conn, (SqlTransaction)tx))
            {
                closeCmd.CommandType = CommandType.StoredProcedure;
                closeCmd.Parameters.AddWithValue("@UsuarioSistemaId", resolvedUsuarioSistemaId);
                await closeCmd.ExecuteNonQueryAsync(ct);
            }

            await using var listCmd = new SqlCommand(SpConsultarTemporadas, conn, (SqlTransaction)tx);
            listCmd.CommandType = CommandType.StoredProcedure;
            listCmd.Parameters.AddWithValue("@Estado", (object?)estado ?? DBNull.Value);
            listCmd.Parameters.AddWithValue("@Texto", string.IsNullOrWhiteSpace(texto) ? DBNull.Value : texto.Trim());

            var dt = new DataTable();
            using (var adapter = new SqlDataAdapter(listCmd))
            {
                adapter.Fill(dt);
            }

            await tx.CommitAsync(ct);
            return Funciones.DataTableToList<TemporadaDto>(dt);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<TemporadaDto?> InsertarTemporadaAsync(CreateTemporadaRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpInsertarTemporada, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@Nombre", request.Nombre.Trim());
        cmd.Parameters.AddWithValue("@FechaInicio", (object?)request.FechaInicio ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@FechaFin", (object?)request.FechaFin ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        return await ExecuteSingleAsync<TemporadaDto>(cmd, ct);
    }

    public async Task<TemporadaDto?> ActualizarTemporadaAsync(Guid id, UpdateTemporadaRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpActualizarTemporada, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Nombre", request.Nombre.Trim());
        cmd.Parameters.AddWithValue("@FechaInicio", (object?)request.FechaInicio ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@FechaFin", (object?)request.FechaFin ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        return await ExecuteSingleAsync<TemporadaDto>(cmd, ct);
    }

    public async Task<TemporadaDto?> CancelarTemporadaAsync(Guid id, string? motivo, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpCancelarTemporada, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Motivo", string.IsNullOrWhiteSpace(motivo) ? DBNull.Value : motivo.Trim());
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        return await ExecuteSingleAsync<TemporadaDto>(cmd, ct);
    }

    public async Task<TemporadaDto?> ReactivarTemporadaAsync(Guid id, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpReactivarTemporada, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        return await ExecuteSingleAsync<TemporadaDto>(cmd, ct);
    }

    public async Task<List<TorneoDto>> ConsultarTorneosAsync(Guid? temporadaId, byte? estado, string? texto, DateTime? fechaInicio, DateTime? fechaFin, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpConsultarTorneos, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@TemporadaId", (object?)temporadaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Estado", (object?)estado ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Texto", string.IsNullOrWhiteSpace(texto) ? DBNull.Value : texto.Trim());
        cmd.Parameters.AddWithValue("@FechaInicio", (object?)fechaInicio ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@FechaFin", (object?)fechaFin ?? DBNull.Value);

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd))
        {
            adapter.Fill(dt);
        }

        return Funciones.DataTableToList<TorneoDto>(dt);
    }

    public async Task<List<TorneoDto>> ConsultarTorneosListadoAsync(Guid? temporadaId, byte? estado, string? texto, DateTime? fechaInicio, DateTime? fechaFin, Guid usuarioSistemaId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            await using (var closeCmd = new SqlCommand(SpCerrarTorneosVencidos, conn, (SqlTransaction)tx))
            {
                closeCmd.CommandType = CommandType.StoredProcedure;
                closeCmd.Parameters.AddWithValue("@UsuarioSistemaId", usuarioSistemaId);
                closeCmd.Parameters.AddWithValue("@FechaCorte", DBNull.Value);
                await closeCmd.ExecuteNonQueryAsync(ct);
            }

            await using var listCmd = new SqlCommand(SpConsultarTorneos, conn, (SqlTransaction)tx);
            listCmd.CommandType = CommandType.StoredProcedure;
            listCmd.Parameters.AddWithValue("@TemporadaId", (object?)temporadaId ?? DBNull.Value);
            listCmd.Parameters.AddWithValue("@Estado", (object?)estado ?? DBNull.Value);
            listCmd.Parameters.AddWithValue("@Texto", string.IsNullOrWhiteSpace(texto) ? DBNull.Value : texto.Trim());
            listCmd.Parameters.AddWithValue("@FechaInicio", (object?)fechaInicio ?? DBNull.Value);
            listCmd.Parameters.AddWithValue("@FechaFin", (object?)fechaFin ?? DBNull.Value);

            var dt = new DataTable();
            using (var adapter = new SqlDataAdapter(listCmd))
            {
                adapter.Fill(dt);
            }

            await tx.CommitAsync(ct);
            return Funciones.DataTableToList<TorneoDto>(dt);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<TorneoDto?> InsertarTorneoAsync(CreateTorneoRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpInsertarTorneo, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@TemporadaId", request.TemporadaId);
        cmd.Parameters.AddWithValue("@Nombre", request.Nombre.Trim());
        cmd.Parameters.AddWithValue("@Clave", string.IsNullOrWhiteSpace(request.Clave) ? DBNull.Value : request.Clave.Trim());
        cmd.Parameters.AddWithValue("@FechaInicio", (object?)request.FechaInicio ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@FechaFin", (object?)request.FechaFin ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        return await ExecuteSingleAsync<TorneoDto>(cmd, ct);
    }

    public async Task<TorneoDto?> ActualizarTorneoAsync(Guid id, UpdateTorneoRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpActualizarTorneo, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@TemporadaId", request.TemporadaId);
        cmd.Parameters.AddWithValue("@Nombre", request.Nombre.Trim());
        cmd.Parameters.AddWithValue("@Clave", string.IsNullOrWhiteSpace(request.Clave) ? DBNull.Value : request.Clave.Trim());
        cmd.Parameters.AddWithValue("@FechaInicio", (object?)request.FechaInicio ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@FechaFin", (object?)request.FechaFin ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        return await ExecuteSingleAsync<TorneoDto>(cmd, ct);
    }

    public async Task<TorneoDto?> CancelarTorneoAsync(Guid id, string? motivo, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpCancelarTorneo, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Motivo", string.IsNullOrWhiteSpace(motivo) ? DBNull.Value : motivo.Trim());
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        return await ExecuteSingleAsync<TorneoDto>(cmd, ct);
    }

    public async Task<TorneoDto?> ActivarTorneoAsync(Guid id, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpActivarTorneo, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        return await ExecuteSingleAsync<TorneoDto>(cmd, ct);
    }

    public async Task<TorneoDto?> CerrarTorneoAsync(Guid id, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpCerrarTorneo, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        return await ExecuteSingleAsync<TorneoDto>(cmd, ct);
    }

    public async Task<TorneoDto?> ReactivarTorneoAsync(Guid id, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpReactivarTorneo, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        return await ExecuteSingleAsync<TorneoDto>(cmd, ct);
    }

    public async Task<int> CerrarTorneosVencidosAsync(Guid usuarioSistemaId, DateTime? fechaCorte, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpCerrarTorneosVencidos, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@UsuarioSistemaId", usuarioSistemaId);
        cmd.Parameters.AddWithValue("@FechaCorte", (object?)fechaCorte ?? DBNull.Value);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return 0;

        var ordinal = reader.GetOrdinal("TorneosCerrados");
        return reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);
    }

    private static async Task<T?> ExecuteSingleAsync<T>(SqlCommand cmd, CancellationToken ct = default) where T : class
    {
        using var reader = await cmd.ExecuteReaderAsync(ct);
        var dt = new DataTable();
        dt.Load(reader);

        return Funciones.DataTableToList<T>(dt).FirstOrDefault();
    }

    private Guid ResolveUsuarioSistemaId(Guid usuarioSistemaIdFromCaller)
    {
        var configured = _configuration["Temporadas:UsuarioSistemaId"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (Guid.TryParse(configured, out var configuredGuid) && configuredGuid != Guid.Empty)
                return configuredGuid;

            throw new InvalidOperationException("Configuracion invalida en Temporadas:UsuarioSistemaId.");
        }

        if (usuarioSistemaIdFromCaller != Guid.Empty)
            return usuarioSistemaIdFromCaller;

        throw new InvalidOperationException("No se pudo resolver UsuarioSistemaId para cerrar temporadas vencidas.");
    }
}
