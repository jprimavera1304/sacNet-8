namespace ISL_Service.Application.DTOs.Catalogos;

/// <summary>
/// Item de tarima para catálogo (solo activas). Para dropdown en Almacén de Cascos.
/// </summary>
public class TarimaCatalogItemDto
{
    public int IdTarima { get; set; }
    public string NombreTarima { get; set; } = string.Empty;
    public int IdTipoCasco { get; set; }
    public int NumeroCascosBase { get; set; }
}
