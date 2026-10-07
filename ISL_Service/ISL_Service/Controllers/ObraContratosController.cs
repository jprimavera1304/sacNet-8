using ISL_Service.Application.DTOs.EscriturasLegacy;
using ISL_Service.Application.DTOs.ObraContratos;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/*
  CONTRATOS DE OBRA (MC): LA PANTALLA COMPLETA.

  La ola 3 ya sirve de lectura los grupos, los conceptos y las estimaciones de un
  contrato (GET /api/obra/contratos/grupos, /conceptos, /estimaciones) desde
  ObraController. Aqui esta todo lo demas: el contrato en si, los convenios, las
  partidas, los periodos, el acumulado de estimaciones por concepto, y TODAS las
  escrituras.

  POR QUE LA CONSULTA DE CONTRATOS CABE AQUI Y NO EN LA OLA 3
  -----------------------------------------------------------
  Porque en la ola 3 se cayo: su registro decia `escribe = si`, y lo decia porque
  Legacy llamaba a sp_n_ConsultaConstantes para leer `formatoFechaDB` y poder
  voltearle el dia y el mes a los filtros de fecha. Ese procedimiento CIERRA EL
  DIA del negocio. Aqui las fechas llegan tipadas (DateOnly) y se mandan en ISO,
  asi que no hace falta leer la constante y la consulta vuelve a ser una
  consulta de verdad: no escribe nada y no puede rodar la fecha de operacion de
  la empresa. Lo mismo vale para convenios, partidas y periodos.

  LOS PERMISOS
  ------------
  `obra.contratos.ver` ya existe (la sembro la ola 3). Se anaden:
    obra.contratos.crear     dar de alta un contrato
    obra.contratos.editar    modificarlo, y modificar sus grupos, conceptos,
                             partidas, periodos y estimaciones
    obra.contratos.cerrar    cerrar o reabrir un contrato o un convenio
    obra.convenios.ver/crear/editar
    obra.estimaciones.editar guardar las cantidades estimadas

  `obra.estimaciones.editar` es aparte de `obra.contratos.editar` a proposito:
  capturar cantidades estimadas es autorizar un pago, y no lo hace la misma
  persona que mantiene el catalogo de conceptos.

  LA FIRMA NO VIENE DEL BODY. Nunca.
*/
[ApiController]
[Route("api/obra")]
[Authorize]
public class ObraContratosController : PermisoControllerBase
{
    private readonly IObraContratosService _service;

    private static readonly string[] VerContratos = { "obra.contratos.ver" };
    private static readonly string[] CrearContratos = { "obra.contratos.crear" };
    private static readonly string[] EditarContratos = { "obra.contratos.editar" };
    private static readonly string[] CerrarContratos = { "obra.contratos.cerrar" };
    private static readonly string[] VerConvenios = { "obra.convenios.ver" };
    private static readonly string[] CrearConvenios = { "obra.convenios.crear" };
    private static readonly string[] EditarConvenios = { "obra.convenios.editar" };
    private static readonly string[] EditarEstimaciones = { "obra.estimaciones.editar" };

    public ObraContratosController(
        IObraContratosService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    private IActionResult Body() => BadRequest(new { ok = false, message = "Body requerido." });

    // ==================================================================
    // El contrato
    // ==================================================================

    /// <summary>
    /// Los contratos de obra.
    ///
    /// Todos los filtros se combinan. En Legacy, mandar el numero de contrato
    /// tiraba en silencio los otros ocho filtros.
    /// </summary>
    [HttpGet("contratos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarContratosAsync([FromQuery] ObraContratosFiltro filtro, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(VerContratos, ct);
        if (no is not null) return no;

        var data = await _service.ConsultarContratosAsync(filtro, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Contratos consultados.", data, total = data.Count });
    }

    /// <summary>Da de alta un contrato de obra.</summary>
    [HttpPost("contratos")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearContratoAsync([FromBody] CrearObraContratoRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(CrearContratos, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.CrearContratoAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Contrato dado de alta.", data });
    }

    /// <summary>Actualiza un contrato de obra.</summary>
    [HttpPut("contratos/{id:int}")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarContratoAsync(int id, [FromBody] ActualizarObraContratoRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(EditarContratos, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.ActualizarContratoAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Contrato actualizado.", data });
    }

    /// <summary>
    /// Cambia solo el desglose por partidas individuales del contrato.
    ///
    /// Es la operacion que Legacy colaba por la ruta de actualizar con un
    /// IDStatus = 3 que no era un status sino un modo. Aqui es su propia ruta, y
    /// el resto del contrato se reenvia tal como esta en la base en vez de
    /// quedar en blanco.
    /// </summary>
    [HttpPut("contratos/{id:int}/partidas-individuales")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarPartidasIndividualesAsync(int id, [FromBody] ActualizarObraContratoPartidasIndividualesRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(EditarContratos, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.ActualizarContratoPartidasIndividualesAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Desglose por partidas actualizado.", data });
    }

    /// <summary>Cierra o reabre un contrato de obra.</summary>
    [HttpPut("contratos/{id:int}/status")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarContratoStatusAsync(int id, [FromBody] ActualizarObraContratoStatusRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(CerrarContratos, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.ActualizarContratoStatusAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = request.ContratoCerrado ? "Contrato cerrado." : "Contrato reabierto.", data });
    }

    // ==================================================================
    // Convenios
    // ==================================================================

    /// <summary>Los convenios de obra.</summary>
    [HttpGet("convenios")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarConveniosAsync([FromQuery] ObraConveniosFiltro filtro, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(VerConvenios, ct);
        if (no is not null) return no;

        var data = await _service.ConsultarConveniosAsync(filtro, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Convenios consultados.", data, total = data.Count });
    }

    /// <summary>Da de alta un convenio sobre un contrato.</summary>
    [HttpPost("convenios")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearConvenioAsync([FromBody] CrearObraConvenioRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(CrearConvenios, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.CrearConvenioAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Convenio dado de alta.", data });
    }

    /// <summary>Actualiza un convenio.</summary>
    [HttpPut("convenios/{id:int}")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarConvenioAsync(int id, [FromBody] ActualizarObraConvenioRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(EditarConvenios, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.ActualizarConvenioAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Convenio actualizado.", data });
    }

    /// <summary>Cierra o reabre un convenio.</summary>
    [HttpPut("convenios/{id:int}/status")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarConvenioStatusAsync(int id, [FromBody] ActualizarObraConvenioStatusRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(CerrarContratos, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.ActualizarConvenioStatusAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = request.ConvenioCerrado ? "Convenio cerrado." : "Convenio reabierto.", data });
    }

    // ==================================================================
    // Grupos del catalogo de conceptos
    // ==================================================================

    /// <summary>Da de alta un grupo (capitulo) del catalogo de conceptos.</summary>
    [HttpPost("contratos/grupos")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearGrupoAsync([FromBody] CrearObraContratoGrupoRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(EditarContratos, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.CrearGrupoAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Grupo dado de alta.", data });
    }

    /// <summary>Actualiza un grupo del catalogo de conceptos.</summary>
    [HttpPut("contratos/grupos/{id:int}")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarGrupoAsync(int id, [FromBody] ActualizarObraContratoGrupoRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(EditarContratos, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.ActualizarGrupoAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Grupo actualizado.", data });
    }

    /// <summary>
    /// Mueve un grupo una posicion arriba o abajo.
    ///
    /// `idContrato` es requerido: el procedimiento lo usa para encontrar al
    /// vecino con el que intercambia la posicion. En Legacy no se exigia, y sin
    /// el quedaban dos grupos con el mismo orden.
    /// </summary>
    [HttpPut("contratos/grupos/{id:int}/orden")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReordenarGrupoAsync(int id, [FromBody] ReordenarRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(EditarContratos, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.ReordenarGrupoAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Grupo movido.", data });
    }

    // ==================================================================
    // Conceptos
    // ==================================================================

    /// <summary>
    /// El acumulado de estimaciones por concepto de una estimacion.
    /// Era POST /api/mccontratos/conceptos/estimaciones/consultar.
    /// </summary>
    [HttpGet("contratos/conceptos/estimaciones")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarConceptosEstimacionesAsync([FromQuery] ObraConceptosEstimacionesFiltro filtro, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(VerContratos, ct);
        if (no is not null) return no;

        var data = await _service.ConsultarConceptosEstimacionesAsync(filtro, ct);
        return Ok(new { ok = true, message = "Acumulado de estimaciones consultado.", data, total = data.Count });
    }

    /// <summary>Da de alta un concepto del catalogo de un contrato.</summary>
    [HttpPost("contratos/conceptos")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearConceptoAsync([FromBody] CrearObraContratoConceptoRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(EditarContratos, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.CrearConceptoAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Concepto dado de alta.", data });
    }

    /// <summary>Actualiza un concepto del catalogo de un contrato.</summary>
    [HttpPut("contratos/conceptos/{id:int}")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarConceptoAsync(int id, [FromBody] ActualizarObraContratoConceptoRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(EditarContratos, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.ActualizarConceptoAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Concepto actualizado.", data });
    }

    /// <summary>Mueve un concepto una posicion arriba o abajo dentro de su grupo.</summary>
    [HttpPut("contratos/conceptos/{id:int}/orden")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReordenarConceptoAsync(int id, [FromBody] ReordenarRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(EditarContratos, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.ReordenarConceptoAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Concepto movido.", data });
    }

    /// <summary>
    /// Da de baja un concepto del catalogo (IDStatus = 2) y reordena los que le
    /// siguen.
    ///
    /// Baja logica: el renglon se queda con su clave, su descripcion, su precio
    /// y su cantidad. En Legacy esta misma ruta los BORRABA y dejaba el concepto
    /// en un status que no existe; el detalle esta en ObraContratosService.
    /// </summary>
    [HttpPut("contratos/conceptos/{id:int}/baja")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EliminarConceptoAsync(int id, [FromQuery] int idContrato, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(EditarContratos, ct);
        if (no is not null) return no;

        var data = await _service.EliminarConceptoAsync(id, idContrato, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Concepto dado de baja.", data });
    }

    /// <summary>Carga el catalogo de conceptos de un contrato desde un archivo.</summary>
    [HttpPost("contratos/conceptos/archivo")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CargarConceptosAsync([FromBody] CargarObraContratoConceptosRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(EditarContratos, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.CargarConceptosDeArchivoAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Catalogo de conceptos cargado.", data });
    }

    /// <summary>
    /// Guarda las cantidades estimadas de los conceptos de una estimacion.
    /// Permiso propio: capturar cantidades estimadas es autorizar un pago.
    /// </summary>
    [HttpPut("contratos/conceptos/estimaciones")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarConceptoEstimacionAsync([FromBody] ActualizarObraConceptoEstimacionRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(EditarEstimaciones, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.ActualizarConceptoEstimacionAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Cantidades estimadas guardadas.", data });
    }

    // ==================================================================
    // Partidas del contrato
    // ==================================================================

    /// <summary>Las partidas presupuestales de un contrato.</summary>
    [HttpGet("contratos/partidas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarContratoPartidasAsync([FromQuery] ObraContratoPartidasFiltro filtro, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(VerContratos, ct);
        if (no is not null) return no;

        var data = await _service.ConsultarContratoPartidasAsync(filtro, ct);
        return Ok(new { ok = true, message = "Partidas del contrato consultadas.", data, total = data.Count });
    }

    /// <summary>Agrega una partida presupuestal a un contrato.</summary>
    [HttpPost("contratos/partidas")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearContratoPartidaAsync([FromBody] CrearObraContratoPartidaRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(EditarContratos, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.CrearContratoPartidaAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Partida agregada al contrato.", data });
    }

    /// <summary>Actualiza una partida presupuestal de un contrato.</summary>
    [HttpPut("contratos/partidas/{id:int}")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarContratoPartidaAsync(int id, [FromBody] ActualizarObraContratoPartidaRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(EditarContratos, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.ActualizarContratoPartidaAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Partida del contrato actualizada.", data });
    }

    /// <summary>Mueve una partida del contrato una posicion arriba o abajo.</summary>
    [HttpPut("contratos/partidas/{id:int}/orden")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReordenarContratoPartidaAsync(int id, [FromBody] ReordenarRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(EditarContratos, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.ReordenarContratoPartidaAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Partida movida.", data });
    }

    // ==================================================================
    // Periodos
    // ==================================================================

    /// <summary>Los periodos del programa de obra de un contrato.</summary>
    [HttpGet("contratos/periodos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarPeriodosAsync([FromQuery] ObraContratoPeriodosFiltro filtro, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(VerContratos, ct);
        if (no is not null) return no;

        var data = await _service.ConsultarPeriodosAsync(filtro, ct);
        return Ok(new { ok = true, message = "Periodos consultados.", data, total = data.Count });
    }

    /// <summary>Da de alta un periodo del programa de obra.</summary>
    [HttpPost("contratos/periodos")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearPeriodoAsync([FromBody] CrearObraContratoPeriodoRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(EditarContratos, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.CrearPeriodoAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Periodo dado de alta.", data });
    }

    /// <summary>Actualiza un periodo del programa de obra.</summary>
    [HttpPut("contratos/periodos/{id:int}")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarPeriodoAsync(int id, [FromBody] ActualizarObraContratoPeriodoRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(EditarContratos, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.ActualizarPeriodoAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Periodo actualizado.", data });
    }

    // ==================================================================
    // Estimaciones
    // ==================================================================

    /// <summary>Da de alta una estimacion de un contrato.</summary>
    [HttpPost("contratos/estimaciones")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearEstimacionAsync([FromBody] CrearObraContratoEstimacionRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(EditarEstimaciones, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.CrearEstimacionAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Estimacion dada de alta.", data });
    }

    /// <summary>Actualiza una estimacion de un contrato.</summary>
    [HttpPut("contratos/estimaciones/{id:int}")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarEstimacionAsync(int id, [FromBody] ActualizarObraContratoEstimacionRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(EditarEstimaciones, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.ActualizarEstimacionAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Estimacion actualizada.", data });
    }
}
