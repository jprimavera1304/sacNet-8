using ISL_Service.Application.DTOs.VentasUsados;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Modificación de usados de una remisión, con las reglas de Mac31 y llamando a
/// sus mismos procedimientos. Ver VentasUsadosService para el porqué de cada regla.
/// </summary>
[ApiController]
[Route("api/ventas/usados")]
[Authorize]
public class VentasUsadosController : PermisoControllerBase
{
    /*
      El mismo permiso que en Mac31 abre btnUsados de Consultar ventas: en la
      base es n_Procesos IDProceso 14 (forma ConsultarVentas, proceso btnUsados),
      y en PermissionService ya esta mapeado a "ventas.usados". No se inventa
      ninguno nuevo.

      Mac31 tiene ademas un permiso aparte para el boton GUARDAR de esta pantalla
      (n_Procesos 5085, forma VentasUsados, proceso btnGuardar) con los mismos
      identificadores en las dos empresas. No se usa aqui porque el puente de
      permisos heredados esta amarrado a UNA forma por modulo —la de Consultar
      ventas— y agregar una segunda forma obligaria a tocar el mecanismo entero.
      Queda anotado como pendiente: hoy quien puede abrir la pantalla puede
      guardar, igual que en la practica pasa en Mac31.
    */
    private static readonly string[] PermisosUsados = { "ventas.usados" };

    private readonly IVentasUsadosService _service;

    public VentasUsadosController(
        IVentasUsadosService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    /// <summary>
    /// Todo lo que pinta la pantalla de una remisión: cabecera, totales, los
    /// cargos de usados, los créditos (con el catálogo completo) y el histórico.
    /// No escribe nada.
    /// </summary>
    /// <response code="200">La pantalla</response>
    /// <response code="400">No se mandó remisión</response>
    /// <response code="409">La remisión no existe, está cancelada o no tiene usados</response>
    [HttpPost("consultar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Consultar([FromBody] VentasUsadosConsultarRequest? body, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosUsados, ct);
        if (sinPermiso is not null) return sinPermiso;

        if (body is null)
            return BadRequest(new { ok = false, message = "Body requerido." });

        var data = await _service.ConsultarAsync(body.IdVenta, ct);
        return Ok(new { ok = true, message = "Usados de la remisión.", data });
    }

    /// <summary>
    /// Guarda las nuevas cantidades de usados a crédito. Vuelve a validar TODO
    /// contra la base antes de escribir: los costos y los totales se recalculan
    /// aquí, nunca se toman del navegador.
    /// </summary>
    /// <response code="200">Se aplicó el ajuste</response>
    /// <response code="400">Falta la remisión o los renglones</response>
    /// <response code="409">Se rebasa el máximo de diferencia, no hay cambios, o legacy se negó</response>
    [HttpPost("guardar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Guardar([FromBody] VentasUsadosGuardarRequest? body, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosUsados, ct);
        if (sinPermiso is not null) return sinPermiso;

        if (body is null)
            return BadRequest(new { ok = false, message = "Body requerido." });

        /*
          Quien ajusta y desde donde salen del TOKEN, nunca del cuerpo. Es la
          firma que queda en [Ventas Usados Creditos Log] y en EmpresaUsadoLog:
          si viajara en el body, cualquiera podria mover cascos a nombre de otro.
        */
        var data = await _service.GuardarAsync(body, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = data.Mensaje, data });
    }
}
