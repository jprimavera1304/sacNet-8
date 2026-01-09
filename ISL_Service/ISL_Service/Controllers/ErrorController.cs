using ISL_Service.Application.DTOs.Responses;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

[ApiController]
[Route("/error")]
public class ErrorController : ControllerBase
{
    [HttpGet]
    public IActionResult Error()
    {
        return Problem(title: "An unexpected error occurred.");
    }
}