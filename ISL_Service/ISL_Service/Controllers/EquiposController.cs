using System.Security.Claims;
using ISL_Service.Application.DTOs.Equipos;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/equipos")]
[Authorize]
public class EquiposController : ControllerBase
{
    private readonly IEquiposService _service;

    public EquiposController(IEquiposService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Policy = "perm:equipos.ver")]
    [ProducesResponseType(typeof(List<EquipoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] byte? estado,
        [FromQuery] Guid? categoriaId,
        [FromQuery] byte? diaJuego,
        [FromQuery] string? texto,
        CancellationToken ct)
    {
        var list = await _service.ConsultarAsync(estado, categoriaId, diaJuego, texto, ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:equipos.ver")]
    [ProducesResponseType(typeof(EquipoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var item = await _service.GetByIdAsync(id, ct);
        if (item is null)
            return NotFound(new { message = "El equipo no existe." });
        return Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = "perm:equipos.crear")]
    [ProducesResponseType(typeof(EquipoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateEquipoRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var validation = ValidateCreate(request);
        if (validation is not null)
            return BadRequest(validation);

        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token inválido." });

        var created = await _service.CrearAsync(new CreateEquipoRequest
        {
            Nombre = request.Nombre.Trim(),
            CategoriaPredeterminadaId = request.CategoriaPredeterminadaId,
            DiaJuegoPredeterminado = request.DiaJuegoPredeterminado,
            ProfesorTitularPredeterminadoId = request.ProfesorTitularPredeterminadoId,
            ProfesorAuxiliarPredeterminadoId = request.ProfesorAuxiliarPredeterminadoId
        }, userId, ct);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:equipos.editar")]
    [ProducesResponseType(typeof(EquipoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEquipoRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var validation = ValidateUpdate(request);
        if (validation is not null)
            return BadRequest(validation);

        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token inválido." });

        var updated = await _service.ActualizarAsync(id, new UpdateEquipoRequest
        {
            Nombre = request.Nombre.Trim(),
            CategoriaPredeterminadaId = request.CategoriaPredeterminadaId,
            DiaJuegoPredeterminado = request.DiaJuegoPredeterminado,
            ProfesorTitularPredeterminadoId = request.ProfesorTitularPredeterminadoId,
            ProfesorAuxiliarPredeterminadoId = request.ProfesorAuxiliarPredeterminadoId
        }, userId, ct);

        return Ok(updated);
    }

    [HttpPost("{id:guid}/inhabilitar")]
    [Authorize(Policy = "perm:equipos.activar")]
    [ProducesResponseType(typeof(EquipoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Inhabilitar(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] InhabilitarEquipoRequest? request,
        CancellationToken ct)
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
    [Authorize(Policy = "perm:equipos.activar")]
    [ProducesResponseType(typeof(EquipoDto), StatusCodes.Status200OK)]
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

    private static object? ValidateCreate(CreateEquipoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return new { message = "nombre es requerido." };
        if (request.Nombre.Length > 120)
            return new { message = "nombre max 120 caracteres." };
        if (request.CategoriaPredeterminadaId == Guid.Empty)
            return new { message = "categoriaPredeterminadaId es requerido." };
        if (request.DiaJuegoPredeterminado is < 1 or > 2)
            return new { message = "diaJuegoPredeterminado inválido. Use 1 o 2." };
        if (request.ProfesorTitularPredeterminadoId == Guid.Empty)
            return new { message = "profesorTitularPredeterminadoId es requerido." };
        if (request.ProfesorAuxiliarPredeterminadoId.HasValue &&
            request.ProfesorAuxiliarPredeterminadoId.Value == request.ProfesorTitularPredeterminadoId)
            return new { message = "Titular y auxiliar no pueden ser el mismo profesor." };
        return null;
    }

    private static object? ValidateUpdate(UpdateEquipoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return new { message = "nombre es requerido." };
        if (request.Nombre.Length > 120)
            return new { message = "nombre max 120 caracteres." };
        if (request.CategoriaPredeterminadaId == Guid.Empty)
            return new { message = "categoriaPredeterminadaId es requerido." };
        if (request.DiaJuegoPredeterminado is < 1 or > 2)
            return new { message = "diaJuegoPredeterminado inválido. Use 1 o 2." };
        if (request.ProfesorTitularPredeterminadoId == Guid.Empty)
            return new { message = "profesorTitularPredeterminadoId es requerido." };
        if (request.ProfesorAuxiliarPredeterminadoId.HasValue &&
            request.ProfesorAuxiliarPredeterminadoId.Value == request.ProfesorTitularPredeterminadoId)
            return new { message = "Titular y auxiliar no pueden ser el mismo profesor." };
        return null;
    }
}
