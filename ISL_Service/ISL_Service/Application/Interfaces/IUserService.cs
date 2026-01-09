using ISL_Service.Application.DTOs.Responses;
using ISL_Service.Application.DTOs.Requests;

namespace ISL_Service.Application.Interfaces;

public interface IUserService
{
    Task<List<UserResponse>> GetAllAsync();
    Task<UserResponse?> RegisterAsync(RegisterRequest request);
    Task<LoginResponse?> LoginAsync(LoginRequest request);
}