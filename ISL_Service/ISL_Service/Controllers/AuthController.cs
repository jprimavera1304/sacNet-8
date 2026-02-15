//using Microsoft.AspNetCore.Mvc;
//using ISL_Service.Application.DTOs.Requests;
//using ISL_Service.Application.DTOs.Responses;
//using ISL_Service.Application.Interfaces;

//namespace ISL_Service.Controllers;

//[ApiController]
//[Route("api/auth")]
//public class AuthController : ControllerBase
//{
//    private readonly IUserService _users;

//    public AuthController(IUserService users)
//    {
//        _users = users;
//    }

//    [HttpGet("ping")]
//    public IActionResult Ping() => Ok("pong");

//    [HttpPost("login")]
//    [Consumes("application/json")]
//    [Produces("application/json")]
//    public async Task<ActionResult<LoginResponse>> Login(
//        [FromBody] LoginRequest request,
//        CancellationToken ct)
//    {
//        var result = await _users.LoginAsync(request, ct);
//        return Ok(result);
//    }
//}
