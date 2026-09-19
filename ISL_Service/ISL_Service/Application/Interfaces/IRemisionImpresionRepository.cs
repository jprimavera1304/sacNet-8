using System.Data;
using ISL_Service.Infrastructure.Reports;

namespace ISL_Service.Application.Interfaces;

/*
  Cuando el procedimiento contesta que esa venta NO se puede imprimir (porque
  otro usuario ya la imprimio antes, por ejemplo) no devuelve datos, devuelve
  un motivo. Legacy junta esos motivos y sigue con las demas ventas.
*/
public sealed class RemisionDatosVentaResultado
{
    public RemisionDatosVenta? Datos { get; init; }
    public string Mensaje { get; init; } = "";
}

/// Lee de la base lo que hace falta para pintar una remision: las plantillas y
/// los seis conjuntos de datos de cada venta. Todo con los sp_n_ de legacy.
public interface IRemisionImpresionRepository
{
    Task<DataTable> ConsultarPlantillasAsync(CancellationToken ct);

    Task<RemisionDatosVentaResultado> ConsultarDatosVentaAsync(
        int idVenta,
        int idUsuarioImpresion,
        int idDescuento,
        CancellationToken ct);

    /*
      LO DE ZARAGOZA VA APARTE PORQUE ES OTRO REPORTE.

      No es la misma remision con otro logo: Zaragoza imprime la "Remision
      Zaragoza Generico", con sus propias plantillas y un solo procedimiento.
      Ver RemisionZaragozaHtmlBuilder para el detalle de por que.
    */

    /// Que empresa es esta base (ZARA, TAU, ...). Sale de Constantes.
    Task<string> ConsultarFuncionalidadAsync(CancellationToken ct);

    /// El logo del reporte y el de la marca de agua, ya con su ruta resuelta.
    Task<RemisionLogos> ConsultarLogosAsync(CancellationToken ct);

    /// Las cinco plantillas de la remision de Zaragoza (cuerpo_n, detalle,
    /// totales, pie, hoja4).
    Task<DataTable> ConsultarPlantillasZaragozaAsync(CancellationToken ct);

    /// Los datos de UNA venta para el papel de Zaragoza.
    Task<RemisionZaragozaResultado> ConsultarDatosVentaZaragozaAsync(
        int idVenta,
        int idUsuarioImpresion,
        CancellationToken ct);
}

/// Las dos imagenes del papel, ya listas para meterlas en el html.
public sealed class RemisionLogos
{
    public string Logo { get; init; } = "";
    public string MarcaDeAgua { get; init; } = "";
}

/// Igual que en Tauro: si la venta no se puede imprimir no vienen datos, viene
/// el motivo, y legacy sigue con las demas.
public sealed class RemisionZaragozaResultado
{
    public DataTable? Datos { get; init; }
    public string Mensaje { get; init; } = "";
}
