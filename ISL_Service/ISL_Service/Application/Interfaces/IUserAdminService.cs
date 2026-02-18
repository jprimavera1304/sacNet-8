using System.Security.Claims;
using ISL_Service.Application.DTOs.Requests;
using ISL_Service.Application.DTOs.Responses;

namespace ISL_Service.Application.Interfaces;

public interface IUserAdminService
{
    Task<CreateUserResponse> CreateUserAsync(CreateUserRequest req, ClaimsPrincipal actor, CancellationToken ct);
    Task<List<UserResponse>> ListUsersAsync(ClaimsPrincipal actor, CancellationToken ct);
    Task<List<UserRoleOptionResponse>> ListRolesCatalogAsync(ClaimsPrincipal actor, CancellationToken ct);
    Task<UserResponse> GetUserByIdAsync(Guid userId, ClaimsPrincipal actor, CancellationToken ct);
    Task<UserResponse> UpdateUserAsync(Guid userId, UpdateUserRequest req, ClaimsPrincipal actor, CancellationToken ct);
    Task<UserResponse> UpdateEstadoAsync(Guid userId, UpdateUserEstadoRequest req, ClaimsPrincipal actor, CancellationToken ct);
    Task<ResetPasswordResponse> ResetPasswordAsync(Guid userId, ClaimsPrincipal actor, CancellationToken ct);
    Task<UserResponse> UpdateEmpresaAsync(Guid userId, UpdateUserEmpresaRequest req, ClaimsPrincipal actor, CancellationToken ct);
    Task ChangeMyPasswordAsync(ChangePasswordRequest req, ClaimsPrincipal actor, CancellationToken ct);
}
