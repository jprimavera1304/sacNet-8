using System.Data;
using ISL_Service.Application.DTOs.GeneracionRolTorneo;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

public class GeneracionRolTorneoRepository : IGeneracionRolTorneoRepository
{
    private readonly IConfiguration _configuration;

    private const string SpConsultar = "dbo.sp_w_ConsultarGeneracionesRolTorneo";
    private const string SpObtener = "dbo.sp_w_ObtenerGeneracionRolTorneo";
    private const string SpInsertar = "dbo.sp_w_InsertarGeneracionRolTorneo";
    private const string SpActualizar = "dbo.sp_w_ActualizarGeneracionRolTorneo";
    private const string SpCancelar = "dbo.sp_w_CancelarGeneracionRolTorneo";
    private const string SpConsultarCategorias = "dbo.sp_w_ConsultarCategoriasGeneracionRolTorneo";
    private const string SpGuardarCanchas = "dbo.sp_w_GuardarCanchasGeneracionRolTorneo";
    private const string SpConsultarCanchas = "dbo.sp_w_ConsultarCanchasGeneracionRolTorneo";
    private const string SpCargarEquipos = "dbo.sp_w_CargarEquiposGeneracionRolTorneo";
    private const string SpActualizarEquipo = "dbo.sp_w_ActualizarParticipacionEquipoGeneracionRolTorneo";
    private const string SpConsultarEquipos = "dbo.sp_w_ConsultarEquiposGeneracionRolTorneo";
    private const string SpGenerarPartidos = "dbo.sp_w_GenerarPartidosGeneracionRolTorneo";
    private const string SpConsultarPartidos = "dbo.sp_w_ConsultarPartidosGeneracionRolTorneo";
    private const string SpActualizarOrdenPartidos = "dbo.sp_w_ActualizarOrdenPartidosGeneracionRolTorneo";
    private const string SpActualizarObservacionPartido = "dbo.sp_w_ActualizarObservacionPartidoGeneracionRolTorneo";

    public GeneracionRolTorneoRepository(IConfiguration configuration)
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

    public async Task<List<GeneracionRolTorneoDto>> ConsultarAsync(string? texto, Guid? torneoId, Guid? jornadaId, DateTime? fechaJuego, byte? diaJuego, byte? estado, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpConsultar, conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@Texto", string.IsNullOrWhiteSpace(texto) ? DBNull.Value : texto.Trim());
        cmd.Parameters.AddWithValue("@TorneoId", (object?)torneoId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@JornadaId", (object?)jornadaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@FechaJuego", (object?)fechaJuego?.Date ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@DiaJuego", (object?)diaJuego ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Estado", (object?)estado ?? DBNull.Value);

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd))
        {
            adapter.Fill(dt);
        }

        return Funciones.DataTableToList<GeneracionRolTorneoDto>(dt);
    }

    public async Task<GeneracionRolTorneoDto?> ObtenerAsync(Guid id, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpObtener, conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@Id", id);

        return await ExecuteSingleAsync<GeneracionRolTorneoDto>(cmd, ct);
    }

    public async Task<GeneracionRolTorneoDto?> InsertarAsync(CreateGeneracionRolTorneoRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpInsertar, conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@TorneoId", request.TorneoId);
        cmd.Parameters.AddWithValue("@TemporadaId", request.TemporadaId);
        cmd.Parameters.AddWithValue("@JornadaId", request.JornadaId);
        cmd.Parameters.AddWithValue("@FechaJuego", request.FechaJuego.Date);
        cmd.Parameters.AddWithValue("@DiaJuego", request.DiaJuego);
        cmd.Parameters.AddWithValue("@HoraInicio", (object?)request.HoraInicio ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@DuracionPartidoMin", (object?)request.DuracionPartidoMin ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@MinutosEntrePartidos", (object?)request.MinutosEntrePartidos ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@NumeroCanchas", (object?)request.NumeroCanchas ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Observaciones", string.IsNullOrWhiteSpace(request.Observaciones) ? DBNull.Value : request.Observaciones.Trim());
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        return await ExecuteSingleAsync<GeneracionRolTorneoDto>(cmd, ct);
    }

    public async Task<GeneracionRolTorneoDto?> ActualizarAsync(Guid id, UpdateGeneracionRolTorneoRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpActualizar, conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@TemporadaId", request.TemporadaId);
        cmd.Parameters.AddWithValue("@JornadaId", request.JornadaId);
        cmd.Parameters.AddWithValue("@FechaJuego", request.FechaJuego.Date);
        cmd.Parameters.AddWithValue("@DiaJuego", request.DiaJuego);
        cmd.Parameters.AddWithValue("@HoraInicio", request.HoraInicio);
        cmd.Parameters.AddWithValue("@DuracionPartidoMin", request.DuracionPartidoMin);
        cmd.Parameters.AddWithValue("@MinutosEntrePartidos", request.MinutosEntrePartidos);
        cmd.Parameters.AddWithValue("@NumeroCanchas", request.NumeroCanchas);
        cmd.Parameters.AddWithValue("@Observaciones", string.IsNullOrWhiteSpace(request.Observaciones) ? DBNull.Value : request.Observaciones.Trim());
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        return await ExecuteSingleAsync<GeneracionRolTorneoDto>(cmd, ct);
    }

    public async Task<GeneracionRolTorneoDto?> CancelarAsync(Guid id, string motivo, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpCancelar, conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@MotivoCancelacion", motivo.Trim());
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        return await ExecuteSingleAsync<GeneracionRolTorneoDto>(cmd, ct);
    }

    public async Task<List<GeneracionRolCategoriaDto>> ConsultarCategoriasAsync(Guid generacionId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpConsultarCategorias, conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@GeneracionRolTorneoId", generacionId);

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd))
        {
            adapter.Fill(dt);
        }

        return Funciones.DataTableToList<GeneracionRolCategoriaDto>(dt);
    }

    public async Task<List<GeneracionRolCanchaDto>> GuardarCanchasAsync(Guid generacionId, Guid usuarioId, IReadOnlyList<GeneracionRolCanchaItemRequest> canchas, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpGuardarCanchas, conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@GeneracionRolTorneoId", generacionId);
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        var table = BuildCanchasTable(canchas);
        var param = cmd.Parameters.AddWithValue("@Canchas", table);
        param.SqlDbType = SqlDbType.Structured;
        param.TypeName = "dbo.WGeneracionRolCanchaType";

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd))
        {
            adapter.Fill(dt);
        }

        return Funciones.DataTableToList<GeneracionRolCanchaDto>(dt);
    }

    public async Task<List<GeneracionRolCanchaDto>> ConsultarCanchasAsync(Guid generacionId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpConsultarCanchas, conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@GeneracionRolTorneoId", generacionId);

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd))
        {
            adapter.Fill(dt);
        }

        return Funciones.DataTableToList<GeneracionRolCanchaDto>(dt);
    }

    public async Task<List<GeneracionRolEquipoDto>> CargarEquiposAsync(Guid generacionId, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpCargarEquipos, conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@GeneracionRolTorneoId", generacionId);
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd))
        {
            adapter.Fill(dt);
        }

        return Funciones.DataTableToList<GeneracionRolEquipoDto>(dt);
    }

    public async Task<List<GeneracionRolEquipoDto>> ConsultarEquiposAsync(Guid generacionId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpConsultarEquipos, conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@GeneracionRolTorneoId", generacionId);

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd))
        {
            adapter.Fill(dt);
        }

        return Funciones.DataTableToList<GeneracionRolEquipoDto>(dt);
    }

    public async Task<GeneracionRolEquipoDto?> ActualizarParticipacionEquipoAsync(Guid id, UpdateParticipacionEquipoRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpActualizarEquipo, conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Participa", request.Participa);
        cmd.Parameters.AddWithValue("@Observaciones", string.IsNullOrWhiteSpace(request.Observaciones) ? DBNull.Value : request.Observaciones.Trim());
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        return await ExecuteSingleAsync<GeneracionRolEquipoDto>(cmd, ct);
    }

    public async Task<GenerarPartidosGeneracionRolTorneoResponse> GenerarPartidosAsync(Guid generacionId, Guid usuarioId, bool confirmarEstado = true, bool soloConfirmarEstado = false, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpGenerarPartidos, conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@GeneracionRolTorneoId", generacionId);
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
        cmd.Parameters.AddWithValue("@ConfirmarEstado", confirmarEstado);
        cmd.Parameters.AddWithValue("@SoloConfirmarEstado", soloConfirmarEstado);

        using var adapter = new SqlDataAdapter(cmd);
        var ds = new DataSet();
        adapter.Fill(ds);

        var partidos = ds.Tables.Count > 0 ? ds.Tables[0] : null;
        var notas = ds.Tables.Count > 1 ? ds.Tables[1] : null;

        return new GenerarPartidosGeneracionRolTorneoResponse
        {
            Partidos = partidos is null ? new List<PartidoGeneracionRolTorneoDto>() : Funciones.DataTableToList<PartidoGeneracionRolTorneoDto>(partidos),
            Notas = notas is null ? new List<GeneracionRolNotaDto>() : Funciones.DataTableToList<GeneracionRolNotaDto>(notas)
        };
    }

    public async Task<List<PartidoGeneracionRolTorneoDto>> ConsultarPartidosAsync(Guid generacionId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpConsultarPartidos, conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@GeneracionRolTorneoId", generacionId);

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd))
        {
            adapter.Fill(dt);
        }

        return Funciones.DataTableToList<PartidoGeneracionRolTorneoDto>(dt);
    }

    public async Task<List<PartidoGeneracionRolTorneoDto>> ActualizarOrdenPartidosAsync(Guid generacionId, Guid usuarioId, IReadOnlyList<OrdenPartidoItemRequest> partidos, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpActualizarOrdenPartidos, conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@GeneracionRolTorneoId", generacionId);
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        var table = BuildOrdenPartidosTable(partidos);
        var param = cmd.Parameters.AddWithValue("@Partidos", table);
        param.SqlDbType = SqlDbType.Structured;
        param.TypeName = "dbo.WGeneracionRolPartidoOrdenType";

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd))
        {
            adapter.Fill(dt);
        }

        return Funciones.DataTableToList<PartidoGeneracionRolTorneoDto>(dt);
    }

    public async Task<PartidoGeneracionRolTorneoDto?> ActualizarObservacionPartidoAsync(Guid partidoId, string? observaciones, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpActualizarObservacionPartido, conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@PartidoId", partidoId);
        cmd.Parameters.AddWithValue("@Observaciones", string.IsNullOrWhiteSpace(observaciones) ? DBNull.Value : observaciones.Trim());
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        return await ExecuteSingleAsync<PartidoGeneracionRolTorneoDto>(cmd, ct);
    }

    private static async Task<T?> ExecuteSingleAsync<T>(SqlCommand cmd, CancellationToken ct = default) where T : class
    {
        using var reader = await cmd.ExecuteReaderAsync(ct);
        var dt = new DataTable();
        dt.Load(reader);
        return Funciones.DataTableToList<T>(dt).FirstOrDefault();
    }

    private static DataTable BuildCanchasTable(IEnumerable<GeneracionRolCanchaItemRequest> canchas)
    {
        var table = new DataTable();
        table.Columns.Add("NombreCancha", typeof(string));
        table.Columns.Add("CategoriaId", typeof(Guid));

        foreach (var item in canchas ?? Array.Empty<GeneracionRolCanchaItemRequest>())
        {
            if (item is null) continue;
            var name = item.NombreCancha?.Trim() ?? string.Empty;
            table.Rows.Add(name, item.CategoriaId);
        }

        return table;
    }

    private static DataTable BuildOrdenPartidosTable(IEnumerable<OrdenPartidoItemRequest> partidos)
    {
        var table = new DataTable();
        table.Columns.Add("PartidoId", typeof(Guid));
        table.Columns.Add("Orden", typeof(int));

        foreach (var item in partidos ?? Array.Empty<OrdenPartidoItemRequest>())
        {
            if (item is null) continue;
            table.Rows.Add(item.PartidoId, item.Orden);
        }

        return table;
    }
}
