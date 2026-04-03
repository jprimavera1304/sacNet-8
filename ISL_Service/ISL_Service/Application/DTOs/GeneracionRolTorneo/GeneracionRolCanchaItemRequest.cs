namespace ISL_Service.Application.DTOs.GeneracionRolTorneo;

public class GeneracionRolCanchaItemRequest
{
    public string NombreCancha { get; set; } = string.Empty;
    public Guid CategoriaId { get; set; }
}
