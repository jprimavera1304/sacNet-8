using ISL_Service.Application.DTOs.Requests;
using ISL_Service.Application.DTOs.Responses;

namespace ISL_Service.Application.Interfaces;

public interface IUserService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct);
}
