using ISL_Service.Application.DTOs.ConsultasLegacy;
using ISL_Service.Application.DTOs.VentasCobranza;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Lecturas de la cobranza de Mac31 (cobros, cobranzas y concentrados).
/// Todo de solo lectura: ningun metodo de aqui escribe.
/// </summary>
public interface IVentasCobranzaConsultaRepository
{
    Task<ConsultaFilasResponse> ValidarFolioCobranzaAsync(CobranzaFolioRequest request, int idUsuarioActual, CancellationToken ct);

    Task<ConsultaFilasResponse> ConsultarCobranzaDetalleAsync(int idCobranza, int formato, CancellationToken ct);

    Task<ConsultaFilasResponse> ValidarFolioConcentradoAsync(ConcentradoFolioRequest request, int idUsuarioActual, CancellationToken ct);

    Task<ConsultaFilasResponse> ConsultarConcentradosAsync(ConcentradosConsultaRequest request, int idUsuarioActual, CancellationToken ct);

    Task<ConsultaFilasResponse> ConsultarConcentradoDetalleAsync(int idConcentrado, CancellationToken ct);

    Task<ConsultaFilasResponse> ConsultarCobrosCentroServicioAsync(int idCobro, int folioCobro, CancellationToken ct);

    Task<ConsultaFilasResponse> ConsultarFoliosUsadosAsync(CobroFoliosUsadosRequest request, CancellationToken ct);

    Task<ConsultaFilasResponse> ConsultarCobroPagosAsync(CobroPagosRequest request, CancellationToken ct);

    Task<ConsultaFilasResponse> ConsultarFoliosSinAsignarAsync(IEnumerable<int> idsEmpresa, int devueltasA, CancellationToken ct);

    Task<ConsultaFilasResponse> ConsultarPagosKilosAsync(PagosKilosRequest request, CancellationToken ct);
}
