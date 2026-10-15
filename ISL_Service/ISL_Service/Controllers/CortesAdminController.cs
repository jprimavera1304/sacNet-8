using ISL_Service.Application.DTOs.OperacionMac;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Cortes de caja y cortes de retiro de centro de servicio.
///
/// Cuelga de api/cortes, donde la ola 3 dejo las lecturas de detalle
/// (GET api/cortes/{id}/detalle, /siguiente-fecha, /centros-servicio/...).
///
/// HAY UNA SOLA RUTA PARA CANCELAR, y es a proposito. Legacy tenia dos
/// (/cortes/cancelar y /cortescs/cancelar) que llamaban al MISMO
/// sp_n_CancelarCorte con los mismos parametros: dos puertas a la misma
/// operacion, indistinguibles en la base, que solo servian para que alguien
/// cancelara por la equivocada y creyera que habia hecho algo distinto.
/// </summary>
[ApiController]
[Route("api/cortes")]
[Authorize]
public class CortesAdminController : PermisoControllerBase
{
    private static readonly string[] PermisosVer = { "cortes.ver", "app_movil.cortes.ver" };
    private static readonly string[] PermisosCrear = { "cortes.crear" };
    // El MISMO endpoint cancela cortes de caja y de centro de servicio, asi que
    // esta clave del telefono habilita las dos. Separarlas pedia dos endpoints.
    private static readonly string[] PermisosCancelar =
        { "cortes.cancelar", "app_movil.cortes.cancelar" };
    // Centro de servicio es OTRA clave, no la de caja: la misma pantalla del
    // telefono cambia de modo con la capacidad `esCentroServicio` de la empresa
    // y entonces pega a otro endpoint. Con solo `app_movil.cortes.ver` esa
    // empresa seguia viendo 403.
    private static readonly string[] PermisosVerCs =
        { "cortes.cs.ver", "app_movil.cortes.cs.ver" };
    private static readonly string[] PermisosCrearCs = { "cortes.cs.crear" };

    private readonly IOperacionMacService _service;

    public CortesAdminController(
        IOperacionMacService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    /// <summary>
    /// Consulta de cortes de caja. Con `idCorte` se ignora el rango de fechas,
    /// que es la precedencia de legacy.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Consultar(
        [FromQuery] int idCorte = 0,
        [FromQuery] DateTime? desde = null,
        [FromQuery] DateTime? hasta = null,
        CancellationToken ct = default)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        var error = ValidaRango(desde, hasta);
        if (error != null) return BadRequest(error);

        var data = await _service.ConsultarCortesAsync(idCorte, desde, hasta, ct);
        return Ok(new { ok = true, message = "Cortes consultados.", data, total = data.Count });
    }

    /// <summary>
    /// Los gastos del corte por tipo.
    ///
    /// LAS DOS FECHAS SON OBLIGATORIAS. El procedimiento las usa sin ramas y
    /// legacy las convertia sin comprobar que vinieran: una consulta sin fechas
    /// reventaba con un error de posicion de caracteres y devolvia 400 con el
    /// stack completo en vez de decir que faltaban.
    /// </summary>
    [HttpGet("tipo-gastos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConsultarTipoGastos(
        [FromQuery] DateTime desde,
        [FromQuery] DateTime hasta,
        [FromQuery] string? corteInfo = null,
        CancellationToken ct = default)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        if (desde == default || hasta == default)
            return BadRequest(new { ok = false, message = "Las dos fechas son requeridas." });
        if (hasta < desde)
            return BadRequest(new { ok = false, message = "El rango de fechas termina antes de empezar." });

        var data = await _service.ConsultarCorteTipoGastosAsync(corteInfo ?? string.Empty, desde, hasta, ct);
        return Ok(new { ok = true, message = "Gastos del corte consultados.", data, total = data.Count });
    }

    /// <summary>
    /// Alta del corte de caja del dia.
    ///
    /// El desglose viaja como veinte campos CON NOMBRE y los gastos como una lista
    /// de renglones. En legacy el desglose era una sola cadena armada en el front
    /// y pegada con '@@@@', y los gastos eran cuatro listas paralelas unidas con
    /// '~' que el procedimiento apareaba por posicion, sin que nadie comprobara
    /// que tuvieran la misma longitud: una lista corrida le ponia a un gasto el
    /// monto de otro.
    ///
    /// Y la respuesta dice de verdad si se guardo: el alta de corte era la UNICA
    /// de legacy que no leia el result del procedimiento, asi que contestaba
    /// result 0 y mensaje vacio tanto si guardaba como si no.
    /// </summary>
    /// <response code="201">Corte guardado</response>
    /// <response code="400">Datos invalidos, o la base rechazo el corte</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Crear([FromBody] CrearCorteRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        if (request.FechaCorte == default)
            return BadRequest(new { ok = false, message = "La fecha del corte es requerida." });

        foreach (var g in request.Gastos)
        {
            if (g.IdTipoGasto <= 0)
                return BadRequest(new { ok = false, message = "Hay un gasto sin tipo de gasto." });
            if (string.IsNullOrWhiteSpace(g.Concepto))
                return BadRequest(new { ok = false, message = "Hay un gasto sin concepto." });
            if (g.Concepto.Contains('~'))
                return BadRequest(new { ok = false, message = "El concepto no puede llevar el caracter ~: es el separador con el que la base recibe la lista." });
        }

        var sinPermiso = await ExigirPermisoAsync(PermisosCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.CrearCorteAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return data.Guardo
            ? StatusCode(StatusCodes.Status201Created, new { ok = true, message = "Corte guardado.", data })
            : Responder(data, "Corte guardado.");
    }

    /// <summary>
    /// Cancela un corte, de caja o de centro de servicio. Es el mismo
    /// procedimiento para los dos (ver la nota de la clase).
    /// </summary>
    [HttpPost("{id:int}/cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancelar([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosCancelar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await _service.CancelarCorteAsync(id, IdUsuarioLegacy(), Equipo(), ct), "Corte cancelado.");
    }

    /// <summary>
    /// Consulta de cortes de retiro de centro de servicio. Con `folio` se ignoran
    /// el id, el tipo de retiro y las fechas: es la precedencia de legacy.
    /// </summary>
    [HttpGet("centros-servicio")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConsultarCentrosServicio(
        [FromQuery] int idCorte = 0,
        [FromQuery] int folio = 0,
        [FromQuery] DateTime? desde = null,
        [FromQuery] DateTime? hasta = null,
        [FromQuery] int idTipoRetiro = 0,
        CancellationToken ct = default)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVerCs, ct);
        if (sinPermiso != null) return sinPermiso;

        var error = ValidaRango(desde, hasta);
        if (error != null) return BadRequest(error);

        var data = await _service.ConsultarCortesCentroServicioAsync(idCorte, folio, desde, hasta, idTipoRetiro, ct);
        return Ok(new { ok = true, message = "Cortes de centro de servicio consultados.", data, total = data.Count });
    }

    /// <summary>
    /// Corte de retiro de un centro de servicio.
    ///
    /// Solo viaja la fecha final. Legacy convertia tambien una fecha inicial, la
    /// exigia de hecho (reventaba si no venia) y nunca la mandaba al
    /// procedimiento.
    /// </summary>
    /// <response code="201">Corte de centro de servicio guardado</response>
    [HttpPost("centros-servicio")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearCentroServicio([FromBody] CrearCorteCentroServicioRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        if (request.IdTipoRetiro <= 0)
            return BadRequest(new { ok = false, message = "El tipo de retiro es requerido." });
        if (request.FechaFinal == default)
            return BadRequest(new { ok = false, message = "La fecha final es requerida." });
        if (request.MontoRetiro < 0 || request.MontoRetiroTarjetaCredito < 0 || request.MontoRetiroTarjetaDebito < 0)
            return BadRequest(new { ok = false, message = "Un monto de retiro no puede ser negativo." });

        var sinPermiso = await ExigirPermisoAsync(PermisosCrearCs, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.CrearCorteCentroServicioAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return data.Guardo
            ? StatusCode(StatusCodes.Status201Created, new { ok = true, message = "Corte de centro de servicio guardado.", data })
            : Responder(data, "Corte de centro de servicio guardado.");
    }

    private IActionResult Responder(OperacionEscrituraResponse r, string mensajeExito)
        => Ok(new
        {
            ok = r.Guardo,
            message = r.Guardo
                ? mensajeExito
                : (string.IsNullOrWhiteSpace(r.Mensaje) ? "La base rechazo la operacion." : r.Mensaje),
            data = r
        });

    private static object? ValidaRango(DateTime? desde, DateTime? hasta)
    {
        if (desde.HasValue != hasta.HasValue)
            return new { ok = false, message = "El rango de fechas necesita las dos fechas." };
        if (desde.HasValue && hasta!.Value < desde.Value)
            return new { ok = false, message = "El rango de fechas termina antes de empezar." };
        return null;
    }
}
