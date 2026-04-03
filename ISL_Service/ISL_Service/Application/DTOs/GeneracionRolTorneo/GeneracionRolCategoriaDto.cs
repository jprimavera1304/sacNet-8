namespace ISL_Service.Application.DTOs.GeneracionRolTorneo;

public class GeneracionRolCategoriaDto
{
    public Guid CategoriaId { get; set; }
    public string? Categoria { get; set; }
    public int? TotalEquipos { get; set; }
    public int? TotalElegibles { get; set; }
    public byte? DiaJuego { get; set; }
    public string? DiaJuegoNombre { get; set; }
}
