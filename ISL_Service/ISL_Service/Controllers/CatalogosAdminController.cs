using ISL_Service.Application.DTOs.CatalogosAdmin;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Escrituras de los catalogos de Mac31: altas, modificaciones y bajas.
///
/// POR QUE CUELGA DE api/catalogos Y NO DE UN PREFIJO NUEVO
/// -------------------------------------------------------
/// Las LECTURAS de estos mismos catalogos ya viven en api/catalogos (ola 1 y
/// ola 3) y son GET. Aqui solo hay POST, PUT y los dos cambios de estatus, asi
/// que no hay ninguna ruta repetida y el front ve un solo recurso: el catalogo
/// de agentes es api/catalogos/agentes, se lea o se escriba. Un prefijo aparte
/// obligaria a la pantalla a saber dos direcciones para la misma cosa.
///
/// LA BAJA ES POST {id}/inhabilitar Y NO DELETE
/// -------------------------------------------
/// Porque no borra: pone IDStatus = 2. Un DELETE que no borra es una mentira en
/// el contrato HTTP, y legacy ya la contaba — su DELETE llamaba al mismo
/// UPDATE y, como el SP pisa todas las columnas, dejaba el renglon con el
/// nombre vacio. Aqui la baja no pide cuerpo: el backend lee los valores
/// actuales y solo cambia el estatus. Y hay habilitar, porque la puerta tiene
/// que abrir en los dos sentidos: una baja sin vuelta es un borrado con otro
/// nombre.
///
/// TRES CATALOGOS NO TIENEN BAJA: ciudades, municipios y colonias. Sus tablas
/// no tienen IDStatus. En legacy el DELETE sobre ellas solo vaciaba el nombre.
/// </summary>
[ApiController]
[Route("api/catalogos")]
[Authorize]
public partial class CatalogosAdminController : PermisoControllerBase
{
    /// <summary>1 = Activo, 2 = Cancelado. La convencion de toda la base nueva.</summary>
    private const int StatusActivo = 1;
    private const int StatusCancelado = 2;

    private static readonly string[] PermisosCrear = { "catalogos.crear" };
    private static readonly string[] PermisosEditar = { "catalogos.editar" };
    private static readonly string[] PermisosBaja = { "catalogos.baja" };

    // Marcas, categorias y grupos: es el catalogo de productos. Permiso propio
    // porque mover una marca de columna cambia los reportes de concentrado de
    // toda la empresa.
    private static readonly string[] PermisosMarcasCrear = { "catalogos.marcas.crear" };
    private static readonly string[] PermisosMarcasEditar = { "catalogos.marcas.editar" };
    private static readonly string[] PermisosMarcasBaja = { "catalogos.marcas.baja" };

    // Galleros: el renglon trae saldo, que es dinero. Mismo criterio que la
    // ola 1 uso para la lectura (catalogos.galleros.ver).
    private static readonly string[] PermisosGallerosCrear = { "catalogos.galleros.crear" };
    private static readonly string[] PermisosGallerosEditar = { "catalogos.galleros.editar" };
    private static readonly string[] PermisosGallerosBaja = { "catalogos.galleros.baja" };

    // Empresas de Mac31 y sus series: RFC, regimen fiscal, cuenta bancaria y el
    // folio con el que se factura. No es un desplegable, es la identidad fiscal.
    private static readonly string[] PermisosEmpresasCrear = { "catalogos.empresas.crear" };
    private static readonly string[] PermisosEmpresasEditar = { "catalogos.empresas.editar" };
    private static readonly string[] PermisosEmpresasBaja = { "catalogos.empresas.baja" };

    // Territorial: estados, ciudades, municipios, colonias, unidades
    // territoriales y centros de servicio. Permiso aparte porque un cambio aqui
    // le mueve el domicilio a clientes y proveedores que ya estaban capturados.
    private static readonly string[] PermisosTerritorialCrear = { "catalogos.territorial.crear" };
    private static readonly string[] PermisosTerritorialEditar = { "catalogos.territorial.editar" };

    // Autos: trae poliza y monto asegurado.
    private static readonly string[] PermisosAutosCrear = { "catalogos.autos.crear" };
    private static readonly string[] PermisosAutosEditar = { "catalogos.autos.editar" };
    private static readonly string[] PermisosAutosBaja = { "catalogos.autos.baja" };

    private readonly ICatalogosAdminService _service;

    public CatalogosAdminController(
        ICatalogosAdminService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    // -----------------------------------------------------------------------
    // Como se contesta
    // -----------------------------------------------------------------------

    /// <summary>
    /// Traduce el resultado de una escritura de catalogo a HTTP.
    ///
    /// 200 con ok=false cuando legacy rechazo por una REGLA DE NEGOCIO (el
    /// nombre repetido). No es 409 a proposito: el front de estos catalogos
    /// muestra el texto de legacy al lado del campo, y un 4xx lo manda al
    /// manejador de errores global, donde ese texto se pierde. El sobre
    /// { ok, message } ya dice si salio o no.
    /// </summary>
    private IActionResult Responder(CatalogoEscrituraResponse r, string mensajeExito)
        => Ok(new
        {
            ok = r.Guardo,
            message = r.Guardo ? mensajeExito : (string.IsNullOrWhiteSpace(r.Mensaje) ? "La base rechazo la operacion." : r.Mensaje),
            data = r
        });

    /// <summary>Lo mismo, pero con el 404 cuando el renglon no existe.</summary>
    private IActionResult Responder(CatalogoEscrituraResponse? r, string mensajeExito, string noExiste)
        => r is null
            ? NotFound(new { ok = false, message = noExiste })
            : Responder(r, mensajeExito);

    /// <summary>Alta: 201 cuando guardo, 200 con ok=false cuando legacy la rechazo.</summary>
    private IActionResult RespondeAlta(CatalogoEscrituraResponse r, string mensajeExito)
        => r.Guardo
            ? StatusCode(StatusCodes.Status201Created, new { ok = true, message = mensajeExito, data = r })
            : Responder(r, mensajeExito);

    /// <summary>
    /// El nombre de un catalogo: requerido y con tope. El tope es el del
    /// varchar del SP; sin el, SQL Server recorta en silencio y el usuario cree
    /// que guardo lo que escribio.
    /// </summary>
    private static object? ValidaNombre(string? valor, int maximo, string campo = "nombre")
    {
        if (string.IsNullOrWhiteSpace(valor))
            return new { ok = false, message = $"El {campo} es requerido." };
        if (valor.Trim().Length > maximo)
            return new { ok = false, message = $"El {campo} no puede pasar de {maximo} caracteres." };
        return null;
    }

    private static object? ValidaId(int id, string campo)
        => id <= 0 ? new { ok = false, message = $"El {campo} es requerido." } : null;

    // =======================================================================
    // Agentes
    // =======================================================================

    /// <summary>Alta de agente.</summary>
    /// <response code="201">Agente dado de alta</response>
    /// <response code="400">Datos invalidos</response>
    [HttpPost("agentes")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearAgente([FromBody] CrearAgenteRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.NombreAgente, 250, "nombre del agente") ?? ValidaId(request.NumeroAgente, "numero de agente");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        return RespondeAlta(await _service.CrearAgenteAsync(request, ct), "Agente dado de alta.");
    }

    /// <summary>Modificacion de agente.</summary>
    [HttpPut("agentes/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarAgente([FromRoute] int id, [FromBody] ActualizarAgenteRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.NombreAgente, 250, "nombre del agente") ?? ValidaId(request.NumeroAgente, "numero de agente");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await _service.ActualizarAgenteAsync(id, request, ct), "Agente actualizado.");
    }

    /// <summary>Baja logica del agente: IDStatus = 2, lo demas se conserva.</summary>
    /// <response code="404">El agente no existe</response>
    [HttpPost("agentes/{id:int}/inhabilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InhabilitarAgente([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await _service.CambiarStatusAgenteAsync(id, StatusCancelado, ct),
            "Agente inhabilitado.", "El agente no existe.");
    }

    /// <summary>Vuelve a activar el agente.</summary>
    [HttpPost("agentes/{id:int}/habilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HabilitarAgente([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await _service.CambiarStatusAgenteAsync(id, StatusActivo, ct),
            "Agente habilitado.", "El agente no existe.");
    }

    // =======================================================================
    // Almacenes
    // =======================================================================

    [HttpPost("almacenes")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearAlmacen([FromBody] CatalogoNombreRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.Nombre, 250);
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        return RespondeAlta(await _service.CrearAlmacenAsync(request, ct), "Almacen dado de alta.");
    }

    [HttpPut("almacenes/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarAlmacen([FromRoute] int id, [FromBody] CatalogoNombreUpdateRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.Nombre, 250);
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await _service.ActualizarAlmacenAsync(id, request, ct), "Almacen actualizado.");
    }

    [HttpPost("almacenes/{id:int}/inhabilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InhabilitarAlmacen([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusAlmacenAsync(id, StatusCancelado, ct),
            "Almacen inhabilitado.", "El almacen no existe.");
    }

    [HttpPost("almacenes/{id:int}/habilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HabilitarAlmacen([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusAlmacenAsync(id, StatusActivo, ct),
            "Almacen habilitado.", "El almacen no existe.");
    }

    // =======================================================================
    // Bancos
    // =======================================================================

    [HttpPost("bancos")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearBanco([FromBody] CatalogoNombreRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.Nombre, 250);
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        return RespondeAlta(await _service.CrearBancoAsync(request, ct), "Banco dado de alta.");
    }

    [HttpPut("bancos/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarBanco([FromRoute] int id, [FromBody] CatalogoNombreUpdateRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.Nombre, 250);
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await _service.ActualizarBancoAsync(id, request, ct), "Banco actualizado.");
    }

    [HttpPost("bancos/{id:int}/inhabilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InhabilitarBanco([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusBancoAsync(id, StatusCancelado, ct),
            "Banco inhabilitado.", "El banco no existe.");
    }

    [HttpPost("bancos/{id:int}/habilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HabilitarBanco([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusBancoAsync(id, StatusActivo, ct),
            "Banco habilitado.", "El banco no existe.");
    }

    // =======================================================================
    // Unidades de medida
    // =======================================================================

    [HttpPost("unidades-medida")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearUnidadMedida([FromBody] CatalogoNombreRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.Nombre, 250);
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        return RespondeAlta(await _service.CrearUnidadMedidaAsync(request, ct), "Unidad de medida dada de alta.");
    }

    [HttpPut("unidades-medida/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarUnidadMedida([FromRoute] int id, [FromBody] CatalogoNombreUpdateRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.Nombre, 250);
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await _service.ActualizarUnidadMedidaAsync(id, request, ct), "Unidad de medida actualizada.");
    }

    [HttpPost("unidades-medida/{id:int}/inhabilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InhabilitarUnidadMedida([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusUnidadMedidaAsync(id, StatusCancelado, ct),
            "Unidad de medida inhabilitada.", "La unidad de medida no existe.");
    }

    [HttpPost("unidades-medida/{id:int}/habilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HabilitarUnidadMedida([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusUnidadMedidaAsync(id, StatusActivo, ct),
            "Unidad de medida habilitada.", "La unidad de medida no existe.");
    }

    // =======================================================================
    // Tipos de gasto
    // =======================================================================

    [HttpPost("tipos-gasto")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearTipoGasto([FromBody] CatalogoNombreRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.Nombre, 250);
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        return RespondeAlta(await _service.CrearTipoGastoAsync(request, IdUsuarioLegacy(), Equipo(), ct),
            "Tipo de gasto dado de alta.");
    }

    [HttpPut("tipos-gasto/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarTipoGasto([FromRoute] int id, [FromBody] CatalogoNombreUpdateRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.Nombre, 250);
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await _service.ActualizarTipoGastoAsync(id, request, IdUsuarioLegacy(), Equipo(), ct),
            "Tipo de gasto actualizado.");
    }

    [HttpPost("tipos-gasto/{id:int}/inhabilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InhabilitarTipoGasto([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusTipoGastoAsync(id, StatusCancelado, IdUsuarioLegacy(), Equipo(), ct),
            "Tipo de gasto inhabilitado.", "El tipo de gasto no existe.");
    }

    [HttpPost("tipos-gasto/{id:int}/habilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HabilitarTipoGasto([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusTipoGastoAsync(id, StatusActivo, IdUsuarioLegacy(), Equipo(), ct),
            "Tipo de gasto habilitado.", "El tipo de gasto no existe.");
    }

    // =======================================================================
    // Tipos de domicilio
    // =======================================================================

    [HttpPost("tipos-domicilio")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearTipoDomicilio([FromBody] CatalogoNombreRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.Nombre, 400);
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        return RespondeAlta(await _service.CrearTipoDomicilioAsync(request, IdUsuarioLegacy(), Equipo(), ct),
            "Tipo de domicilio dado de alta.");
    }

    [HttpPut("tipos-domicilio/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarTipoDomicilio([FromRoute] int id, [FromBody] CatalogoNombreUpdateRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.Nombre, 400);
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await _service.ActualizarTipoDomicilioAsync(id, request, IdUsuarioLegacy(), Equipo(), ct),
            "Tipo de domicilio actualizado.");
    }

    [HttpPost("tipos-domicilio/{id:int}/inhabilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InhabilitarTipoDomicilio([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusTipoDomicilioAsync(id, StatusCancelado, IdUsuarioLegacy(), Equipo(), ct),
            "Tipo de domicilio inhabilitado.", "El tipo de domicilio no existe.");
    }

    [HttpPost("tipos-domicilio/{id:int}/habilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HabilitarTipoDomicilio([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusTipoDomicilioAsync(id, StatusActivo, IdUsuarioLegacy(), Equipo(), ct),
            "Tipo de domicilio habilitado.", "El tipo de domicilio no existe.");
    }

    // =======================================================================
    // Condiciones de credito
    // =======================================================================

    [HttpPost("condiciones-credito")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearCondicionCredito([FromBody] CrearCondicionCreditoRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.CondicionCredito, 250, "nombre de la condicion");
        if (error != null) return BadRequest(error);
        if (request.DiasCredito < 0)
            return BadRequest(new { ok = false, message = "Los dias de credito no pueden ser negativos." });

        var sinPermiso = await ExigirPermisoAsync(PermisosCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        return RespondeAlta(await _service.CrearCondicionCreditoAsync(request, ct), "Condicion de credito dada de alta.");
    }

    [HttpPut("condiciones-credito/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarCondicionCredito([FromRoute] int id, [FromBody] ActualizarCondicionCreditoRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.CondicionCredito, 250, "nombre de la condicion");
        if (error != null) return BadRequest(error);
        if (request.DiasCredito < 0)
            return BadRequest(new { ok = false, message = "Los dias de credito no pueden ser negativos." });

        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await _service.ActualizarCondicionCreditoAsync(id, request, ct), "Condicion de credito actualizada.");
    }

    [HttpPost("condiciones-credito/{id:int}/inhabilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InhabilitarCondicionCredito([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusCondicionCreditoAsync(id, StatusCancelado, ct),
            "Condicion de credito inhabilitada.", "La condicion de credito no existe.");
    }

    [HttpPost("condiciones-credito/{id:int}/habilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HabilitarCondicionCredito([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusCondicionCreditoAsync(id, StatusActivo, ct),
            "Condicion de credito habilitada.", "La condicion de credito no existe.");
    }

    // =======================================================================
    // Documentos
    // =======================================================================

    [HttpPost("documentos")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearDocumento([FromBody] CrearDocumentoRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.Documento, 400, "nombre del documento");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        return RespondeAlta(await _service.CrearDocumentoAsync(request, IdUsuarioLegacy(), Equipo(), ct),
            "Documento dado de alta.");
    }

    [HttpPut("documentos/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarDocumento([FromRoute] int id, [FromBody] ActualizarDocumentoRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.Documento, 400, "nombre del documento");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await _service.ActualizarDocumentoAsync(id, request, IdUsuarioLegacy(), Equipo(), ct),
            "Documento actualizado.");
    }

    [HttpPost("documentos/{id:int}/inhabilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InhabilitarDocumento([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusDocumentoAsync(id, StatusCancelado, IdUsuarioLegacy(), Equipo(), ct),
            "Documento inhabilitado.", "El documento no existe.");
    }

    [HttpPost("documentos/{id:int}/habilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HabilitarDocumento([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusDocumentoAsync(id, StatusActivo, IdUsuarioLegacy(), Equipo(), ct),
            "Documento habilitado.", "El documento no existe.");
    }

    // =======================================================================
    // Galleros
    // =======================================================================

    /// <summary>
    /// Alta de gallero. El saldo inicial solo se puede fijar AQUI: el UPDATE de
    /// legacy tiene el "Saldo = @Saldo" comentado.
    /// </summary>
    [HttpPost("galleros")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearGallero([FromBody] CrearGalleroRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.Gallero, 400, "nombre del gallero");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosGallerosCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        return RespondeAlta(await _service.CrearGalleroAsync(request, IdUsuarioLegacy(), Equipo(), ct),
            "Gallero dado de alta.");
    }

    /// <summary>
    /// Modificacion de gallero. No cambia el saldo, y el request no lo pide:
    /// el UPDATE de legacy no lo escribe.
    /// </summary>
    [HttpPut("galleros/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarGallero([FromRoute] int id, [FromBody] ActualizarGalleroRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.Gallero, 400, "nombre del gallero");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosGallerosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await _service.ActualizarGalleroAsync(id, request, IdUsuarioLegacy(), Equipo(), ct),
            "Gallero actualizado.");
    }

    [HttpPost("galleros/{id:int}/inhabilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InhabilitarGallero([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosGallerosBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusGalleroAsync(id, StatusCancelado, IdUsuarioLegacy(), Equipo(), ct),
            "Gallero inhabilitado.", "El gallero no existe.");
    }

    [HttpPost("galleros/{id:int}/habilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HabilitarGallero([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosGallerosBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusGalleroAsync(id, StatusActivo, IdUsuarioLegacy(), Equipo(), ct),
            "Gallero habilitado.", "El gallero no existe.");
    }

    // =======================================================================
    // Repartidores
    // =======================================================================

    [HttpPost("repartidores")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearRepartidor([FromBody] CrearRepartidorRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.Repartidor, 1000, "nombre del repartidor");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        return RespondeAlta(await _service.CrearRepartidorAsync(request, ct), "Repartidor dado de alta.");
    }

    [HttpPut("repartidores/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarRepartidor([FromRoute] int id, [FromBody] ActualizarRepartidorRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.Repartidor, 1000, "nombre del repartidor");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await _service.ActualizarRepartidorAsync(id, request, ct), "Repartidor actualizado.");
    }

    [HttpPost("repartidores/{id:int}/inhabilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InhabilitarRepartidor([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusRepartidorAsync(id, StatusCancelado, ct),
            "Repartidor inhabilitado.", "El repartidor no existe.");
    }

    [HttpPost("repartidores/{id:int}/habilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HabilitarRepartidor([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusRepartidorAsync(id, StatusActivo, ct),
            "Repartidor habilitado.", "El repartidor no existe.");
    }

    // =======================================================================
    // Tipos de recordatorio
    // =======================================================================

    [HttpPost("tipos-recordatorio")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearTipoRecordatorio([FromBody] CrearTipoRecordatorioRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.TipoRecordatorio, 250);
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        return RespondeAlta(await _service.CrearTipoRecordatorioAsync(request, IdUsuarioLegacy(), Equipo(), ct),
            "Tipo de recordatorio dado de alta.");
    }

    [HttpPut("tipos-recordatorio/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarTipoRecordatorio([FromRoute] int id, [FromBody] ActualizarTipoRecordatorioRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.TipoRecordatorio, 250);
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await _service.ActualizarTipoRecordatorioAsync(id, request, IdUsuarioLegacy(), Equipo(), ct),
            "Tipo de recordatorio actualizado.");
    }

    [HttpPost("tipos-recordatorio/{id:int}/inhabilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InhabilitarTipoRecordatorio([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusTipoRecordatorioAsync(id, StatusCancelado, IdUsuarioLegacy(), Equipo(), ct),
            "Tipo de recordatorio inhabilitado.", "El tipo de recordatorio no existe.");
    }

    [HttpPost("tipos-recordatorio/{id:int}/habilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HabilitarTipoRecordatorio([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusTipoRecordatorioAsync(id, StatusActivo, IdUsuarioLegacy(), Equipo(), ct),
            "Tipo de recordatorio habilitado.", "El tipo de recordatorio no existe.");
    }
}
