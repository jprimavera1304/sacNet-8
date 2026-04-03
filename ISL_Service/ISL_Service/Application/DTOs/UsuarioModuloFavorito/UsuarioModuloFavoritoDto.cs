namespace ISL_Service.Application.DTOs.UsuarioModuloFavorito;

public class UsuarioModuloFavoritoDto
{
    public long Id { get; set; }
    public int EmpresaId { get; set; }
    public Guid UsuarioWebId { get; set; }
    public string ModuloClave { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime FechaActualizacion { get; set; }
}
