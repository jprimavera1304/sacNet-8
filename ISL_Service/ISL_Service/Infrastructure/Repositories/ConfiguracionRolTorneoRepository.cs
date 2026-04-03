using System.Data;
using ISL_Service.Application.DTOs.ConfiguracionRolTorneo;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

public class ConfiguracionRolTorneoRepository : IConfiguracionRolTorneoRepository
{
    private readonly IConfiguration _configuration;

    private const string SpConsultar = "dbo.sp_w_ConsultarConfiguracionesRolTorneo";
    private const string SpObtener = "dbo.sp_w_ObtenerConfiguracionRolTorneo";
    private const string SpInsertar = "dbo.sp_w_InsertarConfiguracionRolTorneo";
    private const string SpActualizar = "dbo.sp_w_ActualizarConfiguracionRolTorneo";
    private const string SpInhabilitar = "dbo.sp_w_InhabilitarConfiguracionRolTorneo";

    public ConfiguracionRolTorneoRepository(IConfiguration configuration)
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

    public async Task<List<ConfiguracionRolTorneoDto>> ConsultarAsync(string? texto, Guid? temporadaId, byte? estado, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpConsultar, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@Texto", string.IsNullOrWhiteSpace(texto) ? DBNull.Value : texto.Trim());
        cmd.Parameters.AddWithValue("@TemporadaId", (object?)temporadaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Estado", (object?)estado ?? DBNull.Value);

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd))
        {
            adapter.Fill(dt);
        }

        return Funciones.DataTableToList<ConfiguracionRolTorneoDto>(dt);
    }

    public async Task<ConfiguracionRolTorneoDto?> ObtenerActivaPorTorneoAsync(Guid torneoId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpObtener, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@TorneoId", torneoId);

        return await ExecuteSingleAsync<ConfiguracionRolTorneoDto>(cmd, ct);
    }

    public async Task<ConfiguracionRolTorneoDto?> InsertarAsync(CreateConfiguracionRolTorneoRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpInsertar, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@TorneoId", request.TorneoId);
        cmd.Parameters.AddWithValue("@HoraInicioPredeterminada", (object?)request.HoraInicioPredeterminada ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@DuracionPartidoMin", (object?)request.DuracionPartidoMin ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@MinutosEntrePartidos", (object?)request.MinutosEntrePartidos ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@NumeroCanchas", (object?)request.NumeroCanchas ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ObservacionesPredeterminadas", string.IsNullOrWhiteSpace(request.ObservacionesPredeterminadas)
            ? DBNull.Value
            : request.ObservacionesPredeterminadas.Trim());
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        return await ExecuteSingleAsync<ConfiguracionRolTorneoDto>(cmd, ct);
    }

    public async Task<ConfiguracionRolTorneoDto?> ActualizarAsync(Guid id, UpdateConfiguracionRolTorneoRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpActualizar, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@HoraInicioPredeterminada", (object?)request.HoraInicioPredeterminada ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@DuracionPartidoMin", (object?)request.DuracionPartidoMin ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@MinutosEntrePartidos", (object?)request.MinutosEntrePartidos ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@NumeroCanchas", (object?)request.NumeroCanchas ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ObservacionesPredeterminadas", string.IsNullOrWhiteSpace(request.ObservacionesPredeterminadas)
            ? DBNull.Value
            : request.ObservacionesPredeterminadas.Trim());
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        return await ExecuteSingleAsync<ConfiguracionRolTorneoDto>(cmd, ct);
    }

    public async Task<ConfiguracionRolTorneoDto?> InhabilitarAsync(Guid id, string motivo, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpInhabilitar, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@MotivoCancelacion", motivo.Trim());
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        return await ExecuteSingleAsync<ConfiguracionRolTorneoDto>(cmd, ct);
    }

    private static async Task<T?> ExecuteSingleAsync<T>(SqlCommand cmd, CancellationToken ct = default) where T : class
    {
        using var reader = await cmd.ExecuteReaderAsync(ct);
        var dt = new DataTable();
        dt.Load(reader);
        return Funciones.DataTableToList<T>(dt).FirstOrDefault();
    }
}
