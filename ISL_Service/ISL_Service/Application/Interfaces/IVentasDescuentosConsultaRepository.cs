using ISL_Service.Application.DTOs.ConsultasLegacy;
using ISL_Service.Application.DTOs.VentasDescuentos;

namespace ISL_Service.Application.Interfaces;

/// <summary>Lecturas del catalogo de descuentos. Nada de aqui escribe.</summary>
public interface IVentasDescuentosConsultaRepository
{
    Task<ConsultaFilasResponse> ConsultarDetalleAsync(DescuentoDetalleRequest request, CancellationToken ct);

    Task<ConsultaFilasResponse> ConsultarDetalleLogAsync(DescuentoDetalleLogRequest request, CancellationToken ct);
}
