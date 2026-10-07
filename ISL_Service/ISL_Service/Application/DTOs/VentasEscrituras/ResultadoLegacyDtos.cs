namespace ISL_Service.Application.DTOs.VentasEscrituras;

/*
  LO QUE CONTESTA UN sp_n_ DE ESCRITURA, SIN REINTERPRETARLO.

  Los procedimientos de legacy no tienen UNA forma de contestar, tienen tres, y
  las tres se ven en esta ola:

    a) un renglon con `result` (1 bien, -1 mal) y `mensaje`;
    b) un renglon con el id de lo que acaban de crear (IDCobranza, IDGarantia…);
    c) nada, y si algo salio mal lo dicen con RAISERROR de severidad 16.

  No se normalizan a una sola. `Filas` baja tal cual lo que devolvio el
  procedimiento y `Resultado`/`Mensaje` se llenan solo cuando esas columnas
  existen de verdad.

  POR QUE 200 NO SIGNIFICA QUE LA OPERACION SE HIZO
  -------------------------------------------------
  Es la convencion que ya usan devolucion, cancelacion y multipago en este
  backend: `ok` dice que la llamada se atendio, y si el negocio dijo que no, lo
  dice el payload (`data.resultado` = -1 con su `mensaje`). Convertir el -1 en
  un 400 seria inventar un contrato que las pantallas no esperan, y perder el
  resto del renglon.
*/
public class ResultadoLegacyDto
{
    /// <summary>
    /// Lo que dijo la columna `result` del procedimiento: 1 se hizo, -1 no se
    /// pudo. `null` cuando el procedimiento no devuelve esa columna — y eso no
    /// es un fallo: significa que avisa por RAISERROR, no por renglon.
    /// </summary>
    public int? Resultado { get; set; }

    /// <summary>El mensaje literal del procedimiento, sin tocar.</summary>
    public string? Mensaje { get; set; }

    /// <summary>
    /// El id de lo que se creo, cuando el procedimiento lo devuelve.
    /// </summary>
    public int? Id { get; set; }

    /// <summary>
    /// Los renglones que devolvio el procedimiento, tal como salieron.
    /// </summary>
    public List<Dictionary<string, object?>> Filas { get; set; } = new();

    /// <summary>
    /// Las tablas siguientes, cuando el procedimiento devuelve mas de una.
    /// Vacio en la mayoria.
    /// </summary>
    public List<List<Dictionary<string, object?>>> TablasExtra { get; set; } = new();

    /// <summary>
    /// Si la operacion se hizo. Cuando el procedimiento no devuelve `result`,
    /// haber llegado hasta aqui sin RAISERROR ES el exito.
    /// </summary>
    public bool Hecho => Resultado is null || Resultado.Value > 0;
}

/// <summary>
/// La salida de una consulta de legacy: los renglones del procedimiento y
/// cuantos son. `total` existe porque el front lo pinta en la esquina y
/// contarlo en el navegador obliga a traer la lista entera a la vista.
/// </summary>
public class FilasLegacyDto
{
    public List<Dictionary<string, object?>> Rows { get; set; } = new();

    public int Total => Rows.Count;

    /// <summary>
    /// Las tablas adicionales del procedimiento, si devolvio mas de una.
    /// </summary>
    public List<List<Dictionary<string, object?>>> Extra { get; set; } = new();
}
