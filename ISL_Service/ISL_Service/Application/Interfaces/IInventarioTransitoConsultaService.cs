using ISL_Service.Application.DTOs.InventarioTransito;

namespace ISL_Service.Application.Interfaces;

/// <summary>Normaliza los filtros del inventario en transito antes de bajar al SP.</summary>
public interface IInventarioTransitoConsultaService
{
    Task<List<InventarioTransitoDto>> ConsultarAsync(InventarioTransitoFiltro filtro, CancellationToken ct);
}
