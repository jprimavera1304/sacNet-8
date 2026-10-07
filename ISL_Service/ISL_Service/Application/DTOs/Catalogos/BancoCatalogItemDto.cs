namespace ISL_Service.Application.DTOs.Catalogos;

public class BancoCatalogItemDto
{
    public int IDBanco { get; set; }
    public int IDStatus { get; set; }
    public string Banco { get; set; } = string.Empty;
    /// <summary>El texto del estatus ("ACTIVO" / "INACTIVO"), que es lo que se
    /// pinta en la columna ESTATUS de la pantalla. Antes solo venia el numero.</summary>
    public string Status { get; set; } = string.Empty;

}
