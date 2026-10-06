using ISL_Service.Application.DTOs.VentasDescuentos;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// El catalogo de descuentos por categoria y marca, de solo lectura: el
/// renglonaje de un descuento y su bitacora de cambios.
///
/// LO QUE NO ESTA AQUI
/// -------------------
/// * `POST /api/descuentos/consultar` de legacy llama a `sp_n_ConsultaDescuentos`,
///   que **no existe en esta base** (se comprobo en `sys.procedures`: hay
///   `sp_Descuentos`, pero no `sp_n_ConsultaDescuentos`). Legacy falla igual, asi
///   que no hay contra que comparar y no se migro.
/// * `POST /api/descuentos/simular` **escribe**: `sp_n_SimularDescuento` hace
///   `INSERT INTO Descuentos` cuando no encuentra un renglon de simulacion para
///   ese usuario y equipo (leido de `sys.sql_modules`). El registro.tsv lo daba
///   como lectura por el nombre; no lo es. Esta ola es de solo lectura.
/// </summary>
[ApiController]
[Route("api/ventas/descuentos")]
[Authorize]
public class VentasDescuentosConsultaController : PermisoControllerBase
{
    /*
      PERMISO NUEVO Y DEL WEB, POR LA MISMA RAZON QUE EN COBRANZA.

      En Mac31 el catalogo de descuentos es su propia forma, no un boton de
      CONSULTA DE VENTAS, asi que no hay un `ventas.*` que signifique esto. Y
      cualquier clave `ventas.*` que no este en la lista blanca del web se borra
      en el siguiente calculo del snapshot (seccion 2.8 del resumen del
      inventario).

      Tampoco se reuso `catalogos.ver` (la familia que sembro la ola de
      catalogos): un descuento no es un desplegable de captura, es **dinero** —
      el porcentaje que se le va a aplicar a un cliente— y su bitacora dice quien
      lo movio. Es el mismo criterio con el que alla se separo
      `catalogos.galleros.ver`, porque ese renglon traia saldo.
    */
    private static readonly string[] PermisosVer = { "descuentos.ver" };

    private readonly IVentasDescuentosConsultaService _service;

    public VentasDescuentosConsultaController(
        IVentasDescuentosConsultaService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    /// <summary>Los porcentajes de un descuento por grupo-categoria, categoria y marca.</summary>
    [HttpPost("detalle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Detalle([FromBody] DescuentoDetalleRequest? body, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso is not null) return sinPermiso;

        var data = await _service.ConsultarDetalleAsync(body ?? new DescuentoDetalleRequest(), ct);
        return Ok(new { ok = true, message = "Detalle del descuento consultado.", data });
    }

    /// <summary>
    /// La bitacora: que porcentaje habia, cual quedo, quien lo cambio y cuando.
    /// </summary>
    /// <remarks>
    /// Se exige el descuento. Legacy no lo exige y con todo en cero el
    /// procedimiento devuelve la bitacora COMPLETA (6,495 renglones en la base de
    /// pruebas, y crece sola): una pantalla que pide "el historial" sin decir de
    /// que no esta pidiendo nada util, esta barriendo la tabla.
    /// </remarks>
    [HttpPost("detalle/log")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DetalleLog([FromBody] DescuentoDetalleLogRequest? body, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso is not null) return sinPermiso;

        var req = body ?? new DescuentoDetalleLogRequest();
        if (req.IDDescuento <= 0)
            return BadRequest(new { ok = false, message = "Selecciona un descuento." });

        var data = await _service.ConsultarDetalleLogAsync(req, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Bitácora del descuento consultada.", data });
    }
}
