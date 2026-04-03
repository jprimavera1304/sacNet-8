using ISL_Service.Application.DTOs.Requests;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/usuarios")]
[Authorize]
public class UsuariosController : ControllerBase
{
    private readonly IUserAdminService _service;
    private readonly IUsuarioModuloFavoritoService _favoritosService;

    public UsuariosController(IUserAdminService service, IUsuarioModuloFavoritoService favoritosService)
    {
        _service = service;
        _favoritosService = favoritosService;
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
