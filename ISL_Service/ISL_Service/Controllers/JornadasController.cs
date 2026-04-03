using System.Security.Claims;
using ISL_Service.Application.DTOs.Jornadas;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/jornadas")]
[Authorize]
public class JornadasController : ControllerBase
{
    private readonly IJornadasService _service;

    public JornadasController(IJornadasService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Policy = "perm:jornadas.ver")]
    [ProducesResponseType(typeof(List<JornadaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] byte? estado, [FromQuery] string? texto, CancellationToken ct)
    {
        var list = await _service.ConsultarAsync(estado, texto, ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:jornadas.ver")]
    [ProducesResponseType(typeof(JornadaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var item = await _service.GetByIdAsync(id, ct);
        if (item is null)
            return NotFound(new { message = "La jornada no existe." });
        return Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = "perm:jornadas.crear")]
    [ProducesResponseType(typeof(JornadaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateJornadaRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var validation = ValidateCreate(request);
        if (validation is not null)
            return BadRequest(validation);

        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token inválido." });

        var created = await _service.CrearAsync(new CreateJornadaRequest
        {
            NumeroJornada = request.NumeroJornada
        }, userId, ct);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:jornadas.editar")]
    [ProducesResponseType(typeof(JornadaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateJornadaRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var validation = ValidateUpdate(request);
        if (validation is not null)
            return BadRequest(validation);

        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token inválido." });

        var updated = await _service.ActualizarAsync(id, new UpdateJornadaRequest
        {
            NumeroJornada = request.NumeroJornada
        }, userId, ct);

        return Ok(updated);
    }

    [HttpPost("{id:guid}/inhabilitar")]
    [Authorize(Policy = "perm:jornadas.activar")]
    [ProducesResponseType(typeof(JornadaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Inhabilitar(Guid id, [FromBody] InhabilitarJornadaRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token inválido." });

        var motivo = request.Motivo?.Trim();
        if (string.IsNullOrWhiteSpace(motivo))
            return BadRequest(new { message = "motivo es requerido." });
        if (motivo.Length > 200)
            return BadRequest(new { message = "motivo max 200 caracteres." });

        var disabled = await _service.InhabilitarAsync(id, motivo, userId, ct);
        return Ok(disabled);
    }

    private bool TryGetUserId(out Guid userId)
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }

    private static object? ValidateCreate(CreateJornadaRequest request)
    {
        if (request.NumeroJornada <= 0)
            return new { message = "numeroJornada debe ser mayor a cero." };
        return null;
    }

    private static object? ValidateUpdate(UpdateJornadaRequest request)
    {
        if (request.NumeroJornada <= 0)
            return new { message = "numeroJornada debe ser mayor a cero." };
        return null;
    }
}
