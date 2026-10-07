using ISL_Service.Application.DTOs.Maestros;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Escrituras de la ficha del cliente: alta, modificacion, baja,
/// bonificaciones, centros de servicio permitidos y domicilios de facturacion.
///
/// Cuelga de api/clientes, el mismo recurso donde la ola 3 dejo las lecturas
/// (GET api/clientes/{id}/bonificaciones, /log, /facturacion). Aqui solo hay
/// POST y PUT, asi que no hay ruta repetida y el front ve un solo recurso.
///
/// LA MODIFICACION PIDE LA FICHA COMPLETA, y eso se dice en vez de disimularlo:
/// el UPDATE de sp_n_ActualizarCliente escribe todas las columnas, asi que un
/// campo que no venga se guarda vacio. La unica excepcion es el IDFacturama, que
/// tiene su propia ruta porque el procedimiento tiene una rama (@Accion = 1) que
/// cambia solo esa columna.
/// </summary>
[ApiController]
[Route("api/clientes")]
[Authorize]
public class ClientesAdminController : MaestrosControllerBase
{
    private static readonly string[] PermisosCrear = { "clientes.crear" };
    private static readonly string[] PermisosEditar = { "clientes.editar" };
    private static readonly string[] PermisosBaja = { "clientes.estado.editar" };

    // La bonificacion por tipo de usado ES DINERO: es lo que se le abona al
    // cliente por cada casco que entrega. Permiso propio por eso.
    private static readonly string[] PermisosBonificaciones = { "clientes.bonificaciones.editar" };

    // Los datos fiscales se miran aparte de los de contacto: quien captura un
    // telefono no tiene por que poder cambiar el RFC con el que se factura.
    private static readonly string[] PermisosFacturacion = { "clientes.facturacion.editar" };

    // En que centros puede comprar: decide donde se le surte, no quien es.
    private static readonly string[] PermisosCentros = { "clientes.centros.editar" };

    public ClientesAdminController(
        IMaestrosService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(service, currentUser, permissionService)
    {
    }

    /// <summary>
    /// Alta de cliente.
    ///
    /// Lo obligatorio depende de `idTipoCliente`: 1 (con factura) exige agente,
    /// colonia, calle, numero, RFC y correo; 2 (sin factura) exige solo el
    /// nombre. Legacy tenia una tercera via sin querer: con idTipoCliente en 0 no
    /// validaba NADA y el alta reventaba mas adentro con una referencia nula.
    /// Aqui 0 no pasa.
    /// </summary>
    /// <response code="201">Cliente dado de alta</response>
    /// <response code="400">Datos invalidos, o legacy rechazo por una regla</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Crear([FromBody] CrearClienteRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaCliente(request);
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        return RespondeAlta(await Service.CrearClienteAsync(request, IdUsuarioLegacy(), Equipo(), ct),
            "Cliente dado de alta.");
    }

    /// <summary>
    /// Modificacion de cliente. Pide la ficha completa (ver la nota de la clase).
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Actualizar([FromRoute] int id, [FromBody] ActualizarClienteRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaCliente(request);
        if (error != null) return BadRequest(error);
        if (request.IdStatus is not (StatusActivo or StatusCancelado))
            return BadRequest(new { ok = false, message = "El estatus solo puede ser 1 (activo) o 2 (cancelado)." });

        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await Service.ActualizarClienteAsync(id, request, IdUsuarioLegacy(), Equipo(), ct),
            "Cliente actualizado.");
    }

    /// <summary>
    /// Cambia SOLO el id de Facturama del cliente.
    ///
    /// Tiene su propia ruta porque es la unica cosa que se puede cambiar sin
    /// mandar la ficha entera: es la rama @Accion = 1 de
    /// sp_n_ActualizarCliente, que hace un UPDATE de esa columna y regresa. Usar
    /// el PUT completo para esto arriesgaria pisar treinta columnas por guardar
    /// un identificador de facturacion.
    /// </summary>
    [HttpPut("{id:int}/facturama")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CambiarFacturama([FromRoute] int id, [FromBody] CambiarFacturamaClienteRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaTopeOpcional(request.IdFacturama, 400, "idFacturama");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await Service.CambiarFacturamaClienteAsync(id, request, IdUsuarioLegacy(), Equipo(), ct),
            "Identificador de facturacion actualizado.");
    }

    /// <summary>
    /// Baja logica del cliente: IDStatus = 2 y el resto de la ficha intacta.
    ///
    /// No pide cuerpo. En legacy este era un DELETE que llamaba al mismo UPDATE
    /// con lo que trajera la peticion, y su validacion solo exigia el id: una
    /// baja dejaba al cliente sin nombre, sin telefonos y sin limite de credito,
    /// con las ventas viejas apuntando a un renglon vaciado.
    /// </summary>
    /// <response code="404">El cliente no existe</response>
    [HttpPost("{id:int}/inhabilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Inhabilitar([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await Service.CambiarStatusClienteAsync(id, StatusCancelado, IdUsuarioLegacy(), Equipo(), ct),
            "Cliente inhabilitado.", "El cliente no existe.");
    }

    /// <summary>Vuelve a activar al cliente.</summary>
    [HttpPost("{id:int}/habilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Habilitar([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await Service.CambiarStatusClienteAsync(id, StatusActivo, IdUsuarioLegacy(), Equipo(), ct),
            "Cliente habilitado.", "El cliente no existe.");
    }

    /// <summary>
    /// Guarda las bonificaciones del cliente por tipo de usado.
    ///
    /// Viaja una LISTA DE PARES, no dos listas paralelas. En legacy el front
    /// mandaba dos cadenas unidas con '~' (los tipos y los importes) que el
    /// procedimiento apareaba por posicion, y nadie comprobaba que tuvieran el
    /// mismo numero de elementos: dos listas desalineadas le ponen a un tipo de
    /// casco la bonificacion de otro, en silencio y en dinero. Con pares eso ya
    /// no se puede.
    /// </summary>
    [HttpPut("{id:int}/bonificaciones")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GuardarBonificaciones(
        [FromRoute] int id, [FromBody] BonificacionesClienteRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        if (request.Bonificaciones.Count == 0)
            return BadRequest(new { ok = false, message = "No hay bonificaciones que guardar." });
        if (request.Bonificaciones.Any(x => x.IdTipoUsado <= 0))
            return BadRequest(new { ok = false, message = "Hay una bonificacion sin tipo de usado." });
        if (request.Bonificaciones.Any(x => x.Bonificacion < 0))
            return BadRequest(new { ok = false, message = "Una bonificacion no puede ser negativa." });
        if (request.Bonificaciones.Select(x => x.IdTipoUsado).Distinct().Count() != request.Bonificaciones.Count)
            return BadRequest(new { ok = false, message = "Hay un tipo de usado repetido: no se sabria que importe aplicar." });

        var sinPermiso = await ExigirPermisoAsync(PermisosBonificaciones, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await Service.GuardarBonificacionesAsync(id, request, IdUsuarioLegacy(), Equipo(), ct),
            "Bonificaciones guardadas.");
    }

    /// <summary>
    /// En que centros de servicio puede comprar el cliente. Mismo cambio que las
    /// bonificaciones: una lista de pares en vez de dos listas paralelas.
    /// </summary>
    [HttpPut("{id:int}/centros-servicio")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GuardarCentrosServicio(
        [FromRoute] int id, [FromBody] CentrosServicioClienteRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        if (request.Centros.Count == 0)
            return BadRequest(new { ok = false, message = "No hay centros que guardar." });
        if (request.Centros.Any(x => x.IdCentro <= 0))
            return BadRequest(new { ok = false, message = "Hay un renglon sin centro." });
        if (request.Centros.Select(x => x.IdCentro).Distinct().Count() != request.Centros.Count)
            return BadRequest(new { ok = false, message = "Hay un centro repetido: no se sabria si dejarlo o quitarlo." });

        var sinPermiso = await ExigirPermisoAsync(PermisosCentros, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await Service.GuardarCentrosServicioAsync(id, request, IdUsuarioLegacy(), Equipo(), ct),
            "Centros de servicio guardados.");
    }

    /// <summary>Alta de un domicilio de facturacion del cliente.</summary>
    [HttpPost("facturacion")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearFacturacion([FromBody] CrearClienteFacturacionRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaFacturacion(request) ?? ValidaId(request.IdCliente, "idCliente");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosFacturacion, ct);
        if (sinPermiso != null) return sinPermiso;

        return RespondeAlta(await Service.CrearClienteFacturacionAsync(request, IdUsuarioLegacy(), Equipo(), ct),
            "Domicilio de facturacion dado de alta.");
    }

    /// <summary>
    /// Modificacion del domicilio de facturacion.
    ///
    /// `idStatus` arranca en 1 cuando no se manda. En legacy el parametro no
    /// tenia valor por omision del lado del cliente, asi que un PUT que no lo
    /// mandara guardaba 0 y APAGABA el domicilio de facturacion sin que nadie lo
    /// pidiera: el cliente dejaba de poder facturar y nadie sabia por que.
    /// </summary>
    [HttpPut("facturacion/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarFacturacion(
        [FromRoute] int id, [FromBody] ActualizarClienteFacturacionRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaFacturacion(request);
        if (error != null) return BadRequest(error);
        if (request.IdStatus is not (StatusActivo or StatusCancelado))
            return BadRequest(new { ok = false, message = "El estatus solo puede ser 1 (activo) o 2 (cancelado)." });

        var sinPermiso = await ExigirPermisoAsync(PermisosFacturacion, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await Service.ActualizarClienteFacturacionAsync(id, request, IdUsuarioLegacy(), Equipo(), ct),
            "Domicilio de facturacion actualizado.");
    }

    /// <summary>
    /// Marca el cliente como facturacion de mostrador.
    ///
    /// El procedimiento recibe un unico parametro y NO guarda quien lo hizo:
    /// legacy tampoco se lo manda porque no lo recibe. Queda dicho aqui, en vez
    /// de prometer una auditoria que la base no escribe.
    /// </summary>
    [HttpPut("{id:int}/facturacion-mostrador")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MarcarFacturacionMostrador([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosFacturacion, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await Service.MarcarFacturacionMostradorAsync(id, ct),
            "Cliente marcado como facturacion de mostrador.");
    }

    // -----------------------------------------------------------------------
    // Validaciones de forma
    // -----------------------------------------------------------------------

    /// <summary>
    /// Lo obligatorio del cliente, que depende del tipo. Es la misma regla de
    /// legacy, con el agujero tapado: alla el tipo 0 no validaba nada.
    /// </summary>
    private static object? ValidaCliente(CrearClienteRequest r)
    {
        if (r.IdTipoCliente is not (1 or 2))
            return new { ok = false, message = "El tipo de cliente debe ser 1 (requiere factura) o 2 (no requiere factura)." };

        var error = ValidaTexto(r.Nombre, 4000, "nombre");
        if (error != null) return error;

        if (r.IdTipoCliente == 1)
        {
            error = ValidaId(r.IdAgente, "idAgente")
                    ?? ValidaId(r.IdColonia, "idColonia")
                    ?? ValidaTexto(r.Calle, 4000, "calle")
                    ?? ValidaTexto(r.NumeroExterior, 4000, "numeroExterior")
                    ?? ValidaTexto(r.RFC, 100, "RFC")
                    ?? ValidaTexto(r.Email, 4000, "email");
            if (error != null) return error;
        }

        error = ValidaTopeOpcional(r.ApellidoPaterno, 4000, "apellidoPaterno")
                ?? ValidaTopeOpcional(r.ApellidoMaterno, 4000, "apellidoMaterno")
                ?? ValidaTopeOpcional(r.NombreNegocio, 4000, "nombreNegocio")
                ?? ValidaTopeOpcional(r.NombreFacturacion, 4000, "nombreFacturacion")
                ?? ValidaTopeOpcional(r.CURP, 100, "CURP")
                ?? ValidaTopeOpcional(r.Telefono1, 100, "telefono1")
                ?? ValidaTopeOpcional(r.Telefono2, 100, "telefono2")
                ?? ValidaTopeOpcional(r.Referencia, 100, "referencia")
                ?? ValidaTopeOpcional(r.IdFacturama, 400, "idFacturama");
        if (error != null) return error;

        if (r.LimiteCredito < 0)
            return new { ok = false, message = "El limite de credito no puede ser negativo." };

        return null;
    }

    private static object? ValidaFacturacion(CrearClienteFacturacionRequest r)
        => ValidaId(r.IdColonia, "idColonia")
           ?? ValidaTexto(r.NombreFacturacion, 4000, "nombreFacturacion")
           ?? ValidaTexto(r.RFC, 100, "RFC")
           ?? ValidaTexto(r.Calle, 4000, "calle")
           ?? ValidaTexto(r.IdFERegimenFiscal, 100, "idFERegimenFiscal")
           ?? ValidaTopeOpcional(r.NumeroExterior, 4000, "numeroExterior")
           ?? ValidaTopeOpcional(r.NumeroInterior, 4000, "numeroInterior")
           ?? ValidaTopeOpcional(r.IdFEFormaPago, 100, "idFEFormaPago")
           ?? ValidaTopeOpcional(r.IdFEUsoCFDI, 100, "idFEUsoCFDI")
           ?? ValidaTopeOpcional(r.IdFEMetodoPago, 100, "idFEMetodoPago");
}
