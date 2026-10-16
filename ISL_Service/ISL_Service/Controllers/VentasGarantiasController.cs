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
    /*
      LA PAREJA WEB/MOVIL DE LA PANTALLA PRINCIPAL DE GARANTIAS.

      `PermisoVer` SI lleva la clave del movil, y aqui la razon es la contraria
      que en la bandeja de aceptar (ver la nota de `PermisoAceptarVer`): esta
      clave abre `catalogos` y `consultar`, que son los DOS endpoints de la
      pantalla de garantias del telefono, y ninguno de los dos es de otra
      pantalla. `app_movil.garantias.ver` por tanto NO ensancha nada: los
      registros previos tienen su propia pareja mas abajo y la bandeja de
      aceptar ya tiene la suya.

      Los `app_movil.*` hay que sembrarlos en el catalogo; el bloque esta en
      `Movil/mac-mac-mobile/lib/modules/verticals/garantias.permisos.md`.
      Mientras no esten, esto se porta exactamente como antes.
    */
    private static readonly string[] PermisoVer =
        { "garantias.ver", "app_movil.garantias.ver" };

    private static readonly string[] PermisoEditar =
        { "garantias.editar", "app_movil.garantias.editar" };

    /*
      CANCELAR NO LLEVA CLAVE DE MOVIL, Y ES A PROPOSITO.

      Este endpoint llama a `sp_n_CancelarGarantia`, y ese procedimiento NO
      cancela: pone `IDStatusGarantia = 5` —ENVIADA AL PROVEEDOR— con el
      comentario `-- 5 - CANCELADA` al lado (el 4 es Cancelada,
      `Utils/Enums.cs:329`), y ademas cancela la Venta y el Pedido ligados sin
      una sola comprobacion. Mac31 nunca lo usa: cancela mandando el estatus 4
      a `actualizar`, que es la rama correcta.

      Asi que el movil NO pasa por aqui: cancela por `actualizar` con el estatus
      4, como Mac31, y exige la clave `app_movil.garantias.cancelar` EN EL
      CLIENTE para no perder la separacion de permisos. Darle aqui la clave del
      telefono seria invitarlo a usar el camino roto.

      Lo que falta para cerrarlo de verdad es un `sp_w_CancelarGarantia` (que
      ponga el 4, mire @@ROWCOUNT y no toque la venta sin los cinco frenos de
      `ValidaCancelarVenta`), uno por empresa. Queda en
      `garantias.permisos.md`.
    */
    private static readonly string[] PermisoCancelar = { "garantias.cancelar" };
    /*
      LA PAREJA WEB/MOVIL DE **ESTA** BANDEJA, Y SOLO DE ESTA.

      `PermisoVer` ("garantias.ver") lo comparten las TRES pantallas de
      garantias: catalogos, consulta, registros previos y esta. Agregarle la
      clave del movil habria dado, con un solo permiso de telefono, la lectura
      de la familia entera.

      Asi que la bandeja de aceptacion lleva sus propias dos listas. Las claves
      del web siguen valiendo —nadie pierde la pantalla el dia del despliegue— y
      las del movil solo abren esto. `ExigirPermisoAsync` pide CUALQUIERA de la
      lista.

      Las dos de `app_movil.` hay que sembrarlas en el catalogo; el bloque esta
      en `Movil/mac-mac-mobile/lib/modules/verticals/garantias_aceptadas.permisos.md`.
      Mientras no esten, esto se porta exactamente como antes.
    */
    private static readonly string[] PermisoAceptar =
        { "garantias.aceptar", "app_movil.garantias.aceptar" };

    private static readonly string[] PermisoAceptarVer =
        { "garantias.ver", "app_movil.garantias.aceptar.ver" };
    private static readonly string[] PermisoRegistroEditar =
        { "garantias.registro.editar", "app_movil.garantias.registros.editar" };

    private static readonly string[] PermisoRegistroCancelar =
        { "garantias.registro.cancelar", "app_movil.garantias.registros.cancelar" };

    /*
      LA CONSULTA DE REGISTROS PREVIOS LLEVA SU PROPIA LISTA.

      Exigia `PermisoVer`, que en el web es el permiso de la familia. Para el
      telefono eso significaba que la clave de ver garantias abria tambien los
      registros previos, y son otra tabla y otra tarea: el movil pide
      `app_movil.garantias.registros.ver` para esto y nada mas. La clave del web
      sigue valiendo, asi que nadie pierde la pantalla el dia del despliegue.
    */
    private static readonly string[] PermisoRegistroVer =
        { "garantias.ver", "app_movil.garantias.registros.ver" };

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
        // La lista PROPIA de esta bandeja, no la de toda la familia. Ver la nota
        // de PermisoAceptarVer arriba.
        var veto = await ExigirPermisoAsync(PermisoAceptarVer, ct);
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
        // La lista PROPIA de los registros previos, no la de toda la familia.
        // Ver la nota de PermisoRegistroVer arriba.
        var veto = await ExigirPermisoAsync(PermisoRegistroVer, ct);
        if (veto is not null) return veto;

        var data = await _service.ConsultarRegistroAsync(request ?? new GarantiaRegistroConsultaRequest(), ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Registros de garantia.", data });
    }

    private static string Mensaje(bool hecho, string siSeHizo)
        => hecho ? siSeHizo : "El procedimiento rechazo la operacion.";
}
