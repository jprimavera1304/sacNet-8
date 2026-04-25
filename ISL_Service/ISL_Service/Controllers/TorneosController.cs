using System.Security.Claims;
using ISL_Service.Application.DTOs.Temporadas;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Backend.Core.Abstractions;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/torneos")]
[Authorize]
public class TorneosController : ControllerBase
{
    
    private readonly ITemporadasService _service;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public TorneosController(ITemporadasService service, ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<TorneoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List([FromQuery] Guid? temporadaId, [FromQuery] byte? estado, [FromQuery] string? texto, [FromQuery] DateTime? fechaInicio, [FromQuery] DateTime? fechaFin, CancellationToken ct)
    {
        if (fechaInicio.HasValue && fechaFin.HasValue && fechaFin < fechaInicio)
            return BadRequest(new { message = "fechaFin no puede ser menor que fechaInicio." });
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token invÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡lido." });

        var list = await _service.ConsultarTorneosListadoAsync(temporadaId, estado, texto, fechaInicio, fechaFin, userId, ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TorneoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var item = await _service.GetTorneoByIdAsync(id, ct);
        if (item is null)
            return NotFound(new { message = "El torneo no existe." });
        return Ok(item);
    }

    [HttpPost]
    [ProducesResponseType(typeof(TorneoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateTorneoRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });
        var validation = ValidateTorneoCreate(request);
        if (validation is not null)
            return BadRequest(validation);
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token invÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡lido." });

        var created = await _service.CrearTorneoAsync(
            new CreateTorneoRequest
            {
                TemporadaId = request.TemporadaId,
                Nombre = request.Nombre.Trim(),
                Clave = request.Clave?.Trim(),
                FechaInicio = request.FechaInicio,
                FechaFin = request.FechaFin
            },
            userId,
            ct);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TorneoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTorneoRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });
        var validation = ValidateTorneoUpdate(request);
        if (validation is not null)
            return BadRequest(validation);
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token invÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡lido." });

        var updated = await _service.ActualizarTorneoAsync(
            id,
            new UpdateTorneoRequest
            {
                TemporadaId = request.TemporadaId,
                Nombre = request.Nombre.Trim(),
                Clave = request.Clave?.Trim(),
                FechaInicio = request.FechaInicio,
                FechaFin = request.FechaFin
            },
            userId,
            ct);

        return Ok(updated);
    }

    [HttpPost("{id:guid}/cancelar")]
    [ProducesResponseType(typeof(TorneoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelTorneoRequest? request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token invÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡lido." });

        var motivo = request?.Motivo?.Trim();
        if (motivo is { Length: > 200 })
            return BadRequest(new { message = "motivo max 200 caracteres." });

        var canceled = await _service.CancelarTorneoAsync(id, motivo, userId, ct);
        return Ok(canceled);
    }

    [HttpPost("{id:guid}/activar")]
    [ProducesResponseType(typeof(TorneoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Activar(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token invÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡lido." });

        var activated = await _service.ActivarTorneoAsync(id, userId, ct);
        return Ok(activated);
    }

    [HttpPost("{id:guid}/cerrar")]
    [ProducesResponseType(typeof(TorneoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cerrar(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token invÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡lido." });

        var closed = await _service.CerrarTorneoAsync(id, userId, ct);
        return Ok(closed);
    }

    [HttpPost("{id:guid}/reactivar")]
    [ProducesResponseType(typeof(TorneoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reactivar(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token invÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡lido." });

        var reactivated = await _service.ReactivarTorneoAsync(id, userId, ct);
        return Ok(reactivated);
    }

    [HttpPost("cerrar-vencidos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CerrarVencidos([FromQuery] DateTime? fechaCorte, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token invÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡lido." });

        var cerrados = await _service.CerrarTorneosVencidosAsync(userId, fechaCorte, ct);
        return Ok(new
        {
            torneosCerrados = cerrados,
            fechaCorte = (fechaCorte ?? DateTime.UtcNow.Date).Date
        });
    }

    private bool TryGetUserId(out Guid userId)
    {
        var value = _currentUserAccessor.GetUserId(User);
        userId = value ?? Guid.Empty;
        return value.HasValue;
    }

    private static object? ValidateTorneoCreate(CreateTorneoRequest request)
    {
        if (request.TemporadaId == Guid.Empty)
            return new { message = "temporadaId es requerido." };
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return new { message = "nombre es requerido." };
        if (request.Nombre.Length > 120)
            return new { message = "nombre max 120 caracteres." };
        if (!string.IsNullOrWhiteSpace(request.Clave) && request.Clave.Length > 30)
            return new { message = "clave max 30 caracteres." };
        if (request.FechaInicio.HasValue && request.FechaFin.HasValue && request.FechaFin < request.FechaInicio)
            return new { message = "fechaFin no puede ser menor que fechaInicio." };
        return null;
    }

    private static object? ValidateTorneoUpdate(UpdateTorneoRequest request)
    {
        if (request.TemporadaId == Guid.Empty)
            return new { message = "temporadaId es requerido." };
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return new { message = "nombre es requerido." };
        if (request.Nombre.Length > 120)
            return new { message = "nombre max 120 caracteres." };
        if (!string.IsNullOrWhiteSpace(request.Clave) && request.Clave.Length > 30)
            return new { message = "clave max 30 caracteres." };
        if (request.FechaInicio.HasValue && request.FechaFin.HasValue && request.FechaFin < request.FechaInicio)
            return new { message = "fechaFin no puede ser menor que fechaInicio." };
        return null;
    }
}
