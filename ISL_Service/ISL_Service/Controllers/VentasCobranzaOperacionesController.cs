using ISL_Service.Application.DTOs.VentasCobranzaOperaciones;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/*
  COBRANZA, CONCENTRADO Y COBRO

  La familia de permisos es `cobranza.*` y no `ventas.*` por lo de siempre: para
  `ventas.*` manda Mac31 y una clave nueva ahi se borra en el siguiente calculo
  del snapshot. Tampoco se reusa `ventas.consultar` (el btnBuscar de Consultar
  ventas): la cobranza son TRES pantallas distintas de Mac31 y hay gente que
  cobra sin consultar ventas.

  Dentro de la familia, cerrar y reabrir un cobro llevan permisos distintos a
  proposito. Cerrar es lo que cuadra el dinero del dia; REABRIR uno cerrado es
  deshacer ese cuadre, y quien puede hacer lo primero no necesariamente debe
  poder hacer lo segundo.
*/
[ApiController]
[Route("api/ventas/cobranza")]
[Authorize]
public class VentasCobranzaOperacionesController : PermisoControllerBase
{
    private static readonly string[] PermisoVer = { "cobranza.ver" };

    /*
      LA LISTA DE COBRANZAS SE LEE CON LA CLAVE DE SU PROPIA FORMA.

      Misma razon que en VentasCobranzaConsultaController: `cobranza.ver` es
      ConsultarCobros.btnBuscar, y quien en Mac31 solo tiene CONSULTA DE
      COBRANZAS no lo recibe nunca. Esta es la ruta que llena la pantalla de
      COBRANZAS del web, asi que acepta tambien lo que esa forma si reparte.
      `cobranza.ver` sigue valiendo: nadie pierde el acceso que ya tenia.
    */
    private static readonly string[] PermisoVerCobranzas =
        { "cobranza.ver", "cobranzas.ver", "cobranzas.consultar" };
    private static readonly string[] PermisoCobranzaEditar = { "cobranza.cobranzas.editar" };
    private static readonly string[] PermisoCobranzaCancelar = { "cobranza.cobranzas.cancelar" };
    private static readonly string[] PermisoConcentradoEditar = { "cobranza.concentrados.editar" };
    private static readonly string[] PermisoConcentradoCancelar = { "cobranza.concentrados.cancelar" };
    private static readonly string[] PermisoCobroEditar = { "cobranza.cobros.editar" };
    private static readonly string[] PermisoCobroCerrar = { "cobranza.cobros.cerrar" };
    private static readonly string[] PermisoCobroAbrir = { "cobranza.cobros.abrir" };
    private static readonly string[] PermisoCobroCancelar = { "cobranza.cobros.cancelar" };

    private readonly IVentasCobranzaOperacionesService _service;

    public VentasCobranzaOperacionesController(
        IVentasCobranzaOperacionesService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    /* ---------------------------- Cobranza ---------------------------- */

    /// <summary>Crea una cobranza con las remisiones del repartidor.</summary>
    [HttpPost("cobranzas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CrearCobranzaAsync([FromBody] CobranzaCrearRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoCobranzaEditar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CrearCobranzaAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Cobranza creada."), data });
    }

    /// <summary>Modifica una cobranza: quita, devuelve y agrega remisiones.</summary>
    [HttpPost("cobranzas/actualizar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ActualizarCobranzaAsync([FromBody] CobranzaActualizarRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoCobranzaEditar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.ActualizarCobranzaAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Cobranza actualizada."), data });
    }

    /// <summary>Cancela una o varias cobranzas.</summary>
    [HttpPost("cobranzas/cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CancelarCobranzaAsync([FromBody] CobranzaCancelarRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoCobranzaCancelar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CancelarCobranzaAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Cobranzas canceladas."), data });
    }

    /// <summary>Consulta las cobranzas.</summary>
    [HttpPost("cobranzas/consultar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarCobranzasAsync([FromBody] CobranzaConsultaRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoVerCobranzas, ct);
        if (veto is not null) return veto;

        var data = await _service.ConsultarCobranzasAsync(
            request ?? new CobranzaConsultaRequest(), IdUsuarioLegacy(), ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Cobranzas.", data });
    }

    /* --------------------------- Concentrado --------------------------- */

    /// <summary>Crea un concentrado de devoluciones.</summary>
    [HttpPost("concentrados")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CrearConcentradoAsync([FromBody] ConcentradoCrearRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoConcentradoEditar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CrearConcentradoAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Concentrado creado."), data });
    }

    /// <summary>Modifica un concentrado.</summary>
    [HttpPost("concentrados/actualizar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ActualizarConcentradoAsync([FromBody] ConcentradoActualizarRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoConcentradoEditar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.ActualizarConcentradoAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Concentrado actualizado."), data });
    }

    /// <summary>Cancela uno o varios concentrados.</summary>
    [HttpPost("concentrados/cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CancelarConcentradoAsync([FromBody] ConcentradoCancelarRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoConcentradoCancelar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CancelarConcentradoAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Concentrados cancelados."), data });
    }

    /* ------------------------------ Cobro ------------------------------ */

    /// <summary>Reabre cobros cerrados. Deshace el cuadre del dia.</summary>
    [HttpPost("cobros/abrir")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AbrirCobroAsync([FromBody] CobroLoteRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoCobroAbrir, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.AbrirCobroAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Cobros abiertos."), data });
    }

    /// <summary>Cierra cobros.</summary>
    [HttpPost("cobros/cerrar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CerrarCobroAsync([FromBody] CobroLoteRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoCobroCerrar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CerrarCobroAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Cobros cerrados."), data });
    }

    /// <summary>Cancela cobros.</summary>
    [HttpPost("cobros/cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CancelarCobroAsync([FromBody] CobroLoteRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoCobroCancelar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CancelarCobroAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Cobros cancelados."), data });
    }

    /// <summary>Reasigna el cobro a otro concentrado o cobranza, o cambia su nota.</summary>
    [HttpPost("cobros/actualizar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ActualizarCobroAsync([FromBody] CobroActualizarRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoCobroEditar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.ActualizarCobroAsync(request, ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Cobro actualizado."), data });
    }

    /// <summary>Consulta los cobros.</summary>
    [HttpPost("cobros/consultar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarCobrosAsync([FromBody] CobroConsultaRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoVer, ct);
        if (veto is not null) return veto;

        var data = await _service.ConsultarCobrosAsync(request ?? new CobroConsultaRequest(), IdUsuarioLegacy(), ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Cobros.", data });
    }

    /// <summary>Los clientes que pagaron dentro de un cobro.</summary>
    [HttpGet("cobros/{idCobro:int}/clientes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarCobroClientesAsync(int idCobro, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoVer, ct);
        if (veto is not null) return veto;

        var data = await _service.ConsultarCobroClientesAsync(idCobro, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Clientes del cobro.", data });
    }

    /// <summary>
    /// La cobranza pendiente y vencida. Lee saldos, asi que sale con
    /// `no-store`: un saldo en la cache del navegador es un saldo equivocado.
    /// </summary>
    [HttpPost("pendientes-vencidos/consultar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarPendientesVencidosAsync(
        [FromBody] CobranzaPendientesVencidosRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoVer, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.ConsultarPendientesVencidosAsync(request, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Cobranza pendiente y vencida.", data });
    }

    private static string Mensaje(bool hecho, string siSeHizo)
        => hecho ? siSeHizo : "El procedimiento rechazo la operacion.";
}
