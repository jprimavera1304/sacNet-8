using ISL_Service.Application.DTOs.ConsultasLegacy;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/// <summary>
/// Las lecturas de detalle de usados. No tienen regla propia: los cinco
/// procedimientos reciben ids y devuelven renglones, y legacy tampoco les hace
/// nada en C# (sus services de `/detalle` no leen Constantes ni convierten
/// fechas; ver UsadoMovimientoService.cs:27-51 y compania en MacServicios2).
/// </summary>
public class VentasUsadosConsultaService : IVentasUsadosConsultaService
{
    private readonly IVentasUsadosConsultaRepository _repository;

    public VentasUsadosConsultaService(IVentasUsadosConsultaRepository repository)
    {
        _repository = repository;
    }

    public Task<ConsultaFilasResponse> ConsultarMovimientoDetalleAsync(int idUsadoMovimiento, CancellationToken ct)
        => _repository.ConsultarMovimientoDetalleAsync(idUsadoMovimiento, ct);

    public Task<ConsultaFilasResponse> ConsultarCobroUsadoDetalleAsync(int idVentaCobroUsado, int idCobro, CancellationToken ct)
        => _repository.ConsultarCobroUsadoDetalleAsync(idVentaCobroUsado, idCobro, ct);

    public Task<ConsultaFilasResponse> ConsultarCobroDineroUsadoDetalleAsync(int idCobroDineroUsado, int idCobro, CancellationToken ct)
        => _repository.ConsultarCobroDineroUsadoDetalleAsync(idCobroDineroUsado, idCobro, ct);

    public Task<ConsultaFilasResponse> ConsultarExcedenteDetalleAsync(
        int idVentaUsadoExcedente, int idCobro, int idCliente, int idPagoVenta, CancellationToken ct)
        => _repository.ConsultarExcedenteDetalleAsync(idVentaUsadoExcedente, idCobro, idCliente, idPagoVenta, ct);

    public Task<ConsultaFilasResponse> ConsultarCreditoLogAsync(int idVenta, CancellationToken ct)
        => _repository.ConsultarCreditoLogAsync(idVenta, ct);
}
