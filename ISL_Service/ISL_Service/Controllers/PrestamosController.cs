using ISL_Service.Application.DTOs.Prestamos;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Prestamos a empleados. AQUI SE MUEVE DINERO.
///
/// DOS COSAS DE LEGACY QUE ESTA API NO EXPONE, A PROPOSITO:
///
///  - El ABONO MANUAL (sp_n_InsertarEmpleadoPrestamosPagos). Hace
///    "SET Abonos = @Abono": PISA el acumulado en vez de sumarlo, y no toca el
///    saldo ni la fecha de pago. Deja el prestamo descuadrado. Se usa mucho
///    desde Mac31, asi que el defecto ya existe; abrirlo tambien desde el web
///    seria multiplicarlo por dos pantallas.
///
///  - El BATCH SEMANAL (sp_n_ValidaEmpleadosPrestamos), que descuenta los
///    prestamos de la nomina: inserta el abono, baja el saldo y liquida. No
///    esta automatizado: lo dispara una persona desde Mac31. Colgarlo de un
///    boton seria mover dinero de la nomina desde un telefono.
/// </summary>
[ApiController]
[Route("api/prestamos")]
[Authorize]
public class PrestamosController : PermisoControllerBase
{
    private static readonly string[] PermisosVer = { "prestamos.ver", "app_movil.prestamos.ver" };
    private static readonly string[] PermisosCrear = { "prestamos.crear", "app_movil.prestamos.crear" };
    private static readonly string[] PermisosEditar = { "prestamos.editar", "app_movil.prestamos.editar" };
    private static readonly string[] PermisosCancelar = { "prestamos.cancelar", "app_movil.prestamos.cancelar" };

    private readonly IPrestamosService _service;

    public PrestamosController(
        IPrestamosService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    /// <summary>
    /// Lista de prestamos.
    /// </summary>
    /// <param name="idEmpleado">0 = todos.</param>
    /// <param name="estado">
    /// todos | pagados | pendientes | cancelados. En palabras a proposito: el
    /// front no tiene por que conocer los 0/1/2/3 de legacy.
    /// </param>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Consultar(
        [FromQuery] int idEmpleado = 0,
        [FromQuery] string? estado = PrestamoEstado.Todos,
        CancellationToken ct = default)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.ConsultarAsync(idEmpleado, estado, ct);
        return Ok(new { ok = true, message = "Prestamos consultados.", data = data.Rows, total = data.Total });
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.ObtenerAsync(id, ct);
        if (data == null)
            return NotFound(new { ok = false, message = "El prestamo no existe." });

        return Ok(new { ok = true, message = "Prestamo consultado.", data });
    }

    /// <summary>
    /// Registra un prestamo.
    ///
    /// Si el empleado YA tenia un prestamo abierto, legacy conserva el
    /// descuento semanal anterior y no aplica el que se mando. La respuesta lo
    /// dice en 'data.advertencia': el capturista escribio un numero y tiene
    /// derecho a saber que no se uso.
    /// </summary>
    /// <response code="201">Prestamo registrado</response>
    /// <response code="400">Datos invalidos, o legacy contesto error</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Crear([FromBody] CrearPrestamoRequest? request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { ok = false, message = "Body requerido." });

        var sinPermiso = await ExigirPermisoAsync(PermisosCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.CrearAsync(request, IdUsuarioLegacy(), ct);
        var message = string.IsNullOrEmpty(data.Advertencia) ? "Prestamo registrado." : data.Advertencia!;

        return StatusCode(StatusCodes.Status201Created, new { ok = true, message, data });
    }

    /// <summary>
    /// Cambia el descuento semanal.
    ///
    /// La ruta dice /monto-pagos y no es un PUT generico del prestamo A
    /// PROPOSITO: legacy SOLO deja cambiar eso. Un PUT /prestamos/{id}
    /// prometeria poder cambiar el monto o el motivo, y mentiria.
    /// </summary>
    /// <response code="200">Descuento cambiado</response>
    /// <response code="404">El prestamo no existe</response>
    /// <response code="409">El prestamo ya esta pagado o cancelado</response>
    [HttpPut("{id:int}/monto-pagos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CambiarMontoPagos([FromRoute] int id, [FromBody] CambiarMontoPagosRequest? request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { ok = false, message = "Body requerido." });

        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        await _service.CambiarMontoPagosAsync(id, request.MontoPagos, IdUsuarioLegacy(), ct);
        return Ok(new { ok = true, message = "Descuento semanal cambiado." });
    }

    /// <summary>
    /// Cancela el prestamo. Si al empleado no le queda ningun otro abierto,
    /// legacy tambien le borra su renglon de descuento semanal.
    /// </summary>
    /// <response code="200">Prestamo cancelado</response>
    /// <response code="404">El prestamo no existe</response>
    /// <response code="409">Ya estaba cancelado</response>
    [HttpPost("{id:int}/cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancelar([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosCancelar, ct);
        if (sinPermiso != null) return sinPermiso;

        await _service.CancelarAsync(id, IdUsuarioLegacy(), ct);
        return Ok(new { ok = true, message = "Prestamo cancelado." });
    }
}
