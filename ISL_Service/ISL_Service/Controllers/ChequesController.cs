using System.Security.Claims;
using ISL_Service.Application.DTOs.Cheques;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Backend.Core.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/cheques")]
[Authorize]
public class ChequesController : ControllerBase
{
    
    private readonly IChequesService _service;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public ChequesController(IChequesService service, ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet]
    [Authorize(Policy = "perm:cheques.ver")]
    [ProducesResponseType(typeof(List<ChequeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? texto,
        [FromQuery] int? idCliente,
        [FromQuery] int? idBanco,
        [FromQuery] byte? estatusCheque,
        [FromQuery] byte? idStatus,
        [FromQuery] DateTime? fechaChequeInicio,
        [FromQuery] DateTime? fechaChequeFin,
        CancellationToken ct)
    {
        var list = await _service.ConsultarAsync(
            texto,
            idCliente,
            idBanco,
            estatusCheque,
            idStatus,
            fechaChequeInicio,
            fechaChequeFin,
            ct);

        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:cheques.ver")]
    [ProducesResponseType(typeof(ChequeDetalleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var item = await _service.GetByIdAsync(id, ct);
        if (item is null)
            return NotFound(new { message = "El cheque no existe." });
        return Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = "perm:cheques.crear")]
    [ProducesResponseType(typeof(ChequeDetalleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateChequeRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var validation = ValidateCreate(request);
        if (validation is not null)
            return BadRequest(validation);

        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token invÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡lido." });

        var created = await _service.CrearAsync(new CreateChequeRequest
        {
            IDCliente = request.IDCliente,
            IDBanco = request.IDBanco,
            NumeroCheque = request.NumeroCheque.Trim(),
            Monto = request.Monto,
            FechaCheque = request.FechaCheque.Date,
            Observaciones = request.Observaciones?.Trim(),
            ResponsableCobroId = request.ResponsableCobroId
        }, userId, ct);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:cheques.editar")]
    [ProducesResponseType(typeof(ChequeDetalleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateChequeRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var validation = ValidateUpdate(request);
        if (validation is not null)
            return BadRequest(validation);

        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token invÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡lido." });

        var updated = await _service.ActualizarAsync(id, new UpdateChequeRequest
        {
            IDCliente = request.IDCliente,
            IDBanco = request.IDBanco,
            NumeroCheque = request.NumeroCheque.Trim(),
            Monto = request.Monto,
            FechaCheque = request.FechaCheque.Date,
            Observaciones = request.Observaciones?.Trim(),
            ResponsableCobroId = request.ResponsableCobroId
        }, userId, ct);

        return Ok(updated);
    }

    [HttpPost("{id:guid}/estatus")]
    [Authorize(Policy = "perm:cheques.activar")]
    [ProducesResponseType(typeof(ChequeDetalleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CambiarEstatus(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CambiarEstatusChequeRequest? request,
        CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var validation = ValidateStatus(request);
        if (validation is not null)
            return BadRequest(validation);

        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Token invÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡lido." });

        var changed = await _service.CambiarEstatusAsync(
            id,
            request.EstatusChequeNuevo,
            request.Motivo?.Trim(),
            request.Observaciones?.Trim(),
            request.FechaMovimiento,
            userId,
            ct);

        return Ok(changed);
    }

    private bool TryGetUserId(out Guid userId)
    {
        var value = _currentUserAccessor.GetUserId(User);
        userId = value ?? Guid.Empty;
        return value.HasValue;
    }

    private static object? ValidateCreate(CreateChequeRequest request)
    {
        if (request.IDCliente <= 0)
            return new { message = "idCliente es requerido." };
        if (request.IDBanco <= 0)
            return new { message = "idBanco es requerido." };
        if (string.IsNullOrWhiteSpace(request.NumeroCheque))
            return new { message = "numeroCheque es requerido." };
        if (request.NumeroCheque.Length > 50)
            return new { message = "numeroCheque max 50 caracteres." };
        if (request.Monto <= 0)
            return new { message = "monto debe ser mayor a 0." };
        if (request.FechaCheque == default)
            return new { message = "fechaCheque es requerida." };
        if (!string.IsNullOrWhiteSpace(request.Observaciones) && request.Observaciones.Length > 500)
            return new { message = "observaciones max 500 caracteres." };
        if (request.ResponsableCobroId.HasValue && request.ResponsableCobroId.Value == Guid.Empty)
            return new { message = "responsableCobroId invÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡lido." };
        return null;
    }

    private static object? ValidateUpdate(UpdateChequeRequest request)
    {
        if (request.IDCliente <= 0)
            return new { message = "idCliente es requerido." };
        if (request.IDBanco <= 0)
            return new { message = "idBanco es requerido." };
        if (string.IsNullOrWhiteSpace(request.NumeroCheque))
            return new { message = "numeroCheque es requerido." };
        if (request.NumeroCheque.Length > 50)
            return new { message = "numeroCheque max 50 caracteres." };
        if (request.Monto <= 0)
            return new { message = "monto debe ser mayor a 0." };
        if (request.FechaCheque == default)
            return new { message = "fechaCheque es requerida." };
        if (!string.IsNullOrWhiteSpace(request.Observaciones) && request.Observaciones.Length > 500)
            return new { message = "observaciones max 500 caracteres." };
        if (request.ResponsableCobroId.HasValue && request.ResponsableCobroId.Value == Guid.Empty)
            return new { message = "responsableCobroId invÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡lido." };
        return null;
    }

    private static object? ValidateStatus(CambiarEstatusChequeRequest request)
    {
        if (request.EstatusChequeNuevo is < 2 or > 4)
            return new { message = "estatusChequeNuevo invÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡lido. Use 2, 3 o 4." };
        if ((request.EstatusChequeNuevo == 3 || request.EstatusChequeNuevo == 4) && string.IsNullOrWhiteSpace(request.Motivo))
            return new { message = "motivo es requerido para estatus devuelto o cancelado." };
        if (!string.IsNullOrWhiteSpace(request.Motivo) && request.Motivo.Length > 300)
            return new { message = "motivo max 300 caracteres." };
        if (!string.IsNullOrWhiteSpace(request.Observaciones) && request.Observaciones.Length > 500)
            return new { message = "observaciones max 500 caracteres." };
        return null;
    }
}
