using ISL_Service.Application.DTOs.OperacionMac;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Compras a proveedor y el inventario en transito.
///
/// La consulta vuelve a ser un GET: en Legacy era POST porque el token viajaba
/// dentro del JSON, aqui va en la cabecera.
/// </summary>
[ApiController]
[Route("api/compras")]
[Authorize]
public class ComprasController : PermisoControllerBase
{
    private static readonly string[] PermisosVer = { "compras.ver" };
    private static readonly string[] PermisosEditar = { "compras.editar" };

    // Cancelar una compra devuelve mercancia al proveedor y mueve el inventario.
    private static readonly string[] PermisosCancelar = { "compras.cancelar" };

    // Procesar el transito mueve existencias entre almacenes.
    private static readonly string[] PermisosTransito = { "inventario.transito.procesar" };

    private readonly IOperacionMacService _service;

    public ComprasController(
        IOperacionMacService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    /// <summary>
    /// Consulta de compras.
    ///
    /// SI SE MANDA `factura`, SE BUSCA SOLO POR FACTURA y los demas filtros se
    /// ignoran: es la precedencia de Legacy, pero dicha. Alla el campo no tenia
    /// valor por omision, asi que una consulta SIN factura entraba igual a esa
    /// rama y tiraba en silencio todos los filtros que el usuario habia puesto.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Consultar([FromQuery] ComprasFiltro? filtro, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        filtro ??= new ComprasFiltro();

        var error = ValidaRango(filtro.CapturaDesde, filtro.CapturaHasta, "captura")
                    ?? ValidaRango(filtro.EmisionDesde, filtro.EmisionHasta, "emision")
                    ?? ValidaRango(filtro.CancelacionDesde, filtro.CancelacionHasta, "cancelacion")
                    ?? ValidaRango(filtro.PagoDesde, filtro.PagoHasta, "pago");
        if (error != null) return BadRequest(error);

        // IdUsuarioActual es la firma de quien consulta y sale del token. El
        // IdUsuarioCaptura del filtro es otra cosa: quien capturo la compra.
        var data = await _service.ConsultarComprasAsync(filtro, IdUsuarioLegacy(), ct);
        return Ok(new { ok = true, message = "Compras consultadas.", data, total = data.Count });
    }

    /// <summary>
    /// Marca o desmarca la compra como factura.
    ///
    /// La ruta dice que hace una cosa y hace una cosa: es lo UNICO que
    /// sp_n_ActualizarCompra cambia. En Legacy era un PUT generico de la compra y
    /// el campo no tenia valor por omision, asi que un PUT que no lo mandara
    /// dejaba la compra marcada como "no es factura" sin que nadie lo pidiera.
    /// </summary>
    [HttpPut("{id:int}/es-factura")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MarcarFactura([FromRoute] int id, [FromBody] MarcarCompraFacturaRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });

        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.MarcarCompraFacturaAsync(id, request.EsFactura, IdUsuarioLegacy(), Equipo(), ct);
        return Responder(data, request.EsFactura ? "Compra marcada como factura." : "Compra marcada como no factura.");
    }

    /// <summary>
    /// Cancela la compra. Devuelve la mercancia y mueve el inventario.
    ///
    /// El motivo es obligatorio: queda guardado en la compra y es lo unico que
    /// explica despues por que el inventario se movio.
    /// </summary>
    [HttpPost("{id:int}/cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cancelar([FromRoute] int id, [FromBody] CancelarCompraRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        if (string.IsNullOrWhiteSpace(request.MotivoCancelacion))
            return BadRequest(new { ok = false, message = "El motivo de la cancelacion es requerido." });
        if (request.MotivoCancelacion.Trim().Length > 1000)
            return BadRequest(new { ok = false, message = "El motivo no puede pasar de 1000 caracteres." });

        var sinPermiso = await ExigirPermisoAsync(PermisosCancelar, ct);
        if (sinPermiso != null) return sinPermiso;

        var (resultado, renglones) = await _service.CancelarCompraAsync(
            id, request.MotivoCancelacion, IdUsuarioLegacy(), Equipo(), ct);

        return Ok(new
        {
            ok = resultado.Guardo,
            message = resultado.Guardo
                ? "Compra cancelada."
                : (string.IsNullOrWhiteSpace(resultado.Mensaje) ? "La base rechazo la cancelacion." : resultado.Mensaje),
            data = resultado,
            // Los renglones traen el folio de la factura cancelada, que la
            // pantalla muestra.
            detalle = renglones
        });
    }

    /// <summary>
    /// Procesa el inventario en transito de un renglon de venta.
    ///
    /// El cliente es obligatorio: el movimiento se le carga a alguien. En Legacy
    /// solo se exigia el renglon de venta, asi que se podia procesar transito
    /// contra el cliente 0 y dependia de lo que el procedimiento hiciera con un
    /// cero.
    /// </summary>
    [HttpPost("/api/inventario/transito")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ProcesarTransito([FromBody] ProcesarTransitoRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        if (request.IdVentaDetalle <= 0)
            return BadRequest(new { ok = false, message = "El renglon de la venta es requerido." });
        if (request.IdCliente <= 0)
            return BadRequest(new { ok = false, message = "El cliente es requerido." });

        var sinPermiso = await ExigirPermisoAsync(PermisosTransito, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(
            await _service.ProcesarTransitoAsync(request, IdUsuarioLegacy(), Equipo(), ct),
            "Inventario en transito procesado.");
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

    /// <summary>
    /// Un rango de fechas tiene que estar completo o vacio. Legacy convertia la
    /// fecha inicial sin comprobar que la final viniera, y al reves, y la
    /// conversion por posicion de caracteres reventaba con la cadena vacia: la
    /// consulta devolvia 400 con el stack completo en vez de decir que faltaba una
    /// fecha. En el rango de pago el defecto era peor, porque el if miraba la
    /// fecha FINAL y convertia la INICIAL.
    /// </summary>
    private static object? ValidaRango(DateTime? desde, DateTime? hasta, string nombre)
    {
        if (desde.HasValue != hasta.HasValue)
            return new { ok = false, message = $"El rango de {nombre} necesita las dos fechas." };
        if (desde.HasValue && hasta!.Value < desde.Value)
            return new { ok = false, message = $"El rango de {nombre} termina antes de empezar." };
        return null;
    }
}
