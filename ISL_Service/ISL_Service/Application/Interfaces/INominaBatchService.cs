using ISL_Service.Application.DTOs.Nomina;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Los tres batches de nomina vistos desde el controlador. El id del usuario sale
/// del token, nunca del cuerpo: en legacy viajaba en el JSON y cualquiera con un
/// token podia disparar el descuento de la nomina a nombre de otra persona.
/// </summary>
public interface INominaBatchService
{
    /// <param name="puedeVerSueldo">
    /// Si el que llama tiene `empleados.sueldo.ver`. Sin esa llave el calculo se
    /// hace igual (eso lo gobierna `nomina.recibos.calcular`) pero los renglones
    /// vuelven con las columnas de importe en null. Ver NominaBatchService.
    /// </param>
    Task<NominaBatchResponse> CalcularRecibosAsync(
        CalcularRecibosRequest r, bool puedeVerSueldo, CancellationToken ct);
    Task<NominaBatchResponse> GenerarPeriodosAsync(int idUsuario, CancellationToken ct);
    Task<NominaBatchResponse> DescontarPrestamosAsync(int idUsuario, CancellationToken ct);
}
