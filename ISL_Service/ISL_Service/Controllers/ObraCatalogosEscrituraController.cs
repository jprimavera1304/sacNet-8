using ISL_Service.Application.DTOs.EscriturasLegacy;
using ISL_Service.Application.DTOs.ObraCatalogos;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/*
  ALTAS Y MODIFICACIONES DE LOS CATALOGOS DEL MODULO DE OBRA (MC).

  Las LECTURAS de estos mismos catalogos las sirve ObraController
  (GET /api/obra/catalogos/...). Esto es la otra mitad: lo que escribe.

  POR QUE UN CONTROLADOR APARTE Y NO ACCIONES DENTRO DEL DE LECTURA
  -----------------------------------------------------------------
  Porque necesita la firma del usuario de Mac31 (@IDUsuario, @Equipo) y por
  tanto PermisoControllerBase, mientras que el de lectura se arregla con una
  politica declarativa. Partirlo asi deja el candado de cada mitad donde se lee
  mejor y no obliga a tocar el archivo de la otra.

  LOS PERMISOS SON DOS, NO UNO
  ----------------------------
  `obra.catalogos.crear` y `obra.catalogos.editar`, aparte del
  `obra.catalogos.ver` que ya sembro la ola 3. Dar de alta un tipo de obra y
  cambiarle el nombre a uno que ya esta usado por contratos firmados no son el
  mismo riesgo.

  Para proveedores y partidas hay claves propias: el renglon de proveedor trae
  RFC, CURP, telefono y correo (dato personal) y el de partida trae el monto
  autorizado del presupuesto.

  LA FIRMA NO VIENE DEL BODY
  --------------------------
  Legacy pedia IDUsuario y Equipo en el cuerpo de la peticion
  (MCPuestoInputDTO.IDUsuario). Si viajara en el body, cualquiera podria dar de
  alta a nombre de otro. Sale de IdUsuarioLegacy() y Equipo().
*/
[ApiController]
[Route("api/obra/catalogos")]
[Authorize]
public class ObraCatalogosEscrituraController : PermisoControllerBase
{
    private readonly IObraCatalogosEscrituraService _service;

    private static readonly string[] PermisoCrear = { "obra.catalogos.crear" };
    private static readonly string[] PermisoEditar = { "obra.catalogos.editar" };
    private static readonly string[] PermisoPartidasVer = { "obra.partidas.ver" };
    private static readonly string[] PermisoPartidasCrear = { "obra.partidas.crear" };
    private static readonly string[] PermisoPartidasEditar = { "obra.partidas.editar" };
    private static readonly string[] PermisoProveedoresCrear = { "obra.proveedores.crear" };
    private static readonly string[] PermisoProveedoresEditar = { "obra.proveedores.editar" };

    public ObraCatalogosEscrituraController(
        IObraCatalogosEscrituraService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    // ==================================================================
    // Tipos de contratacion
    // ==================================================================

    /// <summary>Da de alta un tipo de contratacion de obra.</summary>
    /// <response code="200">El tipo quedo dado de alta; `data.id` es el nuevo id.</response>
    /// <response code="400">Falta el nombre o es mas largo que la columna.</response>
    /// <response code="409">El procedimiento de Mac31 no pudo guardarlo.</response>
    [HttpPost("tipos-contratacion")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearTipoContratacionAsync([FromBody] CrearObraCatalogoSimpleRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(PermisoCrear, ct);
        if (no is not null) return no;
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });

        var data = await _service.CrearTipoContratacionAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Tipo de contratacion dado de alta.", data });
    }

    /// <summary>Actualiza un tipo de contratacion de obra.</summary>
    [HttpPut("tipos-contratacion/{id:int}")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarTipoContratacionAsync(int id, [FromBody] ActualizarObraCatalogoSimpleRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(PermisoEditar, ct);
        if (no is not null) return no;
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });

        var data = await _service.ActualizarTipoContratacionAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Tipo de contratacion actualizado.", data });
    }

    // ==================================================================
    // Tipos de documento
    // ==================================================================

    /// <summary>Da de alta un tipo de documento de obra.</summary>
    [HttpPost("tipos-documento")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearTipoDocumentoAsync([FromBody] CrearObraCatalogoSimpleRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(PermisoCrear, ct);
        if (no is not null) return no;
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });

        var data = await _service.CrearTipoDocumentoAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Tipo de documento dado de alta.", data });
    }

    /// <summary>Actualiza un tipo de documento de obra.</summary>
    [HttpPut("tipos-documento/{id:int}")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarTipoDocumentoAsync(int id, [FromBody] ActualizarObraCatalogoSimpleRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(PermisoEditar, ct);
        if (no is not null) return no;
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });

        var data = await _service.ActualizarTipoDocumentoAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Tipo de documento actualizado.", data });
    }

    // ==================================================================
    // Tipos de obra
    // ==================================================================

    /// <summary>Da de alta un tipo de obra.</summary>
    [HttpPost("tipos-obra")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearTipoObraAsync([FromBody] CrearObraCatalogoSimpleRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(PermisoCrear, ct);
        if (no is not null) return no;
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });

        var data = await _service.CrearTipoObraAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Tipo de obra dado de alta.", data });
    }

    /// <summary>Actualiza un tipo de obra.</summary>
    [HttpPut("tipos-obra/{id:int}")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarTipoObraAsync(int id, [FromBody] ActualizarObraCatalogoSimpleRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(PermisoEditar, ct);
        if (no is not null) return no;
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });

        var data = await _service.ActualizarTipoObraAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Tipo de obra actualizado.", data });
    }

    // ==================================================================
    // Tipos de recurso
    // ==================================================================

    /// <summary>Da de alta un tipo de recurso de obra.</summary>
    [HttpPost("tipos-recurso")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearTipoRecursoAsync([FromBody] CrearObraCatalogoSimpleRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(PermisoCrear, ct);
        if (no is not null) return no;
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });

        var data = await _service.CrearTipoRecursoAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Tipo de recurso dado de alta.", data });
    }

    /// <summary>Actualiza un tipo de recurso de obra.</summary>
    [HttpPut("tipos-recurso/{id:int}")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarTipoRecursoAsync(int id, [FromBody] ActualizarObraCatalogoSimpleRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(PermisoEditar, ct);
        if (no is not null) return no;
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });

        var data = await _service.ActualizarTipoRecursoAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Tipo de recurso actualizado.", data });
    }

    // ==================================================================
    // Puestos
    // ==================================================================

    /// <summary>Da de alta un puesto de obra.</summary>
    [HttpPost("puestos")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearPuestoAsync([FromBody] CrearObraPuestoRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(PermisoCrear, ct);
        if (no is not null) return no;
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });

        var data = await _service.CrearPuestoAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Puesto dado de alta.", data });
    }

    /// <summary>Actualiza un puesto de obra.</summary>
    [HttpPut("puestos/{id:int}")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarPuestoAsync(int id, [FromBody] ActualizarObraPuestoRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(PermisoEditar, ct);
        if (no is not null) return no;
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });

        var data = await _service.ActualizarPuestoAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Puesto actualizado.", data });
    }

    // ==================================================================
    // Residentes
    // ==================================================================

    /// <summary>Da de alta un residente de obra.</summary>
    [HttpPost("residentes")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearResidenteAsync([FromBody] CrearObraResidenteRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(PermisoCrear, ct);
        if (no is not null) return no;
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });

        var data = await _service.CrearResidenteAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Residente dado de alta.", data });
    }

    /// <summary>Actualiza un residente de obra.</summary>
    [HttpPut("residentes/{id:int}")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarResidenteAsync(int id, [FromBody] ActualizarObraResidenteRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(PermisoEditar, ct);
        if (no is not null) return no;
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });

        var data = await _service.ActualizarResidenteAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Residente actualizado.", data });
    }

    // ==================================================================
    // Gerentes
    // ==================================================================

    /// <summary>Da de alta un gerente de obra.</summary>
    [HttpPost("gerentes")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearGerenteAsync([FromBody] CrearObraGerenteRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(PermisoCrear, ct);
        if (no is not null) return no;
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });

        var data = await _service.CrearGerenteAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Gerente dado de alta.", data });
    }

    /// <summary>Actualiza un gerente de obra.</summary>
    [HttpPut("gerentes/{id:int}")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarGerenteAsync(int id, [FromBody] ActualizarObraGerenteRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(PermisoEditar, ct);
        if (no is not null) return no;
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });

        var data = await _service.ActualizarGerenteAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Gerente actualizado.", data });
    }

    // ==================================================================
    // Unidades de medida
    // ==================================================================

    /// <summary>Da de alta una unidad de medida de obra.</summary>
    [HttpPost("unidades-medida")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearUnidadMedidaAsync([FromBody] CrearObraUnidadMedidaRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(PermisoCrear, ct);
        if (no is not null) return no;
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });

        var data = await _service.CrearUnidadMedidaAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Unidad de medida dada de alta.", data });
    }

    /// <summary>Actualiza una unidad de medida de obra.</summary>
    [HttpPut("unidades-medida/{id:int}")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarUnidadMedidaAsync(int id, [FromBody] ActualizarObraUnidadMedidaRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(PermisoEditar, ct);
        if (no is not null) return no;
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });

        var data = await _service.ActualizarUnidadMedidaAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Unidad de medida actualizada.", data });
    }

    // ==================================================================
    // Partidas presupuestales
    // ==================================================================

    /// <summary>
    /// Las partidas presupuestales de obra.
    ///
    /// Era POST /api/mcpartidas/consultar en Legacy, porque alla el token iba
    /// dentro del JSON. Aqui el token va en la cabecera, asi que una consulta
    /// vuelve a ser un GET.
    /// </summary>
    /// <param name="idPartida">0 = todas.</param>
    /// <param name="idStatus">0 = todos, 1 = activas, 2 = inactivas.</param>
    /// <param name="partida">Filtro por clave de partida.</param>
    /// <param name="exacto">true = la clave tiene que coincidir completa.</param>
    [HttpGet("partidas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarPartidasAsync(
        [FromQuery] int idPartida,
        [FromQuery] int idStatus,
        [FromQuery] string? partida,
        [FromQuery] bool exacto,
        CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(PermisoPartidasVer, ct);
        if (no is not null) return no;

        var data = await _service.ConsultarPartidasAsync(
            new ObraPartidasFiltro { IdPartida = idPartida, IdStatus = idStatus, Partida = partida, Exacto = exacto }, ct);
        return Ok(new { ok = true, message = "Partidas consultadas.", data, total = data.Count });
    }

    /// <summary>Da de alta una partida presupuestal de obra.</summary>
    [HttpPost("partidas")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearPartidaAsync([FromBody] CrearObraPartidaRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(PermisoPartidasCrear, ct);
        if (no is not null) return no;
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });

        var data = await _service.CrearPartidaAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Partida dada de alta.", data });
    }

    /// <summary>Actualiza una partida presupuestal de obra.</summary>
    [HttpPut("partidas/{id:int}")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarPartidaAsync(int id, [FromBody] ActualizarObraPartidaRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(PermisoPartidasEditar, ct);
        if (no is not null) return no;
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });

        var data = await _service.ActualizarPartidaAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Partida actualizada.", data });
    }

    // ==================================================================
    // Padron de proveedores de obra
    // ==================================================================

    /// <summary>
    /// Da de alta un proveedor (contratista) en el padron de obra.
    /// Permiso propio: el renglon trae RFC, CURP, telefono y correo.
    /// </summary>
    [HttpPost("/api/obra/proveedores")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearProveedorAsync([FromBody] CrearObraProveedorRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(PermisoProveedoresCrear, ct);
        if (no is not null) return no;
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });

        var data = await _service.CrearProveedorAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Proveedor de obra dado de alta.", data });
    }

    /// <summary>Actualiza un proveedor del padron de obra.</summary>
    [HttpPut("/api/obra/proveedores/{id:int}")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarProveedorAsync(int id, [FromBody] ActualizarObraProveedorRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(PermisoProveedoresEditar, ct);
        if (no is not null) return no;
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });

        var data = await _service.ActualizarProveedorAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Proveedor de obra actualizado.", data });
    }
}
