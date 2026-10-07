using ISL_Service.Application.DTOs.Maestros;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Escrituras de la ficha del producto.
///
/// Cuelga de api/productos, donde la ola 3 dejo la lectura (GET api/productos).
///
/// LA MODIFICACION PIDE LA FICHA COMPLETA. El UPDATE de
/// sp_n_ActualizarProducto escribe todas las columnas, incluidos el STOCK y los
/// TRES PRECIOS. Un PATCH aqui dejaria el inventario en cero y el precio en
/// cero sin avisar, que es exactamente lo que hacia el DELETE de legacy.
/// </summary>
[ApiController]
[Route("api/productos")]
[Authorize]
public class ProductosAdminController : MaestrosControllerBase
{
    private static readonly string[] PermisosCrear = { "productos.crear" };
    private static readonly string[] PermisosEditar = { "productos.editar" };
    private static readonly string[] PermisosBaja = { "productos.estado.editar" };

    public ProductosAdminController(
        IMaestrosService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(service, currentUser, permissionService)
    {
    }

    /// <summary>
    /// Alta de producto.
    ///
    /// Sin codigo de barras se usa el codigo, que es lo que hace Mac31: el lector
    /// de la caja dispara la misma busqueda con los dos.
    /// </summary>
    /// <response code="201">Producto dado de alta</response>
    /// <response code="400">Datos invalidos, o legacy rechazo por una regla</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Crear([FromBody] CrearProductoRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaProducto(request);
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        return RespondeAlta(await Service.CrearProductoAsync(request, ct), "Producto dado de alta.");
    }

    /// <summary>
    /// Modificacion de producto. Ficha completa (ver la nota de la clase).
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Actualizar([FromRoute] int id, [FromBody] ActualizarProductoRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaProducto(request);
        if (error != null) return BadRequest(error);
        if (request.IdStatus is not (StatusActivo or StatusCancelado))
            return BadRequest(new { ok = false, message = "El estatus solo puede ser 1 (activo) o 2 (cancelado)." });

        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await Service.ActualizarProductoAsync(id, request, IdUsuarioLegacy(), Equipo(), ct),
            "Producto actualizado.");
    }

    /// <summary>
    /// Baja logica del producto: IDStatus = 2 conservando precios y existencia.
    ///
    /// No pide cuerpo. En legacy este DELETE llamaba al mismo UPDATE con lo que
    /// trajera la peticion y solo exigia el id: dar de baja un producto lo dejaba
    /// con precio cero, stock cero y sin categoria, y el historico de compras
    /// dejaba de cuadrar contra el catalogo. (Ademas reventaba con una referencia
    /// nula si el cuerpo no traia la clave.)
    /// </summary>
    /// <response code="404">El producto no existe</response>
    [HttpPost("{id:int}/inhabilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Inhabilitar([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await Service.CambiarStatusProductoAsync(id, StatusCancelado, IdUsuarioLegacy(), Equipo(), ct),
            "Producto inhabilitado.", "El producto no existe.");
    }

    /// <summary>Vuelve a activar el producto.</summary>
    [HttpPost("{id:int}/habilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Habilitar([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await Service.CambiarStatusProductoAsync(id, StatusActivo, IdUsuarioLegacy(), Equipo(), ct),
            "Producto habilitado.", "El producto no existe.");
    }

    /// <summary>
    /// Adjunta un archivo al producto (su ficha o su foto).
    ///
    /// EL PRECIO NO SE PIDE. sp_n_ActualizarProductoFile recibe @PrecioLista y lo
    /// escribe; en legacy, quien subia una foto sin mandar el precio dejaba el
    /// precio del producto en CERO. Aqui el backend lee el precio actual y se lo
    /// devuelve al procedimiento, para que adjuntar un archivo no pueda cambiar
    /// un precio.
    /// </summary>
    /// <response code="404">El producto no existe</response>
    [HttpPut("{id:int}/archivo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GuardarArchivo([FromRoute] int id, [FromBody] ArchivoProductoRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaTexto(request.Archivo, 4000, "archivo");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await Service.GuardarArchivoProductoAsync(id, request, IdUsuarioLegacy(), Equipo(), ct),
            "Archivo guardado.", "El producto no existe.");
    }

    /// <summary>
    /// Lo obligatorio del producto. Los topes son los de los varchar del
    /// procedimiento; el del nombre es 1000 en el alta y 1000 en la
    /// modificacion, se toma el menor para que el mismo texto valga en los dos.
    /// </summary>
    private static object? ValidaProducto(CrearProductoRequest r)
    {
        var error = ValidaId(r.IdCategoria, "idCategoria")
                    ?? ValidaId(r.IdMarca, "idMarca")
                    ?? ValidaTexto(r.Codigo, 1000, "codigo")
                    ?? ValidaTexto(r.Producto, 1000, "producto")
                    ?? ValidaTopeOpcional(r.CodigoBarras, 1000, "codigoBarras")
                    ?? ValidaTopeOpcional(r.Descripcion, 2000, "descripcion")
                    ?? ValidaTopeOpcional(r.DescripcionSat, 2000, "descripcionSat")
                    ?? ValidaTopeOpcional(r.Observaciones, 1000, "observaciones")
                    ?? ValidaTopeOpcional(r.UnidadMedidaSat, 200, "unidadMedidaSat")
                    ?? ValidaTopeOpcional(r.ClaveSat, 200, "claveSat")
                    ?? ValidaTopeOpcional(r.IdFacturama, 400, "idFacturama");
        if (error != null) return error;

        if (r.PrecioLista < 0 || r.PrecioLista2 < 0 || r.PrecioListaCS < 0)
            return new { ok = false, message = "Un precio no puede ser negativo." };

        if (r.StockMinimo < 0 || r.StockMaximo < 0)
            return new { ok = false, message = "El stock minimo y el maximo no pueden ser negativos." };

        if (r.StockMaximo > 0 && r.StockMinimo > r.StockMaximo)
            return new { ok = false, message = "El stock minimo no puede ser mayor que el maximo." };

        if (r.PesoKg < 0 || r.Litros < 0)
            return new { ok = false, message = "El peso y los litros no pueden ser negativos." };

        // NumPiezas en cero deja un producto del que no se puede contar cuantas
        // piezas lleva un pedido: 'productos' en un pedido es la SUMA de piezas.
        if (r.NumPiezas < 0)
            return new { ok = false, message = "El numero de piezas no puede ser negativo." };

        return null;
    }
}
