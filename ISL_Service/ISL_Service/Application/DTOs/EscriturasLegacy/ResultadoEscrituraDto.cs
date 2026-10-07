namespace ISL_Service.Application.DTOs.EscriturasLegacy;

/// <summary>
/// Lo que contesta una escritura de obra (MC) o de aviacion.
///
/// Los sp_n_ de estos dos modulos contestan con un renglon
/// (result, mensaje): en un alta `result` es el ID NUEVO, en una modificacion
/// es 1, y -1 es error del propio procedimiento. Aqui sale como `id` + el
/// `mensaje` que el SP haya querido dar; el -1 no llega al cliente como dato,
/// se vuelve un 409 con el texto del procedimiento.
/// </summary>
public class ResultadoEscrituraDto
{
    /// <summary>El id del renglon: el nuevo en un alta, el que se toco en una modificacion.</summary>
    public int Id { get; set; }

    /// <summary>El texto que devolvio el procedimiento, si devolvio alguno.</summary>
    public string Mensaje { get; set; } = string.Empty;
}
