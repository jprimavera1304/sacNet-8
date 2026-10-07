using ISL_Service.Application.DTOs.Sueltos;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Cuatro pantallas que no caen en ningun otro lote: recordatorios, servicios,
/// proyectos de obra por consulta ciudadana y la cola de sincronizacion de cajas.
///
/// OCHO DE SUS NUEVE PROCEDIMIENTOS NO ESTAN EN ESTA BASE. Comprobado en
/// sys.objects de Produccion_svr: faltan los cuatro de proyectos, los tres de
/// servicios y el de sincronizacion. Legacy falla igual aqui. Estan implementados
/// con los parametros exactos que manda MacServicios2 y marcados como NO
/// VERIFICABLES: la comprobacion tiene que hacerse en la instalacion que si los
/// tiene. Recordatorios si se puede comprobar en esta base.
/// </summary>
[ApiController]
[Route("api")]
[Authorize]
public class SueltosController : PermisoControllerBase
{
    private static readonly string[] PermisosRecordatoriosVer = { "recordatorios.ver" };
    private static readonly string[] PermisosRecordatoriosCrear = { "recordatorios.crear" };
    private static readonly string[] PermisosRecordatoriosEditar = { "recordatorios.editar" };

    private static readonly string[] PermisosServiciosVer = { "servicios.ver" };
    private static readonly string[] PermisosServiciosCrear = { "servicios.crear" };
    private static readonly string[] PermisosServiciosCancelar = { "servicios.cancelar" };

    private static readonly string[] PermisosProyectosVer = { "proyectos.ver" };
    private static readonly string[] PermisosProyectosCrear = { "proyectos.crear" };
    private static readonly string[] PermisosProyectosEditar = { "proyectos.editar" };

    private static readonly string[] PermisosSincronizacion = { "sincronizacion.ver" };

    private readonly ISueltosService _service;

    public SueltosController(
        ISueltosService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    // =======================================================================
    // RECORDATORIOS
    // =======================================================================

    [HttpGet("recordatorios")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConsultarRecordatorios([FromQuery] RecordatoriosFiltro? filtro, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosRecordatoriosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        filtro ??= new RecordatoriosFiltro();
        var error = ValidaRango(filtro.Desde, filtro.Hasta);
        if (error != null) return BadRequest(error);

        var data = await _service.ConsultarRecordatoriosAsync(filtro, ct);
        return Ok(new { ok = true, message = "Recordatorios consultados.", data, total = data.Count });
    }

    /// <summary>
    /// Alta de recordatorio.
    ///
    /// La fecha limite es obligatoria. En legacy no se validaba y la conversion se
    /// hacia sin comprobar que viniera: un alta sin fecha limite reventaba con un
    /// error de posicion de caracteres y devolvia 400 con el stack completo. Un
    /// recordatorio sin fecha limite tampoco recuerda nada.
    /// </summary>
    /// <response code="201">Recordatorio creado</response>
    [HttpPost("recordatorios")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearRecordatorio([FromBody] CrearRecordatorioRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        if (string.IsNullOrWhiteSpace(request.Recordatorio))
            return BadRequest(new { ok = false, message = "El texto del recordatorio es requerido." });
        if (request.Recordatorio.Trim().Length > 2000)
            return BadRequest(new { ok = false, message = "El recordatorio no puede pasar de 2000 caracteres." });
        if (request.IdTipoRecordatorio <= 0)
            return BadRequest(new { ok = false, message = "El tipo de recordatorio es requerido." });
        if (request.FechaLimite == default)
            return BadRequest(new { ok = false, message = "La fecha limite es requerida." });

        var sinPermiso = await ExigirPermisoAsync(PermisosRecordatoriosCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.CrearRecordatorioAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return data.Guardo
            ? StatusCode(StatusCodes.Status201Created, new { ok = true, message = "Recordatorio creado.", data })
            : Responder(data, "Recordatorio creado.");
    }

    /// <summary>
    /// Modificacion de recordatorio. Pide la ficha completa: el UPDATE del
    /// procedimiento escribe todas las columnas, asi que un campo que no venga se
    /// guarda vacio.
    /// </summary>
    [HttpPut("recordatorios/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarRecordatorio(
        [FromRoute] int id, [FromBody] ActualizarRecordatorioRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        if (string.IsNullOrWhiteSpace(request.Recordatorio))
            return BadRequest(new { ok = false, message = "El texto del recordatorio es requerido." });
        if (request.Recordatorio.Trim().Length > 2000)
            return BadRequest(new { ok = false, message = "El recordatorio no puede pasar de 2000 caracteres." });
        if (request.IdTipoRecordatorio <= 0)
            return BadRequest(new { ok = false, message = "El tipo de recordatorio es requerido." });
        if (request.IdStatusRecordatorio <= 0)
            return BadRequest(new { ok = false, message = "El estatus del recordatorio es requerido." });
        if (request.FechaLimite == default)
            return BadRequest(new { ok = false, message = "La fecha limite es requerida." });

        var sinPermiso = await ExigirPermisoAsync(PermisosRecordatoriosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(
            await _service.ActualizarRecordatorioAsync(id, request, IdUsuarioLegacy(), Equipo(), ct),
            "Recordatorio actualizado.");
    }

    // =======================================================================
    // SERVICIOS
    // =======================================================================

    /// <summary>
    /// Consulta de servicios. Con `folioInicial` se busca por rango de folios y se
    /// conserva solo la empresa: es la precedencia de legacy.
    /// </summary>
    [HttpGet("servicios")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConsultarServicios([FromQuery] ServiciosFiltro? filtro, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosServiciosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        filtro ??= new ServiciosFiltro();
        var error = ValidaRango(filtro.RecepcionDesde, filtro.RecepcionHasta)
                    ?? ValidaRango(filtro.RegistroDesde, filtro.RegistroHasta)
                    ?? ValidaRango(filtro.CancelacionDesde, filtro.CancelacionHasta);
        if (error != null) return BadRequest(error);

        var data = await _service.ConsultarServiciosAsync(filtro, ct);
        return Ok(new { ok = true, message = "Servicios consultados.", data, total = data.Count });
    }

    /// <summary>Alta de servicio.</summary>
    /// <response code="201">Servicio registrado</response>
    [HttpPost("servicios")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearServicio([FromBody] CrearServicioRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        if (request.IdEmpresa <= 0) return BadRequest(new { ok = false, message = "La empresa es requerida." });
        if (request.IdCliente <= 0) return BadRequest(new { ok = false, message = "El cliente es requerido." });
        if (request.IdProducto <= 0) return BadRequest(new { ok = false, message = "El producto es requerido." });
        if (request.IdRepartidor <= 0) return BadRequest(new { ok = false, message = "El repartidor es requerido." });
        if (request.Cantidad <= 0) return BadRequest(new { ok = false, message = "La cantidad es requerida." });
        if (request.FechaRecepcion == default) return BadRequest(new { ok = false, message = "La fecha de recepcion es requerida." });
        if (string.IsNullOrWhiteSpace(request.Motivo)) return BadRequest(new { ok = false, message = "El motivo es requerido." });
        if (request.Importe < 0) return BadRequest(new { ok = false, message = "El importe no puede ser negativo." });

        var sinPermiso = await ExigirPermisoAsync(PermisosServiciosCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        var (resultado, renglones) = await _service.CrearServicioAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        if (!resultado.Guardo) return Responder(resultado, "Servicio registrado.");

        // El renglon que devuelve el procedimiento trae el folio asignado.
        return StatusCode(StatusCodes.Status201Created,
            new { ok = true, message = "Servicio registrado.", data = resultado, detalle = renglones });
    }

    /// <summary>
    /// Cancela el servicio.
    ///
    /// AQUI SE RECONSTRUYE UNA LLAMADA QUE EN LEGACY NO FUNCIONA. El repositorio de
    /// MacServicios2 ejecuta sp_n_CancelarServicio SIN UN SOLO PARAMETRO —sus tres
    /// AddWithValue estan comentados, y los nombres que usaban eran de otro modulo
    /// (@IDAjusteInventario)— y no valida nada, porque su bloque de validaciones
    /// tambien esta comentado. O sea que ese endpoint cancela "el servicio" sin
    /// decir cual.
    ///
    /// El procedimiento no esta en esta base, asi que no hay firma que leer. Se
    /// manda lo que la operacion necesita para significar algo: que servicio, quien
    /// lo cancela y desde donde. Queda marcado como NO VERIFICABLE y hay que
    /// confirmarlo contra la instalacion que si tiene el procedimiento.
    /// </summary>
    [HttpPost("servicios/{id:int}/cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelarServicio([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosServiciosCancelar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(
            await _service.CancelarServicioAsync(id, IdUsuarioLegacy(), Equipo(), ct),
            "Servicio cancelado.");
    }

    // =======================================================================
    // SINCRONIZACION
    // =======================================================================

    /// <summary>
    /// La cola de sincronizacion de cajas, SIEMPRE contra la base de este backend.
    ///
    /// Legacy tenia una bandera booleana en el cuerpo que, en true, reemplazaba la
    /// conexion por la cadena guardada en Constantes.AviacionDBServer: el cliente
    /// elegia la base destino desde el JSON. Eso no se hereda — la base a la que
    /// apunta el backend no la decide quien llama.
    /// </summary>
    [HttpGet("sincronizacion")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarSincronizacion([FromQuery] SincronizacionFiltro? filtro, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosSincronizacion, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.ConsultarSincronizacionAsync(filtro ?? new SincronizacionFiltro(), ct);
        return Ok(new { ok = true, message = "Cola de sincronizacion consultada.", data, total = data.Count });
    }

    // =======================================================================
    // PROYECTOS
    // =======================================================================

    /// <summary>
    /// Consulta de proyectos. Con `clave` se busca solo por clave: es la
    /// precedencia de legacy, dicha en vez de escondida.
    /// </summary>
    [HttpGet("proyectos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConsultarProyectos([FromQuery] ProyectosFiltro? filtro, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosProyectosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        filtro ??= new ProyectosFiltro();
        var error = ValidaRango(filtro.ContratacionDesde, filtro.ContratacionHasta);
        if (error != null) return BadRequest(error);

        var data = await _service.ConsultarProyectosAsync(filtro, ct);
        return Ok(new { ok = true, message = "Proyectos consultados.", data, total = data.Count });
    }

    /// <summary>
    /// Alta de proyecto.
    ///
    /// La clave es obligatoria: es con la que se busca el proyecto y la unica que
    /// lo identifica para la gente. En legacy TODAS las validaciones de este
    /// endpoint estaban comentadas menos usuario y equipo, asi que se podia dar de
    /// alta un proyecto sin clave, sin contratista y sin contrato.
    /// </summary>
    /// <response code="201">Proyecto creado</response>
    [HttpPost("proyectos")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearProyecto([FromBody] ProyectoRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaProyecto(request);
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosProyectosCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.CrearProyectoAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return data.Guardo
            ? StatusCode(StatusCodes.Status201Created, new { ok = true, message = "Proyecto creado.", data })
            : Responder(data, "Proyecto creado.");
    }

    /// <summary>Modificacion de proyecto. Ficha completa.</summary>
    [HttpPut("proyectos/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarProyecto(
        [FromRoute] int id, [FromBody] ActualizarProyectoRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaProyecto(request);
        if (error != null) return BadRequest(error);
        if (request.IdStatus <= 0)
            return BadRequest(new { ok = false, message = "El estatus del proyecto es requerido." });

        var sinPermiso = await ExigirPermisoAsync(PermisosProyectosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(
            await _service.ActualizarProyectoAsync(id, request, IdUsuarioLegacy(), Equipo(), ct),
            "Proyecto actualizado.");
    }

    /// <summary>
    /// Las cinco facturas del proyecto.
    ///
    /// Tiene su propia ruta porque tiene su propio procedimiento. En legacy esos
    /// mismos importes tambien viajaban en el PUT del proyecto, asi que dos
    /// pantallas podian pisarse el mismo dato sin saberlo; aqui solo se escriben
    /// desde aqui.
    /// </summary>
    [HttpPut("proyectos/{id:int}/facturas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarFacturasProyecto(
        [FromRoute] int id, [FromBody] FacturasProyectoRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        if (request.Facturas.Count > 5)
            return BadRequest(new { ok = false, message = "El proyecto solo admite cinco facturas: el procedimiento tiene cinco lugares." });
        if (request.Facturas.Any(x => x.Monto < 0))
            return BadRequest(new { ok = false, message = "El monto de una factura no puede ser negativo." });

        var sinPermiso = await ExigirPermisoAsync(PermisosProyectosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(
            await _service.ActualizarFacturasProyectoAsync(id, request, ct),
            "Facturas del proyecto actualizadas.");
    }

    // =======================================================================

    private IActionResult Responder(SueltoEscrituraResponse r, string mensajeExito)
        => Ok(new
        {
            ok = r.Guardo,
            message = r.Guardo
                ? mensajeExito
                : (string.IsNullOrWhiteSpace(r.Mensaje) ? "La base rechazo la operacion." : r.Mensaje),
            data = r
        });

    private static object? ValidaRango(DateTime? desde, DateTime? hasta)
    {
        if (desde.HasValue != hasta.HasValue)
            return new { ok = false, message = "El rango de fechas necesita las dos fechas." };
        if (desde.HasValue && hasta!.Value < desde.Value)
            return new { ok = false, message = "El rango de fechas termina antes de empezar." };
        return null;
    }

    private static object? ValidaProyecto(ProyectoRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Clave))
            return new { ok = false, message = "La clave del proyecto es requerida." };

        if (r.CuentaConContrato && (r.FechaInicioContrato is null || r.FechaTerminoContrato is null))
            return new { ok = false, message = "Si el proyecto cuenta con contrato, sus dos fechas son requeridas." };

        if (r.FechaInicioContrato.HasValue && r.FechaTerminoContrato.HasValue
            && r.FechaTerminoContrato.Value < r.FechaInicioContrato.Value)
            return new { ok = false, message = "El contrato termina antes de empezar." };

        if (r.PresupuestoAsignadoProyecto < 0 || r.Contratado < 0 || r.MontoFactura < 0
            || r.Ejercido < 0 || r.SaldoDelCompromiso < 0)
            return new { ok = false, message = "Un importe del proyecto no puede ser negativo." };

        if (r.AvanceEjecucion is < 0 or > 100)
            return new { ok = false, message = "El avance de ejecucion va de 0 a 100." };

        if (r.NumEvidenciasFoto is < 0 or > 5)
            return new { ok = false, message = "Las evidencias de foto van de 0 a 5: el procedimiento tiene cinco lugares." };

        return null;
    }
}
