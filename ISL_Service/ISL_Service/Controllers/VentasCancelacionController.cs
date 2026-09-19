using ISL_Service.Application.DTOs.VentasCancelacion;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Cancelar remisiones, con las reglas de Mac31 y llamando a sus mismos
/// procedimientos. Ver VentasCancelacionService para el porque de cada regla.
/// </summary>
[ApiController]
[Route("api/ventas/cancelacion")]
[Authorize]
public class VentasCancelacionController : PermisoControllerBase
{
    // El mismo permiso que en Mac31 abre btnCancelarVenta (PermissionService).
    private static readonly string[] PermisosCancelar = { "ventas.cancelar" };

    private readonly IVentasCancelacionService _service;

    public VentasCancelacionController(
        IVentasCancelacionService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    /// <summary>
    /// Dice, sin tocar nada, cuales de las remisiones marcadas se pueden
    /// cancelar y cual es el impedimento de las que no.
    /// </summary>
    [HttpPost("verificar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Verificar([FromBody] VentasCancelacionVerificarRequest? body, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosCancelar, ct);
        if (sinPermiso is not null) return sinPermiso;

        var data = await _service.VerificarAsync(body?.IdsVenta ?? new List<int>(), ct);
        return Ok(new { ok = true, message = "Verificación lista.", data });
    }

    /// <summary>
    /// Cancela. Vuelve a validar todo antes de escribir, y si alguna de las
    /// marcadas ya no se puede cancelar no cancela NINGUNA — igual que Mac31.
    /// </summary>
    /// <response code="200">Se ejecuto la cancelacion; cada folio trae su resultado</response>
    /// <response code="400">No se mando ninguna remision</response>
    /// <response code="409">Algo impide cancelar, o la contrasena de supervisor no es la buena</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancelar([FromBody] VentasCancelacionRequest? body, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosCancelar, ct);
        if (sinPermiso is not null) return sinPermiso;

        if (body is null)
            return BadRequest(new { ok = false, message = "Body requerido." });

        /*
          Quien cancela y desde donde salen del TOKEN, nunca del cuerpo. Es la
          firma que queda escrita en la venta: si viajara en el body, cualquiera
          podria cancelar a nombre de otro.
        */
        var data = await _service.CancelarAsync(body, IdUsuarioLegacy(), Equipo(), ct);

        var mensaje = data.Fallidas == 0
            ? "Cancelación realizada."
            : "La cancelación terminó con avisos.";

        return Ok(new { ok = true, message = mensaje, data });
    }
}
