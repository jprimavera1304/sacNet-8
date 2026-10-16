using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// La consulta del padron de proveedores: el GET raiz de api/proveedores, que
/// faltaba.
///
/// POR QUE UNA CLASE APARTE Y NO DENTRO DE ProveedoresAnexosController
/// -------------------------------------------------------------------
/// Tres clases comparten el prefijo api/proveedores: Admin (escrituras), Anexos
/// (las pestanas: apoderados, domicilios, documentos) y esta (el buscador).
/// El GET raiz no es un anexo del proveedor: es la lista de proveedores, o sea
/// lo que hay ANTES de tener uno. Meterlo en Anexos pondria la busqueda al lado
/// de los documentos de un proveedor concreto y quien venga a leer el buscador
/// tendria que pasar por tres consultas que no son suyas.
///
/// LA RUTA NO CHOCA. Antes de esta clase `GET api/proveedores` contestaba 405
/// (method not allowed): la ruta base existia, publicada por Admin y Anexos,
/// pero sin ningun GET en la raiz. Los dos atributos de ruta de aquellas clases
/// son todos con sufijo ("{id:int}", "apoderados", ...), asi que la raiz estaba
/// libre.
///
/// REUSA `proveedores.ver`, que ya existe y significa exactamente esto, y le
/// suma la pareja del canal movil (ver la nota del atributo).
/// </summary>
/// CENTROS DE SERVICIO: ESTE MODULO *NO* LOS SOPORTA. `soportaCentros = false`.
/// -------------------------------------------------------------------------
/// Queda declarado para que la capa de conexion conteste **409 con motivo** si
/// llega `X-Centro != 0`, y NUNCA datos de la matriz.
///
/// No es un pendiente, es una decision, y son cuatro razones medidas en las
/// bases locales:
///   1. El catalogo es de la MATRIZ. `SELECT COUNT(*) FROM Proveedores` da 52 en
///      Produccion_svr y 2 en MacZ; en MacZCS1, MacZCS2 y MacZCS7 da 2 en cada
///      una, que es la misma semilla de MacZ. Listar "los proveedores del
///      centro" seria ensenar un catalogo vacio, y eso se lee como una averia.
///   2. Un alta en un centro SE PIERDE: nace en la base del centro, donde nadie
///      captura compras, y la matriz no la ve.
///   3. `IDColonia`, `IDCondicionesCredito` e `IDDescuento` son ids DE LA BASE
///      donde vive el renglon: no cruzan. Y en Tauro hay filas de
///      CentrosServicio que apuntan a bases de Zaragoza (MacZCS7..MacZCS10).
///   4. El dinero es de otro modulo: `ProveedoresPagosController`, que tampoco
///      esta declarado. Partir el catalogo por centro y los pagos no dejaria
///      media operacion en dos bases.

[ApiController]
[Route("api/proveedores")]
[Authorize]
public class ProveedoresConsultaController : ControllerBase
{
    private readonly IProveedoresConsultaService _service;

    public ProveedoresConsultaController(IProveedoresConsultaService service)
    {
        _service = service;
    }

    /// <summary>
    /// Busca proveedores.
    /// </summary>
    /// <param name="idStatus">0 = todos, 1 = activos (default), 2 = inactivos.</param>
    /// <param name="idDescuento">
    /// Se acepta por simetria con la firma del SP, pero EL PROCEDIMIENTO NO LO
    /// USA: lo declara y nunca lo mete en su WHERE. No filtra aqui ni en Mac31.
    /// </param>
    /// <param name="exacto">true = igualdad; false = LIKE.</param>
    /// <response code="200">Proveedores consultados</response>
    [HttpGet]
    // EL PAR: la clave del web O la del movil. La politica `perm:a|b` se cobra
    // como un O, asi que sumar la del canal del telefono no le quita el acceso
    // a nadie: quien hoy entra con `proveedores.ver` sigue entrando igual. Lo
    // que habilita es lo contrario: poder dar el catalogo en el TELEFONO sin
    // darlo en la computadora, que es lo que ya hacen clientes y ventas.
    //
    // `app_movil.proveedores.ver` TODAVIA NO ESTA en el catalogo de
    // `PermissionService`: la siembra el agente de permisos (el bloque esta en
    // Movil/mac-mac-mobile/lib/modules/verticals/proveedores.permisos.md).
    // Hasta entonces la politica se comporta exactamente como antes.
    [Authorize(Policy = "perm:proveedores.ver|app_movil.proveedores.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Consultar(
        [FromQuery] int idProveedor = 0,
        [FromQuery] string? numero = null,
        [FromQuery] string? nombre = null,
        [FromQuery] string? apellidoPaterno = null,
        [FromQuery] string? apellidoMaterno = null,
        [FromQuery] string? rfc = null,
        [FromQuery] int idStatus = 1,
        [FromQuery] int idCondicionesCredito = 0,
        [FromQuery] int idDescuento = 0,
        [FromQuery] bool exacto = false,
        CancellationToken ct = default)
    {
        var data = await _service.ConsultarAsync(new ProveedoresConsultaFiltro
        {
            IDProveedor = idProveedor,
            Numero = numero,
            Nombre = nombre,
            ApellidoPaterno = apellidoPaterno,
            ApellidoMaterno = apellidoMaterno,
            Rfc = rfc,
            IDStatus = idStatus,
            IDCondicionesCredito = idCondicionesCredito,
            IDDescuento = idDescuento,
            Exacto = exacto
        }, ct);

        // El renglon trae limite de credito del proveedor: no se guarda en
        // ninguna cache intermedia.
        Response.Headers["Cache-Control"] = "no-store";

        return Ok(new { ok = true, message = "Proveedores consultados.", data, total = data.Count });
    }
}
