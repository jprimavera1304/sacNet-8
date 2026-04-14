using ISL_Service.Application.DTOs.Equipos;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Application.Services;

public class EquiposService : IEquiposService
{
    private readonly IEquiposRepository _repository;

    public EquiposService(IEquiposRepository repository)
    {
        _repository = repository;
    }

    public Task<List<EquipoDto>> ConsultarAsync(byte? estado, Guid? categoriaId, byte? diaJuego, string? texto, CancellationToken ct = default)
        => _repository.ConsultarAsync(estado, categoriaId, diaJuego, NormalizeText(texto), ct);

    public async Task<EquipoDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var list = await _repository.ConsultarAsync(null, null, null, null, ct);
        return list.FirstOrDefault(x => x.Id == id);
    }

    public async Task<EquipoDto> CrearAsync(CreateEquipoRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        try
        {
            var created = await _repository.InsertarAsync(request, usuarioId, ct);
            if (created is null)
                throw new InvalidOperationException("No se pudo crear el equipo.");
            return created;
        }
        catch (SqlException ex)
        {
            ThrowMappedException(ex);
            throw;
        }
    }

    public async Task<EquipoDto> ActualizarAsync(Guid id, UpdateEquipoRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        var actual = await GetByIdAsync(id, ct);
        if (actual is null)
            throw new NotFoundException("El equipo no existe.", id.ToString());
        if (actual.Estado == 2)
            throw new ConflictException("No se puede modificar un equipo inactivo.");

        try
        {
            var updated = await _repository.ActualizarAsync(id, request, usuarioId, ct);
            if (updated is null)
                throw new InvalidOperationException("No se pudo actualizar el equipo.");
            return updated;
        }
        catch (SqlException ex)
        {
            ThrowMappedException(ex);
            throw;
        }
    }

    public async Task<EquipoDto> InhabilitarAsync(Guid id, string? motivo, Guid usuarioId, CancellationToken ct = default)
    {
        var actual = await GetByIdAsync(id, ct);
        if (actual is null)
            throw new NotFoundException("El equipo no existe.", id.ToString());
        if (actual.Estado == 2)
            return actual;

        try
        {
            var disabled = await _repository.InhabilitarAsync(id, motivo, usuarioId, ct);
            if (disabled is null)
                throw new InvalidOperationException("No se pudo inhabilitar el equipo.");
            return disabled;
        }
        catch (SqlException ex)
        {
            ThrowMappedException(ex);
            throw;
        }
    }

    public async Task<EquipoDto> HabilitarAsync(Guid id, Guid usuarioId, CancellationToken ct = default)
    {
        var actual = await GetByIdAsync(id, ct);
        if (actual is null)
            throw new NotFoundException("El equipo no existe.", id.ToString());
        if (actual.Estado == 1)
            return actual;

        try
        {
            var enabled = await _repository.HabilitarAsync(id, usuarioId, ct);
            if (enabled is null)
                throw new InvalidOperationException("No se pudo habilitar el equipo.");
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
        if (ex.Number is 2601 or 2627)
            throw new ConflictException(msg);

        if (msg.Contains("ya existe", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("duplic", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("ux_wequipo_nombre", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("ux_wequipo_nombre_categoria", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("ux_wequipo_nombre_categoria_diajuego", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException(msg);
        if (msg.Contains("no existe", StringComparison.OrdinalIgnoreCase))
            throw new NotFoundException(msg);
        if (msg.Contains("inactivo", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("inhabilitado", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException(msg);
        if (ex.Number >= 50000 && ex.Number <= 99999)
            throw new ArgumentException(msg);
    }
}
