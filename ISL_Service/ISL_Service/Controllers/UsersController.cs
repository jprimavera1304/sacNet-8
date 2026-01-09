using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _users;

    public UsersController(IUserService users) => _users = users;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var list = await _users.GetAllAsync();
        return Ok(list);
    }
}