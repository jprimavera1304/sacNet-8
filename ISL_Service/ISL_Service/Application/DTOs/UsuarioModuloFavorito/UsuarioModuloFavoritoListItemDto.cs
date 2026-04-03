namespace ISL_Service.Application.DTOs.UsuarioModuloFavorito;

public class UsuarioModuloFavoritoListItemDto
{
    public int ModuloId { get; set; }
    public string ModuloClave { get; set; } = string.Empty;
    public string ModuloNombre { get; set; } = string.Empty;
    public DateTime FechaCreacionFavorito { get; set; }
}
