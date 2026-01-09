using ISL_Service.Application.DTOs.Requests;
using ISL_Service.Application.DTOs.Responses;
using ISL_Service.Application.Interfaces;
using ISL_Service.Domain.Entities;

namespace ISL_Service.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _repo;
    private readonly IJwtTokenGenerator _jwt;

    public UserService(IUserRepository repo, IJwtTokenGenerator jwt)
    {
        _repo = repo;
        _jwt = jwt;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var email = request.Email.Trim().ToLower();

        var user = await _repo.FindByEmailAsync(email);
        if (user is null) return null;

        var ok = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        if (!ok) return null;

        var token = _jwt.Create(user.Id, user.Email, user.Role);
        return new LoginResponse(token);
    }

    public async Task<List<UserResponse>> GetAllAsync()
    {
        var users = await _repo.GetAllAsync();

        return users.Select(u => new UserResponse(
            u.Id, u.Email, u.Role, u.CreatedAt
        )).ToList();
    }

    public async Task<UserResponse?> RegisterAsync(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLower();

        var existing = await _repo.FindByEmailAsync(email);
        if (existing is not null) return null;

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = "User",
            CreatedAt = DateTime.UtcNow
        };

        await _repo.AddAsync(user);
        await _repo.SaveChangesAsync();

        return new UserResponse(user.Id, user.Email, user.Role, user.CreatedAt);
    }
}