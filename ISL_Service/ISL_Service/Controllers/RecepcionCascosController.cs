using ISL_Service.Application.DTOs.RecepcionCascos;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using ISL_Service.Infrastructure.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Recepcion de cascos usados (TAURO).
///
/// QUE REEMPLAZA
/// La hoja que hoy se llena a mano: "Relacion de cascos usados que entregan del
/// dia [FECHA]". Cuantos cascos usados entrego cada repartidor, separados por
/// tamano, mas los apartados de tiendas y galleros. Aqui se capturan la fecha,
/// de quien se recibieron y las PIEZAS por tamano; el total lo suma la base.
///
/// LO QUE ESTE MODULO NO HACE, Y ES A PROPOSITO
/// No mueve inventario ni saldos. En la base no hay nada que diga si el casco
/// que entrega un repartidor venia de un cliente, y no se invento: registrar lo que
/// llega es util por si mismo, y el dia que el negocio defina a donde se aplica
/// se agrega con ese dato en la mano. Ver el comentario del script
/// 62 Recepcion De Cascos Usados.
///
/// PERMISOS SEPARADOS WEB Y MOVIL, como en cascos a cambio y en empleados: el
/// telefono se presta y la computadora de la oficina no. Cada operacion tiene
/// su clave, y las dos se aceptan en el mismo endpoint porque el endpoint es
/// uno: quien decide que puede cada canal es el reparto de claves, no el codigo.
/// </summary>
[ApiController]
[Route("api/recepcion-cascos")]
[Authorize]
public class RecepcionCascosController : PermisoControllerBase
{
    private static readonly string[] PermisosVer =
        { "recepcion_cascos.ver", "app_movil.recepcion_cascos.ver" };
    private static readonly string[] PermisosCrear =
        { "recepcion_cascos.crear", "app_movil.recepcion_cascos.crear" };
    private static readonly string[] PermisosEditar =
        { "recepcion_cascos.editar", "app_movil.recepcion_cascos.editar" };
    private static readonly string[] PermisosCancelar =
        { "recepcion_cascos.cancelar", "app_movil.recepcion_cascos.cancelar" };
    private static readonly string[] PermisosImprimir =
        { "recepcion_cascos.imprimir", "app_movil.recepcion_cascos.imprimir" };

    /* El estatus de una recepcion cancelada, como lo guarda la tabla. */
    private const int EstatusCancelada = 2;
    /*
      El historial tiene clave propia y SOLO del web: es una pantalla de
      revision, no de captura, y en el telefono no existe. Una clave movil que
      no abre nada promete algo que no ocurre.
    */
    private static readonly string[] PermisosHistorial =
        { "recepcion_cascos.historial" };

    private readonly IRecepcionCascosService _service;

    /* Solo para la llave con la que se firma el pase del reporte. */
    private readonly IConfiguration _configuration;

    public RecepcionCascosController(
        IRecepcionCascosService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService,
        IConfiguration configuration)
        : base(currentUser, permissionService)
    {
        _service = service;
        _configuration = configuration;
    }

    /// <summary>
    /// Los tamanos de casco, en el orden de la hoja. El front pinta las
    /// columnas de captura con esto: la lista NO esta escrita en el front.
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

    /// <summary>Repartidores activos, para elegir quien entrego.</summary>
    [HttpGet("repartidores")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Repartidores(CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.ConsultarRepartidoresAsync(ct);
        return Ok(new { ok = true, message = "Repartidores consultados.", data });
    }

    /// <summary>
    /// Las recepciones del periodo.
    /// </summary>
    /// <param name="idRepartidor">Sin valor: todos.</param>
    /// <param name="incluirCanceladas">
    /// Por omision NO. Una cancelada se ve solo si se pide: esta ahi para poder
    /// explicar lo que paso, no para estorbar en la lista de todos los dias.
    /// </param>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Consultar(
        [FromQuery] DateTime? fechaInicio,
        [FromQuery] DateTime? fechaFin,
        [FromQuery] int? idRepartidor,
        [FromQuery] int? estatus,
        [FromQuery] bool incluirCanceladas = false,
        CancellationToken ct = default)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.ConsultarAsync(
            fechaInicio, fechaFin, idRepartidor, estatus, incluirCanceladas, ct);

        return Ok(new { ok = true, message = "Recepciones consultadas.", data, total = data.Count });
    }

    /// <summary>
    /// El desglose de una recepcion: TODOS los tamanos, con cero en los que no
    /// se capturaron, para que la pantalla de modificar pinte la rejilla
    /// completa sin cruzar dos listas.
    /// </summary>
    [HttpGet("{id:int}/detalle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Detalle([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        if (await _service.EstatusDeAsync(id, ct) == 0)
            return NotFound(new { ok = false, message = "Esa recepcion no existe." });

        var data = await _service.ConsultarDetalleAsync(id, ct);
        return Ok(new { ok = true, message = "Detalle consultado.", data });
    }

    /// <summary>
    /// El historial de una recepcion: que cambio, quien y cuando.
    /// </summary>
    [HttpGet("{id:int}/historial")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Historial([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosHistorial, ct);
        if (sinPermiso != null) return sinPermiso;

        if (await _service.EstatusDeAsync(id, ct) == 0)
            return NotFound(new { ok = false, message = "Esa recepcion no existe." });

        var data = await _service.ConsultarBitacoraAsync(id, ct);
        return Ok(new { ok = true, message = "Historial consultado.", data });
    }

    /// <summary>Registra una recepcion.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Crear(
        [FromBody] CrearRecepcionCascoRequest request, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        if (request == null)
            return BadRequest(new { ok = false, message = "No llego la recepcion." });

        var resultado = await _service.CrearAsync(
            request, IdUsuarioLegacy(), Equipo(), Canal(), ct);

        if (!resultado.ok)
            return BadRequest(new { ok = false, message = resultado.mensaje });

        return Ok(new
        {
            ok = true,
            message = resultado.mensaje,
            data = new { idRecepcion = resultado.idRecepcion },
            advertencia = resultado.advertencia
        });
    }

    /// <summary>Corrige una recepcion. El historial conserva que cambio.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Actualizar(
        [FromRoute] int id,
        [FromBody] ActualizarRecepcionCascoRequest request,
        CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        if (request == null)
            return BadRequest(new { ok = false, message = "No llego la recepcion." });

        /*
          El id de la ruta manda. Si el cuerpo trae otro, se para en vez de
          elegir uno: los dos vienen del mismo cliente y que no coincidan
          significa que algo se armo mal, no que haya que adivinar. Es el mismo
          criterio que ya sigue la pantalla de permisos.
        */
        if (request.IdRecepcion != 0 && request.IdRecepcion != id)
            return UnprocessableEntity(new
            {
                ok = false,
                message = "La recepcion de la direccion no es la misma que la del cuerpo."
            });

        request.IdRecepcion = id;

        if (await _service.EstatusDeAsync(id, ct) == 0)
            return NotFound(new { ok = false, message = "Esa recepcion no existe." });

        var resultado = await _service.ActualizarAsync(
            request, IdUsuarioLegacy(), Equipo(), Canal(), ct);

        if (!resultado.ok)
            return BadRequest(new { ok = false, message = resultado.mensaje });

        return Ok(new
        {
            ok = true,
            message = resultado.mensaje,
            data = new { idRecepcion = resultado.idRecepcion },
            advertencia = resultado.advertencia
        });
    }

    /// <summary>
    /// Cancela una o varias recepciones en una sola operacion: o todas o
    /// ninguna. El historial conserva una entrada por cada una.
    /// </summary>
    [HttpPost("cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cancelar(
        [FromBody] CancelarRecepcionesCascoRequest request, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosCancelar, ct);
        if (sinPermiso != null) return sinPermiso;

        if (request == null)
            return BadRequest(new { ok = false, message = "No llego nada que cancelar." });

        var resultado = await _service.CancelarAsync(
            request, IdUsuarioLegacy(), Equipo(), Canal(), ct);

        if (!resultado.ok)
            return BadRequest(new { ok = false, message = resultado.mensaje });

        return Ok(new
        {
            ok = true,
            message = resultado.mensaje,
            data = new { canceladas = resultado.canceladas }
        });
    }

    /// <summary>
    /// Pide el reporte. Devuelve la direccion donde esta el PDF, firmada y con
    /// caducidad: el papel lo abre una pestana nueva, y una pestana nueva no
    /// manda el encabezado de autorizacion.
    /// </summary>
    [HttpPost("reporte")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reporte(
        [FromQuery] DateTime? fechaInicio,
        [FromQuery] DateTime? fechaFin,
        [FromQuery] int? idRepartidor,
        /*
          EL PAPEL SALE CON LOS FILTROS QUE SE ELIGIERON, Y LAS CANCELADAS SON
          UNO DE ELLOS. La pantalla puede estar ensenandolas y el papel salia
          sin ellas: dos respuestas distintas a la misma pregunta, y la de
          papel es la que se archiva.

          Se escribe igual que en la consulta de la lista —`incluirCanceladas`—
          porque es el mismo filtro, y de fabrica va apagado: quien no lo pida
          recibe el reporte operativo de siempre.
        */
        [FromQuery] bool incluirCanceladas = false,
        CancellationToken ct = default)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosImprimir, ct);
        if (sinPermiso != null) return sinPermiso;

        if (fechaInicio == null)
            return BadRequest(new { ok = false, message = "Elija la fecha del reporte." });

        var desde = fechaInicio.Value.Date;
        var hasta = (fechaFin ?? fechaInicio.Value).Date;
        if (hasta < desde) (desde, hasta) = (hasta, desde);

        var llave = _configuration["Jwt:Key"] ?? "";
        var ticket = ReporteRecepcionCascosTicket.Firmar(
            llave, desde, hasta, idRepartidor ?? 0,
            IdUsuarioLegacy(), CurrentUser.GetUsername(User, ""),
            incluirCanceladas);

        /*
          La direccion se arma con el esquema y el host DE LA PETICION, no de
          configuracion: asi sirve igual en local, detras del proxy y en
          produccion. Y la ruta corta es lo que ve el empleado en la barra.
        */
        var pathBase = Request.PathBase.HasValue ? Request.PathBase.Value : "";
        var url = $"{Request.Scheme}://{Request.Host}{pathBase}/cascos-recibidos?t={Uri.EscapeDataString(ticket)}";

        return await Task.FromResult<IActionResult>(
            Ok(new { ok = true, message = "Reporte listo.", data = new { url } }));
    }

    /// <summary>
    /// Entrega el PDF. Anonimo a la fuerza —una pestana nueva no manda
    /// Authorization— y autorizado por el pase firmado que viaja en la
    /// direccion.
    /// </summary>
    [HttpGet("/cascos-recibidos")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReportePdf([FromQuery] string? t, CancellationToken ct)
    {
        var pase = ReporteRecepcionCascosTicket.Validar(_configuration["Jwt:Key"] ?? "", t);
        if (pase == null)
            /* Texto plano y no JSON: esto lo lee una persona en una pestana. */
            return BadRequest("El enlace del reporte no es válido o ya venció. Vuelve a pedirlo desde la pantalla.");

        var datos = await _service.ConsultarDatosDeReporteAsync(
            pase.Desde, pase.Hasta, pase.IncluirCanceladas, ct);

        /*
          El pase puede traer un repartidor: se recorta aqui y no en el
          procedimiento porque es la MISMA consulta con menos renglones, y un
          parametro mas en el reporte obligaria a tocar la base por una
          pregunta que no cambia como se consulta.
        */
        if (pase.IdRepartidor > 0)
            datos.Renglones = datos.Renglones
                .Where(r => r.idRepartidor == pase.IdRepartidor).ToList();

        /*
          Y si se recorto, los TOTALES por tipo hay que recalcularlos sobre lo
          que queda: los que trae el procedimiento son del periodo completo, y
          un papel filtrado con los totales del periodo entero dice dos cosas
          distintas en la misma hoja.
        */
        if (pase.IdRepartidor > 0)
        {
            foreach (var total in datos.Totales)
            {
                /*
                  LAS DOS SUMAS SE RECALCULAN POR SEPARADO, Y SE SEPARAN POR
                  ESTATUS. Sumar aqui todos los renglones metia las canceladas
                  en `piezas` —el total de cascos recibidos— y el papel diria
                  que hay cascos que nadie tiene. La regla es la misma que
                  aplica el procedimiento cuando no se filtra por repartidor:
                  lo recibido es lo activo, y lo cancelado va aparte.
                */
                var suyos = datos.Renglones.Where(r => r.idTipoUsado == total.idTipoUsado);
                total.piezas = suyos.Where(r => r.estatus != EstatusCancelada).Sum(r => r.piezas);
                total.piezasCanceladas = suyos
                    .Where(r => r.estatus == EstatusCancelada).Sum(r => r.piezas);
            }
        }

        var (logo, empresa) = await _service.ConsultarMarcaParaReporteAsync(ct);

        var nombreRepartidor = pase.IdRepartidor > 0
            ? datos.Renglones
                  .FirstOrDefault(r => r.idRepartidor == pase.IdRepartidor)?.quienEntrego ?? ""
            : "";

        var encabezado = RecepcionCascosHtmlBuilder.Encabezado(
            empresa, logo, pase.Desde, pase.Hasta,
            nombreRepartidor, pase.Usuario);

        var html = RecepcionCascosHtmlBuilder.Construir(datos);

        /*
          Apaisado: son once columnas (fecha, quien entrego, ocho tamanos,
          total) y en vertical el nombre del repartidor se parte en dos
          renglones.
        */
        var pdf = await WkhtmltopdfHtmlPdfRenderer.RenderAsync(
            html, "Landscape", "Cascos usados recibidos", ct,
            new WkhtmltopdfHtmlPdfRenderer.Opciones
            {
                /*
                  MargenSuperior es el HUECO donde CABE el encabezado, no
                  estetica: wkhtmltopdf NO empuja el contenido, dibuja el
                  encabezado encima de lo que haya. Con menos, la primera fila
                  queda debajo del titulo.
                */
                MargenSuperior = 24,
                EspacioEncabezado = 4,
                MargenInferior = 12,
                MargenIzquierdo = 10,
                MargenDerecho = 10,
                /*
                  El logo entra a 3015 px y por omision se remuestrea a 600 dpi:
                  a ese tamano los contornos blancos se promedian con el fondo y
                  la marca sale lavada.
                */
                ImagenDpi = 900,
                /*
                  El pie va en la FRANJA de la hoja, no al final del cuerpo:
                  como parrafo, su margen podia no caber en lo que quedaba de
                  pagina y arrastraba una hoja en blanco para una sola linea.
                */
                PieIzquierdo = string.IsNullOrWhiteSpace(empresa) ? null : empresa,
                PieConLinea = false,
                /*
                  wkhtmltopdf dibuja el pie con SU tipografia (Arial), no con la
                  del HTML: sin esto el reporte sale todo en Tahoma menos el
                  renglon de abajo.
                */
                PieFuente = "Tahoma",
                EncabezadoHtml = encabezado
            });

        Response.Headers["Cache-Control"] = "no-store";
        return File(pdf, "application/pdf");
    }

    /* ---------------------------------------------------------------- */

    /*
      POR DONDE ENTRO. Sale del encabezado que pone la app, no del cuerpo: es
      informativo —se guarda para poder saber despues si una recepcion se
      capturo en la oficina o en la calle— y no decide ningun permiso. Quien
      decide eso es el canal firmado en el token.
    */
    private string? Canal()
    {
        var cliente = Request.Headers["X-Cliente-App"].ToString();
        if (string.IsNullOrWhiteSpace(cliente)) return "WEB";
        return cliente.Contains("movil", StringComparison.OrdinalIgnoreCase) ? "MOVIL" : "WEB";
    }
}
