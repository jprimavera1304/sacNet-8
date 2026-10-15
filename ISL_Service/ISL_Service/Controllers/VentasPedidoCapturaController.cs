using ISL_Service.Application.DTOs.VentasPedidoCaptura;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

// Captura de pedidos. La usan DOS superficies con catalogos de permiso
// distintos, y por eso las policies son compuestas ("a|b" = basta con una):
//
//   - Oficina, desde el web  -> ventas.pedidos.crear   (Web\QR_Web ... pedido.api.js
//                                                       pega a estos mismos endpoints)
//   - Repartidor, desde el movil -> app_movil.pedidos
//
// Exigir solo uno deja fuera al otro: con solo ventas.pedidos.crear el
// repartidor recibia 403 en todo y la app se quedaba en blanco; con solo
// app_movil.pedidos se rompia la captura del web.
//
// NO se resolvio dandole ventas.pedidos.crear al repartidor porque ese permiso,
// por si solo, tambien le abre la pantalla de captura del web (pedido.main.js
// entra con un OR) y ademas lo lee el Mac31 de produccion (btnNuevoPedido).
[ApiController]
[Route("api/ventas/pedidos/captura")]
[Authorize]
public class VentasPedidoCapturaController : ControllerBase
{
    private readonly IVentasPedidoCapturaService _service;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IPermissionService _permissionService;

    public VentasPedidoCapturaController(IVentasPedidoCapturaService service, ICurrentUserAccessor currentUserAccessor, IPermissionService permissionService)
    {
        _service = service;
        _currentUserAccessor = currentUserAccessor;
        _permissionService = permissionService;
    }

    /*
      LAS DOS POLITICAS DE ESTE CONTROLADOR, Y POR QUE SON DOS.

      EL AGUJERO QUE CIERRAN
      ----------------------
      La app gatea su boton "Modificar" de CONSULTA DE VENTAS con
      `app_movil.ventas.pedidos.modificar` (ventas_consulta.dart), y esa clave
      no gobernaba NADA en el servidor: el unico sitio que la miraba era
      `POST api/ventas/pedidos/usados-credito`
      (PedidosOperacionesController.PermisoModificar), que es ajustar cascos a
      credito, no modificar el pedido. Las rutas que de verdad modifican un
      pedido guardado son las de AQUI. El efecto era de los peores posibles,
      porque fallaba en las dos direcciones: quitarle la clave a alguien le
      escondia el boton y dejaba el PUT abierto, y darsela sola le dibujaba el
      boton para que el PUT le contestara 403.

      LA CORRECCION: que estas rutas ACEPTEN la clave que la pantalla de
      permisos promete. Va en los dos sitios que deciden —esta politica y
      HayQueCobrarLaClaveDeEditarAsync—, porque con uno solo la clave seguiria
      sin alcanzar.

      POR QUE EN ESTA DIRECCION Y NO CAMBIANDO LA APP. La alternativa era que
      la app gateara Modificar con `app_movil.pedidos.editar`, y eso AMPLIA
      alcance sin que nadie lo conceda: esa clave ya esta repartida en
      produccion y los repartidores la usan hoy para editar sus pedidos
      pendientes desde "Mis pedidos", asi que todos ellos verian aparecer
      Modificar en Consulta de ventas al dia siguiente. Por este lado el efecto
      HOY ES CERO —nadie tiene la clave nueva, son 0 filas en WPermiso en las
      dos bases— y pasa a ser correcto en cuanto alguien la reparta a mano.

      NO SE QUITA NADA A QUIEN HOY EDITA: `app_movil.pedidos.editar` y
      `ventas.pedidos.crear` siguen valiendo exactamente igual. La clave nueva
      se SUMA con un OR; un OR no puede cerrar nada que estuviera abierto.

      POR QUE SON DOS POLITICAS Y NO UNA
      ----------------------------------
      `app_movil.ventas.pedidos.modificar` es la clave de MODIFICAR un pedido
      que ya existe. No es la de CREAR uno. Si se pusiera en todas las rutas,
      quien tuviera solo esa clave podria estrenar pedidos por `bootstrap` y
      tirar borradores, que es alcance que nadie le concedio. Asi que:

        * PoliticaCaptura    — crear: bootstrap, borrador, mis-pedidos.
        * PoliticaModificar  — abrir y modificar uno que ya existe: el GET, el
          historial, los buscadores que hacen falta para cambiar un renglon, el
          alta y baja de detalle y el PUT.

      Las dos siguen aceptando `ventas.pedidos.crear` (oficina/Mac31) y
      `app_movil.pedidos` (la app), que es lo que funciona hoy.
    */
    private const string PoliticaCaptura =
        "perm:ventas.pedidos.crear|app_movil.pedidos";

    private const string PoliticaModificar =
        "perm:ventas.pedidos.crear|app_movil.pedidos|app_movil.ventas.pedidos.modificar";

    // Blindaje de propiedad para usuarios "solo movil" (repartidores).
    //
    // Los sp_n_ de captura NO validan que el pedido sea del usuario del token:
    // cualquiera con token puede leer, editar y hasta apropiarse del pedido de
    // otro pasando un idPedido ajeno (el PUT reescribe IDUsuario). Como esos SP
    // son legacy e intocables, la validacion se hace aqui.
    //
    // Solo se restringe a repartidores. Oficina (ventas.pedidos.crear) y
    // SuperAdmin conservan su comportamiento actual, para no destabilizar el web.
    // Un repartidor solo puede tocar pedidos SUYOS, y solo en borrador (0) o
    // pendiente (1): no un pedido ya procesado.
    //
    // Devuelve un IActionResult de error si hay que rechazar; null si esta bien.
    // Canal/equipo que queda registrado en el pedido. Si el cliente lo declara
    // (MOVIL / WEB) se usa eso; si no, se conserva el comportamiento anterior:
    // el nombre de usuario/maquina. Se recorta a 100 (tamaño de la columna).
    private string ResolveEquipo(string? requestEquipo)
    {
        var equipo = string.IsNullOrWhiteSpace(requestEquipo)
            ? _currentUserAccessor.GetUsername(User, Environment.MachineName)
            : requestEquipo.Trim();
        return equipo.Length > 100 ? equipo[..100] : equipo;
    }

    /// <param name="esModificacion">
    /// True solo en las rutas que ESCRIBEN sobre el pedido (guardar, agregar o
    /// quitar un renglon). Es lo que decide si se cobra
    /// `app_movil.pedidos.editar`.
    ///
    /// Las LECTURAS -abrir el pedido, ver su historial- no se cobran, y es
    /// deliberado: `app_movil.pedidos.editar` gobierna el boton "Editar
    /// pedido", no la tarjeta de Mis pedidos. Si se cobrara tambien al leer, a
    /// quien le quitaras la clave dejaria de poder ABRIR sus propios pedidos
    /// —la pantalla entera se le llenaria de 403— y eso no se lo quito nadie.
    /// </param>
    private async Task<IActionResult?> ValidarPropiedadPedidoAsync(int idPedido, CancellationToken ct, bool esModificacion = false)
    {
        if (idPedido <= 0) return null; // pedido nuevo, aun no tiene dueño

        var esSoloMovil = await EsSoloMovilAsync(ct);
        var hayQueCobrarEdicion = esModificacion && await HayQueCobrarLaClaveDeEditarAsync(ct);

        // Oficina / super / legacy en canal web: igual que hoy, ni una consulta
        // de mas.
        if (!esSoloMovil && !hayQueCobrarEdicion) return null;

        var propiedad = await _service.ObtenerPropiedadPedidoAsync(idPedido, ct);
        if (propiedad == null)
        {
            // Se conserva TAL CUAL el comportamiento de hoy: el 404 solo lo
            // devolvia el blindaje de propiedad. Para los demas, un idPedido que
            // no existe lo sigue contestando legacy como siempre.
            return esSoloMovil
                ? NotFound(new { ok = false, message = "Pedido no encontrado." })
                : null;
        }

        if (esSoloMovil)
        {
            var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
            // Es mio si legacy lo tiene a mi nombre, O si yo lo cree segun la bitacora
            // (aunque legacy haya cambiado el usuario al modificarlo). Sin esto, abrir
            // mi propio pedido reasignado daria 403.
            if (propiedad.IdUsuario != idUsuario
                && !await _service.EsCreadorPorBitacoraAsync(idPedido, idUsuario, ct))
                return StatusCode(403, new { ok = false, message = "Este pedido no es tuyo." });

            if (propiedad.IdStatus != 0 && propiedad.IdStatus != 1)
                return StatusCode(403, new { ok = false, message = "Este pedido ya no se puede modificar." });
        }

        /*
          `app_movil.pedidos.editar`, EXIGIDO DE VERDAD.

          QUE PASABA
          ----------
          La app esconde el boton "Editar pedido" de Mis pedidos a quien no
          tiene esta clave (mis_pedidos.dart, `_puedeEditar`), y eso era TODO el
          control. El endpoint de guardar pedia
          `perm:ventas.pedidos.crear|app_movil.pedidos`, asi que cualquier
          repartidor que pudiera capturar podia tambien MODIFICAR un pedido ya
          guardado llamando al PUT a mano, aunque le hubieran quitado la clave
          de editar. Esconder el boton no es quitar el permiso.

          COMO SE DISTINGUE "CREAR" DE "MODIFICAR" SIN ROMPER LA CAPTURA
          --------------------------------------------------------------
          Por `Pedidos.IDStatusPedido`, que es un dato del SERVIDOR (lectura
          directa de la tabla, el mismo renglon que ya lee el blindaje de
          propiedad), no algo que el cliente declare:

            0 = borrador  -> el pedido se esta capturando AHORA. El renglon lo
                             creo `sp_*_InsertarPedidoDetalle` al agregar el
                             primer producto, y el PUT que viene despues es la
                             PRIMERA grabada: eso es CREAR y no se cobra nada.
            1 = pendiente -> el pedido ya esta guardado. Volver a tocarlo es
                             MODIFICAR, y es exactamente el caso del boton
                             Editar de Mis pedidos (la app solo lo ofrece
                             cuando el estatus es "Pendiente").
            otros         -> autorizado/procesado/cancelado: ya los rechaza el
                             blindaje de propiedad mas arriba.

          No se puede usar `idPedido > 0` como senal de "ya existe": el PUT
          SIEMPRE llega con un idPedido mayor que cero -lo rechaza si no
          (`"Pedido y domicilio son requeridos"`)- porque el renglon nace al
          agregar el primer renglon de detalle. Cobrar por `idPedido > 0`
          habria roto la captura de un pedido NUEVO, que es justo lo que no
          puede pasar.

          Cubre las tres rutas que modifican: el PUT de guardar y el alta/baja
          de detalle sobre un pedido ya guardado (las tres pasan por aqui).

          A QUIEN DEJA FUERA: A NADIE, HOY
          --------------------------------
          Medido el 2026-10-15 en las dos bases locales, usuario por usuario:
          TODOS los que tienen `app_movil.pedidos` tienen tambien
          `app_movil.pedidos.editar`. Zaragoza (MacZ): 19 usuarios con clave
          movil, los 19 con las dos. Tauro (Produccion_svr): DEMO, con las dos.
          O sea que esto no le quita hoy la edicion a nadie; lo que cierra es
          que quitarle la clave a alguien pase a SERVIR de algo.

          Y SE ACEPTA LA CLAVE DE OFICINA. `ventas.pedidos.crear` es la del web
          y la que lee Mac31 (btnNuevoPedido): quien la tiene ya puede modificar
          pedidos desde el web por estas mismas rutas, asi que negarselo por
          venir del telefono seria un candado nuevo, no el cierre de este
          agujero. Es el mismo OR web|movil que ya tiene la policy del endpoint,
          con la clave de editar en la casilla del movil.

          SOLO EN CANAL MOVIL. El web y Mac31 no cambian en nada: ver
          HayQueCobrarLaClaveDeEditarAsync.
        */
        if (hayQueCobrarEdicion && propiedad.IdStatus == 1)
        {
            return StatusCode(403, new
            {
                ok = false,
                message = "No tienes permiso para editar pedidos "
                          + "(app_movil.pedidos.editar o app_movil.ventas.pedidos.modificar)."
            });
        }

        return null;
    }

    /// <summary>
    /// True si a esta peticion hay que exigirle `app_movil.pedidos.editar`
    /// para tocar un pedido YA GUARDADO.
    /// </summary>
    /// <remarks>
    /// Solo al CANAL MOVIL, y el canal sale del claim firmado del token, no de
    /// una cabecera que el cliente pueda inventar (ver CanalDeAcceso).
    ///
    /// Queda fuera, y cada exclusion esta medida:
    ///   * Canal web -> el web usa ESTAS MISMAS rutas para modificar
    ///     (`pedido.api.js` pega aqui). Gatearlo con una clave `app_movil.*`
    ///     dejaria fuera a toda la oficina de golpe, porque esa clave no se le
    ///     da a nadie del web: medido, 0 usuarios de canal web tienen
    ///     `app_movil.pedidos.editar` sin tener tambien `ventas.pedidos.crear`.
    ///   * Token sin canal (los emitidos antes de este cambio, 4 h de vida):
    ///     se comporta como ayer, para no echar fuera a quien tenga la app
    ///     abierta durante el despliegue.
    ///   * SuperAdmin: pasa, como en toda la API.
    ///   * Instalacion sin modelo de permisos: pasa; no hay claves contra las
    ///     que comparar y negar dejaria muerta una base que hoy funciona.
    /// </remarks>
    private async Task<bool> HayQueCobrarLaClaveDeEditarAsync(CancellationToken ct)
    {
        if (CanalDeAcceso.De(User) != Canal.Movil) return false;
        if (CurrentUser.IsSuperAdmin(User)) return false;

        var snapshot = await _permissionService.GetPermissionsAsync(
            CurrentUser.GetUserId(User),
            CurrentUser.GetEmpresaId(User),
            User.FindFirst("rolLegacy")?.Value ?? CurrentUser.GetRol(User),
            ct);

        if (!snapshot.PermissionsEnabled) return false;

        /*
          LAS TRES CLAVES QUE DEJAN MODIFICAR UN PEDIDO YA GUARDADO.

          `app_movil.pedidos.editar`  el boton Editar de "Mis pedidos". Es la
                                      que ya esta repartida y la que usan hoy
                                      los repartidores: no se toca.
          `ventas.pedidos.crear`      la de oficina y la que lee Mac31. Quien
                                      la tiene ya modifica por estas rutas
                                      desde el web.
          `app_movil.ventas.pedidos.modificar`  la del boton Modificar de
                                      Consulta de ventas en la app. SE SUMA
                                      AQUI porque es la que la pantalla de
                                      permisos promete, y hasta ahora no
                                      gobernaba ninguna de estas rutas (ver el
                                      bloque de PoliticaModificar, arriba). Es
                                      un OR mas: no le quita la edicion a
                                      nadie, y hoy no se la da a nadie tampoco
                                      —son 0 filas en las dos bases—.

          Y UN PEDIDO EN BORRADOR (IDStatusPedido = 0) NO COBRA NINGUNA DE LAS
          TRES: es la primera grabada de un pedido nuevo, que es CREAR. Eso lo
          decide quien llama (el `propiedad.IdStatus == 1` de arriba), no esta
          funcion.
        */
        var puedeEditar = snapshot.Permissions.Any(p =>
            string.Equals(p, "app_movil.pedidos.editar", StringComparison.OrdinalIgnoreCase)
            || string.Equals(p, "ventas.pedidos.crear", StringComparison.OrdinalIgnoreCase)
            || string.Equals(p, "app_movil.ventas.pedidos.modificar", StringComparison.OrdinalIgnoreCase));

        return !puedeEditar;
    }

    // True si quien llama es un usuario "solo movil": no SuperAdmin y sin el
    // permiso de oficina ventas.pedidos.crear. En modo legacy (capacidades
    // apagadas) no se restringe, para no cambiar el comportamiento actual.
    private async Task<bool> EsSoloMovilAsync(CancellationToken ct)
    {
        if (CurrentUser.IsSuperAdmin(User)) return false;

        var userId = CurrentUser.GetUserId(User);
        var empresaId = CurrentUser.GetEmpresaId(User);
        var rolLegacy = User.FindFirst("rolLegacy")?.Value ?? CurrentUser.GetRol(User);

        var snapshot = await _permissionService.GetPermissionsAsync(userId, empresaId, rolLegacy, ct);
        if (!snapshot.PermissionsEnabled) return false;

        var esOficina = snapshot.Permissions.Any(p =>
            string.Equals(p, "ventas.pedidos.crear", StringComparison.OrdinalIgnoreCase));
        return !esOficina;
    }

    [HttpGet("bootstrap")]
    [Authorize(Policy = PoliticaCaptura)]
    public async Task<IActionResult> Bootstrap(CancellationToken ct)
    {
        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var data = await _service.BootstrapAsync(idUsuario, ct);
        return Ok(new { ok = true, message = "Nuevo pedido inicializado.", data });
    }

    [HttpGet("{idPedido:int}")]
    [Authorize(Policy = PoliticaModificar)]
    public async Task<IActionResult> Obtener([FromRoute] int idPedido, CancellationToken ct)
    {
        var rechazo = await ValidarPropiedadPedidoAsync(idPedido, ct);
        if (rechazo != null) return rechazo;

        var data = await _service.ConsultarPedidoAsync(idPedido, ct);
        return Ok(new { ok = true, message = "Pedido consultado.", data });
    }

    [HttpPost("clientes/buscar")]
    [Authorize(Policy = PoliticaModificar)]
    public async Task<IActionResult> BuscarCliente([FromBody] PedidoClienteBuscarRequest? request, CancellationToken ct)
    {
        // El usuario sale del token, NUNCA del body: si viniera del cliente,
        // cualquiera podria pedir los clientes de otro agente mandando otro id.
        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var data = await _service.BuscarClienteAsync(request ?? new PedidoClienteBuscarRequest(), idUsuario, ct);
        return Ok(new { ok = true, message = "Cliente consultado.", data });
    }

    [HttpPost("productos/buscar")]
    [Authorize(Policy = PoliticaModificar)]
    public async Task<IActionResult> BuscarProducto([FromBody] PedidoProductoBuscarRequest? request, CancellationToken ct)
    {
        var safe = request ?? new PedidoProductoBuscarRequest();
        if (safe.IDAlmacen <= 0)
            return BadRequest(new { ok = false, message = "Seleccione almacen." });

        var data = await _service.BuscarProductoAsync(safe, ct);
        return Ok(new { ok = true, message = "Producto consultado.", data });
    }

    // Version paginada de productos/buscar: devuelve solo productos con existencia
    // y solo las columnas que usa la app. productos/buscar sigue vivo para la app
    // publicada.
    [HttpPost("productos/pagina")]
    [Authorize(Policy = PoliticaModificar)]
    public async Task<IActionResult> BuscarProductoPagina([FromBody] PedidoProductoPaginaRequest? request, CancellationToken ct)
    {
        var safe = request ?? new PedidoProductoPaginaRequest();
        if (safe.IDAlmacen <= 0)
            return BadRequest(new { ok = false, message = "Seleccione almacen." });

        var modo = PedidoModo.Normalizar(safe.Modo);
        if (modo == null)
            return BadRequest(new { ok = false, message = "Modo invalido. Use 'normal' o 'aceites'." });

        // No hace falta un candado extra: el filtrado de aceites solo aplica a
        // empresas ZARA (lo resuelve el repositorio por la funcionalidad de la
        // empresa del usuario). Para las demas, pedir 'aceites' simplemente
        // devuelve el catalogo normal, sin efecto.
        safe.Modo = modo;
        var data = await _service.BuscarProductoPaginaAsync(safe, ct);
        return Ok(new { ok = true, message = "Productos consultados.", data });
    }

    [HttpPost("detalle")]
    [Authorize(Policy = PoliticaModificar)]
    public async Task<IActionResult> AgregarDetalle([FromBody] PedidoAgregarDetalleRequest? request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { ok = false, message = "Body requerido." });
        if (request.IDCliente <= 0 || request.IDDomicilio <= 0 || request.IDProducto <= 0 || request.IDAlmacen <= 0)
            return BadRequest(new { ok = false, message = "Cliente, domicilio, almacen y producto son requeridos." });
        if (request.Cantidad <= 0)
            return BadRequest(new { ok = false, message = "Cantidad invalida." });

        var rechazo = await ValidarPropiedadPedidoAsync(request.IDPedido, ct, esModificacion: true);
        if (rechazo != null) return rechazo;

        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var equipo = ResolveEquipo(request.Equipo);
        var data = await _service.AgregarDetalleAsync(request, idUsuario, equipo, ct);
        return Ok(new { ok = true, message = "Producto agregado.", data });
    }

    [HttpDelete("detalle")]
    [Authorize(Policy = PoliticaModificar)]
    public async Task<IActionResult> EliminarDetalle([FromBody] PedidoEliminarDetalleRequest? request, CancellationToken ct)
    {
        if (request == null || request.IDPedidoDetalle <= 0)
            return BadRequest(new { ok = false, message = "IDPedidoDetalle requerido." });

        var rechazo = await ValidarPropiedadPedidoAsync(request.IDPedido, ct, esModificacion: true);
        if (rechazo != null) return rechazo;

        var data = await _service.EliminarDetalleAsync(request, ct);
        return Ok(new { ok = true, message = "Producto eliminado.", data });
    }

    [HttpPut("{idPedido:int}")]
    [Authorize(Policy = PoliticaModificar)]
    public async Task<IActionResult> Guardar([FromRoute] int idPedido, [FromBody] PedidoGuardarRequest? request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { ok = false, message = "Body requerido." });
        request.IDPedido = idPedido;
        if (request.IDPedido <= 0 || request.IDDomicilio <= 0)
            return BadRequest(new { ok = false, message = "Pedido y domicilio son requeridos." });

        var rechazo = await ValidarPropiedadPedidoAsync(request.IDPedido, ct, esModificacion: true);
        if (rechazo != null) return rechazo;

        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var equipo = ResolveEquipo(request.Equipo);
        var data = await _service.GuardarAsync(request, idUsuario, equipo, ct);

        // Bitacora propia: registra al creador/modificador para que "Mis pedidos"
        // reconozca al dueño aunque legacy sobrescriba el usuario, y para el
        // historial del detalle. Best-effort: si falla, no rompe el guardado.
        try
        {
            var usuario = _currentUserAccessor.GetUsername(User, string.Empty);
            await _service.RegistrarEventoAsync(request.IDPedido, idUsuario, usuario, equipo, request.Productos, request.TotalPagar, ct);
        }
        catch { /* la bitacora es informativa; nunca debe tumbar el guardado */ }

        return Ok(new { ok = true, message = "Pedido guardado.", data });
    }

    // "Mis pedidos" del movil: los que son mios por la bitacora (aunque legacy
    // haya cambiado el usuario) o por el usuario legacy. El idUsuario sale del
    // token, no del body.
    [HttpPost("mis-pedidos")]
    [Authorize(Policy = PoliticaCaptura)]
    public async Task<IActionResult> MisPedidos([FromBody] MisPedidosRequest? request, CancellationToken ct)
    {
        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var data = await _service.ConsultarMisPedidosAsync(
            idUsuario, request?.FechaInicial ?? string.Empty, request?.FechaFinal ?? string.Empty, ct);
        return Ok(new { ok = true, message = "Mis pedidos consultados.", data });
    }

    // Historial de cambios de un pedido (para el detalle): creado/modificado
    // (bitacora) + autorizado/cancelado (legacy), con quien y cuando.
    [HttpGet("{idPedido:int}/historial")]
    [Authorize(Policy = PoliticaModificar)]
    public async Task<IActionResult> Historial([FromRoute] int idPedido, CancellationToken ct)
    {
        var rechazo = await ValidarPropiedadPedidoAsync(idPedido, ct);
        if (rechazo != null) return rechazo;

        var data = await _service.ConsultarHistorialPedidoAsync(idPedido, ct);
        return Ok(new { ok = true, message = "Historial consultado.", data });
    }

    [HttpDelete("borrador")]
    [Authorize(Policy = PoliticaCaptura)]
    public async Task<IActionResult> EliminarBorrador(CancellationToken ct)
    {
        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var data = await _service.EliminarBorradorAsync(idUsuario, ct);
        return Ok(new { ok = true, message = "Borrador eliminado.", data });
    }
}
