using ISL_Service.Application.DTOs.Cheques;

namespace ISL_Service.Application.Interfaces;

public interface IChequesService
{
    Task<List<ChequeDto>> ConsultarAsync(
        string? texto,
        int? idCliente,
        int? idBanco,
        byte? estatusCheque,
        byte? idStatus,
        DateTime? fechaChequeInicio,
        DateTime? fechaChequeFin,
        CancellationToken ct = default);

    Task<ChequeDetalleDto?> GetByIdAsync(Guid chequeId, CancellationToken ct = default);

    Task<ChequeDetalleDto> CrearAsync(CreateChequeRequest request, Guid usuarioId, CancellationToken ct = default);

    Task<ChequeDetalleDto> ActualizarAsync(Guid chequeId, UpdateChequeRequest request, Guid usuarioId, CancellationToken ct = default);

    Task<ChequeDetalleDto> CambiarEstatusAsync(
        Guid chequeId,
        byte estatusChequeNuevo,
        string? motivo,
        string? observaciones,
        DateTime? fechaMovimiento,
        Guid usuarioId,
        CancellationToken ct = default);
}
