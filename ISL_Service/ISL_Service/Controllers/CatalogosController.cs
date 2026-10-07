using ISL_Service.Application.DTOs.Catalogos;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Endpoints de catalogos (tipos de casco, etc.) para dropdowns y solo lectura.
/// </summary>
[ApiController]
[Route("api/catalogos")]
[Authorize]
public class CatalogosController : ControllerBase
{
    private readonly ICatalogosRepository _repository;

    // Quien pregunta. NUNCA sale del cuerpo ni del query: si el id de usuario
    // viajara de fuera, el filtro por agente del desplegable de clientes se
    // podria pedir a nombre de otro.
    private readonly ICurrentUserAccessor _currentUser;

    public CatalogosController(ICatalogosRepository repository, ICurrentUserAccessor currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Lista tipos de casco (TiposUsados). Para dropdown en alta/edicion de tarimas.
    /// </summary>
    /// <param name="status">Filtro por IdStatus (ej. 1 = activos). NULL = todos</param>
    /// <returns>Lista con idTipoCasco (IdTipoUsado) y descripcion</returns>
    /// <response code="200">Lista obtenida</response>
    [HttpGet("tipos-casco")]
    [ProducesResponseType(typeof(List<TipoCascoItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTiposCasco([FromQuery] int? status, CancellationToken ct)
    {
        var list = await _repository.ListTiposCascoAsync(status, ct);
        return Ok(list);
    }

    /// <summary>
    /// Lista repartidores (Catalogo Repartidores). Para dropdown en Almacén de Cascos.
    /// </summary>
    /// <param name="status">Filtro por IDStatus (ej. 1 = activos). NULL = todos</param>
    [HttpGet("repartidores")]
    [ProducesResponseType(typeof(List<RepartidorItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRepartidores(
        [FromQuery] int? status, [FromQuery] int? idStatus, [FromQuery] string? repartidor, CancellationToken ct)
    {
        /*
          `status` e `idStatus` son el MISMO filtro con dos nombres, y los dos se
          aceptan a proposito. `status` es el que mandan los veinte consumidores
          del front viejo y no se les puede cambiar; `idStatus` es como se llama
          en el resto de api/catalogos -los diez endpoints de la ola 3 y los siete
          nuevos-, y es el que manda el front nuevo. Obligar a uno de los dos
          lados a usar el nombre del otro seria o romper lo que ya funciona o
          dejar este endpoint como la unica excepcion del modulo.
        */
        var list = await _repository.ListRepartidoresAsync(status ?? idStatus, repartidor, ct);
        return Ok(list);
    }

    /// <summary>
    /// Lista tarimas (WTarima). Para dropdown en Almacén de Cascos.
    /// </summary>
    /// <param name="status">Filtro por IdStatus (ej. 1 = activos). NULL = todos</param>
    [HttpGet("tarimas")]
    [ProducesResponseType(typeof(List<TarimaCatalogItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTarimas([FromQuery] int? status, CancellationToken ct)
    {
        var list = await _repository.ListTarimasAsync(status, ct);
        return Ok(list);
    }

    /// <summary>
    /// Lista clientes (Clientes). Para dropdowns de módulos administrativos.
    /// </summary>
    /// <param name="status">Filtro por IDStatus (1 activo, 2 inactivo). NULL = todos</param>
    /// <response code="409">
    /// No se pudo resolver el filtro por agente del usuario (falta desplegar
    /// sp_w_ConsultaAgenteUsuario). A proposito NO se contesta la lista.
    /// </response>
    /*
      ESTE ENDPOINT PUBLICABA EL PADRON COMPLETO SIN PERMISO. ARREGLADO.
      ===============================================================

      Dos agujeros, los dos en el mismo GET:

      1. NO PEDIA PERMISO. Solo tenia el [Authorize] de la clase, asi que
         cualquier usuario autenticado -- un repartidor, un checador -- podia
         pedir la lista de clientes de la empresa. Ahora pide
         `clientes.ver`, la misma clave que la ficha y que GET api/clientes:
         es el mismo dato, aunque venga recortado a cuatro columnas.

      2. NO FILTRABA POR AGENTE. Un usuario ADMIN amarrado a UN agente veia
         los 1,042 clientes de los 15 agentes de la empresa. El filtro se
         aplica ahora en el repositorio, con FiltroDeAgente y el id de usuario
         del TOKEN.

      LO QUE NO SE CAMBIA, A PROPOSITO: la FORMA de la respuesta. Sigue siendo
      la lista desnuda, sin el sobre { ok, message, data }, porque el front
      viejo ya la consume asi y meterla en un sobre lo rompe. Los otros GET de
      este controlador tampoco lo llevan, por lo mismo (ver el comentario de
      CatalogosGeneralesController).
    */
    [HttpGet("clientes")]
    [Authorize(Policy = "perm:clientes.ver")]
    [ProducesResponseType(typeof(List<ClienteCatalogItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetClientes([FromQuery] int? status, CancellationToken ct)
    {
        var list = await _repository.ListClientesAsync(status, _currentUser.GetLegacyUserId(User), ct);
        return Ok(list);
    }

    /// <summary>
    /// Lista bancos (Catalogo Bancos). Para dropdowns de módulos administrativos.
    /// </summary>
    /// <param name="status">Filtro por IDStatus (1 activo, 2 inactivo). NULL = todos</param>
    [HttpGet("bancos")]
    [ProducesResponseType(typeof(List<BancoCatalogItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBancos(
        [FromQuery] int? status, [FromQuery] int? idStatus, [FromQuery] string? banco, CancellationToken ct)
    {
        /* Ver el comentario de GetRepartidores: `status` es el nombre con el que
           lo llama el front viejo, `idStatus` el del resto de api/catalogos. */
        var list = await _repository.ListBancosAsync(status ?? idStatus, banco, ct);
        return Ok(list);
    }
}
