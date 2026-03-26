namespace ISL_Service.Application.DTOs.Catalogos;

public class ClienteCatalogItemDto
{
    public int IDCliente { get; set; }
    public int IDStatus { get; set; }
    public string Numero { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}
