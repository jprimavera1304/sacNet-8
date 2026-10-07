using ISL_Service.Application.DTOs.Nomina;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Los tres batches de nomina vistos desde el controlador. El id del usuario sale
/// del token, nunca del cuerpo: en legacy viajaba en el JSON y cualquiera con un
/// token podia disparar el descuento de la nomina a nombre de otra persona.
/// </summary>
public interface INominaBatchService
{
    Task<NominaBatchResponse> CalcularRecibosAsync(CalcularRecibosRequest r, CancellationToken ct);
    Task<NominaBatchResponse> GenerarPeriodosAsync(int idUsuario, CancellationToken ct);
    Task<NominaBatchResponse> DescontarPrestamosAsync(int idUsuario, CancellationToken ct);
}
