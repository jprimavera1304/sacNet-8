using System.Data;
using ISL_Service.Application.DTOs.AlmacenCascos;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// Repository Almacen de Cascos: SP sp_w_* y consultas directas a WMovimientoCasco / WMovimientoCascoDetalle.
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
       ISNULL(tu.[Tipo de Usado], N'') AS NombreTarima,
       ISNULL(tu.[Tipo de Usado], N'') AS TipoCascoDescripcion
FROM dbo.WMovimientoCascoDetalle d
LEFT JOIN [Catalogo TiposUsados] tu ON tu.IdTipoUsado = d.IdTipoCasco
WHERE d.IdMovimiento = @IdMovimiento
ORDER BY d.NumeroTarima, d.IdDetalle";

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
                throw new ArgumentException("No se obtuvo IdMovimiento al insertar cabecera.");

            var lineas = FlattenTarimas(request.Tarimas);
            var totalTarimas = lineas.Select(x => x.NumeroTarima).Distinct().Count();
            var totalPiezas = lineas.Sum(x => x.Piezas);

            foreach (var item in lineas)
            {
                await using var cmdDet = new SqlCommand(@"
INSERT INTO dbo.WMovimientoCascoDetalle (IdMovimiento, IdTarima, IdTipoCasco, NumeroTarima, Piezas)
VALUES (@IdMovimiento, @IdTarima, @IdTipoCasco, @NumeroTarima, @Piezas)", conn, tran);
                cmdDet.Parameters.AddWithValue("@IdMovimiento", idMovimiento);
                cmdDet.Parameters.AddWithValue("@IdTarima", DBNull.Value);
                cmdDet.Parameters.AddWithValue("@IdTipoCasco", item.IdTipoCasco);
                cmdDet.Parameters.AddWithValue("@NumeroTarima", item.NumeroTarima);
                cmdDet.Parameters.AddWithValue("@Piezas", item.Piezas);
                await cmdDet.ExecuteNonQueryAsync(ct);
            }

            await using var cmdUpd = new SqlCommand(@"
UPDATE m
SET TotalTarimas = @TotalTarimas,
    TotalPiezas  = @TotalPiezas,
    TotalKilos   = 0,
    NombreTarima = det.TipoCascoDescripcion
FROM dbo.WMovimientoCasco m
OUTER APPLY (
    SELECT TOP 1 ISNULL(tu.[Tipo de Usado], N'') AS TipoCascoDescripcion
    FROM dbo.WMovimientoCascoDetalle d
    LEFT JOIN [Catalogo TiposUsados] tu ON tu.IdTipoUsado = d.IdTipoCasco
    WHERE d.IdMovimiento = @IdMovimiento
    ORDER BY d.NumeroTarima, d.IdDetalle
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
        using var tran = conn.BeginTransaction();

        try
        {
            await using (var cmdMov = new SqlCommand(@"
SELECT TipoMovimiento, Estatus
FROM dbo.WMovimientoCasco WITH (UPDLOCK, ROWLOCK)
WHERE IdMovimiento = @IdMovimiento", conn, tran))
            {
                cmdMov.Parameters.AddWithValue("@IdMovimiento", idMovimiento);
                await using var reader = await cmdMov.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                    throw new ArgumentException("Movimiento no encontrado.");

                var tipoMov = reader.GetInt32(0);
                var estatus = reader.GetInt32(1);
                if (tipoMov != 1)
                    throw new ArgumentException("El movimiento no corresponde a una salida.");
                if (estatus == 3)
                    throw new ArgumentException("No se puede modificar un movimiento cancelado.");
                await reader.CloseAsync();
            }

            await using (var cmdUpdHeader = new SqlCommand(@"
UPDATE dbo.WMovimientoCasco
SET IdRepartidorEntrega = @IdRepartidorEntrega,
    Observaciones = @Observaciones,
    UsuarioModificacion = @Usuario,
    FechaModificacion = GETDATE()
WHERE IdMovimiento = @IdMovimiento", conn, tran))
            {
                cmdUpdHeader.Parameters.AddWithValue("@IdMovimiento", idMovimiento);
                cmdUpdHeader.Parameters.AddWithValue("@IdRepartidorEntrega", request.IdRepartidorEntrega);
                cmdUpdHeader.Parameters.AddWithValue("@Observaciones", string.IsNullOrWhiteSpace(request.Observaciones) ? DBNull.Value : request.Observaciones!.Trim());
                cmdUpdHeader.Parameters.AddWithValue("@Usuario", string.IsNullOrWhiteSpace(usuario) ? DBNull.Value : usuario.Trim());
                await cmdUpdHeader.ExecuteNonQueryAsync(ct);
            }

            await using (var cmdDel = new SqlCommand("DELETE FROM dbo.WMovimientoCascoDetalle WHERE IdMovimiento = @IdMovimiento", conn, tran))
            {
                cmdDel.Parameters.AddWithValue("@IdMovimiento", idMovimiento);
                await cmdDel.ExecuteNonQueryAsync(ct);
            }

            var lineas = FlattenTarimas(request.Tarimas);
            var totalTarimas = lineas.Select(x => x.NumeroTarima).Distinct().Count();
            var totalPiezas = lineas.Sum(x => x.Piezas);

            foreach (var item in lineas)
            {
                await using var cmdDet = new SqlCommand(@"
INSERT INTO dbo.WMovimientoCascoDetalle (IdMovimiento, IdTarima, IdTipoCasco, NumeroTarima, Piezas)
VALUES (@IdMovimiento, @IdTarima, @IdTipoCasco, @NumeroTarima, @Piezas)", conn, tran);
                cmdDet.Parameters.AddWithValue("@IdMovimiento", idMovimiento);
                cmdDet.Parameters.AddWithValue("@IdTarima", DBNull.Value);
                cmdDet.Parameters.AddWithValue("@IdTipoCasco", item.IdTipoCasco);
                cmdDet.Parameters.AddWithValue("@NumeroTarima", item.NumeroTarima);
                cmdDet.Parameters.AddWithValue("@Piezas", item.Piezas);
                await cmdDet.ExecuteNonQueryAsync(ct);
            }

            await using (var cmdUpdTot = new SqlCommand(@"
UPDATE m
SET TotalTarimas = @TotalTarimas,
    TotalPiezas  = @TotalPiezas,
    NombreTarima = det.TipoCascoDescripcion,
    UsuarioModificacion = @Usuario,
    FechaModificacion = GETDATE()
FROM dbo.WMovimientoCasco m
OUTER APPLY (
    SELECT TOP 1 ISNULL(tu.[Tipo de Usado], N'') AS TipoCascoDescripcion
    FROM dbo.WMovimientoCascoDetalle d
    LEFT JOIN [Catalogo TiposUsados] tu ON tu.IdTipoUsado = d.IdTipoCasco
    WHERE d.IdMovimiento = @IdMovimiento
    ORDER BY d.NumeroTarima, d.IdDetalle
) det
WHERE m.IdMovimiento = @IdMovimiento", conn, tran))
            {
                cmdUpdTot.Parameters.AddWithValue("@IdMovimiento", idMovimiento);
                cmdUpdTot.Parameters.AddWithValue("@TotalTarimas", totalTarimas);
                cmdUpdTot.Parameters.AddWithValue("@TotalPiezas", totalPiezas);
                cmdUpdTot.Parameters.AddWithValue("@Usuario", string.IsNullOrWhiteSpace(usuario) ? DBNull.Value : usuario.Trim());
                await cmdUpdTot.ExecuteNonQueryAsync(ct);
            }

            tran.Commit();
        }
        catch
        {
            tran.Rollback();
            throw;
        }
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

    private static List<(int NumeroTarima, int IdTipoCasco, int Piezas)> FlattenTarimas(IEnumerable<SalidaTarimaDto> tarimas)
    {
        var result = new List<(int NumeroTarima, int IdTipoCasco, int Piezas)>();

        foreach (var tarima in tarimas)
        {
            foreach (var linea in tarima.Lineas)
            {
                result.Add((tarima.NumeroTarima, linea.IdTipoCasco, linea.Piezas));
            }
        }

        return result;
    }
}
