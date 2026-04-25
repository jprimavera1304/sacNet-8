using System.Security.Claims;
using ISL_Service.Application.DTOs.Categorias;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Backend.Core.Abstractions;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/categorias")]
[Authorize]
public class CategoriasController : ControllerBase
{
    
    private readonly ICategoriasService _service;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public CategoriasController(ICategoriasService service, ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet]
    [Authorize(Policy = "perm:categorias.ver")]
    [ProducesResponseType(typeof(List<CategoriaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] byte? estado, [FromQuery] string? texto, CancellationToken ct)
    {
        var list = await _service.ConsultarAsync(estado, texto, ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:categorias.ver")]
    [ProducesResponseType(typeof(CategoriaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var item = await _service.GetByIdAsync(id, ct);
        if (item is null)
            return NotFound(new { message = "La categoria no existe." });
        return Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = "perm:categorias.crear")]
    [ProducesResponseType(typeof(CategoriaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateCategoriaRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var validation = ValidateCreate(request);
        if (validation is not null)
            return BadRequest(validation);

        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token invÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡lido." });

        var created = await _service.CrearAsync(new CreateCategoriaRequest
        {
            Nombre = request.Nombre.Trim()
        }, userId, ct);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:categorias.editar")]
    [ProducesResponseType(typeof(CategoriaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCategoriaRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var validation = ValidateUpdate(request);
        if (validation is not null)
            return BadRequest(validation);

        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token invÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡lido." });

        var updated = await _service.ActualizarAsync(id, new UpdateCategoriaRequest
        {
            Nombre = request.Nombre.Trim()
        }, userId, ct);

        return Ok(updated);
    }

    [HttpPost("{id:guid}/inhabilitar")]
    [Authorize(Policy = "perm:categorias.activar")]
    [ProducesResponseType(typeof(CategoriaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Inhabilitar(Guid id, [FromBody] InhabilitarCategoriaRequest? request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token invÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡lido." });

        var motivo = request?.Motivo?.Trim();
        if (motivo is { Length: > 200 })
            return BadRequest(new { message = "motivo max 200 caracteres." });

        var disabled = await _service.InhabilitarAsync(id, motivo, userId, ct);
        return Ok(disabled);
    }

    [HttpPost("{id:guid}/habilitar")]
    [Authorize(Policy = "perm:categorias.activar")]
    [ProducesResponseType(typeof(CategoriaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Habilitar(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token invÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡lido." });

        var enabled = await _service.HabilitarAsync(id, userId, ct);
        return Ok(enabled);
    }

    private bool TryGetUserId(out Guid userId)
    {
        var value = _currentUserAccessor.GetUserId(User);
        userId = value ?? Guid.Empty;
        return value.HasValue;
    }

    private static object? ValidateCreate(CreateCategoriaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return new { message = "nombre es requerido." };
        if (request.Nombre.Length > 120)
            return new { message = "nombre max 120 caracteres." };
        return null;
    }

    private static object? ValidateUpdate(UpdateCategoriaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return new { message = "nombre es requerido." };
        if (request.Nombre.Length > 120)
            return new { message = "nombre max 120 caracteres." };
        return null;
    }
}
