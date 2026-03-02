using ISL_Service.Application.DTOs.Temporadas;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Application.Services;

public class TemporadasService : ITemporadasService
{
    private readonly ITemporadasRepository _repository;

    public TemporadasService(ITemporadasRepository repository)
    {
        _repository = repository;
    }

    public Task<List<TemporadaDto>> ConsultarTemporadasAsync(byte? estado, string? texto, CancellationToken ct = default)
        => _repository.ConsultarTemporadasAsync(estado, NormalizeText(texto), ct);

    public Task<List<TemporadaDto>> ConsultarTemporadasListadoAsync(byte? estado, string? texto, Guid usuarioSistemaId, CancellationToken ct = default)
        => _repository.ConsultarTemporadasListadoAsync(estado, NormalizeText(texto), usuarioSistemaId, ct);

    public async Task<TemporadaDto?> GetTemporadaByIdAsync(Guid id, CancellationToken ct = default)
    {
        var list = await _repository.ConsultarTemporadasAsync(null, null, ct);
        return list.FirstOrDefault(x => x.Id == id);
    }

    public async Task<TemporadaDto> CrearTemporadaAsync(CreateTemporadaRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        try
        {
            var created = await _repository.InsertarTemporadaAsync(request, usuarioId, ct);
            if (created is null)
                throw new InvalidOperationException("No se pudo crear la temporada.");
            return created;
        }
        catch (SqlException ex)
        {
            ThrowMappedException(ex);
            throw;
        }
    }

    public async Task<TemporadaDto> ActualizarTemporadaAsync(Guid id, UpdateTemporadaRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        var actual = await GetTemporadaByIdAsync(id, ct);
        if (actual is null)
            throw new NotFoundException("La temporada no existe.", id.ToString());
        if (actual.Estado is 0 or 2)
            throw new ConflictException("No se puede modificar: temporada inhabilitada o cancelada.");

        try
        {
            var updated = await _repository.ActualizarTemporadaAsync(id, request, usuarioId, ct);
            if (updated is null)
                throw new InvalidOperationException("No se pudo actualizar la temporada.");
            return updated;
        }
        catch (SqlException ex)
        {
            ThrowMappedException(ex);
            throw;
        }
    }

    public async Task<TemporadaDto> CancelarTemporadaAsync(Guid id, string? motivo, Guid usuarioId, CancellationToken ct = default)
    {
        var actual = await GetTemporadaByIdAsync(id, ct);
        if (actual is null)
            throw new NotFoundException("La temporada no existe.", id.ToString());
        if (actual.Estado == 2)
            return actual;

        try
        {
            var canceled = await _repository.CancelarTemporadaAsync(id, motivo, usuarioId, ct);
            if (canceled is null)
                throw new InvalidOperationException("No se pudo cancelar la temporada.");
            return canceled;
        }
        catch (SqlException ex)
        {
            ThrowMappedException(ex);
            throw;
        }
    }

    public async Task<TemporadaDto> ReactivarTemporadaAsync(Guid id, Guid usuarioId, CancellationToken ct = default)
    {
        var actual = await GetTemporadaByIdAsync(id, ct);
        if (actual is null)
            throw new NotFoundException("La temporada no existe.", id.ToString());
        if (actual.Estado == 1)
            return actual;
        if (actual.Estado == 3)
            throw new ConflictException("No se puede reactivar una temporada cerrada.");

        try
        {
            var updated = await _repository.ReactivarTemporadaAsync(id, usuarioId, ct);
            if (updated is null)
                throw new InvalidOperationException("No se pudo reactivar la temporada.");
            return updated;
        }
        catch (SqlException ex)
        {
            ThrowMappedException(ex);
            throw;
        }
    }

    public Task<List<TorneoDto>> ConsultarTorneosAsync(Guid? temporadaId, byte? estado, string? texto, CancellationToken ct = default)
        => _repository.ConsultarTorneosAsync(temporadaId, estado, NormalizeText(texto), ct);

    public async Task<TorneoDto?> GetTorneoByIdAsync(Guid id, CancellationToken ct = default)
    {
        var list = await _repository.ConsultarTorneosAsync(null, null, null, ct);
        return list.FirstOrDefault(x => x.Id == id);
    }

    public async Task<TorneoDto> CrearTorneoAsync(CreateTorneoRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        try
        {
            var created = await _repository.InsertarTorneoAsync(request, usuarioId, ct);
            if (created is null)
                throw new InvalidOperationException("No se pudo crear el torneo.");
            return created;
        }
        catch (SqlException ex)
        {
            ThrowMappedException(ex);
            throw;
        }
    }

    public async Task<TorneoDto> ActualizarTorneoAsync(Guid id, UpdateTorneoRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        var actual = await GetTorneoByIdAsync(id, ct);
        if (actual is null)
            throw new NotFoundException("El torneo no existe.", id.ToString());
        if (actual.Estado is 0 or 2)
            throw new ConflictException("No se puede modificar: torneo inhabilitado o cancelado.");

        try
        {
            var updated = await _repository.ActualizarTorneoAsync(id, request, usuarioId, ct);
            if (updated is null)
                throw new InvalidOperationException("No se pudo actualizar el torneo.");
            return updated;
        }
        catch (SqlException ex)
        {
            ThrowMappedException(ex);
            throw;
        }
    }

    public async Task<TorneoDto> CancelarTorneoAsync(Guid id, string? motivo, Guid usuarioId, CancellationToken ct = default)
    {
        var actual = await GetTorneoByIdAsync(id, ct);
        if (actual is null)
            throw new NotFoundException("El torneo no existe.", id.ToString());
        if (actual.Estado == 2)
            return actual;

        try
        {
            var canceled = await _repository.CancelarTorneoAsync(id, motivo, usuarioId, ct);
            if (canceled is null)
                throw new InvalidOperationException("No se pudo cancelar el torneo.");
            return canceled;
        }
        catch (SqlException ex)
        {
            ThrowMappedException(ex);
            throw;
        }
    }

    private static string? NormalizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        return text.Trim();
    }

    private static void ThrowMappedException(SqlException ex)
    {
        var msg = ex.Message;
        if (msg.Contains("No se puede modificar: temporada inhabilitada o cancelada", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("No se puede modificar: temporada inhabilitada o cancelada.", msg);
        if (msg.Contains("No se puede modificar: torneo inhabilitado o cancelado", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("No se puede modificar: torneo inhabilitado o cancelado.", msg);
        if (msg.Contains("No se puede cancelar: existen torneos activos", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("No se puede cancelar: existen torneos activos en la temporada.", msg);
        if (msg.Contains("No se puede reactivar", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException(msg);
        if (msg.Contains("Temporada invalida o no activa", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Temporada invalida o no activa.");
        if (msg.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("duplic", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("UX_WTemporada_Nombre", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Ya existe una temporada con ese nombre.", msg);
        if (msg.Contains("UX_WTorneo_Temporada_Nombre", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Ya existe un torneo con ese nombre en la temporada.", msg);
        if (ex.Number >= 50000 && ex.Number <= 99999)
            throw new ArgumentException(msg);
    }
}
