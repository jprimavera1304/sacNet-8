using ISL_Service.Application.DTOs.Jornadas;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Application.Services;

public class JornadasService : IJornadasService
{
    private readonly IJornadasRepository _repository;

    public JornadasService(IJornadasRepository repository)
    {
        _repository = repository;
    }

    public Task<List<JornadaDto>> ConsultarAsync(byte? estado, string? texto, CancellationToken ct = default)
        => _repository.ConsultarAsync(estado, NormalizeText(texto), ct);

    public async Task<JornadaDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var list = await _repository.ConsultarAsync(null, null, ct);
        return list.FirstOrDefault(x => x.Id == id);
    }

    public async Task<JornadaDto> CrearAsync(CreateJornadaRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        try
        {
            var created = await _repository.InsertarAsync(request, usuarioId, ct);
            if (created is null)
                throw new InvalidOperationException("No se pudo crear la jornada.");
            return created;
        }
        catch (SqlException ex)
        {
            ThrowMappedException(ex);
            throw;
        }
    }

    public async Task<JornadaDto> ActualizarAsync(Guid id, UpdateJornadaRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        var actual = await GetByIdAsync(id, ct);
        if (actual is null)
            throw new NotFoundException("La jornada no existe.", id.ToString());
        if (actual.Estado == 2)
            throw new ConflictException("No se puede modificar una jornada inactiva.");

        try
        {
            var updated = await _repository.ActualizarAsync(id, request, usuarioId, ct);
            if (updated is null)
                throw new InvalidOperationException("No se pudo actualizar la jornada.");
            return updated;
        }
        catch (SqlException ex)
        {
            ThrowMappedException(ex);
            throw;
        }
    }

    public async Task<JornadaDto> InhabilitarAsync(Guid id, string motivo, Guid usuarioId, CancellationToken ct = default)
    {
        var actual = await GetByIdAsync(id, ct);
        if (actual is null)
            throw new NotFoundException("La jornada no existe.", id.ToString());
        if (actual.Estado == 2)
            return actual;

        try
        {
            var disabled = await _repository.InhabilitarAsync(id, motivo, usuarioId, ct);
            if (disabled is null)
                throw new InvalidOperationException("No se pudo inhabilitar la jornada.");
            return disabled;
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
            msg.Contains("duplic", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("ux_wjornada", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException(msg);
        if (msg.Contains("no existe", StringComparison.OrdinalIgnoreCase))
            throw new NotFoundException(msg);
        if (msg.Contains("inactiva", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("inhabil", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException(msg);
        if (ex.Number >= 50000 && ex.Number <= 99999)
            throw new ArgumentException(msg);
    }
}
