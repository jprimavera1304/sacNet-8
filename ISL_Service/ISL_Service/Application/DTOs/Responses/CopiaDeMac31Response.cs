namespace ISL_Service.Application.DTOs.Responses;

/// <summary>
/// Una persona que ya tiene sus permisos copiados de Mac31 y por tanto ya no
/// hereda en vivo.
/// </summary>
public class CopiaDeMac31Item
{
    public Guid UserId { get; set; }

    /// <summary>Cuando se copio. En UTC, igual que el resto de las fechas de auditoria.</summary>
    public DateTime Fecha { get; set; }

    /// <summary>Quien pulso el boton. Null si la fila se escribio desde un script.</summary>
    public string? CopiadoPor { get; set; }

    /// <summary>Cuantas claves escribio la copia.</summary>
    public int Claves { get; set; }
}

/// <summary>
/// Lo que paso al copiar, en numeros que la pantalla puede contar al empleado
/// sin hablar de tablas.
/// </summary>
public class CopiaDeMac31Resultado
{
    /// <summary>Falso solo cuando no habia nada que hacer (ya estaba copiada).</summary>
    public bool Copiado { get; set; }

    /// <summary>Claves que quedaron CONCEDIDAS como excepcion propia del web.</summary>
    public int Concedidos { get; set; }

    /// <summary>
    /// Claves que quedaron QUITADAS. Son las que el rol del web da y Mac31 no:
    /// hasta hoy se las borraba la herencia en cada calculo, y al dejar de
    /// heredar hay que dejarlas quitadas a mano o la persona ganaria accesos
    /// que nunca tuvo.
    /// </summary>
    public int Quitados { get; set; }

    /// <summary>
    /// Claves que Mac31 le da y que esta base no tiene en su catalogo, asi que
    /// no se pudieron escribir. Deberia ser 0; si no lo es, es que el catalogo
    /// de permisos no esta sembrado.
    /// </summary>
    public int SinCatalogo { get; set; }

    public string Mensaje { get; set; } = string.Empty;
}
