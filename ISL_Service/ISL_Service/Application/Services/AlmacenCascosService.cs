using ISL_Service.Application.DTOs.AlmacenCascos;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Application.Services;

/// <summary>
/// Servicio Almacen de Cascos: validaciones (repartidores/tipos activos, kilos, piezas) y mapeo de errores SQL a 400/409.
/// </summary>
public class AlmacenCascosService : IAlmacenCascosService
{
    private readonly IAlmacenCascosRepository _repository;
    private readonly ICatalogosRepository _catalogos;

    public AlmacenCascosService(IAlmacenCascosRepository repository, ICatalogosRepository catalogos)
    {
        _repository = repository;
        _catalogos = catalogos;
    }

    public async Task<List<MovimientoCascoDto>> ConsultarMovimientosAsync(int? estatus, int? tipoMovimiento, DateTime? fechaInicio, DateTime? fechaFin, CancellationToken ct = default)
    {
        return await _repository.ConsultarMovimientosAsync(estatus, tipoMovimiento, fechaInicio, fechaFin, ct);
    }

    public async Task<List<MovimientoCascoDetalleDto>> GetDetalleMovimientoAsync(int idMovimiento, CancellationToken ct = default)
    {
        return await _repository.GetDetalleMovimientoAsync(idMovimiento, ct);
    }

    public async Task<int> CrearSalidaAsync(CreateSalidaRequest request, string usuario, CancellationToken ct = default)
    {
        await ValidarSalidaAsync(request, ct);

        try
        {
            return await _repository.InsertarSalidaAsync(request, usuario, ct);
        }
        catch (SqlException ex)
        {
            MapSqlException(ex);
            throw;
        }
    }

    public async Task AceptarEntradaAsync(CreateEntradaRequest request, string usuario, CancellationToken ct = default)
    {
        await ValidarEntradaAsync(request, ct);

        try
        {
            await _repository.AceptarEntradaAsync(request, usuario, ct);
        }
        catch (SqlException ex)
        {
            MapSqlException(ex);
            throw;
        }
    }

    public async Task ActualizarSalidaAsync(int idMovimiento, UpdateSalidaRequest request, string usuario, CancellationToken ct = default)
    {
        if (idMovimiento <= 0)
            throw new ArgumentException("idMovimiento es requerido y debe ser mayor a 0.");

        await ValidarSalidaUpdateAsync(request, ct);

        try
        {
            await _repository.ActualizarSalidaAsync(idMovimiento, request, usuario, ct);
        }
        catch (SqlException ex)
        {
            MapSqlException(ex);
            throw;
        }
    }

    public async Task ActualizarEntradaAsync(int idMovimiento, UpdateEntradaRequest request, string usuario, CancellationToken ct = default)
    {
        if (idMovimiento <= 0)
            throw new ArgumentException("idMovimiento es requerido y debe ser mayor a 0.");

        await ValidarEntradaUpdateAsync(request, ct);

        try
        {
            await _repository.ActualizarEntradaAsync(idMovimiento, request, usuario, ct);
        }
        catch (SqlException ex)
        {
            MapSqlException(ex);
            throw;
        }
    }

    public async Task CancelarMovimientoAsync(int idMovimiento, string motivoCancelacion, string usuario, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivoCancelacion))
            throw new ArgumentException("motivoCancelacion es requerido.");

        try
        {
            await _repository.CancelarMovimientoAsync(idMovimiento, motivoCancelacion.Trim(), usuario, ct);
        }
        catch (SqlException ex)
        {
            MapSqlException(ex);
            throw;
        }
    }

    private async Task ValidarSalidaAsync(CreateSalidaRequest request, CancellationToken ct)
    {
        if (request.IdRepartidorEntrega <= 0)
            throw new ArgumentException("idRepartidorEntrega es requerido y debe ser mayor a 0.");

        if (request.Tarimas == null || request.Tarimas.Count == 0)
            throw new ArgumentException("tarimas es requerido y debe tener al menos un item.");

        var repartidores = await _catalogos.ListRepartidoresAsync(1, ct);
        if (repartidores.All(r => r.IdRepartidor != request.IdRepartidorEntrega))
            throw new ArgumentException("idRepartidorEntrega no es un repartidor activo.");

        var tipos = await _catalogos.ListTiposCascoAsync(1, ct);
        var idsTipoCasco = tipos.Select(t => t.IdTipoCasco).ToHashSet();
        var numeros = new HashSet<int>();

        foreach (var tarima in request.Tarimas)
        {
            if (tarima.NumeroTarima <= 0)
                throw new ArgumentException("Cada tarima debe tener numeroTarima mayor a 0.");
            if (!numeros.Add(tarima.NumeroTarima))
                throw new ArgumentException($"numeroTarima repetido: {tarima.NumeroTarima}.");
            if (tarima.Lineas == null || tarima.Lineas.Count == 0)
                throw new ArgumentException($"La tarima {tarima.NumeroTarima} debe tener al menos una linea.");

            var tiposTarima = new HashSet<int>();
            foreach (var linea in tarima.Lineas)
            {
                if (linea.IdTipoCasco <= 0)
                    throw new ArgumentException("Cada linea debe tener idTipoCasco mayor a 0.");
                if (!idsTipoCasco.Contains(linea.IdTipoCasco))
                    throw new ArgumentException($"El tipo de casco IdTipoCasco={linea.IdTipoCasco} no existe o no esta activo.");
                if (!tiposTarima.Add(linea.IdTipoCasco))
                    throw new ArgumentException($"Tipo de casco repetido en la tarima {tarima.NumeroTarima}: {linea.IdTipoCasco}.");
                if (linea.Piezas <= 0)
                    throw new ArgumentException("Cada linea debe tener piezas mayor a 0.");
            }
        }
    }

    private async Task ValidarEntradaAsync(CreateEntradaRequest request, CancellationToken ct)
    {
        if (request.IdMovimientoSalida <= 0)
            throw new ArgumentException("idMovimientoSalida es requerido y debe ser mayor a 0.");
        if (request.IdRepartidorRecibe <= 0)
            throw new ArgumentException("idRepartidorRecibe es requerido y debe ser mayor a 0.");
        if (request.Kilos <= 0)
            throw new ArgumentException("kilos debe ser mayor a 0.");

        request.Kilos = Math.Round(request.Kilos, 4);

        var repartidores = await _catalogos.ListRepartidoresAsync(1, ct);
        if (repartidores.All(r => r.IdRepartidor != request.IdRepartidorRecibe))
            throw new ArgumentException("idRepartidorRecibe no es un repartidor activo.");

        if (request.Detalle is { Count: > 0 })
        {
            var actual = await _repository.GetDetalleMovimientoAsync(request.IdMovimientoSalida, ct);

            var expected = request.Detalle
                .Select(x => (x.NumeroTarima, x.IdTipoCasco, x.Piezas))
                .OrderBy(x => x.NumeroTarima).ThenBy(x => x.IdTipoCasco).ThenBy(x => x.Piezas)
                .ToList();
            var current = actual
                .Select(x => (x.NumeroTarima, x.IdTipoCasco, x.Piezas))
                .OrderBy(x => x.NumeroTarima).ThenBy(x => x.IdTipoCasco).ThenBy(x => x.Piezas)
                .ToList();

            if (expected.Count != current.Count || !expected.SequenceEqual(current))
                throw new ArgumentException("El detalle de entrada no coincide con el detalle de la salida.");
        }
    }

    /// <summary>
    /// Mapea SqlException a ConflictException (409) o ArgumentException (400). Unique constraint -> 409.
    /// </summary>
    private static void MapSqlException(SqlException ex)
    {
        var msg = ex.Message;
        if (ex.Number == 2627 || ex.Number == 2601 || msg.Contains("UX_WMovimientoCasco_EntradaUnica", StringComparison.OrdinalIgnoreCase) || msg.Contains("duplicate", StringComparison.OrdinalIgnoreCase) && msg.Contains("IdMovimientoSalida", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Ya existe una entrada registrada para esta salida.", msg);
        if (msg.Contains("ya existe una entrada para esta salida", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Ya existe una entrada registrada para esta salida.", msg);
        if (msg.Contains("ya esta cancelado", StringComparison.OrdinalIgnoreCase) || msg.Contains("ya cancelado", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("El movimiento ya esta cancelado.", msg);
        if (msg.Contains("no se puede modificar", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException(msg, msg);
        if (msg.Contains("no se puede cancelar", StringComparison.OrdinalIgnoreCase) || msg.Contains("tiene entrada aceptada", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException(msg, msg);
        if (msg.Contains("no tiene detalle", StringComparison.OrdinalIgnoreCase) || msg.Contains("sin detalle", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(msg);
        if (msg.Contains("no valida", StringComparison.OrdinalIgnoreCase) || msg.Contains("Repartidor no valido", StringComparison.OrdinalIgnoreCase) || msg.Contains("salida no esta", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(msg);
        if (ex.Number >= 50000 && ex.Number <= 99999)
            throw new ArgumentException(msg);
    }

    private async Task ValidarSalidaUpdateAsync(UpdateSalidaRequest request, CancellationToken ct)
    {
        if (request.IdRepartidorEntrega <= 0)
            throw new ArgumentException("idRepartidorEntrega es requerido y debe ser mayor a 0.");
        if (request.Tarimas == null || request.Tarimas.Count == 0)
            throw new ArgumentException("tarimas es requerido y debe tener al menos un item.");

        var repartidores = await _catalogos.ListRepartidoresAsync(1, ct);
        if (repartidores.All(r => r.IdRepartidor != request.IdRepartidorEntrega))
            throw new ArgumentException("idRepartidorEntrega no es un repartidor activo.");

        var tipos = await _catalogos.ListTiposCascoAsync(1, ct);
        var idsTipoCasco = tipos.Select(t => t.IdTipoCasco).ToHashSet();
        var numeros = new HashSet<int>();

        foreach (var tarima in request.Tarimas)
        {
            if (tarima.NumeroTarima <= 0)
                throw new ArgumentException("Cada tarima debe tener numeroTarima mayor a 0.");
            if (!numeros.Add(tarima.NumeroTarima))
                throw new ArgumentException($"numeroTarima repetido: {tarima.NumeroTarima}.");
            if (tarima.Lineas == null || tarima.Lineas.Count == 0)
                throw new ArgumentException($"La tarima {tarima.NumeroTarima} debe tener al menos una linea.");

            var tiposTarima = new HashSet<int>();
            foreach (var linea in tarima.Lineas)
            {
                if (linea.IdTipoCasco <= 0)
                    throw new ArgumentException("Cada linea debe tener idTipoCasco mayor a 0.");
                if (!idsTipoCasco.Contains(linea.IdTipoCasco))
                    throw new ArgumentException($"El tipo de casco IdTipoCasco={linea.IdTipoCasco} no existe o no esta activo.");
                if (!tiposTarima.Add(linea.IdTipoCasco))
                    throw new ArgumentException($"Tipo de casco repetido en la tarima {tarima.NumeroTarima}: {linea.IdTipoCasco}.");
                if (linea.Piezas <= 0)
                    throw new ArgumentException("Cada linea debe tener piezas mayor a 0.");
            }
        }
    }

    private async Task ValidarEntradaUpdateAsync(UpdateEntradaRequest request, CancellationToken ct)
    {
        if (request.IdRepartidorRecibe <= 0)
            throw new ArgumentException("idRepartidorRecibe es requerido y debe ser mayor a 0.");
        if (request.Kilos <= 0)
            throw new ArgumentException("kilos debe ser mayor a 0.");
        request.Kilos = Math.Round(request.Kilos, 4);

        var repartidores = await _catalogos.ListRepartidoresAsync(1, ct);
        if (repartidores.All(r => r.IdRepartidor != request.IdRepartidorRecibe))
            throw new ArgumentException("idRepartidorRecibe no es un repartidor activo.");
    }
}
