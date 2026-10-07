using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// LAS CAPACIDADES DE LA EMPRESA EN LA QUE CORRE ESTA INSTALACION.
///
/// Esto NO es /api/capacidades. Ese devuelve las capacidades de PERMISO del usuario
/// (que puede hacer el). Este devuelve las capacidades de la EMPRESA (como se
/// comporta el sistema aqui), que es un eje distinto y se combinan: una accion se
/// dibuja si la empresa la tiene Y el usuario tiene permiso.
///
/// EL CAMINO NORMAL ES /api/me. Las mismas capacidades viajan dentro de /api/me, en
/// el campo "empresa", porque el web y el movil ya lo llaman justo despues de
/// autenticar y ya lo cachean (authz.cache.<userId> en movil, la sesion en el web).
/// Colgarlas ahi fue la unica forma de no agregar un tercer round-trip al arranque.
///
/// Este endpoint existe para RE-LEERLAS sin volver a pedir la sesion entera: al
/// cambiar la fecha de operacion, al volver de segundo plano, o al refrescar la
/// pantalla de configuracion. Devuelve exactamente el mismo objeto.
///
/// NO EXIGE PERMISO, igual que /api/me: sin esto el front no sabe que dibujar, asi
/// que negarlo no protege nada y en cambio dejaria la pantalla en blanco. Dentro no
/// hay ningun dato sensible: son banderas y umbrales de configuracion.
/// </summary>
[ApiController]
[Route("api/empresa")]
[Authorize]
public class EmpresaCapacidadesController : ControllerBase
{
    private readonly ICapacidadesEmpresaService _service;
    private readonly ICurrentUserAccessor _currentUser;

    public EmpresaCapacidadesController(
        ICapacidadesEmpresaService service,
        ICurrentUserAccessor currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Las banderas, los umbrales y las dos fechas de operacion de esta empresa.
    /// El IDUsuario de Mac31 sale del token, nunca del cliente: en Zaragoza la
    /// fecha de pagos es de cada usuario y pedirla por parametro dejaria ver -y
    /// luego mover- la de otro.
    /// </summary>
    [HttpGet("capacidades")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Capacidades(CancellationToken ct)
    {
        var data = await _service.ConsultarAsync(_currentUser.GetLegacyUserId(User), ct);
        return Ok(new { ok = true, message = "Capacidades de la empresa.", data });
    }
}
