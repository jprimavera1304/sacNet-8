using ISL_Service.Application.DTOs.Requests;
using ISL_Service.Application.DTOs.Responses;

namespace ISL_Service.Application.Interfaces;

public interface IUserService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct);
    // Canjea un refresh token por un nuevo access token (y rota el refresh).
    Task<LoginResponse> RefreshAsync(string refreshToken, CancellationToken ct);
    // Revoca un refresh token (logout).
    Task LogoutAsync(string refreshToken, CancellationToken ct);
}
