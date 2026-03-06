using System.Data;
using ISL_Service.Application.DTOs.Equipos;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

public class EquiposRepository : IEquiposRepository
{
    private readonly IConfiguration _configuration;

    private const string SpConsultarEquipos = "dbo.sp_w_ConsultarEquipos";
    private const string SpInsertarEquipo = "dbo.sp_w_InsertarEquipo";
    private const string SpActualizarEquipo = "dbo.sp_w_ActualizarEquipo";
    private const string SpInhabilitarEquipo = "dbo.sp_w_InhabilitarEquipo";

    public EquiposRepository(IConfiguration configuration)
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

    public async Task<List<EquipoDto>> ConsultarAsync(byte? estado, Guid? categoriaId, byte? diaJuego, string? texto, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpConsultarEquipos, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@Texto", string.IsNullOrWhiteSpace(texto) ? DBNull.Value : texto.Trim());
        cmd.Parameters.AddWithValue("@Estado", (object?)estado ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CategoriaId", (object?)categoriaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@DiaJuego", (object?)diaJuego ?? DBNull.Value);

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd))
        {
            adapter.Fill(dt);
        }

        return Funciones.DataTableToList<EquipoDto>(dt);
    }

    public async Task<EquipoDto?> InsertarAsync(CreateEquipoRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpInsertarEquipo, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@Nombre", request.Nombre.Trim());
        cmd.Parameters.AddWithValue("@CategoriaPredeterminadaId", request.CategoriaPredeterminadaId);
        cmd.Parameters.AddWithValue("@DiaJuegoPredeterminado", request.DiaJuegoPredeterminado);
        cmd.Parameters.AddWithValue("@ProfesorTitularPredeterminadoId", request.ProfesorTitularPredeterminadoId);
        cmd.Parameters.AddWithValue("@ProfesorAuxiliarPredeterminadoId", (object?)request.ProfesorAuxiliarPredeterminadoId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        return await ExecuteSingleAsync<EquipoDto>(cmd, ct);
    }

    public async Task<EquipoDto?> ActualizarAsync(Guid id, UpdateEquipoRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpActualizarEquipo, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@EquipoId", id);
        cmd.Parameters.AddWithValue("@Nombre", request.Nombre.Trim());
        cmd.Parameters.AddWithValue("@CategoriaPredeterminadaId", request.CategoriaPredeterminadaId);
        cmd.Parameters.AddWithValue("@DiaJuegoPredeterminado", request.DiaJuegoPredeterminado);
        cmd.Parameters.AddWithValue("@ProfesorTitularPredeterminadoId", request.ProfesorTitularPredeterminadoId);
        cmd.Parameters.AddWithValue("@ProfesorAuxiliarPredeterminadoId", (object?)request.ProfesorAuxiliarPredeterminadoId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        return await ExecuteSingleAsync<EquipoDto>(cmd, ct);
    }

    public async Task<EquipoDto?> InhabilitarAsync(Guid id, string? motivo, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpInhabilitarEquipo, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@EquipoId", id);
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
        cmd.Parameters.AddWithValue("@MotivoCancelacion", string.IsNullOrWhiteSpace(motivo) ? DBNull.Value : motivo.Trim());

        return await ExecuteSingleAsync<EquipoDto>(cmd, ct);
    }

    public async Task<EquipoDto?> HabilitarAsync(Guid id, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        const string sql = @"
UPDATE dbo.WEquipo
SET
    Estado = 1,
    FechaActualizacion = SYSUTCDATETIME(),
    UsuarioActualizacionId = @UsuarioId,
    FechaCancelacion = NULL,
    UsuarioCancelacionId = NULL,
    MotivoCancelacion = NULL
WHERE Id = @EquipoId;

SELECT
    e.Id,
    e.Nombre,
    e.CategoriaPredeterminadaId,
    c.Nombre AS CategoriaPredeterminadaNombre,
    e.DiaJuegoPredeterminado,
    e.ProfesorTitularPredeterminadoId,
    p1.Nombre AS ProfesorTitular,
    e.ProfesorAuxiliarPredeterminadoId,
    p2.Nombre AS ProfesorAuxiliar,
    e.Estado,
    e.FechaCreacion,
    e.UsuarioCreacionId,
    e.FechaActualizacion,
    e.UsuarioActualizacionId,
    e.FechaCancelacion,
    e.UsuarioCancelacionId,
    e.MotivoCancelacion,
    e.RowVer
FROM dbo.WEquipo e
INNER JOIN dbo.WCategoria c ON c.Id = e.CategoriaPredeterminadaId
LEFT JOIN dbo.WProfesor p1 ON p1.Id = e.ProfesorTitularPredeterminadoId
LEFT JOIN dbo.WProfesor p2 ON p2.Id = e.ProfesorAuxiliarPredeterminadoId
WHERE e.Id = @EquipoId;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.CommandType = CommandType.Text;
        cmd.Parameters.AddWithValue("@EquipoId", id);
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        return await ExecuteSingleAsync<EquipoDto>(cmd, ct);
    }

    private static async Task<T?> ExecuteSingleAsync<T>(SqlCommand cmd, CancellationToken ct = default) where T : class
    {
        using var reader = await cmd.ExecuteReaderAsync(ct);
        var dt = new DataTable();
        dt.Load(reader);
        return Funciones.DataTableToList<T>(dt).FirstOrDefault();
    }
}

