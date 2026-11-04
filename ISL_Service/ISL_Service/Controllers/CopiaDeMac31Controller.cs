using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// EL BOTON DE "COPIAR PERMISOS DE MAC31", DEL LADO DEL SERVIDOR.
/// </summary>
/// <remarks>
/// POR QUE UN CONTROLADOR APARTE Y NO TRES METODOS EN PermisosWebController:
/// porque son tres operaciones de una sola decision -sacar a una persona de la
/// herencia en vivo- y conviene poder leerlas juntas, con su explicacion, sin
/// buscarlas entre los nueve endpoints de la pantalla de permisos. Ademas ese
/// archivo lo esta trabajando otra persona.
///
/// EL PERMISO ES EL MISMO QUE YA PROTEGE LOS AJUSTES POR USUARIO
/// (`permisosweb.overrides.editar`), y es a proposito: copiar es exactamente
/// eso -escribirle ajustes a una persona-, solo que de golpe y calculados. Dar
/// una llave nueva obligaria a repartirla y dejaria el boton apagado para
/// quien ya administra permisos hoy.
/// </remarks>
[ApiController]
[Route("api/permisos-web/copia-mac31")]
[Authorize]
public class CopiaDeMac31Controller : ControllerBase
{
    private readonly ICopiaDeMac31Service _copia;
    private readonly ICurrentUserAccessor _currentUser;

    public CopiaDeMac31Controller(ICopiaDeMac31Service copia, ICurrentUserAccessor currentUser)
    {
        _copia = copia;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Quien de esta empresa ya tiene sus propios permisos.
    /// </summary>
    /// <remarks>
    /// Lo lee la pantalla de permisos para poder decir en que estado esta cada
    /// persona. Pide la misma llave que el bootstrap de esa pantalla, no la de
    /// editar: consultar el estado es parte de verla.
    /// </remarks>
    [HttpGet]
    [Authorize(Policy = "perm:permisosweb.bootstrap|app_movil.permisosweb.bootstrap")]
    public async Task<IActionResult> Listar(CancellationToken ct)
    {
        if (!TryGetEmpresaId(out var empresaId))
            return Unauthorized(new { message = "Token invalido." });

        var copias = await _copia.ListarCopiasDeMac31Async(empresaId, ct);
        return Ok(new { ok = true, copias });
    }

    [HttpPost("{userId:guid}")]
    [Authorize(Policy = "perm:permisosweb.overrides.editar|app_movil.permisos_usuarios.editar")]
    public async Task<IActionResult> Copiar([FromRoute] Guid userId, CancellationToken ct)
    {
        if (!TryGetEmpresaId(out var empresaId))
            return Unauthorized(new { message = "Token invalido." });

        try
        {
            var resultado = await _copia.CopiarDeMac31Async(
                empresaId, userId, _currentUser.GetUsername(User), ct);
            return Ok(resultado);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
    }

    [HttpDelete("{userId:guid}")]
    [Authorize(Policy = "perm:permisosweb.overrides.editar|app_movil.permisos_usuarios.editar")]
    public async Task<IActionResult> Deshacer([FromRoute] Guid userId, CancellationToken ct)
    {
        if (!TryGetEmpresaId(out var empresaId))
            return Unauthorized(new { message = "Token invalido." });

        try
        {
            var deshecha = await _copia.DeshacerCopiaDeMac31Async(empresaId, userId, ct);
            return Ok(new { ok = true, deshecha });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    private bool TryGetEmpresaId(out int empresaId)
    {
        var value = _currentUser.GetCompanyId(User);
        empresaId = value ?? 0;
        return value is not null;
    }
}
