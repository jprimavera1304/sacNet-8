using ISL_Service.Application.DTOs.VentasUsadosOperaciones;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/*
  LAS VEINTE OPERACIONES DE CASCOS USADOS

  POR QUE UNA FAMILIA DE PERMISOS NUEVA (`usados.*`) Y NO `ventas.usados`
  ----------------------------------------------------------------------
  Para la familia `ventas.*` manda Mac31: al construir el snapshot se BORRA del
  usuario todo lo que empiece por "ventas." y no este en la lista blanca del web,
  y se recalcula desde los botones de la forma CONSULTA DE VENTAS
  (PermissionService, VentasLegacyPermissionMap). Una clave nueva ahi se guarda
  en WUsuarioPermiso y DESAPARECE en el siguiente calculo, sin error y sin
  rastro: es la cicatriz que ya dejo "pendientes de autorizar".

  Mac31 tiene UN boton para todo esto (btnUsados -> `ventas.usados`), y un solo
  interruptor no alcanza: aqui hay cuatro pantallas y, dentro de ellas, APLICAR
  un cobro de dinero mueve los saldos de los clientes mientras guardar el reparto
  no mueve nada. Dar los dos juntos porque Mac31 no sabe distinguirlos seria
  heredar una limitacion de la pantalla vieja.

  Las consultas SI aceptan tambien `ventas.usados`, que es lo que Mac31 manda de
  verdad: asi quien hoy puede abrir los usados de una remision sigue pudiendo
  consultarlos el primer dia, sin que nadie tenga que acordarse de encender una
  casilla nueva.
*/
[ApiController]
[Route("api/ventas/usados")]
[Authorize]
public class VentasUsadosOperacionesController : PermisoControllerBase
{
    /* Ver: la clave nueva o la que manda Mac31. */
    private static readonly string[] PermisoVer = { "usados.ver", "ventas.usados" };

    private static readonly string[] PermisoCobroDineroEditar = { "usados.cobros_dinero.editar" };
    private static readonly string[] PermisoCobroDineroAplicar = { "usados.cobros_dinero.aplicar" };
    private static readonly string[] PermisoCobroDineroCancelar = { "usados.cobros_dinero.cancelar" };
    private static readonly string[] PermisoCobroEditar = { "usados.cobros.editar" };
    private static readonly string[] PermisoCobroCancelar = { "usados.cobros.cancelar" };
    private static readonly string[] PermisoExcedenteEditar = { "usados.excedentes.editar" };
    private static readonly string[] PermisoExcedenteCancelar = { "usados.excedentes.cancelar" };
    private static readonly string[] PermisoMovimientoEditar = { "usados.movimientos.editar" };
    private static readonly string[] PermisoMovimientoCancelar = { "usados.movimientos.cancelar" };

    private readonly IVentasUsadosOperacionesService _service;

    public VentasUsadosOperacionesController(
        IVentasUsadosOperacionesService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    /* ============================================================ */
    /* Cobros de dinero por cascos                                   */
    /* ============================================================ */

    /// <summary>Registra un cobro de dinero por cascos usados.</summary>
    /// <response code="200">Lo que contesto el procedimiento de legacy.</response>
    /// <response code="400">Falta un dato, o el procedimiento rechazo la operacion.</response>
    /// <response code="403">No tiene el permiso.</response>
    [HttpPost("cobros-dinero")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CrearCobroDineroAsync(
        [FromBody] CobroDineroUsadoCrearRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoCobroDineroEditar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CrearCobroDineroAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Cobro de dinero registrado."), data });
    }

    /// <summary>
    /// Guarda el reparto del cobro entre los clientes, SIN aplicarlo.
    /// Es la mitad inofensiva de lo que en legacy decidia la bandera
    /// `esAplicacion` del cuerpo.
    /// </summary>
    [HttpPost("cobros-dinero/reparto")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GuardarRepartoAsync(
        [FromBody] CobroDineroUsadoRepartoRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoCobroDineroEditar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.GuardarRepartoCobroDineroAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Reparto guardado."), data });
    }

    /// <summary>
    /// APLICA el reparto a los saldos de los clientes. Esta es la que mueve
    /// dinero, y por eso lleva su propio permiso y su propia ruta.
    /// </summary>
    [HttpPost("cobros-dinero/aplicar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AplicarAsync(
        [FromBody] CobroDineroUsadoRepartoRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoCobroDineroAplicar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.AplicarCobroDineroAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Cobro aplicado."), data });
    }

    /// <summary>Cancela la aplicacion de un cobro de dinero por cascos.</summary>
    [HttpPost("cobros-dinero/cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CancelarCobroDineroAsync(
        [FromBody] CobroDineroUsadoCancelarRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoCobroDineroCancelar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CancelarCobroDineroAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Aplicacion cancelada."), data });
    }

    /// <summary>Consulta los cobros de dinero por cascos.</summary>
    [HttpPost("cobros-dinero/consultar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarCobrosDineroAsync(
        [FromBody] CobroDineroUsadoConsultaRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoVer, ct);
        if (veto is not null) return veto;

        var data = await _service.ConsultarCobrosDineroAsync(request ?? new CobroDineroUsadoConsultaRequest(), ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Cobros de dinero.", data });
    }

    /// <summary>
    /// Los clientes de la hoja de cobro: sobre quienes se reparte el dinero y
    /// los cascos.
    /// </summary>
    /// <remarks>
    /// Es lo primero que carga la forma TRABAJAR DINERO Y CASCOS de Mac31
    /// (`DineroUsadosCobroCliente`). Sin esto, `cobros-dinero/reparto` es un
    /// contrato que nadie puede llenar: pide listas de IDCliente y no habia
    /// manera de averiguar cuales son los de la hoja.
    ///
    /// Lleva el permiso de VER y no el de editar: es una lectura, y quien va a
    /// repartir necesita verla antes de que el permiso de escritura entre en
    /// juego — el reparto ya exige el suyo en su propia ruta.
    /// </remarks>
    [HttpPost("cobros-dinero/clientes/consultar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarClientesDelCobroAsync(
        [FromBody] CobroDineroUsadoClientesConsultaRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoVer, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.ConsultarClientesDelCobroAsync(request, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Clientes de la hoja de cobro.", data });
    }

    /// <summary>Registra un cheque dentro de un cobro de dinero.</summary>
    [HttpPost("cobros-dinero/cheques")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CrearChequeAsync(
        [FromBody] CobroDineroUsadoChequeCrearRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoCobroDineroEditar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CrearChequeAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Cheque registrado."), data });
    }

    /// <summary>Modifica un cheque o le cambia el estatus.</summary>
    [HttpPost("cobros-dinero/cheques/actualizar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ActualizarChequeAsync(
        [FromBody] CobroDineroUsadoChequeActualizarRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoCobroDineroEditar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.ActualizarChequeAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Cheque actualizado."), data });
    }

    /// <summary>Consulta los cheques de un cobro de dinero.</summary>
    [HttpPost("cobros-dinero/cheques/consultar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarChequesAsync(
        [FromBody] CobroDineroUsadoChequeConsultaRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoVer, ct);
        if (veto is not null) return veto;

        var data = await _service.ConsultarChequesAsync(request ?? new CobroDineroUsadoChequeConsultaRequest(), ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Cheques del cobro.", data });
    }

    /// <summary>Liga un corte de un centro de servicio al cobro de dinero.</summary>
    [HttpPost("cobros-dinero/cortes-cs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CrearCorteCsAsync(
        [FromBody] CobroDineroUsadoCorteCsRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoCobroDineroEditar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CrearCorteCsAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Corte ligado al cobro."), data });
    }

    /// <summary>Desliga un corte de centro de servicio del cobro de dinero.</summary>
    [HttpPost("cobros-dinero/cortes-cs/eliminar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> EliminarCorteCsAsync(
        [FromBody] CobroDineroUsadoCorteCsRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoCobroDineroEditar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.EliminarCorteCsAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Corte desligado del cobro."), data });
    }

    /// <summary>Consulta los cortes de centro de servicio del cobro.</summary>
    [HttpPost("cobros-dinero/cortes-cs/consultar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarCortesCsAsync(
        [FromBody] CobroDineroUsadoCorteCsConsultaRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoVer, ct);
        if (veto is not null) return veto;

        var data = await _service.ConsultarCortesCsAsync(request ?? new CobroDineroUsadoCorteCsConsultaRequest(), ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Cortes del cobro.", data });
    }

    /* ============================================================ */
    /* Ingresos de cascos usados                                     */
    /* ============================================================ */

    /// <summary>Registra el ingreso de cascos usados contra un cobro.</summary>
    [HttpPost("cobros")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CrearCobroUsadoAsync(
        [FromBody] VentasCobroUsadoCrearRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoCobroEditar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CrearCobroUsadoAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Ingreso de cascos registrado."), data });
    }

    /// <summary>Cancela un ingreso de cascos usados.</summary>
    [HttpPost("cobros/cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CancelarCobroUsadoAsync(
        [FromBody] VentasCobroUsadoCancelarRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoCobroCancelar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CancelarCobroUsadoAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Ingreso cancelado."), data });
    }

    /// <summary>Consulta los ingresos de cascos usados.</summary>
    [HttpPost("cobros/consultar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarCobrosUsadoAsync(
        [FromBody] VentasCobroUsadoConsultaRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoVer, ct);
        if (veto is not null) return veto;

        var data = await _service.ConsultarCobrosUsadoAsync(request ?? new VentasCobroUsadoConsultaRequest(), ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Ingresos de cascos.", data });
    }

    /* ============================================================ */
    /* Excedentes de cascos                                          */
    /* ============================================================ */

    /// <summary>Registra un excedente de cascos con su vale.</summary>
    [HttpPost("excedentes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CrearExcedenteAsync(
        [FromBody] VentasUsadoExcedenteCrearRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoExcedenteEditar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CrearExcedenteAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Excedente registrado."), data });
    }

    /// <summary>Cancela un excedente de cascos.</summary>
    [HttpPost("excedentes/cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CancelarExcedenteAsync(
        [FromBody] VentasUsadoExcedenteCancelarRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoExcedenteCancelar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CancelarExcedenteAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Excedente cancelado."), data });
    }

    /// <summary>Consulta los excedentes de cascos.</summary>
    [HttpPost("excedentes/consultar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarExcedentesAsync(
        [FromBody] VentasUsadoExcedenteConsultaRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoVer, ct);
        if (veto is not null) return veto;

        var data = await _service.ConsultarExcedentesAsync(request ?? new VentasUsadoExcedenteConsultaRequest(), ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Excedentes de cascos.", data });
    }

    /* ============================================================ */
    /* Movimientos del almacen de usados                             */
    /* ============================================================ */

    /// <summary>Registra un movimiento del almacen de cascos usados.</summary>
    [HttpPost("movimientos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CrearMovimientoAsync(
        [FromBody] UsadoMovimientoCrearRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoMovimientoEditar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CrearMovimientoAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Movimiento registrado."), data });
    }

    /// <summary>Cancela un movimiento del almacen de cascos usados.</summary>
    [HttpPost("movimientos/cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CancelarMovimientoAsync(
        [FromBody] UsadoMovimientoCancelarRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoMovimientoCancelar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CancelarMovimientoAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Movimiento cancelado."), data });
    }

    /// <summary>Consulta los movimientos del almacen de cascos usados.</summary>
    [HttpPost("movimientos/consultar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarMovimientosAsync(
        [FromBody] UsadoMovimientoConsultaRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoVer, ct);
        if (veto is not null) return veto;

        var data = await _service.ConsultarMovimientosAsync(request ?? new UsadoMovimientoConsultaRequest(), ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Movimientos de usados.", data });
    }

    /// <summary>Consulta los movimientos ya pagados (los del corte).</summary>
    [HttpPost("movimientos/pagados/consultar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarMovimientosPagadosAsync(
        [FromBody] UsadoMovimientoPagadoConsultaRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoVer, ct);
        if (veto is not null) return veto;

        var data = await _service.ConsultarMovimientosPagadosAsync(
            request ?? new UsadoMovimientoPagadoConsultaRequest(), ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Movimientos pagados.", data });
    }

    /// <summary>
    /// El mensaje del sobre. Cuando el procedimiento dijo que NO, el mensaje que
    /// vale es el suyo, no uno nuestro: de esos textos dependen pantallas
    /// enteras y reescribirlos seria perder la unica explicacion que hay.
    /// </summary>
    private static string Mensaje(bool hecho, string siSeHizo)
        => hecho ? siSeHizo : "El procedimiento rechazo la operacion.";
}
