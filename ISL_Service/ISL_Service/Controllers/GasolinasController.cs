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
*/
[ApiController]
[Route("api/gasolinas")]
[Authorize]
public class GasolinasController : PermisoControllerBase
{
    private readonly IGasolinasService _service;

    private static readonly string[] Ver = { "gasolinas.ver" };
    private static readonly string[] Crear = { "gasolinas.crear" };
    private static readonly string[] Editar = { "gasolinas.editar" };
    private static readonly string[] Cancelar = { "gasolinas.cancelar" };

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
