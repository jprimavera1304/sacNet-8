using ISL_Service.Application.DTOs.VentasEscrituras;
using ISL_Service.Application.DTOs.VentasUsados;

namespace ISL_Service.Application.Interfaces;

/*
  LO QUE PINTA LA PANTALLA DE "SIMULAR CON REMISION"

  Es la forma `VentasSimular` de Mac31 (Legacy/Mac31/Mac31/Forms/Ventas/
  VentasSimular.cs), que se abre sobre una remision YA emitida para ver como
  quedaria su cuenta con otro descuento, sin tocarla.

  Su `_Load` hace cinco lecturas en este orden (VentasSimular.cs:52-76):

    ConsultarVenta()             sp_n_ConsultaVentas      cabecera y totales
    ConsultarVentaDetalle()      sp_n_VentasDetalle       partidas y Simula*
    ConsultarVentaUsadosCargo()  sp_n_VentasUsadosCargo   cascos que se cobran
    ConsultarVentaUsadosCredito() sp_n_VentasUsadosCredito cascos entregados
    SimularDescuento()           sp_n_SimularDescuento    el esquema de prueba

  Aqui van las cuatro primeras. La quinta NO, y es a proposito: ese
  procedimiento ESCRIBE -da de alta un renglon en `Descuentos` a nombre del
  usuario y del equipo la primera vez- y ya tiene su propio endpoint
  (`POST api/ventas/descuentos/simular`), que la pantalla llama cuando la
  persona lo pide. Meterlo en esta lectura haria que abrir la pantalla diera de
  alta un descuento, que es justo lo que el web evita desde la ola 2.

  POR QUE NO SE REUSA `POST api/ventas/usados/consultar`, QUE YA TRAE ESTO

  Porque ese endpoint es la pantalla de USADOS y aplica SUS reglas: se niega si
  la remision esta cancelada y se niega con "EL FOLIO NO TIENE USADOS." cuando
  el bloque de cargos viene vacio (VentasUsadosService ~94, copiado de
  VentasUsados.cs:233-246). `VentasSimular` NO tiene ninguna de las dos: se
  abre sobre cualquier remision, con cascos o sin ellos. Reusarlo habria dejado
  el web negando simulaciones que Mac31 hace sin chistar, y ademas pidiendo el
  permiso de usados para simular.

  Se reusa el REPOSITORIO, que es donde estan los sp_n_, no el servicio.
*/
public interface ISimulacionDatosService
{
    Task<SimulacionDatos> ConsultarAsync(int idVenta, int idDescuento, CancellationToken ct);
}

public sealed class SimulacionDatos
{
    public int IdVenta { get; set; }

    /* ---- Cabecera, de sp_n_ConsultaVentas (VentasSimular.cs:135-138) ---- */
    public string Folio { get; set; } = string.Empty;
    public string Empresa { get; set; } = string.Empty;
    public string Agente { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;

    /*
      LA COLUMNA "ACTUAL" del recuadro de totales. Son los numeros de la venta
      tal como estan hoy, sin simular nada (VentasSimular.cs:140-162).

      OJO con dos que Mac31 ensena distinto de como vienen, y se respeta:
        - `Creditos` de la columna ACTUAL arranca en 0.00 y solo se llena si se
          marca "cascos a cambio" (lblCreditos.Text = "0.00", linea 151).
          Por eso aqui viaja el valor REAL y es la pantalla la que decide,
          igual que alla.
        - `TotalPagar` de la columna ACTUAL es el IMPORTE, no el total pagar
          guardado (`lblTotalPagar.Text = lbImporte.Text`, linea 156).
    */
    public VentasUsadosTotalesVentaDto Totales { get; set; } = new();

    /// Creditos + IvaCreditos de la venta: lo que se abona si se marcan los
    /// cascos a cambio (VentasSimular.cs:154).
    public decimal CreditosConIva { get; set; }

    /*
      Las partidas con sus columnas `Simula*` cuando se pidio con descuento.
      Es `sp_n_VentasDetalle(@IDVenta, @IDDescuento)` crudo: la pantalla elige
      que columna pintar segun si hay descuento o no, igual que `GridDetalle`
      (VentasSimular.cs:230-240) cambia entre `montoDescuento`/`importePartida`
      y `simulaDescuentoPartida`/`simulaImportePartida`.
    */
    public FilasLegacyDto Detalle { get; set; } = new();

    /// Los cascos que se le COBRAN, con sus totales. El total de aqui es el que
    /// se abona al marcar "cascos a cambio" (VentasSimular.cs:567).
    public VentasUsadosBloqueDto Cargos { get; set; } = new();

    /// Los cascos ENTREGADOS, con sus totales.
    public VentasUsadosBloqueDto Creditos { get; set; } = new();
}
