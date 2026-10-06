using ISL_Service.Application.DTOs.Cortes;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Las cuatro lecturas de corte de caja que no arrastran el cierre del dia.
/// </summary>
public interface ICortesRepository
{
    Task<List<CorteDetalleDto>> ConsultarDetalleAsync(int idCorte, CancellationToken ct);
    Task<CorteSiguienteFechaDto?> ConsultarSiguienteFechaAsync(CancellationToken ct);
    Task<List<CorteCsCobroDto>> ConsultarCobrosCsAsync(int idCorte, int idCobroDineroUsado, int idTipoRetiro, CancellationToken ct);
    Task<CorteVentasCanceladasCsDto> ConsultarVentasCanceladasCsAsync(int idCorte, int idTipoRetiro, CancellationToken ct);
}

/// <inheritdoc cref="ICortesRepository"/>
public interface ICortesService
{
    Task<List<CorteDetalleDto>> ConsultarDetalleAsync(int idCorte, CancellationToken ct);
    Task<CorteSiguienteFechaDto?> ConsultarSiguienteFechaAsync(CancellationToken ct);
    Task<List<CorteCsCobroDto>> ConsultarCobrosCsAsync(int idCorte, int idCobroDineroUsado, int idTipoRetiro, CancellationToken ct);
    Task<CorteVentasCanceladasCsDto> ConsultarVentasCanceladasCsAsync(int idCorte, int idTipoRetiro, CancellationToken ct);
}
