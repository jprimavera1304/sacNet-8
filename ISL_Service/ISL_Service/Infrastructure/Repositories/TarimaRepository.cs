using System.Data;
using ISL_Service.Application.DTOs.Tarima;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// Repository de Tarimas usando SP: sp_w_ConsultarTarimas, sp_w_InsertarTarima, sp_w_ActualizarTarima, sp_w_CancelarTarima.
/// </summary>
public class TarimaRepository : ITarimaRepository
{
    private readonly IConfiguration _configuration;
    private const string SpConsultar = "dbo.sp_w_ConsultarTarimas";
    private const string SpInsertar = "dbo.sp_w_InsertarTarima";
    private const string SpActualizar = "dbo.sp_w_ActualizarTarima";
    private const string SpCancelar = "dbo.sp_w_CancelarTarima";

    public TarimaRepository(IConfiguration configuration)
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

    public async Task<List<TarimaDto>> ConsultarTarimasAsync(int? idStatus, string? busqueda, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpConsultar, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@IdStatus", (object?)idStatus ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Busqueda", string.IsNullOrWhiteSpace(busqueda) ? DBNull.Value : busqueda.Trim());

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd))
        {
            adapter.Fill(dt);
        }
        return Funciones.DataTableToList<TarimaDto>(dt);
    }

    public async Task<int> InsertarTarimaAsync(CreateTarimaRequest request, string usuarioCreacion, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpInsertar, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@NombreTarima", request.NombreTarima.Trim());
        cmd.Parameters.AddWithValue("@IdTipoCasco", request.IdTipoCasco);
        cmd.Parameters.AddWithValue("@NumeroCascosBase", request.NumeroCascosBase);
        cmd.Parameters.AddWithValue("@Observaciones", string.IsNullOrWhiteSpace(request.Observaciones) ? DBNull.Value : request.Observaciones!.Trim());
        cmd.Parameters.AddWithValue("@UsuarioCreacion", string.IsNullOrWhiteSpace(usuarioCreacion) ? DBNull.Value : usuarioCreacion.Trim());

        var outId = new SqlParameter("@IdTarima", SqlDbType.Int) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(outId);

        await cmd.ExecuteNonQueryAsync(ct);
        return outId.Value is DBNull or null ? 0 : Convert.ToInt32(outId.Value);
    }

    public async Task ActualizarTarimaAsync(int idTarima, UpdateTarimaRequest request, string usuarioModificacion, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpActualizar, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@IdTarima", idTarima);
        cmd.Parameters.AddWithValue("@NombreTarima", request.NombreTarima.Trim());
        cmd.Parameters.AddWithValue("@IdTipoCasco", request.IdTipoCasco);
        cmd.Parameters.AddWithValue("@NumeroCascosBase", request.NumeroCascosBase);
        cmd.Parameters.AddWithValue("@Observaciones", string.IsNullOrWhiteSpace(request.Observaciones) ? DBNull.Value : request.Observaciones!.Trim());
        cmd.Parameters.AddWithValue("@UsuarioModificacion", string.IsNullOrWhiteSpace(usuarioModificacion) ? DBNull.Value : usuarioModificacion.Trim());

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task CambiarStatusTarimaAsync(int idTarima, int idStatus, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpCancelar, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@IdTarima", idTarima);
        cmd.Parameters.AddWithValue("@IdStatus", idStatus);

        await cmd.ExecuteNonQueryAsync(ct);
    }
}
