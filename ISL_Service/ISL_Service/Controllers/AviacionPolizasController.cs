using ISL_Service.Application.DTOs.Aviacion;
using ISL_Service.Application.DTOs.EscriturasLegacy;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/*
  POLIZAS DE CHEQUES DE AVIACION.

  Cuatro permisos y no uno, porque son cuatro riesgos distintos:
    aviacion.polizas.ver        mirar las polizas y sus cheques
    aviacion.polizas.generar    GENERAR una poliza: emite los cheques
    aviacion.polizas.cancelar   cancelarla
    aviacion.polizas.editar     corregir un numero de cheque o el encabezado

  Generar una poliza es firmar los cheques con los que se paga a los
  permisionarios; corregir un numero de cheque es otra cosa y la hace quien
  imprime.

  NO VERIFICABLE EN ESTA MAQUINA: ninguno de los once sp_n_ esta en
  Produccion_svr y Constantes.AviacionDBServer esta vacia. Legacy falla igual.
*/
[ApiController]
[Route("api/aviacion/polizas")]
[Authorize]
public class AviacionPolizasController : PermisoControllerBase
{
    private readonly IAviacionPolizasService _service;

    private static readonly string[] Ver = { "aviacion.polizas.ver" };
    private static readonly string[] Generar = { "aviacion.polizas.generar" };
    private static readonly string[] Cancelar = { "aviacion.polizas.cancelar" };
    private static readonly string[] Editar = { "aviacion.polizas.editar" };

    public AviacionPolizasController(
        IAviacionPolizasService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    private IActionResult Body() => BadRequest(new { ok = false, message = "Body requerido." });

    private IActionResult Lista(string mensaje, List<Dictionary<string, object?>> data)
    {
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = mensaje, data, total = data.Count });
    }

    /// <summary>Las polizas. Todos los filtros se combinan.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConsultarAsync([FromQuery] AviacionPolizasFiltro filtro, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(Ver, ct);
        if (no is not null) return no;
        return Lista("Polizas consultadas.", await _service.ConsultarAsync(filtro, ct));
    }

    /// <summary>El detalle de una poliza.</summary>
    [HttpGet("{id:int}/detalle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConsultarDetalleAsync(int id, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(Ver, ct);
        if (no is not null) return no;
        return Lista("Detalle de la poliza.", await _service.ConsultarDetalleAsync(id, ct));
    }

    /// <summary>Los cheques de una poliza.</summary>
    [HttpGet("{id:int}/cheques")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConsultarChequesAsync(int id, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(Ver, ct);
        if (no is not null) return no;
        return Lista("Cheques de la poliza.", await _service.ConsultarChequesAsync(id, ct));
    }

    /// <summary>Los encabezados de poliza configurados.</summary>
    [HttpGet("encabezados")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConsultarEncabezadosAsync([FromQuery] int idPolizaEncabezado, [FromQuery] int idStatus, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(Ver, ct);
        if (no is not null) return no;
        return Lista("Encabezados de poliza.", await _service.ConsultarEncabezadosAsync(idPolizaEncabezado, idStatus, ct));
    }

    /// <summary>El reporte de ingresos de poliza.</summary>
    [HttpPost("reportes/ingresos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConsultarIngresosAsync([FromBody] AviacionPolizaReporteFiltro? filtro, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(Ver, ct);
        if (no is not null) return no;
        if (filtro is null) return Body();
        return Lista("Ingresos de poliza.", await _service.ConsultarIngresosAsync(filtro, ct));
    }

    /// <summary>El reporte de fianzas de poliza.</summary>
    [HttpPost("reportes/fianzas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConsultarFianzasAsync([FromBody] AviacionPolizaReporteFiltro? filtro, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(Ver, ct);
        if (no is not null) return no;
        if (filtro is null) return Body();
        return Lista("Fianzas de poliza.", await _service.ConsultarFianzasAsync(filtro, ct));
    }

    /// <summary>El reporte de documentos de poliza.</summary>
    [HttpPost("reportes/documentos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConsultarDocumentosAsync([FromBody] AviacionPolizaReporteFiltro? filtro, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(Ver, ct);
        if (no is not null) return no;
        if (filtro is null) return Body();
        return Lista("Documentos de poliza.", await _service.ConsultarDocumentosAsync(filtro, ct));
    }

    /// <summary>Genera una poliza de cheques.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearAsync([FromBody] CrearAviacionPolizaRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(Generar, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.CrearAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Poliza generada.", data });
    }

    /// <summary>Cancela una poliza.</summary>
    [HttpPut("{id:int}/cancelar")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelarAsync(int id, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(Cancelar, ct);
        if (no is not null) return no;

        var data = await _service.CancelarAsync(id, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Poliza cancelada.", data });
    }

    /// <summary>
    /// Guarda el encabezado de poliza.
    ///
    /// Sin id en la ruta a proposito: sp_n_ActualizarPolizaEncabezado no recibe
    /// ningun identificador de renglon, asi que la instalacion tiene uno y es ese
    /// el que se guarda. Poner un id en la ruta prometeria algo que la base no
    /// hace.
    /// </summary>
    [HttpPut("encabezado")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarEncabezadoAsync([FromBody] ActualizarAviacionPolizaEncabezadoRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(Editar, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.ActualizarEncabezadoAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Encabezado de poliza guardado.", data });
    }

    /// <summary>Corrige el numero de cheque y de poliza de un renglon.</summary>
    [HttpPut("cheques/{id:int}")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarChequeAsync(int id, [FromBody] ActualizarAviacionPolizaChequeRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(Editar, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.ActualizarChequeAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Cheque de la poliza corregido.", data });
    }
}
