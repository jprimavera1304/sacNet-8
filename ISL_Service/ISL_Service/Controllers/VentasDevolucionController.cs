using ISL_Service.Application.DTOs.VentasDevolucion;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Devolución de una remisión, con las reglas de Mac31 y llamando a sus mismos
/// procedimientos. Ver VentasDevolucionService para el porqué de cada regla.
/// </summary>
[ApiController]
[Route("api/ventas/devolucion")]
[Authorize]
public class VentasDevolucionController : PermisoControllerBase
{
    /*
      El mismo permiso que en Mac31 enseña btnDevolucion. Ya existe en el
      catalogo (PermissionService, binding "btnDevolucion" -> "ventas.devolucion"):
      no se inventa ninguno, porque un permiso nuevo es un boton que nadie ve
      hasta que alguien lo da de alta a mano en cada empresa.
    */
    private static readonly string[] PermisosDevolucion = { "ventas.devolucion" };

    private readonly IVentasDevolucionService _service;

    public VentasDevolucionController(
        IVentasDevolucionService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    /// <summary>
    /// Todo lo que la pantalla necesita para abrirse: encabezado, totales,
    /// partidas y los dos bloques de cascos. Sin tocar nada.
    /// </summary>
    [HttpGet("{idVenta:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Vista(int idVenta, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosDevolucion, ct);
        if (sinPermiso is not null) return sinPermiso;

        if (idVenta <= 0)
            return BadRequest(new { ok = false, message = "Falta la remisión." });

        var data = await _service.VistaAsync(idVenta, ct);
        return Ok(new { ok = true, message = "Devolución lista.", data });
    }

    /// <summary>
    /// Devuelve. Quien decide de verdad es sp_n_DevolucionVenta, y cuando dice
    /// que no, su mensaje viaja tal cual: son los textos "NO SE PUEDE REALIZAR
    /// LA DEVOLUCIÓN 002/003/004" con las cifras que no cuadran.
    /// </summary>
    /// <response code="200">Se intentó; `data.ok` dice si se registró</response>
    /// <response code="400">No se mandó la remisión</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Devolver([FromBody] VentasDevolucionRequest? body, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosDevolucion, ct);
        if (sinPermiso is not null) return sinPermiso;

        if (body is null)
            return BadRequest(new { ok = false, message = "Body requerido." });

        /*
          Quien devuelve y desde donde salen del TOKEN, nunca del cuerpo: es la
          firma que queda escrita en la venta (IDUsuarioDevolucion y Equipo del
          SP). Si viajaran en el body, cualquiera podria devolver a nombre de
          otro.
        */
        var data = await _service.DevolverAsync(body, IdUsuarioLegacy(), Equipo(), ct);

        var mensaje = data.Ok ? "LA DEVOLUCIÓN HA SIDO REGISTRADA." : "La devolución no se realizó.";
        return Ok(new { ok = true, message = mensaje, data });
    }
}
