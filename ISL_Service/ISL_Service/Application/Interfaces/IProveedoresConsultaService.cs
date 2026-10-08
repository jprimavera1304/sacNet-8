using ISL_Service.Application.DTOs.ProveedoresConsulta;

namespace ISL_Service.Application.Interfaces;

/// <summary>Normaliza los filtros del padron de proveedores antes de bajar al SP.</summary>
public interface IProveedoresConsultaService
{
    Task<List<ProveedorConsultaDto>> ConsultarAsync(ProveedoresConsultaFiltro filtro, CancellationToken ct);
}
