using System.Security.Claims;
using ISL_Service.Application.DTOs.Requests;
using ISL_Service.Application.DTOs.Responses;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using ISL_Service.Domain.Entities;

namespace ISL_Service.Application.Services;

public class UserAdminService : IUserAdminService
{
    private const int EMPRESA_FIJA = 1;

    // Estados segun regla:
    // 1 = Activo, 2 = Inactivo, 3 = Bloqueado
    private const int ESTADO_ACTIVO = 1;
    private const int ESTADO_INACTIVO = 2;
    private const int ESTADO_BLOQUEADO = 3;

    private readonly IUserRepository _repo;

    public UserAdminService(IUserRepository repo)
    {
        _repo = repo;
    }

    public async Task<CreateUserResponse> CreateUserAsync(CreateUserRequest req, ClaimsPrincipal actor, CancellationToken ct)
    {
        var usuarioNombre = NormalizeUsuario(req.Usuario);
        var rol = await NormalizeRolAsync(req.Rol, ct);

        if (await _repo.ExistsByUsuarioAsync(usuarioNombre, ct))
            throw new ConflictException("El usuario ya existe.");

        var hash = BCrypt.Net.BCrypt.HashPassword(req.PasswordTemporal);
        var entity = await _repo.UpsertWebAndLegacyAsync(
            usuarioNombre,
            req.PasswordTemporal,
            hash,
            usuarioNombre,
            rol,
            debeCambiarContrasena: true,
            estado: ESTADO_ACTIVO,
            ct);

        return new CreateUserResponse
        {
            User = Map(entity),
            PasswordTemporal = req.PasswordTemporal
        };
    }

    public async Task<List<UserResponse>> ListUsersAsync(ClaimsPrincipal actor, CancellationToken ct)
    {
        var list = await _repo.ListAsync(EMPRESA_FIJA, ct);
        return list.Select(Map).ToList();
    }

    public async Task<List<UserRoleOptionResponse>> ListRolesCatalogAsync(ClaimsPrincipal actor, CancellationToken ct)
    {
        var roles = await _repo.ListRolesCatalogAsync(EMPRESA_FIJA, ct);
        return roles
            .Select(r =>
            {
                var code = NormalizeRoleCode(r.Code);
                return new UserRoleOptionResponse
                {
                    Code = code,
                    Name = string.IsNullOrWhiteSpace(r.Name) ? code : r.Name.Trim(),
                    Value = ToStoredRolValue(code)
                };
            })
            .Where(r => !string.IsNullOrWhiteSpace(r.Code))
            .GroupBy(r => r.Code, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(r => r.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<UserResponse> UpdateUserAsync(Guid userId, UpdateUserRequest req, ClaimsPrincipal actor, CancellationToken ct)
    {
        var user = await _repo.GetByIdAsync(userId, ct) ?? throw new NotFoundException("Usuario no encontrado.");

        EnsureActorCanManageTarget(actor, user);

        var usuarioNuevo = NormalizeUsuario(req.Usuario);
        var rolNuevo = await NormalizeRolAsync(req.Rol, ct);

        var existing = await _repo.GetByUsuarioAsync(usuarioNuevo, ct);
        if (existing is not null && existing.Id != userId)
            throw new ConflictException("El usuario ya existe.");

        var updated = await _repo.UpdateUsuarioAndRolAsync(userId, usuarioNuevo, rolNuevo, ct);
        return Map(updated);
    }

    public async Task<UserResponse> UpdateEstadoAsync(Guid userId, UpdateUserEstadoRequest req, ClaimsPrincipal actor, CancellationToken ct)
    {
        var user = await _repo.GetByIdAsync(userId, ct) ?? throw new NotFoundException("Usuario no encontrado.");

        EnsureActorCanManageTarget(actor, user);
        var updated = await _repo.UpdateEstadoWithLegacyAsync(userId, req.Estado, ct);
        return Map(updated);
    }

    public async Task<ResetPasswordResponse> ResetPasswordAsync(Guid userId, ClaimsPrincipal actor, CancellationToken ct)
    {
        var user = await _repo.GetByIdAsync(userId, ct) ?? throw new NotFoundException("Usuario no encontrado.");

        EnsureActorCanManageTarget(actor, user);

        var temp = PasswordGenerator.Generate(12);
        var hash = BCrypt.Net.BCrypt.HashPassword(temp);

        var updated = await _repo.UpsertWebAndLegacyAsync(
            user.UsuarioNombre,
            temp,
            hash,
            user.UsuarioNombre,
            user.Rol,
            debeCambiarContrasena: true,
            estado: user.Estado,
            ct);

        return new ResetPasswordResponse
        {
            UserId = updated.Id,
            PasswordTemporal = temp,
            DebeCambiarContrasena = true
        };
    }

    public Task<UserResponse> UpdateEmpresaAsync(Guid userId, UpdateUserEmpresaRequest req, ClaimsPrincipal actor, CancellationToken ct)
    {
        return Task.FromException<UserResponse>(
            new InvalidOperationException("En este modelo por base de datos, EmpresaId siempre es 1."));
    }

    public async Task ChangeMyPasswordAsync(ChangePasswordRequest req, ClaimsPrincipal actor, CancellationToken ct)
    {
        var myId = CurrentUser.GetUserId(actor);
        var user = await _repo.GetByIdAsync(myId, ct) ?? throw new NotFoundException("Usuario no encontrado.");

        if (user.Estado != ESTADO_ACTIVO)
        {
            if (user.Estado == ESTADO_INACTIVO) throw new UnauthorizedAccessException("Usuario inactivo.");
            if (user.Estado == ESTADO_BLOQUEADO) throw new UnauthorizedAccessException("Usuario bloqueado.");
            throw new UnauthorizedAccessException("Usuario no autorizado.");
        }

        if (!BCrypt.Net.BCrypt.Verify(req.ContrasenaActual, user.ContrasenaHash))
            throw new InvalidOperationException("Contrasena actual incorrecta.");

        var hashNueva = BCrypt.Net.BCrypt.HashPassword(req.NuevaContrasena);

        await _repo.UpsertWebAndLegacyAsync(
            user.UsuarioNombre,
            req.NuevaContrasena,
            hashNueva,
            user.UsuarioNombre,
            user.Rol,
            debeCambiarContrasena: false,
            estado: user.Estado,
            ct);
    }

    private static void EnsureActorCanManageTarget(ClaimsPrincipal actor, Usuario target)
    {
        var isSuper = CurrentUser.IsSuperAdmin(actor);
        if (isSuper) return;

        var actorEmpresaId = CurrentUser.GetEmpresaId(actor);
        if (actorEmpresaId != target.EmpresaId)
            throw new UnauthorizedAccessException("No puedes administrar usuarios fuera de tu empresa.");

        // Evita que Admin administre a SuperAdmin
        if (string.Equals(target.Rol, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("No puedes administrar usuarios SuperAdmin.");
    }

    private static UserResponse Map(Usuario u) => new()
    {
        Id = u.Id,
        Usuario = u.UsuarioNombre,
        Rol = u.Rol,
        EmpresaId = u.EmpresaId,
        Estado = u.Estado,
        DebeCambiarContrasena = u.DebeCambiarContrasena,
        FechaCreacion = u.FechaCreacion,
        FechaActualizacion = u.FechaActualizacion
    };

    public async Task<UserResponse> GetUserByIdAsync(Guid userId, ClaimsPrincipal actor, CancellationToken ct)
    {
        var user = await _repo.GetByIdAsync(userId, ct) ?? throw new NotFoundException("Usuario no encontrado.");

        EnsureActorCanManageTarget(actor, user);

        return Map(user);
    }

    private static string NormalizeUsuario(string usuario)
    {
        var normalized = (usuario ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized.Length < 4 || normalized.Length > 60)
            throw new ArgumentException("Usuario invalido. Longitud permitida: 4 a 60.");

        foreach (var ch in normalized)
        {
            var isValid = (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') || ch == '_';
            if (!isValid)
                throw new ArgumentException("Usuario invalido. Usa A-Z, 0-9 y _.");
        }

        return normalized;
    }

    private async Task<string> NormalizeRolAsync(string rol, CancellationToken ct)
    {
        var input = (rol ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Rol invalido. Seleccione un rol.");

        var inputCode = NormalizeRoleCode(input);
        var roles = await _repo.ListRolesCatalogAsync(EMPRESA_FIJA, ct);
        var match = roles.FirstOrDefault(r =>
            string.Equals(NormalizeRoleCode(r.Code), inputCode, StringComparison.OrdinalIgnoreCase) ||
            string.Equals((r.Name ?? string.Empty).Trim(), input, StringComparison.OrdinalIgnoreCase));

        if (match is null)
            throw new ArgumentException("Rol invalido. No existe en catalogo de roles.");

        return ToStoredRolValue(NormalizeRoleCode(match.Code));
    }

    private static string NormalizeRoleCode(string roleCode)
    {
        return string.Concat((roleCode ?? string.Empty)
                .Trim()
                .ToUpperInvariant()
                .Select(ch =>
                {
                    if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9')) return ch;
                    if (ch == '_' || ch == '-' || char.IsWhiteSpace(ch)) return '_';
                    return '\0';
                })
                .Where(ch => ch != '\0'))
            .Trim('_');
    }

    private static string ToStoredRolValue(string roleCode)
    {
        if (string.Equals(roleCode, "SUPER_ADMIN", StringComparison.OrdinalIgnoreCase)) return "SuperAdmin";
        if (string.Equals(roleCode, "ADMIN", StringComparison.OrdinalIgnoreCase)) return "Admin";
        if (string.Equals(roleCode, "USER", StringComparison.OrdinalIgnoreCase)) return "User";
        return roleCode;
    }
}
