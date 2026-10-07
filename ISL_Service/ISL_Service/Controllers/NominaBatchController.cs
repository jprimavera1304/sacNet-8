using ISL_Service.Application.DTOs.Nomina;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Los tres procesos de nomina de Mac31. LOS TRES ESCRIBEN.
///
/// En MacServicios2 venian disfrazados, y eso es lo que mas cambia aqui:
///
///  - POST /api/empleadostiposueldorecibos/consultar NO consultaba: CALCULA la
///    nomina. El procedimiento borra los recibos del periodo y los vuelve a
///    insertar con un cursor. Ahora es POST recibos/calcular.
///  - POST /api/periodostiposueldodias/validar NO validaba: CREA los periodos de
///    nomina que falten. Se disparaba en el LOGIN de Mac31, asi que lo corria
///    cualquiera que entrara. Ahora es POST periodos/generar, con su permiso.
///  - POST /api/empleadosprestamo/validar NO validaba: es el BATCH QUE DESCUENTA
///    LOS PRESTAMOS DE LA NOMINA. Ahora es POST prestamos/descontar.
///
/// Y el id del usuario sale del TOKEN. En legacy viajaba en el cuerpo: cualquiera
/// con un token valido podia disparar el descuento de la nomina firmandolo a
/// nombre de otra persona.
///
/// LOS PERMISOS SON DE LA FAMILIA `nomina.*`, NO DE `empleados.*` NI `prestamos.*`,
/// y eso es a proposito. Esas dos familias las gobierna Mac31: el puente de
/// permisos las recalcula desde los botones de sus formas y borra lo que haya
/// guardado el web. Un permiso `prestamos.descontar` dado desde la pantalla de
/// permisos desapareceria en el siguiente calculo, sin error y sin rastro, y el
/// boton quedaria apagado sin que nadie supiera por que.
/// </summary>
[ApiController]
[Route("api/nomina")]
[Authorize]
public class NominaBatchController : PermisoControllerBase
{
    private static readonly string[] PermisosCalcular = { "nomina.recibos.calcular" };
    private static readonly string[] PermisosPeriodos = { "nomina.periodos.generar" };
    private static readonly string[] PermisosDescontar = { "nomina.prestamos.descontar" };

    private readonly INominaBatchService _service;

    public NominaBatchController(
        INominaBatchService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    /// <summary>
    /// Calcula los recibos de un periodo. BORRA LOS RECIBOS DEL PERIODO Y LOS
    /// VUELVE A HACER.
    ///
    /// `idParametros` es el renglon de la tabla Parametros donde la pantalla dejo
    /// los filtros (que empleados, que periodo, que rango). Ese contrato se
    /// conserva porque el procedimiento los recupera de ahi con un
    /// INSERT ... EXEC: pasarle los datos directamente significaria reescribirlo, y
    /// los sp_n_ no se tocan. Los parametros se guardan con PUT api/parametros.
    ///
    /// SE EJECUTA UNA VEZ, para el periodo que se pida. Legacy lo corria DOS veces
    /// por peticion —nomina y comision— sin transaccion, y calculaba el periodo de
    /// comision como "el de nomina mas uno": si no era el consecutivo inmediato,
    /// recalculaba la nomina de un periodo ajeno. Para el de comision se llama otra
    /// vez con su id.
    ///
    /// DIFERENCIA DE ALCANCE QUE QUEDA DICHA: esto devuelve los DATOS del recibo,
    /// no el documento. El armado del papel (las plantillas 1020 de Template_Html,
    /// cuatro recibos por hoja en Tauro y uno en Zaragoza) es un REPORTE y le toca
    /// a la ola de reportes, que tiene su propio plan y su propio motor. Lo que
    /// escribe —el calculo— esta aqui completo.
    /// </summary>
    /// <response code="200">Recibos calculados, o el motivo por el que no</response>
    [HttpPost("recibos/calcular")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CalcularRecibos([FromBody] CalcularRecibosRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        if (request.IdParametros <= 0)
            return BadRequest(new { ok = false, message = "Falta el renglon de parametros con los filtros del recibo." });
        if (request.IdPeriodoTipoSueldo < 0)
            return BadRequest(new { ok = false, message = "El periodo no puede ser negativo." });

        var sinPermiso = await ExigirPermisoAsync(PermisosCalcular, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await _service.CalcularRecibosAsync(request, ct), "Recibos calculados.");
    }

    /// <summary>
    /// Genera los periodos de nomina que falten.
    ///
    /// Es una accion explicita y no un efecto del login, que es lo que era en
    /// Mac31: ahi lo disparaba cualquiera que entrara, y dos personas entrando al
    /// mismo tiempo los creaban dos veces.
    /// </summary>
    [HttpPost("periodos/generar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerarPeriodos(CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosPeriodos, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await _service.GenerarPeriodosAsync(IdUsuarioLegacy(), ct), "Periodos generados.");
    }

    /// <summary>
    /// Descuenta los prestamos de la nomina. AQUI SE MUEVE DINERO: inserta los
    /// pagos, baja el saldo de cada prestamo y liquida los que quedan pagados.
    ///
    /// No esta automatizado ni aqui ni en Mac31: lo dispara una persona. Por eso la
    /// ruta lo dice y por eso tiene su propio permiso, aparte del de consultar
    /// prestamos: ver un prestamo y descontarle a la nomina de todos no son la
    /// misma autoridad.
    ///
    /// La ficha de prestamos (api/prestamos) sigue sin exponer esto, y esta bien:
    /// desde la pantalla de UN prestamo no se dispara el descuento de la nomina
    /// completa.
    /// </summary>
    [HttpPost("prestamos/descontar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DescontarPrestamos(CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosDescontar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(
            await _service.DescontarPrestamosAsync(IdUsuarioLegacy(), ct),
            "Prestamos descontados de la nomina.");
    }

    private IActionResult Responder(NominaBatchResponse r, string mensajeExito)
        => Ok(new
        {
            ok = r.Guardo,
            message = r.Guardo
                ? mensajeExito
                : (string.IsNullOrWhiteSpace(r.Mensaje) ? "El proceso no se completo." : r.Mensaje),
            data = r.Renglones,
            total = r.Total
        });
}
