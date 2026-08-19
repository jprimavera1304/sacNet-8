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
    // Estados segun regla:
    // 1 = Activo, 2 = Inactivo, 3 = Bloqueado
    private const int ESTADO_ACTIVO = 1;
    private const int ESTADO_INACTIVO = 2;
    private const int ESTADO_BLOQUEADO = 3;

    private readonly IUserRepository _repo;
    private readonly IPermissionService _permissionService;

    public UserAdminService(IUserRepository repo, IPermissionService permissionService)
    {
        _repo = repo;
        _permissionService = permissionService;
    }

    public async Task<CreateUserResponse> CreateUserAsync(CreateUserRequest req, ClaimsPrincipal actor, CancellationToken ct)
    {
        var usuarioNombre = NormalizeUsuario(req.Usuario);
        var rol = await NormalizeRolAsync(req.Rol, actor, ct);

        if (await _repo.ExistsByUsuarioAsync(usuarioNombre, ct))
            throw new ConflictException("El usuario ya existe.");

        var password = NormalizePassword(req.ResolverPassword());
        var hash = BCrypt.Net.BCrypt.HashPassword(password);

        // Sin cambio forzado: la contrasena que escribio el administrador es la
        // definitiva, y el usuario entra con ella a la primera.
        var entity = await _repo.UpsertWebAndLegacyAsync(
            usuarioNombre,
            password,
            hash,
            usuarioNombre,
            rol,
            debeCambiarContrasena: false,
            estado: ESTADO_ACTIVO,
            ct);

        // Usuario nuevo debe iniciar con permisos efectivos del rol (sin overrides heredados/previos).
        try
        {
            await _permissionService.UpsertUserOverridesAsync(
                entity.EmpresaId,
                entity.Id,
                Array.Empty<string>(),
                Array.Empty<string>(),
                ct);
        }
        catch
        {
            // Si el esquema de capacidades aún no existe, no bloquea la creación de usuario.
        }

        return new CreateUserResponse
        {
            User = Map(entity),
            Password = password
        };
    }

    public async Task<List<UserResponse>> ListUsersAsync(ClaimsPrincipal actor, CancellationToken ct)
    {
        var empresaId = ResolveEmpresaId(actor);
        var list = await _repo.ListAsync(empresaId, ct);
        return list.Select(Map).ToList();
    }

    public async Task<List<UserRoleOptionResponse>> ListRolesCatalogAsync(ClaimsPrincipal actor, CancellationToken ct)
    {
        var empresaId = ResolveEmpresaId(actor);
        var roles = await _repo.ListRolesCatalogAsync(empresaId, ct);
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
        var rolNuevo = await NormalizeRolAsync(req.Rol, actor, ct);

        var existing = await _repo.GetByUsuarioAsync(usuarioNuevo, ct);
        if (existing is not null && existing.Id != userId)
            throw new ConflictException("El usuario ya existe.");

        var updated = await _repo.UpdateUsuarioAndRolAsync(userId, usuarioNuevo, rolNuevo, ct);

        // La contrasena es opcional al editar: dejarla vacia significa "no la toques".
        if (!string.IsNullOrWhiteSpace(req.Password))
        {
            var password = NormalizePassword(req.Password!);
            var hash = BCrypt.Net.BCrypt.HashPassword(password);
            updated = await _repo.UpsertWebAndLegacyAsync(
                usuarioNuevo,
                password,
                hash,
                usuarioNuevo,
                rolNuevo,
                debeCambiarContrasena: false,
                estado: updated.Estado,
                ct);
        }

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

        // Sin contrasena a la mano se genera una, pero ya no es "temporal": queda
        // como la definitiva y el administrador la puede ver con el ojito.
        var generada = PasswordGenerator.Generate(12);
        return await AplicarPasswordAsync(user, generada, ct);
    }

    public async Task<ResetPasswordResponse> SetPasswordAsync(Guid userId, SetUserPasswordRequest req, ClaimsPrincipal actor, CancellationToken ct)
    {
        var user = await _repo.GetByIdAsync(userId, ct) ?? throw new NotFoundException("Usuario no encontrado.");

        EnsureActorCanManageTarget(actor, user);

        return await AplicarPasswordAsync(user, NormalizePassword(req.ResolverPassword()), ct);
    }

    public async Task<UserPasswordResponse> GetPasswordAsync(Guid userId, ClaimsPrincipal actor, CancellationToken ct)
    {
        var user = await _repo.GetByIdAsync(userId, ct) ?? throw new NotFoundException("Usuario no encontrado.");

        EnsureActorCanManageTarget(actor, user);

        var password = await _repo.GetLegacyPasswordAsync(user.UsuarioNombre, ct);

        // El hash de UsuarioWeb no se puede revertir. Si legacy no tiene al usuario
        // no hay contrasena que mostrar, y eso se dice claro en vez de tronar.
        if (string.IsNullOrEmpty(password))
        {
            return new UserPasswordResponse
            {
                UserId = user.Id,
                Usuario = user.UsuarioNombre,
                Password = null,
                Disponible = false,
                Mensaje = "No hay contrasena guardada en legacy para este usuario. Asigne una nueva para poder verla."
            };
        }

        return new UserPasswordResponse
        {
            UserId = user.Id,
            Usuario = user.UsuarioNombre,
            Password = password,
            Disponible = true
        };
    }

    private async Task<ResetPasswordResponse> AplicarPasswordAsync(Usuario user, string password, CancellationToken ct)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(password);

        // Un solo camino para las dos escrituras: el SP deja el hash en UsuarioWeb
        // y la contrasena en claro en legacy. Si esto se partiera en dos, los dos
        // lados se desincronizarian a la primera falla.
        var updated = await _repo.UpsertWebAndLegacyAsync(
            user.UsuarioNombre,
            password,
            hash,
            user.UsuarioNombre,
            user.Rol,
            debeCambiarContrasena: false,
            estado: user.Estado,
            ct);

        return new ResetPasswordResponse
        {
            UserId = updated.Id,
            Password = password,
            DebeCambiarContrasena = false
        };
    }

    private static string NormalizePassword(string password)
    {
        var value = (password ?? string.Empty).Trim();
        if (value.Length < 8 || value.Length > 100)
            throw new ArgumentException("Contrasena invalida. Longitud permitida: 8 a 100.");
        return value;
    }

    public Task<UserResponse> UpdateEmpresaAsync(Guid userId, UpdateUserEmpresaRequest req, ClaimsPrincipal actor, CancellationToken ct)
    {
        return Task.FromException<UserResponse>(
            new InvalidOperationException("En este modelo por base de datos, cada base maneja una sola empresa."));
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
            throw new ArgumentException("Usuario inválido. Longitud permitida: 4 a 60.");

        foreach (var ch in normalized)
        {
            var isValid = (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') || ch == '_';
            if (!isValid)
                throw new ArgumentException("Usuario inválido. Usa A-Z, 0-9 y _.");
        }

        return normalized;
    }

    private async Task<string> NormalizeRolAsync(string rol, ClaimsPrincipal actor, CancellationToken ct)
    {
        var input = (rol ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Rol inválido. Seleccione un rol.");

        var inputCode = NormalizeRoleCode(input);
        var roles = await _repo.ListRolesCatalogAsync(ResolveEmpresaId(actor), ct);
        var match = roles.FirstOrDefault(r =>
            string.Equals(NormalizeRoleCode(r.Code), inputCode, StringComparison.OrdinalIgnoreCase) ||
            string.Equals((r.Name ?? string.Empty).Trim(), input, StringComparison.OrdinalIgnoreCase));

        if (match is null)
            throw new ArgumentException("Rol inválido. No existe en catálogo de roles.");

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

    private static int ResolveEmpresaId(ClaimsPrincipal? actor = null)
    {
        var id = actor is null ? 0 : CurrentUser.GetEmpresaId(actor);
        return id > 0 ? id : 1;
    }
}
