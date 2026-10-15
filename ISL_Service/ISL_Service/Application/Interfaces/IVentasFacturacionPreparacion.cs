using ISL_Service.Application.DTOs.VentasFacturacion;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Arma y valida un comprobante ANTES de timbrarlo.
/// </summary>
/// <remarks>
/// POR QUE ES UN SERVICIO APARTE Y NO UN METODO MAS DE `IVentasFacturacionService`
/// -------------------------------------------------------------------------------
/// Porque no comparten nada. `IVentasFacturacionService` TIMBRA: habla con
/// Facturapi, escribe en la base y lo que hace no se deshace. Esto solo LEE y
/// cuenta: ni una escritura, ni una llamada al PAC. Mezclarlos significaria que
/// el servicio del que depende una pantalla de revision es el mismo que puede
/// emitir un CFDI, y en una familia donde el error vale un comprobante fiscal esa
/// separacion vale mas que una clase menos.
///
/// Y hay una razon practica: asi se puede probar entero sin base, con dobles de
/// los cinco servicios de lectura, y sin riesgo de que una prueba timbre algo.
/// </remarks>
public interface IVentasFacturacionPreparacionService
{
    /// <summary>
    /// Prepara una factura de una o varias remisiones (o de un pedido, cuando se
    /// factura sin remision).
    /// </summary>
    /// <param name="idUsuario">El del token. Decide el candado de almacen y de agente de la consulta.</param>
    Task<FacturaPreparadaDto> PrepararFacturaAsync(
        FacturaPrepararRequest request, int idUsuario, bool puedeVerDeOtros, CancellationToken ct);

    /// <summary>Prepara el ENCABEZADO fiscal de una nota de credito ya capturada.</summary>
    Task<NotaCreditoPreparadaDto> PrepararNotaCreditoAsync(
        NotaCreditoPrepararRequest request, CancellationToken ct);

    /// <summary>Prepara un complemento de pago sobre una o varias facturas.</summary>
    Task<ComplementoPreparadoDto> PrepararComplementoAsync(
        ComplementoPrepararRequest request, CancellationToken ct);
}
