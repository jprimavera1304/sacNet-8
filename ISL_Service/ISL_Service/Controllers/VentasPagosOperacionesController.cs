using ISL_Service.Application.DTOs.VentasPagosOperaciones;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/*
  LO QUE QUEDABA DE PAGOS DE VENTA

  En legacy, estos trece endpoints viven en un controlador con
  [AllowAnonymous] en la clase. Conviene decir con precision que significaba:
  NO que estuvieran abiertos —el handler global sigue exigiendo la cabecera
  Authorization y el servicio valida el token del cuerpo—, sino que la
  autorizacion no estaba declarada en ningun lado y habia que leer el servicio
  para saber que pedia. Aqui lleva [Authorize] y el permiso esta en el metodo.

  LA FAMILIA ES `cobros.*`, NO `ventas.pagos.*`
  ---------------------------------------------
  `ventas.pagos.ver` y `ventas.pagos.multi` existen y los manda Mac31 (btnPagos y
  btnMultiPago de la forma CONSULTA DE VENTAS). Son dos: ver la pantalla de pagos
  y usar el multipago. Lo de aqui es otra cosa — declarar incobrable, abonar
  kilos, aplicar un pago del esquema nuevo, cancelar una transferencia
  bancaria— y no tiene boton en esa forma. Inventar `ventas.pagos.incobrable`
  seria poner una clave en la familia que Mac31 recalcula: se guardaria y
  desapareceria en el siguiente snapshot.

  `cobros.transferencias.cancelar` va aparte de los demas a proposito: ese
  procedimiento actualiza cinco tablas y la bitacora, y deshace un movimiento
  bancario. Quien captura un abono no tiene por que poder deshacer una
  transferencia.
*/
[ApiController]
[Route("api/ventas/pagos")]
[Authorize]
public class VentasPagosOperacionesController : PermisoControllerBase
{
    private static readonly string[] PermisoVer = { "ventas.pagos.ver", "cobros.pagos.editar" };
    private static readonly string[] PermisoEditar = { "cobros.pagos.editar" };
    private static readonly string[] PermisoCancelar = { "cobros.pagos.cancelar" };
    private static readonly string[] PermisoAplicar = { "cobros.pagos.aplicar" };
    private static readonly string[] PermisoIncobrable = { "cobros.pagos.incobrable" };
    private static readonly string[] PermisoKilos = { "cobros.pagos.kilos" };
    private static readonly string[] PermisoTransferencias = { "cobros.transferencias.cancelar" };
    private static readonly string[] PermisoReasignar = { "cobros.reasignar" };

    private readonly IVentasPagosOperacionesService _service;

    public VentasPagosOperacionesController(
        IVentasPagosOperacionesService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    /// <summary>
    /// Declara una remision como incobrable. Es un pago que no se va a cobrar:
    /// cierra el saldo sin que entre dinero, asi que lleva su propio permiso.
    /// </summary>
    [HttpPost("incobrable")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> IncobrableAsync([FromBody] VentaIncobrableRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoIncobrable, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.DeclararIncobrableAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Remision declarada incobrable."), data });
    }

    /// <summary>Abona kilos de casco a una remision.</summary>
    [HttpPost("kilos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AbonarKilosAsync([FromBody] VentaPagoKilosRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoKilos, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.AbonarKilosAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Abono en kilos registrado."), data });
    }

    /// <summary>Cancela un abono en kilos.</summary>
    [HttpPost("kilos/cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CancelarAbonoKilosAsync([FromBody] VentaPagoKilosCancelarRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoKilos, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CancelarAbonoKilosAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Abono en kilos cancelado."), data });
    }

    /// <summary>
    /// Limpia los pagos de centro de servicio. NO recibe ids a proposito: en
    /// legacy el procedimiento se llama siempre con ceros, es decir que la
    /// operacion siempre fue global y los campos del cuerpo nunca llegaron a la
    /// base.
    /// </summary>
    [HttpPost("centro-servicio/limpiar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> LimpiarCentroServicioAsync(CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoEditar, ct);
        if (veto is not null) return veto;

        var data = await _service.LimpiarPagosCentroServicioAsync(ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Pagos de centro de servicio limpiados."), data });
    }

    /// <summary>
    /// Captura un pago del esquema nuevo: cuelga del cliente, no de la remision.
    /// </summary>
    [HttpPost("nuevos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CrearNuevoPagoAsync([FromBody] NuevoPagoCrearRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoEditar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CrearNuevoPagoAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Pago registrado."), data });
    }

    /// <summary>Aplica un pago del esquema nuevo a las remisiones del cliente.</summary>
    [HttpPost("nuevos/aplicar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AplicarNuevoPagoAsync([FromBody] NuevoPagoRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoAplicar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.AplicarNuevoPagoAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Pago aplicado."), data });
    }

    /// <summary>Cancela un pago del esquema nuevo.</summary>
    [HttpPost("nuevos/cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CancelarNuevoPagoAsync([FromBody] NuevoPagoRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoCancelar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CancelarNuevoPagoAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Pago cancelado."), data });
    }

    /// <summary>
    /// Cancela una transferencia bancaria completa. Mueve cinco tablas y la
    /// bitacora; es la operacion mas larga de esta ola en efecto de base.
    /// </summary>
    [HttpPost("transferencias/cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CancelarTransferenciaAsync([FromBody] TransferenciaCancelarRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoTransferencias, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CancelarTransferenciaAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Transferencia cancelada."), data });
    }

    /// <summary>
    /// Cancela UN pago hecho con transferencia, dejando viva la transferencia.
    /// Devuelve los cuatro saldos a favor que recalculo el procedimiento.
    /// </summary>
    [HttpPost("transferencias/pago/cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CancelarTransferenciaPagoAsync([FromBody] TransferenciaPagoCancelarRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoTransferencias, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CancelarTransferenciaPagoAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Pago con transferencia cancelado."), data });
    }

    /// <summary>Reasigna remisiones a otro cliente o a otro agente.</summary>
    [HttpPost("/api/ventas/reasignar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ReasignarAsync([FromBody] VentasReasignarRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoReasignar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.ReasignarAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Remisiones reasignadas."), data });
    }

    /// <summary>Cambia la nota de una remision. Es lo unico que se puede cambiar.</summary>
    [HttpPost("/api/ventas/actualizar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ActualizarVentaAsync([FromBody] VentaActualizarRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoEditar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.ActualizarVentaAsync(request, ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Remision actualizada."), data });
    }

    /// <summary>Marca o desmarca remisiones como "se pagan por kilo".</summary>
    [HttpPost("/api/ventas/usados-credito/kilos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MarcarPagoPorKiloAsync([FromBody] VentasPagoPorKiloRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoKilos, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.MarcarPagoPorKiloAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Marca de pago por kilo actualizada."), data });
    }

    /// <summary>Las partidas de una o varias remisiones.</summary>
    [HttpPost("/api/ventas/detalles/consultar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarDetalleAsync([FromBody] VentasDetalleConsultaRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoVer, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.ConsultarDetalleAsync(request, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Detalle de la remision.", data });
    }

    private static string Mensaje(bool hecho, string siSeHizo)
        => hecho ? siSeHizo : "El procedimiento rechazo la operacion.";
}
