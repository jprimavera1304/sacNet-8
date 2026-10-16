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

    /*
      LAS TRES PANTALLAS DE LA COBRANZA SON TRES FORMAS DE MAC31, Y MAC31 LAS
      REPARTE POR SEPARADO.

      `cobranza.ver` sale de ConsultarCobros.btnBuscar. Quien en Mac31 solo
      tiene CONSULTA DE COBRANZAS o CONSULTA DE CONCENTRADOS -y no CONSULTA DE
      COBROS- nunca lo recibe, y hasta hoy eso le cerraba las dos pantallas que
      si usaba. El caso que lo destapo: XOCHITL (Tauro) abre COBRANZAS en Mac31
      con ConsultarCobranzas.btnVer y el web le contestaba "hace falta
      cobranza.ver", un permiso de OTRA forma.

      Asi que cada grupo de rutas acepta TAMBIEN la clave de su propia forma:

        cobranzas/*     ConsultarCobranzas    -> cobranzas.{ver,consultar}
        concentrados/*  ConsultarConcentrados -> cobranza.concentrados.{consultar,ver}
        cobros/*        ConsultarCobros       -> cobranza.ver (como estaba)

      `cobranza.ver` se queda en las tres listas: quien ya lo tenia configurado
      no pierde nada. Esto no concede nada nuevo -las claves que se suman son
      las que Mac31 ya reparte por esas formas-, solo deja de exigir la de una
      forma ajena.
    */
    private static readonly string[] PermisosVerCobranzas =
        { "cobranza.ver", "cobranzas.ver", "cobranzas.consultar" };

    /*
      Y EL CANAL MOVIL TRAE SU PROPIA CLAVE: `app_movil.concentrados.ver`.

      No es un duplicado por gusto: el telefono se presta y la computadora de la
      oficina no, asi que la consulta de concentrados se puede dar en una y no
      en la otra. Es la misma regla que ya siguen `app_movil.ventas.ver`,
      `app_movil.facturacion.ver` y las otras 17 claves del canal, y la propia
      PermisoControllerBase lo dice: "un endpoint sirve a los dos canales y cada
      canal tiene su propia clave".

      NO CONCEDE NADA A NADIE DEL WEB: `CanalDeAcceso.Filtrar` tira las claves
      `app_movil.*` cuando el token es de canal web, asi que presentarla desde el
      navegador no sirve de nada. Y `app_movil.*` no se gatea con ninguna entrada
      de modulo (`EntradasDeModulo`: el telefono no tiene tarjetas de inicio),
      asi que basta con tenerla.

      LA CLAVE TODAVIA NO EXISTE EN NINGUNA BASE. Hasta que se siembre, no la
      tiene nadie y esta linea no cambia el comportamiento de nadie. El script
      queda propuesto en el movil, en `concentrados.permisos.md`.
    */
    private static readonly string[] PermisosVerConcentrados =
    {
        "cobranza.ver", "cobranza.concentrados.consultar", "cobranza.concentrados.ver",
        "app_movil.concentrados.ver"
    };

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
        var sinPermiso = await ExigirPermisoAsync(PermisosVerCobranzas, ct);
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
        var sinPermiso = await ExigirPermisoAsync(PermisosVerCobranzas, ct);
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
        var sinPermiso = await ExigirPermisoAsync(PermisosVerConcentrados, ct);
        if (sinPermiso is not null) return sinPermiso;

        var error = ValidarFolio(body?.IDEmpresaVenta ?? 0, body?.FolioVenta);
        if (error is not null) return BadRequest(error);

        var data = await _service.ValidarFolioConcentradoAsync(body!, IdUsuarioLegacy(), ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Folio validado.", data });
    }

    /// <summary>La rejilla de la pantalla CONCENTRADOS.</summary>
    /// <remarks>
    /// Va por POST y no por GET porque son cinco filtros y porque es lo que ya
    /// hacen las otras consultas de esta familia.
    ///
    /// SIN FILTROS NO SE BARRE TODO. El procedimiento con los cinco en cero
    /// devuelve el historico entero (36,371 concentrados en Tauro, medido), y la
    /// pantalla de Mac31 nunca lo pide: arranca con la fecha de operacion en las
    /// dos cajas. Se exige al menos un filtro para que un cuerpo vacio por
    /// descuido no se lleve la base por delante.
    /// </remarks>
    [HttpPost("concentrados/buscar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarConcentrados([FromBody] ConcentradosConsultaRequest? body, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVerConcentrados, ct);
        if (sinPermiso is not null) return sinPermiso;

        if (body is null)
            return BadRequest(new { ok = false, message = "Body requerido." });

        var sinFechas = string.IsNullOrWhiteSpace(body.FechaInicial) || string.IsNullOrWhiteSpace(body.FechaFinal);
        if (sinFechas && body.FolioConcentrado <= 0 && body.FolioVenta <= 0)
            return BadRequest(new { ok = false, message = "Indica un rango de fechas o un folio." });

        var data = await _service.ConsultarConcentradosAsync(body, IdUsuarioLegacy(), ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Concentrados consultados.", data });
    }

    /// <summary>Las notas que trae un concentrado.</summary>
    [HttpGet("concentrados/{idConcentrado:int}/detalle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConcentradoDetalle(int idConcentrado, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVerConcentrados, ct);
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
