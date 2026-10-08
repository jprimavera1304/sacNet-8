using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// La consulta del padron de clientes: la pantalla ConsultarClientes de Mac31.
///
/// Es el GET raiz de api/clientes, que faltaba: ClientesAnexosController publica
/// las pestanas de la ficha y ClientesAdminController las escrituras, pero nadie
/// contestaba "dame los clientes". El web lo estaba supliendo con
/// GET api/catalogos/clientes, que es un desplegable sin permiso ni filtro
/// (arreglado en esa misma entrega).
///
/// REUSA `clientes.ver`. Es la misma decision que la ficha del cliente: quien
/// puede ver a un cliente puede buscarlo. Una clave `clientes.consultar` aparte
/// seria un permiso que nadie va a querer dar por separado.
/// </summary>
[ApiController]
[Route("api/clientes")]
[Authorize]
public class ClientesConsultaController : ControllerBase
{
    private readonly IClientesConsultaService _service;
    private readonly ICurrentUserAccessor _currentUser;

    public ClientesConsultaController(IClientesConsultaService service, ICurrentUserAccessor currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Busca clientes. El resultado SIEMPRE viene acotado al agente del usuario.
    /// </summary>
    /// <param name="idStatus">0 = todos, 1 = activos (default), 2 = inactivos.</param>
    /// <param name="idTipoCliente">0 = todos, 1 = si factura, 2 = no factura.</param>
    /// <param name="conHuella">0 = todos, 1 = con huella, 2 = sin huella.</param>
    /// <param name="idEmpresaCs">
    /// Empresa de centro de servicio: decide a que base pregunta el SP. SI NO SE
    /// MANDA se usa dbo.Constantes.IDEmpresaCS (1 en MacZ), igual que Mac31. Un 0
    /// explicito se respeta, aunque en MacZ no devuelve ningun cliente.
    /// </param>
    /// <param name="esMostrador">0 = todos, 1 = solo los de mostrador.</param>
    /// <param name="exacto">true = igualdad; false = LIKE.</param>
    /// <param name="idAgente">
    /// ACOTAR a un agente. Solo se obedece si el usuario ve a todos; si esta
    /// amarrado a un agente se ignora. Ver el comentario de abajo.
    /// </param>
    /// <response code="200">Clientes consultados</response>
    /// <response code="409">
    /// No se pudo resolver el filtro por agente del usuario (falta desplegar
    /// sp_w_ConsultaAgenteUsuario). A proposito NO se contesta el padron.
    /// </response>
    /*
      LA PUERTA ES `clientes.ver_modulo`, NO `clientes.ver`.

      `clientes.ver` se llama, literalmente, "Clientes - Ver bonificaciones y
      centros de servicio": es la llave estrecha de las pestanas satelite.
      Usarla para el catalogo entero mezcla dos permisos distintos, y encima la
      tenian CERO de los 8 usuarios activos de MacZ, asi que exigirla dejaba en
      403 a todo el que no fuera SuperAdmin. Este endpoint lo consume el front
      viejo en una veintena de sitios: lo que se caia no era una pantalla nueva.

      `clientes.ver_modulo` es la llave de "puede ver clientes", que es lo que
      aqui toca. Se piden las dos con `|` -basta una- para no quitarle el
      catalogo a quien ya tenga la estrecha. Y el handler trae un puente:
      `CanUseLegacyVerAsModuleView` acepta tambien a quien tenga el `clientes.ver`
      de legacy cuando se exige el `.ver_modulo`.

      Esto NO sustituye al filtro por agente, que es el arreglo de verdad: antes
      cualquier autenticado se llevaba los 887 clientes de los 15 agentes.
    */
    [HttpGet]
    [Authorize(Policy = "perm:clientes.ver_modulo|clientes.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Consultar(
        [FromQuery] int idCliente = 0,
        [FromQuery] string? numero = null,
        [FromQuery] string? nombre = null,
        [FromQuery] string? apellidoPaterno = null,
        [FromQuery] string? apellidoMaterno = null,
        [FromQuery] string? rfc = null,
        [FromQuery] string? referencia = null,
        [FromQuery] int idStatus = 1,
        [FromQuery] int idTipoCliente = 0,
        [FromQuery] int conHuella = 0,
        [FromQuery] int? idEmpresaCs = null,
        [FromQuery] int esMostrador = 0,
        [FromQuery] bool exacto = false,
        [FromQuery] int idAgente = 0,
        CancellationToken ct = default)
    {
        /*
          ========================================================================
          EL FILTRO POR AGENTE NO SE PUEDE PEDIR DESDE FUERA. NI AQUI NI EN NINGUN
          OTRO LADO.
          ========================================================================

          `idAgente` de este query NO es el filtro. El filtro lo resuelve el
          repositorio con FiltroDeAgente a partir de
          CurrentUser.GetLegacyUserId(User) -- o sea, del TOKEN, no del cuerpo ni
          de la URL. Si viniera de fuera, un agente mandaria `idAgente=0` y veria
          el padron completo: seria un filtro que cualquiera puede apagar.

          Que hace entonces `idAgente`: ACOTAR, y solo hacia dentro.
            * filtro resuelto = 0 (el usuario ve a todos) -> se obedece, para que
              la pantalla pueda enfocar un agente.
            * filtro resuelto > 0 (el usuario esta amarrado) -> se IGNORA y manda
              el resuelto. Nunca amplia lo que se puede ver.

          Y si el filtro NO SE PUEDE RESOLVER, esto no contesta: FiltroDeAgente
          lanza ConflictException y el ExceptionHandlingMiddleware la traduce a
          409 con `code = "CONFLICTO"`, el mensaje para el usuario y el detalle
          (AppException.Detail si viaja siempre, tambien en Production: lo redacta
          el backend, no el motor). Se eligio 409 y no 0 porque 0 significa "ve
          todos": degradar a 0 no quitaria informacion, APAGARIA el filtro.

          La razon con numeros: un usuario ADMIN amarrado a un agente llego a ver
          los 1,042 clientes de 15 agentes porque la resolucion se saltaba en
          silencio y "no se pudo resolver" devolvia el mismo 0 que "ve todos".
        */
        var idUsuario = _currentUser.GetLegacyUserId(User);

        var data = await _service.ConsultarAsync(new ClientesConsultaFiltro
        {
            IDCliente = idCliente,
            Numero = numero,
            Nombre = nombre,
            ApellidoPaterno = apellidoPaterno,
            ApellidoMaterno = apellidoMaterno,
            Rfc = rfc,
            Referencia = referencia,
            IDStatus = idStatus,
            IDTipoCliente = idTipoCliente,
            ConHuella = conHuella,
            /*
              SIN idEmpresaCs NO SE MANDA 0: SE RESUELVE DESDE LAS CONSTANTES.
              sp_n_ConsultaClientes anade SIEMPRE ' AND C.IDEmpresaCS =
              @IDEmpresaCS', y en MacZ los 887 clientes del padron tienen
              IDEmpresaCS = 1. Con el 0 que estaba cableado aqui como valor por
              omision, GET api/clientes contestaba {"total":0} y GET
              api/clientes?idEmpresaCs=1 contestaba los 887: un default que
              devuelve lista vacia se lee como un filtro mal puesto, no como un
              default mal elegido.

              El nulo baja hasta el repositorio, que lo resuelve con
              dbo.Constantes.IDEmpresaCS -- de donde lo saca Mac31
              (Variables.IDEmpresaCS) -- con un SELECT y nunca con
              sp_n_ConsultaConstantes, que HACE EL CIERRE DEL DIA.

              Si viene explicito se obedece, tambien el 0, por si alguien quiere el
              comportamiento viejo a proposito.
            */
            IDEmpresaCS = idEmpresaCs,
            EsMostrador = esMostrador,
            Exacto = exacto,
            IDAgenteAcotar = idAgente
        }, idUsuario, ct);

        // El renglon trae limite de credito, saldo a favor y saldo de cascos: es
        // dinero del cliente. No se guarda en ninguna cache intermedia.
        Response.Headers["Cache-Control"] = "no-store";

        return Ok(new { ok = true, message = "Clientes consultados.", data, total = data.Count });
    }
}
