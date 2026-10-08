using ISL_Service.Application.DTOs.OperacionMac;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Cambios de vigencia: los productos que un cliente entrega para que se les
/// renueve la vigencia o la garantia.
/// </summary>
[ApiController]
[Route("api/cambios-vigencia")]
[Authorize]
public class CambiosVigenciaController : PermisoControllerBase
{
    private static readonly string[] PermisosVer = { "cambiosvigencia.ver" };
    private static readonly string[] PermisosCrear = { "cambiosvigencia.crear" };
    private static readonly string[] PermisosEditar = { "cambiosvigencia.editar" };
    private static readonly string[] PermisosCancelar = { "cambiosvigencia.cancelar" };

    private readonly IOperacionMacService _service;

    public CambiosVigenciaController(
        IOperacionMacService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    /// <summary>
    /// Consulta de cambios de vigencia. Con `idCambioVigencia` se ignoran los
    /// demas filtros: es la precedencia de legacy, dicha en vez de escondida.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Consultar([FromQuery] CambiosVigenciaFiltro? filtro, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        filtro ??= new CambiosVigenciaFiltro();
        if (filtro.Desde.HasValue != filtro.Hasta.HasValue)
            return BadRequest(new { ok = false, message = "El rango de fechas necesita las dos fechas." });
        if (filtro.Desde.HasValue && filtro.Hasta!.Value < filtro.Desde.Value)
            return BadRequest(new { ok = false, message = "El rango de fechas termina antes de empezar." });

        var data = await _service.ConsultarCambiosVigenciaAsync(filtro, ct);
        return Ok(new { ok = true, message = "Cambios de vigencia consultados.", data, total = data.Count });
    }

    /// <summary>
    /// Los PRODUCTOS de un cambio de vigencia: un renglon por producto, con su
    /// marca, su clave, su vale, su vigencia y su garantia.
    ///
    /// Mismo permiso que la consulta de cabeceras (`cambiosvigencia.ver`): es el
    /// mismo documento visto de cerca, no otro dato.
    ///
    /// LA CABECERA SE REPITE EN CADA RENGLON y no se recorta a proposito: el
    /// procedimiento devuelve un JOIN plano (cliente, folio, estatus y los
    /// bloques de cancelacion, impresion y entrega junto a cada producto), y
    /// partirlo aqui en dos objetos obligaria a elegir cual de las dos copias es
    /// la buena cuando el renglon trae su propio IDUsuarioEntregada -que lo trae,
    /// porque la entrega se registra POR PRODUCTO-.
    ///
    /// Si el cambio de vigencia no existe, o no tiene renglones, la lista viene
    /// vacia: no es un 404. El documento pudo existir y quedarse sin productos, y
    /// esa diferencia la contesta la consulta de cabeceras, que es quien sabe si
    /// el folio existe.
    /// </summary>
    [HttpGet("{id:int}/detalle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarDetalle([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        if (id <= 0)
            return BadRequest(new { ok = false, message = "El cambio de vigencia es requerido." });

        var data = await _service.ConsultarCambiosVigenciaDetalleAsync(id, ct);
        return Ok(new { ok = true, message = "Detalle del cambio de vigencia.", data, total = data.Count });
    }

    /// <summary>
    /// Alta de un cambio de vigencia con sus productos.
    ///
    /// Los productos viajan como lista de objetos. En legacy eran TRES cadenas
    /// unidas con '~' (productos, vigencias, garantias) apareadas por posicion por
    /// el procedimiento, mas un `NumProductos` aparte que podia contradecirlas:
    /// cuatro datos que tenian que hablar del mismo renglon y nada lo comprobaba.
    /// Aqui el numero lo cuenta el backend desde la lista.
    /// </summary>
    /// <response code="201">Cambio de vigencia registrado</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Crear([FromBody] CrearCambioVigenciaRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        if (request.IdCliente <= 0)
            return BadRequest(new { ok = false, message = "El cliente es requerido." });
        if (request.Productos.Count == 0)
            return BadRequest(new { ok = false, message = "No hay productos en el cambio de vigencia." });
        if (request.Productos.Any(x => x.IdProducto <= 0))
            return BadRequest(new { ok = false, message = "Hay un renglon sin producto." });
        if (request.Productos.Any(x => x.Vigencia.Contains('~') || x.Garantia.Contains('~')))
            return BadRequest(new { ok = false, message = "La vigencia y la garantia no pueden llevar el caracter ~: es el separador con el que la base recibe la lista." });

        var sinPermiso = await ExigirPermisoAsync(PermisosCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.CrearCambioVigenciaAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return data.Guardo
            ? StatusCode(StatusCodes.Status201Created, new { ok = true, message = "Cambio de vigencia registrado.", data })
            : Responder(data, "Cambio de vigencia registrado.");
    }

    /// <summary>
    /// Modifica UN renglon del cambio de vigencia.
    ///
    /// Es un producto por llamada porque asi lo recibe el procedimiento
    /// (@IDProducto en singular). En legacy el cliente llamaba N veces sin
    /// transaccion para cambiar varios renglones, y si fallaba a la mitad quedaban
    /// unos cambiados y otros no. Aqui es lo mismo pero dicho: la ruta es de un
    /// renglon, no promete un lote.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Actualizar([FromRoute] int id, [FromBody] ActualizarCambioVigenciaRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        if (request.IdStatusCambioVigencia <= 0)
            return BadRequest(new { ok = false, message = "El estatus del cambio de vigencia es requerido." });
        if (request.IdCliente <= 0)
            return BadRequest(new { ok = false, message = "El cliente es requerido." });

        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(
            await _service.ActualizarCambioVigenciaAsync(id, request, IdUsuarioLegacy(), Equipo(), ct),
            "Cambio de vigencia actualizado.");
    }

    /// <summary>
    /// Cancela el cambio de vigencia.
    ///
    /// NO PIDE MOTIVO porque el procedimiento no lo guarda: recibe solo el id, el
    /// usuario y el equipo. Legacy aceptaba un campo de observaciones, lo saneaba y
    /// lo tiraba sin mandarlo — el capturista escribia el motivo y no quedaba en
    /// ninguna parte. Pedirlo aqui seria repetir esa mentira.
    /// </summary>
    [HttpPost("{id:int}/cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancelar([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosCancelar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(
            await _service.CancelarCambioVigenciaAsync(id, IdUsuarioLegacy(), Equipo(), ct),
            "Cambio de vigencia cancelado.");
    }

    /// <summary>
    /// Devuelve el cambio de vigencia al estado anterior. Mismo caso que cancelar:
    /// el procedimiento no guarda motivo.
    /// </summary>
    [HttpPost("{id:int}/regresar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Regresar([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(
            await _service.RegresarCambioVigenciaAsync(id, IdUsuarioLegacy(), Equipo(), ct),
            "Cambio de vigencia regresado.");
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
}
