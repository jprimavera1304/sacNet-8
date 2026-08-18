using ISL_Service.Application.DTOs.Requests;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/usuarios")]
[Authorize]
public class UsuariosController : ControllerBase
{
    // Ver la contrasena de alguien mas no es lo mismo que administrarlo: se separa
    // a proposito de usuarios.editar para que se pueda dar suelto y a quien sea.
    private const string PermisoVerPassword = "usuarios.password.ver";

    private readonly IUserAdminService _service;
    private readonly IUsuarioModuloFavoritoService _favoritosService;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IPermissionService _permissionService;

    public UsuariosController(
        IUserAdminService service,
        IUsuarioModuloFavoritoService favoritosService,
        ICurrentUserAccessor currentUserAccessor,
        IPermissionService permissionService)
    {
        _service = service;
        _favoritosService = favoritosService;
        _currentUserAccessor = currentUserAccessor;
        _permissionService = permissionService;
    }

    /// <summary>
    /// Devuelve el 403 a regresar si al usuario le falta el permiso, o null si puede pasar.
    ///
    /// Se resuelve contra el token y no contra un [Authorize(Policy=...)] porque el
    /// modelo de permisos es por empresa y vive en la base: una politica estatica no
    /// sabe si el tenant siquiera lo tiene prendido.
    /// </summary>
    private async Task<IActionResult?> ExigirPermisoAsync(string permiso, CancellationToken ct)
    {
        var userId = _currentUserAccessor.GetUserId(User);
        if (userId is null)
            return Unauthorized(new { ok = false, message = "Token invalido." });

        var empresaId = _currentUserAccessor.GetCompanyId(User) ?? 0;
        var rol = _currentUserAccessor.GetRole(User);
        var snapshot = await _permissionService.GetPermissionsAsync(userId.Value, empresaId, rol, ct);

        // Instalacion sin modelo de permisos: no hay nada contra que comparar y negar
        // dejaria muerta una funcion en bases que hoy trabajan.
        if (!snapshot.PermissionsEnabled)
            return null;

        var tiene = snapshot.Permissions.Any(x => string.Equals(x, permiso, StringComparison.OrdinalIgnoreCase));
        if (tiene)
            return null;

        return StatusCode(StatusCodes.Status403Forbidden, new { ok = false, message = "No tienes permiso para esta accion." });
    }

    [HttpPost]
    [Authorize(Policy = "perm:usuarios.crear")]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req, CancellationToken ct)
    {
        var result = await _service.CreateUserAsync(req, User, ct);
        return Ok(result);
    }

    [HttpGet]
    [Authorize(Policy = "perm:usuarios.ver_modulo")]
    public async Task<IActionResult> List(CancellationToken ct = default)
    {
        var result = await _service.ListUsersAsync(User, ct);
        return Ok(result);
    }

    [HttpGet("roles")]
    [Authorize(Policy = "perm:usuarios.ver_modulo")]
    public async Task<IActionResult> ListRoles(CancellationToken ct = default)
    {
        var result = await _service.ListRolesCatalogAsync(User, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:usuarios.editar")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest req, CancellationToken ct)
    {
        var result = await _service.UpdateUserAsync(id, req, User, ct);
        return Ok(result);
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = "perm:usuarios.editar")]
    public async Task<IActionResult> PatchUpdate(Guid id, [FromBody] UpdateUserRequest req, CancellationToken ct)
    {
        var result = await _service.UpdateUserAsync(id, req, User, ct);
        return Ok(result);
    }

    [HttpPut]
    [Authorize(Policy = "perm:usuarios.editar")]
    public async Task<IActionResult> UpdateWithBodyId([FromBody] UpdateUserWithIdRequest req, CancellationToken ct)
    {
        var result = await _service.UpdateUserAsync(req.Id, req, User, ct);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/estado")]
    [Authorize(Policy = "perm:usuarios.estado.editar")]
    public async Task<IActionResult> UpdateEstado(Guid id, [FromBody] UpdateUserEstadoRequest req, CancellationToken ct)
    {
        var result = await _service.UpdateEstadoAsync(id, req, User, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}/estado")]
    [Authorize(Policy = "perm:usuarios.estado.editar")]
    public async Task<IActionResult> PutEstado(Guid id, [FromBody] UpdateUserEstadoRequest req, CancellationToken ct)
    {
        var result = await _service.UpdateEstadoAsync(id, req, User, ct);
        return Ok(result);
    }

    [HttpPut("estado")]
    [Authorize(Policy = "perm:usuarios.estado.editar")]
    public async Task<IActionResult> PutEstadoWithBodyId([FromBody] UpdateUserEstadoWithIdRequest req, CancellationToken ct)
    {
        var result = await _service.UpdateEstadoAsync(req.Id, req, User, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}/inactivar")]
    [Authorize(Policy = "perm:usuarios.estado.editar")]
    public async Task<IActionResult> Inactivar(Guid id, CancellationToken ct)
    {
        var result = await _service.UpdateEstadoAsync(id, new UpdateUserEstadoRequest { Estado = 2 }, User, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}/activar")]
    [Authorize(Policy = "perm:usuarios.estado.editar")]
    public async Task<IActionResult> Activar(Guid id, CancellationToken ct)
    {
        var result = await _service.UpdateEstadoAsync(id, new UpdateUserEstadoRequest { Estado = 1 }, User, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/reset-password")]
    [Authorize(Policy = "perm:usuarios.password.reset")]
    public async Task<IActionResult> ResetPassword(Guid id, CancellationToken ct)
    {
        var result = await _service.ResetPasswordAsync(id, User, ct);
        return Ok(result);
    }

    /// <summary>
    /// Fija la contrasena que escribio el administrador. Se guarda en los dos lados:
    /// hash en UsuarioWeb y texto plano en legacy.
    /// </summary>
    [HttpPut("{id:guid}/password")]
    [Authorize(Policy = "perm:usuarios.password.reset")]
    public async Task<IActionResult> SetPassword(Guid id, [FromBody] SetUserPasswordRequest req, CancellationToken ct)
    {
        var result = await _service.SetPasswordAsync(id, req, User, ct);
        return Ok(result);
    }

    /// <summary>
    /// Contrasena en claro del usuario, leida de legacy. Solo lectura.
    /// </summary>
    [HttpGet("{id:guid}/password")]
    public async Task<IActionResult> GetPassword(Guid id, CancellationToken ct)
    {
        var denegado = await ExigirPermisoAsync(PermisoVerPassword, ct);
        if (denegado is not null) return denegado;

        var result = await _service.GetPasswordAsync(id, User, ct);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/empresa")]
    [Authorize(Policy = "perm:usuarios.empresa.editar")]
    public async Task<IActionResult> UpdateEmpresa(Guid id, [FromBody] UpdateUserEmpresaRequest req, CancellationToken ct)
    {
        var result = await _service.UpdateEmpresaAsync(id, req, User, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:usuarios.ver")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetUserByIdAsync(id, User, ct);
        return Ok(result);
    }

    [HttpGet("{usuarioId:guid}/favoritos-modulos")]
    public async Task<IActionResult> ListarFavoritos(Guid usuarioId, CancellationToken ct)
    {
        var data = await _favoritosService.ListarAsync(usuarioId, User, ct);
        return Ok(new { ok = true, message = "Favoritos consultados.", data });
    }

    [HttpPost("{usuarioId:guid}/favoritos-modulos")]
    public async Task<IActionResult> AgregarFavorito(Guid usuarioId, [FromBody] UsuarioModuloFavoritoRequest req, CancellationToken ct)
    {
        var data = await _favoritosService.AgregarAsync(usuarioId, req.ModuloClave, User, ct);
        return Ok(new { ok = true, message = "Favorito agregado.", data });
    }

    [HttpDelete("{usuarioId:guid}/favoritos-modulos/{moduloClave}")]
    public async Task<IActionResult> QuitarFavorito(Guid usuarioId, string moduloClave, CancellationToken ct)
    {
        var data = await _favoritosService.QuitarAsync(usuarioId, moduloClave, User, ct);
        return Ok(new { ok = true, message = "Favorito quitado.", data });
    }

    [HttpPost("{usuarioId:guid}/favoritos-modulos/toggle")]
    public async Task<IActionResult> ToggleFavorito(Guid usuarioId, [FromBody] UsuarioModuloFavoritoRequest req, CancellationToken ct)
    {
        var data = await _favoritosService.ToggleAsync(usuarioId, req.ModuloClave, User, ct);
        return Ok(new { ok = true, message = "Favorito actualizado.", data });
    }

}
