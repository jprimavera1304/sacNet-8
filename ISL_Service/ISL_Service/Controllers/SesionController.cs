using Microsoft.AspNetCore.Mvc;
using ISL_Service.Application.DTOs.Requests;
using ISL_Service.Application.DTOs.Responses;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Controllers;

[ApiController]
public class SesionController : ControllerBase
{
    private readonly IUserService _users;

    public SesionController(IUserService users)
    {
        _users = users;
    }

    // RUTA ABSOLUTA (no choca con api/[controller])
    [HttpPost("/api/sesion/login")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _users.LoginAsync(request, ct);
        return Ok(result);
    }
}
