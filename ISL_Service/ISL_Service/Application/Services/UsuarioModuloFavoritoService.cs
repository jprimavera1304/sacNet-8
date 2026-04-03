using System.Security.Claims;
using ISL_Service.Application.DTOs.UsuarioModuloFavorito;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;

namespace ISL_Service.Application.Services;

public class UsuarioModuloFavoritoService : IUsuarioModuloFavoritoService
{
    private const int ESTADO_ACTIVO = 1;

    private readonly IUsuarioModuloFavoritoRepository _repo;
    private readonly IUserRepository _userRepo;
    private readonly IPermissionService _permissionService;

    public UsuarioModuloFavoritoService(
        IUsuarioModuloFavoritoRepository repo,
        IUserRepository userRepo,
        IPermissionService permissionService)
    {
        _repo = repo;
        _userRepo = userRepo;
        _permissionService = permissionService;
    }

    public async Task<IReadOnlyList<UsuarioModuloFavoritoListItemDto>> ListarAsync(Guid usuarioId, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await EnsureActorCanAccessAsync(usuarioId, actor, ct);
        return await _repo.ListarAsync(usuarioId, ct);
    }

    public async Task<UsuarioModuloFavoritoDto> AgregarAsync(Guid usuarioId, string moduloClave, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        var target = await EnsureActorCanAccessAsync(usuarioId, actor, ct);
        await EnsureModuloActivoAsync(target.EmpresaId, moduloClave, ct);

        var result = await _repo.AgregarAsync(usuarioId, moduloClave, ct);
        if (result is null)
            throw new InvalidOperationException("No se pudo agregar el favorito.");
        return result;
    }

    public async Task<UsuarioModuloFavoritoDto> QuitarAsync(Guid usuarioId, string moduloClave, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await EnsureActorCanAccessAsync(usuarioId, actor, ct);

        var result = await _repo.QuitarAsync(usuarioId, moduloClave, ct);
        if (result is null)
            throw new InvalidOperationException("No se pudo quitar el favorito.");
        return result;
    }

    public async Task<UsuarioModuloFavoritoDto> ToggleAsync(Guid usuarioId, string moduloClave, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        var target = await EnsureActorCanAccessAsync(usuarioId, actor, ct);
        await EnsureModuloActivoAsync(target.EmpresaId, moduloClave, ct);

        var result = await _repo.ToggleAsync(usuarioId, moduloClave, ct);
        if (result is null)
            throw new InvalidOperationException("No se pudo actualizar el favorito.");
        return result;
    }

    private async Task<Domain.Entities.Usuario> EnsureActorCanAccessAsync(Guid usuarioId, ClaimsPrincipal actor, CancellationToken ct)
    {
        var currentId = CurrentUser.GetUserId(actor);
        var isSuper = CurrentUser.IsSuperAdmin(actor);
        if (!isSuper && currentId != usuarioId)
            throw new UnauthorizedException("No puedes modificar favoritos de otro usuario.");

        var user = await _userRepo.GetByIdAsync(usuarioId, ct);
        if (user is null)
            throw new NotFoundException("Usuario no encontrado.");
        if (user.Estado != ESTADO_ACTIVO)
            throw new ConflictException("Usuario no existe o esta inactivo.");

        return user;
    }

    private async Task EnsureModuloActivoAsync(int empresaId, string moduloClave, CancellationToken ct)
    {
        var clave = (moduloClave ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(clave))
            throw new ArgumentException("ModuloClave es obligatoria.");

        var modules = await _permissionService.GetModuleCatalogAsync(empresaId, ct);
        var match = modules.FirstOrDefault(m => string.Equals(m.ModuloClave, clave, StringComparison.OrdinalIgnoreCase));
        if (match is null || match.IdStatus != 1)
            throw new ConflictException("Modulo no existe o esta inactivo.");
    }
}
