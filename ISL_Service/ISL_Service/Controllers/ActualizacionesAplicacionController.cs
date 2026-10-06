using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// El manifiesto de actualizacion de las aplicaciones de escritorio: que archivo
/// se copia, de donde a donde, si se pisa y si se ejecuta.
///
/// NO SE JUNTA CON api/app/version. Ese endpoint es ANONIMO a proposito (el
/// telefono pregunta antes del login) y solo dice dos numeros de version y la liga
/// a la tienda. Esto, en cambio, publica RUTAS DEL SERVIDOR de archivos y el
/// orden en que se reemplazan los ejecutables: es un mapa de donde vive el
/// programa. Mezclarlos en un controlador compartido habria puesto esas rutas a
/// un `[AllowAnonymous]` de distancia.
///
/// SOLO LECTURA: `sp_n_ActualizarAplicacionActualizacion` existe y no se expone.
/// </summary>
[ApiController]
[Route("api/actualizaciones-aplicacion")]
[Authorize]
public class ActualizacionesAplicacionController : ControllerBase
{
    private readonly IActualizacionesAplicacionService _service;

    public ActualizacionesAplicacionController(IActualizacionesAplicacionService service)
    {
        _service = service;
    }

    /// <summary>
    /// Archivos del manifiesto de actualizacion.
    /// </summary>
    /// <param name="idStatus">
    /// 0 = todos (default, igual que legacy), 1 = activos, 2 = inactivos. OJO: el
    /// default del SP es 1, pero legacy mandaba 0 explicito, asi que "sin filtro"
    /// siempre quiso decir "todos" y aqui se conserva.
    /// </param>
    /// <param name="archivoOrigen">Busca por contenido del nombre (LIKE %texto%).</param>
    [HttpGet]
    [Authorize(Policy = "perm:aplicaciones.actualizaciones.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Consultar(
        [FromQuery] int id = 0,
        [FromQuery] int idAplicacion = 0,
        [FromQuery] int idStatus = 0,
        [FromQuery] string? archivoOrigen = null,
        CancellationToken ct = default)
    {
        var data = await _service.ConsultarAsync(id, idAplicacion, idStatus, archivoOrigen, ct);

        // Son rutas del servidor: que no se queden en el cache del navegador.
        Response.Headers["Cache-Control"] = "no-store";

        return Ok(new { ok = true, message = "Actualizaciones consultadas.", data });
    }
}
