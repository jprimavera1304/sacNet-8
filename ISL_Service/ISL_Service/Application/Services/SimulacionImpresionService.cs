using System.Data;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Reports;

namespace ISL_Service.Application.Services;

/*
  EL ARMADO DEL REPORTE 1175

  Es el hermano de RemisionImpresionService y comparte con el casi todo, porque
  en legacy TAMBIEN es el mismo codigo: `findImprimirAsync` atiende la remision
  y la simulacion en el mismo bucle, con los mismos seis procedimientos y el
  mismo `GeneraRemisionHtml`. Lo unico que cambia es de donde salen las
  plantillas -IDTipoTemplateHtml 1175 en vez de 1001- y el ajuste de cascos de
  mas abajo.

  Por eso aqui se reusan el repositorio y el constructor de html en vez de
  copiarlos: si manana ajustan `GeneraRemisionHtml`, los dos papeles se enteran.

  ZARAGOZA NO SE DESVIA AQUI, Y ESO TAMBIEN ES MEDIDO. El desvio de Zaragoza a
  su propio reporte esta dentro de `if (IDReporte == enumReportes.Remision)`
  (Legacy/MacServicios2/.../VentasImprimirService.cs ~118): la simulacion no
  entra. Y las diez plantillas del 1175 estan en las DOS bases. O sea que las
  dos empresas simulan con el mismo papel.
*/
public class SimulacionImpresionService : ISimulacionImpresionService
{
    /// Igual que la remision: MacReportes la pide parada
    /// (ReportesController ~119, orientacion = "portrait").
    private const string Orientacion = "portrait";

    private const int Copias = 1;

    /// Los de `enumSimulaCascos` de Mac31 (Enums.cs:308).
    private const int ConCascos = 1;
    private const int SinCascos = 2;

    /*
      LO QUE LA PLANTILLA PIDE Y UN PEDIDO NO TIENE.

      Al simular un PEDIDO, `sp_n_VentasDetalle` se va por su rama de
      simulacion y devuelve 18 columnas en vez de las 120 de una venta. De las
      que usa la plantilla del 1175, la unica que se queda sin dato es
      `Observaciones`: en una venta es el texto que arma el procedimiento ("SI
      ENTREGA LOS CASCOS USADOS COMPLETOS, USTED SOLO PAGARA: $...") y para un
      pedido no lo arma, porque todavia no hay entrega.

      Medido con el pedido de prueba: ese es el unico marcador que sobrevive.
      Mac31 lo imprime literal, con almohadillas y todo; aqui sale vacio. Ver
      la nota de RemisionHtmlBuilder.Construir.

      La lista va aqui, nombre por nombre, y no como "vacia lo que sobre": si
      manana la plantilla usa una columna mas, el papel tiene que tronar y
      decirlo, no salir con un hueco que nadie nota.
    */
    private static readonly string[] MarcadoresQueNoTieneUnPedido = { "Observaciones" };

    private readonly IRemisionImpresionRepository _repository;

    public SimulacionImpresionService(IRemisionImpresionRepository repository)
    {
        _repository = repository;
    }

    public async Task<SimulacionPdf> GenerarAsync(
        int idVenta,
        int idUsuarioImpresion,
        int idDescuento,
        int simularConCascos,
        CancellationToken ct)
    {
        if (idVenta == 0)
            throw new InvalidOperationException("No se indico que simular.");

        var plantillas = await _repository.ConsultarPlantillasSimulacionAsync(ct);
        if (plantillas.Rows.Count == 0)
        {
            throw new InvalidOperationException(
                "Esta base no tiene las plantillas del reporte de simulacion (Template_Html, " +
                "IDTipoTemplateHtml 1175). Sin ellas no se puede armar el papel.");
        }

        /*
          SIN BANDERAS DE IMPRESION, COMO LEGACY.

          `ConsultarSimular` de MacReportes no les pone valor, asi que llegan en
          cero: simular no sella la remision ni rechaza una ya impresa. Si aqui
          fuera 1, simular sobre una remision ya impresa contestaria "YA FUE
          IMPRESO POR ..." en vez de ensenar el papel.
        */
        var resultado = await _repository.ConsultarDatosVentaAsync(
            idVenta, idUsuarioImpresion, idDescuento, 0, 0, "", ct);

        if (resultado.Datos is null)
        {
            throw new InvalidOperationException(resultado.Mensaje is { Length: > 0 }
                ? resultado.Mensaje
                : "No se pudo obtener la informacion para simular.");
        }

        AjustarCascosACambio(resultado.Datos, simularConCascos);

        /*
          `hayReimpresion: false` porque el 1175 no tiene juego `reimp_` y
          porque legacy arma la simulacion siempre con el juego normal. Ver
          RemisionHtmlBuilder.Construir.
        */
        var html = RemisionHtmlBuilder.Construir(
            new[] { resultado.Datos },
            plantillas,
            Copias,
            raiz: "simular",
            hayReimpresion: false,
            marcadoresQueNoVienen: idVenta < 0 ? MarcadoresQueNoTieneUnPedido : null);

        var pdf = await WkhtmltopdfHtmlPdfRenderer.RenderAsync(html, Orientacion, TituloDePestana(idVenta), ct);

        return new SimulacionPdf { Contenido = pdf };
    }

    /*
      EL AJUSTE DE "CASCOS A CAMBIO", PORTADO TAL CUAL

      Es el bloque `if (SimularConCascos > 0)` de
      Legacy/MacServicios2/.../VentasImprimirService.cs (~241-277), y se hace
      SOBRE LOS DATOS, antes de pintar: la pantalla de simular deja marcar o
      desmarcar "cascos a cambio" y el papel tiene que salir con esa cuenta, no
      con la que trae el procedimiento.

      CON cascos  -> los cargos por cascos se le abonan: viajan a Creditos (con
                     su IVA) y se restan del total a pagar.
      SIN cascos  -> no se le abona nada: Creditos en cero y el total a pagar es
                     el importe limpio.

      Con 0 no se toca nada, que es lo que manda la captura al simular un
      pedido: ahi no hay casilla que marcar.

      El formato de los campos `...Ftm` se repite con el mismo "###,###,##0.00"
      y el mismo espacio delante que pone legacy, porque esos son los que se
      pintan en el papel: cualquier otra forma se veria distinta a la de Mac31.
    */
    private static void AjustarCascosACambio(RemisionDatosVenta datos, int simularConCascos)
    {
        if (simularConCascos != ConCascos && simularConCascos != SinCascos)
            return;

        if (datos.Detalle.Rows.Count == 0 || datos.Informacion.Rows.Count == 0)
            return;

        var detalle = datos.Detalle.Rows[0];

        var porcentajeIva = Numero(datos.Informacion.Rows[0], "PorcentajeIVA") / 100m;
        var importe = Numero(detalle, "SimulaImporte");
        var montoCargos = Numero(detalle, "MontoCargos");
        var montoCargosIva = montoCargos * porcentajeIva;

        if (simularConCascos == ConCascos)
        {
            Poner(detalle, "Creditos", montoCargos);
            Poner(detalle, "CreditosFtm", " " + montoCargos.ToString("###,###,##0.00"));

            Poner(detalle, "IVACreditos", montoCargosIva);
            Poner(detalle, "IVACreditosFtm", " " + montoCargosIva.ToString("###,###,##0.00"));

            var total = importe - montoCargos - montoCargosIva;
            Poner(detalle, "SimulaTotalPagar", total);
            Poner(detalle, "SimulaTotalPagarFtm", total.ToString("###,###,##0.00"));
            return;
        }

        Poner(detalle, "Creditos", 0m);
        Poner(detalle, "CreditosFtm", " 0.00");

        Poner(detalle, "IVACreditos", 0m);
        Poner(detalle, "IVACreditosFtm", " 0.00");

        Poner(detalle, "SimulaTotalPagar", importe);
        Poner(detalle, "SimulaTotalPagarFtm", importe.ToString("###,###,##0.00"));
    }

    /*
      Si la columna no viene, no se inventa: se deja como esta.

      El procedimiento es de legacy y puede cambiar de columnas; tronar aqui
      dejaria sin papel a quien solo queria ver una cuenta. Y si falta una
      columna que la plantilla SI usa, el constructor de html lo dice al final
      con el marcador sin sustituir, que es donde se nota de verdad.
    */
    private static void Poner(DataRow fila, string columna, object valor)
    {
        if (fila.Table.Columns.Contains(columna))
            fila[columna] = valor;
    }

    private static decimal Numero(DataRow fila, string columna)
    {
        if (!fila.Table.Columns.Contains(columna))
            return 0m;

        var valor = fila[columna];
        if (valor == DBNull.Value)
            return 0m;

        return decimal.TryParse(Convert.ToString(valor), out var n) ? n : 0m;
    }

    /*
      LO QUE SE LEE EN LA PESTANA DEL NAVEGADOR.

      Dice SIMULACION con todas sus letras y, cuando es un pedido sin emitir,
      su folio de pedido. Es el mismo cuidado que el nombre del archivo: quien
      tenga cuatro pestanas abiertas tiene que poder distinguir de un golpe la
      cuenta que esta probando del papel que si entrego.
    */
    private static string TituloDePestana(int idVenta)
        => idVenta < 0
            ? $"Simulacion del pedido {idVenta * -1}"
            : $"Simulacion de la venta {idVenta}";
}
