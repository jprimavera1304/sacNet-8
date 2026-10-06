using ISL_Service.Application.DTOs.Cortes;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/// <summary>
/// Cortes de caja. Los cuatro SP de este lote no reciben texto ni fechas —toda
/// la conversion de `ConvertFtmDate` de legacy esta en los tres endpoints que
/// quedaron fuera por escribir— asi que no hay nada que normalizar y el service
/// pasa derecho. Existe para no saltarse la capa.
/// </summary>
public class CortesService : ICortesService
{
    private readonly ICortesRepository _repository;

    public CortesService(ICortesRepository repository)
    {
        _repository = repository;
    }

    public Task<List<CorteDetalleDto>> ConsultarDetalleAsync(int idCorte, CancellationToken ct)
        => _repository.ConsultarDetalleAsync(idCorte, ct);

    public Task<CorteSiguienteFechaDto?> ConsultarSiguienteFechaAsync(CancellationToken ct)
        => _repository.ConsultarSiguienteFechaAsync(ct);

    public Task<List<CorteCsCobroDto>> ConsultarCobrosCsAsync(int idCorte, int idCobroDineroUsado, int idTipoRetiro, CancellationToken ct)
        => _repository.ConsultarCobrosCsAsync(idCorte, idCobroDineroUsado, idTipoRetiro, ct);

    public Task<CorteVentasCanceladasCsDto> ConsultarVentasCanceladasCsAsync(int idCorte, int idTipoRetiro, CancellationToken ct)
        => _repository.ConsultarVentasCanceladasCsAsync(idCorte, idTipoRetiro, ct);
}
