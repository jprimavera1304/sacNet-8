namespace ISL_Service.Application.Interfaces;

/// El PDF del reporte 1175 (SIMULAR) ya armado.
public sealed class SimulacionPdf
{
    public byte[] Contenido { get; init; } = Array.Empty<byte>();

    /// Con "simulacion" en el nombre a proposito: este papel NO es una
    /// remision y no debe poder confundirse con una en el escritorio de nadie.
    public string NombreArchivo { get; init; } = "simulacion.pdf";
}

/*
  EL REPORTE 1175, "SIMULAR"

  Es el papel que falta para que los dos botones SIMULAR de CONSULTA DE VENTAS
  hagan algo. Los dos acaban aqui:

    SIMULAR SIN REMISION  (btnSimularSin) captura un pedido con la bandera
                          `Simular = 1` y lo manda a pantalla con
                          `IDVenta = IDPedido * -1` (Ventas.cs:3970-3977).
    SIMULAR CON REMISION  (btnSimular) abre la forma `VentasSimular` sobre una
                          remision YA emitida y manda su IDVenta de verdad
                          (VentasSimular.cs:529).

  EL SIGNO ES LA FUNCION, NO UN TRUCO. `sp_n_VentasInformacion` arranca con
  `IF @IDVenta < 0 SELECT @IDPedido = @IDVenta * -1 -- Es una Simulacion`:
  el procedimiento de legacy ya sabe leer un pedido sin emitir cuando el id
  llega en negativo. Por eso aqui el id va firmado y no hay dos caminos.

  Y POR ESO ESTE PAPEL NO MARCA NADA. Con el id en negativo, los UPDATE de
  `sp_n_VentasInformacion` (que es el que sella quien imprimio) no encuentran
  fila: no hay venta con ese IDVenta. Con un IDVenta positivo -simular con
  remision- se le manda PrimerImpresion 0 y Reimpresion 0, que es lo que manda
  legacy, asi que tampoco rechaza ni resella nada. Simular no gasta folios.
*/
public interface ISimulacionImpresionService
{
    /// <param name="idVenta">
    /// Positivo para simular sobre una remision emitida; NEGATIVO
    /// (-IDPedido) para simular un pedido sin emitir.
    /// </param>
    /// <param name="idDescuento">
    /// El descuento que se esta simulando. 0 = sin descuento simulado; el
    /// papel sale con los importes de siempre.
    /// </param>
    /// <param name="simularConCascos">
    /// 0 = como venga, 1 = CON cascos a cambio, 2 = SIN cascos a cambio. Son
    /// los valores de `enumSimulaCascos` de Mac31 y solo los manda la pantalla
    /// de simular con remision; la captura manda 0 (Ventas.cs:3973).
    /// </param>
    Task<SimulacionPdf> GenerarAsync(
        int idVenta,
        int idUsuarioImpresion,
        int idDescuento,
        int simularConCascos,
        CancellationToken ct);
}
