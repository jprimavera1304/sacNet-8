using ISL_Service.Application.DTOs.Requests;
using ISL_Service.Application.DTOs.Responses;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserService _users;

    public AuthController(IUserService users) => _users = users;

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var created = await _users.RegisterAsync(request);

        if (created is null)
            return Conflict(new ApiError("Email already exists"));

        return Created("", created);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await _users.LoginAsync(request);
        if (result is null) return Unauthorized(new { message = "Invalid credentials" });
        return Ok(result);
    }
}