using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Los detalles de usados (cascos) que MacServicios2 servia: el renglonaje de
/// un movimiento de usados, de un cobro de usados, de un cobro de dinero y
/// usados, de un excedente, y la bitacora de creditos de usados de una venta.
///
/// POR QUE `api/ventas/usados/consulta` Y NO `api/ventas/usados`
/// ------------------------------------------------------------
/// Porque `api/ventas/usados` ya es de `VentasUsadosController`, que es la
/// pantalla de MODIFICAR los usados de una remision (btnUsados de Mac31) y
/// escribe. Esto es la mitad de solo lectura y de otras pantallas; compartir
/// ruta haria que un permiso de escritura y uno de lectura vivieran en el mismo
/// prefijo y se confundieran al leer el codigo.
///
/// LO QUE NO ESTA AQUI: las cabeceras. `usadosmovimientos/consultar`,
/// `ventascobrosusados/consultar`, `ventascobrosdinerousados/consultar` (y sus
/// variantes de cheques y cortes) y `ventasusadosexcedentes/consultar` empiezan
/// en legacy por `sp_n_ConsultaConstantes`, que hace el cierre del dia cuando la
/// fecha de operacion esta atrasada
/// (`migracion/CUIDADO-consultas-que-escriben.md`). Esta ola es de solo
/// lectura, asi que se quedaron fuera.
/// </summary>
[ApiController]
[Route("api/ventas/usados/consulta")]
[Authorize]
public class VentasUsadosConsultaController : PermisoControllerBase
{
    /*
      SE REUSA `ventas.usados`, QUE SI LO MANDA MAC31 Y AQUI SIRVE.

      Es btnUsados de la forma CONSULTA DE VENTAS, ya mapeado en
      PermissionService (VentasLegacyPermissionMap). A diferencia de la
      cobranza, estas cinco lecturas SON de la pantalla de usados de Consultar
      ventas: quien puede abrir los usados de una remision es exactamente quien
      debe poder ver su detalle y su bitacora. Reusarlo mantiene una sola
      verdad —la de Mac31— en vez de dos interruptores que hay que acordarse de
      encender juntos.

      Se acepta tambien el permiso del canal movil, por la razon de siempre: el
      telefono tiene sus propias claves porque se presta y la computadora de la
      oficina no.
    */
    private static readonly string[] PermisosVer = { "ventas.usados", "app_movil.ventas.usados" };

    private readonly IVentasUsadosConsultaService _service;

    public VentasUsadosConsultaController(
        IVentasUsadosConsultaService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    /// <summary>El renglonaje de un movimiento de usados (entradas y salidas por tipo).</summary>
    [HttpGet("movimientos/{idUsadoMovimiento:int}/detalle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MovimientoDetalle(int idUsadoMovimiento, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso is not null) return sinPermiso;

        if (idUsadoMovimiento <= 0)
            return BadRequest(new { ok = false, message = "Selecciona un movimiento de usados." });

        var data = await _service.ConsultarMovimientoDetalleAsync(idUsadoMovimiento, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Detalle del movimiento consultado.", data });
    }

    /// <summary>El renglonaje de un cobro de usados.</summary>
    /// <remarks>
    /// Los dos filtros son alternativos, como en legacy: se pide por el id del
    /// ingreso de usados o por el cobro al que pertenece. Se exige al menos uno
    /// porque con los dos en cero el procedimiento barre la tabla entera.
    /// </remarks>
    [HttpGet("cobros/detalle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CobroUsadoDetalle(
        [FromQuery] int idVentaCobroUsado,
        [FromQuery] int idCobro,
        CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso is not null) return sinPermiso;

        if (idVentaCobroUsado <= 0 && idCobro <= 0)
            return BadRequest(new { ok = false, message = "Indica el ingreso de usados o el cobro." });

        var data = await _service.ConsultarCobroUsadoDetalleAsync(idVentaCobroUsado, idCobro, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Detalle del cobro de usados consultado.", data });
    }

    /// <summary>El renglonaje de un cobro de dinero y usados.</summary>
    [HttpGet("cobros-dinero/detalle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CobroDineroUsadoDetalle(
        [FromQuery] int idCobroDineroUsado,
        [FromQuery] int idCobro,
        CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso is not null) return sinPermiso;

        if (idCobroDineroUsado <= 0 && idCobro <= 0)
            return BadRequest(new { ok = false, message = "Indica el cobro de dinero y usados o el cobro." });

        var data = await _service.ConsultarCobroDineroUsadoDetalleAsync(idCobroDineroUsado, idCobro, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Detalle del cobro de dinero y usados consultado.", data });
    }

    /// <summary>El renglonaje de un excedente de usados.</summary>
    /// <remarks>
    /// Cuatro filtros alternativos (excedente, cobro, cliente o pago de venta),
    /// los mismos que legacy manda al procedimiento.
    /// </remarks>
    [HttpGet("excedentes/detalle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExcedenteDetalle(
        [FromQuery] int idVentaUsadoExcedente,
        [FromQuery] int idCobro,
        [FromQuery] int idCliente,
        [FromQuery] int idPagoVenta,
        CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso is not null) return sinPermiso;

        if (idVentaUsadoExcedente <= 0 && idCobro <= 0 && idCliente <= 0 && idPagoVenta <= 0)
            return BadRequest(new { ok = false, message = "Indica el excedente, el cobro, el cliente o el pago." });

        var data = await _service.ConsultarExcedenteDetalleAsync(idVentaUsadoExcedente, idCobro, idCliente, idPagoVenta, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Detalle del excedente consultado.", data });
    }

    /// <summary>La bitacora de creditos de usados de una remision.</summary>
    /// <remarks>
    /// Es `[Ventas Usados Creditos Log]`: quien cambio los cascos a credito de
    /// esa venta, cuando y desde que equipo. Se LEE la bitacora; nada se
    /// escribe aqui.
    /// </remarks>
    [HttpGet("creditos/{idVenta:int}/log")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreditoLog(int idVenta, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso is not null) return sinPermiso;

        // Legacy tambien lo exige (VentasController.cs:142: IDVenta == 0 -> 400).
        if (idVenta <= 0)
            return BadRequest(new { ok = false, message = "Selecciona una remisión." });

        var data = await _service.ConsultarCreditoLogAsync(idVenta, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Bitácora de créditos de usados consultada.", data });
    }
}
