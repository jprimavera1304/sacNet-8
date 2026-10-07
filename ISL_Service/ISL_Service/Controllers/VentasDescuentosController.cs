using ISL_Service.Application.DTOs.VentasDescuentos;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/*
  DESCUENTOS Y AJUSTES DE VENTA

  La familia `descuentos.*` es nueva y es la misma que estreno la ola 2 para las
  consultas (`descuentos.ver`). El motivo esta escrito alla y sigue valiendo: un
  descuento es DINERO, no un desplegable, asi que no cabe en `catalogos.ver`. Es
  la misma razon por la que la ola 1 separo `catalogos.galleros.ver`.

  AUTORIZAR LLEVA SU PROPIO PERMISO (`descuentos.ajustes.autorizar`), separado del
  de solicitar. Es el reparto clasico y el unico que protege algo: quien pide un
  descuento sobre una remision no puede ser quien lo aprueba. En legacy los dos
  ids de usuario —el que autoriza y el que rechaza— viajan en el cuerpo, asi que
  ese reparto no existia: cualquiera podia mandar el id del jefe.
*/
[ApiController]
[Route("api/ventas/descuentos")]
[Authorize]
public class VentasDescuentosController : PermisoControllerBase
{
    private static readonly string[] PermisoVer = { "descuentos.ver" };
    private static readonly string[] PermisoEditar = { "descuentos.editar" };
    private static readonly string[] PermisoEliminar = { "descuentos.eliminar" };
    private static readonly string[] PermisoSimular = { "descuentos.simular" };
    private static readonly string[] PermisoAjusteVer = { "descuentos.ajustes.ver", "descuentos.ver" };
    private static readonly string[] PermisoAjusteCrear = { "descuentos.ajustes.crear" };
    private static readonly string[] PermisoAjusteAutorizar = { "descuentos.ajustes.autorizar" };

    private readonly IVentasDescuentosService _service;

    public VentasDescuentosController(
        IVentasDescuentosService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    /* ------------------------------ Catalogo ------------------------------ */

    /// <summary>
    /// Crea un esquema de descuento. El procedimiento de legacy no recibe el
    /// nombre, asi que el renglon nace en blanco y el mensaje lo dice.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CrearAsync([FromBody] DescuentoCrearRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoEditar, ct);
        if (veto is not null) return veto;

        var data = await _service.CrearDescuentoAsync(request ?? new DescuentoCrearRequest(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Descuento creado."), data });
    }

    /// <summary>Modifica un esquema de descuento.</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ActualizarAsync([FromBody] DescuentoActualizarRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoEditar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.ActualizarDescuentoAsync(request, ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Descuento actualizado."), data });
    }

    /// <summary>Elimina un esquema de descuento.</summary>
    [HttpPost("eliminar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> EliminarAsync([FromBody] DescuentoIdRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoEliminar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.EliminarDescuentoAsync(request, ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Descuento eliminado."), data });
    }

    /// <summary>Consulta los esquemas de descuento.</summary>
    [HttpPost("consultar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarAsync([FromBody] DescuentoIdRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoVer, ct);
        if (veto is not null) return veto;

        var data = await _service.ConsultarDescuentosAsync(request ?? new DescuentoIdRequest(), ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Descuentos.", data });
    }

    /// <summary>
    /// Simula un descuento. OJO: escribe — el procedimiento inserta el renglon de
    /// simulacion del usuario y el equipo si no existe.
    /// </summary>
    [HttpPost("simular")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SimularAsync([FromBody] DescuentoSimularRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoSimular, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.SimularDescuentoAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Descuento simulado."), data });
    }

    /* ------------------------------- Detalle ------------------------------- */

    /// <summary>
    /// Agrega un renglon al detalle de un esquema.
    ///
    /// POR QUE `detalle/alta` Y NO `detalle` A SECAS
    /// --------------------------------------------
    /// Porque `POST api/ventas/descuentos/detalle` YA EXISTE y es la CONSULTA:
    /// `VentasDescuentosConsultaController.Detalle`, migrada en la ola 2 y
    /// comparada contra Legacy (129 renglones). Las dos acciones son POST sobre la
    /// misma plantilla, asi que ASP.NET no puede elegir y contesta
    /// AmbiguousMatchException: un 500 con cuerpo vacio, sin mensaje y sin rastro
    /// en la respuesta.
    ///
    /// Y no rompia una, rompia LAS DOS: la consulta tambien dejaba de responder.
    /// Los dos casos del banco que la cubren (ola2-descuentos-detalle y
    /// ola2-descuentos-detalle-marcas) pasaron de OK a 500 sin que nadie tocara su
    /// codigo. Las dos olas se escribieron en ramas separadas y la colision solo
    /// aparece al juntarlas, que es justo donde nadie la estaba mirando.
    ///
    /// Se movio ESTA y no la consulta porque la consulta es anterior, esta
    /// comparada y es la que el web ya llama. El sufijo va a juego con los
    /// hermanos de este mismo controlador: `detalle/eliminar`, `ajustes/status`.
    /// </summary>
    [HttpPost("detalle/alta")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CrearDetalleAsync([FromBody] DescuentoDetalleCrearRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoEditar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CrearDetalleAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Renglon agregado."), data });
    }

    /// <summary>Modifica un renglon del detalle.</summary>
    [HttpPut("detalle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ActualizarDetalleAsync([FromBody] DescuentoDetalleActualizarRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoEditar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.ActualizarDetalleAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Renglon actualizado."), data });
    }

    /// <summary>Elimina un renglon del detalle.</summary>
    [HttpPost("detalle/eliminar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> EliminarDetalleAsync([FromBody] DescuentoDetalleEliminarRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoEliminar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.EliminarDetalleAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Renglon eliminado."), data });
    }

    /* -------------------------- Ajustes de venta -------------------------- */

    /// <summary>Solicita un descuento o ajuste sobre una remision emitida.</summary>
    [HttpPost("ajustes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CrearAjusteAsync([FromBody] VentaDescuentoAjusteCrearRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoAjusteCrear, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CrearAjusteAsync(request, IdUsuarioLegacy(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Ajuste solicitado."), data });
    }

    /// <summary>
    /// Autoriza o rechaza un ajuste. Quien lo hace sale del token: en legacy ese
    /// id venia en el cuerpo y es el que despues se audita.
    /// </summary>
    [HttpPost("ajustes/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CambiarStatusAjusteAsync([FromBody] VentaDescuentoAjusteStatusRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoAjusteAutorizar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CambiarStatusAjusteAsync(request, IdUsuarioLegacy(), ct);
        var hecho = request.Autorizar ? "Ajuste autorizado." : "Ajuste rechazado.";
        return Ok(new { ok = true, message = Mensaje(data.Hecho, hecho), data });
    }

    /// <summary>Simula el ajuste sin guardarlo.</summary>
    [HttpPost("ajustes/simular")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SimularAjusteAsync([FromBody] VentaDescuentoAjusteSimularRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoAjusteCrear, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.SimularAjusteAsync(request, IdUsuarioLegacy(), ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Simulacion del ajuste.", data });
    }

    /// <summary>Consulta los ajustes de descuento.</summary>
    [HttpPost("ajustes/consultar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarAjustesAsync([FromBody] VentaDescuentoAjusteConsultaRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoAjusteVer, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.ConsultarAjustesAsync(request, IdUsuarioLegacy(), ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Ajustes de descuento.", data });
    }

    private static string Mensaje(bool hecho, string siSeHizo)
        => hecho ? siSeHizo : "El procedimiento rechazo la operacion.";
}
