using ISL_Service.Application.DTOs.VentasPagos;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Consultar los pagos de una remision y cancelar uno, con las reglas de Mac31
/// (Forms/Ventas/ConsultarVentasPagos.cs) y llamando a sus mismos
/// procedimientos. El porque de cada regla esta en VentasPagosService.
/// </summary>
[ApiController]
[Route("api/ventas/pagos")]
[Authorize]
public class VentasPagosController : PermisoControllerBase
{
    /*
      LOS PERMISOS SON LOS DE MAC31, NO INVENTADOS.

      "ventas.pagos.ver" es btnPagos de la forma CONSULTA DE VENTAS: es el que
      deja ABRIR esta pantalla, y ya estaba mapeado en PermissionService.

      "ventas.pagos.cancelar" es btnCancelar de la forma CONSULTA DE VENTAS
      PAGOS (IDForma 9, IDProceso 1033). Es un permiso SEPARADO a proposito, y
      asi es en Mac31: entrar a ver el movimiento de una cuenta y deshacer un
      cobro son dos cosas distintas, y hay gente que tiene la primera sin la
      segunda. Los IDProceso son los MISMOS numeros en Tauro y en Zaragoza
      (1032 y 1033), asi que el puente vale igual en las dos.
    */
    private static readonly string[] PermisosVer = { "ventas.pagos.ver" };
    private static readonly string[] PermisosCancelar = { "ventas.pagos.cancelar" };

    private readonly IVentasPagosService _service;

    public VentasPagosController(
        IVentasPagosService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    /// <summary>
    /// La pantalla completa de una remision: cabecera, movimiento y totales.
    /// </summary>
    [HttpGet("{idVenta:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Consultar(int idVenta, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso is not null) return sinPermiso;

        if (idVenta <= 0)
            return BadRequest(new { ok = false, message = "Selecciona una remisión." });

        var data = await _service.ConsultarAsync(idVenta, ct);
        return Ok(new { ok = true, message = "Pagos consultados.", data });
    }

    /// <summary>
    /// Cancela UN pago. Vuelve a validar contra la base antes de escribir.
    /// </summary>
    /// <response code="200">Se intento; `data.cancelado` dice si se logro y
    /// `data.mensaje` por que no. Viene ademas la pantalla recargada.</response>
    [HttpPost("cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Cancelar([FromBody] VentasPagoCancelarRequest? body, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosCancelar, ct);
        if (sinPermiso is not null) return sinPermiso;

        if (body is null || body.IdVenta <= 0 || body.IdPagoVenta <= 0)
            return BadRequest(new { ok = false, message = "Selecciona el pago a cancelar." });

        /*
          Quien cancela y desde donde salen del TOKEN, nunca del cuerpo. Es la
          firma que queda escrita en [Ventas Pagos].IDUsuarioCancelo y
          .EquipoCancelo: si viajara en el body, cualquiera podria cancelar un
          cobro a nombre de otro.
        */
        var data = await _service.CancelarPagoAsync(body, IdUsuarioLegacy(), Equipo(), ct);

        return Ok(new { ok = true, message = data.Mensaje, data });
    }
}
