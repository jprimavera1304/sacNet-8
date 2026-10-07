using ISL_Service.Application.DTOs.Maestros;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Escrituras de la ficha del proveedor, con sus apoderados, domicilios y
/// documentos. Cuelga de api/proveedores, donde la ola 3 dejo las lecturas.
///
/// Reusa los permisos que ya existian en el catalogo (proveedores.crear,
/// proveedores.editar, proveedores.estado.editar): son los mismos de siempre, no
/// hacia falta inventar otros.
///
/// TRES COSAS DE LEGACY QUE NO SE PUEDEN ARREGLAR DESDE AQUI, Y QUE QUEDAN
/// DICHAS EN VEZ DE DISIMULADAS:
///  - sp_n_ActualizarProveedor no recibe @IDUsuario ni @Equipo, asi que la
///    modificacion de un proveedor no deja firma en la base. Ni desde el web ni
///    desde Mac31.
///  - Tampoco recibe @IDDescuento ni @IDFacturama, que si van en el alta. Por eso
///    el request de modificacion no los pide: ofrecerlos seria prometer un cambio
///    que la base no va a hacer.
///  - EsRepresentanteLegal y EsRepresentanteTecnico del apoderado se consultan
///    pero no se guardan: ningun procedimiento de escritura los recibe. No se
///    exponen, por lo mismo.
/// </summary>
[ApiController]
[Route("api/proveedores")]
[Authorize]
public class ProveedoresAdminController : MaestrosControllerBase
{
    private static readonly string[] PermisosCrear = { "proveedores.crear" };
    private static readonly string[] PermisosEditar = { "proveedores.editar" };
    private static readonly string[] PermisosBaja = { "proveedores.estado.editar" };

    public ProveedoresAdminController(
        IMaestrosService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(service, currentUser, permissionService)
    {
    }

    // =======================================================================
    // Proveedor
    // =======================================================================

    /// <summary>Alta de proveedor.</summary>
    /// <response code="201">Proveedor dado de alta</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Crear([FromBody] CrearProveedorRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaId(request.IdColonia, "idColonia")
                    ?? ValidaTexto(request.Nombre, 400, "nombre")
                    ?? ValidaTexto(request.CalleNumero, 400, "calleNumero")
                    ?? ValidaTexto(request.RFC, 100, "RFC")
                    ?? ValidaProveedorOpcionales(request.ApellidoPaterno, request.ApellidoMaterno,
                        request.Telefono1, request.Telefono2, request.Email, request.Observaciones, request.PaginaWeb)
                    ?? ValidaTopeOpcional(request.IdFacturama, 400, "idFacturama");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        return RespondeAlta(await Service.CrearProveedorAsync(request, ct), "Proveedor dado de alta.");
    }

    /// <summary>
    /// Modificacion de proveedor. Ficha completa: el UPDATE del procedimiento
    /// pisa todas las columnas.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Actualizar([FromRoute] int id, [FromBody] ActualizarProveedorRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaId(request.IdColonia, "idColonia")
                    ?? ValidaTexto(request.Nombre, 400, "nombre")
                    ?? ValidaTexto(request.CalleNumero, 400, "calleNumero")
                    ?? ValidaTexto(request.RFC, 100, "RFC")
                    ?? ValidaProveedorOpcionales(request.ApellidoPaterno, request.ApellidoMaterno,
                        request.Telefono1, request.Telefono2, request.Email, request.Observaciones, request.PaginaWeb);
        if (error != null) return BadRequest(error);
        if (request.IdStatus is not (StatusActivo or StatusCancelado))
            return BadRequest(new { ok = false, message = "El estatus solo puede ser 1 (activo) o 2 (cancelado)." });

        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await Service.ActualizarProveedorAsync(id, request, ct), "Proveedor actualizado.");
    }

    /// <summary>
    /// Baja logica del proveedor: IDStatus = 2 y la ficha intacta.
    ///
    /// No pide cuerpo. El DELETE de legacy reusaba el UPDATE y solo exigia el id,
    /// asi que una baja dejaba al proveedor sin nombre, sin RFC y sin domicilio.
    /// </summary>
    /// <response code="404">El proveedor no existe</response>
    [HttpPost("{id:int}/inhabilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Inhabilitar([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await Service.CambiarStatusProveedorAsync(id, StatusCancelado, ct),
            "Proveedor inhabilitado.", "El proveedor no existe.");
    }

    [HttpPost("{id:int}/habilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Habilitar([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await Service.CambiarStatusProveedorAsync(id, StatusActivo, ct),
            "Proveedor habilitado.", "El proveedor no existe.");
    }

    // =======================================================================
    // Apoderados
    // =======================================================================

    [HttpPost("apoderados")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearApoderado([FromBody] CrearProveedorApoderadoRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaId(request.IdProveedor, "idProveedor")
                    ?? ValidaId(request.IdPuesto, "idPuesto")
                    ?? ValidaTexto(request.Apoderado, 400, "apoderado");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return RespondeAlta(await Service.CrearApoderadoAsync(request, IdUsuarioLegacy(), Equipo(), ct),
            "Apoderado dado de alta.");
    }

    /// <summary>
    /// Modificacion del apoderado.
    ///
    /// El id va en la ruta y es obligatorio. En legacy el PUT no lo exigia: un
    /// cuerpo sin IDProveedorApoderado actualizaba el renglon 0, que no existe, y
    /// contestaba exito.
    /// </summary>
    [HttpPut("apoderados/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarApoderado([FromRoute] int id, [FromBody] ActualizarProveedorApoderadoRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaId(request.IdProveedor, "idProveedor")
                    ?? ValidaId(request.IdPuesto, "idPuesto")
                    ?? ValidaTexto(request.Apoderado, 400, "apoderado");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await Service.ActualizarApoderadoAsync(id, request, IdUsuarioLegacy(), Equipo(), ct),
            "Apoderado actualizado.");
    }

    [HttpPost("apoderados/{id:int}/inhabilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InhabilitarApoderado([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await Service.CambiarStatusApoderadoAsync(id, StatusCancelado, IdUsuarioLegacy(), Equipo(), ct),
            "Apoderado inhabilitado.", "El apoderado no existe.");
    }

    [HttpPost("apoderados/{id:int}/habilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HabilitarApoderado([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await Service.CambiarStatusApoderadoAsync(id, StatusActivo, IdUsuarioLegacy(), Equipo(), ct),
            "Apoderado habilitado.", "El apoderado no existe.");
    }

    // =======================================================================
    // Domicilios
    // =======================================================================

    [HttpPost("domicilios")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearDomicilio([FromBody] CrearProveedorDomicilioRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaId(request.IdProveedor, "idProveedor")
                    ?? ValidaTexto(request.Domicilio, 400, "domicilio")
                    ?? ValidaTexto(request.CalleNumero, 400, "calleNumero");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return RespondeAlta(await Service.CrearDomicilioAsync(request, IdUsuarioLegacy(), Equipo(), ct),
            "Domicilio dado de alta.");
    }

    /// <summary>
    /// Modificacion del domicilio. En legacy ni el POST exigia idProveedor ni el
    /// PUT exigia el id del domicilio: se insertaba o actualizaba el renglon 0.
    /// </summary>
    [HttpPut("domicilios/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarDomicilio([FromRoute] int id, [FromBody] ActualizarProveedorDomicilioRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaTexto(request.Domicilio, 400, "domicilio")
                    ?? ValidaTexto(request.CalleNumero, 400, "calleNumero");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await Service.ActualizarDomicilioAsync(id, request, IdUsuarioLegacy(), Equipo(), ct),
            "Domicilio actualizado.");
    }

    [HttpPost("domicilios/{id:int}/inhabilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InhabilitarDomicilio([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await Service.CambiarStatusDomicilioAsync(id, StatusCancelado, IdUsuarioLegacy(), Equipo(), ct),
            "Domicilio inhabilitado.", "El domicilio no existe.");
    }

    [HttpPost("domicilios/{id:int}/habilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HabilitarDomicilio([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await Service.CambiarStatusDomicilioAsync(id, StatusActivo, IdUsuarioLegacy(), Equipo(), ct),
            "Domicilio habilitado.", "El domicilio no existe.");
    }

    // =======================================================================
    // Documentos
    // =======================================================================

    /// <summary>
    /// Alta de documento del proveedor.
    ///
    /// NO LLEVA 'escaneado' NI 'idStatus'. El repositorio de legacy le manda a
    /// sp_n_InsertarProveedorDocumento un @IDStatus y un @Escaneado que el
    /// procedimiento NO TIENE: leidos de sys.parameters en Produccion_svr, sus
    /// unicos parametros son IDProveedor, IDTipoDocumento, Documento, Archivo,
    /// ArchivoFtp, IDUsuario y Equipo. Es decir que esta alta de MacServicios2
    /// revienta en esta base antes de tocar la tabla. Aqui se manda lo que el
    /// procedimiento pide.
    /// </summary>
    [HttpPost("documentos")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearDocumento([FromBody] CrearProveedorDocumentoRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaId(request.IdProveedor, "idProveedor")
                    ?? ValidaId(request.IdTipoDocumento, "idTipoDocumento")
                    ?? ValidaTexto(request.Documento, 400, "documento")
                    ?? ValidaTopeOpcional(request.Archivo, 1000, "archivo")
                    ?? ValidaTopeOpcional(request.ArchivoFtp, 1000, "archivoFtp");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return RespondeAlta(await Service.CrearDocumentoProveedorAsync(request, IdUsuarioLegacy(), Equipo(), ct),
            "Documento dado de alta.");
    }

    [HttpPut("documentos/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarDocumento([FromRoute] int id, [FromBody] ActualizarProveedorDocumentoRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaId(request.IdProveedor, "idProveedor")
                    ?? ValidaId(request.IdTipoDocumento, "idTipoDocumento")
                    ?? ValidaTexto(request.Documento, 400, "documento")
                    ?? ValidaTopeOpcional(request.Archivo, 1000, "archivo")
                    ?? ValidaTopeOpcional(request.ArchivoFtp, 1000, "archivoFtp");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await Service.ActualizarDocumentoProveedorAsync(id, request, IdUsuarioLegacy(), Equipo(), ct),
            "Documento actualizado.");
    }

    [HttpPost("documentos/{id:int}/inhabilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InhabilitarDocumento([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await Service.CambiarStatusDocumentoProveedorAsync(id, StatusCancelado, IdUsuarioLegacy(), Equipo(), ct),
            "Documento inhabilitado.", "El documento no existe.");
    }

    [HttpPost("documentos/{id:int}/habilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HabilitarDocumento([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await Service.CambiarStatusDocumentoProveedorAsync(id, StatusActivo, IdUsuarioLegacy(), Equipo(), ct),
            "Documento habilitado.", "El documento no existe.");
    }

    /// <summary>Los topes de los campos opcionales del proveedor.</summary>
    private static object? ValidaProveedorOpcionales(
        string? apPaterno, string? apMaterno, string? tel1, string? tel2,
        string? email, string? observaciones, string? paginaWeb)
        => ValidaTopeOpcional(apPaterno, 400, "apellidoPaterno")
           ?? ValidaTopeOpcional(apMaterno, 400, "apellidoMaterno")
           ?? ValidaTopeOpcional(tel1, 100, "telefono1")
           ?? ValidaTopeOpcional(tel2, 100, "telefono2")
           ?? ValidaTopeOpcional(email, 400, "email")
           ?? ValidaTopeOpcional(observaciones, 4000, "observaciones")
           ?? ValidaTopeOpcional(paginaWeb, 4000, "paginaWeb");
}
