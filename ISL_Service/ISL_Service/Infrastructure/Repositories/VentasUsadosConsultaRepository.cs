using ISL_Service.Application.DTOs.ConsultasLegacy;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// Los cinco detalles de usados de MacServicios2, con sus mismos `sp_n_` y sus
/// mismos parametros.
/// </summary>
/// <remarks>
/// LA FALTA DE ORTOGRAFIA DE LEGACY SE CONSERVA EN EL NOMBRE DEL PROCEDIMIENTO
/// Y SOLO AHI. `sp_n_ConsultarVentasUsadosExcendentesDetalle` dice
/// "Excendentes" con una n de mas, y asi se llama en la base: cambiarlo seria
/// editar un `sp_n_`, que no se toca. En el codigo nuevo el nombre va escrito
/// bien (`Excedente`), porque el dia que ese procedimiento se reescriba como
/// `sp_w_` el error no tiene por que sobrevivirle.
/// </remarks>
public class VentasUsadosConsultaRepository : ConsultaLegacyRepositoryBase, IVentasUsadosConsultaRepository
{
    private const string SpMovimientoDetalle = "dbo.sp_n_ConsultarUsadosMovimientosDetalle";
    private const string SpCobroUsadoDetalle = "dbo.sp_n_ConsultarVentasCobrosUsadosDetalle";
    private const string SpCobroDineroUsadoDetalle = "dbo.sp_n_ConsultarCobroDineroUsadosDetalle";
    private const string SpExcedenteDetalle = "dbo.sp_n_ConsultarVentasUsadosExcendentesDetalle";
    private const string SpCreditoLog = "dbo.sp_n_VentasUsadosCreditoLog";

    public VentasUsadosConsultaRepository(IConfiguration configuration) : base(configuration)
    {
    }

    public Task<ConsultaFilasResponse> ConsultarMovimientoDetalleAsync(int idUsadoMovimiento, CancellationToken ct)
        => EjecutarFilasAsync(SpMovimientoDetalle, p => p.AddWithValue("@IDUsadoMovimiento", idUsadoMovimiento), ct);

    public Task<ConsultaFilasResponse> ConsultarCobroUsadoDetalleAsync(int idVentaCobroUsado, int idCobro, CancellationToken ct)
    {
        return EjecutarFilasAsync(SpCobroUsadoDetalle, p =>
        {
            p.AddWithValue("@IDVentaCobroUsado", idVentaCobroUsado);
            p.AddWithValue("@IDCobro", idCobro);
        }, ct);
    }

    public Task<ConsultaFilasResponse> ConsultarCobroDineroUsadoDetalleAsync(int idCobroDineroUsado, int idCobro, CancellationToken ct)
    {
        return EjecutarFilasAsync(SpCobroDineroUsadoDetalle, p =>
        {
            p.AddWithValue("@IDCobroDineroUsado", idCobroDineroUsado);
            p.AddWithValue("@IDCobro", idCobro);
        }, ct);
    }

    public Task<ConsultaFilasResponse> ConsultarExcedenteDetalleAsync(
        int idVentaUsadoExcedente, int idCobro, int idCliente, int idPagoVenta, CancellationToken ct)
    {
        return EjecutarFilasAsync(SpExcedenteDetalle, p =>
        {
            // El parametro del procedimiento SI lleva la n de mas.
            p.AddWithValue("@IDVentaUsadoExcendente", idVentaUsadoExcedente);
            p.AddWithValue("@IDCobro", idCobro);
            p.AddWithValue("@IDCliente", idCliente);
            p.AddWithValue("@IDPagoVenta", idPagoVenta);
        }, ct);
    }

    public Task<ConsultaFilasResponse> ConsultarCreditoLogAsync(int idVenta, CancellationToken ct)
        => EjecutarFilasAsync(SpCreditoLog, p => p.AddWithValue("@IDVenta", idVenta), ct);
}
