using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Las huellas registradas de usuarios, empleados y clientes.
///
/// EL PERMISO ES EL DE MAC31, Y ESO NO ES PEREZA
/// ---------------------------------------------
/// Se exige `empleados.huellas`, que es la clave que el puente de permisos
/// deriva del boton btnHuellas de la forma CONSULTAR EMPLEADOS de Mac31
/// (PermissionService, EmpleadosLegacyPermissionMap). Inventar aqui un
/// `huellas.ver` no habria servido de nada: la familia `empleados.*` la gobierna
/// Mac31 y todo lo que no este en su lista blanca se BORRA del snapshot en el
/// siguiente recalculo, sin error y sin rastro (seccion 2.8 del resumen de
/// arquitectura). O sea: se habria podido dar desde la pantalla de permisos del
/// web y habria dejado de funcionar al dia siguiente.
///
/// Que las huellas de CLIENTES tambien queden detras del permiso de empleados es
/// una consecuencia de eso, y se asume: el permiso significa "puede ver datos
/// biometricos", y en Mac31 es un solo boton.
///
/// SOLO LECTURA: `sp_n_ActualizarHuella` existe y no se expone. Registrar una
/// huella desde el navegador necesita el lector, y eso es la app, no el web.
/// </summary>
[ApiController]
[Route("api/huellas")]
[Authorize]
public class HuellasController : PermisoControllerBase
{
    private static readonly string[] PermisosVer = { "empleados.huellas" };

    private readonly IHuellasService _service;

    public HuellasController(
        IHuellasService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    /// <summary>
    /// Huellas de un usuario, un empleado o un cliente.
    /// </summary>
    /// <param name="origen">`usuario`, `empleado` o `cliente`.</param>
    /// <param name="id">A quien. Se ignora si <paramref name="todas"/> es true.</param>
    /// <param name="todas">
    /// true = todas las de ese origen. En empleados, el SP ademas limita a los
    /// activos; en usuarios y clientes no. Es asi en Mac31.
    /// </param>
    /// <response code="200">Huellas consultadas</response>
    /// <response code="400">Origen desconocido, o ni id ni todas</response>
    /// <response code="403">Falta el permiso de huellas</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Consultar(
        [FromQuery] string? origen,
        [FromQuery] int id = 0,
        [FromQuery] bool todas = false,
        CancellationToken ct = default)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        var cual = (origen ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "usuario" => HuellaOrigen.Usuario,
            "empleado" => HuellaOrigen.Empleado,
            "cliente" => HuellaOrigen.Cliente,
            _ => (HuellaOrigen?)null
        };

        // Se exige el origen en vez de aceptar "cualquiera": sin origen el SP no
        // emite ningun result set y legacy contestaba 500. Un 400 que dice que
        // falta el origen es mas util que un error de base.
        if (cual is null)
            return BadRequest(new { message = "origen requerido: usuario, empleado o cliente." });

        if (!todas && id <= 0)
            return BadRequest(new { message = "Falta id, o pide todas=true." });

        var data = await _service.ConsultarAsync(cual.Value, id, todas, ct);

        // Son identificadores biometricos: no se guardan en el camino.
        Response.Headers["Cache-Control"] = "no-store";

        return Ok(new { ok = true, message = "Huellas consultadas.", data, total = data.Count });
    }
}
