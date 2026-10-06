using ISL_Service.Application.DTOs.Obra;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Obra publica: los contratos MC_* de Mac31 y sus catalogos.
///
/// SOLO LECTURA, Y A PROPOSITO. En la base existen los `sp_n_Insertar_MC*` y
/// `sp_n_Actualizar_MC*` de los doce catalogos y de los contratos. No se
/// exponen aqui: dar de alta un contrato de obra toca importes, periodos y
/// estimaciones, y eso es una entrega aparte con su permiso `.crear` y su
/// `sp_w_`. Un controlador que empieza a crecer por el lado de la escritura
/// termina publicando el alta antes de haber decidido quien puede firmarla.
///
/// POR QUE POST -> GET. En legacy eran POST porque su handler exigia el token
/// dentro del cuerpo JSON (ver COMO-HABLARLE-A-LEGACY.md). Aqui el token va en
/// la cabecera, asi que una consulta vuelve a ser un GET y se puede cachear,
/// enlazar y leer en el log.
///
/// POR QUE `obra` Y NO `catalogos`. Los desplegables de captura viven en
/// api/catalogos con permiso `catalogos.*`. Esto no son desplegables: son las
/// tablas de un modulo de obra publica que la mayoria de las empresas no tiene
/// prendido (Constantes.EsAlcaldiaMC). Meterlos ahi daria acceso a los
/// contratos a cualquiera que pudiera ver un combo de ciudades.
/// </summary>
[ApiController]
[Route("api/obra")]
[Authorize]
public class ObraController : ControllerBase
{
    private readonly IObraService _service;

    public ObraController(IObraService service)
    {
        _service = service;
    }

    /// <summary>
    /// Lo que los doce endpoints reciben igual. `idStatus` 0 = todos (es lo que
    /// entiende fn_RegresaTablaStatus y lo que manda Mac31 cuando no se filtra),
    /// 1 = activos, 2 = inactivos.
    /// </summary>
    private static ObraCatalogoFiltro Filtro(int id, int idStatus, string? texto, bool exacto) => new()
    {
        Id = id,
        IDStatus = idStatus,
        Texto = texto,
        Exacto = exacto
    };

    // ---------------------------------------------------------------- catalogos

    /// <summary>Gerentes de obra.</summary>
    [HttpGet("catalogos/gerentes")]
    [Authorize(Policy = "perm:obra.catalogos.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Gerentes(
        [FromQuery] int id = 0,
        [FromQuery] int idTipoGerente = 0,
        [FromQuery] int idStatus = 0,
        [FromQuery] string? texto = null,
        [FromQuery] bool exacto = false,
        CancellationToken ct = default)
    {
        var data = await _service.ConsultarGerentesAsync(Filtro(id, idStatus, texto, exacto), idTipoGerente, ct);
        return Ok(new { ok = true, message = "Gerentes consultados.", data });
    }

    /// <summary>
    /// Puestos de obra.
    /// </summary>
    /// <param name="esRepresentanteLegal">
    /// true deja solo los que lo son. Sin valor no filtra. false tampoco
    /// filtra: el SP compara `&lt;&gt; 0`, asi que "los que NO lo son" no se
    /// puede pedir — tampoco en Mac31.
    /// </param>
    [HttpGet("catalogos/puestos")]
    [Authorize(Policy = "perm:obra.catalogos.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Puestos(
        [FromQuery] int id = 0,
        [FromQuery] int idStatus = 0,
        [FromQuery] string? texto = null,
        [FromQuery] bool? esRepresentanteLegal = null,
        [FromQuery] bool? esRepresentanteTecnico = null,
        [FromQuery] bool exacto = false,
        CancellationToken ct = default)
    {
        var data = await _service.ConsultarPuestosAsync(
            Filtro(id, idStatus, texto, exacto), esRepresentanteLegal, esRepresentanteTecnico, ct);
        return Ok(new { ok = true, message = "Puestos consultados.", data });
    }

    /// <summary>
    /// Residentes. `Tipo` lo calcula el SP: SUPERVISION cuando EsSupervision = 1,
    /// OBRA en cualquier otro caso.
    /// </summary>
    [HttpGet("catalogos/residentes")]
    [Authorize(Policy = "perm:obra.catalogos.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Residentes(
        [FromQuery] int id = 0,
        [FromQuery] int idPuesto = 0,
        [FromQuery] int idStatus = 0,
        [FromQuery] string? texto = null,
        [FromQuery] bool? esSupervision = null,
        [FromQuery] bool? esObra = null,
        [FromQuery] bool exacto = false,
        CancellationToken ct = default)
    {
        var data = await _service.ConsultarResidentesAsync(
            Filtro(id, idStatus, texto, exacto), idPuesto, esSupervision, esObra, ct);
        return Ok(new { ok = true, message = "Residentes consultados.", data });
    }

    /// <summary>Tipos de contratacion.</summary>
    [HttpGet("catalogos/tipos-contratacion")]
    [Authorize(Policy = "perm:obra.catalogos.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<IActionResult> TiposContratacion(
        [FromQuery] int id = 0, [FromQuery] int idStatus = 0, [FromQuery] string? texto = null,
        [FromQuery] bool exacto = false, CancellationToken ct = default)
        => Tipo(ObraTipoCatalogo.TiposContratacion, "Tipos de contratacion consultados.", id, idStatus, texto, exacto, ct);

    /// <summary>
    /// Tipos de documento de obra.
    /// </summary>
    /// <remarks>
    /// `exacto` NO aplica aqui. sp_n_Consultar_MCTiposDocumento recibe
    /// @Identico y no lo usa: su filtro de texto es siempre LIKE %texto%. Se
    /// deja el parametro para que las doce rutas tengan la misma forma, con esta
    /// nota, en vez de callar que en este caso no hace nada.
    /// </remarks>
    [HttpGet("catalogos/tipos-documento")]
    [Authorize(Policy = "perm:obra.catalogos.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<IActionResult> TiposDocumento(
        [FromQuery] int id = 0, [FromQuery] int idStatus = 0, [FromQuery] string? texto = null,
        [FromQuery] bool exacto = false, CancellationToken ct = default)
        => Tipo(ObraTipoCatalogo.TiposDocumento, "Tipos de documento consultados.", id, idStatus, texto, exacto, ct);

    /// <summary>
    /// Tipos de obra.
    /// </summary>
    /// <remarks>
    /// El SP no expone EsObra / EsServicio / EsProyecto de MC_TiposObra, aunque
    /// las columnas existen: quien las necesita es sp_n_Consultar_MCContratos,
    /// que las trae ya resueltas en el contrato. No se agregan por SQL propio
    /// para no tener dos fuentes de la misma verdad.
    /// </remarks>
    [HttpGet("catalogos/tipos-obra")]
    [Authorize(Policy = "perm:obra.catalogos.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<IActionResult> TiposObra(
        [FromQuery] int id = 0, [FromQuery] int idStatus = 0, [FromQuery] string? texto = null,
        [FromQuery] bool exacto = false, CancellationToken ct = default)
        => Tipo(ObraTipoCatalogo.TiposObra, "Tipos de obra consultados.", id, idStatus, texto, exacto, ct);

    /// <summary>Tipos de recurso (local / federal). Tampoco expone EsLocal / EsFederal.</summary>
    [HttpGet("catalogos/tipos-recurso")]
    [Authorize(Policy = "perm:obra.catalogos.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<IActionResult> TiposRecurso(
        [FromQuery] int id = 0, [FromQuery] int idStatus = 0, [FromQuery] string? texto = null,
        [FromQuery] bool exacto = false, CancellationToken ct = default)
        => Tipo(ObraTipoCatalogo.TiposRecurso, "Tipos de recurso consultados.", id, idStatus, texto, exacto, ct);

    private async Task<IActionResult> Tipo(
        ObraTipoCatalogo tipo, string mensaje, int id, int idStatus, string? texto, bool exacto, CancellationToken ct)
    {
        var data = await _service.ConsultarTipoAsync(tipo, Filtro(id, idStatus, texto, exacto), ct);
        return Ok(new { ok = true, message = mensaje, data });
    }

    /// <summary>Unidades de medida de obra.</summary>
    [HttpGet("catalogos/unidades-medida")]
    [Authorize(Policy = "perm:obra.catalogos.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UnidadesMedida(
        [FromQuery] int id = 0,
        [FromQuery] int idStatus = 0,
        [FromQuery] string? texto = null,
        [FromQuery] bool exacto = false,
        CancellationToken ct = default)
    {
        var data = await _service.ConsultarUnidadesMedidaAsync(Filtro(id, idStatus, texto, exacto), ct);
        return Ok(new { ok = true, message = "Unidades de medida consultadas.", data });
    }

    /// <summary>
    /// Proveedores de obra (MC_Proveedores). NO es el catalogo de proveedores de
    /// compras: son dos tablas distintas.
    /// </summary>
    /// <param name="numero">
    /// Busqueda por PREFIJO (LIKE 'numero%'), no por contenido: asi lo hace el
    /// SP, al contrario que los demas filtros de texto.
    /// </param>
    [HttpGet("proveedores")]
    [Authorize(Policy = "perm:obra.proveedores.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Proveedores(
        [FromQuery] int id = 0,
        [FromQuery] int idStatus = 0,
        [FromQuery] string? numero = null,
        [FromQuery] string? nombre = null,
        [FromQuery] string? apellidoPaterno = null,
        [FromQuery] string? apellidoMaterno = null,
        [FromQuery] string? rfc = null,
        [FromQuery] bool exacto = false,
        CancellationToken ct = default)
    {
        var data = await _service.ConsultarProveedoresAsync(new ObraProveedorFiltro
        {
            IDProveedor = id,
            IDStatus = idStatus,
            Numero = numero,
            Nombre = nombre,
            ApellidoPaterno = apellidoPaterno,
            ApellidoMaterno = apellidoMaterno,
            Rfc = rfc,
            Exacto = exacto
        }, ct);

        return Ok(new { ok = true, message = "Proveedores de obra consultados.", data });
    }

    // ---------------------------------------------------------------- contratos

    /// <summary>Grupos de conceptos de un contrato de obra.</summary>
    [HttpGet("contratos/grupos")]
    [Authorize(Policy = "perm:obra.contratos.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ContratoGrupos(
        [FromQuery] int id = 0,
        [FromQuery] int idContrato = 0,
        [FromQuery] int idStatus = 0,
        [FromQuery] string? texto = null,
        [FromQuery] bool exacto = false,
        CancellationToken ct = default)
    {
        var data = await _service.ConsultarContratoGruposAsync(id, idContrato, Filtro(0, idStatus, texto, exacto), ct);
        return Ok(new { ok = true, message = "Grupos del contrato consultados.", data });
    }

    /// <summary>
    /// Conceptos de un contrato de obra.
    /// </summary>
    /// <remarks>
    /// OJO CON LOS RENGLONES DE CABECERA. El SP mete, ademas de los conceptos,
    /// un renglon sintetico por grupo (IDContratoConcepto = 0, Concepto = el
    /// nombre del grupo) para que la rejilla de Mac31 pinte el titulo. Esos
    /// renglones SOLO aparecen si se piden con idContrato e idStatus concretos:
    /// su propio INSERT compara `IDStatus = @IDStatus` y `IDContrato =
    /// @IDContrato` literales, sin pasar por fn_RegresaTablaStatus. Con los
    /// defaults (0 y 0) la respuesta trae solo conceptos reales. No se filtran
    /// aqui porque son los mismos datos que ve Mac31; el front decide si pinta
    /// el titulo o se lo arma solo agrupando por `idContratoGrupo`.
    /// </remarks>
    [HttpGet("contratos/conceptos")]
    [Authorize(Policy = "perm:obra.contratos.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ContratoConceptos(
        [FromQuery] int id = 0,
        [FromQuery] int idContratoGrupo = 0,
        [FromQuery] int idContrato = 0,
        [FromQuery] int idStatus = 0,
        [FromQuery] string? texto = null,
        [FromQuery] bool exacto = false,
        CancellationToken ct = default)
    {
        var data = await _service.ConsultarContratoConceptosAsync(
            id, idContratoGrupo, idContrato, Filtro(0, idStatus, texto, exacto), ct);
        return Ok(new { ok = true, message = "Conceptos del contrato consultados.", data });
    }

    /// <summary>
    /// Estimaciones de un contrato de obra.
    /// </summary>
    /// <remarks>
    /// Legacy tenia aqui un filtro `Contrato` (por texto) que no servia para
    /// nada y hacia dano: su service, al recibirlo, RECONSTRUIA el DTO dejando
    /// solo ese campo —tirando idContrato, idStatus y nombre— y despues el
    /// repositorio ni lo mandaba al SP, porque el SP no tiene ese parametro
    /// (MCContratoEstimacionService.cs:38-45). Resultado: buscar por nombre de
    /// contrato devolvia TODAS las estimaciones de TODOS los contratos. No se
    /// migra: un filtro que amplia el resultado es peor que no tenerlo.
    ///
    /// `idContratoEstimacion` sin `idContrato` si funciona: el SP deduce el
    /// contrato a partir de la estimacion.
    /// </remarks>
    [HttpGet("contratos/estimaciones")]
    [Authorize(Policy = "perm:obra.contratos.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ContratoEstimaciones(
        [FromQuery] int id = 0,
        [FromQuery] int idContrato = 0,
        [FromQuery] int idStatus = 0,
        [FromQuery] string? texto = null,
        [FromQuery] bool exacto = false,
        CancellationToken ct = default)
    {
        var data = await _service.ConsultarContratoEstimacionesAsync(
            id, idContrato, Filtro(0, idStatus, texto, exacto), ct);
        return Ok(new { ok = true, message = "Estimaciones del contrato consultadas.", data });
    }
}
