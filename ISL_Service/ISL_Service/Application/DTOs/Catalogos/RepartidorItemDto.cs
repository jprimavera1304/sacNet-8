namespace ISL_Service.Application.DTOs.Catalogos;

/// <summary>
/// Item del catálogo [Catalogo Repartidores]. Para dropdown en Almacén de Cascos.
/// </summary>
public class RepartidorItemDto
{
    public int IdRepartidor { get; set; }
    public string Repartidor { get; set; } = string.Empty;
}
