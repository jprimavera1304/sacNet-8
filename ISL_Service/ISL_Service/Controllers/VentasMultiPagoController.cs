using ISL_Service.Application.DTOs.VentasMultiPago;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Multi pago: cobrar varias remisiones de un golpe, con las reglas de Mac31
/// (Forms/Ventas/ConsultarVentas.cs btnMultiPago_Click, linea 4017, y
/// Forms/Ventas/VentasPago.cs). El porque de cada regla esta en
/// VentasMultiPagoService.
/// </summary>
[ApiController]
[Route("api/ventas/multipago")]
[Authorize]
public class VentasMultiPagoController : PermisoControllerBase
{
    /*
      EL PERMISO ES EL DE MAC31, Y YA EXISTIA.

      "ventas.pagos.multi" es btnMultiPago de la forma ConsultarVentas
      (IDForma 1, IDProceso 5196), y ya estaba mapeado en
      PermissionService.cs, linea 43. Se comprobo que el IDProceso es el MISMO
      numero en Tauro y en Zaragoza (5196 en las dos), asi que el puente de
      permisos heredados vale igual en las dos empresas y no hay nada nuevo que
      sembrar.

      Es un permiso distinto de "ventas.pagos.ver" (btnPagos): mirar el
      movimiento de una remision y cobrarla son dos cosas, y en Mac31 tambien
      son dos botones con su propio renglon en n_Procesos.
    */
    private static readonly string[] PermisosMultiPago = { "ventas.pagos.multi" };

    private readonly IVentasMultiPagoService _service;

    public VentasMultiPagoController(
        IVentasMultiPagoService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    /// <summary>
    /// Todo lo que la pantalla necesita: las remisiones con sus saldos, las
    /// formas de pago que aplican a ESTA empresa y a ESTA seleccion, los
    /// catalogos y los topes.
    /// </summary>
    /// <remarks>
    /// Es POST y no GET porque la entrada es una lista de ids que puede ser
    /// larga: metida en la direccion, una seleccion grande se pasaria del
    /// limite de la URL y el fallo se veria como un 414 sin explicacion.
    /// </remarks>
    [HttpPost("pantalla")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Pantalla([FromBody] VentasMultiPagoPantallaRequest? body, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosMultiPago, ct);
        if (sinPermiso is not null) return sinPermiso;

        if (body is null || body.IdsVenta is null || body.IdsVenta.Count == 0)
            return BadRequest(new { ok = false, message = "Selecciona una remisión." });

        var data = await _service.PantallaAsync(body, ct);
        return Ok(new { ok = true, message = "", data });
    }

    /// <summary>
    /// Aplica el pago. Vuelve a leer y a validar TODO contra la base antes de
    /// escribir: entre que se pinto la pantalla y que se pulso Guardar, la
    /// remision pudo cancelarse o cobrarla otro cajero.
    /// </summary>
    /// <response code="200">
    /// `data.guardo` dice si se aplico. Cuando `data.requiereConfirmacion`
    /// viene lleno NO es un error: es una de las preguntas que Mac31 hace con
    /// un si/no (pagar de mas, el cargo, el saldo a favor, la clave del dia).
    /// El front pregunta y reintenta con la confirmacion puesta.
    /// </response>
    [HttpPost("guardar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Guardar([FromBody] VentasMultiPagoGuardarRequest? body, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosMultiPago, ct);
        if (sinPermiso is not null) return sinPermiso;

        if (body is null || body.IdsVenta is null || body.IdsVenta.Count == 0)
            return BadRequest(new { ok = false, message = "Selecciona una remisión." });

        /*
          QUIEN COBRA Y DESDE DONDE SALEN DEL TOKEN, NUNCA DEL CUERPO.

          Es la firma que queda escrita en [Ventas Pagos]: IDUsuario y Equipo.
          Si viajaran en el body, cualquiera podria registrar un cobro a nombre
          de otro cajero — y en una pantalla de dinero eso no es un detalle.
        */
        var data = await _service.GuardarAsync(body, IdUsuarioLegacy(), Equipo(), ct);

        return Ok(new { ok = true, message = data.Mensaje, data });
    }
}
