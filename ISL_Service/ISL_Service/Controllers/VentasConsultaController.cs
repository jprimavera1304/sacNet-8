using ISL_Service.Application.DTOs.VentasConsulta;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISL_Service.Application.Security;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/ventas/consulta")]
[Authorize]
public class VentasConsultaController : PermisoControllerBase
{
    /// <summary>
    /// El permiso que separa "puedo consultar lo de cualquiera" de "solo lo
    /// mio": es btnBuscar de Consultar ventas en Mac31. Ver
    /// VentasConsultaService.AcotarFiltroDeUsuario.
    /// </summary>
    private static readonly string[] PermisoConsultarDeOtros =
    {
        "ventas.consultar", "ventas.ver_modulo",
        // El telefono tiene la suya: sin esto, un usuario de SOLO movil quedaba
        // amarrado a sus propios renglones sin que nadie se lo hubiera negado.
        // No es un 403, es una lista a medias, que se diagnostica peor.
        "app_movil.ventas.consulta.ver_de_otros"
    };

    /*
      LAS CUATRO ACCIONES DEL REPORTE, CADA UNA CON SU CLAVE.

      Estos endpoints no comprobaban NADA mas que estar autenticado -medido el
      2026-10-12-, asi que cualquiera con cuenta podia imprimir y REIMPRIMIR
      remisiones por API. Reimprimir es justo lo que Mac31 protege con
      contrasena, y el front si escondia los botones: la decision vivia entera
      en el navegador y se saltaba con un curl.

      Las claves no son nuevas ni inventadas: son los botones btnPantalla,
      btnImprimir, btnReimprimir y btnPedidosFaltantes de CONSULTA DE VENTAS,
      que el puente ya trae de Mac31. Exigirlas es hacer que el web obedezca lo
      que Mac31 ya decide, no cerrarle la puerta a nadie que alla la tenga
      abierta. Medido en las dos empresas antes de ponerlo.

      Y VAN EN PAR CON LA CLAVE DEL MOVIL, como el resto del backend
      (PermisoControllerBase), porque la app llama a estos tres mismos
      endpoints -lib/modules/verticals/ventas_consulta.dart-. Sin el par, cerrar
      esto le apagaria la impresion a la app, que es el error contrario y
      igual de malo. El dia que el movil tenga claves por accion, el par se
      parte; hoy el movil no comprueba nada aqui, asi que el par no afloja: lo
      que habia era cero.
    */
    /*
      LAS TRES DE IMPRIMIR, CADA UNA CON SU CLAVE DE MOVIL.

      Aqui las tres aceptaban `app_movil.ventas.consulta.ver`, o sea que dar
      "ver" en el telefono daba tambien imprimir y reimprimir. En Mac31 y en el
      web son tres permisos distintos -reimprimir deja volver a sacar un papel
      que ya se entrego- y ahora tambien lo son en el telefono.

      Esto QUITA algo a quien hoy imprime con la clave de ver, asi que el script
      de traspaso (Querrys/48) le da las tres a quien ya tenga la de ver.
    */
    private static readonly string[] PermisoPantalla =
        { "ventas.pantalla", "app_movil.ventas.consulta.pantalla" };

    private static readonly string[] PermisoImprimir =
        { "ventas.imprimir", "app_movil.ventas.consulta.imprimir" };

    private static readonly string[] PermisoReimprimir =
        { "ventas.reimprimir", "app_movil.ventas.consulta.reimprimir" };

    private static readonly string[] PermisoPedidosFaltantes =
        { "ventas.pedidos.faltantes", "app_movil.ventas.consulta.ver" };

    /*
      LOS DOS SIMULADORES, CADA UNO CON SU CLAVE Y YA CON SU PAR DE MOVIL.

      Son btnSimular y btnSimularSin de CONSULTA DE VENTAS, y el puente ya
      traia sus claves de Mac31 (VentasLegacyPermissionMap).

      AQUI DECIA QUE NO LLEVABAN PAR DE MOVIL, Y ERA CIERTO HASTA QUE LA APP
      LLAMO. El motivo de entonces era que la app no pedia estos endpoints, asi
      que el par no protegia nada y en cambio le habria abierto la simulacion a
      cualquiera que pudiera entrar a ventas desde el telefono. Desde el
      2026-10-14 la app SI los pide -`VentasSimulacionScreen`,
      lib/modules/verticals/ventas_simulacion.dart- y sin el par un usuario con
      solo claves `app_movil.*` tendria la pantalla y no podria usarla.

      Las parejas son PROPIAS de simular ('...simular.con_remision' /
      '...simular.sin_remision'), no la clave general del modulo: el canal movil
      tiene sus permisos y esto no se concede de regalo con "ver ventas".

      Cual se exige lo decide el SIGNO de lo que se pide, que es la misma
      frontera que en Mac31: un pedido sin emitir es "sin remision", una venta
      emitida es "con remision". Ver PermisoDeLaSimulacion.
    */
    private static readonly string[] PermisoSimularSinRemision =
        { "ventas.simular.sin_remision", "app_movil.ventas.simular.sin_remision" };

    private static readonly string[] PermisoSimularConRemision =
        { "ventas.simular.con_remision", "app_movil.ventas.simular.con_remision" };

    /// <summary>
    /// La clave que exige esa accion del reporte.
    /// </summary>
    /// <remarks>
    /// La accion llega en el cuerpo (VentasReporteRequest.Accion) y por defecto
    /// es "pantalla". Lo que NO se reconoce cae en la mas restrictiva de las
    /// tres -reimprimir- a proposito: una accion nueva que nadie haya mapeado
    /// aqui no puede acabar siendo la que no pide permiso. Fallar cerrado.
    /// </remarks>
    private static string[] PermisoDeLaAccion(string? accion) =>
        (accion ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "pantalla" => PermisoPantalla,
            "imprimir" => PermisoImprimir,
            "" => PermisoPantalla,
            _ => PermisoReimprimir
        };

    private readonly IVentasConsultaService _service;
    private readonly IRemisionImpresionService _remisiones;
    private readonly ISimulacionImpresionService _simulaciones;
    private readonly ISimulacionDatosService _simulacionDatos;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IConfiguration _configuration;

    public VentasConsultaController(
        IVentasConsultaService service,
        IRemisionImpresionService remisiones,
        ISimulacionImpresionService simulaciones,
        ISimulacionDatosService simulacionDatos,
        ICurrentUserAccessor currentUserAccessor,
        IPermissionService permissionService,
        IConfiguration configuration)
        : base(currentUserAccessor, permissionService)
    {
        _service = service;
        _remisiones = remisiones;
        _simulaciones = simulaciones;
        _simulacionDatos = simulacionDatos;
        _currentUserAccessor = currentUserAccessor;
        _configuration = configuration;
    }

    /*
      QUIEN PREGUNTA SALE DEL TOKEN; "DE QUIEN QUIERO VER" ES OTRA COSA.

      @IDUsuarioActual (el candado de almacen de los procedimientos) ya no se
      lee del cuerpo: lo pone el servicio con el id del token, siempre.

      @IDUsuario si es un filtro de pantalla legitimo, pero apuntar al de OTRO
      necesita permiso. Esto resuelve los dos casos:

        - sin permiso y pidiendo explicitamente otro usuario -> 403, para que se
          vea que se nego y no parezca que ese usuario no tiene ventas;
        - sin permiso y sin pedir a nadie -> el servicio lo acota a los suyos.
          Antes, un cliente movil que omitia `SoloMisPedidos` veia los pedidos
          de todos.
    */
    private async Task<(bool PuedeVerDeOtros, IActionResult? Negado)> ResolverAlcanceAsync(
        VentasConsultaRequest request, int idUsuarioToken, CancellationToken ct)
    {
        var permisos = await LeerPermisosAsync(ct);
        if (!permisos.TokenValido)
            return (false, Unauthorized(new { ok = false, message = "Token invalido." }));

        if (permisos.Tiene(PermisoConsultarDeOtros))
            return (true, null);

        if (request.IDUsuario > 0 && request.IDUsuario != idUsuarioToken)
        {
            return (false, StatusCode(StatusCodes.Status403Forbidden, new
            {
                ok = false,
                message = "No tienes permiso para consultar las ventas de otro usuario."
            }));
        }

        return (false, null);
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
        var req = request ?? new VentasConsultaRequest();
        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var (puedeVerDeOtros, negado) = await ResolverAlcanceAsync(req, idUsuario, ct);
        if (negado is not null) return negado;

        var data = await _service.ConsultarRemisionesAsync(req, idUsuario, puedeVerDeOtros, ct);
        return Ok(new { ok = true, message = "Remisiones consultadas.", data });
    }

    [HttpPost("pedidos")]
    public async Task<IActionResult> Pedidos([FromBody] VentasConsultaRequest? request, CancellationToken ct)
    {
        var req = request ?? new VentasConsultaRequest();
        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var (puedeVerDeOtros, negado) = await ResolverAlcanceAsync(req, idUsuario, ct);
        if (negado is not null) return negado;

        var data = await _service.ConsultarPedidosAsync(req, idUsuario, puedeVerDeOtros, ct);
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
        var req = request ?? new VentasConsultaRequest();
        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var (puedeVerDeOtros, negado) = await ResolverAlcanceAsync(req, idUsuario, ct);
        if (negado is not null) return negado;

        var data = await _service.ConsultarPagosAsync(req, idUsuario, puedeVerDeOtros, ct);
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
        var negado = await ExigirPermisoAsync(PermisoPedidosFaltantes, ct);
        if (negado is not null) return negado;

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
        var negado = await ExigirPermisoAsync(PermisoDeLaAccion(request?.Accion), ct);
        if (negado is not null) return negado;

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
        /*
          ESTE ES EL QUE DE VERDAD HAY QUE CERRAR.

          El GET /remision es anonimo a proposito y esta bien que lo sea: lo
          que autoriza alli es el pase firmado. Asi que la comprobacion tiene
          que estar en el endpoint que EMITE el pase, que es este.
        */
        var negado = await ExigirPermisoAsync(PermisoDeLaAccion(request?.Accion), ct);
        if (negado is not null) return negado;

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
      EL PAPEL DE LA SIMULACION: EL REPORTE 1175

      Dos endpoints por lo mismo que la remision: el POST comprueba el permiso y
      firma un pase, y el GET anonimo entrega el PDF a la pestana que ya se
      abrio. Lo que autoriza es el pase.

      EL SIGNO LO PONE EL SERVIDOR. Quien llama dice "el pedido 216063" o "la
      venta 142407"; aqui se convierte a lo que entiende
      `sp_n_VentasInformacion`, que lee un pedido sin emitir cuando el IDVenta
      llega en negativo. Que el cliente no pueda mandar el id ya firmado tiene
      consecuencia: no hay forma de pedir "la venta -X" para colarse por la
      puerta del permiso de la otra.
    */
    /*
      LO QUE PINTA LA PANTALLA DE SIMULAR CON REMISION

      Las cuatro lecturas de `VentasSimular._Load` en una llamada. La quinta de
      alla -el alta del esquema de prueba- no esta aqui porque ESCRIBE y tiene
      su propio endpoint. Ver ISimulacionDatosService.

      Se pide con el descuento que se este probando; con 0 contesta la cuenta
      tal como esta hoy, que es con lo que abre la pantalla.
    */
    [HttpPost("simulacion/datos")]
    public async Task<IActionResult> SimulacionDatos([FromBody] VentasSimulacionRequest? request, CancellationToken ct)
    {
        var negado = await ExigirPermisoAsync(PermisoSimularConRemision, ct);
        if (negado is not null) return negado;

        var peticion = request ?? new VentasSimulacionRequest();

        try
        {
            var data = await _simulacionDatos.ConsultarAsync(peticion.IdVenta, peticion.IdDescuento, ct);
            Response.Headers["Cache-Control"] = "no-store";
            return Ok(new { ok = true, message = "Datos de la simulacion.", data });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { ok = false, message = ex.Message });
        }
    }

    [HttpPost("simulacion")]
    public async Task<IActionResult> Simulacion([FromBody] VentasSimulacionRequest? request, CancellationToken ct)
    {
        var peticion = request ?? new VentasSimulacionRequest();

        var idVenta = peticion.IdPedido > 0 ? peticion.IdPedido * -1 : peticion.IdVenta;
        if (idVenta == 0)
            return BadRequest(new { ok = false, message = "No se indico que simular." });

        var negado = await ExigirPermisoAsync(
            idVenta < 0 ? PermisoSimularSinRemision : PermisoSimularConRemision, ct);
        if (negado is not null) return negado;

        var llave = _configuration["Jwt:Key"] ?? "";
        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);

        var ticket = SimulacionTicket.Firmar(
            llave, idVenta, idUsuario, peticion.IdDescuento, peticion.ConCascos);

        var pathBase = Request.PathBase.HasValue ? Request.PathBase.Value : "";
        var url = $"{Request.Scheme}://{Request.Host}{pathBase}/simulacion?t={Uri.EscapeDataString(ticket)}";

        var data = new VentasReporteResponse { Ok = true, Url = url };
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Simulacion lista.", data });
    }

    /// Entrega el PDF de la simulacion. Anonimo por la misma razon que
    /// /remision: una pestana nueva no manda Authorization.
    [HttpGet("/simulacion")]
    [AllowAnonymous]
    public async Task<IActionResult> SimulacionPdf([FromQuery] string? t, CancellationToken ct)
    {
        var llave = _configuration["Jwt:Key"] ?? "";
        var pase = SimulacionTicket.Validar(llave, t);

        if (pase is null)
            return BadRequest("El enlace de la simulacion no es valido o ya vencio. Vuelve a generarlo desde la pantalla.");

        SimulacionPdf pdf;
        try
        {
            pdf = await _simulaciones.GenerarAsync(
                pase.IdVenta, pase.IdUsuario, pase.IdDescuento, pase.ConCascos, ct);
        }
        catch (InvalidOperationException ex)
        {
            // Texto y no json: esto se ve en una pestana, no lo consume codigo.
            return BadRequest(ex.Message);
        }

        Response.Headers["Cache-Control"] = "no-store";

        /*
          Siempre dentro del navegador, nunca como descarga: en Mac31 la
          simulacion sale a PANTALLA y no tiene boton de descargar
          (`Descargar = 0` en las dos, Ventas.cs:3971 y VentasSimular.cs:525).
          Un archivo "simulacion.pdf" en la carpeta de descargas de alguien es
          justo lo que no debe quedar de una cuenta que no se emitio.
        */
        return File(pdf.Contenido, "application/pdf");
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
