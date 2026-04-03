namespace ISL_Service.Application.DTOs.GeneracionRolTorneo;

public class GeneracionRolNotaDto
{
    public Guid? CategoriaId { get; set; }
    public string? Categoria { get; set; }
    public string Nota { get; set; } = string.Empty;
}
