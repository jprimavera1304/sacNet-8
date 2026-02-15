//using Microsoft.AspNetCore.Mvc;
//using BCrypt.Net;

//namespace ISL_Service.Controllers;

//[ApiController]
//[Route("api/dev")]
//public class DevController : ControllerBase
//{
//    [HttpPost("hash")]
//    public IActionResult Hash([FromBody] string password)
//    {
//        var hash = BCrypt.Net.BCrypt.HashPassword(password);
//        return Ok(new { password, hash });
//    }
//}
