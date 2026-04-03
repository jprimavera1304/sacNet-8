using ISL_Service.Application.DTOs.UsuarioModuloFavorito;

namespace ISL_Service.Application.Interfaces;

public interface IUsuarioModuloFavoritoRepository
{
    Task<IReadOnlyList<UsuarioModuloFavoritoListItemDto>> ListarAsync(Guid usuarioId, CancellationToken ct = default);
    Task<UsuarioModuloFavoritoDto?> AgregarAsync(Guid usuarioId, string moduloClave, CancellationToken ct = default);
    Task<UsuarioModuloFavoritoDto?> QuitarAsync(Guid usuarioId, string moduloClave, CancellationToken ct = default);
    Task<UsuarioModuloFavoritoDto?> ToggleAsync(Guid usuarioId, string moduloClave, CancellationToken ct = default);
}
