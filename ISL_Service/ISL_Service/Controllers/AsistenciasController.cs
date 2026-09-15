using ISL_Service.Application.DTOs.Asistencias;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Asistencias por PERIODO de nomina: la rejilla de siete dias y la captura
/// manual de una celda.
///
/// LA VISTA DE UN DIA NO ESTA AQUI. Ya existe como
/// GET /api/checador/asistencia?fecha=, que responde en milisegundos y es lo
/// que consulta el telefono parado en la bodega. Duplicarla aqui seria tener
/// dos endpoints que contestan lo mismo y que un dia dejaran de contestar lo
/// mismo.
/// </summary>
[ApiController]
[Route("api/asistencias")]
[Authorize]
public class AsistenciasController : PermisoControllerBase
{
    private static readonly string[] PermisosVer = { "asistencias.ver", "app_movil.asistencias.ver" };
    private static readonly string[] PermisosEditar = { "asistencias.editar", "app_movil.asistencias.editar" };

    private readonly IAsistenciasService _service;

    public AsistenciasController(
        IAsistenciasService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    /// <summary>
    /// Periodos de nomina para el combo, del mas nuevo al mas viejo.
    /// </summary>
    /// <param name="idTipoSueldo">0 = todos. 1 nomina, 2 comision.</param>
    /// <param name="top">Cuantos traer. Default 52 (un anio).</param>
    [HttpGet("periodos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Periodos([FromQuery] int idTipoSueldo = 0, [FromQuery] int top = 52, CancellationToken ct = default)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.ConsultarPeriodosAsync(idTipoSueldo, top, ct);
        return Ok(new { ok = true, message = "Periodos consultados.", data });
    }

    /// <summary>
    /// Los dias del periodo. Es lo que arma los encabezados de la rejilla
    /// ("LUN 02/03").
    /// </summary>
    [HttpGet("periodos/{id:int}/dias")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Dias([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.ConsultarDiasAsync(id, ct);
        return Ok(new { ok = true, message = "Dias consultados.", data });
    }

    /// <summary>
    /// La rejilla: un renglon por empleado, siete columnas de dia.
    ///
    /// TARDA. Es un doble cursor dentro de legacy (empleados x dias): 914 ms
    /// medidos con los 25 empleados de Tauro y 3.7 s con los 85 de Zaragoza.
    /// La respuesta trae 'milisegundos' para que la pantalla pueda avisar
    /// honestamente en vez de girar un spinner mudo.
    ///
    /// OJO EN ZARAGOZA: 'asistencias' e 'inasistencias' pueden venir en null
    /// para algunos empleados. No es un error de la API: a esa copia del
    /// procedimiento de legacy le falta un ISNULL que la de Tauro si tiene, y
    /// basta un dia con salida sin entrada para que la suma del periodo se
    /// vuelva null. Se pasa tal cual; la pantalla debe pintar "--", no "0".
    /// </summary>
    /// <param name="idStatus">1 activos (default), 2 inactivos, 3 todos.</param>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Rejilla(
        [FromQuery] int idPeriodoTipoSueldo,
        [FromQuery] int idEmpleado = 0,
        [FromQuery] int idStatus = 1,
        CancellationToken ct = default)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.ConsultarRejillaAsync(idPeriodoTipoSueldo, idEmpleado, idStatus, ct);
        return Ok(new { ok = true, message = "Asistencias consultadas.", data = data.Rows, total = data.Total, milisegundos = data.Milisegundos });
    }

    /// <summary>
    /// Captura manual de un dia (insert o update, lo decide legacy).
    ///
    /// Viaja la FECHA, no el IDPeriodoTipoSueldoDia: el cruce lo hace
    /// sp_w_GuardarAsistenciaDia adentro de la base. Si ese id viajara por
    /// HTTP, un front con un bug escribiria la asistencia en la semana
    /// equivocada y no se notaria hasta que saliera mal la nomina.
    /// </summary>
    /// <response code="200">Asistencia guardada</response>
    /// <response code="400">Horas invalidas, dia sin periodo de nomina, o falta/vacacion contradictorias</response>
    [HttpPut("dia")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GuardarDia([FromBody] GuardarAsistenciaDiaRequest? request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { ok = false, message = "Body requerido." });

        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        await _service.GuardarDiaAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Asistencia guardada." });
    }

    /// <summary>
    /// Borra la asistencia de un dia. Es BAJA LOGICA: legacy pone IDStatus = 2
    /// y el renglon se queda. Se respeta tal cual; borrar de verdad seria
    /// perder la unica evidencia de que alguien capturo algo y luego lo quito.
    /// </summary>
    /// <response code="200">Asistencia borrada</response>
    /// <response code="404">Ese empleado no tenia asistencia capturada en esa fecha</response>
    [HttpDelete("dia")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EliminarDia(
        [FromQuery] int idEmpleado,
        [FromQuery] DateTime? fecha,
        CancellationToken ct = default)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        await _service.EliminarDiaAsync(idEmpleado, fecha, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Asistencia borrada." });
    }
}
