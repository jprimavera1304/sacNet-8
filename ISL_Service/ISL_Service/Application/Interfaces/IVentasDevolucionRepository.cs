using ISL_Service.Application.DTOs.VentasDevolucion;

namespace ISL_Service.Application.Interfaces;

public interface IVentasDevolucionRepository
{
    /// Constantes.Funcionalidad ('TAU' / 'ZARA'). Es lo que bifurca los SP.
    Task<string> ConsultarFuncionalidadAsync(CancellationToken ct);

    /// Encabezado y totales: sp_n_ConsultaVentas por @IDVenta.
    Task<(VentasDevolucionVistaResponse Cabecera, bool Existe)> ConsultarCabeceraAsync(int idVenta, CancellationToken ct);

    /// Las partidas devolubles: sp_n_VentasDetalle.
    Task<List<VentasDevolucionPartida>> ConsultarPartidasAsync(int idVenta, CancellationToken ct);

    /// CASCOS REMISION: sp_n_VentasUsadosCargo.
    Task<VentasDevolucionCascosResumen> ConsultarCascosRemisionAsync(int idVenta, CancellationToken ct);

    /// CASCOS ENTREGADOS: sp_n_VentasUsadosCredito con @Accion = 2.
    Task<VentasDevolucionCascosResumen> ConsultarCascosEntregadosAsync(int idVenta, CancellationToken ct);

    /// La devolucion: sp_n_DevolucionVenta. Es quien decide de verdad.
    Task<VentasDevolucionResponse> DevolverAsync(
        int idVenta,
        int idUsuarioDevolucion,
        string idsVentasDetalle,
        string cantidades,
        string equipo,
        CancellationToken ct);
}
