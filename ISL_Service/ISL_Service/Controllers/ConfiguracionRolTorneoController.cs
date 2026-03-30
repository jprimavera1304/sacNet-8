using System.Security.Claims;
using ISL_Service.Application.DTOs.ConfiguracionRolTorneo;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/configuracion-rol-torneo")]
[Authorize]
public class ConfiguracionRolTorneoController : ControllerBase
{
    private readonly IConfiguracionRolTorneoService _service;

    public ConfiguracionRolTorneoController(IConfiguracionRolTorneoService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Policy = "perm:configuracionroltorneo.ver")]
    [ProducesResponseType(typeof(List<ConfiguracionRolTorneoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? texto,
        [FromQuery] Guid? temporadaId,
        [FromQuery] byte? estado,
        CancellationToken ct)
    {
        var list = await _service.ConsultarAsync(texto, temporadaId, estado, ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:configuracionroltorneo.ver")]
    [ProducesResponseType(typeof(ConfiguracionRolTorneoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var item = await _service.GetByIdAsync(id, ct);
        if (item is null)
            return NotFound(new { message = "La configuración no existe." });
        return Ok(item);
    }

    [HttpGet("torneo/{torneoId:guid}")]
    [Authorize(Policy = "perm:configuracionroltorneo.ver")]
    [ProducesResponseType(typeof(ConfiguracionRolTorneoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActivaByTorneo(Guid torneoId, CancellationToken ct)
    {
        var item = await _service.ObtenerActivaPorTorneoAsync(torneoId, ct);
        if (item is null)
            return NotFound(new { message = "No hay configuración activa para el torneo." });
        return Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = "perm:configuracionroltorneo.crear")]
    [ProducesResponseType(typeof(ConfiguracionRolTorneoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateConfiguracionRolTorneoRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var validation = ValidateCreate(request);
        if (validation is not null)
            return BadRequest(validation);

        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token invalido." });

        var created = await _service.CrearAsync(new CreateConfiguracionRolTorneoRequest
        {
            TorneoId = request.TorneoId,
            HoraInicioPredeterminada = request.HoraInicioPredeterminada,
            DuracionPartidoMin = request.DuracionPartidoMin,
            MinutosEntrePartidos = request.MinutosEntrePartidos,
            NumeroCanchas = request.NumeroCanchas,
            ObservacionesPredeterminadas = request.ObservacionesPredeterminadas?.Trim()
        }, userId, ct);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:configuracionroltorneo.editar")]
    [ProducesResponseType(typeof(ConfiguracionRolTorneoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateConfiguracionRolTorneoRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var validation = ValidateUpdate(request);
        if (validation is not null)
            return BadRequest(validation);

        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token invalido." });

        var updated = await _service.ActualizarAsync(id, new UpdateConfiguracionRolTorneoRequest
        {
            HoraInicioPredeterminada = request.HoraInicioPredeterminada,
            DuracionPartidoMin = request.DuracionPartidoMin,
            MinutosEntrePartidos = request.MinutosEntrePartidos,
            NumeroCanchas = request.NumeroCanchas,
            ObservacionesPredeterminadas = request.ObservacionesPredeterminadas?.Trim()
        }, userId, ct);

        return Ok(updated);
    }

    [HttpPost("{id:guid}/inhabilitar")]
    [Authorize(Policy = "perm:configuracionroltorneo.activar")]
    [ProducesResponseType(typeof(ConfiguracionRolTorneoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Inhabilitar(Guid id, [FromBody] InhabilitarConfiguracionRolTorneoRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var motivo = request.Motivo?.Trim();
        if (string.IsNullOrWhiteSpace(motivo))
            return BadRequest(new { message = "motivo es requerido." });
        if (motivo.Length > 200)
            return BadRequest(new { message = "motivo max 200 caracteres." });

        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token invalido." });

        var disabled = await _service.InhabilitarAsync(id, motivo, userId, ct);
        return Ok(disabled);
    }

    private bool TryGetUserId(out Guid userId)
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }

    private static object? ValidateCreate(CreateConfiguracionRolTorneoRequest request)
    {
        if (request.TorneoId == Guid.Empty)
            return new { message = "torneoId es requerido." };
        if (!request.HoraInicioPredeterminada.HasValue)
            return new { message = "horaInicioPredeterminada es requerida." };
        if (!request.DuracionPartidoMin.HasValue || request.DuracionPartidoMin <= 0)
            return new { message = "duracionPartidoMin debe ser mayor a cero." };
        if (!request.MinutosEntrePartidos.HasValue || request.MinutosEntrePartidos < 0)
            return new { message = "minutosEntrePartidos no puede ser negativo." };
        if (!request.NumeroCanchas.HasValue || request.NumeroCanchas <= 0)
            return new { message = "numeroCanchas debe ser mayor a cero." };
        if (!string.IsNullOrWhiteSpace(request.ObservacionesPredeterminadas) && request.ObservacionesPredeterminadas.Length > 500)
            return new { message = "observacionesPredeterminadas max 500 caracteres." };
        return null;
    }

    private static object? ValidateUpdate(UpdateConfiguracionRolTorneoRequest request)
    {
        if (!request.HoraInicioPredeterminada.HasValue)
            return new { message = "horaInicioPredeterminada es requerida." };
        if (!request.DuracionPartidoMin.HasValue || request.DuracionPartidoMin <= 0)
            return new { message = "duracionPartidoMin debe ser mayor a cero." };
        if (!request.MinutosEntrePartidos.HasValue || request.MinutosEntrePartidos < 0)
            return new { message = "minutosEntrePartidos no puede ser negativo." };
        if (!request.NumeroCanchas.HasValue || request.NumeroCanchas <= 0)
            return new { message = "numeroCanchas debe ser mayor a cero." };
        if (!string.IsNullOrWhiteSpace(request.ObservacionesPredeterminadas) && request.ObservacionesPredeterminadas.Length > 500)
            return new { message = "observacionesPredeterminadas max 500 caracteres." };
        return null;
    }
}
