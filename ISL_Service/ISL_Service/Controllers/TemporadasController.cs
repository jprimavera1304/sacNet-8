using System.Security.Claims;
using ISL_Service.Application.DTOs.Temporadas;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/temporadas")]
[Authorize]
public class TemporadasController : ControllerBase
{
    private readonly ITemporadasService _service;

    public TemporadasController(ITemporadasService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<TemporadaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] byte? estado, [FromQuery] string? texto, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token invalido." });

        var list = await _service.ConsultarTemporadasListadoAsync(estado, texto, userId, ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TemporadaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var item = await _service.GetTemporadaByIdAsync(id, ct);
        if (item is null)
            return NotFound(new { message = "La temporada no existe." });
        return Ok(item);
    }

    [HttpPost]
    [ProducesResponseType(typeof(TemporadaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateTemporadaRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });
        var validation = ValidateTemporadaCreate(request);
        if (validation is not null)
            return BadRequest(validation);
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token invalido." });

        var created = await _service.CrearTemporadaAsync(
            new CreateTemporadaRequest
            {
                Nombre = request.Nombre.Trim(),
                FechaInicio = request.FechaInicio,
                FechaFin = request.FechaFin
            },
            userId,
            ct);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TemporadaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTemporadaRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });
        var validation = ValidateTemporadaUpdate(request);
        if (validation is not null)
            return BadRequest(validation);
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token invalido." });

        var updated = await _service.ActualizarTemporadaAsync(
            id,
            new UpdateTemporadaRequest
            {
                Nombre = request.Nombre.Trim(),
                FechaInicio = request.FechaInicio,
                FechaFin = request.FechaFin
            },
            userId,
            ct);

        return Ok(updated);
    }

    [HttpPost("{id:guid}/cancelar")]
    [ProducesResponseType(typeof(TemporadaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelTemporadaRequest? request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token invalido." });

        var motivo = request?.Motivo?.Trim();
        if (motivo is { Length: > 200 })
            return BadRequest(new { message = "motivo max 200 caracteres." });

        var canceled = await _service.CancelarTemporadaAsync(id, motivo, userId, ct);
        return Ok(canceled);
    }

    [HttpPost("{id:guid}/reactivar")]
    [ProducesResponseType(typeof(TemporadaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reactivar(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token invalido." });

        var updated = await _service.ReactivarTemporadaAsync(id, userId, ct);
        return Ok(updated);
    }

    private bool TryGetUserId(out Guid userId)
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }

    private static object? ValidateTemporadaCreate(CreateTemporadaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return new { message = "nombre es requerido." };
        if (request.Nombre.Length > 80)
            return new { message = "nombre max 80 caracteres." };
        if (request.FechaInicio.HasValue && request.FechaFin.HasValue && request.FechaFin < request.FechaInicio)
            return new { message = "fechaFin no puede ser menor que fechaInicio." };
        return null;
    }

    private static object? ValidateTemporadaUpdate(UpdateTemporadaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return new { message = "nombre es requerido." };
        if (request.Nombre.Length > 80)
            return new { message = "nombre max 80 caracteres." };
        if (request.FechaInicio.HasValue && request.FechaFin.HasValue && request.FechaFin < request.FechaInicio)
            return new { message = "fechaFin no puede ser menor que fechaInicio." };
        return null;
    }
}
