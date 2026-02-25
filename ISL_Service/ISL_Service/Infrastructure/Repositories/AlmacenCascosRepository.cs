using System.Data;
using ISL_Service.Application.DTOs.AlmacenCascos;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// Repository Almacén de Cascos: SP sp_w_* y consultas directas a WMovimientoCasco / WMovimientoCascoDetalle.
/// </summary>
public class AlmacenCascosRepository : IAlmacenCascosRepository
{
    private readonly IConfiguration _configuration;
    private const string SpConsultar = "dbo.sp_w_ConsultarMovimientosCasco";
    private const string SpInsertarSalida = "dbo.sp_w_InsertarMovimientoCasco";
    private const string SpAceptarEntrada = "dbo.sp_w_AceptarEntradaCasco";
    private const string SpCancelar = "dbo.sp_w_CancelarMovimientoCasco";
    private const string SpActualizar = "dbo.sp_w_ActualizarMovimientoCasco";

    public AlmacenCascosRepository(IConfiguration configuration)
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

    public async Task<List<MovimientoCascoDto>> ConsultarMovimientosAsync(int? estatus, int? tipoMovimiento, DateTime? fechaInicio, DateTime? fechaFin, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpConsultar, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@Estatus", (object?)estatus ?? DBNull.Value);

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd))
        {
            adapter.Fill(dt);
        }

        var list = Funciones.DataTableToList<MovimientoCascoDto>(dt);

        if (tipoMovimiento.HasValue)
            list = list.Where(x => x.TipoMovimiento == tipoMovimiento.Value).ToList();

        if (fechaInicio.HasValue)
            list = list.Where(x => x.FechaCreacion.HasValue && x.FechaCreacion.Value.Date >= fechaInicio.Value.Date).ToList();

        if (fechaFin.HasValue)
            list = list.Where(x => x.FechaCreacion.HasValue && x.FechaCreacion.Value.Date <= fechaFin.Value.Date).ToList();

        return list;
    }

    public async Task<List<MovimientoCascoDetalleDto>> GetDetalleMovimientoAsync(int idMovimiento, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        const string sql = @"
SELECT d.IdDetalle, d.IdMovimiento, d.IdTarima, d.IdTipoCasco, d.NumeroTarima, d.Piezas,
       t.NombreTarima,
       ISNULL(tu.[Tipo de Usado], N'') AS TipoCascoDescripcion
FROM dbo.WMovimientoCascoDetalle d
INNER JOIN dbo.WTarima t ON t.IdTarima = d.IdTarima
LEFT JOIN [Catalogo TiposUsados] tu ON tu.IdTipoUsado = d.IdTipoCasco
WHERE d.IdMovimiento = @IdMovimiento
ORDER BY d.IdDetalle";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@IdMovimiento", idMovimiento);

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd))
        {
            adapter.Fill(dt);
        }
        return Funciones.DataTableToList<MovimientoCascoDetalleDto>(dt);
    }

    public async Task<int> InsertarSalidaAsync(CreateSalidaRequest request, string usuario, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        using var tran = conn.BeginTransaction();

        try
        {
            int idMovimiento;
            // 1) SP crea cabecera SALIDA y devuelve IdMovimiento
            await using (var cmdSp = new SqlCommand(SpInsertarSalida, conn, tran))
            {
                cmdSp.CommandType = CommandType.StoredProcedure;
                cmdSp.Parameters.AddWithValue("@IdRepartidorEntrega", request.IdRepartidorEntrega);
                cmdSp.Parameters.AddWithValue("@Observaciones", string.IsNullOrWhiteSpace(request.Observaciones) ? DBNull.Value : request.Observaciones!.Trim());
                cmdSp.Parameters.AddWithValue("@Usuario", string.IsNullOrWhiteSpace(usuario) ? DBNull.Value : usuario.Trim());

                await using var reader = await cmdSp.ExecuteReaderAsync(ct);
                idMovimiento = 0;
                if (await reader.ReadAsync(ct))
                    idMovimiento = Convert.ToInt32(reader["IdMovimiento"]);
                await reader.CloseAsync();
            }

            if (idMovimiento <= 0)
            {
                tran.Rollback();
                throw new InvalidOperationException("No se obtuvo IdMovimiento al insertar cabecera.");
            }

            // 2) Obtener IdTipoCasco por cada IdTarima
            var idTarimas = request.Detalle.Select(x => x.IdTarima).Distinct().ToList();
            var tarimaTipoMap = new Dictionary<int, int>();
            if (idTarimas.Count > 0)
            {
                var inList = string.Join(",", idTarimas);
                await using var cmdTarimas = new SqlCommand($"SELECT IdTarima, IdTipoCasco FROM dbo.WTarima WHERE IdTarima IN ({inList})", conn, tran);
                await using var rTarimas = await cmdTarimas.ExecuteReaderAsync(ct);
                while (await rTarimas.ReadAsync(ct))
                    tarimaTipoMap[rTarimas.GetInt32(0)] = rTarimas.GetInt32(1);
            }

            int totalTarimas = request.Detalle.Sum(x => x.NumeroTarima);
            int totalPiezas = 0;

            foreach (var item in request.Detalle)
            {
                if (!tarimaTipoMap.TryGetValue(item.IdTarima, out var idTipoCasco))
                {
                    tran.Rollback();
                    throw new InvalidOperationException($"Tarima IdTarima={item.IdTarima} no encontrada o sin IdTipoCasco.");
                }
                totalPiezas += item.Piezas;

                await using var cmdDet = new SqlCommand(@"
INSERT INTO dbo.WMovimientoCascoDetalle (IdMovimiento, IdTarima, IdTipoCasco, NumeroTarima, Piezas)
VALUES (@IdMovimiento, @IdTarima, @IdTipoCasco, @NumeroTarima, @Piezas)", conn, tran);
                cmdDet.Parameters.AddWithValue("@IdMovimiento", idMovimiento);
                cmdDet.Parameters.AddWithValue("@IdTarima", item.IdTarima);
                cmdDet.Parameters.AddWithValue("@IdTipoCasco", idTipoCasco);
                cmdDet.Parameters.AddWithValue("@NumeroTarima", item.NumeroTarima);
                cmdDet.Parameters.AddWithValue("@Piezas", item.Piezas);
                await cmdDet.ExecuteNonQueryAsync(ct);
            }

            // 3) Actualizar totales en cabecera + NombreTarima (primer detalle)
            await using var cmdUpd = new SqlCommand(@"
UPDATE m
SET TotalTarimas = @TotalTarimas,
    TotalPiezas  = @TotalPiezas,
    TotalKilos   = 0,
    NombreTarima = ISNULL(m.NombreTarima, det.NombreTarima)
FROM dbo.WMovimientoCasco m
OUTER APPLY (
    SELECT TOP 1 t.NombreTarima
    FROM dbo.WMovimientoCascoDetalle d
    INNER JOIN dbo.WTarima t ON t.IdTarima = d.IdTarima
    WHERE d.IdMovimiento = @IdMovimiento
    ORDER BY d.IdDetalle
) det
WHERE m.IdMovimiento = @IdMovimiento", conn, tran);
            cmdUpd.Parameters.AddWithValue("@TotalTarimas", totalTarimas);
            cmdUpd.Parameters.AddWithValue("@TotalPiezas", totalPiezas);
            cmdUpd.Parameters.AddWithValue("@IdMovimiento", idMovimiento);
            await cmdUpd.ExecuteNonQueryAsync(ct);

            tran.Commit();
            return idMovimiento;
        }
        catch
        {
            tran.Rollback();
            throw;
        }
    }

    public async Task AceptarEntradaAsync(CreateEntradaRequest request, string usuario, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpAceptarEntrada, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@IdMovimientoSalida", request.IdMovimientoSalida);
        cmd.Parameters.AddWithValue("@IdRepartidorRecibe", request.IdRepartidorRecibe);
        cmd.Parameters.AddWithValue("@Kilos", request.Kilos);
        cmd.Parameters.AddWithValue("@Observaciones", string.IsNullOrWhiteSpace(request.Observaciones) ? DBNull.Value : request.Observaciones!.Trim());
        cmd.Parameters.AddWithValue("@Usuario", string.IsNullOrWhiteSpace(usuario) ? DBNull.Value : usuario.Trim());

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ActualizarSalidaAsync(int idMovimiento, UpdateSalidaRequest request, string usuario, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpActualizar, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@IdMovimiento", idMovimiento);
        cmd.Parameters.AddWithValue("@IdRepartidorEntrega", request.IdRepartidorEntrega);
        cmd.Parameters.AddWithValue("@IdTarima", request.IdTarima);
        cmd.Parameters.AddWithValue("@NumeroTarima", request.NumeroTarima);
        cmd.Parameters.AddWithValue("@Piezas", request.Piezas);
        cmd.Parameters.AddWithValue("@Kilos", DBNull.Value);
        cmd.Parameters.AddWithValue("@IdRepartidorRecibe", DBNull.Value);
        cmd.Parameters.AddWithValue("@Observaciones", string.IsNullOrWhiteSpace(request.Observaciones) ? DBNull.Value : request.Observaciones!.Trim());
        cmd.Parameters.AddWithValue("@Usuario", string.IsNullOrWhiteSpace(usuario) ? DBNull.Value : usuario.Trim());

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ActualizarEntradaAsync(int idMovimiento, UpdateEntradaRequest request, string usuario, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpActualizar, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@IdMovimiento", idMovimiento);
        cmd.Parameters.AddWithValue("@IdRepartidorEntrega", DBNull.Value);
        cmd.Parameters.AddWithValue("@IdTarima", DBNull.Value);
        cmd.Parameters.AddWithValue("@NumeroTarima", DBNull.Value);
        cmd.Parameters.AddWithValue("@Piezas", DBNull.Value);
        cmd.Parameters.AddWithValue("@Kilos", request.Kilos);
        cmd.Parameters.AddWithValue("@IdRepartidorRecibe", request.IdRepartidorRecibe);
        cmd.Parameters.AddWithValue("@Observaciones", string.IsNullOrWhiteSpace(request.Observaciones) ? DBNull.Value : request.Observaciones!.Trim());
        cmd.Parameters.AddWithValue("@Usuario", string.IsNullOrWhiteSpace(usuario) ? DBNull.Value : usuario.Trim());

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task CancelarMovimientoAsync(int idMovimiento, string motivoCancelacion, string usuario, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpCancelar, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@IdMovimiento", idMovimiento);
        cmd.Parameters.AddWithValue("@Motivo", motivoCancelacion.Trim());
        cmd.Parameters.AddWithValue("@Usuario", string.IsNullOrWhiteSpace(usuario) ? DBNull.Value : usuario.Trim());

        await cmd.ExecuteNonQueryAsync(ct);
    }
}
