using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Los catalogos de lectura que quedaron fuera de la primera ola: tipos de
/// domicilio, condiciones de credito, series de empresa, los tres catalogos del
/// SAT y marcas.
///
/// MISMO PREFIJO Y MISMOS PERMISOS QUE LOS DEMAS CATALOGOS, EN ARCHIVO APARTE
/// -------------------------------------------------------------------------
/// Las rutas viven bajo `api/catalogos` y usan `catalogos.ver`,
/// `catalogos.fe.ver` y `catalogos.marcas.ver`, que son los permisos que ya
/// sembro la entrega de catalogos. NO se vuelven a sembrar aqui: dos filas
/// declarando la misma clave significa dos duenos de la misma llave, y el dia
/// que alguien cambie el nombre de una cambiaria solo una de las dos.
///
/// El archivo es aparte para que las dos entregas no se pisen; las rutas no
/// chocan porque ninguna se repite.
/// </summary>
[ApiController]
[Route("api/catalogos")]
[Authorize]
public class CatalogosComplementosController : ControllerBase
{
    private readonly ICatalogosComplementosService _service;

    public CatalogosComplementosController(ICatalogosComplementosService service)
    {
        _service = service;
    }

    /// <summary>
    /// Tipos de domicilio ([Catalogo Tipos Domicilio]).
    /// </summary>
    /// <param name="idStatus">0 = todos (default), 1 = activos, 2 = inactivos.</param>
    /// <remarks>
    /// NO lleva `exacto`. sp_n_Consultar_TiposDomicilio declara @Identico y
    /// nunca lo mira: su filtro es siempre LIKE %texto%. Ofrecer el parametro
    /// seria prometer una busqueda exacta que no existe.
    /// </remarks>
    [HttpGet("tipos-domicilio")]
    [Authorize(Policy = "perm:catalogos.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> TiposDomicilio(
        [FromQuery] int id = 0,
        [FromQuery] int idStatus = 0,
        [FromQuery] string? texto = null,
        CancellationToken ct = default)
    {
        var data = await _service.ConsultarTiposDomicilioAsync(id, idStatus, texto, ct);
        return Ok(new { ok = true, message = "Tipos de domicilio consultados.", data });
    }

    /// <summary>
    /// Condiciones de credito.
    /// </summary>
    /// <remarks>
    /// No se expone filtro por dias de credito: el SP recibe @DiasCredito y no
    /// lo usa en ninguna rama. Legacy ni lo mandaba.
    ///
    /// El filtro de texto busca por PREFIJO (LIKE 'texto%') cuando no se pide
    /// exacto: asi lo hace el SP.
    /// </remarks>
    [HttpGet("condiciones-credito")]
    [Authorize(Policy = "perm:catalogos.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CondicionesCredito(
        [FromQuery] int id = 0,
        [FromQuery] int idStatus = 0,
        [FromQuery] string? texto = null,
        [FromQuery] bool exacto = false,
        CancellationToken ct = default)
    {
        var data = await _service.ConsultarCondicionesCreditoAsync(id, idStatus, texto, exacto, ct);
        return Ok(new { ok = true, message = "Condiciones de credito consultadas.", data });
    }

    /// <summary>Series de folios por empresa.</summary>
    [HttpGet("empresas-series")]
    [Authorize(Policy = "perm:catalogos.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> EmpresasSeries(
        [FromQuery] int id = 0,
        [FromQuery] int idEmpresa = 0,
        [FromQuery] int idStatus = 0,
        [FromQuery] string? texto = null,
        [FromQuery] bool exacto = false,
        CancellationToken ct = default)
    {
        var data = await _service.ConsultarEmpresasSeriesAsync(id, idEmpresa, idStatus, texto, exacto, ct);
        return Ok(new { ok = true, message = "Series de empresa consultadas.", data });
    }

    /// <summary>
    /// Formas de pago del SAT.
    /// </summary>
    /// <remarks>
    /// NO lleva `idStatus`: el SP lo recibe y lo ignora — la linea que usaba
    /// fn_RegresaTablaStatus esta comentada y el WHERE esta clavado a
    /// `IDStatus = 1`. Solo hay activos, y eso es lo que se publica.
    /// </remarks>
    [HttpGet("fe/formas-pago")]
    [Authorize(Policy = "perm:catalogos.fe.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<IActionResult> FeFormasPago(
        [FromQuery] int id = 0, [FromQuery] string? clave = null, [FromQuery] bool exacto = false,
        CancellationToken ct = default)
        => Fe(CatalogoFe.FormasPago, "Formas de pago consultadas.", id, clave, exacto, ct);

    /// <summary>Metodos de pago del SAT (PUE / PPD). Tampoco acepta idStatus, por lo mismo.</summary>
    [HttpGet("fe/metodos-pago")]
    [Authorize(Policy = "perm:catalogos.fe.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<IActionResult> FeMetodosPago(
        [FromQuery] int id = 0, [FromQuery] string? clave = null, [FromQuery] bool exacto = false,
        CancellationToken ct = default)
        => Fe(CatalogoFe.MetodosPago, "Metodos de pago consultados.", id, clave, exacto, ct);

    /// <summary>Motivos de cancelacion del SAT. Tampoco acepta idStatus, por lo mismo.</summary>
    [HttpGet("fe/motivos-cancelacion")]
    [Authorize(Policy = "perm:catalogos.fe.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<IActionResult> FeMotivosCancelacion(
        [FromQuery] int id = 0, [FromQuery] string? clave = null, [FromQuery] bool exacto = false,
        CancellationToken ct = default)
        => Fe(CatalogoFe.MotivosCancelacion, "Motivos de cancelacion consultados.", id, clave, exacto, ct);

    private async Task<IActionResult> Fe(CatalogoFe cual, string mensaje, int id, string? clave, bool exacto, CancellationToken ct)
    {
        var data = await _service.ConsultarCatalogoFeAsync(cual, id, clave, exacto, ct);
        return Ok(new { ok = true, message = mensaje, data });
    }

    /// <summary>
    /// Marcas de producto.
    /// </summary>
    /// <param name="id">Una marca. Gana sobre <paramref name="ids"/>, igual que en Mac31.</param>
    /// <param name="ids">Varias marcas. Los ceros se descartan (ver el service).</param>
    /// <remarks>
    /// El filtro de texto busca por PREFIJO (LIKE 'texto%') cuando no se pide
    /// exacto: asi lo hace el SP, al contrario que la mayoria de los catalogos.
    /// </remarks>
    [HttpGet("marcas")]
    [Authorize(Policy = "perm:catalogos.marcas.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Marcas(
        [FromQuery] int id = 0,
        [FromQuery] int[]? ids = null,
        [FromQuery] int idStatus = 0,
        [FromQuery] string? texto = null,
        [FromQuery] bool exacto = false,
        CancellationToken ct = default)
    {
        var data = await _service.ConsultarMarcasAsync(id, ids, idStatus, texto, exacto, ct);
        return Ok(new { ok = true, message = "Marcas consultadas.", data });
    }
}
