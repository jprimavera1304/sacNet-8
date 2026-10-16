using ISL_Service.Application.Centros;
using ISL_Service.Application.DTOs.Cobranzas;
using ISL_Service.Application.DTOs.EscriturasLegacy;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/*
  COBRANZAS: EL FAJO DE REMISIONES QUE SE LLEVA UN REPARTIDOR.

  PERMISOS: SON DE FAMILIA PROPIA, NO DE ventas.*
  ----------------------------------------------
  Aqui hay una trampa que vale escribir. `ventas.*` es una de las CUATRO familias
  que gobierna Mac31: al calcular el snapshot, el puente de permisos BORRA del
  snapshot lo que diga el web para esas familias y lo recalcula desde los botones
  de la forma CONSULTA DE VENTAS (PermissionService, seccion 2.8 del resumen de
  arquitectura). Si estas claves se hubieran llamado `ventas.cobranzas.*`, darlas
  desde la pantalla de permisos del web NO habria servido de nada: se guardarian
  en WUsuarioPermiso y desaparecerian en el siguiente calculo, sin error y sin
  rastro.

  Por eso son `cobranzas.*`: familia propia, del web, fuera del puente.

    cobranzas.ver_modulo  la tarjeta del modulo
    cobranzas.ver         consultar cobranzas y su detalle
    cobranzas.crear       armar una cobranza
    cobranzas.editar      cambiarle el contenido
    cobranzas.cancelar    cancelarla
    cobranzas.pendientes.ver  la pantalla de cobranza pendiente y vencida

  QUIEN PREGUNTA SALE DEL TOKEN
  -----------------------------
  sp_n_ConsultarCobranzas recibe @IDUsuarioActual y con eso decide que cobranzas
  se ven. Legacy lo pedia en el cuerpo: mandando el id de otro se veian las
  cobranzas de otro.
*/
[ApiController]
[Route("api/cobranzas")]
[Authorize]
public class CobranzasController : PermisoControllerBase
{
    private readonly ICobranzasService _service;

    /*
      CADA PERMISO ACEPTA LA CLAVE DEL WEB *Y* LA DEL CANAL MOVIL.

      Un endpoint sirve a los dos canales y cada canal tiene su propia clave,
      porque el telefono se presta y se pierde y la computadora de la oficina
      no: poder dar la cobranza en una y no en la otra es justo el caso para el
      que existe el canal. `CanalDeAcceso.Filtrar` tira las `app_movil.*` cuando
      el token es de canal web, asi que esto no abre nada en el navegador, y
      hoy no se las tiene nadie: las claves todavia no estan sembradas (el
      bloque esta propuesto en el movil, en `cobranza.permisos.md`).

      LA LISTA ACEPTA ADEMAS `cobranzas.consultar`, Y ESO ES UN ARREGLO MEDIDO.
      ---------------------------------------------------------------------
      El puente deriva `cobranzas.ver` de `btnVer` de la forma
      CONSULTA DE COBRANZAS, y `btnVer` en Mac31 es EL DETALLE; la LISTA es
      `btnBuscar`, que deriva `cobranzas.consultar` — una clave que este
      controlador no pedia. El comentario de
      `PermissionService.Familias.Cobros.cs` ya dejaba dicho el desajuste y lo
      daba por inofensivo "porque hoy el front no llama a este controlador".
      Ahora si lo llama (el movil), asi que se midio quien quedaba fuera:

          btnBuscar sin btnVer sobre ConsultarCobranzas
            MacZ           -> KARLA   (1 persona)
            Produccion_svr -> ninguna

      O sea: una persona que en Mac31 puede BUSCAR cobranzas recibia un 403 en
      la lista de la pantalla que si tiene. Se acepta tambien esa clave SOLO
      para la lista, no para el detalle —ese si es `btnVer`—, con el mismo
      criterio que ya usa la otra familia
      (`VentasCobranzaOperacionesController.PermisoVerCobranzas`).
    */
    private static readonly string[] Ver =
        { "cobranzas.ver", "cobranzas.consultar", "app_movil.cobranzas.ver" };

    /// <summary>El DETALLE si es `btnVer`: aqui no entra `cobranzas.consultar`.</summary>
    private static readonly string[] VerDetalle =
        { "cobranzas.ver", "app_movil.cobranzas.ver" };

    private static readonly string[] Crear =
        { "cobranzas.crear", "app_movil.cobranzas.crear" };
    private static readonly string[] Editar =
        { "cobranzas.editar", "app_movil.cobranzas.editar" };
    private static readonly string[] Cancelar =
        { "cobranzas.cancelar", "app_movil.cobranzas.cancelar" };
    private static readonly string[] VerPendientes =
        { "cobranzas.pendientes.ver", "app_movil.cobranzas.pendientes.ver" };

    /*
      ESTE MODULO NO SABE DE CENTROS DE SERVICIO, Y HASTA AHORA NO LO DECIA.
      ====================================================================

      `CobranzasRepository.GetConnection()` arma su cadena a mano desde
      `ConnectionString("Main")`: no pasa por `IConexionDelCentro`. Y "cobranzas"
      NO esta declarado en `Application/Centros/ModulosConCentros.cs` (ahi solo
      esta "reportes"), asi que la capa de conexion no tiene nada que decir.

      Con las dos cosas, una peticion con `X-Centro = 2` se atendia contra la
      MATRIZ y contestaba **200**. Una cobranza "armada en EJE 6" se habria
      guardado en Zaragoza sin que nada avisara, y cobrar en la base equivocada
      no se deshace con un boton. El punto 5 del contrato de centros dice
      exactamente esto: un modulo no declarado, con `X-Centro != 0`, devuelve
      409 con motivo y NUNCA datos de la matriz.

      Asi que se le pone el mismo guardia que ya usa
      `VentasReasignacionController`. Es el candado del SERVIDOR, para que no
      dependa de que cada cliente se acuerde: el movil ya no pide nada fuera de
      la matriz, pero un APK viejo —o cualquier modulo futuro que comparta el
      `ApiClient`, que pone `X-Centro` en TODAS las peticiones— si lo haria.
    */
    private const string MotivoSinCentros =
        "La cobranza todavía solo funciona en la matriz. El procedimiento que " +
        "la guarda arma su conexión por su cuenta y no sabe de centros de " +
        "servicio: una cobranza capturada \"en el centro\" se guardaría en la " +
        "matriz sin que nada avisara. Vuelve a la matriz para armar o cancelar " +
        "cobranzas.";

    public CobranzasController(
        ICobranzasService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    private IActionResult Body() => BadRequest(new { ok = false, message = "Body requerido." });

    /// <summary>
    /// 409 con motivo si la peticion pide un centro. Nunca datos de la matriz.
    /// </summary>
    private IActionResult? RechazarSiEsOtroCentro()
    {
        var cabecera = Request.Headers[CentroPedido.Cabecera].ToString();

        if (!CentroPedido.Interpretar(cabecera, out var idCentro))
        {
            return Conflict(new
            {
                ok = false,
                code = "CENTRO_INVALIDO",
                message = "El centro pedido no es válido.",
                detail = $"La cabecera {CentroPedido.Cabecera} tiene que ser un número entero. " +
                         "Vuelve a elegir el centro.",
            });
        }

        if (idCentro == CentroPedido.Matriz) return null;

        return Conflict(new
        {
            ok = false,
            code = "MODULO_SIN_CENTROS",
            message = "Este módulo todavía no sabe de centros.",
            // EL `detail` ES DONDE VIAJA LA FRASE QUE DICE QUE HACER. El sobre
            // de error de esta casa es {code, message, detail} y los clientes
            // pintan el detail: dejarlo vacio es dejar a la persona con un
            // "no se pudo" y nada mas.
            detail = MotivoSinCentros,
            idCentro,
        });
    }

    private IActionResult Lista(string mensaje, List<Dictionary<string, object?>> data)
    {
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = mensaje, data, total = data.Count });
    }

    /// <summary>
    /// Las cobranzas. Todos los filtros se combinan, y quien pregunta sale del
    /// token.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarAsync([FromQuery] CobranzasFiltro filtro, CancellationToken ct)
    {
        var centro = RechazarSiEsOtroCentro();
        if (centro is not null) return centro;

        var no = await ExigirPermisoAsync(Ver, ct);
        if (no is not null) return no;
        return Lista("Cobranzas consultadas.", await _service.ConsultarAsync(filtro, IdUsuarioLegacy(), ct));
    }

    /// <summary>El detalle de una cobranza.</summary>
    [HttpGet("{id:int}/detalle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarDetalleAsync(int id, [FromQuery] int presentacion, CancellationToken ct)
    {
        var centro = RechazarSiEsOtroCentro();
        if (centro is not null) return centro;

        var no = await ExigirPermisoAsync(VerDetalle, ct);
        if (no is not null) return no;

        var filtro = new CobranzaDetalleFiltro { IdCobranza = id, Presentacion = presentacion };
        return Lista("Detalle de la cobranza.", await _service.ConsultarDetalleAsync(filtro, ct));
    }

    /// <summary>
    /// Dice si una venta se puede meter en una cobranza (si no la trae ya otra,
    /// si no esta pagada, si no esta cancelada).
    /// </summary>
    [HttpGet("validar-folio")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ValidarFolioAsync([FromQuery] CobranzaValidarFolioFiltro filtro, CancellationToken ct)
    {
        var centro = RechazarSiEsOtroCentro();
        if (centro is not null) return centro;

        var no = await ExigirPermisoAsync(Crear, ct);
        if (no is not null) return no;
        return Lista("Folio validado.", await _service.ValidarFolioAsync(filtro, IdUsuarioLegacy(), ct));
    }

    /// <summary>
    /// La cobranza pendiente y vencida.
    ///
    /// La fecha de operacion contra la que se mide el vencimiento la pone la base,
    /// no el cliente: en Legacy viajaba en el cuerpo y con eso se podia hacer que
    /// una venta vencida saliera como pendiente.
    /// </summary>
    [HttpGet("pendientes-vencidos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConsultarPendientesVencidosAsync([FromQuery] CobranzaPendientesVencidosFiltro filtro, CancellationToken ct)
    {
        var centro = RechazarSiEsOtroCentro();
        if (centro is not null) return centro;

        var no = await ExigirPermisoAsync(VerPendientes, ct);
        if (no is not null) return no;
        return Lista("Cobranza pendiente y vencida.", await _service.ConsultarPendientesVencidosAsync(filtro, ct));
    }

    /// <summary>Arma una cobranza con un repartidor y una lista de ventas.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearAsync([FromBody] CrearCobranzaRequest? request, CancellationToken ct)
    {
        var centro = RechazarSiEsOtroCentro();
        if (centro is not null) return centro;

        var no = await ExigirPermisoAsync(Crear, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.CrearAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Cobranza armada.", data });
    }

    /// <summary>
    /// Cambia el contenido de una cobranza: quita renglones, los devuelve y/o
    /// agrega ventas. Hay que pedir al menos una de las tres cosas.
    /// </summary>
    [HttpPut("{id:int}/detalle")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarDetalleAsync(int id, [FromBody] ActualizarCobranzaDetalleRequest? request, CancellationToken ct)
    {
        var centro = RechazarSiEsOtroCentro();
        if (centro is not null) return centro;

        var no = await ExigirPermisoAsync(Editar, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.ActualizarDetalleAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Cobranza actualizada.", data });
    }

    /// <summary>Cancela una o varias cobranzas.</summary>
    [HttpPut("cancelar")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelarAsync([FromBody] CancelarCobranzasRequest? request, CancellationToken ct)
    {
        var centro = RechazarSiEsOtroCentro();
        if (centro is not null) return centro;

        var no = await ExigirPermisoAsync(Cancelar, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.CancelarAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Cobranzas canceladas.", data });
    }
}
