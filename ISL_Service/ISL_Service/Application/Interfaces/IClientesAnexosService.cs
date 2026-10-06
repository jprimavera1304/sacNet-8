using ISL_Service.Application.DTOs.ClientesAnexos;

namespace ISL_Service.Application.Interfaces;

/// <summary>Normaliza los filtros de las pestanas de la ficha del cliente.</summary>
public interface IClientesAnexosService
{
    Task<List<ClienteBonificacionDto>> ConsultarBonificacionesAsync(int idCliente, CancellationToken ct);
    Task<List<ClienteCentroServicioDto>> ConsultarCentrosServicioAsync(int idCliente, int idEmpresa, CancellationToken ct);
    Task<List<ClienteLogDto>> ConsultarLogAsync(int idCliente, CancellationToken ct);
    Task<List<ClienteFacturacionDto>> ConsultarFacturacionAsync(ClienteFacturacionFiltro filtro, CancellationToken ct);
}
