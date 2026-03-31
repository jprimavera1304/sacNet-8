using System.Security.Claims;
using ISL_Service.Application.DTOs.GeneracionRolTorneo;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/generacion-rol-torneo")]
[Authorize]
public class GeneracionRolTorneoController : ControllerBase
{
    private readonly IGeneracionRolTorneoService _service;

    public GeneracionRolTorneoController(IGeneracionRolTorneoService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Policy = "perm:generacionroltorneo.ver")]
    [ProducesResponseType(typeof(List<GeneracionRolTorneoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? texto,
        [FromQuery] Guid? torneoId,
        [FromQuery] Guid? jornadaId,
        [FromQuery] DateTime? fechaJuego,
        [FromQuery] byte? diaJuego,
        [FromQuery] byte? estado,
        CancellationToken ct)
    {
        var list = await _service.ConsultarAsync(texto, torneoId, jornadaId, fechaJuego, diaJuego, estado, ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:generacionroltorneo.ver")]
    [ProducesResponseType(typeof(GeneracionRolTorneoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var item = await _service.ObtenerAsync(id, ct);
        if (item is null)
            return NotFound(new { message = "La generación no existe." });
        return Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = "perm:generacionroltorneo.crear")]
    [ProducesResponseType(typeof(GeneracionRolTorneoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateGeneracionRolTorneoRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var validation = ValidateCreate(request);
        if (validation is not null)
            return BadRequest(validation);

        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token invalido." });

        var created = await _service.CrearAsync(new CreateGeneracionRolTorneoRequest
        {
            TorneoId = request.TorneoId,
            JornadaId = request.JornadaId,
            FechaJuego = request.FechaJuego,
            DiaJuego = request.DiaJuego,
            HoraInicio = request.HoraInicio,
            DuracionPartidoMin = request.DuracionPartidoMin,
            MinutosEntrePartidos = request.MinutosEntrePartidos,
            NumeroCanchas = request.NumeroCanchas,
            Observaciones = request.Observaciones
        }, userId, ct);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:generacionroltorneo.editar")]
    [ProducesResponseType(typeof(GeneracionRolTorneoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateGeneracionRolTorneoRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var validation = ValidateUpdate(request);
        if (validation is not null)
            return BadRequest(validation);

        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token invalido." });

        var updated = await _service.ActualizarAsync(id, new UpdateGeneracionRolTorneoRequest
        {
            JornadaId = request.JornadaId,
            FechaJuego = request.FechaJuego,
            DiaJuego = request.DiaJuego,
            HoraInicio = request.HoraInicio,
            DuracionPartidoMin = request.DuracionPartidoMin,
            MinutosEntrePartidos = request.MinutosEntrePartidos,
            NumeroCanchas = request.NumeroCanchas,
            Observaciones = request.Observaciones
        }, userId, ct);

        return Ok(updated);
    }

    [HttpPost("{id:guid}/cancelar")]
    [Authorize(Policy = "perm:generacionroltorneo.activar")]
    [ProducesResponseType(typeof(GeneracionRolTorneoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancelar(Guid id, [FromBody] CancelarGeneracionRolTorneoRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token invalido." });

        var motivo = request.Motivo?.Trim();
        if (string.IsNullOrWhiteSpace(motivo))
            return BadRequest(new { message = "motivo es requerido." });
        if (motivo.Length > 200)
            return BadRequest(new { message = "motivo max 200 caracteres." });

        var canceled = await _service.CancelarAsync(id, motivo, userId, ct);
        return Ok(canceled);
    }

    [HttpPost("{id:guid}/equipos/cargar")]
    [Authorize(Policy = "perm:generacionroltorneo.editar")]
    [ProducesResponseType(typeof(List<GeneracionRolEquipoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CargarEquipos(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token invalido." });

        var equipos = await _service.CargarEquiposAsync(id, userId, ct);
        return Ok(equipos);
    }

    [HttpGet("{id:guid}/equipos")]
    [Authorize(Policy = "perm:generacionroltorneo.ver")]
    [ProducesResponseType(typeof(List<GeneracionRolEquipoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarEquipos(Guid id, CancellationToken ct)
    {
        var equipos = await _service.ConsultarEquiposAsync(id, ct);
        return Ok(equipos);
    }

    [HttpPut("equipos/{id:guid}")]
    [Authorize(Policy = "perm:generacionroltorneo.editar")]
    [ProducesResponseType(typeof(GeneracionRolEquipoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarParticipacion(Guid id, [FromBody] UpdateParticipacionEquipoRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var validation = ValidateParticipacion(request);
        if (validation is not null)
            return BadRequest(validation);

        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token invalido." });

        var updated = await _service.ActualizarParticipacionEquipoAsync(id, request, userId, ct);
        return Ok(updated);
    }

    [HttpPost("{id:guid}/generar-partidos")]
    [Authorize(Policy = "perm:generacionroltorneo.activar")]
    [ProducesResponseType(typeof(GenerarPartidosGeneracionRolTorneoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GenerarPartidos(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token invalido." });

        var response = await _service.GenerarPartidosAsync(id, userId, ct);
        return Ok(response);
    }

    [HttpGet("{id:guid}/partidos")]
    [Authorize(Policy = "perm:generacionroltorneo.ver")]
    [ProducesResponseType(typeof(List<PartidoGeneracionRolTorneoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarPartidos(Guid id, CancellationToken ct)
    {
        var partidos = await _service.ConsultarPartidosAsync(id, ct);
        return Ok(partidos);
    }

    private bool TryGetUserId(out Guid userId)
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }

    private static object? ValidateCreate(CreateGeneracionRolTorneoRequest request)
    {
        if (request.TorneoId == Guid.Empty)
            return new { message = "torneoId es requerido." };
        if (request.JornadaId == Guid.Empty)
            return new { message = "jornadaId es requerido." };
        if (request.FechaJuego == default)
            return new { message = "fechaJuego es requerido." };
        if (request.DiaJuego is not (1 or 2))
            return new { message = "diaJuego debe ser 1 o 2." };
        if (request.DuracionPartidoMin is not null && request.DuracionPartidoMin <= 0)
            return new { message = "duracionPartidoMin debe ser mayor a cero." };
        if (request.MinutosEntrePartidos is not null && request.MinutosEntrePartidos < 0)
            return new { message = "minutosEntrePartidos no puede ser negativo." };
        if (request.NumeroCanchas is not null && request.NumeroCanchas <= 0)
            return new { message = "numeroCanchas debe ser mayor a cero." };
        if (request.Observaciones is not null && request.Observaciones.Length > 500)
            return new { message = "observaciones max 500 caracteres." };
        return null;
    }

    private static object? ValidateUpdate(UpdateGeneracionRolTorneoRequest request)
    {
        if (request.JornadaId == Guid.Empty)
            return new { message = "jornadaId es requerido." };
        if (request.FechaJuego == default)
            return new { message = "fechaJuego es requerido." };
        if (request.DiaJuego is not (1 or 2))
            return new { message = "diaJuego debe ser 1 o 2." };
        if (request.DuracionPartidoMin <= 0)
            return new { message = "duracionPartidoMin debe ser mayor a cero." };
        if (request.MinutosEntrePartidos < 0)
            return new { message = "minutosEntrePartidos no puede ser negativo." };
        if (request.NumeroCanchas <= 0)
            return new { message = "numeroCanchas debe ser mayor a cero." };
        if (request.Observaciones is not null && request.Observaciones.Length > 500)
            return new { message = "observaciones max 500 caracteres." };
        return null;
    }

    private static object? ValidateParticipacion(UpdateParticipacionEquipoRequest request)
    {
        if (request.Observaciones is not null && request.Observaciones.Length > 200)
            return new { message = "observaciones max 200 caracteres." };
        return null;
    }
}
