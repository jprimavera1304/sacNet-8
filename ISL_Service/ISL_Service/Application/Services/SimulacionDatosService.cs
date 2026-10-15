using ISL_Service.Application.DTOs.VentasPagosOperaciones;
using ISL_Service.Application.DTOs.VentasUsados;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/// Las cuatro lecturas de `VentasSimular._Load`. El por que de cada decision
/// esta en <see cref="ISimulacionDatosService"/>.
public class SimulacionDatosService : ISimulacionDatosService
{
    private readonly IVentasUsadosRepository _usados;
    private readonly IVentasPagosOperacionesRepository _ventas;

    public SimulacionDatosService(
        IVentasUsadosRepository usados,
        IVentasPagosOperacionesRepository ventas)
    {
        _usados = usados;
        _ventas = ventas;
    }

    public async Task<SimulacionDatos> ConsultarAsync(int idVenta, int idDescuento, CancellationToken ct)
    {
        if (idVenta <= 0)
            throw new InvalidOperationException("Falta la remision que se quiere simular.");

        var venta = await _usados.ConsultarVentaAsync(idVenta, ct)
            ?? throw new InvalidOperationException($"La remision {idVenta} no existe.");

        /*
          NO SE VALIDA NADA MAS, Y ESO ES LA MEDIDA.

          `VentasSimular._Load` solo se cierra si una de sus consultas falla:
          no mira si la remision esta cancelada, ni si tiene cascos, ni si ya
          se pago. Simular es mirar una cuenta; no cambia nada, asi que no hay
          estado que proteger. Negar aqui lo que alla se abre seria inventarse
          una regla.
        */

        var detalle = await _ventas.ConsultarDetalleAsync(
            new VentasDetalleConsultaRequest { IdVenta = idVenta, IdDescuento = idDescuento }, ct);

        var cargos = await _usados.ConsultarCargosAsync(idVenta, ct);
        var creditos = await _usados.ConsultarCreditosAsync(idVenta, ct);

        return new SimulacionDatos
        {
            IdVenta = idVenta,
            Folio = venta.FolioFtm.Length > 0 ? venta.FolioFtm : venta.Folio,
            Empresa = venta.Empresa,
            Agente = venta.Agente,
            Cliente = venta.Cliente,
            Totales = new VentasUsadosTotalesVentaDto
            {
                Importe = venta.Importe,
                Cargos = venta.Cargos,
                Creditos = venta.Creditos + venta.IvaCreditos,
                /*
                  El total a pagar de la columna ACTUAL es el IMPORTE: alla
                  `lblTotalPagar.Text = lbImporte.Text` (VentasSimular.cs:156),
                  no el TotalPagar guardado. Se manda ya resuelto para que la
                  pantalla no tenga que saberlo.
                */
                TotalPagar = venta.Importe,
                Descuentos = venta.Descuentos,
                Abonos = venta.Abonos,
                Saldo = venta.Saldo
            },
            CreditosConIva = venta.Creditos + venta.IvaCreditos,
            Detalle = detalle,
            Cargos = cargos,
            Creditos = creditos
        };
    }
}
