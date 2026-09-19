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
    public static RemisionPlantillas Desde(DataTable dtTemplateHtml, string prefijo)
    {
        return new RemisionPlantillas
        {
            Cuerpo = Buscar(dtTemplateHtml, prefijo + "remision_cuerpo_n2"),
            Encabezado = Buscar(dtTemplateHtml, prefijo + "remision_encabezado_n"),
            Detalle = Buscar(dtTemplateHtml, prefijo + "remision_detalle_n"),
            UsadoCargo = Buscar(dtTemplateHtml, prefijo + "remision_usado_cargo_n"),
            UsadoCreditoAnteriorDetalle = Buscar(dtTemplateHtml, prefijo + "remision_usado_credito_anterior_detalle"),
            UsadoCreditoAnteriorEncabezado = Buscar(dtTemplateHtml, prefijo + "remision_usado_credito_anterior_encabezado"),
            UsadoCreditoActual = Buscar(dtTemplateHtml, prefijo + "remision_usado_credito_actual"),
            DevolucionEncabezado = Buscar(dtTemplateHtml, prefijo + "remision_devoluciones_encabezado"),
            DevolucionDetalle = Buscar(dtTemplateHtml, prefijo + "remision_devoluciones_detalle"),
            Pie = Buscar(dtTemplateHtml, prefijo + "remision_pie_n")
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
            $"Falta la plantilla '{descripcion}' en Template_Html (IDTipoTemplateHtml 1001). " +
            "Sin ella la remision saldria incompleta, asi que no se genera.");
    }
}
