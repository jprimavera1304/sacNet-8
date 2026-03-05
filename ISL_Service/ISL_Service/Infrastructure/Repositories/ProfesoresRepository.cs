using System.Data;
using ISL_Service.Application.DTOs.Profesores;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

public class ProfesoresRepository : IProfesoresRepository
{
    private readonly IConfiguration _configuration;

    private const string SpConsultarProfesores = "dbo.sp_w_ConsultarProfesores";
    private const string SpInsertarProfesor = "dbo.sp_w_InsertarProfesor";
    private const string SpActualizarProfesor = "dbo.sp_w_ActualizarProfesor";
    private const string SpInhabilitarProfesor = "dbo.sp_w_InhabilitarProfesor";

    public ProfesoresRepository(IConfiguration configuration)
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

    public async Task<List<ProfesorDto>> ConsultarAsync(byte? estado, string? texto, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpConsultarProfesores, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@Texto", string.IsNullOrWhiteSpace(texto) ? DBNull.Value : texto.Trim());
        cmd.Parameters.AddWithValue("@Estado", (object?)estado ?? DBNull.Value);

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd))
        {
            adapter.Fill(dt);
        }

        return Funciones.DataTableToList<ProfesorDto>(dt);
    }

    public async Task<ProfesorDto?> InsertarAsync(CreateProfesorRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpInsertarProfesor, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@Nombre", request.Nombre.Trim());
        cmd.Parameters.AddWithValue("@Telefono", request.Telefono.Trim());
        cmd.Parameters.AddWithValue("@Correo", string.IsNullOrWhiteSpace(request.Correo) ? DBNull.Value : request.Correo.Trim());
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        return await ExecuteSingleAsync<ProfesorDto>(cmd, ct);
    }

    public async Task<ProfesorDto?> ActualizarAsync(Guid id, UpdateProfesorRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpActualizarProfesor, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@ProfesorId", id);
        cmd.Parameters.AddWithValue("@Nombre", request.Nombre.Trim());
        cmd.Parameters.AddWithValue("@Telefono", request.Telefono.Trim());
        cmd.Parameters.AddWithValue("@Correo", string.IsNullOrWhiteSpace(request.Correo) ? DBNull.Value : request.Correo.Trim());
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        return await ExecuteSingleAsync<ProfesorDto>(cmd, ct);
    }

    public async Task<ProfesorDto?> InhabilitarAsync(Guid id, string? motivo, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpInhabilitarProfesor, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@ProfesorId", id);
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
        cmd.Parameters.AddWithValue("@MotivoCancelacion", string.IsNullOrWhiteSpace(motivo) ? DBNull.Value : motivo.Trim());

        return await ExecuteSingleAsync<ProfesorDto>(cmd, ct);
    }

    private static async Task<T?> ExecuteSingleAsync<T>(SqlCommand cmd, CancellationToken ct = default) where T : class
    {
        using var reader = await cmd.ExecuteReaderAsync(ct);
        var dt = new DataTable();
        dt.Load(reader);
        return Funciones.DataTableToList<T>(dt).FirstOrDefault();
    }
}
