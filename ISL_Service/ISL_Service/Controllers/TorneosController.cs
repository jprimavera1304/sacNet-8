using System.Security.Claims;
using ISL_Service.Application.DTOs.Temporadas;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISL_Service.Application.Security;

namespace ISL_Service.Controllers;

/// <summary>
/// Torneos.
///
/// PERMISOS
/// --------
/// Las siete escrituras llevan ahora permiso propio: antes solo comprobaban que
/// el token trajera un userId, asi que cualquier usuario autenticado podia
/// crear, modificar, cancelar, activar, cerrar y reactivar torneos — y lanzar el
/// cierre MASIVO de vencidos.
///
///   crear    -> POST api/torneos
///   editar   -> PUT  api/torneos/{id}
///   cancelar -> POST .../cancelar, .../activar, .../cerrar, .../reactivar,
///               POST api/torneos/cerrar-vencidos
///
/// Las claves (torneos.*) son las que ya pide el front en
/// shared/js/core/module-action-flags.js.
/// </summary>
[ApiController]
[Route("api/torneos")]
[Authorize]
public class TorneosController : PermisoControllerBase
{
    /// <summary>Permiso para cerrar/cancelar/activar/reactivar torneos.</summary>
    private static readonly string[] PermisoCerrar = { "torneos.cancelar" };

    private readonly ITemporadasService _service;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public TorneosController(
        ITemporadasService service,
        ICurrentUserAccessor currentUserAccessor,
        IPermissionService permissionService)
        : base(currentUserAccessor, permissionService)
    {
        _service = service;
        _currentUserAccessor = currentUserAccessor;
    }

    // LAS DOS LECTURAS TAMBIEN PIDEN PERMISO.
    //
    // Las escrituras de este controlador si lo pedian; las lecturas se queda-
    // ron con solo [Authorize], o sea que cualquiera con sesion las leia. No se
    // pudo medir en Zaragoza ni en Tauro -los SP de la liga no estan desplega-
    // dos alla y contestan 500-, pero en la base donde el modulo existe seria
    // una lista abierta. La clave ya esta en el catalogo y la exige la entrada
    // del modulo (EntradasDeModulo).
    [Authorize(Policy = "perm:torneos.ver")]
    [HttpGet]
    [ProducesResponseType(typeof(List<TorneoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List([FromQuery] Guid? temporadaId, [FromQuery] byte? estado, [FromQuery] string? texto, [FromQuery] DateTime? fechaInicio, [FromQuery] DateTime? fechaFin, CancellationToken ct)
    {
        if (fechaInicio.HasValue && fechaFin.HasValue && fechaFin < fechaInicio)
            return BadRequest(new { message = "fechaFin no puede ser menor que fechaInicio." });
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token inválido." });

        /*
          ESTE GET PUEDE CERRAR TORNEOS VENCIDOS.

          El listado ejecutaba sp_w_CerrarTorneosVencidos antes de leer, SIEMPRE
          y sin decirlo: abrir la pantalla cambiaba datos, y lo disparaba
          tambien quien solo tenia permiso de mirar.

          No se quita porque es lo unico que los cierra: el endpoint explicito
          (POST api/torneos/cerrar-vencidos) existe pero el front no lo llama
          nunca —comprobado: "cerrar-vencidos" no aparece en Web/QR_Web—, asi que
          quitarlo dejaria los vencidos en "activo" para siempre.

          Lo que se hizo: el efecto solo corre si el usuario PUEDE cerrar
          (torneos.cancelar); quien solo lee pasa por ConsultarTorneosAsync, que
          no escribe. Y cuando ocurre, la respuesta lo dice en la cabecera
          X-Efecto-Lateral.
        */
        if (!await TienePermisoAsync(PermisoCerrar, ct))
            return Ok(await _service.ConsultarTorneosAsync(temporadaId, estado, texto, fechaInicio, fechaFin, ct));

        Response.Headers["X-Efecto-Lateral"] = "cerrar-torneos-vencidos";
        var list = await _service.ConsultarTorneosListadoAsync(temporadaId, estado, texto, fechaInicio, fechaFin, userId, ct);
        return Ok(list);
    }

    [Authorize(Policy = "perm:torneos.ver")]
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
    [Authorize(Policy = "perm:torneos.crear")]
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
            return Unauthorized(new { message = "Token inválido." });

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
    [Authorize(Policy = "perm:torneos.editar")]
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
            return Unauthorized(new { message = "Token inválido." });

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
    [Authorize(Policy = "perm:torneos.cancelar")]
    [ProducesResponseType(typeof(TorneoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelTorneoRequest? request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token inválido." });

        var motivo = request?.Motivo?.Trim();
        if (motivo is { Length: > 200 })
            return BadRequest(new { message = "motivo max 200 caracteres." });

        var canceled = await _service.CancelarTorneoAsync(id, motivo, userId, ct);
        return Ok(canceled);
    }

    [HttpPost("{id:guid}/activar")]
    [Authorize(Policy = "perm:torneos.cancelar")]
    [ProducesResponseType(typeof(TorneoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Activar(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token inválido." });

        var activated = await _service.ActivarTorneoAsync(id, userId, ct);
        return Ok(activated);
    }

    [HttpPost("{id:guid}/cerrar")]
    [Authorize(Policy = "perm:torneos.cancelar")]
    [ProducesResponseType(typeof(TorneoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cerrar(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token inválido." });

        var closed = await _service.CerrarTorneoAsync(id, userId, ct);
        return Ok(closed);
    }

    [HttpPost("{id:guid}/reactivar")]
    [Authorize(Policy = "perm:torneos.cancelar")]
    [ProducesResponseType(typeof(TorneoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reactivar(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token inválido." });

        var reactivated = await _service.ReactivarTorneoAsync(id, userId, ct);
        return Ok(reactivated);
    }

    [HttpPost("cerrar-vencidos")]
    [Authorize(Policy = "perm:torneos.cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CerrarVencidos([FromQuery] DateTime? fechaCorte, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token inválido." });

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
