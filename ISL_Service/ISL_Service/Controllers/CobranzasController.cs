using ISL_Service.Application.DTOs.Cobranzas;
using ISL_Service.Application.DTOs.EscriturasLegacy;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/*
  COBRANZAS: EL FAJO DE REMISIONES QUE SE LLEVA UN REPARTIDOR.

  PERMISOS: SON DE FAMILIA PROPIA, NO DE ventas.*
  ----------------------------------------------
  Aqui hay una trampa que vale escribir. `ventas.*` es una de las CUATRO familias
  que gobierna Mac31: al calcular el snapshot, el puente de permisos BORRA del
  snapshot lo que diga el web para esas familias y lo recalcula desde los botones
  de la forma CONSULTA DE VENTAS (PermissionService, seccion 2.8 del resumen de
  arquitectura). Si estas claves se hubieran llamado `ventas.cobranzas.*`, darlas
  desde la pantalla de permisos del web NO habria servido de nada: se guardarian
  en WUsuarioPermiso y desaparecerian en el siguiente calculo, sin error y sin
  rastro.

  Por eso son `cobranzas.*`: familia propia, del web, fuera del puente.

    cobranzas.ver_modulo  la tarjeta del modulo
    cobranzas.ver         consultar cobranzas y su detalle
    cobranzas.crear       armar una cobranza
    cobranzas.editar      cambiarle el contenido
    cobranzas.cancelar    cancelarla
    cobranzas.pendientes.ver  la pantalla de cobranza pendiente y vencida

  QUIEN PREGUNTA SALE DEL TOKEN
  -----------------------------
  sp_n_ConsultarCobranzas recibe @IDUsuarioActual y con eso decide que cobranzas
  se ven. Legacy lo pedia en el cuerpo: mandando el id de otro se veian las
  cobranzas de otro.
*/
[ApiController]
[Route("api/cobranzas")]
[Authorize]
public class CobranzasController : PermisoControllerBase
{
    private readonly ICobranzasService _service;

    private static readonly string[] Ver = { "cobranzas.ver" };
    private static readonly string[] Crear = { "cobranzas.crear" };
    private static readonly string[] Editar = { "cobranzas.editar" };
    private static readonly string[] Cancelar = { "cobranzas.cancelar" };
    private static readonly string[] VerPendientes = { "cobranzas.pendientes.ver" };

    public CobranzasController(
        ICobranzasService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    private IActionResult Body() => BadRequest(new { ok = false, message = "Body requerido." });

    private IActionResult Lista(string mensaje, List<Dictionary<string, object?>> data)
    {
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = mensaje, data, total = data.Count });
    }

    /// <summary>
    /// Las cobranzas. Todos los filtros se combinan, y quien pregunta sale del
    /// token.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarAsync([FromQuery] CobranzasFiltro filtro, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(Ver, ct);
        if (no is not null) return no;
        return Lista("Cobranzas consultadas.", await _service.ConsultarAsync(filtro, IdUsuarioLegacy(), ct));
    }

    /// <summary>El detalle de una cobranza.</summary>
    [HttpGet("{id:int}/detalle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarDetalleAsync(int id, [FromQuery] int presentacion, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(Ver, ct);
        if (no is not null) return no;

        var filtro = new CobranzaDetalleFiltro { IdCobranza = id, Presentacion = presentacion };
        return Lista("Detalle de la cobranza.", await _service.ConsultarDetalleAsync(filtro, ct));
    }

    /// <summary>
    /// Dice si una venta se puede meter en una cobranza (si no la trae ya otra,
    /// si no esta pagada, si no esta cancelada).
    /// </summary>
    [HttpGet("validar-folio")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ValidarFolioAsync([FromQuery] CobranzaValidarFolioFiltro filtro, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(Crear, ct);
        if (no is not null) return no;
        return Lista("Folio validado.", await _service.ValidarFolioAsync(filtro, IdUsuarioLegacy(), ct));
    }

    /// <summary>
    /// La cobranza pendiente y vencida.
    ///
    /// La fecha de operacion contra la que se mide el vencimiento la pone la base,
    /// no el cliente: en Legacy viajaba en el cuerpo y con eso se podia hacer que
    /// una venta vencida saliera como pendiente.
    /// </summary>
    [HttpGet("pendientes-vencidos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConsultarPendientesVencidosAsync([FromQuery] CobranzaPendientesVencidosFiltro filtro, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(VerPendientes, ct);
        if (no is not null) return no;
        return Lista("Cobranza pendiente y vencida.", await _service.ConsultarPendientesVencidosAsync(filtro, ct));
    }

    /// <summary>Arma una cobranza con un repartidor y una lista de ventas.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearAsync([FromBody] CrearCobranzaRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(Crear, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.CrearAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Cobranza armada.", data });
    }

    /// <summary>
    /// Cambia el contenido de una cobranza: quita renglones, los devuelve y/o
    /// agrega ventas. Hay que pedir al menos una de las tres cosas.
    /// </summary>
    [HttpPut("{id:int}/detalle")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarDetalleAsync(int id, [FromBody] ActualizarCobranzaDetalleRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(Editar, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.ActualizarDetalleAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Cobranza actualizada.", data });
    }

    /// <summary>Cancela una o varias cobranzas.</summary>
    [HttpPut("cancelar")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelarAsync([FromBody] CancelarCobranzasRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(Cancelar, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.CancelarAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Cobranzas canceladas.", data });
    }
}
