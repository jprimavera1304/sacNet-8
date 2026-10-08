using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// La CONSULTA del inventario en transito. Faltaba: solo existia el POST que lo
/// procesa (ComprasController, con permiso `inventario.transito.procesar`), asi
/// que se podia aceptar un renglon pero no ver que habia pendiente.
///
/// QUE PERMISO LLEVA Y POR QUE NO ES `compras.ver`
/// -----------------------------------------------
/// Lo que lista no son compras a proveedor: son REMISIONES DE VENTA de la matriz
/// hacia los centros de servicio que el centro todavia no ha aceptado. Otro
/// origen, otra pantalla y otra gente. Lleva `inventario.transito.ver`, que es la
/// pareja de lectura de `inventario.transito.procesar`, el permiso que ya existia
/// para el POST. Dos claves de la misma familia: ver lo que viene en camino y
/// aceptarlo no son la misma decision -- quien recibe la mercancia la acepta,
/// quien planea la ruta solo necesita verla.
///
/// Va en su propia clase y no dentro de ComprasController porque ese controlador
/// resuelve permisos con ExigirPermisoAsync (modelo por empresa) y esta lectura
/// usa la politica `perm:` como el resto de las consultas nuevas.
/// </summary>
[ApiController]
[Route("api/inventario/transito")]
[Authorize]
public class InventarioTransitoConsultaController : ControllerBase
{
    private readonly IInventarioTransitoConsultaService _service;

    public InventarioTransitoConsultaController(IInventarioTransitoConsultaService service)
    {
        _service = service;
    }

    /// <summary>
    /// Lo que va en camino a los centros de servicio.
    /// </summary>
    /// <param name="status">
    /// 0 = todos, 1 = en transito (default, como el combo de Mac31), 2 = aceptado.
    /// OJO: con 1 el procedimiento NO aplica el rango de fechas, solo
    /// "sin aceptar"; el rango solo filtra con 0 y 2.
    /// </param>
    /// <param name="idCliente">
    /// Centro de servicio. 0 = el de esta instalacion (Constantes.IDClienteCS) y,
    /// si no hay, todos los centros.
    /// </param>
    /// <param name="desde">
    /// Fecha de emision inicial. Si no viene, HOY. No se puede dejar vacia:
    /// ver el comentario de abajo.
    /// </param>
    /// <param name="hasta">Fecha de emision final. Si no viene, HOY (el dia completo).</param>
    /// <param name="agrupado">
    /// true = un renglon por remision (la rejilla de arriba de Mac31);
    /// false = un renglon por pieza (la de abajo).
    /// </param>
    /// <response code="200">Inventario en transito consultado</response>
    /// <response code="400">El rango de fechas esta incompleto o al reves</response>
    [HttpGet]
    [Authorize(Policy = "perm:inventario.transito.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Consultar(
        [FromQuery] int status = 1,
        [FromQuery] int idCliente = 0,
        [FromQuery] DateTime? desde = null,
        [FromQuery] DateTime? hasta = null,
        [FromQuery] bool agrupado = false,
        CancellationToken ct = default)
    {
        /*
          LAS FECHAS NO PUEDEN QUEDAR VACIAS, Y NO ES UN CAPRICHO DE VALIDACION.

          sp_n_ConsultaVentasTransito tiene un bloque que se dispara cuando
          @StatusInventario = 1 Y @FechaEmisionInicial = '': abre un cursor y
          llama sp_n_ProcesarInventarioTransito por cada renglon encontrado,
          firmando como el usuario 1057 ('SISTEMA'). O sea, ACEPTA el inventario y
          mueve existencias, y ni siquiera devuelve el listado.

          Un GET no puede tener ese camino. Con las dos fechas puestas el SP se
          queda en la rama de consulta. El default es HOY, igual que la pantalla
          de Mac31, que inicializa los dos selectores en la fecha actual
          (InventarioTransito_Load). El repositorio lo vuelve a comprobar, para
          que no dependa de que alguien mas llame bien.
        */
        var hoy = DateTime.Today;
        var inicial = (desde ?? hoy).Date;
        var final = (hasta ?? hoy).Date;

        if (final < inicial)
            return BadRequest(new { ok = false, message = "El rango de emision termina antes de empezar." });

        if (status is < 0 or > 2)
            return BadRequest(new { ok = false, message = "status solo admite 0 (todos), 1 (en transito) o 2 (aceptado)." });

        var data = await _service.ConsultarAsync(new InventarioTransitoFiltro
        {
            StatusInventario = status,
            IDCliente = idCliente,
            FechaEmisionInicial = inicial,
            FechaEmisionFinal = final,
            Agrupado = agrupado
        }, ct);

        return Ok(new { ok = true, message = "Inventario en transito consultado.", data, total = data.Count });
    }
}
