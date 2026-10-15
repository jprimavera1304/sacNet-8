using ISL_Service.Application.DTOs.InventarioOperaciones;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/*
  AJUSTES DE INVENTARIO, INVENTARIO DE PEDIDOS Y TIPOS DE CASCO

  La familia `inventario.*` es nueva porque no existe en Mac31 como tal: alla
  estas pantallas cuelgan del menu de inventario y su permiso no esta en el
  puente de legacy (el puente va por UNA forma por modulo, y la forma de ventas
  ya esta tomada por `ventas.*`). Al no ser `ventas.*`, `reportes.*`,
  `empleados.*` ni `prestamos.*`, lo que se guarde desde la pantalla de permisos
  web SI se conserva.

  El catalogo de tipos de casco vive aqui y no en `catalogos.*` por un motivo
  concreto: sus renglones son PRECIOS. `catalogos.ver` abre desplegables —
  ciudades, puestos, unidades de medida—, y en la ola 1 ya se separo
  `catalogos.galleros.ver` por exactamente lo mismo: el renglon traia saldo. Un
  tipo de casco trae cuatro precios, una bonificacion y un costo de excedente;
  quien puede editar eso decide lo que se le cobra al cliente.
*/
[ApiController]
[Route("api/inventario")]
[Authorize]
public class InventarioOperacionesController : PermisoControllerBase
{
    /*
      LOS AJUSTES TIENEN DOS CANALES; LOS PEDIDOS DE INVENTARIO, UNO.

      `inventario.ajustes.*` lleva su pareja `app_movil.` porque la pantalla de
      Ajustes existe en el telefono y hasta ahora compartia el permiso del web:
      negarselo en la app se lo negaba tambien en la computadora.

      `inventario.pedidos.*` y `usados.tipos.editar` NO la llevan, y no es un
      olvido: esas dos pantallas no existen en la app. Una clave `app_movil.`
      que nada pide seria una casilla que alguien reparte creyendo que hace
      algo.
    */
    private static readonly string[] PermisoAjusteVer = { "inventario.ajustes.ver", "app_movil.inventario.ajustes.ver" };
    private static readonly string[] PermisoAjusteCrear = { "inventario.ajustes.crear", "app_movil.inventario.ajustes.crear" };
    private static readonly string[] PermisoAjusteCancelar = { "inventario.ajustes.cancelar", "app_movil.inventario.ajustes.cancelar" };
    private static readonly string[] PermisoPedidoVer = { "inventario.pedidos.ver" };
    private static readonly string[] PermisoPedidoCrear = { "inventario.pedidos.crear" };
    private static readonly string[] PermisoPedidoCancelar = { "inventario.pedidos.cancelar" };
    private static readonly string[] PermisoTiposEditar = { "usados.tipos.editar" };

    private readonly IInventarioOperacionesService _service;

    public InventarioOperacionesController(
        IInventarioOperacionesService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    /* ------------------------ Ajustes de inventario ------------------------ */

    /// <summary>Captura un ajuste manual de existencias.</summary>
    [HttpPost("ajustes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CrearAjusteAsync([FromBody] AjusteInventarioCrearRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoAjusteCrear, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CrearAjusteAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Ajuste capturado."), data });
    }

    /// <summary>Cancela un ajuste de inventario.</summary>
    [HttpPost("ajustes/cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CancelarAjusteAsync([FromBody] AjusteInventarioCancelarRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoAjusteCancelar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CancelarAjusteAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Ajuste cancelado."), data });
    }

    /// <summary>Consulta los ajustes de inventario de un rango de fechas.</summary>
    [HttpPost("ajustes/consultar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarAjustesAsync([FromBody] AjusteInventarioConsultaRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoAjusteVer, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.ConsultarAjustesAsync(request, IdUsuarioLegacy(), ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Ajustes de inventario.", data });
    }

    /* ----------------------- Inventario de pedidos ----------------------- */

    /// <summary>Captura un inventario de pedidos.</summary>
    [HttpPost("pedidos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CrearInventarioPedidoAsync([FromBody] InventarioPedidoCrearRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoPedidoCrear, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CrearInventarioPedidoAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Inventario de pedidos capturado."), data });
    }

    /// <summary>Cancela un inventario de pedidos.</summary>
    [HttpPost("pedidos/cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CancelarInventarioPedidoAsync([FromBody] InventarioPedidoCancelarRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoPedidoCancelar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CancelarInventarioPedidoAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Inventario de pedidos cancelado."), data });
    }

    /// <summary>Consulta los inventarios de pedidos.</summary>
    [HttpPost("pedidos/consultar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarInventarioPedidosAsync([FromBody] InventarioPedidoConsultaRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoPedidoVer, ct);
        if (veto is not null) return veto;

        var data = await _service.ConsultarInventarioPedidosAsync(request ?? new InventarioPedidoConsultaRequest(), ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Inventario de pedidos.", data });
    }

    /* --------------------- Catalogo de tipos de casco --------------------- */

    /// <summary>Da de alta un tipo de casco usado.</summary>
    [HttpPost("tipos-usado")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CrearTipoUsadoAsync([FromBody] TipoUsadoCrearRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoTiposEditar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CrearTipoUsadoAsync(request, ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Tipo de casco creado."), data });
    }

    /// <summary>
    /// Modifica un tipo de casco usado. NO cambia su estatus: para eso esta
    /// `tipos-usado/status`.
    /// </summary>
    [HttpPut("tipos-usado")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActualizarTipoUsadoAsync([FromBody] TipoUsadoActualizarRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoTiposEditar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.ActualizarTipoUsadoAsync(request, ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Tipo de casco actualizado."), data });
    }

    /// <summary>
    /// Activa o inhabilita un tipo de casco usado. Reemplaza al DELETE de
    /// legacy, que reusaba el actualizar completo y, con un cuerpo incompleto,
    /// dejaba el renglon con todos los precios en cero.
    /// </summary>
    [HttpPost("tipos-usado/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CambiarStatusTipoUsadoAsync([FromBody] TipoUsadoCambiarStatusRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoTiposEditar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CambiarStatusTipoUsadoAsync(request, ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Status del tipo de casco actualizado."), data });
    }

    private static string Mensaje(bool hecho, string siSeHizo)
        => hecho ? siSeHizo : "El procedimiento rechazo la operacion.";
}
