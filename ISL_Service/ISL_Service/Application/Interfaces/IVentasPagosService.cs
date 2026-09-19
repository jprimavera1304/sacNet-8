using ISL_Service.Application.DTOs.VentasPagos;

namespace ISL_Service.Application.Interfaces;

public interface IVentasPagosService
{
    Task<VentasPagosRespuesta> ConsultarAsync(int idVenta, CancellationToken ct);

    Task<VentasPagoCancelarRespuesta> CancelarPagoAsync(
        VentasPagoCancelarRequest peticion,
        int idUsuario,
        string equipo,
        CancellationToken ct);
}
