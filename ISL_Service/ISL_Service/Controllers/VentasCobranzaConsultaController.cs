using ISL_Service.Application.DTOs.VentasCobranza;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Las lecturas de la cobranza de Mac31: cobranzas, concentrados y cobros.
///
/// POR QUE UN CONTROLADOR NUEVO Y NO DENTRO DE VentasConsultaController
/// --------------------------------------------------------------------
/// `api/ventas/consulta` es la pantalla "Consultar ventas" de Mac31, y sus
/// permisos salen de la forma CONSULTA DE VENTAS. La cobranza son TRES
/// pantallas distintas de Mac31 (Cobranza, Concentrado, Cobro), con su propio
/// permiso y su propio ciclo. Meterlas ahi mezclaria dos modulos del negocio en
/// el mismo candado.
///
/// QUE NO ESTA AQUI, Y POR QUE
/// ---------------------------
/// Las consultas "de pantalla completa" de cobros y cobranzas
/// (`POST /api/cobros/consultar` y `/api/cobranzas/consultar` de legacy) NO se
/// migraron: su primer paso es `sp_n_ConsultaConstantes`, que cuando la fecha de
/// operacion esta atrasada **hace el cierre del dia** — borra y rehace
/// `AlmacenProductoHist` y adelanta `Constantes.FechaOperacion`
/// (`migracion/CUIDADO-consultas-que-escriben.md`). Esta ola es de solo
/// lectura, asi que quedaron fuera y anotadas.
/// </summary>
[ApiController]
[Route("api/ventas")]
[Authorize]
public class VentasCobranzaConsultaController : PermisoControllerBase
{
    /*
      EL PERMISO ES NUEVO Y ES DEL WEB, Y HAY UNA RAZON PARA CADA COSA.

      NO SE REUSO `ventas.consultar`: para la familia `ventas.*` manda Mac31
      (PermissionService, seccion 2.8 del resumen). Al leer el snapshot se BORRA
      lo que diga el web y se recalcula desde los botones de la forma CONSULTA DE
      VENTAS. `ventas.consultar` es btnBuscar de ESA pantalla; darselo a alguien
      para que vea la cobranza seria abrirle tambien la consulta de ventas, y
      quitarselo de la cobranza le cerraria la de ventas. Son dos pantallas, no
      una.

      NO SE INVENTO UN `ventas.cobranza.ver`: cualquier clave que empiece por
      "ventas." y no este en la lista blanca `VentasWebPermissionSeeds` se
      guarda en WUsuarioPermiso y **desaparece en el siguiente calculo**, sin
      error y sin rastro. Es la cicatriz que ya dejo "pendientes de autorizar".

      Y NO SE AMARRO A UNA FORMA DE MAC31: el puente de permisos heredados
      (`LegacyModuleBinding`) va por UNA forma por modulo, y la cobranza son
      tres formas distintas. Amarrarlo obligaria a cambiar el puente, que es
      justo la pieza que mas rompe.

      Asi que el permiso vive en una familia NUEVA, `cobranza.*`, que es del web
      y por lo tanto se respeta: queda sembrado en
      `PermissionService.PermissionCatalogSeeds`.
    */
    private static readonly string[] PermisosVer = { "cobranza.ver" };

    private readonly IVentasCobranzaConsultaService _service;

    public VentasCobranzaConsultaController(
        IVentasCobranzaConsultaService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    /// <summary>
    /// ¿Se puede agregar este folio de venta a esta cobranza?
    /// </summary>
    /// <remarks>
    /// Es una CONSULTA aunque se llame "validar": el procedimiento solo lee y
    /// devuelve el renglon de la venta mas las columnas `Valido`,
    /// `ValidoAlmacen` y `Detalle` con el motivo por el que no se puede. Nada
    /// se agrega aqui.
    ///
    /// Va por POST y no por GET porque son cuatro filtros, uno de ellos texto,
    /// y porque es lo que ya hace la pantalla.
    /// </remarks>
    /// <response code="200">`data.rows[0].valido` vacio = se puede agregar.</response>
    [HttpPost("cobranzas/validar-folio")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ValidarFolioCobranza([FromBody] CobranzaFolioRequest? body, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso is not null) return sinPermiso;

        var error = ValidarFolio(body?.IDEmpresaVenta ?? 0, body?.FolioVenta);
        if (error is not null) return BadRequest(error);

        var data = await _service.ValidarFolioCobranzaAsync(body!, IdUsuarioLegacy(), ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Folio validado.", data });
    }

    /// <summary>Las notas que trae una cobranza.</summary>
    /// <param name="idCobranza">La cobranza.</param>
    /// <param name="formato">0 pantalla (default), 1 impresora, 2 pdf. Cambia las columnas.</param>
    [HttpGet("cobranzas/{idCobranza:int}/detalle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CobranzaDetalle(int idCobranza, [FromQuery] int formato, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso is not null) return sinPermiso;

        if (idCobranza <= 0)
            return BadRequest(new { ok = false, message = "Selecciona una cobranza." });

        var data = await _service.ConsultarCobranzaDetalleAsync(idCobranza, formato, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Detalle de la cobranza consultado.", data });
    }

    /// <summary>¿Se puede agregar este folio de venta a este concentrado?</summary>
    [HttpPost("concentrados/validar-folio")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ValidarFolioConcentrado([FromBody] ConcentradoFolioRequest? body, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso is not null) return sinPermiso;

        var error = ValidarFolio(body?.IDEmpresaVenta ?? 0, body?.FolioVenta);
        if (error is not null) return BadRequest(error);

        var data = await _service.ValidarFolioConcentradoAsync(body!, IdUsuarioLegacy(), ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Folio validado.", data });
    }

    /// <summary>Las notas que trae un concentrado.</summary>
    [HttpGet("concentrados/{idConcentrado:int}/detalle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConcentradoDetalle(int idConcentrado, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso is not null) return sinPermiso;

        if (idConcentrado <= 0)
            return BadRequest(new { ok = false, message = "Selecciona un concentrado." });

        var data = await _service.ConsultarConcentradoDetalleAsync(idConcentrado, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Detalle del concentrado consultado.", data });
    }

    /// <summary>Un cobro de centro de servicio, por su id o por su folio.</summary>
    /// <remarks>
    /// Los dos filtros son alternativos y el procedimiento acepta ambos en 0
    /// (entonces trae todos). Se exige al menos uno porque pedir "todos los
    /// cobros de todos los centros" por accidente es un barrido caro que nadie
    /// queria.
    /// </remarks>
    [HttpGet("cobros/centro-servicio")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CobrosCentroServicio(
        [FromQuery] int idCobro,
        [FromQuery] int folioCobro,
        CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso is not null) return sinPermiso;

        if (idCobro <= 0 && folioCobro <= 0)
            return BadRequest(new { ok = false, message = "Indica el cobro o su folio." });

        var data = await _service.ConsultarCobrosCentroServicioAsync(idCobro, folioCobro, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Cobros de centro de servicio consultados.", data });
    }

    /// <summary>Los folios y los cascos de un concentrado, cobranza o cobro.</summary>
    [HttpPost("cobros/folios-usados")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> FoliosUsados([FromBody] CobroFoliosUsadosRequest? body, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso is not null) return sinPermiso;

        var req = body ?? new CobroFoliosUsadosRequest();
        if (req.IDConcentrado <= 0 && req.IDCobranza <= 0 && req.IDCobro <= 0)
            return BadRequest(new { ok = false, message = "Indica el concentrado, la cobranza o el cobro." });

        var data = await _service.ConsultarFoliosUsadosAsync(req, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Folios y cascos consultados.", data });
    }

    /// <summary>Los pagos aplicados dentro de un concentrado, cobranza o cobro.</summary>
    [HttpPost("cobros/pagos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CobroPagos([FromBody] CobroPagosRequest? body, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso is not null) return sinPermiso;

        var req = body ?? new CobroPagosRequest();
        if (req.IDConcentrado <= 0 && req.IDCobranza <= 0 && req.IDCobro <= 0)
            return BadRequest(new { ok = false, message = "Indica el concentrado, la cobranza o el cobro." });

        var data = await _service.ConsultarCobroPagosAsync(req, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Pagos del cobro consultados.", data });
    }

    /// <summary>Los pagos por kilo (el dinero que entra pesando los cascos).</summary>
    /// <remarks>
    /// Va aqui y no en `api/ventas/pagos` porque el procedimiento se filtra por
    /// `@IDCobro` y sus columnas son del cobro (`FolioCob`, `UsuarioCob`,
    /// `FechaCobFtm`): es la pestaña de kilos del COBRO, no el movimiento de una
    /// remision, que es lo que vive en VentasPagosController.
    /// </remarks>
    [HttpPost("cobros/pagos-kilos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PagosKilos([FromBody] PagosKilosRequest? body, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso is not null) return sinPermiso;

        var data = await _service.ConsultarPagosKilosAsync(body ?? new PagosKilosRequest(), ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Pagos por kilo consultados.", data });
    }

    /// <summary>Los folios de venta que todavia no estan en un concentrado.</summary>
    /// <param name="idsEmpresa">Las empresas a mirar. Repetir el parametro para varias.</param>
    /// <param name="devueltasA">
    /// El filtro de devueltas de Mac31. 0 = todas.
    /// </param>
    /// <remarks>
    /// Se exige al menos una empresa. Legacy no lo exige, y sin empresas el
    /// procedimiento barre TODAS: en una instalacion con varias empresas eso
    /// mezcla folios de negocios distintos en la misma lista, que es exactamente
    /// lo que la pantalla intenta separar.
    /// </remarks>
    [HttpGet("folios-sin-asignar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> FoliosSinAsignar(
        [FromQuery] List<int>? idsEmpresa,
        [FromQuery] int devueltasA,
        CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso is not null) return sinPermiso;

        var empresas = (idsEmpresa ?? new List<int>()).Where(x => x > 0).ToList();
        if (empresas.Count == 0)
            return BadRequest(new { ok = false, message = "Selecciona al menos una empresa." });

        var data = await _service.ConsultarFoliosSinAsignarAsync(empresas, devueltasA, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Folios sin asignar consultados.", data });
    }

    /*
      LA MISMA VALIDACION QUE LEGACY, CON EL MISMO ALCANCE.

      ConsultaVentasCobranzasController.cs:46-49 y
      ConsultaVentasConcentradosController.cs:41-43 exigen empresa y folio y
      nada mas. Se conserva igual: el folio por si solo no identifica una venta
      (se repite entre empresas) y sin el no hay nada que validar.
    */
    private static object? ValidarFolio(int idEmpresaVenta, string? folioVenta)
    {
        if (idEmpresaVenta <= 0)
            return new { ok = false, message = "Selecciona la empresa del folio." };
        if (string.IsNullOrWhiteSpace(folioVenta))
            return new { ok = false, message = "Captura el folio de la venta." };
        return null;
    }
}
