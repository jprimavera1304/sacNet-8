using ISL_Service.Application.DTOs.Catalogos;
using ISL_Service.Application.Interfaces;
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

    public CatalogosController(ICatalogosRepository repository)
    {
        _repository = repository;
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
}
