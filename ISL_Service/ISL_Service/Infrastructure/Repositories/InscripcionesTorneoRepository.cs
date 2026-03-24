using System.Data;
using ISL_Service.Application.DTOs.InscripcionesTorneo;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

public class InscripcionesTorneoRepository : IInscripcionesTorneoRepository
{
    private readonly IConfiguration _configuration;

    private const string SpConsultarInscripciones = "dbo.sp_w_ConsultarInscripcionesTorneo";
    private const string SpInsertarInscripcion = "dbo.sp_w_InsertarInscripcionTorneo";
    private const string SpActualizarInscripcion = "dbo.sp_w_ActualizarInscripcionTorneo";
    private const string SpInhabilitarInscripcion = "dbo.sp_w_InhabilitarInscripcionTorneo";

    public InscripcionesTorneoRepository(IConfiguration configuration)
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

    public async Task<List<InscripcionTorneoDto>> ConsultarAsync(Guid? torneoId, Guid? categoriaId, byte? estado, string? texto, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpConsultarInscripciones, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@TorneoId", (object?)torneoId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CategoriaId", (object?)categoriaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Estado", (object?)estado ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Texto", string.IsNullOrWhiteSpace(texto) ? DBNull.Value : texto.Trim());

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd))
        {
            adapter.Fill(dt);
        }

        return Funciones.DataTableToList<InscripcionTorneoDto>(dt);
    }

    public async Task<InscripcionTorneoDto?> InsertarAsync(CreateInscripcionTorneoRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpInsertarInscripcion, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@TorneoId", request.TorneoId);
        cmd.Parameters.AddWithValue("@EquipoId", request.EquipoId);
        cmd.Parameters.AddWithValue("@CategoriaId", (object?)request.CategoriaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@DiaJuego", (object?)request.DiaJuego ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ProfesorTitularId", (object?)request.ProfesorTitularId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ProfesorAuxiliarId", (object?)request.ProfesorAuxiliarId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        return await ExecuteSingleAsync<InscripcionTorneoDto>(cmd, ct);
    }

    public async Task<InscripcionTorneoDto?> ActualizarAsync(Guid id, UpdateInscripcionTorneoRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpActualizarInscripcion, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@InscripcionTorneoId", id);
        cmd.Parameters.AddWithValue("@CategoriaId", (object?)request.CategoriaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@DiaJuego", (object?)request.DiaJuego ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ProfesorTitularId", (object?)request.ProfesorTitularId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ProfesorAuxiliarId", (object?)request.ProfesorAuxiliarId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        return await ExecuteSingleAsync<InscripcionTorneoDto>(cmd, ct);
    }

    public async Task<InscripcionTorneoDto?> InhabilitarAsync(Guid id, string? motivo, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpInhabilitarInscripcion, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@InscripcionTorneoId", id);
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
        cmd.Parameters.AddWithValue("@MotivoCancelacion", string.IsNullOrWhiteSpace(motivo) ? DBNull.Value : motivo.Trim());

        return await ExecuteSingleAsync<InscripcionTorneoDto>(cmd, ct);
    }

    public async Task<InscripcionTorneoDto?> HabilitarAsync(Guid id, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        const string sql = @"
UPDATE dbo.WInscripcionTorneo
SET
    Estado = 1,
    FechaActualizacion = SYSUTCDATETIME(),
    UsuarioActualizacionId = @UsuarioId,
    FechaCancelacion = NULL,
    UsuarioCancelacionId = NULL,
    MotivoCancelacion = NULL
WHERE Id = @InscripcionTorneoId;

SELECT
    i.Id,
    i.TorneoId,
    t.Nombre AS TorneoNombre,
    i.EquipoId,
    e.Nombre AS EquipoNombre,
    i.CategoriaId,
    c.Nombre AS CategoriaNombre,
    i.DiaJuego,
    i.ProfesorTitularId,
    pt.Nombre AS ProfesorTitularNombre,
    i.ProfesorAuxiliarId,
    pa.Nombre AS ProfesorAuxiliarNombre,
    i.Estado,
    i.FechaCreacion,
    i.UsuarioCreacionId,
    i.FechaActualizacion,
    i.UsuarioActualizacionId,
    i.FechaCancelacion,
    i.UsuarioCancelacionId,
    i.MotivoCancelacion,
    i.RowVer
FROM dbo.WInscripcionTorneo i
INNER JOIN dbo.WTorneo t ON i.TorneoId = t.Id
INNER JOIN dbo.WEquipo e ON i.EquipoId = e.Id
INNER JOIN dbo.WCategoria c ON i.CategoriaId = c.Id
INNER JOIN dbo.WProfesor pt ON i.ProfesorTitularId = pt.Id
LEFT JOIN dbo.WProfesor pa ON i.ProfesorAuxiliarId = pa.Id
WHERE i.Id = @InscripcionTorneoId;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.CommandType = CommandType.Text;
        cmd.Parameters.AddWithValue("@InscripcionTorneoId", id);
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        return await ExecuteSingleAsync<InscripcionTorneoDto>(cmd, ct);
    }

    private static async Task<T?> ExecuteSingleAsync<T>(SqlCommand cmd, CancellationToken ct = default) where T : class
    {
        using var reader = await cmd.ExecuteReaderAsync(ct);
        var dt = new DataTable();
        dt.Load(reader);
        return Funciones.DataTableToList<T>(dt).FirstOrDefault();
    }
}