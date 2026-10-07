using System.Security.Claims;
using ISL_Service.Application.DTOs.Temporadas;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISL_Service.Application.Security;

namespace ISL_Service.Controllers;

/// <summary>
/// Temporadas.
///
/// PERMISOS
/// --------
/// Las cuatro escrituras llevan ahora permiso propio: antes bastaba con que el
/// token trajera un userId, o sea cualquier usuario de cualquier rol podia
/// crear, modificar, cancelar y reactivar temporadas.
///
///   crear    -> POST api/temporadas
///   editar   -> PUT  api/temporadas/{id}
///   cancelar -> POST api/temporadas/{id}/cancelar, .../reactivar,
///               POST api/temporadas/cerrar-vencidas
///
/// Las claves (temporadas.*) son las que ya pide el front en
/// shared/js/core/module-action-flags.js.
/// </summary>
[ApiController]
[Route("api/temporadas")]
[Authorize]
public class TemporadasController : PermisoControllerBase
{
    /// <summary>Permiso para cerrar/cancelar/reactivar temporadas.</summary>
    private static readonly string[] PermisoCerrar = { "temporadas.cancelar" };

    private readonly ITemporadasService _service;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public TemporadasController(
        ITemporadasService service,
        ICurrentUserAccessor currentUserAccessor,
        IPermissionService permissionService)
        : base(currentUserAccessor, permissionService)
    {
        _service = service;
        _currentUserAccessor = currentUserAccessor;
    }

    /// <summary>
    /// Lista temporadas.
    /// </summary>
    /// <remarks>
    /// ESTE GET PUEDE CERRAR TEMPORADAS VENCIDAS, Y HAY QUE SABERLO.
    /// -----------------------------------------------------------
    /// Antes lo hacia SIEMPRE y sin decirlo: el listado ejecutaba
    /// sp_w_CerrarTemporadasVencidas antes de leer, asi que abrir la pantalla
    /// cambiaba datos. En un GET eso sorprende a cualquiera —un reintento, un
    /// prefetch del navegador o un monitor de salud se volvian escrituras— y
    /// ademas lo disparaba quien solo tenia permiso de mirar.
    ///
    /// No se puede simplemente quitar: es LO UNICO que cierra temporadas
    /// vencidas (el front no llama a ningun endpoint de cierre), asi que
    /// quitarlo dejaria las vencidas en "activa" para siempre.
    ///
    /// Lo que se hizo:
    ///   1. El efecto solo corre si el usuario puede cerrar
    ///      (temporadas.cancelar). Quien solo lee recibe una lectura limpia.
    ///   2. El cierre tiene ya puerta propia y explicita:
    ///      POST api/temporadas/cerrar-vencidas.
    ///   3. La respuesta lo declara en la cabecera X-Efecto-Lateral cuando
    ///      ocurre, para que no haya que leer el codigo para enterarse.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(List<TemporadaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] byte? estado, [FromQuery] string? texto, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token inválido." });

        if (!await TienePermisoAsync(PermisoCerrar, ct))
            return Ok(await _service.ConsultarTemporadasAsync(estado, texto, ct));

        Response.Headers["X-Efecto-Lateral"] = "cerrar-temporadas-vencidas";
        var list = await _service.ConsultarTemporadasListadoAsync(estado, texto, userId, ct);
        return Ok(list);
    }

    /// <summary>
    /// Cierra las temporadas vencidas. La puerta explicita del efecto que el
    /// listado hacia por su cuenta.
    /// </summary>
    [HttpPost("cerrar-vencidas")]
    [Authorize(Policy = "perm:temporadas.cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CerrarVencidas(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token inválido." });

        var cerradas = await _service.CerrarTemporadasVencidasAsync(userId, ct);
        return Ok(new { temporadasCerradas = cerradas });
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
    [Authorize(Policy = "perm:temporadas.crear")]
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
            return Unauthorized(new { message = "Token inválido." });

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
    [Authorize(Policy = "perm:temporadas.editar")]
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
            return Unauthorized(new { message = "Token inválido." });

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
    [Authorize(Policy = "perm:temporadas.cancelar")]
    [ProducesResponseType(typeof(TemporadaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelTemporadaRequest? request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token inválido." });

        var motivo = request?.Motivo?.Trim();
        if (motivo is { Length: > 200 })
            return BadRequest(new { message = "motivo max 200 caracteres." });

        var canceled = await _service.CancelarTemporadaAsync(id, motivo, userId, ct);
        return Ok(canceled);
    }

    [HttpPost("{id:guid}/reactivar")]
    [Authorize(Policy = "perm:temporadas.cancelar")]
    [ProducesResponseType(typeof(TemporadaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reactivar(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token inválido." });

        var updated = await _service.ReactivarTemporadaAsync(id, userId, ct);
        return Ok(updated);
    }

    private bool TryGetUserId(out Guid userId)
    {
        var value = _currentUserAccessor.GetUserId(User);
        userId = value ?? Guid.Empty;
        return value.HasValue;
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
