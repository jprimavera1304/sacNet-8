using ISL_Service.Application.DTOs.Aviacion;
using ISL_Service.Application.DTOs.EscriturasLegacy;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/*
  LOS CATALOGOS DEL MODULO DE AVIACION.

  DOS ORIGENES, UN ENDPOINT
  -------------------------
  Legacy tenia /consultar y /consultar/server para cuentas, economicos, motivos
  de fianza, operadores y permisionarios: la primera pregunta a la replica local
  de la caja, la segunda al servidor central. Aqui es `?central=true`. Las
  escrituras van SIEMPRE al central, igual que en Legacy.

  LOS PERMISOS SON POR CATALOGO, NO UNO PARA TODO AVIACION
  --------------------------------------------------------
  Porque no es la misma gente. Quien mantiene el catalogo de cuentas (que define
  cuanto se cobra) no es quien da de alta operadores, y "aceptar" —que es lo que
  convierte un alta de caja en un renglon bueno del central— es de quien
  supervisa, no de quien captura. De ahi que cada familia tenga ver/crear/editar
  y que aceptar tenga su propia clave.

  Son permisos del WEB, no derivados de Mac31: no caen en el puente de las cuatro
  familias (ventas.*, reportes.*, empleados.*, prestamos.*), asi que lo que se
  de desde la pantalla de permisos se respeta.

  NO VERIFICABLE EN ESTA MAQUINA
  ------------------------------
  Ninguno de estos sp_n_ esta instalado en Produccion_svr y
  Constantes.AviacionDBServer esta vacia. Legacy falla igual. El detalle esta en
  AviacionCatalogosRepository.
*/
[ApiController]
[Route("api/aviacion")]
[Authorize]
public class AviacionCatalogosController : PermisoControllerBase
{
    private readonly IAviacionCatalogosService _service;

    private static readonly string[] EditarCajas = { "aviacion.cajas.editar" };
    private static readonly string[] VerConceptos = { "aviacion.conceptos.ver" };
    private static readonly string[] CrearConceptos = { "aviacion.conceptos.crear" };
    private static readonly string[] EditarConceptos = { "aviacion.conceptos.editar" };
    private static readonly string[] VerCuentas = { "aviacion.cuentas.ver" };
    private static readonly string[] CrearCuentas = { "aviacion.cuentas.crear" };
    private static readonly string[] EditarCuentas = { "aviacion.cuentas.editar" };
    private static readonly string[] AceptarCuentas = { "aviacion.cuentas.aceptar" };
    private static readonly string[] VerEconomicos = { "aviacion.economicos.ver" };
    private static readonly string[] CrearEconomicos = { "aviacion.economicos.crear" };
    private static readonly string[] EditarEconomicos = { "aviacion.economicos.editar" };
    private static readonly string[] AceptarEconomicos = { "aviacion.economicos.aceptar" };
    private static readonly string[] ReasignarEconomicos = { "aviacion.economicos.reasignar" };
    private static readonly string[] VerFianzas = { "aviacion.fianzas.ver" };
    private static readonly string[] CrearFianzas = { "aviacion.fianzas.crear" };
    private static readonly string[] EditarFianzas = { "aviacion.fianzas.editar" };
    private static readonly string[] VerOperadores = { "aviacion.operadores.ver" };
    private static readonly string[] CrearOperadores = { "aviacion.operadores.crear" };
    private static readonly string[] EditarOperadores = { "aviacion.operadores.editar" };
    private static readonly string[] AceptarOperadores = { "aviacion.operadores.aceptar" };
    private static readonly string[] VerPermisionarios = { "aviacion.permisionarios.ver" };
    private static readonly string[] CrearPermisionarios = { "aviacion.permisionarios.crear" };
    private static readonly string[] EditarPermisionarios = { "aviacion.permisionarios.editar" };
    private static readonly string[] AceptarPermisionarios = { "aviacion.permisionarios.aceptar" };
    private static readonly string[] VerImpresoras = { "aviacion.impresoras.ver" };
    private static readonly string[] EditarImpresoras = { "aviacion.impresoras.editar" };

    public AviacionCatalogosController(
        IAviacionCatalogosService service,
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

    // ==================================================================
    // Cajas
    // ==================================================================

    /// <summary>
    /// Actualiza una caja de recaudacion.
    ///
    /// No hay alta: en Legacy esta comentada a proposito, una caja se crea
    /// instalando el equipo.
    /// </summary>
    [HttpPut("cajas/{id:int}")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarCajaAsync(int id, [FromBody] ActualizarAviacionCajaRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(EditarCajas, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.ActualizarCajaAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Caja actualizada.", data });
    }

    // ==================================================================
    // Conceptos
    // ==================================================================

    /// <summary>Los conceptos de aviacion.</summary>
    [HttpGet("conceptos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarConceptosAsync([FromQuery] AviacionConceptosFiltro filtro, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(VerConceptos, ct);
        if (no is not null) return no;
        return Lista("Conceptos consultados.", await _service.ConsultarConceptosAsync(filtro, ct));
    }

    /// <summary>El siguiente numero de cuenta libre de un concepto.</summary>
    [HttpGet("conceptos/{id:int}/siguiente-cuenta")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarConceptoSiguienteCuentaAsync(int id, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(VerConceptos, ct);
        if (no is not null) return no;
        return Lista("Siguiente cuenta del concepto.", await _service.ConsultarConceptoSiguienteCuentaAsync(id, ct));
    }

    /// <summary>Da de alta un concepto de aviacion.</summary>
    [HttpPost("conceptos")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearConceptoAsync([FromBody] CrearAviacionConceptoRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(CrearConceptos, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.CrearConceptoAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Concepto dado de alta.", data });
    }

    /// <summary>Actualiza un concepto de aviacion.</summary>
    [HttpPut("conceptos/{id:int}")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarConceptoAsync(int id, [FromBody] ActualizarAviacionConceptoRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(EditarConceptos, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.ActualizarConceptoAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Concepto actualizado.", data });
    }

    // ==================================================================
    // Cuentas
    // ==================================================================

    /// <summary>
    /// Las cuentas de aviacion.
    /// </summary>
    /// <param name="central">
    /// false (por omision) = la replica local de la caja; true = el servidor
    /// central de aviacion. Son las dos rutas que Legacy tenia separadas.
    /// </param>
    [HttpGet("cuentas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConsultarCuentasAsync([FromQuery] AviacionCuentasFiltro filtro, [FromQuery] bool central, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(VerCuentas, ct);
        if (no is not null) return no;
        return Lista("Cuentas consultadas.", await _service.ConsultarCuentasAsync(filtro, central, ct));
    }

    /// <summary>El siguiente numero de cuenta libre.</summary>
    [HttpGet("cuentas/siguiente-numero")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConsultarCuentaSiguienteNumeroAsync([FromQuery] int idCuenta, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(VerCuentas, ct);
        if (no is not null) return no;
        return Lista("Siguiente numero de cuenta.", await _service.ConsultarCuentaSiguienteNumeroAsync(idCuenta, ct));
    }

    /// <summary>Da de alta una cuenta de aviacion.</summary>
    [HttpPost("cuentas")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearCuentaAsync([FromBody] CrearAviacionCuentaRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(CrearCuentas, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.CrearCuentaAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Cuenta dada de alta.", data });
    }

    /// <summary>Actualiza una cuenta de aviacion.</summary>
    [HttpPut("cuentas/{id:int}")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarCuentaAsync(int id, [FromBody] ActualizarAviacionCuentaRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(EditarCuentas, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.ActualizarCuentaAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Cuenta actualizada.", data });
    }

    /// <summary>Acepta una cuenta que quedo pendiente de aceptacion.</summary>
    [HttpPut("cuentas/{id:int}/aceptar")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AceptarCuentaAsync(int id, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(AceptarCuentas, ct);
        if (no is not null) return no;

        var data = await _service.AceptarCuentaAsync(id, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Cuenta aceptada.", data });
    }

    /// <summary>Los porcentajes de distribucion por tarjeta.</summary>
    [HttpGet("cuentas/tarjeta-porcentajes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConsultarTarjetaPorcentajesAsync([FromQuery] int idCuenta, [FromQuery] int idStatus, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(VerCuentas, ct);
        if (no is not null) return no;
        return Lista("Porcentajes de tarjeta consultados.", await _service.ConsultarTarjetaPorcentajesAsync(idCuenta, idStatus, ct));
    }

    /// <summary>
    /// Guarda los porcentajes de distribucion por tarjeta de varias cuentas.
    /// Tienen que sumar 100.
    /// </summary>
    [HttpPut("cuentas/tarjeta-porcentajes")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarTarjetaPorcentajesAsync([FromBody] ActualizarAviacionTarjetaPorcentajesRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(EditarCuentas, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.ActualizarTarjetaPorcentajesAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Porcentajes de tarjeta guardados.", data });
    }

    // ==================================================================
    // Economicos
    // ==================================================================

    /// <summary>Los economicos (unidades).</summary>
    [HttpGet("economicos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConsultarEconomicosAsync([FromQuery] AviacionEconomicosFiltro filtro, [FromQuery] bool central, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(VerEconomicos, ct);
        if (no is not null) return no;
        return Lista("Economicos consultados.", await _service.ConsultarEconomicosAsync(filtro, central, ct));
    }

    /// <summary>El siguiente id de economico de un permisionario.</summary>
    [HttpGet("economicos/siguiente-id")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConsultarEconomicoSiguienteIdAsync([FromQuery] int idPermisionario, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(VerEconomicos, ct);
        if (no is not null) return no;
        return Lista("Siguiente id de economico.", await _service.ConsultarEconomicoSiguienteIdAsync(idPermisionario, ct));
    }

    /// <summary>El siguiente numero de cuenta de economico de un permisionario.</summary>
    [HttpGet("economicos/siguiente-numero-cuenta")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConsultarEconomicoSiguienteNumeroCuentaAsync([FromQuery] int idPermisionario, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(VerEconomicos, ct);
        if (no is not null) return no;
        return Lista("Siguiente numero de cuenta de economico.", await _service.ConsultarEconomicoSiguienteNumeroCuentaAsync(idPermisionario, ct));
    }

    /// <summary>Los auxiliares (los cobros pendientes) de un economico.</summary>
    [HttpGet("economicos/auxiliares")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarEconomicosAuxiliaresAsync([FromQuery] AviacionEconomicosAuxiliaresFiltro filtro, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(VerEconomicos, ct);
        if (no is not null) return no;
        return Lista("Auxiliares consultados.", await _service.ConsultarEconomicosAuxiliaresAsync(filtro, ct));
    }

    /// <summary>Da de alta un economico.</summary>
    [HttpPost("economicos")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearEconomicoAsync([FromBody] CrearAviacionEconomicoRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(CrearEconomicos, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.CrearEconomicoAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Economico dado de alta.", data });
    }

    /// <summary>Actualiza un economico.</summary>
    [HttpPut("economicos/{id:int}")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarEconomicoAsync(int id, [FromBody] ActualizarAviacionEconomicoRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(EditarEconomicos, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.ActualizarEconomicoAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Economico actualizado.", data });
    }

    /// <summary>Cambia que conceptos paga un economico.</summary>
    [HttpPut("economicos/{id:int}/amortizacion")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarEconomicoAmortizacionAsync(int id, [FromBody] ActualizarAviacionEconomicoAmortizacionRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(EditarEconomicos, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.ActualizarEconomicoAmortizacionAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Conceptos del economico actualizados.", data });
    }

    /// <summary>
    /// Pasa un economico de un permisionario a otro.
    /// Permiso propio: cambia de dueno una unidad y deja rastro en el historico.
    /// </summary>
    [HttpPut("economicos/{id:int}/reasignacion")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReasignarEconomicoAsync(int id, [FromBody] ReasignarAviacionEconomicoRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(ReasignarEconomicos, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.ReasignarEconomicoAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Economico reasignado.", data });
    }

    /// <summary>Acepta un economico que quedo pendiente de aceptacion.</summary>
    [HttpPut("economicos/{id:int}/aceptar")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AceptarEconomicoAsync(int id, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(AceptarEconomicos, ct);
        if (no is not null) return no;

        var data = await _service.AceptarEconomicoAsync(id, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Economico aceptado.", data });
    }

    // ==================================================================
    // Motivos de fianza
    // ==================================================================

    /// <summary>Los motivos de fianza.</summary>
    [HttpGet("motivos-fianza")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConsultarMotivosFianzaAsync([FromQuery] AviacionMotivosFianzaFiltro filtro, [FromQuery] bool central, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(VerFianzas, ct);
        if (no is not null) return no;
        return Lista("Motivos de fianza consultados.", await _service.ConsultarMotivosFianzaAsync(filtro, central, ct));
    }

    /// <summary>El siguiente id de motivo de fianza.</summary>
    [HttpGet("motivos-fianza/siguiente-id")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConsultarMotivoFianzaSiguienteIdAsync([FromQuery] int idMotivoFianza, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(VerFianzas, ct);
        if (no is not null) return no;
        return Lista("Siguiente id de motivo de fianza.", await _service.ConsultarMotivoFianzaSiguienteIdAsync(idMotivoFianza, ct));
    }

    /// <summary>
    /// Da de alta un motivo de fianza. De ingreso o de egreso, y solo uno:
    /// despues no se puede corregir porque el SP de actualizar no recibe esas
    /// banderas.
    /// </summary>
    [HttpPost("motivos-fianza")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearMotivoFianzaAsync([FromBody] CrearAviacionMotivoFianzaRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(CrearFianzas, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.CrearMotivoFianzaAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Motivo de fianza dado de alta.", data });
    }

    /// <summary>Actualiza un motivo de fianza.</summary>
    [HttpPut("motivos-fianza/{id:int}")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarMotivoFianzaAsync(int id, [FromBody] ActualizarAviacionMotivoFianzaRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(EditarFianzas, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.ActualizarMotivoFianzaAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Motivo de fianza actualizado.", data });
    }

    // ==================================================================
    // Operadores
    // ==================================================================

    /// <summary>Los operadores.</summary>
    [HttpGet("operadores")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConsultarOperadoresAsync([FromQuery] AviacionOperadoresFiltro filtro, [FromQuery] bool central, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(VerOperadores, ct);
        if (no is not null) return no;
        return Lista("Operadores consultados.", await _service.ConsultarOperadoresAsync(filtro, central, ct));
    }

    /// <summary>El siguiente numero de cuenta de operador.</summary>
    [HttpGet("operadores/siguiente-numero-cuenta")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConsultarOperadorSiguienteNumeroCuentaAsync([FromQuery] int idOperador, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(VerOperadores, ct);
        if (no is not null) return no;
        return Lista("Siguiente numero de cuenta de operador.", await _service.ConsultarOperadorSiguienteNumeroCuentaAsync(idOperador, ct));
    }

    /// <summary>Da de alta un operador.</summary>
    [HttpPost("operadores")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearOperadorAsync([FromBody] CrearAviacionOperadorRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(CrearOperadores, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.CrearOperadorAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Operador dado de alta.", data });
    }

    /// <summary>Actualiza un operador.</summary>
    [HttpPut("operadores/{id:int}")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarOperadorAsync(int id, [FromBody] ActualizarAviacionOperadorRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(EditarOperadores, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.ActualizarOperadorAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Operador actualizado.", data });
    }

    /// <summary>Acepta un operador que quedo pendiente de aceptacion.</summary>
    [HttpPut("operadores/{id:int}/aceptar")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AceptarOperadorAsync(int id, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(AceptarOperadores, ct);
        if (no is not null) return no;

        var data = await _service.AceptarOperadorAsync(id, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Operador aceptado.", data });
    }

    /// <summary>Cambia el importe de la fianza de un operador.</summary>
    [HttpPut("operadores/{id:int}/fianza")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarOperadorFianzaAsync(int id, [FromBody] ActualizarAviacionOperadorFianzaRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(EditarOperadores, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.ActualizarOperadorFianzaAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Fianza del operador guardada.", data });
    }

    /// <summary>Asigna una cuenta a un operador con su importe.</summary>
    [HttpPut("operadores/{id:int}/cuenta")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarOperadorCuentaAsync(int id, [FromBody] ActualizarAviacionOperadorCuentaRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(EditarOperadores, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.ActualizarOperadorCuentaAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Cuenta del operador asignada.", data });
    }

    // ==================================================================
    // Permisionarios
    // ==================================================================

    /// <summary>Los permisionarios.</summary>
    [HttpGet("permisionarios")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConsultarPermisionariosAsync([FromQuery] AviacionPermisionariosFiltro filtro, [FromQuery] bool central, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(VerPermisionarios, ct);
        if (no is not null) return no;
        return Lista("Permisionarios consultados.", await _service.ConsultarPermisionariosAsync(filtro, central, ct));
    }

    /// <summary>
    /// El siguiente numero de cuenta de permisionario.
    ///
    /// No lleva parametros: el SP recibe un @IDPermisionario que Legacy fijaba en
    /// 0 siempre, porque el numero es el siguiente del catalogo y no depende de
    /// ningun permisionario. Ofrecer el filtro seria ofrecer algo que no hace
    /// nada.
    /// </summary>
    [HttpGet("permisionarios/siguiente-numero-cuenta")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConsultarPermisionarioSiguienteNumeroCuentaAsync(CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(VerPermisionarios, ct);
        if (no is not null) return no;
        return Lista("Siguiente numero de cuenta de permisionario.", await _service.ConsultarPermisionarioSiguienteNumeroCuentaAsync(ct));
    }

    /// <summary>Da de alta un permisionario.</summary>
    [HttpPost("permisionarios")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearPermisionarioAsync([FromBody] CrearAviacionPermisionarioRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(CrearPermisionarios, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.CrearPermisionarioAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Permisionario dado de alta.", data });
    }

    /// <summary>Actualiza un permisionario.</summary>
    [HttpPut("permisionarios/{id:int}")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarPermisionarioAsync(int id, [FromBody] ActualizarAviacionPermisionarioRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(EditarPermisionarios, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.ActualizarPermisionarioAsync(id, request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Permisionario actualizado.", data });
    }

    /// <summary>Acepta un permisionario que quedo pendiente de aceptacion.</summary>
    [HttpPut("permisionarios/{id:int}/aceptar")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AceptarPermisionarioAsync(int id, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(AceptarPermisionarios, ct);
        if (no is not null) return no;

        var data = await _service.AceptarPermisionarioAsync(id, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Permisionario aceptado.", data });
    }

    // ==================================================================
    // Impresoras
    // ==================================================================

    /// <summary>Las impresoras configuradas, por equipo.</summary>
    [HttpGet("impresoras")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarImpresorasAsync([FromQuery] AviacionImpresorasFiltro filtro, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(VerImpresoras, ct);
        if (no is not null) return no;
        return Lista("Impresoras consultadas.", await _service.ConsultarImpresorasAsync(filtro, ct));
    }

    /// <summary>
    /// Guarda las impresoras de un equipo.
    ///
    /// `equipo` viaja en el cuerpo porque aqui es la LLAVE del renglon (de que
    /// computadora son las impresoras), no la firma de quien hace el cambio. La
    /// firma sigue saliendo del token. Es el unico sitio de obra y aviacion donde
    /// el equipo es un dato.
    /// </summary>
    [HttpPut("impresoras")]
    [ProducesResponseType(typeof(ResultadoEscrituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarImpresoraAsync([FromBody] ActualizarAviacionImpresoraRequest? request, CancellationToken ct)
    {
        var no = await ExigirPermisoAsync(EditarImpresoras, ct);
        if (no is not null) return no;
        if (request is null) return Body();

        var data = await _service.ActualizarImpresoraAsync(request, IdUsuarioLegacy(), ct);
        return Ok(new { ok = true, message = "Impresoras guardadas.", data });
    }
}
