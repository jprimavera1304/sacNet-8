using ISL_Service.Application.DTOs.VentasUsados;

namespace ISL_Service.Application.Interfaces;

public interface IVentasUsadosService
{
    Task<VentasUsadosPantallaDto> ConsultarAsync(int idVenta, CancellationToken ct);

    Task<VentasUsadosGuardarResultadoDto> GuardarAsync(
        VentasUsadosGuardarRequest peticion,
        int idUsuario,
        string equipo,
        CancellationToken ct);
}
