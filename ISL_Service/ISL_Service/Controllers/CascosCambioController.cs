using ISL_Service.Application.DTOs.CascosCambio;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using ISL_Service.Infrastructure.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Cascos a cambio: la cuenta de cascos y dinero entre Tauro y Zaragoza.
///
/// QUE REEMPLAZA
/// Una hoja de Excel que se lleva a mano ("CASCOS JAZMIN ZARAGOZA"), con los
/// precios tecleados abajo y los importes escritos renglon por renglon. Aqui se
/// capturan la fecha, la remision, la persona y las PIEZAS; todo lo demas —
/// precios, importes, totales y el saldo que corre — se calcula. Esa es la
/// diferencia entre las dos cosas, no la pantalla.
///
/// LOS DOS PRECIOS
/// El mismo casco vale distinto para cada empresa (400 contra 380, 520 contra
/// 495, ... verificado en las dos bases). Cada movimiento se guarda valuado de
/// los dos lados y el corte dice cuanto falta o sobra. Esa conciliacion es el
/// modulo; lo demas es captura.
///
/// PERMISOS SEPARADOS WEB Y MOVIL, como en Empleados y Prestamos: el telefono
/// se presta y la computadora de la oficina no.
/// </summary>
[ApiController]
[Route("api/cascos-cambio")]
[Authorize]
public class CascosCambioController : PermisoControllerBase
{
    private static readonly string[] PermisosVer = { "cascos_cambio.ver", "app_movil.cascos_cambio.ver" };
    private static readonly string[] PermisosCrear = { "cascos_cambio.crear", "app_movil.cascos_cambio.crear" };
    private static readonly string[] PermisosCancelar = { "cascos_cambio.cancelar", "app_movil.cascos_cambio.cancelar" };

    private readonly ICascosCambioService _service;

    /* Solo para la llave con la que se firma el pase del reporte. */
    private readonly IConfiguration _configuration;

    public CascosCambioController(
        ICascosCambioService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService,
        IConfiguration configuration)
        : base(currentUser, permissionService)
    {
        _service = service;
        _configuration = configuration;
    }

    /// <summary>
    /// Los tipos de casco con sus dos precios (el propio y el de la
    /// contraparte). El front pinta las columnas de captura con esto: la lista
    /// de tipos NO esta escrita en el front.
    /// </summary>
    [HttpGet("tipos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Tipos(CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.ConsultarTiposAsync(ct);
        return Ok(new { ok = true, message = "Tipos consultados.", data });
    }

    /// <summary>
    /// Movimientos del periodo, cada uno con el saldo que lleva la cuenta hasta
    /// ese renglon, mas el corte del periodo.
    /// </summary>
    /// <param name="tipoMovimiento">1 entrega, 2 pedido, 3 pago, 4 saldo inicial. Sin valor: todos.</param>
    /// <param name="incluirCancelados">
    /// Por omision NO. Un cancelado se ve solo si se pide: esta ahi para poder
    /// explicarle a la contraparte una remision que ella si tiene, no para
    /// estorbar en la lista de todos los dias.
    /// </param>
    [HttpGet("movimientos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Movimientos(
        [FromQuery] DateTime? fechaInicio,
        [FromQuery] DateTime? fechaFin,
        [FromQuery] int? tipoMovimiento,
        [FromQuery] bool incluirCancelados = false,
        /*
          Por cual de las dos fechas se filtra. false —el valor por omision— es
          la fecha que se teclea al capturar, que es como se ha comportado
          siempre: quien no mande el parametro ve lo mismo que antes.
        */
        [FromQuery] bool filtrarPorRegistro = false,
        CancellationToken ct = default)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.ConsultarMovimientosAsync(fechaInicio, fechaFin, tipoMovimiento, incluirCancelados, filtrarPorRegistro, ct);
        return Ok(new
        {
            ok = true,
            message = "Movimientos consultados.",
            data = data.movimientos,
            corte = data.corte,
            total = data.movimientos.Count
        });
    }

    /// <summary>
    /// Renglones de un movimiento: piezas por tipo, los dos precios y los dos
    /// importes.
    /// </summary>
    [HttpGet("movimientos/{id:int}/detalle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Detalle([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.ConsultarDetalleAsync(id, ct);
        return Ok(new { ok = true, message = "Detalle consultado.", data });
    }

    /// <summary>
    /// Piezas y dinero por tipo en el periodo: es el pie de la hoja de Excel,
    /// calculado.
    /// </summary>
    [HttpGet("resumen")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Resumen(
        [FromQuery] DateTime? fechaInicio,
        [FromQuery] DateTime? fechaFin,
        CancellationToken ct = default)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.ConsultarResumenAsync(fechaInicio, fechaFin, ct);
        return Ok(new { ok = true, message = "Resumen consultado.", data });
    }

    /// <summary>
    /// Registra un movimiento.
    ///
    /// El body NO lleva precios ni importes de cascos: aunque los mandara, se
    /// ignoran. El importe solo se captura cuando el movimiento es de dinero
    /// (pago o saldo inicial), porque ahi no hay piezas de donde sacarlo.
    /// </summary>
    /// <response code="201">Movimiento registrado (puede traer una advertencia)</response>
    /// <response code="400">Datos invalidos o regla de negocio</response>
    [HttpPost("movimientos")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Crear([FromBody] CrearMovimientoCascoCambioRequest? request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { ok = false, message = "Body requerido." });

        var sinPermiso = await ExigirPermisoAsync(PermisosCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.CrearAsync(request, Equipo(), ct);
        var message = string.IsNullOrWhiteSpace(data.Advertencia) ? "Movimiento registrado." : data.Advertencia;

        return StatusCode(StatusCodes.Status201Created, new { ok = true, message, data });
    }

    /// <summary>
    /// Cancela un movimiento. No se borra nunca: la contraparte tiene esa
    /// remision en su hoja y un renglon que desaparece no se puede explicar.
    /// </summary>
    [HttpPost("movimientos/{id:int}/cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cancelar(
        [FromRoute] int id,
        [FromBody] CancelarMovimientoCascoCambioRequest? request,
        CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosCancelar, ct);
        if (sinPermiso != null) return sinPermiso;

        await _service.CancelarAsync(id, request?.Motivo, Equipo(), ct);
        return Ok(new { ok = true, message = "Movimiento cancelado." });
    }

    /// <summary>
    /// Vuelve a copiar los precios de la contraparte desde SU base.
    ///
    /// Se dispara a mano y no en cada consulta a proposito: si los precios se
    /// releyeran solos, el dia que la otra empresa suba los suyos cambiarian
    /// los numeros de una conciliacion que ya se habia firmado. Los movimientos
    /// ya capturados no se tocan: cada uno guarda la foto de su precio.
    /// </summary>
    [HttpPost("precios-contraparte/sincronizar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SincronizarPrecios(CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.SincronizarPreciosContraparteAsync(ct);
        return Ok(new { ok = data.Ok, message = data.Mensaje, data });
    }

    /// <summary>
    /// Pide el reporte del periodo y devuelve la direccion donde esta el PDF.
    ///
    /// DOS PASOS Y NO UNO, igual que la remision de Ventas: esta llamada va
    /// autenticada y solo entrega un enlace; el PDF lo baja la pestaña nueva.
    /// Devolver aqui el PDF obligaria a la pantalla a cargarse los megabytes en
    /// memoria para volver a soltarlos, y el navegador ya sabe abrir una
    /// direccion.
    /// </summary>
    [HttpPost("reporte")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Reporte(
        [FromQuery] DateTime? fechaInicio,
        [FromQuery] DateTime? fechaFin,
        /* "todos", "activos" o "cancelados". Lo que llegue distinto se toma
           como "todos" al firmar el pase. */
        [FromQuery] string estatus = "todos",
        [FromQuery] bool filtrarPorRegistro = false,
        CancellationToken ct = default)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        var llave = _configuration["Jwt:Key"] ?? "";
        var ticket = ReporteUsadosTicket.Firmar(
            llave, fechaInicio?.Date, fechaFin?.Date, estatus, filtrarPorRegistro,
            CurrentUser.GetLegacyUserId(User));

        /*
          La direccion se arma con el esquema, host y ruta base de ESTA peticion
          y no con una configuracion: asi sirve igual en local, detras del proxy
          y en produccion, sin una clave mas que mantener y que se pueda quedar
          apuntando al sitio equivocado.
        */
        var pathBase = Request.PathBase.HasValue ? Request.PathBase.Value : "";
        var url = $"{Request.Scheme}://{Request.Host}{pathBase}/usados?t={Uri.EscapeDataString(ticket)}";

        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Reporte listo.", data = new { url } });
    }

    /// <summary>
    /// Entrega el PDF.
    ///
    /// ANONIMO A PROPOSITO: lo abre una pestaña nueva del navegador, que no
    /// manda el encabezado Authorization. Lo que autoriza es el pase firmado
    /// del parametro "t", que trae el periodo, el usuario y una vigencia corta.
    ///
    /// La direccion es corta —"/usados"— y no el organigrama del backend: lo
    /// que se ve en la barra del navegador lo mira una persona que solo queria
    /// un papel. Empieza con "/" para salirse del prefijo del controlador.
    ///
    /// El PDF se genera al vuelo y no se guarda: una cancelacion posterior
    /// tiene que verse la proxima vez que se imprima.
    /// </summary>
    [HttpGet("/usados")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReportePdf([FromQuery] string? t, CancellationToken ct)
    {
        var pase = ReporteUsadosTicket.Validar(_configuration["Jwt:Key"] ?? "", t);
        if (pase is null)
            /* En texto y no en json: esto se ve en una pestaña del navegador, no
               lo consume codigo. */
            return BadRequest("El enlace del reporte no es válido o ya venció. Vuelve a generarlo desde la pantalla.");

        /*
          EL PROCEDIMIENTO SOLO SABE DECIR DOS COSAS: con cancelados o sin
          ellos. Para "solo cancelados" se le piden TODOS y se recorta aqui;
          pedirle una tercera opcion significaria tocar el procedimiento para
          una pregunta que no cambia como se consulta, solo que se enseña.

          El saldo se calcula ANTES del recorte, y eso importa: el saldo corre
          sobre la cuenta entera, asi que recortando primero saldria un saldo
          que no existe en ningun lado.
        */
        var soloCancelados = pase.Estatus == "cancelados";
        var data = await _service.ConsultarMovimientosAsync(
            pase.Desde, pase.Hasta, null, pase.Estatus != "activos", pase.PorRegistro, ct);

        var movimientos = soloCancelados
            ? data.movimientos.Where(m => m.estatus == 2).ToList()
            : data.movimientos;

        var (logo, empresa) = await _service.ConsultarMarcaParaReporteAsync(ct);

        var html = UsadosHtmlBuilder.Construir(
            movimientos, data.corte, pase.Desde, pase.Hasta, pase.PorRegistro, pase.Estatus,
            logo);

        /*
          Apaisado: son siete columnas y en vertical el chofer y la remision se
          parten en dos renglones. El titulo es lo que se lee en la pestaña del
          visor — sin el, el navegador usa el ultimo pedazo de la direccion y la
          pestaña dice "usados" con el icono generico.
        */
        var pdf = await WkhtmltopdfHtmlPdfRenderer.RenderAsync(
            html, "Landscape", "Usados a cambio", ct,
            new WkhtmltopdfHtmlPdfRenderer.Opciones
            {
                /*
                  15 mm parejos: los del renderer (2 izq contra 8 der) son de la
                  remision, y en una tabla de siete columnas dejan el contenido
                  pegado al filo izquierdo y descentrado. El diseño pide 0.6 in;
                  15 mm es eso mismo redondeado al milimetro, que es la unidad
                  que entiende wkhtmltopdf.
                */
                MargenSuperior = 15,
                MargenInferior = 15,
                MargenIzquierdo = 15,
                MargenDerecho = 15,
                /*
                  El logo entra a 3015 px de ancho y con el valor por omision
                  sale reducido a 520: a ese tamaño los contornos blancos de las
                  letras se promedian con el fondo y la marca se ve lavada, como
                  si fuera transparente.

                  Con 900 entra a unos 1400 px para dibujarse a 152: sobra
                  resolucion para cualquier zoom y para la impresora, y el PDF
                  pesa una cuarta parte que con el maximo, que embebia el
                  archivo entero de 3015 px sin necesidad.
                */
                ImagenDpi = 900,
                /*
                  EL PIE VA EN LA FRANJA DE LA HOJA, no al final del cuerpo.

                  Siendo un parrafo mas del documento, su margen podia no caber
                  en lo que quedaba de pagina y arrastraba una hoja entera en
                  blanco para enseñar una sola linea. Aqui vive fuera del flujo:
                  no empuja nada y sale en TODAS las hojas, que es justo lo que
                  se espera de un "impreso el".

                  Y de paso el numero de pagina, que en un reporte de catorce
                  hojas entregado a otra empresa es lo que permite decir "mira
                  la 7" y comprobar que no falta ninguna.
                */
                PieIzquierdo = UsadosHtmlBuilder.TextoDePie(empresa),
                PieDerecho = "[page] / [topage]",
                PieConLinea = true
            });

        Response.Headers["Cache-Control"] = "no-store";
        return File(pdf, "application/pdf");
    }
}
