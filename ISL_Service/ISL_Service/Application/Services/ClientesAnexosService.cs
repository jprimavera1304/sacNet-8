using ISL_Service.Application.DTOs.ClientesAnexos;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/// <summary>
/// Normaliza los filtros de las pestanas del cliente.
///
/// El apostrofo se quita aqui por la misma razon de siempre:
/// sp_n_ConsultarClientesFacturacion concatena @NombreFacturacion y @RFC dentro
/// de su EXEC(@Sql) y legacy no los saneaba en la rama de consulta.
/// </summary>
public class ClientesAnexosService : IClientesAnexosService
{
    private readonly IClientesAnexosRepository _repository;

    public ClientesAnexosService(IClientesAnexosRepository repository)
    {
        _repository = repository;
    }

    private static string Texto(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return string.Empty;
        return valor.Replace("'", string.Empty).Trim();
    }

    public Task<List<ClienteBonificacionDto>> ConsultarBonificacionesAsync(int idCliente, CancellationToken ct)
        => _repository.ConsultarBonificacionesAsync(idCliente, ct);

    public Task<List<ClienteCentroServicioDto>> ConsultarCentrosServicioAsync(int idCliente, int idEmpresa, CancellationToken ct)
        => _repository.ConsultarCentrosServicioAsync(idCliente, idEmpresa, ct);

    public Task<List<ClienteLogDto>> ConsultarLogAsync(int idCliente, CancellationToken ct)
        => _repository.ConsultarLogAsync(idCliente, ct);

    public Task<List<ClienteFacturacionDto>> ConsultarFacturacionAsync(ClienteFacturacionFiltro filtro, CancellationToken ct)
        => _repository.ConsultarFacturacionAsync(new ClienteFacturacionFiltro
        {
            IDClienteFacturacion = filtro.IDClienteFacturacion,
            IDCliente = filtro.IDCliente,
            IDStatus = filtro.IDStatus,
            NombreFacturacion = Texto(filtro.NombreFacturacion),
            Rfc = Texto(filtro.Rfc),
            Exacto = filtro.Exacto,
            IDEmpresaCS = filtro.IDEmpresaCS
        }, ct);
}
