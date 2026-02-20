using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/empresas")]
[Authorize(Policy = "perm:empresas.ver")]
public class EmpresasController : ControllerBase
{
    private readonly IEmpresaService _service;

    public EmpresasController(IEmpresaService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _service.ListAsync(User, ct);
        return Ok(result);
    }
}
