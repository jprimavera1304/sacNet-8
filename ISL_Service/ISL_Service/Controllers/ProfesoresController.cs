using System.Security.Claims;
using ISL_Service.Application.DTOs.Profesores;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Backend.Core.Abstractions;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/profesores")]
[Authorize]
public class ProfesoresController : ControllerBase
{
    
    private readonly IProfesoresService _service;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public ProfesoresController(IProfesoresService service, ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet]
    [Authorize(Policy = "perm:profesores.ver")]
    [ProducesResponseType(typeof(List<ProfesorDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] byte? estado, [FromQuery] string? texto, CancellationToken ct)
    {
        var list = await _service.ConsultarAsync(estado, texto, ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:profesores.ver")]
    [ProducesResponseType(typeof(ProfesorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var item = await _service.GetByIdAsync(id, ct);
        if (item is null)
            return NotFound(new { message = "El profesor no existe." });
        return Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = "perm:profesores.crear")]
    [ProducesResponseType(typeof(ProfesorDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateProfesorRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var validation = ValidateCreate(request);
        if (validation is not null)
            return BadRequest(validation);

        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token inválido." });

        var created = await _service.CrearAsync(new CreateProfesorRequest
        {
            Nombre = request.Nombre.Trim(),
            Telefono = request.Telefono.Trim(),
            Correo = request.Correo?.Trim()
        }, userId, ct);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:profesores.editar")]
    [ProducesResponseType(typeof(ProfesorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProfesorRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var validation = ValidateUpdate(request);
        if (validation is not null)
            return BadRequest(validation);

        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token inválido." });

        var updated = await _service.ActualizarAsync(id, new UpdateProfesorRequest
        {
            Nombre = request.Nombre.Trim(),
            Telefono = request.Telefono.Trim(),
            Correo = request.Correo?.Trim()
        }, userId, ct);

        return Ok(updated);
    }

    [HttpPost("{id:guid}/inhabilitar")]
    [Authorize(Policy = "perm:profesores.activar")]
    [ProducesResponseType(typeof(ProfesorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Inhabilitar(Guid id, [FromBody] InhabilitarProfesorRequest? request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token inválido." });

        // Motivo opcional: si no viene, no falla.
        var motivo = request?.Motivo?.Trim();
        if (motivo is { Length: > 200 })
            return BadRequest(new { message = "motivo max 200 caracteres." });

        var disabled = await _service.InhabilitarAsync(id, motivo, userId, ct);
        return Ok(disabled);
    }

    [HttpPost("{id:guid}/habilitar")]
    [Authorize(Policy = "perm:profesores.activar")]
    [ProducesResponseType(typeof(ProfesorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Habilitar(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token inválido." });

        var enabled = await _service.HabilitarAsync(id, userId, ct);
        return Ok(enabled);
    }

    private bool TryGetUserId(out Guid userId)
    {
        var value = _currentUserAccessor.GetUserId(User);
        userId = value ?? Guid.Empty;
        return value.HasValue;
    }

    private static object? ValidateCreate(CreateProfesorRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return new { message = "nombre es requerido." };
        if (request.Nombre.Length > 120)
            return new { message = "nombre max 120 caracteres." };
        if (string.IsNullOrWhiteSpace(request.Telefono))
            return new { message = "telefono es requerido." };
        if (request.Telefono.Length > 30)
            return new { message = "telefono max 30 caracteres." };
        if (!string.IsNullOrWhiteSpace(request.Correo) && request.Correo.Length > 200)
            return new { message = "correo max 200 caracteres." };
        return null;
    }

    private static object? ValidateUpdate(UpdateProfesorRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return new { message = "nombre es requerido." };
        if (request.Nombre.Length > 120)
            return new { message = "nombre max 120 caracteres." };
        if (string.IsNullOrWhiteSpace(request.Telefono))
            return new { message = "telefono es requerido." };
        if (request.Telefono.Length > 30)
            return new { message = "telefono max 30 caracteres." };
        if (!string.IsNullOrWhiteSpace(request.Correo) && request.Correo.Length > 200)
            return new { message = "correo max 200 caracteres." };
        return null;
    }
}
