using System.Security.Claims;
using ISL_Service.Application.DTOs.Requests;
using ISL_Service.Application.DTOs.Responses;
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
        var isSuper = CurrentUser.IsSuperAdmin(actor);

        // Admin normal no puede crear SuperAdmin
        if (!isSuper && string.Equals(req.Rol, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("No puedes crear usuarios SuperAdmin.");

        var usuarioNombre = req.Usuario.Trim();

        if (await _repo.ExistsByUsuarioAsync(usuarioNombre, ct))
            throw new InvalidOperationException("El usuario ya existe.");

        var hash = BCrypt.Net.BCrypt.HashPassword(req.PasswordTemporal);
        var entity = await _repo.UpsertWebAndLegacyAsync(
            usuarioNombre,
            req.PasswordTemporal,
            hash,
            usuarioNombre,
            req.Rol.Trim(),
            debeCambiarContrasena: true,
            estado: ESTADO_ACTIVO,
            ct);

        return new CreateUserResponse
        {
            User = Map(entity),
            PasswordTemporal = req.PasswordTemporal
        };
    }

    public async Task<List<UserResponse>> ListUsersAsync(int? empresaId, ClaimsPrincipal actor, CancellationToken ct)
    {
        var list = await _repo.ListAsync(EMPRESA_FIJA, ct);
        return list.Select(Map).ToList();
    }

    public async Task<UserResponse> UpdateEstadoAsync(Guid userId, UpdateUserEstadoRequest req, ClaimsPrincipal actor, CancellationToken ct)
    {
        var user = await _repo.GetByIdAsync(userId, ct) ?? throw new KeyNotFoundException("Usuario no encontrado.");

        EnsureActorCanManageTarget(actor, user);

        user.Estado = req.Estado;
        user.FechaActualizacion = DateTime.UtcNow;

        await _repo.UpdateAsync(user, ct);
        await _repo.SaveChangesAsync(ct);

        return Map(user);
    }

    public async Task<ResetPasswordResponse> ResetPasswordAsync(Guid userId, ClaimsPrincipal actor, CancellationToken ct)
    {
        var user = await _repo.GetByIdAsync(userId, ct) ?? throw new KeyNotFoundException("Usuario no encontrado.");

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
        var user = await _repo.GetByIdAsync(myId, ct) ?? throw new KeyNotFoundException("Usuario no encontrado.");

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
        var user = await _repo.GetByIdAsync(userId, ct) ?? throw new KeyNotFoundException("Usuario no encontrado.");

        EnsureActorCanManageTarget(actor, user);

        return Map(user);
    }
}
