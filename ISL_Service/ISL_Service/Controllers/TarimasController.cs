using System.Security.Claims;
using ISL_Service.Application.DTOs.Tarima;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// API de Tarimas (dbo.WTarima). Estatus: 1 = Activo, 2 = Cancelado.
/// </summary>
[ApiController]
[Route("api/tarimas")]
[Authorize]
public class TarimasController : ControllerBase
{
    private readonly ITarimaService _service;

    public TarimasController(ITarimaService service)
    {
        _service = service;
    }

    /// <summary>
    /// Lista tarimas con filtros opcionales por estatus y busqueda por nombre.
    /// </summary>
    /// <param name="idStatus">NULL = todas, 1 = Activo, 2 = Cancelado/Inactivo</param>
    /// <param name="estatus">Alternativa a idStatus: "activo"=1, "inactivo" o "cancelado"=2, "todos"=todas. Si se envía idStatus tiene prioridad.</param>
    /// <param name="busqueda">Filtro por nombre (opcional)</param>
    /// <returns>Lista de TarimaDto</returns>
    /// <response code="200">Lista obtenida correctamente</response>
    /// <response code="401">No autenticado</response>
    /// <response code="403">Sin permiso tarimas.ver</response>
    [HttpGet]
    [Authorize(Policy = "perm:tarimas.ver")]
    [ProducesResponseType(typeof(List<TarimaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] int? idStatus, [FromQuery] string? estatus, [FromQuery] string? busqueda, CancellationToken ct)
    {
        var statusFilter = ResolveStatusFilter(idStatus, estatus);
        var list = await _service.ConsultarTarimasAsync(statusFilter, busqueda, ct);
        return Ok(list);
    }

    /// <summary>Mapea idStatus (prioridad) o estatus (activo/inactivo/cancelado/todos) a 1, 2 o null.</summary>
    private static int? ResolveStatusFilter(int? idStatus, string? estatus)
    {
        if (idStatus.HasValue)
            return idStatus.Value;
        var e = estatus?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(e) || e == "todos") return null;
        if (e == "activo") return 1;
        if (e == "inactivo" || e == "cancelado") return 2;
        return null;
    }

    /// <summary>
    /// Obtiene una tarima por IdTarima.
    /// </summary>
    /// <param name="idTarima">Id de la tarima</param>
    /// <response code="200">Tarima encontrada</response>
    /// <response code="404">Tarima no encontrada</response>
    /// <response code="403">Sin permiso tarimas.ver</response>
    [HttpGet("{idTarima:int}")]
    [Authorize(Policy = "perm:tarimas.ver")]
    [ProducesResponseType(typeof(TarimaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int idTarima, CancellationToken ct)
    {
        var tarima = await _service.GetByIdAsync(idTarima, ct);
        if (tarima == null)
            return NotFound(new { message = "La tarima no existe." });
        return Ok(tarima);
    }

    /// <summary>
    /// Crea una tarima. Usuario se toma del token (no enviar en body).
    /// </summary>
    /// <response code="201">Tarima creada (devuelve IdTarima)</response>
    /// <response code="400">Validacion fallida</response>
    /// <response code="409">Ya existe una tarima activa con el mismo nombre y tipo de casco</response>
    /// <response code="403">Sin permiso tarimas.crear</response>
    [HttpPost]
    [Authorize(Policy = "perm:tarimas.crear")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateTarimaRequest request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { message = "Body requerido." });
        var trimmed = new CreateTarimaRequest
        {
            NombreTarima = request.NombreTarima?.Trim() ?? "",
            IdTipoCasco = request.IdTipoCasco,
            NumeroCascosBase = request.NumeroCascosBase,
            Observaciones = request.Observaciones?.Trim()
        };
        var validation = ValidateCreate(trimmed);
        if (validation != null)
            return BadRequest(validation);

        var usuario = User.FindFirstValue("username") ?? User.Identity?.Name ?? "Sistema";
        var id = await _service.CrearAsync(trimmed, usuario, ct);
        if (id <= 0)
            return StatusCode(500, new { message = "No se pudo crear la tarima." });
        return CreatedAtAction(nameof(GetById), new { idTarima = id }, new { idTarima = id });
    }

    /// <summary>
    /// Actualiza una tarima. Solo si esta activa (IdStatus = 1).
    /// </summary>
    /// <response code="200">Actualizada</response>
    /// <response code="400">Validacion fallida</response>
    /// <response code="404">Tarima no encontrada</response>
    /// <response code="409">Solo se puede actualizar una tarima activa / duplicado nombre+tipo casco</response>
    /// <response code="403">Sin permiso tarimas.editar</response>
    [HttpPut("{idTarima:int}")]
    [Authorize(Policy = "perm:tarimas.editar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int idTarima, [FromBody] UpdateTarimaRequest request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { message = "Body requerido." });
        var trimmed = new UpdateTarimaRequest
        {
            NombreTarima = request.NombreTarima?.Trim() ?? "",
            IdTipoCasco = request.IdTipoCasco,
            NumeroCascosBase = request.NumeroCascosBase,
            Observaciones = request.Observaciones?.Trim()
        };
        var validation = ValidateUpdate(trimmed);
        if (validation != null)
            return BadRequest(validation);

        var usuario = User.FindFirstValue("username") ?? User.Identity?.Name ?? "Sistema";
        await _service.ActualizarAsync(idTarima, trimmed, usuario, ct);
        return Ok(new { message = "Tarima actualizada." });
    }

    /// <summary>
    /// Cambia el estatus de una tarima: 1 = Activo, 2 = Cancelado.
    /// Si ya esta en el mismo estatus, responde 200 sin cambios.
    /// </summary>
    /// <response code="200">Estatus actualizado o ya estaba en ese estatus</response>
    /// <response code="400">idStatus no es 1 ni 2</response>
    /// <response code="404">Tarima no encontrada</response>
    /// <response code="403">Sin permiso tarimas.estado.editar</response>
    [HttpPatch("{idTarima:int}/status")]
    [Authorize(Policy = "perm:tarimas.estado.editar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(int idTarima, [FromBody] UpdateTarimaStatusRequest request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { message = "Body requerido. idStatus debe ser 1 (Activo) o 2 (Cancelado)." });
        var usuario = GetUsuarioFromToken();
        await _service.CambiarStatusAsync(idTarima, request.IdStatus, usuario, ct);
        return Ok(new { message = "Estatus actualizado." });
    }

    /// <summary>Obtiene el nombre de usuario del token (varios claims posibles). Nunca devuelve null/vacío: usa "Sistema" como fallback.</summary>
    private string GetUsuarioFromToken()
    {
        var u = User.FindFirstValue("username")
            ?? User.FindFirstValue("preferred_username")
            ?? User.FindFirstValue("name")
            ?? User.FindFirstValue("unique_name")
            ?? User.Identity?.Name;
        return string.IsNullOrWhiteSpace(u) ? "Sistema" : u.Trim();
    }

    private static object? ValidateCreate(CreateTarimaRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.NombreTarima)) return new { message = "nombreTarima es requerido." };
        if (r.NombreTarima.Length > 150) return new { message = "nombreTarima max 150 caracteres." };
        if (r.IdTipoCasco <= 0) return new { message = "idTipoCasco debe ser mayor a 0." };
        if (r.NumeroCascosBase < 1 || r.NumeroCascosBase > 99999) return new { message = "numeroCascosBase debe estar entre 1 y 99999." };
        if (r.Observaciones != null && r.Observaciones.Length > 500) return new { message = "observaciones max 500 caracteres." };
        return null;
    }

    private static object? ValidateUpdate(UpdateTarimaRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.NombreTarima)) return new { message = "nombreTarima es requerido." };
        if (r.NombreTarima.Length > 150) return new { message = "nombreTarima max 150 caracteres." };
        if (r.IdTipoCasco <= 0) return new { message = "idTipoCasco debe ser mayor a 0." };
        if (r.NumeroCascosBase < 1 || r.NumeroCascosBase > 99999) return new { message = "numeroCascosBase debe estar entre 1 y 99999." };
        if (r.Observaciones != null && r.Observaciones.Length > 500) return new { message = "observaciones max 500 caracteres." };
        return null;
    }
}
