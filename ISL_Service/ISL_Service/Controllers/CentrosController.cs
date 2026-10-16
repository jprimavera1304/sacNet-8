using ISL_Service.Application.DTOs.Centros;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// EL CATALOGO DE CENTROS DE SERVICIO, GLOBAL.
/// </summary>
/// <remarks>
/// POR QUE NO CUELGA DE REPORTES
/// -----------------------------
/// Porque los centros ya no son una cosa de reportes: el selector vive en el
/// contexto de la aplicacion y cualquier modulo declarado va a trabajar con el.
/// Un catalogo colgado de `/api/reportes/ventas/centros` obliga a quien quiera
/// cambiar de sucursal a pedirle permiso al modulo de reportes, y es la razon
/// por la que las claves de hoy se llaman `reportes.centros.*` gobernando algo
/// que no es de reportes.
///
/// `GET /api/reportes/ventas/centros` NO SE BORRA NI SE CAMBIA. Lo consume el
/// web y lo consumen los APK que ya estan instalados. Esto se suma; aquello
/// sigue contestando exactamente igual. El dia que no quede nadie llamandolo se
/// retira, y ese dia se mide, no se supone.
///
/// NO HAY POLITICA [Authorize(Policy = ...)] DE MODULO AQUI, a proposito: el
/// catalogo no es de ningun modulo. Basta con estar autenticado; QUE centros
/// salen lo decide el servidor contra los permisos de cada uno, y a quien no
/// pueda cambiar le sale la matriz y una lista vacia.
/// </remarks>
[ApiController]
[Route("api/centros")]
[Authorize]
public class CentrosController : ControllerBase
{
    private readonly IConexionDelCentro _centros;

    public CentrosController(IConexionDelCentro centros)
    {
        _centros = centros;
    }

    /// <summary>
    /// Los centros que ESTE usuario puede ver, mas la matriz con el nombre
    /// comercial de la empresa.
    /// </summary>
    /// <remarks>
    /// `Cache-Control: no-store` porque la respuesta depende de QUIEN pregunta.
    /// Un intermediario que la guardara le ensenaria las sucursales de un
    /// usuario a otro, y el nombre de la empresa es lo de menos: la lista de
    /// centros ES el permiso dibujado.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(CentrosResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Consultar(CancellationToken ct)
    {
        var respuesta = await _centros.CatalogoAsync(ct);

        Response.Headers["Cache-Control"] = "no-store";
        return Ok(respuesta);
    }
}
