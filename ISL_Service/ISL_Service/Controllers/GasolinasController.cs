using ISL_Service.Application.DTOs.EscriturasLegacy;
using ISL_Service.Application.DTOs.Gasolinas;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/*
  RECIBOS DE GASOLINA DE LOS REPARTIDORES.

  Modulo nuevo del web. Los cuatro sp_n_ estan en Produccion_svr con la firma que
  espera Legacy, asi que este es el grupo comparable del lote junto con cobranzas
  y los catalogos de obra.

  PERMISOS
  --------
    gasolinas.ver_modulo  la tarjeta del modulo en el inicio
    gasolinas.ver         consultar los recibos
    gasolinas.crear       capturar un recibo
    gasolinas.editar      corregirlo
    gasolinas.cancelar    cancelarlo

  Cancelar va aparte de editar porque es lo que borra un gasto ya capturado del
  control del repartidor.

  LA CONSULTA ES UN GET Y YA NO TOCA Constantes: ver GasolinasRepository.

  CENTROS DE SERVICIO: ESTE MODULO *NO* LOS SOPORTA. `soportaCentros = false`.
  -------------------------------------------------------------------------
  Queda declarado aqui para que la capa de conexion conteste **409 con motivo**
  si llega `X-Centro != 0`, y NUNCA datos de la matriz.

  No es un pendiente, es una decision:
    1. `GasolinasRepository.GetConnection()` arma la cadena desde
       `ConnectionString("Main")` a mano; no pasa por la capa unica.
    2. Y aunque pasara, los dos desplegables de la pantalla salen de
       `/api/catalogos/autos` y `/api/catalogos/repartidores`, que tampoco
       pasan. Un alta "en el centro" tomaria un `idAuto` del catalogo de la
       MATRIZ y lo insertaria en la base del centro, donde ese id es otro coche
       o no existe; y como `sp_n_ConsultarGasolinas` hace INNER JOIN con
       `[Catalogo Autos]`, ese recibo no volveria a salir en ninguna consulta.

  Un gasto cargado al coche equivocado —o directamente invisible— es peor que
  no tener la funcion en los centros.
*/
[ApiController]
[Route("api/gasolinas")]
[Authorize]
public class GasolinasController : PermisoControllerBase
{
    private readonly IGasolinasService _service;

    /*
      LAS CLAVES VAN EN PAREJA: LA DEL WEB Y LA DEL MOVIL.

      `ExigirPermisoAsync` cobra el array como un O (basta UNA), asi que agregar
      la clave de `app_movil.*` no le quita el acceso a nadie: quien hoy entra
      con `gasolinas.ver` sigue entrando igual. Lo que habilita es lo contrario:
      poder dar el modulo en el TELEFONO sin darlo en la computadora, que es lo
      que ya hacen ventas y reportes.

      Las cuatro claves `app_movil.gasolinas.*` TODAVIA NO ESTAN en el catalogo
      de `PermissionService`: las deja sembradas el agente de permisos (el
      bloque esta en
      Movil/mac-mac-mobile/lib/modules/verticals/gasolina.permisos.md). Hasta
      entonces este array se comporta exactamente como antes.

      La pantalla del telefono cobra las mismas dos claves por permiso, para que
      no haya un sitio donde el candado sea mas flojo que en el otro.
    */
    private static readonly string[] Ver = { "gasolinas.ver", "app_movil.gasolinas.ver" };
    private static readonly string[] Crear = { "gasolinas.crear", "app_movil.gasolinas.crear" };
    private static readonly string[] Editar = { "gasolinas.editar", "app_movil.gasolinas.editar" };
    private static readonly string[] Cancelar = { "gasolinas.cancelar", "app_movil.gasolinas.cancelar" };

    public GasolinasController(
        IGasolinasService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    private IActionResult Body() => BadRequest(new { ok = false, message = "Body requerido." });

    /// <summary>
    /// Los recibos de gasolina. Todos los filtros se combinan: en Legacy, mandar
    /// el numero de recibo tiraba el auto, el repartidor y las cuatro fechas.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarAsync([FromQuery] GasolinasFiltro filtro, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(Ver, ct);
        if (no is not null) return no;

        var data = await _service.ConsultarAsync(filtro, ct);
        return Ok(new { ok = true, message = "Recibos de gasolina consultados.", data, total = data.Count });
    }

    /// <summary>Captura un recibo de gasolina.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearAsync([FromBody] CrearGasolinaRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(Crear, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.CrearAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Recibo de gasolina registrado.", data });
    }

    /// <summary>Corrige un recibo de gasolina.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarAsync(int id, [FromBody] ActualizarGasolinaRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(Editar, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.ActualizarAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Recibo de gasolina actualizado.", data });
    }

    /// <summary>
    /// Cancela uno o varios recibos.
    ///
    /// Las dos listas (ids y numeros de recibo) tienen que medir lo mismo: el SP
    /// las recorre a la vez para la bitacora de cancelacion. Y los numeros solo
    /// admiten digitos, porque el procedimiento los convierte a entero.
    ///
    /// La respuesta trae UN RENGLON POR RECIBO con su resultado
    /// (`data.recibos[].resultado`), porque el procedimiento contesta asi: en una
    /// cancelacion de varios, unos pueden quedar cancelados y otros no. Un 200 no
    /// significa que se cancelaron todos.
    /// </summary>
    [HttpPut("cancelar")]
    [ProducesResponseType(typeof(ResultadoCancelacionGasolinasDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelarAsync([FromBody] CancelarGasolinasRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(Cancelar, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.CancelarAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Cancelacion procesada: revisa el resultado de cada recibo.", data });
    }
}
