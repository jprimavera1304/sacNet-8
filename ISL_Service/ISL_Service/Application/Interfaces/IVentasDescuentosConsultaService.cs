using ISL_Service.Application.DTOs.ConsultasLegacy;
using ISL_Service.Application.DTOs.VentasDescuentos;

namespace ISL_Service.Application.Interfaces;

public interface IVentasDescuentosConsultaService
{
    Task<ConsultaFilasResponse> ConsultarDetalleAsync(DescuentoDetalleRequest request, CancellationToken ct);

    Task<ConsultaFilasResponse> ConsultarDetalleLogAsync(DescuentoDetalleLogRequest request, CancellationToken ct);
}
