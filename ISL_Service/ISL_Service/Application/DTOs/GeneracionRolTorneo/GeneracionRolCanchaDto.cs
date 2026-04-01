namespace ISL_Service.Application.DTOs.GeneracionRolTorneo;

public class GeneracionRolCanchaDto
{
    public Guid Id { get; set; }
    public Guid GeneracionRolTorneoId { get; set; }
    public Guid CategoriaId { get; set; }
    public string? Categoria { get; set; }
    public string? NombreCancha { get; set; }
}
