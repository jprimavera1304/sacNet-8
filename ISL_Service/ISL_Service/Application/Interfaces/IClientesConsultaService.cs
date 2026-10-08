using ISL_Service.Application.DTOs.ClientesConsulta;

namespace ISL_Service.Application.Interfaces;

/// <summary>Normaliza los filtros del padron de clientes antes de bajar al SP.</summary>
public interface IClientesConsultaService
{
    Task<List<ClienteConsultaDto>> ConsultarAsync(ClientesConsultaFiltro filtro, int idUsuario, CancellationToken ct);
}
