using System.Security.Claims;
using ISL_Service.Application.DTOs.UsuarioModuloFavorito;

namespace ISL_Service.Application.Interfaces;

public interface IUsuarioModuloFavoritoService
{
    Task<IReadOnlyList<UsuarioModuloFavoritoListItemDto>> ListarAsync(Guid usuarioId, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<UsuarioModuloFavoritoDto> AgregarAsync(Guid usuarioId, string moduloClave, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<UsuarioModuloFavoritoDto> QuitarAsync(Guid usuarioId, string moduloClave, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<UsuarioModuloFavoritoDto> ToggleAsync(Guid usuarioId, string moduloClave, ClaimsPrincipal actor, CancellationToken ct = default);
}
