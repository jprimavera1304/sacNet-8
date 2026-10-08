using ISL_Service.Application.DTOs.VentasGarantias;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/*
  GARANTIAS

  POR QUE `garantias.*` Y NO EL `ventas.garantias` QUE YA EXISTE
  -------------------------------------------------------------
  En el puente de permisos hay una clave `ventas.garantias`, pero viene del boton
  `btnAjustePedido` de la forma CONSULTA DE VENTAS: es el AJUSTE DE UN PEDIDO por
  garantia, una cosa que se hace desde la pantalla de ventas. Este modulo son las
  tres pantallas de garantias de Mac31, que son otra cosa y otra gente.

  Reusarla ademas la ataria al borrado de la familia `ventas.*`, donde manda
  Mac31: un permiso nuevo ahi se pierde en el siguiente recalculo del snapshot.

  ACEPTAR LLEVA SU PROPIO PERMISO porque escribe en la base de OTRA empresa (la
  del centro de servicio). Es la unica operacion de esta ola que sale del
  catalogo propio, y quien la hace es el centro, no quien captura garantias.
*/
[ApiController]
[Route("api/ventas/garantias")]
[Authorize]
public class VentasGarantiasController : PermisoControllerBase
{
    private static readonly string[] PermisoVer = { "garantias.ver" };
    private static readonly string[] PermisoEditar = { "garantias.editar" };
    private static readonly string[] PermisoCancelar = { "garantias.cancelar" };
    private static readonly string[] PermisoAceptar = { "garantias.aceptar" };
    private static readonly string[] PermisoRegistroEditar = { "garantias.registro.editar" };
    private static readonly string[] PermisoRegistroCancelar = { "garantias.registro.cancelar" };

    private readonly IVentasGarantiasService _service;

    public VentasGarantiasController(
        IVentasGarantiasService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    /// <summary>
    /// Los cuatro catalogos que abren las pantallas de garantias: empresas, los
    /// almacenes DEL USUARIO, repartidores y productos de carga.
    /// </summary>
    /*
      UNA LLAMADA, UN PERMISO: EL DE ESTA PANTALLA.

      Antes de esto, el formulario tenia que pedir tres endpoints de otros
      modulos y por eso exigia `empresas.ver`, `catalogos.ver` y `productos.ver`
      ademas de `garantias.ver`. Quien captura garantias y nada mas no podia
      trabajar: le salian tres desplegables vacios, o tres 403.

      Y lo que se pedia prestado no era poco: `empresas.ver` abre la ficha de
      empresas con sus datos fiscales y `productos.ver` el catalogo con precios,
      precio anterior y margen. Para llenar cuatro combos se estaban concediendo
      tres pantallas enteras -- el camino corto a que alguien acabe con el permiso
      de todo.

      LOS ALMACENES SON LOS DEL USUARIO. Es la otra mitad del arreglo y no es
      comodidad: GET api/catalogos/almacenes entrega el catalogo completo, y con
      el se puede registrar una garantia en un almacen al que el capturista no
      tiene acceso. Mac31 pide los del usuario (Garantia.cs:58-67) y se cierra si
      no tiene ninguno. El id de usuario sale del TOKEN, nunca del query.
    */
    [HttpGet("catalogos")]
    [ProducesResponseType(typeof(GarantiaCatalogosResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarCatalogosAsync(CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoVer, ct);
        if (veto is not null) return veto;

        var data = await _service.ConsultarCatalogosAsync(IdUsuarioLegacy(), ct);
        return Ok(new { ok = true, message = "Catalogos de garantias.", data });
    }

    /// <summary>Registra una garantia.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CrearAsync([FromBody] GarantiaCrearRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoEditar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CrearAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Garantia registrada."), data });
    }

    /// <summary>
    /// Modifica una o varias garantias. Es tambien la via por la que se cambia
    /// su estatus, incluido el de cancelada.
    /// </summary>
    [HttpPost("actualizar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ActualizarAsync([FromBody] GarantiaActualizarRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoEditar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.ActualizarAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Garantias actualizadas."), data });
    }

    /// <summary>Cancela una o varias garantias.</summary>
    [HttpPost("cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CancelarAsync([FromBody] GarantiaCancelarRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoCancelar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CancelarAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Garantias canceladas."), data });
    }

    /// <summary>Consulta las garantias.</summary>
    [HttpPost("consultar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarAsync([FromBody] GarantiaConsultaRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoVer, ct);
        if (veto is not null) return veto;

        var data = await _service.ConsultarAsync(request ?? new GarantiaConsultaRequest(), IdUsuarioLegacy(), ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Garantias.", data });
    }

    /// <summary>
    /// Acepta una garantia en un centro de servicio. Escribe en la base de ese
    /// centro, no en la de aqui.
    /// </summary>
    [HttpPost("aceptar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AceptarAsync([FromBody] GarantiaAceptarRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoAceptar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.AceptarAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Garantia aceptada."), data });
    }

    /// <summary>Consulta las garantias pendientes de aceptar o ya aceptadas.</summary>
    [HttpPost("aceptar/consultar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarAceptarAsync([FromBody] GarantiaConsultaRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoVer, ct);
        if (veto is not null) return veto;

        var data = await _service.ConsultarAceptarAsync(request ?? new GarantiaConsultaRequest(), IdUsuarioLegacy(), ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Garantias por aceptar.", data });
    }

    /// <summary>Da de alta un registro previo de garantia.</summary>
    [HttpPost("registros")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CrearRegistroAsync([FromBody] GarantiaRegistroCrearRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoRegistroEditar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CrearRegistroAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Registro de garantia creado."), data });
    }

    /// <summary>Cancela uno o varios registros previos de garantia.</summary>
    [HttpPost("registros/cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CancelarRegistroAsync([FromBody] GarantiaRegistroCancelarRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoRegistroCancelar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CancelarRegistroAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Registros cancelados."), data });
    }

    /// <summary>Consulta los registros previos de garantia.</summary>
    [HttpPost("registros/consultar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarRegistroAsync([FromBody] GarantiaRegistroConsultaRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoVer, ct);
        if (veto is not null) return veto;

        var data = await _service.ConsultarRegistroAsync(request ?? new GarantiaRegistroConsultaRequest(), ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Registros de garantia.", data });
    }

    private static string Mensaje(bool hecho, string siSeHizo)
        => hecho ? siSeHizo : "El procedimiento rechazo la operacion.";
}
