using ISL_Service.Application.DTOs.VentasConsulta;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISL_Service.Application.Security;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/ventas/consulta")]
[Authorize]
public class VentasConsultaController : ControllerBase
{
    private readonly IVentasConsultaService _service;
    private readonly IRemisionImpresionService _remisiones;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IConfiguration _configuration;

    public VentasConsultaController(
        IVentasConsultaService service,
        IRemisionImpresionService remisiones,
        ICurrentUserAccessor currentUserAccessor,
        IConfiguration configuration)
    {
        _service = service;
        _remisiones = remisiones;
        _currentUserAccessor = currentUserAccessor;
        _configuration = configuration;
    }

    [HttpGet("catalogos")]
    public async Task<IActionResult> Catalogos(CancellationToken ct)
    {
        var data = await _service.ConsultarCatalogosAsync(ct);
        return Ok(new { ok = true, message = "Catalogos consultados.", data });
    }

    [HttpPost("remisiones")]
    public async Task<IActionResult> Remisiones([FromBody] VentasConsultaRequest? request, CancellationToken ct)
    {
        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var data = await _service.ConsultarRemisionesAsync(request ?? new VentasConsultaRequest(), idUsuario, ct);
        return Ok(new { ok = true, message = "Remisiones consultadas.", data });
    }

    [HttpPost("pedidos")]
    public async Task<IActionResult> Pedidos([FromBody] VentasConsultaRequest? request, CancellationToken ct)
    {
        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var data = await _service.ConsultarPedidosAsync(request ?? new VentasConsultaRequest(), idUsuario, ct);
        return Ok(new { ok = true, message = "Pedidos consultados.", data });
    }

    [HttpPost("pendientes-imprimir")]
    public async Task<IActionResult> PendientesImprimir([FromBody] VentasConsultaRequest? request, CancellationToken ct)
    {
        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var data = await _service.ConsultarPendientesImprimirAsync(request ?? new VentasConsultaRequest(), idUsuario, ct);
        return Ok(new { ok = true, message = "Pendientes de imprimir consultados.", data });
    }

    [HttpPost("pagos")]
    public async Task<IActionResult> Pagos([FromBody] VentasConsultaRequest? request, CancellationToken ct)
    {
        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var data = await _service.ConsultarPagosAsync(request ?? new VentasConsultaRequest(), idUsuario, ct);
        return Ok(new { ok = true, message = "Pagos consultados.", data });
    }

    /*
      PREPARA EL REPORTE Y DICE A DONDE IR

      No devuelve el PDF: devuelve la direccion donde esta. La pantalla ya abrio
      la pestana antes de llamar aqui (si la abriera al recibir la respuesta, el
      navegador la bloquearia por emergente), asi que lo unico que le falta es a
      donde apuntarla.
    */
    /*
      PEDIDOS FALTANTES

      GET y sin cuerpo porque en Mac31 tampoco se le pasa nada: el boton
      (ConsultarVentas.cs:3472) llama derecho a la consulta y el procedimiento
      resuelve solo el dia y la empresa.
    */
    [HttpGet("pedidos-faltantes")]
    public async Task<IActionResult> PedidosFaltantes(CancellationToken ct)
    {
        var data = await _service.ConsultarPedidosFaltantesAsync(ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Pedidos faltantes consultados.", data });
    }

    /*
      LO QUE HAY QUE PREGUNTAR ANTES

      Mac31 pregunta antes de imprimir: contrasena si toca, confirmacion de
      reimpresion, dialogo de impresora. Aqui eso se resuelve en una llamada
      aparte porque el navegador obliga: la pestaña del PDF tiene que abrirse en
      el mismo clic o el bloqueador de emergentes se la come, asi que la
      pantalla necesita saber si va a abrir una pestaña o un dialogo ANTES de
      pedir nada.
    */
    [HttpPost("reporte/preparar")]
    public async Task<IActionResult> PrepararReporte([FromBody] VentasReporteRequest? request, CancellationToken ct)
    {
        try
        {
            var data = await _service.PrepararReporteAsync(request ?? new VentasReporteRequest(), ct);
            Response.Headers["Cache-Control"] = "no-store";
            return Ok(new { ok = true, message = "Listo.", data });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { ok = false, message = ex.Message });
        }
    }

    [HttpPost("reporte")]
    public async Task<IActionResult> Reporte([FromBody] VentasReporteRequest? request, CancellationToken ct)
    {
        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);

        string ticket;
        try
        {
            /*
              El equipo sale del token, no del cuerpo: es lo que queda escrito en
              la venta como "desde donde se imprimio". Si viajara en el body,
              cualquiera podria firmar una impresion a nombre de otra maquina.
            */
            var equipo = _currentUserAccessor.GetUsername(User, Environment.MachineName);
            ticket = await _service.CrearTicketReporteAsync(request ?? new VentasReporteRequest(), idUsuario, equipo, ct);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { ok = false, message = ex.Message });
        }

        var data = new VentasReporteResponse { Ok = true, Url = BuildRemisionUrl(ticket) };
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Reporte listo.", data });
    }

    /*
      ENTREGA EL PDF DE LA REMISION

      Anonimo a proposito: lo abre una pestana nueva del navegador, que no manda
      el encabezado Authorization. Lo que autoriza es el pase firmado del
      parametro "t", que trae las ventas, el usuario y una vigencia corta.

      El PDF se genera al vuelo, no se guarda: el papel tiene que reflejar el
      estado de la venta en el momento en que se pide (cancelaciones,
      devoluciones y pagos cambian despues de emitida).
    */
    /*
      DOS DIRECCIONES PARA LA MISMA HOJA, Y LA CORTA ES LA BUENA

      Lo que se ve en la barra cuando alguien abre una remision era
      "/api/ventas/consulta/reporte/pdf?t=...": el organigrama del backend
      colgado en la pantalla de una persona que solo queria un papel. "/remision"
      dice lo mismo y cabe en la barra junto al pase.

      La ruta empieza con "/" para salirse del prefijo del controlador; no se
      mueve de archivo porque es la MISMA accion, no una copia.

      La vieja se queda por una razon concreta: el pase dura diez minutos, asi
      que durante un despliegue hay pestañas ya abiertas apuntando a la
      direccion anterior. Quitarla les daria 404 a media jornada.
    */
    [HttpGet("/remision")]
    [HttpGet("reporte/pdf")]
    [AllowAnonymous]
    public async Task<IActionResult> ReportePdf([FromQuery] string? t, CancellationToken ct)
    {
        var llave = _configuration["Jwt:Key"] ?? "";
        var pase = RemisionTicket.Validar(llave, t);

        if (pase is null)
            return BadRequest("El enlace del reporte no es valido o ya vencio. Vuelve a generarlo desde la consulta.");

        RemisionPdf pdf;
        try
        {
            /*
              El equipo sale del PASE y no de esta peticion: este GET es
              anonimo, asi que aqui ya no se sabe quien lo pidio y
              GetUsername devolveria el nombre de la maquina del SERVIDOR.
              Ese dato lo guarda el procedimiento en Ventas y en Pedidos: dejar
              ahi "el servidor" seria borrar el rastro, no guardarlo.
            */
            pdf = await _remisiones.GenerarAsync(
                pase.IdsVenta,
                pase.IdUsuario,
                pase.PrimerImpresion,
                pase.Reimpresion,
                pase.Equipo,
                ct);
        }
        catch (InvalidOperationException ex)
        {
            /*
              El motivo se devuelve en texto y no como json: esto se ve en una
              pestana del navegador, no lo consume codigo.
            */
            return BadRequest(ex.Message);
        }

        Response.Headers["Cache-Control"] = "no-store";

        // d=1 descarga con nombre de archivo; d=0 se ve dentro del navegador.
        return pase.Descargar == 1
            ? File(pdf.Contenido, "application/pdf", pdf.NombreArchivo)
            : File(pdf.Contenido, "application/pdf");
    }

    /*
      La direccion se arma con el esquema, host y ruta base de la peticion —no
      con una configuracion— para que sirva igual en local, detras de un proxy y
      en produccion, sin una clave mas que mantener. Mismo patron que
      ReportesVentasController.BuildReportesV3WUrl.
    */
    private string BuildRemisionUrl(string ticket)
    {
        var pathBase = Request.PathBase.HasValue ? Request.PathBase.Value : "";
        return $"{Request.Scheme}://{Request.Host}{pathBase}/remision?t={Uri.EscapeDataString(ticket)}";
    }
}
