using ISL_Service.Application.DTOs.Requests;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/usuarios")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class UsuariosController : ControllerBase
{
    private readonly IUserAdminService _service;

    public UsuariosController(IUserAdminService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req, CancellationToken ct)
    {
        var result = await _service.CreateUserAsync(req, User, ct);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? empresaId = null, CancellationToken ct = default)
    {
        var result = await _service.ListUsersAsync(empresaId, User, ct);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/estado")]
    public async Task<IActionResult> UpdateEstado(Guid id, [FromBody] UpdateUserEstadoRequest req, CancellationToken ct)
    {
        var result = await _service.UpdateEstadoAsync(id, req, User, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, CancellationToken ct)
    {
        var result = await _service.ResetPasswordAsync(id, User, ct);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/empresa")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> UpdateEmpresa(Guid id, [FromBody] UpdateUserEmpresaRequest req, CancellationToken ct)
    {
        var result = await _service.UpdateEmpresaAsync(id, req, User, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetUserByIdAsync(id, User, ct);
        return Ok(result);
    }

}
