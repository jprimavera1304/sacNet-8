using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISL_Service.Application.DTOs.Requests;
using ISL_Service.Application.DTOs.Empresa;
using ISL_Service.Application.DTOs.Responses;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/me")]
public class MeController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly IUserAdminService _userAdminService;
    private readonly IEmpresaRepository _empresas;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly ICapacidadesEmpresaService _capacidadesEmpresa;

    public MeController(
        IUserRepository users,
        IUserAdminService userAdminService,
        IEmpresaRepository empresas,
        IPermissionService permissionService,
        ICurrentUserAccessor currentUserAccessor,
        ICapacidadesEmpresaService capacidadesEmpresa)
    {
        _users = users;
        _userAdminService = userAdminService;
        _empresas = empresas;
        _permissionService = permissionService;
        _currentUserAccessor = currentUserAccessor;
        _capacidadesEmpresa = capacidadesEmpresa;
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var userId = _currentUserAccessor.GetUserId(User);
        if (userId is null)
            return Unauthorized(new { message = "Token invalido." });

        var user = await _users.GetByIdAsync(userId.Value, ct);
        if (user is null)
            return NotFound(new { message = "Usuario no encontrado." });

        var permissionSnapshot = await _permissionService.GetPermissionsAsync(
            user.Id,
            user.EmpresaId,
            user.Rol,
            ct);

        var companyKey = await _empresas.GetCompanyKeyAsync(ct);

        /*
          LAS CAPACIDADES DE LA EMPRESA VIAJAN AQUI.

          Sin esto el front no sabe en que empresa esta parado y una pantalla nueva
          solo puede implementar una de las dos: Legacy ramifica en 430 sitios por
          seis valores de dbo.Constantes y ninguno llegaba al web ni al movil.

          Va dentro de /api/me porque es la llamada que los dos fronts ya hacen al
          autenticar y la que ya cachean; un endpoint propio habria sumado un tercer
          round-trip al arranque.

          Si la lectura falla, /api/me NO se cae: el usuario entra, el campo queda en
          null y el front lo trata como "todavia no se pueden dibujar reglas por
          empresa". Dejar sin sesion a todo el mundo porque dbo.Constantes no
          contesta seria peor que entrar con menos informacion.
        */
        CapacidadesEmpresaResponse? capacidades = null;
        try
        {
            capacidades = await _capacidadesEmpresa.ConsultarAsync(
                _currentUserAccessor.GetLegacyUserId(User), ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            capacidades = null;
        }

        var response = new MeResponse
        {
            UserId = user.Id,
            IdUsuario = _currentUserAccessor.GetLegacyUserId(User),
            Id = user.Id,
            Usuario = user.UsuarioNombre,
            RolLegacy = user.Rol,
            Rol = user.Rol,
            EmpresaId = user.EmpresaId,
            PermissionsEnabled = permissionSnapshot.PermissionsEnabled,
            Permissions = permissionSnapshot.Permissions,
            PermissionsVersion = permissionSnapshot.PermissionsVersion,
            CompanyKey = companyKey,
            DebeCambiarContrasena = user.DebeCambiarContrasena,
            Empresa = capacidades
        };

        return Ok(response);
    }
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req, CancellationToken ct)
    {
        await _userAdminService.ChangeMyPasswordAsync(req, User, ct);
        return NoContent();
    }
}
