using ISL_Service.Application.DTOs.InscripcionesTorneo;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Application.Services;

public class InscripcionesTorneoService : IInscripcionesTorneoService
{
    private readonly IInscripcionesTorneoRepository _repository;

    public InscripcionesTorneoService(IInscripcionesTorneoRepository repository)
    {
        _repository = repository;
    }

    public Task<List<InscripcionTorneoDto>> ConsultarAsync(Guid? torneoId, Guid? categoriaId, byte? estado, string? texto, CancellationToken ct = default)
        => _repository.ConsultarAsync(torneoId, categoriaId, estado, NormalizeText(texto), ct);

    public async Task<InscripcionTorneoDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var list = await _repository.ConsultarAsync(null, null, null, null, ct);
        return list.FirstOrDefault(x => x.Id == id);
    }

    public async Task<InscripcionTorneoDto> CrearAsync(CreateInscripcionTorneoRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        try
        {
            var created = await _repository.InsertarAsync(request, usuarioId, ct);
            if (created is null)
                throw new InvalidOperationException("No se pudo crear la inscripci\u00F3n.");
            return created;
        }
        catch (SqlException ex)
        {
            ThrowMappedException(ex);
            throw;
        }
    }

    public async Task<InscripcionTorneoDto> ActualizarAsync(Guid id, UpdateInscripcionTorneoRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        var actual = await GetByIdAsync(id, ct);
        if (actual is null)
            throw new NotFoundException("La inscripci\u00F3n no existe.", id.ToString());
        if (actual.Estado == 2)
            throw new ConflictException("No se puede modificar una inscripci\u00F3n inhabilitada.");

        try
        {
            var updated = await _repository.ActualizarAsync(id, request, usuarioId, ct);
            if (updated is null)
                throw new InvalidOperationException("No se pudo actualizar la inscripci\u00F3n.");
            return updated;
        }
        catch (SqlException ex)
        {
            ThrowMappedException(ex);
            throw;
        }
    }

    public async Task<InscripcionTorneoDto> InhabilitarAsync(Guid id, string? motivo, Guid usuarioId, CancellationToken ct = default)
    {
        var actual = await GetByIdAsync(id, ct);
        if (actual is null)
            throw new NotFoundException("La inscripci\u00F3n no existe.", id.ToString());
        if (actual.Estado == 2)
            return actual;

        try
        {
            var disabled = await _repository.InhabilitarAsync(id, motivo, usuarioId, ct);
            if (disabled is null)
                throw new InvalidOperationException("No se pudo inhabilitar la inscripci\u00F3n.");
            return disabled;
        }
        catch (SqlException ex)
        {
            ThrowMappedException(ex);
            throw;
        }
    }

    public async Task<InscripcionTorneoDto> HabilitarAsync(Guid id, Guid usuarioId, CancellationToken ct = default)
    {
        var actual = await GetByIdAsync(id, ct);
        if (actual is null)
            throw new NotFoundException("La inscripci\u00F3n no existe.", id.ToString());
        if (actual.Estado == 1)
            return actual;

        try
        {
            var enabled = await _repository.HabilitarAsync(id, usuarioId, ct);
            if (enabled is null)
                throw new InvalidOperationException("No se pudo habilitar la inscripci\u00F3n.");
            return enabled;
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
        if (msg.Contains("ya existe", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("ya est\u00E1", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("duplic", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException(msg);
        if (msg.Contains("no existe", StringComparison.OrdinalIgnoreCase))
            throw new NotFoundException(msg);
        if (msg.Contains("inactivo", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("inhabilitado", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("no se permite", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException(msg);
        if (ex.Number >= 50000 && ex.Number <= 99999)
            throw new ArgumentException(msg);
    }
}
