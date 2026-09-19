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
}
