using System.Security.Claims;
using ISL_Service.Application.DTOs.InscripcionesTorneo;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/inscripciones-torneo")]
[Authorize]
public class InscripcionesTorneoController : ControllerBase
{
    private readonly IInscripcionesTorneoService _service;

    public InscripcionesTorneoController(IInscripcionesTorneoService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Policy = "perm:inscripciones.ver")]
    [ProducesResponseType(typeof(List<InscripcionTorneoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? torneoId,
        [FromQuery] Guid? categoriaId,
        [FromQuery] byte? estado,
        [FromQuery] string? texto,
        CancellationToken ct)
    {
        var list = await _service.ConsultarAsync(torneoId, categoriaId, estado, texto, ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:inscripciones.ver")]
    [ProducesResponseType(typeof(InscripcionTorneoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var item = await _service.GetByIdAsync(id, ct);
        if (item is null)
            return NotFound(new { message = "La inscripción no existe." });
        return Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = "perm:inscripciones.crear")]
    [ProducesResponseType(typeof(InscripcionTorneoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateInscripcionTorneoRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var validation = ValidateCreate(request);
        if (validation is not null)
            return BadRequest(validation);

        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token inválido." });

        var created = await _service.CrearAsync(new CreateInscripcionTorneoRequest
        {
            TorneoId = request.TorneoId,
            EquipoId = request.EquipoId,
            CategoriaId = request.CategoriaId,
            DiaJuego = request.DiaJuego,
            ProfesorTitularId = request.ProfesorTitularId,
            ProfesorAuxiliarId = request.ProfesorAuxiliarId
        }, userId, ct);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:inscripciones.editar")]
    [ProducesResponseType(typeof(InscripcionTorneoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateInscripcionTorneoRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var validation = ValidateUpdate(request);
        if (validation is not null)
            return BadRequest(validation);

        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token inválido." });

        var updated = await _service.ActualizarAsync(id, new UpdateInscripcionTorneoRequest
        {
            CategoriaId = request.CategoriaId,
            DiaJuego = request.DiaJuego,
            ProfesorTitularId = request.ProfesorTitularId,
            ProfesorAuxiliarId = request.ProfesorAuxiliarId
        }, userId, ct);

        return Ok(updated);
    }

    [HttpPost("{id:guid}/inhabilitar")]
    [Authorize(Policy = "perm:inscripciones.activar")]
    [ProducesResponseType(typeof(InscripcionTorneoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Inhabilitar(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] InhabilitarInscripcionTorneoRequest? request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token inválido." });

        var motivo = request?.Motivo?.Trim();
        if (motivo is { Length: > 200 })
            return BadRequest(new { message = "motivo max 200 caracteres." });

        var disabled = await _service.InhabilitarAsync(id, motivo, userId, ct);
        return Ok(disabled);
    }

    [HttpPost("{id:guid}/habilitar")]
    [Authorize(Policy = "perm:inscripciones.activar")]
    [ProducesResponseType(typeof(InscripcionTorneoDto), StatusCodes.Status200OK)]
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
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }

    private static object? ValidateCreate(CreateInscripcionTorneoRequest request)
    {
        if (request.TorneoId == Guid.Empty)
            return new { message = "torneoId es requerido." };
        if (request.EquipoId == Guid.Empty)
            return new { message = "equipoId es requerido." };
        if (request.CategoriaId.HasValue && request.CategoriaId.Value == Guid.Empty)
            return new { message = "categoriaId inválido." };
        if (request.DiaJuego.HasValue && request.DiaJuego is < 1 or > 2)
            return new { message = "diaJuego inválido. Use 1 o 2." };
        if (request.ProfesorTitularId.HasValue && request.ProfesorTitularId.Value == Guid.Empty)
            return new { message = "profesorTitularId inválido." };
        if (request.ProfesorAuxiliarId.HasValue && request.ProfesorAuxiliarId.Value == Guid.Empty)
            return new { message = "profesorAuxiliarId inválido." };
        if (request.ProfesorAuxiliarId.HasValue && request.ProfesorTitularId.HasValue &&
            request.ProfesorAuxiliarId.Value == request.ProfesorTitularId.Value)
            return new { message = "Titular y auxiliar no pueden ser el mismo profesor." };
        return null;
    }

    private static object? ValidateUpdate(UpdateInscripcionTorneoRequest request)
    {
        if (request.CategoriaId.HasValue && request.CategoriaId.Value == Guid.Empty)
            return new { message = "categoriaId inválido." };
        if (request.DiaJuego.HasValue && request.DiaJuego is < 1 or > 2)
            return new { message = "diaJuego inválido. Use 1 o 2." };
        if (request.ProfesorTitularId.HasValue && request.ProfesorTitularId.Value == Guid.Empty)
            return new { message = "profesorTitularId inválido." };
        if (request.ProfesorAuxiliarId.HasValue && request.ProfesorAuxiliarId.Value == Guid.Empty)
            return new { message = "profesorAuxiliarId inválido." };
        if (request.ProfesorAuxiliarId.HasValue && request.ProfesorTitularId.HasValue &&
            request.ProfesorAuxiliarId.Value == request.ProfesorTitularId.Value)
            return new { message = "Titular y auxiliar no pueden ser el mismo profesor." };
        return null;
    }
}