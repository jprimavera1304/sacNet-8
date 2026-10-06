using ISL_Service.Application.DTOs.ClientesAnexos;

namespace ISL_Service.Application.Interfaces;

/// <summary>Lectura de las pestanas de la ficha del cliente.</summary>
public interface IClientesAnexosRepository
{
    Task<List<ClienteBonificacionDto>> ConsultarBonificacionesAsync(int idCliente, CancellationToken ct);
    Task<List<ClienteCentroServicioDto>> ConsultarCentrosServicioAsync(int idCliente, int idEmpresa, CancellationToken ct);
    Task<List<ClienteLogDto>> ConsultarLogAsync(int idCliente, CancellationToken ct);
    Task<List<ClienteFacturacionDto>> ConsultarFacturacionAsync(ClienteFacturacionFiltro filtro, CancellationToken ct);
}

/// <summary>Filtros de los datos de facturacion de un cliente.</summary>
public class ClienteFacturacionFiltro
{
    public int IDClienteFacturacion { get; set; }
    public int IDCliente { get; set; }
    public int IDStatus { get; set; }
    public string? NombreFacturacion { get; set; }
    public string? Rfc { get; set; }
    public bool Exacto { get; set; }
    /// <summary>
    /// Empresa de centro de servicio. Decide a que base apunta el SP: la de
    /// Zaragoza, la de Luro, o la local si no coincide con ninguna.
    /// </summary>
    public int IDEmpresaCS { get; set; }
}
