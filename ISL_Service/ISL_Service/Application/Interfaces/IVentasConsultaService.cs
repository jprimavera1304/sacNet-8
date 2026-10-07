using ISL_Service.Application.DTOs.VentasConsulta;

namespace ISL_Service.Application.Interfaces;

public interface IVentasConsultaService
{
    Task<VentasConsultaCatalogosResponse> ConsultarCatalogosAsync(CancellationToken ct);
    /*
      `idUsuarioToken` es QUIEN PREGUNTA y sale del token; va aparte del request
      a proposito, para que no se pueda mandar en el cuerpo.

      `puedeVerDeOtros` dice si ese usuario tiene permiso de consultar los
      renglones de OTRO usuario (`ventas.consultar`). No es identidad: es el
      alcance del filtro @IDUsuario, que SI es un filtro legitimo de pantalla.
      El detalle completo esta en VentasConsultaService.
    */
    Task<VentasConsultaRowsResponse> ConsultarRemisionesAsync(VentasConsultaRequest request, int idUsuarioToken, bool puedeVerDeOtros, CancellationToken ct);
    Task<VentasConsultaRowsResponse> ConsultarPedidosAsync(VentasConsultaRequest request, int idUsuarioToken, bool puedeVerDeOtros, CancellationToken ct);
    Task<VentasConsultaRowsResponse> ConsultarPendientesImprimirAsync(VentasConsultaRequest request, int idUsuarioToken, CancellationToken ct);
    Task<VentasConsultaRowsResponse> ConsultarPagosAsync(VentasConsultaRequest request, int idUsuarioToken, bool puedeVerDeOtros, CancellationToken ct);

    /// Lo que se pidio hoy y no se pudo surtir (btnPedidosFaltantes de Mac31).
    Task<List<VentasPedidoFaltanteItem>> ConsultarPedidosFaltantesAsync(CancellationToken ct);

    /// Lo que Mac31 pregunta ANTES de sacar el papel: folios que no salen por
    /// aqui, contrasena rotatoria y confirmaciones. Ver VentasImpresionReglas.
    Task<VentasReportePreparacionResponse> PrepararReporteAsync(VentasReporteRequest request, CancellationToken ct);

    /// Firma el pase de pocos minutos con el que el navegador puede ir por el PDF.
    Task<string> CrearTicketReporteAsync(VentasReporteRequest request, int idUsuarioToken, string equipo, CancellationToken ct);
}
