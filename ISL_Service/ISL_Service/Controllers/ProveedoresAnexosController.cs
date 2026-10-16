using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Las pestanas de la ficha del proveedor: apoderados, domicilios y documentos.
///
/// SOLO LECTURA: los `sp_n_InsertarProveedor*` y `sp_n_ActualizarProveedor*`
/// existen y no se exponen aqui.
///
/// REUSA `proveedores.ver`, que ya existe en el catalogo de permisos del web y
/// significa exactamente esto: ver la ficha del proveedor. Inventar
/// `proveedores.apoderados.ver` partiria en tres un permiso que nadie va a
/// querer dar por partes — quien ve al proveedor ve sus domicilios.
///
/// Se le suma la pareja del canal movil (ver la nota del primer atributo).
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
public class ProveedoresAnexosController : ControllerBase
{
    private readonly IProveedoresAnexosService _service;

    public ProveedoresAnexosController(IProveedoresAnexosService service)
    {
        _service = service;
    }

    /// <summary>
    /// Apoderados del proveedor y el puesto con el que firman.
    /// </summary>
    /// <param name="texto">
    /// Busca por PREFIJO del nombre (LIKE 'texto%'), no por contenido: asi lo
    /// hace el SP.
    /// </param>
    /// <param name="esRepresentanteLegal">
    /// true deja solo los que lo son. Sin valor, o false, no filtra: el SP
    /// compara `&lt;&gt; 0` y "los que NO lo son" no se puede pedir.
    /// </param>
    [HttpGet("apoderados")]
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
    public async Task<IActionResult> Apoderados(
        [FromQuery] int id = 0,
        [FromQuery] int idProveedor = 0,
        [FromQuery] int idStatus = 0,
        [FromQuery] string? texto = null,
        [FromQuery] bool? esRepresentanteLegal = null,
        [FromQuery] bool? esRepresentanteTecnico = null,
        [FromQuery] bool exacto = false,
        CancellationToken ct = default)
    {
        var data = await _service.ConsultarApoderadosAsync(
            id, idProveedor, idStatus, texto, esRepresentanteLegal, esRepresentanteTecnico, exacto, ct);
        return Ok(new { ok = true, message = "Apoderados consultados.", data });
    }

    /// <summary>
    /// Domicilios del proveedor, con la colonia ya resuelta.
    /// </summary>
    /// <remarks>
    /// OJO: todos los joins de colonia, ciudad, municipio y estado son INNER
    /// JOIN. Un domicilio con una colonia que ya no existe en el catalogo NO
    /// aparece en el listado — ni aqui ni en Mac31. Si un proveedor "pierde" un
    /// domicilio, el dato esta: lo que falta es su colonia.
    ///
    /// El filtro de texto busca por PREFIJO (LIKE 'texto%').
    /// </remarks>
    [HttpGet("domicilios")]
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
    public async Task<IActionResult> Domicilios(
        [FromQuery] int id = 0,
        [FromQuery] int idProveedor = 0,
        [FromQuery] int idTipoDomicilio = 0,
        [FromQuery] int idStatus = 0,
        [FromQuery] string? texto = null,
        [FromQuery] bool exacto = false,
        CancellationToken ct = default)
    {
        var data = await _service.ConsultarDomiciliosAsync(id, idProveedor, idTipoDomicilio, idStatus, texto, exacto, ct);
        return Ok(new { ok = true, message = "Domicilios consultados.", data });
    }

    /// <summary>
    /// Documentos del proveedor.
    /// </summary>
    /// <remarks>
    /// Para saber si hay archivo adjunto, mirar `archivo`. NO se publica el
    /// `ArchivoFtm` ('SI'/'NO') que calcula el SP: su CASE solo compara contra
    /// cadena vacia, asi que con el archivo en NULL contesta 'SI' y la pantalla
    /// ofrece descargar algo que no existe.
    ///
    /// El filtro de texto busca por PREFIJO (LIKE 'texto%').
    /// </remarks>
    [HttpGet("documentos")]
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
    public async Task<IActionResult> Documentos(
        [FromQuery] int id = 0,
        [FromQuery] int idProveedor = 0,
        [FromQuery] int idStatus = 0,
        [FromQuery] string? texto = null,
        [FromQuery] bool exacto = false,
        CancellationToken ct = default)
    {
        var data = await _service.ConsultarDocumentosAsync(id, idProveedor, idStatus, texto, exacto, ct);
        return Ok(new { ok = true, message = "Documentos consultados.", data });
    }
}
