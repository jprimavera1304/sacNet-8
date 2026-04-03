using System.Data;
using ISL_Service.Application.DTOs.UsuarioModuloFavorito;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

public class UsuarioModuloFavoritoRepository : IUsuarioModuloFavoritoRepository
{
    private readonly IConfiguration _configuration;

    private const string SpAgregar = "dbo.sp_w_UsuarioModuloFavorito_Agregar";
    private const string SpQuitar = "dbo.sp_w_UsuarioModuloFavorito_Quitar";
    private const string SpToggle = "dbo.sp_w_UsuarioModuloFavorito_Toggle";
    private const string SpListar = "dbo.sp_w_UsuarioModuloFavorito_Listar";

    public UsuarioModuloFavoritoRepository(IConfiguration configuration)
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

    public async Task<IReadOnlyList<UsuarioModuloFavoritoListItemDto>> ListarAsync(Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpListar, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd))
        {
            adapter.Fill(dt);
        }

        return Funciones.DataTableToList<UsuarioModuloFavoritoListItemDto>(dt);
    }

    public async Task<UsuarioModuloFavoritoDto?> AgregarAsync(Guid usuarioId, string moduloClave, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpAgregar, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
        cmd.Parameters.AddWithValue("@ModuloClave", moduloClave);

        return await ExecuteSingleAsync<UsuarioModuloFavoritoDto>(cmd, ct);
    }

    public async Task<UsuarioModuloFavoritoDto?> QuitarAsync(Guid usuarioId, string moduloClave, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpQuitar, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
        cmd.Parameters.AddWithValue("@ModuloClave", moduloClave);

        return await ExecuteSingleAsync<UsuarioModuloFavoritoDto>(cmd, ct);
    }

    public async Task<UsuarioModuloFavoritoDto?> ToggleAsync(Guid usuarioId, string moduloClave, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpToggle, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
        cmd.Parameters.AddWithValue("@ModuloClave", moduloClave);

        return await ExecuteSingleAsync<UsuarioModuloFavoritoDto>(cmd, ct);
    }

    private static async Task<T?> ExecuteSingleAsync<T>(SqlCommand cmd, CancellationToken ct = default) where T : class
    {
        using var reader = await cmd.ExecuteReaderAsync(ct);
        var dt = new DataTable();
        dt.Load(reader);
        return Funciones.DataTableToList<T>(dt).FirstOrDefault();
    }
}
