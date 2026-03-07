using ISL_Service.Application.DTOs.Cheques;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Application.Services;

public class ChequesService : IChequesService
{
    private readonly IChequesRepository _repository;

    public ChequesService(IChequesRepository repository)
    {
        _repository = repository;
    }

    public Task<List<ChequeDto>> ConsultarAsync(
        string? texto,
        int? idCliente,
        int? idBanco,
        byte? estatusCheque,
        byte? idStatus,
        DateTime? fechaChequeInicio,
        DateTime? fechaChequeFin,
        CancellationToken ct = default)
        => _repository.ConsultarAsync(
            NormalizeText(texto),
            idCliente,
            idBanco,
            estatusCheque,
            idStatus,
            fechaChequeInicio?.Date,
            fechaChequeFin?.Date,
            ct);

    public Task<ChequeDetalleDto?> GetByIdAsync(Guid chequeId, CancellationToken ct = default)
        => _repository.ConsultarDetalleAsync(chequeId, ct);

    public async Task<ChequeDetalleDto> CrearAsync(CreateChequeRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        try
        {
            var created = await _repository.InsertarAsync(request, usuarioId, ct);
            if (created is null)
                throw new InvalidOperationException("No se pudo crear el cheque.");
            return created;
        }
        catch (SqlException ex)
        {
            ThrowMappedException(ex);
            throw;
        }
    }

    public async Task<ChequeDetalleDto> ActualizarAsync(Guid chequeId, UpdateChequeRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        var actual = await GetByIdAsync(chequeId, ct);
        if (actual is null)
            throw new NotFoundException("El cheque no existe.", chequeId.ToString());

        try
        {
            var updated = await _repository.ActualizarAsync(chequeId, request, usuarioId, ct);
            if (updated is null)
                throw new InvalidOperationException("No se pudo actualizar el cheque.");
            return updated;
        }
        catch (SqlException ex)
        {
            ThrowMappedException(ex);
            throw;
        }
    }

    public async Task<ChequeDetalleDto> CambiarEstatusAsync(
        Guid chequeId,
        byte estatusChequeNuevo,
        string? motivo,
        string? observaciones,
        DateTime? fechaMovimiento,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        var actual = await GetByIdAsync(chequeId, ct);
        if (actual is null)
            throw new NotFoundException("El cheque no existe.", chequeId.ToString());

        try
        {
            var changed = await _repository.CambiarEstatusAsync(
                chequeId,
                estatusChequeNuevo,
                NormalizeText(motivo),
                NormalizeText(observaciones),
                fechaMovimiento,
                usuarioId,
                ct);

            if (changed is null)
                throw new InvalidOperationException("No se pudo cambiar el estatus del cheque.");
            return changed;
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
            msg.Contains("duplic", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException(msg);
        if (msg.Contains("no existe", StringComparison.OrdinalIgnoreCase))
            throw new NotFoundException(msg);
        if (msg.Contains("solo se puede", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("solo se pueden", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException(msg);
        if (ex.Number >= 50000 && ex.Number <= 99999)
            throw new ArgumentException(msg);
    }
}
