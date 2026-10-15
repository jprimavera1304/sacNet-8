using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Las pestanas de la ficha del cliente: bonificaciones por tipo de usado,
/// centros de servicio permitidos, datos de facturacion y bitacora de cambios.
///
/// SOLO LECTURA. Los `sp_n_ActualizarCliente*` y `sp_n_InsertarCliente*`
/// existen; no se exponen aqui. Cambiar un RFC o una bonificacion es una
/// entrega con su permiso `.editar`, su `sp_w_` y su firma de usuario.
///
/// POR QUE `clientes.ver` Y NO `ventas.cliente.ver`
/// -----------------------------------------------
/// `ventas.cliente.ver` ya existe, pero la familia `ventas.*` la gobierna Mac31:
/// lo que no este en la lista blanca del web se borra del snapshot en el
/// siguiente calculo (PermissionService, seccion 2.8 del resumen de
/// arquitectura). `ventas.cliente.ver` SI esta en la lista blanca, pero
/// significa otra cosa — "ver a quien le estoy vendiendo mientras capturo un
/// pedido"— y aqui se publican datos fiscales y una bitacora de quien cambio
/// que. Son dos decisiones distintas y tienen que poder darse por separado.
/// La familia `clientes.*` es del web, asi que lo que se de desde la pantalla de
/// permisos si se respeta.
/// </summary>
[ApiController]
[Route("api/clientes")]
[Authorize]
public class ClientesAnexosController : ControllerBase
{
    private readonly IClientesAnexosService _service;

    public ClientesAnexosController(IClientesAnexosService service)
    {
        _service = service;
    }

    /// <summary>
    /// Bonificaciones del cliente por tipo de casco usado.
    /// </summary>
    /// <remarks>
    /// Devuelve UN RENGLON POR TIPO DE USADO ACTIVO, con
    /// `idClienteBonificacion = 0` donde no hay bonificacion pactada: el SP
    /// recorre el catalogo y hace LEFT JOIN con lo del cliente. Es a proposito —
    /// la pantalla de Mac31 es una rejilla con todos los tipos para poder
    /// capturar el que falta.
    /// </remarks>
    /// <response code="400">idCliente obligatorio</response>
    [HttpGet("{idCliente:int}/bonificaciones")]
    [Authorize(Policy = "perm:clientes.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Bonificaciones([FromRoute] int idCliente, CancellationToken ct)
    {
        if (idCliente <= 0)
            return BadRequest(new { message = "idCliente requerido." });

        var data = await _service.ConsultarBonificacionesAsync(idCliente, ct);
        return Ok(new { ok = true, message = "Bonificaciones consultadas.", data });
    }

    /// <summary>
    /// Centros de servicio y si el cliente puede comprar en cada uno.
    /// </summary>
    /// <param name="idEmpresa">
    /// Empresa de centro de servicio. Decide a que base pregunta el SP (la de
    /// Zaragoza, la de Luro o la local). 0 = la local.
    /// </param>
    /// <remarks>
    /// EL SP REVIENTA SI EL CLIENTE NO EXISTE. Arma su consulta concatenando el
    /// nombre del cliente; con un idCliente inexistente el nombre es NULL, toda
    /// la cadena se vuelve NULL y EXEC(NULL) falla. Se exige idCliente &gt; 0 y lo
    /// demas lo contesta la base: un 400 por id vacio es claro, un 500 de SQL no.
    /// </remarks>
    [HttpGet("{idCliente:int}/centros-servicio")]
    [Authorize(Policy = "perm:clientes.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CentrosServicio(
        [FromRoute] int idCliente, [FromQuery] int idEmpresa = 0, CancellationToken ct = default)
    {
        if (idCliente <= 0)
            return BadRequest(new { message = "idCliente requerido." });

        var data = await _service.ConsultarCentrosServicioAsync(idCliente, idEmpresa, ct);
        return Ok(new { ok = true, message = "Centros de servicio consultados.", data });
    }

    /// <summary>
    /// Bitacora de cambios del cliente: altas, modificaciones y reasignaciones
    /// de agente, en un solo listado ordenado por fecha.
    /// </summary>
    /// <remarks>
    /// NO HAY FILTRO POR FECHA, y no es un olvido: el WHERE de fechas de
    /// sp_n_ConsultarClientesLog esta comentado en la base y sus dos parametros
    /// tienen defaults de prueba de diciembre de 2024. Legacy tampoco los
    /// mandaba. Ofrecer un rango que la base ignora seria peor que no ofrecerlo:
    /// la pantalla ensenaria un periodo y los datos serian de otro.
    ///
    /// Se exige idCliente: con 0 el primer bloque del SP no filtra y devuelve la
    /// bitacora COMPLETA de todos los clientes.
    /// </remarks>
    [HttpGet("{idCliente:int}/log")]
    [Authorize(Policy = "perm:clientes.log.ver|app_movil.clientes.log.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Log([FromRoute] int idCliente, CancellationToken ct)
    {
        if (idCliente <= 0)
            return BadRequest(new { message = "idCliente requerido." });

        var data = await _service.ConsultarLogAsync(idCliente, ct);
        return Ok(new { ok = true, message = "Bitacora del cliente consultada.", data });
    }

    /// <summary>
    /// Datos de facturacion del cliente. Un cliente puede tener varios.
    /// </summary>
    /// <param name="idStatus">0 = todos (default), 1 = activos, 2 = inactivos.</param>
    /// <param name="exacto">true = igualdad; false = LIKE %texto%.</param>
    /// <remarks>
    /// QUIEN FACTURA TAMBIEN PUEDE LEER ESTO, Y NO ES UN AFLOJE.
    ///
    /// `clientes.facturacion.ver` se queda y se le suman las dos claves de
    /// facturar. Medido: `VentasFacturacion._Load` llama a esta consulta
    /// (`ConsultarClienteFacturacion`, VentasFacturacion.cs:201 y :1150) SIN
    /// comprobar permiso, y de su resultado sale el RECEPTOR del CFDI: el RFC,
    /// la razon social, el codigo postal y el regimen fiscal. Sin ella no hay
    /// factura que timbrar, y la propia pantalla de alla se niega con "INGRESE
    /// EL RFC DEL CLIENTE." cuando la reja viene vacia.
    ///
    /// O sea: con solo `clientes.facturacion.ver`, un usuario que en Mac31
    /// factura sin problema abria la pantalla del web sin receptor y no podia
    /// facturar nada.
    ///
    /// No se concede nada de mas: lo que se lee son los datos fiscales del
    /// cliente que se esta facturando, que es exactamente lo que una factura
    /// necesita. Dar de ALTA o EDITAR esos datos sigue pidiendo
    /// `clientes.facturacion.editar`, que no se toco.
    /// </remarks>
    [Authorize(Policy = "perm:clientes.facturacion.ver|facturacion.timbrar|ventas.facturar|app_movil.clientes.facturacion.ver")]
    [HttpGet("facturacion")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Facturacion(
        [FromQuery] int id = 0,
        [FromQuery] int idCliente = 0,
        [FromQuery] int idStatus = 0,
        [FromQuery] string? nombre = null,
        [FromQuery] string? rfc = null,
        [FromQuery] bool exacto = false,
        [FromQuery] int idEmpresa = 0,
        CancellationToken ct = default)
    {
        var data = await _service.ConsultarFacturacionAsync(new ClienteFacturacionFiltro
        {
            IDClienteFacturacion = id,
            IDCliente = idCliente,
            IDStatus = idStatus,
            NombreFacturacion = nombre,
            Rfc = rfc,
            Exacto = exacto,
            IDEmpresaCS = idEmpresa
        }, ct);

        return Ok(new { ok = true, message = "Datos de facturacion consultados.", data });
    }
}
