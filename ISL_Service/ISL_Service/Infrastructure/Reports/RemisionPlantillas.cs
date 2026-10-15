using System.Data;

namespace ISL_Service.Infrastructure.Reports;

/*
  LAS PLANTILLAS DE LA REMISION VIVEN EN LA BASE, NO EN EL CODIGO

  Estan en la tabla Template_Html, una fila por pedazo (cuerpo, encabezado,
  renglon del detalle, pie...). Eso es intencional en legacy: el formato del
  papel se ajusta sin recompilar nada. Por eso aqui tampoco se copian al
  codigo: se leen de la misma tabla, y asi el web y Mac31 imprimen el MISMO
  papel aunque alguien ajuste una plantilla manana.

  Hay dos juegos completos:
    - sin prefijo      -> primera impresion
    - prefijo "reimp_" -> reimpresion (lleva la marca de quien reimprimio)
  Cual se usa lo decide sp_n_VentasInformacion: si devuelve la columna
  Reimpresion con texto, es reimpresion.

  Es la copia de SetHtmlImprimir de
  Legacy/MacServicios2/.../VentasImprimirService.cs.

  Y TAMBIEN SIRVEN PARA LA SIMULACION, CON OTRA RAIZ

  El reporte 1175 (SIMULAR) tiene en Template_Html las MISMAS DIEZ PIEZAS con
  el mismo sufijo y otra raiz: `simular_cuerpo_n2`, `simular_encabezado_n`,
  `simular_detalle_n`... Medido en las dos bases: diez filas cada una bajo
  IDTipoTemplateHtml 1175. Por eso la raiz es un parametro y no hay una segunda
  clase copiada: el papel de la simulacion es el de la remision con otros
  textos, y asi se mantienen los dos de una sola vez.

  La simulacion NO tiene juego `reimp_`: alla no hay reimpresion que marcar
  porque no hay venta emitida. Ver SetHtmlSimular, que solo lee el juego
  normal.
*/
public sealed class RemisionPlantillas
{
    public string Cuerpo { get; private init; } = "";
    public string Encabezado { get; private init; } = "";
    public string Detalle { get; private init; } = "";
    public string UsadoCargo { get; private init; } = "";
    public string UsadoCreditoAnteriorDetalle { get; private init; } = "";
    public string UsadoCreditoAnteriorEncabezado { get; private init; } = "";
    public string UsadoCreditoActual { get; private init; } = "";
    public string DevolucionEncabezado { get; private init; } = "";
    public string DevolucionDetalle { get; private init; } = "";
    public string Pie { get; private init; } = "";

    /*
      Si falta UNA plantilla se truena con su nombre.

      Es a proposito: una plantilla que no esta no deja el papel "un poco
      distinto", deja un pedazo entero fuera —el pie, o los cargos por cascos—
      y una remision con menos conceptos de los que se entregaron no la nota
      nadie hasta que el cliente reclama.
    */
    /// <param name="raiz">
    /// "remision" para el papel de la venta (1001) y "simular" para el de la
    /// simulacion (1175). Son los nombres tal cual estan en Template_Html.
    /// </param>
    public static RemisionPlantillas Desde(DataTable dtTemplateHtml, string prefijo, string raiz = "remision")
    {
        return new RemisionPlantillas
        {
            Cuerpo = Buscar(dtTemplateHtml, prefijo + raiz + "_cuerpo_n2"),
            Encabezado = Buscar(dtTemplateHtml, prefijo + raiz + "_encabezado_n"),
            Detalle = Buscar(dtTemplateHtml, prefijo + raiz + "_detalle_n"),
            UsadoCargo = Buscar(dtTemplateHtml, prefijo + raiz + "_usado_cargo_n"),
            UsadoCreditoAnteriorDetalle = Buscar(dtTemplateHtml, prefijo + raiz + "_usado_credito_anterior_detalle"),
            UsadoCreditoAnteriorEncabezado = Buscar(dtTemplateHtml, prefijo + raiz + "_usado_credito_anterior_encabezado"),
            UsadoCreditoActual = Buscar(dtTemplateHtml, prefijo + raiz + "_usado_credito_actual"),
            DevolucionEncabezado = Buscar(dtTemplateHtml, prefijo + raiz + "_devoluciones_encabezado"),
            DevolucionDetalle = Buscar(dtTemplateHtml, prefijo + raiz + "_devoluciones_detalle"),
            Pie = Buscar(dtTemplateHtml, prefijo + raiz + "_pie_n")
        };
    }

    private static string Buscar(DataTable dtTemplateHtml, string descripcion)
    {
        foreach (DataRow row in dtTemplateHtml.Rows)
        {
            var actual = Convert.ToString(row["Descripcion"]) ?? "";
            if (string.Equals(actual.Trim(), descripcion, StringComparison.OrdinalIgnoreCase))
                return Convert.ToString(row["html"]) ?? "";
        }

        throw new InvalidOperationException(
            $"Falta la plantilla '{descripcion}' en Template_Html. " +
            "Sin ella el papel saldria incompleto, asi que no se genera.");
    }
}
