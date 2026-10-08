using ISL_Service.Application.DTOs.VentasFacturacion;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/*
  FACTURACION ELECTRONICA

  En legacy estos cinco endpoints viven en un controlador con [AllowAnonymous] y
  uno de ellos —sendemail— es el UNICO de todo MacServicios2 que no valida el
  token: su servicio no llama a validateJwtTokenAsync. Lo unico que lo protegia
  era el handler global, que solo comprueba que EXISTA la cabecera Authorization
  sin mirar su contenido. O sea: cualquiera con una cabecera cualquiera podia
  mandar la factura de cualquier cliente al correo que quisiera.

  Aqui los cinco llevan [Authorize] y su permiso.

  LA FAMILIA ES `facturacion.*` Y NO SE REUSA `ventas.facturar`
  ------------------------------------------------------------
  `ventas.facturar` existe (btnFacturar de la forma CONSULTA DE VENTAS) y es el
  permiso de "mandar a facturar desde la pantalla de ventas". No alcanza para esto:
  cancelar un CFDI ante el SAT y mandar una factura por correo son dos cosas
  distintas de timbrarla, y en una familia donde manda Mac31 no se pueden crear
  claves nuevas (se borran en el siguiente recalculo del snapshot). Timbrar SI
  acepta las dos, la nueva y la de Mac31, para que quien hoy factura siga
  facturando.
*/
[ApiController]
[Route("api/ventas/facturas")]
[Authorize]
public class VentasFacturacionController : PermisoControllerBase
{
    private static readonly string[] PermisoVer = { "facturacion.ver", "ventas.facturar" };
    private static readonly string[] PermisoTimbrar = { "facturacion.timbrar", "ventas.facturar" };
    private static readonly string[] PermisoCancelar = { "facturacion.cancelar" };
    private static readonly string[] PermisoEnviar = { "facturacion.enviar" };

    private readonly IVentasFacturacionService _service;

    public VentasFacturacionController(
        IVentasFacturacionService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    /// <summary>
    /// Timbra una factura o una nota de credito ante el SAT.
    ///
    /// ES IRREVERSIBLE: si contesta 200, hay un CFDI emitido. Cuando el timbrado
    /// funciona pero el guardado en Mac31 falla, el mensaje lo dice con el UUID y
    /// el id de Facturapi, porque ese comprobante existe ante el SAT y hay que
    /// registrarlo a mano.
    /// </summary>
    /// <response code="200">El comprobante timbrado y las facturas que se crearon.</response>
    /// <response code="409">El candado del key no paso, o Facturapi rechazo el comprobante.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> TimbrarAsync([FromBody] FacturaTimbrarRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoTimbrar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.TimbrarAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = data.Mensaje ?? "Comprobante timbrado.", data });
    }

    /// <summary>
    /// Timbra un complemento de pago. Crea UN comprobante ante el SAT y un renglon
    /// de factura en Mac31 por cada documento pagado.
    /// </summary>
    [HttpPost("complemento-pago")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> TimbrarComplementoAsync(
        [FromBody] ComplementoPagoTimbrarRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoTimbrar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.TimbrarComplementoAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = data.Mensaje ?? "Complemento de pago timbrado.", data });
    }

    /// <summary>
    /// Cancela un CFDI ante el SAT y lo registra.
    ///
    /// El estatus que se guarda es el que contesto el SAT: si el cliente rechaza la
    /// cancelacion, la factura queda VIGENTE. Legacy forzaba siempre "aceptada", lo
    /// que dejaba facturas validas ante el SAT que el sistema daba por canceladas.
    /// </summary>
    [HttpPost("cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelarAsync([FromBody] FacturaCancelarRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoCancelar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CancelarAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = data.Mensaje ?? "Cancelacion registrada.", data });
    }

    /// <summary>
    /// Consulta las facturas. A diferencia de la de legacy, esta NO escribe: no
    /// cancela nada por su cuenta.
    /// </summary>
    [HttpPost("consultar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarAsync([FromBody] FacturaConsultaRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoVer, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.ConsultarAsync(request, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Facturas.", data });
    }

    /// <summary>
    /// Baja el PDF o el XML de un comprobante ya timbrado.
    ///
    /// Son los dos botones de Mac31 (IMPRIMIR PDF F7 y DESCARGAR XML F8) que no
    /// tenian endpoint: hasta ahora lo UNICO que servia un comprobante timbrado era
    /// el correo, asi que para releer una factura habia que mandarsela a alguien.
    ///
    /// Lleva el permiso de ver y no uno nuevo: quien puede consultar la factura
    /// puede leer su propio comprobante. Mandarlo por correo es otra cosa y por eso
    /// tiene su clave aparte.
    /// </summary>
    /// <response code="200">El archivo.</response>
    /// <response code="404">La factura no existe.</response>
    /// <response code="409">El archivo no esta en disco; el mensaje trae la ruta.</response>
    [HttpGet("{idVentaFactura:int}/documento")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DocumentoAsync(
        int idVentaFactura,
        [FromQuery] string formato,
        CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoVer, ct);
        if (veto is not null) return veto;

        var documento = await _service.LeerDocumentoAsync(idVentaFactura, formato, ct);

        /*
          Cache-Control no-store: un CFDI puede cancelarse, y un PDF guardado en
          la cache del navegador seguiria diciendo que la factura esta vigente.
        */
        Response.Headers["Cache-Control"] = "no-store";
        return File(documento.Contenido, documento.TipoContenido, documento.NombreArchivo);
    }

    /// <summary>
    /// Manda la factura por correo con su PDF y su XML.
    ///
    /// Lleva permiso propio: en legacy este era el unico endpoint del sistema que
    /// no validaba el token, asi que cualquiera podia mandar la factura de
    /// cualquier cliente al correo que quisiera.
    /// </summary>
    [HttpPost("enviar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EnviarAsync([FromBody] FacturaEnviarCorreoRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoEnviar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var destinatarios = await _service.EnviarCorreoAsync(request, ct);
        return Ok(new { ok = true, message = $"Factura enviada a {destinatarios}.", data = new { destinatarios } });
    }
}
