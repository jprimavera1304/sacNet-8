using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// La consulta del padron de proveedores: el GET raiz de api/proveedores, que
/// faltaba.
///
/// POR QUE UNA CLASE APARTE Y NO DENTRO DE ProveedoresAnexosController
/// -------------------------------------------------------------------
/// Tres clases comparten el prefijo api/proveedores: Admin (escrituras), Anexos
/// (las pestanas: apoderados, domicilios, documentos) y esta (el buscador).
/// El GET raiz no es un anexo del proveedor: es la lista de proveedores, o sea
/// lo que hay ANTES de tener uno. Meterlo en Anexos pondria la busqueda al lado
/// de los documentos de un proveedor concreto y quien venga a leer el buscador
/// tendria que pasar por tres consultas que no son suyas.
///
/// LA RUTA NO CHOCA. Antes de esta clase `GET api/proveedores` contestaba 405
/// (method not allowed): la ruta base existia, publicada por Admin y Anexos,
/// pero sin ningun GET en la raiz. Los dos atributos de ruta de aquellas clases
/// son todos con sufijo ("{id:int}", "apoderados", ...), asi que la raiz estaba
/// libre.
///
/// REUSA `proveedores.ver`, que ya existe y significa exactamente esto.
/// </summary>
[ApiController]
[Route("api/proveedores")]
[Authorize]
public class ProveedoresConsultaController : ControllerBase
{
    private readonly IProveedoresConsultaService _service;

    public ProveedoresConsultaController(IProveedoresConsultaService service)
    {
        _service = service;
    }

    /// <summary>
    /// Busca proveedores.
    /// </summary>
    /// <param name="idStatus">0 = todos, 1 = activos (default), 2 = inactivos.</param>
    /// <param name="idDescuento">
    /// Se acepta por simetria con la firma del SP, pero EL PROCEDIMIENTO NO LO
    /// USA: lo declara y nunca lo mete en su WHERE. No filtra aqui ni en Mac31.
    /// </param>
    /// <param name="exacto">true = igualdad; false = LIKE.</param>
    /// <response code="200">Proveedores consultados</response>
    [HttpGet]
    [Authorize(Policy = "perm:proveedores.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Consultar(
        [FromQuery] int idProveedor = 0,
        [FromQuery] string? numero = null,
        [FromQuery] string? nombre = null,
        [FromQuery] string? apellidoPaterno = null,
        [FromQuery] string? apellidoMaterno = null,
        [FromQuery] string? rfc = null,
        [FromQuery] int idStatus = 1,
        [FromQuery] int idCondicionesCredito = 0,
        [FromQuery] int idDescuento = 0,
        [FromQuery] bool exacto = false,
        CancellationToken ct = default)
    {
        var data = await _service.ConsultarAsync(new ProveedoresConsultaFiltro
        {
            IDProveedor = idProveedor,
            Numero = numero,
            Nombre = nombre,
            ApellidoPaterno = apellidoPaterno,
            ApellidoMaterno = apellidoMaterno,
            Rfc = rfc,
            IDStatus = idStatus,
            IDCondicionesCredito = idCondicionesCredito,
            IDDescuento = idDescuento,
            Exacto = exacto
        }, ct);

        // El renglon trae limite de credito del proveedor: no se guarda en
        // ninguna cache intermedia.
        Response.Headers["Cache-Control"] = "no-store";

        return Ok(new { ok = true, message = "Proveedores consultados.", data, total = data.Count });
    }
}
