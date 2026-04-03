using ISL_Service.Application.DTOs.GeneracionRolTorneo;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Application.Services;

public class GeneracionRolTorneoService : IGeneracionRolTorneoService
{
    private readonly IGeneracionRolTorneoRepository _repository;

    public GeneracionRolTorneoService(IGeneracionRolTorneoRepository repository)
    {
        _repository = repository;
    }

    public Task<List<GeneracionRolTorneoDto>> ConsultarAsync(string? texto, Guid? torneoId, Guid? jornadaId, DateTime? fechaJuego, byte? diaJuego, byte? estado, CancellationToken ct = default)
        => _repository.ConsultarAsync(NormalizeText(texto), torneoId, jornadaId, fechaJuego, diaJuego, estado, ct);

    public Task<GeneracionRolTorneoDto?> ObtenerAsync(Guid id, CancellationToken ct = default)
        => _repository.ObtenerAsync(id, ct);

    public async Task<GeneracionRolTorneoDto> CrearAsync(CreateGeneracionRolTorneoRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        try
        {
            var created = await _repository.InsertarAsync(request, usuarioId, ct);
            if (created is null)
                throw new InvalidOperationException("No se pudo crear la generación.");
            return created;
        }
        catch (SqlException ex)
        {
            ThrowMappedException(ex);
            throw;
        }
    }

    public async Task<GeneracionRolTorneoDto> ActualizarAsync(Guid id, UpdateGeneracionRolTorneoRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        var actual = await ObtenerAsync(id, ct);
        if (actual is null)
            throw new NotFoundException("La generación no existe.", id.ToString());
        if (actual.Estado != 1)
            throw new ConflictException("Solo se puede actualizar una generación en borrador.");

        try
        {
            var updated = await _repository.ActualizarAsync(id, request, usuarioId, ct);
            if (updated is null)
                throw new InvalidOperationException("No se pudo actualizar la generación.");
            return updated;
        }
        catch (SqlException ex)
        {
            ThrowMappedException(ex);
            throw;
        }
    }

    public async Task<GeneracionRolTorneoDto> CancelarAsync(Guid id, string motivo, Guid usuarioId, CancellationToken ct = default)
    {
        var actual = await ObtenerAsync(id, ct);
        if (actual is null)
            throw new NotFoundException("La generación no existe.", id.ToString());
        if (actual.Estado == 3)
            return actual;

        try
        {
            var canceled = await _repository.CancelarAsync(id, motivo, usuarioId, ct);
            if (canceled is null)
                throw new InvalidOperationException("No se pudo cancelar la generación.");
            return canceled;
        }
        catch (SqlException ex)
        {
            ThrowMappedException(ex);
            throw;
        }
    }

    public async Task<List<GeneracionRolCategoriaDto>> ConsultarCategoriasAsync(Guid generacionId, CancellationToken ct = default)
    {
        try
        {
            return await _repository.ConsultarCategoriasAsync(generacionId, ct);
        }
        catch (SqlException ex)
        {
            ThrowMappedException(ex);
            throw;
        }
    }

    public async Task<List<GeneracionRolCanchaDto>> GuardarCanchasAsync(Guid generacionId, Guid usuarioId, IReadOnlyList<GeneracionRolCanchaItemRequest> canchas, CancellationToken ct = default)
    {
        try
        {
            return await _repository.GuardarCanchasAsync(generacionId, usuarioId, canchas, ct);
        }
        catch (SqlException ex)
        {
            ThrowMappedException(ex);
            throw;
        }
    }

    public async Task<List<GeneracionRolCanchaDto>> ConsultarCanchasAsync(Guid generacionId, CancellationToken ct = default)
    {
        try
        {
            return await _repository.ConsultarCanchasAsync(generacionId, ct);
        }
        catch (SqlException ex)
        {
            ThrowMappedException(ex);
            throw;
        }
    }

    public async Task<List<GeneracionRolEquipoDto>> CargarEquiposAsync(Guid generacionId, Guid usuarioId, CancellationToken ct = default)
    {
        try
        {
            return await _repository.CargarEquiposAsync(generacionId, usuarioId, ct);
        }
        catch (SqlException ex)
        {
            ThrowMappedException(ex);
            throw;
        }
    }

    public async Task<List<GeneracionRolEquipoDto>> ConsultarEquiposAsync(Guid generacionId, CancellationToken ct = default)
    {
        try
        {
            return await _repository.ConsultarEquiposAsync(generacionId, ct);
        }
        catch (SqlException ex)
        {
            ThrowMappedException(ex);
            throw;
        }
    }

    public async Task<GeneracionRolEquipoDto> ActualizarParticipacionEquipoAsync(Guid id, UpdateParticipacionEquipoRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        try
        {
            var updated = await _repository.ActualizarParticipacionEquipoAsync(id, request, usuarioId, ct);
            if (updated is null)
                throw new InvalidOperationException("No se pudo actualizar la participación.");
            return updated;
        }
        catch (SqlException ex)
        {
            ThrowMappedException(ex);
            throw;
        }
    }

    public async Task<GenerarPartidosGeneracionRolTorneoResponse> GenerarPartidosAsync(Guid generacionId, Guid usuarioId, bool confirmarEstado = true, bool soloConfirmarEstado = false, CancellationToken ct = default)
    {
        try
        {
            return await _repository.GenerarPartidosAsync(generacionId, usuarioId, confirmarEstado, soloConfirmarEstado, ct);
        }
        catch (SqlException ex)
        {
            ThrowMappedException(ex);
            throw;
        }
    }

    public async Task<List<PartidoGeneracionRolTorneoDto>> ConsultarPartidosAsync(Guid generacionId, CancellationToken ct = default)
    {
        try
        {
            return await _repository.ConsultarPartidosAsync(generacionId, ct);
        }
        catch (SqlException ex)
        {
            ThrowMappedException(ex);
            throw;
        }
    }

    public async Task<List<PartidoGeneracionRolTorneoDto>> ActualizarOrdenPartidosAsync(Guid generacionId, Guid usuarioId, IReadOnlyList<OrdenPartidoItemRequest> partidos, CancellationToken ct = default)
    {
        try
        {
            return await _repository.ActualizarOrdenPartidosAsync(generacionId, usuarioId, partidos, ct);
        }
        catch (SqlException ex)
        {
            ThrowMappedException(ex);
            throw;
        }
    }

    public async Task<PartidoGeneracionRolTorneoDto> ActualizarObservacionPartidoAsync(Guid partidoId, string? observaciones, Guid usuarioId, CancellationToken ct = default)
    {
        try
        {
            var updated = await _repository.ActualizarObservacionPartidoAsync(partidoId, observaciones, usuarioId, ct);
            if (updated is null)
                throw new InvalidOperationException("No se pudo actualizar la observacion.");
            return updated;
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
        if (ex.Number is 2601 or 2627 or 547)
            throw new ConflictException(msg);
        if (msg.Contains("ya existe", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("duplic", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException(msg);
        if (msg.Contains("no existe", StringComparison.OrdinalIgnoreCase))
            throw new NotFoundException(msg);
        if (msg.Contains("borrador", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("cancelad", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("inactiva", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("mismo profesor titular", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("emparejamiento", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException(msg);
        if (ex.Number >= 50000 && ex.Number <= 99999)
            throw new ArgumentException(msg);
    }
}
