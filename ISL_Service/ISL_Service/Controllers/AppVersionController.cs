using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Version minima y recomendada de la app movil.
///
/// Va ANONIMO a proposito: la app pregunta ANTES del login. Si su version quedo
/// fuera, no tiene caso dejarla siquiera intentar entrar; y si el usuario no
/// puede entrar por viejo, tampoco podria pedir un token para preguntarlo.
///
/// No expone nada sensible: solo dos numeros de version y la liga a la tienda,
/// que de todos modos es publica.
/// </summary>
[ApiController]
[Route("api/app")]
[AllowAnonymous]
public class AppVersionController : ControllerBase
{
    private static readonly string[] PlataformasValidas = { "android", "ios" };

    private readonly IAppVersionRepository _repository;

    public AppVersionController(IAppVersionRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Devuelve que version exige el servidor para esa plataforma.
    /// </summary>
    /// <response code="200">Siempre 200. Si la plataforma no se reconoce, responde 0.0.0 (no bloquear).</response>
    [HttpGet("version")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Consultar([FromQuery] string? plataforma, CancellationToken ct)
    {
        var normalizada = (plataforma ?? string.Empty).Trim().ToLowerInvariant();
        if (!PlataformasValidas.Contains(normalizada))
            normalizada = string.Empty;

        var data = await _repository.ConsultarAsync(normalizada, ct);
        return Ok(new { ok = true, message = "Version consultada.", data });
    }
}
