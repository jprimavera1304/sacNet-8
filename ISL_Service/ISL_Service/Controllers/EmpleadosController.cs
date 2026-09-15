using ISL_Service.Application.DTOs.Empleados;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Empleados: la lista rica (con puesto, tipo de sueldo, agente y sueldo) y el
/// formulario de alta y cambio.
///
/// LAS HUELLAS NO ESTAN AQUI. Ya viven en ChecadorController
/// (/api/checador/empleados/{id}/huellas y compania), que es el btnHuellas de
/// legacy ya resuelto. Duplicarlas seria tener dos puertas al mismo dato
/// biometrico.
/// </summary>
[ApiController]
[Route("api/empleados")]
[Authorize]
public class EmpleadosController : PermisoControllerBase
{
    private static readonly string[] PermisosVer = { "empleados.ver", "app_movil.empleados.ver" };
    private static readonly string[] PermisosCrear = { "empleados.crear", "app_movil.empleados.crear" };
    private static readonly string[] PermisosEditar = { "empleados.editar", "app_movil.empleados.editar" };

    // Ver cuanto gana la gente es un permiso APARTE de capturar altas y
    // corregir un RFC. Sin el, el backend borra los campos de dinero del DTO.
    private static readonly string[] PermisosSueldo = { "empleados.sueldo.ver", "app_movil.empleados.sueldo.ver" };

    private readonly IEmpleadosService _service;

    public EmpleadosController(
        IEmpleadosService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    /// <summary>
    /// Lista de empleados.
    /// </summary>
    /// <param name="idStatus">1 activos (default), 2 inactivos, 0 todos.</param>
    /// <param name="filtro">
    /// Texto libre sobre nombre, numero, puesto, RFC o agente. Se aplica EN
    /// MEMORIA: el SP de legacy que filtra por nombre concatena el texto sin
    /// escapar dentro de un LIKE, asi que mandarselo seria inyeccion SQL.
    /// </param>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Consultar([FromQuery] int idStatus = 1, [FromQuery] string? filtro = null, CancellationToken ct = default)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        var puedeVerSueldo = await TienePermisoAsync(PermisosSueldo, ct);
        var data = await _service.ConsultarAsync(idStatus, filtro, puedeVerSueldo, ct);

        return Ok(new { ok = true, message = "Empleados consultados.", data, puedeVerSueldo });
    }

    /// <summary>Los cuatro catalogos del formulario en una sola llamada.</summary>
    [HttpGet("catalogos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Catalogos(CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.ConsultarCatalogosAsync(ct);
        return Ok(new { ok = true, message = "Catalogos consultados.", data });
    }

    /// <summary>Un empleado, este activo o no.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        var puedeVerSueldo = await TienePermisoAsync(PermisosSueldo, ct);
        var data = await _service.ObtenerAsync(id, puedeVerSueldo, ct);
        if (data == null)
            return NotFound(new { ok = false, message = "El empleado no existe." });

        return Ok(new { ok = true, message = "Empleado consultado.", data, puedeVerSueldo });
    }

    /// <summary>
    /// Alta de empleado. El numero de empleado lo genera legacy (MAX + 1) y el
    /// alta siempre entra ACTIVA: los dos salen en la respuesta.
    /// </summary>
    /// <response code="201">Empleado creado</response>
    /// <response code="400">Datos invalidos, o legacy contesto error</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Crear([FromBody] CrearEmpleadoRequest? request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { ok = false, message = "Body requerido." });

        var sinPermiso = await ExigirPermisoAsync(PermisosCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.CrearAsync(request, IdUsuarioLegacy(), ct);
        return StatusCode(StatusCodes.Status201Created,
            new { ok = true, message = "Empleado dado de alta.", data });
    }

    /// <summary>
    /// Cambio de empleado. Tambien es como se da de baja: idStatus = 2.
    /// </summary>
    /// <response code="200">Empleado guardado</response>
    /// <response code="400">Datos invalidos, o legacy contesto error</response>
    /// <response code="404">El empleado no existe</response>
    /// <response code="409">
    /// El empleado tiene mas de un tipo de sueldo y no se mando
    /// confirmarMultiple. Legacy pisa TODOS sus renglones con los mismos
    /// valores; no se hace en silencio.
    /// </response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Actualizar([FromRoute] int id, [FromBody] ActualizarEmpleadoRequest? request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { ok = false, message = "Body requerido." });

        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        await _service.ActualizarAsync(id, request, IdUsuarioLegacy(), ct);
        return Ok(new { ok = true, message = "Empleado guardado." });
    }
}
