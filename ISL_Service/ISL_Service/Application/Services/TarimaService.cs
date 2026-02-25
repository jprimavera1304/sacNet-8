using ISL_Service.Application.DTOs.Tarima;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Application.Services;

public class TarimaService : ITarimaService
{
    private readonly ITarimaRepository _repository;

    public TarimaService(ITarimaRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<TarimaDto>> ConsultarTarimasAsync(int? idStatus, string? busqueda, CancellationToken ct = default)
    {
        return await _repository.ConsultarTarimasAsync(idStatus, busqueda?.Trim(), ct);
    }

    public async Task<TarimaDto?> GetByIdAsync(int idTarima, CancellationToken ct = default)
    {
        var lista = await _repository.ConsultarTarimasAsync(null, null, ct);
        return lista.FirstOrDefault(t => t.IdTarima == idTarima);
    }

    public async Task<int> CrearAsync(CreateTarimaRequest request, string usuarioCreacion, CancellationToken ct = default)
    {
        try
        {
            return await _repository.InsertarTarimaAsync(request, usuarioCreacion, ct);
        }
        catch (SqlException ex)
        {
            ThrowMappedException(ex);
            throw;
        }
    }

    public async Task ActualizarAsync(int idTarima, UpdateTarimaRequest request, string usuarioModificacion, CancellationToken ct = default)
    {
        try
        {
            await _repository.ActualizarTarimaAsync(idTarima, request, usuarioModificacion, ct);
        }
        catch (SqlException ex)
        {
            ThrowMappedException(ex);
            throw;
        }
    }

    public async Task CambiarStatusAsync(int idTarima, int idStatus, string? usuario, CancellationToken ct = default)
    {
        if (idStatus != 1 && idStatus != 2)
            throw new ArgumentException("IdStatus debe ser 1 (Activo) o 2 (Cancelado).");

        var lista = await _repository.ConsultarTarimasAsync(null, null, ct);
        var tarima = lista.FirstOrDefault(t => t.IdTarima == idTarima);
        if (tarima == null)
            throw new NotFoundException("La tarima no existe.", idTarima.ToString());
        if (tarima.IdStatus == idStatus)
            return; // 200 sin cambios

        try
        {
            var usuarioParaSp = string.IsNullOrWhiteSpace(usuario) ? "Sistema" : usuario.Trim();
            await _repository.CambiarStatusTarimaAsync(idTarima, idStatus, usuarioParaSp, ct);
        }
        catch (SqlException ex)
        {
            ThrowMappedException(ex);
            throw;
        }
    }

    private static void ThrowMappedException(SqlException ex)
    {
        var msg = ex.Message;
        if (msg.Contains("Ya existe una tarima activa con el mismo nombre y tipo de casco", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Ya existe una tarima activa con el mismo nombre y tipo de casco.", msg);
        if (msg.Contains("La tarima no existe", StringComparison.OrdinalIgnoreCase))
            throw new NotFoundException("La tarima no existe.", msg);
        if (msg.Contains("Solo se puede actualizar una tarima activa", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Solo se puede actualizar una tarima activa.", msg);
        if (msg.Contains("IdStatus debe ser 1", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(msg);
        if (ex.Number >= 50000 && ex.Number <= 99999)
            throw new ArgumentException(msg);
    }
}
