using ISL_Service.Application.DTOs.VentasConsulta;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Tests;

/// <summary>
/// Base para los dobles de <see cref="IVentasConsultaService"/>.
/// </summary>
/// <remarks>
/// Existe porque la interfaz tiene ocho metodos y las pruebas que la necesitan
/// usan UNO. Cada doble sobreescribe lo que de verdad le importa.
///
/// LOS DEMAS LANZAN, NO DEVUELVEN VACIO. Un doble que conteste "no hay nada" a
/// algo que la prueba no previo convierte un camino inesperado en un resultado
/// plausible, y entonces la prueba mide otra cosa sin avisar. Si una prueba
/// llama a un metodo que no preparo, es mejor que reviente y se vea.
/// </remarks>
public abstract class ConsultaDeVentasDobleBase : IVentasConsultaService
{
    private static Exception NoPreparado(string metodo) => new NotImplementedException(
        $"Este doble no prepara {metodo}. Sobreescribelo en la prueba que lo necesite.");

    public virtual Task<VentasConsultaCatalogosResponse> ConsultarCatalogosAsync(CancellationToken ct)
        => throw NoPreparado(nameof(ConsultarCatalogosAsync));

    public virtual Task<VentasConsultaRowsResponse> ConsultarRemisionesAsync(
        VentasConsultaRequest request, int idUsuarioToken, bool puedeVerDeOtros, CancellationToken ct)
        => throw NoPreparado(nameof(ConsultarRemisionesAsync));

    public virtual Task<VentasConsultaRowsResponse> ConsultarPedidosAsync(
        VentasConsultaRequest request, int idUsuarioToken, bool puedeVerDeOtros, CancellationToken ct)
        => throw NoPreparado(nameof(ConsultarPedidosAsync));

    public virtual Task<VentasConsultaRowsResponse> ConsultarPendientesImprimirAsync(
        VentasConsultaRequest request, int idUsuarioToken, CancellationToken ct)
        => throw NoPreparado(nameof(ConsultarPendientesImprimirAsync));

    public virtual Task<VentasConsultaRowsResponse> ConsultarPagosAsync(
        VentasConsultaRequest request, int idUsuarioToken, bool puedeVerDeOtros, CancellationToken ct)
        => throw NoPreparado(nameof(ConsultarPagosAsync));

    public virtual Task<List<VentasPedidoFaltanteItem>> ConsultarPedidosFaltantesAsync(CancellationToken ct)
        => throw NoPreparado(nameof(ConsultarPedidosFaltantesAsync));

    public virtual Task<VentasReportePreparacionResponse> PrepararReporteAsync(
        VentasReporteRequest request, CancellationToken ct)
        => throw NoPreparado(nameof(PrepararReporteAsync));

    public virtual Task<string> CrearTicketReporteAsync(
        VentasReporteRequest request, int idUsuarioToken, string equipo, CancellationToken ct)
        => throw NoPreparado(nameof(CrearTicketReporteAsync));
}
