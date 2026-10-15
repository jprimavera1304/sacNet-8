using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// El catalogo de productos con precios y existencias.
///
/// PERMISO PROPIO Y NO `catalogos.ver`: el renglon trae precio de lista, precio
/// anterior, la diferencia entre los dos y el stock. Es la lista de precios de
/// la empresa, no un desplegable de captura. Quien necesita llenar un combo de
/// productos para capturar un pedido ya tiene
/// `POST api/ventas/pedidos/captura/productos/buscar`, que devuelve lo justo.
///
/// SOLO LECTURA: `sp_n_ActualizarProducto*` existe y no se expone.
/// </summary>
[ApiController]
[Route("api/productos")]
[Authorize]
public class ProductosController : ControllerBase
{
    /// <summary>
    /// Tope de porcentajes de descuento. Son los escalones de la lista Roshfrans
    /// y el SP los aplica con un cursor anidado por producto: con una lista larga
    /// la consulta se vuelve impracticable. Diez es mas de los que usa cualquier
    /// lista real.
    /// </summary>
    private const int MaxPorcentajes = 10;

    private readonly IProductosService _service;

    public ProductosController(IProductosService service)
    {
        _service = service;
    }

    /// <summary>
    /// Consulta el catalogo de productos.
    /// </summary>
    /// <param name="idStatus">0 = todos (default), 1 = activos, 2 = inactivos.</param>
    /// <param name="clave">
    /// Codigo del producto. Busca por PREFIJO (LIKE 'clave%'). Si se manda junto
    /// con <paramref name="descripcion"/>, el SP las une con OR, no con AND.
    /// </param>
    /// <param name="descripcion">Busca por contenido (LIKE %descripcion%).</param>
    /// <param name="listaRoshfrans">
    /// true = solo los productos marcados como lista Roshfrans, con
    /// <paramref name="porcentajesDescuento"/> aplicados en cascada sobre el
    /// precio con IVA.
    /// </param>
    /// <param name="esCarga">true = solo productos de carga.</param>
    /// <param name="porcentajesDescuento">
    /// Los escalones de descuento, EN ORDEN DE APLICACION: se aplican en cascada,
    /// asi que el orden cambia el resultado. Solo tienen efecto con
    /// <paramref name="listaRoshfrans"/> en true.
    /// </param>
    /// <response code="200">Catalogo consultado</response>
    /// <response code="400">
    /// Se pidieron a la vez listaRoshfrans y esCarga, o demasiados porcentajes
    /// </response>
    [HttpGet]
    /* Misma razon que en PersonasController: CONSULTA DE PRODUCTOS se abre en
       Mac31 con cualquier boton concedido. NAOLANY tiene NUEVO y LISTA DE
       PRECIOS ROSHFRANS y no tiene VER, y la pantalla la rebotaba entera. */
    /*
      DOS CANALES Y DOS PANTALLAS DEL TELEFONO.

      `app_movil.productos` es la del modulo Productos. `app_movil.inventario.
      ajustes.ver` esta porque el buscador de producto de AJUSTES DE INVENTARIO
      pega aqui: sin ella, un usuario de solo movil abriria Ajustes y no podria
      elegir que producto ajustar, que es la pantalla entera.
    */
    [Authorize(Policy = "perm:productos.ver|app_movil.productos|app_movil.inventario.ajustes.ver|modulo:productos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Consultar(
        [FromQuery] int id = 0,
        [FromQuery] int[]? idsMarca = null,
        [FromQuery] int[]? idsCategoria = null,
        [FromQuery] int[]? idsGrupoCategoria = null,
        [FromQuery] int idStatus = 0,
        [FromQuery] string? clave = null,
        [FromQuery] string? descripcion = null,
        [FromQuery] bool exacto = false,
        [FromQuery] bool listaRoshfrans = false,
        [FromQuery] bool esCarga = false,
        [FromQuery] decimal[]? porcentajesDescuento = null,
        CancellationToken ct = default)
    {
        /*
          LOS DOS INTERRUPTORES NO PUEDEN VENIR JUNTOS, Y ESTO ES UN 400 A
          PROPOSITO.

          Dentro del SP, tanto @ListaRoshfrans = 1 como @EsCarga = 1 REINICIAN el
          WHERE completo: tiran el status, el id, las tres listas, la clave y la
          descripcion que ya se habian acumulado. Y si llegan los dos, gana
          @EsCarga porque su bloque va despues — pero los cursores de descuento se
          ejecutan igual, porque dependen de @ListaRoshfrans. O sea: se pide la
          lista Roshfrans, se recibe la de carga, con precios descontados, y nada
          avisa. Legacy lo dejaba pasar. Preferimos decir que no se puede.
        */
        if (listaRoshfrans && esCarga)
            return BadRequest(new { message = "listaRoshfrans y esCarga no se pueden pedir juntos: cada uno reemplaza el resto de los filtros." });

        if (porcentajesDescuento is { Length: > MaxPorcentajes })
            return BadRequest(new { message = $"Maximo {MaxPorcentajes} porcentajes de descuento." });

        var data = await _service.ConsultarAsync(new ProductosFiltro
        {
            IDProducto = id,
            IDsMarca = idsMarca,
            IDsCategoria = idsCategoria,
            IDsGrupoCategoria = idsGrupoCategoria,
            IDStatus = idStatus,
            Clave = clave,
            Descripcion = descripcion,
            Exacto = exacto,
            ListaRoshfrans = listaRoshfrans,
            EsCarga = esCarga,
            PorcentajesDescuento = porcentajesDescuento
        }, ct);

        // La lista entera son miles de renglones con cursores de por medio; que
        // el navegador no la guarde es mas importante que ahorrarse la llamada,
        // porque un precio cacheado es un precio equivocado.
        Response.Headers["Cache-Control"] = "no-store";

        return Ok(new { ok = true, message = "Productos consultados.", data, total = data.Count });
    }
}
