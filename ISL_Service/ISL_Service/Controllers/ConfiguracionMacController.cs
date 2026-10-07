using ISL_Service.Application.DTOs.SeguridadMac;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Configuracion de Mac31: constantes de empresa, el latido de actividad del
/// usuario, los parametros con los que se abre un reporte, las huellas y la
/// bitacora de contrasenas de autorizacion.
///
/// UNA COSA DE LEGACY QUE NO SE MIGRA, Y POR QUE
/// --------------------------------------------
/// POST y PUT /api/catformasprocesos de MacServicios2 NO se reimplementan. No es
/// un recorte de alcance: no existe la capacidad. Lo que hay en legacy es un
/// copy-paste comprobado en el codigo:
///   - el POST llama a sp_n_InsertarDiaFestivo mandandole solo @IDUsuario y
///     @Equipo, asi que "dar de alta una forma-proceso" INSERTA UN DIA FESTIVO EN
///     BLANCO, y el IDForma que recibe no se manda a ninguna parte;
///   - el PUT llama a sp_n_ActualizarDiaFestivo y le pasa el MISMO IDForma en
///     @IDDiaFestivo, @IDStatus y @DiaFestivo a la vez, e ignora el resto del
///     cuerpo.
/// Y no hay a donde apuntarlos bien: en Produccion_svr no existe ningun
/// sp_n_InsertarFormaProceso ni sp_n_ActualizarFormaProceso. El unico
/// procedimiento de escritura de ese terreno es sp_n_ActualizarUsuarioFormaProceso,
/// que asigna un proceso A UN USUARIO — y eso si esta migrado, en
/// PUT api/usuarios/mac31/{id}/procesos. Portar los otros dos solo serviria para
/// llenar el calendario de dias festivos vacios desde una pantalla de seguridad.
/// </summary>
[ApiController]
[Route("api")]
[Authorize]
public class ConfiguracionMacController : PermisoControllerBase
{
    // Las constantes son de empresa: cambiarlas mueve reglas de toda la
    // operacion (hasta cuanto puede diferir el conteo de usados, por ejemplo).
    private static readonly string[] PermisosConstantes = { "constantes.editar" };

    // La fecha de operacion de pagos decide con que fecha entran los cobros.
    private static readonly string[] PermisosFechaPagos = { "constantes.fechapagos.editar" };

    private static readonly string[] PermisosHuellas = { "huellas.editar" };
    private static readonly string[] PermisosParametrosVer = { "reportes.parametros.ver" };
    private static readonly string[] PermisosParametrosGuardar = { "reportes.parametros.editar" };

    // La bitacora de intentos de contrasena es un apunte de seguridad, no una
    // huella: permiso propio.
    private static readonly string[] PermisosBitacora = { "seguridad.bitacora.registrar" };

    private readonly ISeguridadMacService _service;

    public ConfiguracionMacController(
        ISeguridadMacService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    /// <summary>
    /// Guarda los cuatro parametros de operacion de la empresa.
    ///
    /// LOS CUATRO SON OBLIGATORIOS. En legacy este PUT no validaba nada: un cuerpo
    /// vacio escribia CERO en los cuatro, y con la maxima diferencia de usados en
    /// cero deja de cuadrar la revision de usados de todas las ventas. Son
    /// constantes que se cambian a mano una vez al ano; pedirlas completas no
    /// cuesta nada porque la pantalla ya las tiene leidas.
    /// </summary>
    [HttpPut("constantes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarConstantes([FromBody] ActualizarConstantesRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        if (request.MaximaDiferenciaUsados < 0)
            return BadRequest(new { ok = false, message = "La maxima diferencia de usados no puede ser negativa." });
        if (request.HorasLaborales is <= 0 or > 24)
            return BadRequest(new { ok = false, message = "Las horas laborales tienen que estar entre 1 y 24." });
        if (request.DiasFolioConcentrado < 0)
            return BadRequest(new { ok = false, message = "Los dias de folio del concentrado no pueden ser negativos." });
        if (request.NumDiasPedidosRechazados < 0)
            return BadRequest(new { ok = false, message = "Los dias de pedidos rechazados no pueden ser negativos." });

        var sinPermiso = await ExigirPermisoAsync(PermisosConstantes, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.ActualizarConstantesAsync(request, ct);
        return Ok(new { ok = true, message = "Constantes guardadas.", data });
    }

    /// <summary>
    /// Mueve la fecha de operacion de PAGOS: la que decide con que fecha entran los
    /// cobros.
    ///
    /// No es la fecha de operacion del negocio. Esa la rueda sp_n_ConsultaConstantes
    /// en el cierre del dia y no se toca desde aqui.
    /// </summary>
    [HttpPut("constantes/fecha-operacion-pagos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarFechaOperacionPagos(
        [FromBody] ActualizarFechaOperacionPagosRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });

        var sinPermiso = await ExigirPermisoAsync(PermisosFechaPagos, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.ActualizarFechaOperacionPagosAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Fecha de operacion de pagos actualizada.", data });
    }

    /// <summary>
    /// Apunta que el usuario sigue trabajando: es el latido de la sesion de Mac31,
    /// con el que la pantalla de usuarios conectados sabe quien esta dentro.
    ///
    /// No lleva permiso propio: cualquiera con sesion valida puede decir que sigue
    /// vivo, y el id que se apunta es el del token — no el del cuerpo, como en
    /// legacy, donde se podia mandar actividad a nombre de otra persona.
    /// </summary>
    [HttpPut("constantes/actividad")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RegistrarActividad(CancellationToken ct)
    {
        var data = await _service.RegistrarActividadAsync(IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Actividad registrada.", data });
    }

    /// <summary>
    /// Lee los parametros guardados con los que se abre un reporte.
    ///
    /// El procedimiento filtra SOLO por id. Legacy exigia tambien el usuario y el
    /// nombre del equipo, los validaba y no los mandaba: pedia tres datos y filtraba
    /// por uno. Aqui se pide lo que de verdad filtra y el candado esta en el
    /// permiso.
    /// </summary>
    [HttpGet("parametros/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarParametros([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosParametrosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.ConsultarParametrosAsync(id, ct);
        return Ok(new { ok = true, message = "Parametros consultados.", data, total = data.Count });
    }

    /// <summary>
    /// Guarda los veinte parametros con los que se va a abrir un reporte.
    ///
    /// Los nulos se convierten a cadena vacia: en legacy cualquiera de los veinte
    /// en nulo tiraba la llamada con una excepcion de argumento nulo, asi que el
    /// cliente tenia que mandar los veinte aunque usara tres.
    ///
    /// El nombre del equipo sale del token y no del cuerpo, porque es parte de la
    /// llave con la que el reporte recupera sus parametros: dejarlo viajar en el
    /// cuerpo permite escribir sobre los parametros de otra maquina.
    /// </summary>
    [HttpPut("parametros")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GuardarParametros([FromBody] GuardarParametrosRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        if (request.IdReporte <= 0)
            return BadRequest(new { ok = false, message = "El reporte es requerido." });
        if (request.EnOrden().Any(x => x.Length > 4000))
            return BadRequest(new { ok = false, message = "Un parametro no puede pasar de 4000 caracteres." });

        var sinPermiso = await ExigirPermisoAsync(PermisosParametrosGuardar, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.GuardarParametrosAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Parametros guardados.", data });
    }

    /// <summary>
    /// Registra o actualiza una huella.
    ///
    /// Es un PUT porque el procedimiento es un upsert (sp_n_ActualizarHuella sirve
    /// para las dos cosas). Las plantillas llegan en base64 y van a la base como
    /// varbinary(MAX) explicito: con el tipo sin acotar, ADO.NET puede inferir
    /// varbinary(8000) y recortar la plantilla en silencio — una huella truncada no
    /// identifica a nadie y el error no aparece al guardarla, aparece semanas
    /// despues cuando alguien no puede checar.
    ///
    /// Exactamente UNO de los tres (usuario, empleado o cliente) lleva valor: la
    /// huella es de alguien, y con dos no se sabria de quien.
    /// </summary>
    [HttpPut("huellas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GuardarHuella([FromBody] GuardarHuellaRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });

        var duenos = new[] { request.IdUsuario, request.IdEmpleado, request.IdCliente }.Count(x => x > 0);
        if (duenos == 0)
            return BadRequest(new { ok = false, message = "La huella tiene que ser de un usuario, de un empleado o de un cliente." });
        if (duenos > 1)
            return BadRequest(new { ok = false, message = "La huella es de uno solo: usuario, empleado o cliente." });

        if (request.IdMano <= 0)
            return BadRequest(new { ok = false, message = "La mano es requerida." });
        if (request.IdDedo <= 0)
            return BadRequest(new { ok = false, message = "El dedo es requerido." });
        if (string.IsNullOrWhiteSpace(request.Huella))
            return BadRequest(new { ok = false, message = "La plantilla de la huella es requerida." });
        if (request.IdStatus is not (1 or 2))
            return BadRequest(new { ok = false, message = "El estatus solo puede ser 1 (activa) o 2 (cancelada)." });

        var sinPermiso = await ExigirPermisoAsync(PermisosHuellas, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.GuardarHuellaAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        if (data is null)
            return BadRequest(new { ok = false, message = "La plantilla de la huella no es base64 valido." });

        return Responder(data, "Huella guardada.");
    }

    /// <summary>
    /// Apunta un intento de contrasena de autorizacion: que forma y que proceso la
    /// pidieron, si la escrita era correcta y cual se escribio.
    ///
    /// Guarda la contrasena escrita porque asi es la tabla de legacy y Mac31
    /// escribe en la misma: cambiarlo solo desde el web dejaria media bitacora con
    /// dato y media sin. Queda dicho para que no parezca un descuido.
    /// </summary>
    [HttpPost("contrasenas-capturadas")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegistrarContrasenaCapturada(
        [FromBody] RegistrarContrasenaCapturadaRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        if (request.IdForma <= 0)
            return BadRequest(new { ok = false, message = "La forma es requerida." });
        if (string.IsNullOrWhiteSpace(request.Forma))
            return BadRequest(new { ok = false, message = "El nombre de la forma es requerido." });
        if (request.Forma.Trim().Length > 300)
            return BadRequest(new { ok = false, message = "El nombre de la forma no puede pasar de 300 caracteres." });
        if (request.Contrasena != null && request.Contrasena.Length > 300)
            return BadRequest(new { ok = false, message = "La contrasena no puede pasar de 300 caracteres." });

        var sinPermiso = await ExigirPermisoAsync(PermisosBitacora, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.RegistrarContrasenaCapturadaAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return data.Guardo
            ? StatusCode(StatusCodes.Status201Created, new { ok = true, message = "Intento registrado.", data })
            : Responder(data, "Intento registrado.");
    }

    private IActionResult Responder(SeguridadEscrituraResponse r, string mensajeExito)
        => Ok(new
        {
            ok = r.Guardo,
            message = r.Guardo
                ? mensajeExito
                : (string.IsNullOrWhiteSpace(r.Mensaje) ? "La base rechazo la operacion." : r.Mensaje),
            data = r
        });
}
