namespace ISL_Service.Application.DTOs.Catalogos;

/// <summary>
/// Item del catalogo de tipos de casco (TiposUsados). Para dropdown en front.
/// </summary>
public class TipoCascoItemDto
{
    public int IdTipoCasco { get; set; }
    public string Descripcion { get; set; } = string.Empty;
}
