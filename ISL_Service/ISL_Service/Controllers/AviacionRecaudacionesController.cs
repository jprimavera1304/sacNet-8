using ISL_Service.Application.DTOs.Aviacion;
using ISL_Service.Application.DTOs.EscriturasLegacy;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/*
  RECAUDACIONES DE AVIACION: EL COBRO DE LA CAJA.

  POR QUE api/aviacion/recaudaciones Y NO api/recaudaciones
  --------------------------------------------------------
  Porque api/Recaudaciones ya existe en este backend y es otra cosa: el ticket
  por QR (RecaudacionesController, anonimo, autoriza el propio QR cifrado).
  Colgar aqui siete rutas mas habria juntado dos modulos que no se parecen
  —uno es publico y de solo lectura, el otro cobra dinero— bajo el mismo prefijo.

  LAS CONSULTAS SON GET Y NO TOCAN Constantes
  -------------------------------------------
  En Legacy eran POST (el token iba en el JSON) y las cuatro llamaban a
  sp_n_ConsultaConstantes solo para leer `formatoFechaDB` y voltear las fechas del
  filtro. Ese procedimiento CIERRA EL DIA del negocio. Aqui las fechas llegan
  tipadas, no hace falta la constante, y abrir la pantalla de recaudaciones ya no
  puede rodar la fecha de operacion de la empresa.

  PERMISOS
  --------
    aviacion.recaudaciones.ver       mirar los cobros y los cortes
    aviacion.recaudaciones.cobrar    registrar un cobro
    aviacion.recaudaciones.cancelar  cancelarlo
    aviacion.recaudaciones.cortar    cerrar el dia

  Cobrar, cancelar y cortar son tres personas distintas en una caja: el cajero
  cobra, el supervisor cancela, y el corte lo hace quien entrega el dinero.

  NO VERIFICABLE EN ESTA MAQUINA: de los siete sp_n_ solo dos estan en
  Produccion_svr, y uno de ellos (sp_n_ConsultaRecaudaciones) es una version vieja
  de 9 parametros contra los 31 que manda Legacy. Legacy falla igual.
*/
[ApiController]
[Route("api/aviacion/recaudaciones")]
[Authorize]
public class AviacionRecaudacionesController : PermisoControllerBase
{
    private readonly IAviacionRecaudacionesService _service;

    private static readonly string[] Ver = { "aviacion.recaudaciones.ver" };
    private static readonly string[] Cobrar = { "aviacion.recaudaciones.cobrar" };
    private static readonly string[] Cancelar = { "aviacion.recaudaciones.cancelar" };
    private static readonly string[] Cortar = { "aviacion.recaudaciones.cortar" };

    public AviacionRecaudacionesController(
        IAviacionRecaudacionesService service,
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
    /// Los cobros. Todos los filtros se combinan; pedir un folio devuelve ese
    /// folio (en Legacy devolvia todos).
    ///
    /// Va por POST aunque sea una consulta: el filtro son treinta y un campos,
    /// con seis listas de ids dentro, y eso no cabe en una query string sin
    /// volverse ilegible. Es la misma decision que toma el resto del backend
    /// nuevo para las consultas de ventas.
    /// </summary>
    [HttpPost("consultar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarAsync([FromBody] AviacionRecaudacionesFiltro? filtro, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(Ver, ct);
        if (no is not null) return no;
        return Lista("Recaudaciones consultadas.", await _service.ConsultarAsync(filtro ?? new AviacionRecaudacionesFiltro(), ct));
    }

    /// <summary>Los cobros recurrentes.</summary>
    [HttpPost("recurrentes/consultar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarRecurrentesAsync([FromBody] AviacionRecaudacionesRecurrentesFiltro? filtro, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(Ver, ct);
        if (no is not null) return no;
        return Lista("Recaudaciones recurrentes consultadas.", await _service.ConsultarRecurrentesAsync(filtro ?? new AviacionRecaudacionesRecurrentesFiltro(), ct));
    }

    /// <summary>Los cortes de recaudacion.</summary>
    [HttpGet("cortes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarCortesAsync([FromQuery] AviacionRecaudacionesCortesFiltro filtro, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(Ver, ct);
        if (no is not null) return no;
        return Lista("Cortes consultados.", await _service.ConsultarCortesAsync(filtro, ct));
    }

    /// <summary>
    /// El siguiente folio de recaudacion de una caja.
    ///
    /// No reserva nada: es el siguiente en el momento de preguntar.
    /// </summary>
    [HttpGet("siguiente-folio")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarSiguienteFolioAsync([FromQuery] int idCaja, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(Cobrar, ct);
        if (no is not null) return no;
        return Lista("Siguiente folio de recaudacion.", await _service.ConsultarSiguienteFolioAsync(idCaja, ct));
    }

    /// <summary>Registra un cobro de caja.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearAsync([FromBody] CrearAviacionRecaudacionRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(Cobrar, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.CrearAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Cobro registrado.", data });
    }

    /// <summary>Cancela un cobro.</summary>
    [HttpPut("{id:int}/cancelar")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelarAsync(int id, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(Cancelar, ct);
        if (no is not null) return no;

        var data = await _service.CancelarAsync(id, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Cobro cancelado.", data });
    }

    /// <summary>Cierra el dia de recaudacion.</summary>
    [HttpPost("cortes")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearCorteAsync([FromBody] CrearAviacionRecaudacionCorteRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(Cortar, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.CrearCorteAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Dia de recaudacion cerrado.", data });
    }
}
