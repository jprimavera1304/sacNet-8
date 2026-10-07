using ISL_Service.Application.DTOs.SeguridadMac;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// El usuario de MAC31 (dbo.Usuarios): alta, modificacion, baja, sus almacenes,
/// sus procesos y sus fechas.
///
/// POR QUE api/usuarios/mac31 Y NO api/usuarios
/// -------------------------------------------
/// Porque api/usuarios ya existe y administra OTRA COSA: el usuario del WEB, que
/// vive en dbo.UsuarioWeb, tiene llave Guid y contrasena con BCrypt. Este es el de
/// Mac31: llave entera y contrasena como la guarda legacy. Un id 7 y un Guid no se
/// confunden nunca, pero dos pantallas que las dos dicen "usuarios" si, y
/// confundirlas aqui significa dar de baja a la persona equivocada.
///
/// LOS TRES LOTES SON TRANSACCIONALES, y es el arreglo principal de este
/// controlador. En MacServicios2 los almacenes, los procesos y las fechas del
/// usuario se guardaban recorriendo dos cadenas unidas con '~' y ejecutando el
/// procedimiento una vez por elemento, SIN transaccion: si fallaba en la quinta de
/// ocho, las cuatro anteriores quedaban escritas, la respuesta hablaba de la
/// ultima vuelta y podia decir que todo salio bien. Un usuario con la mitad de sus
/// permisos cambiados y una pantalla que dice que no paso nada es la peor
/// combinacion posible en una pantalla de seguridad.
/// </summary>
[ApiController]
[Route("api/usuarios/mac31")]
[Authorize]
public class UsuariosMacController : PermisoControllerBase
{
    private static readonly string[] PermisosCrear = { "usuarios.crear" };
    private static readonly string[] PermisosEditar = { "usuarios.editar" };
    private static readonly string[] PermisosEstado = { "usuarios.estado.editar" };

    // Los procesos del usuario son el mapa de seguridad de Mac31 y la fuente de
    // cuatro familias de permisos del web. Quien los toca administra seguridad,
    // asi que se exige el permiso de la pantalla de permisos.
    private static readonly string[] PermisosProcesos = { "permisosweb.bootstrap" };

    private readonly ISeguridadMacService _service;

    public UsuariosMacController(
        ISeguridadMacService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    /// <summary>
    /// Alta de usuario de Mac31.
    ///
    /// La contrasena se guarda como la guarda legacy (sin hash) a proposito: si el
    /// web guardara un hash, Mac31 no podria validar el acceso y la persona no
    /// entraria a la aplicacion de escritorio. El usuario del WEB si usa BCrypt y
    /// vive en otra tabla.
    /// </summary>
    /// <response code="201">Usuario dado de alta</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Crear([FromBody] CrearUsuarioMacRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaUsuario(request);
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.CrearUsuarioMacAsync(request, ct);
        return data.Guardo
            ? StatusCode(StatusCodes.Status201Created, new { ok = true, message = "Usuario dado de alta.", data })
            : Responder(data, "Usuario dado de alta.");
    }

    /// <summary>
    /// Modificacion del usuario de Mac31. Pide la ficha completa, contrasena
    /// incluida: el UPDATE del procedimiento escribe todas esas columnas.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Actualizar([FromRoute] int id, [FromBody] ActualizarUsuarioMacRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaUsuario(request);
        if (error != null) return BadRequest(error);
        if (request.IdStatus is not (1 or 2 or 3))
            return BadRequest(new { ok = false, message = "El estatus solo puede ser 1 (activo), 2 (inactivo) o 3 (bloqueado)." });

        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await _service.ActualizarUsuarioMacAsync(id, request, ct), "Usuario actualizado.");
    }

    /// <summary>
    /// Inactiva al usuario de Mac31: IDStatus = 2, conservando su contrasena y sus
    /// datos.
    ///
    /// No pide cuerpo. En legacy este era un DELETE que llamaba al mismo UPDATE con
    /// lo que trajera la peticion y solo exigia el id: dejaba al usuario sin
    /// nombre, sin perfil y SIN CONTRASENA — y si alguien lo reactivaba despues,
    /// entraba sin contrasena.
    /// </summary>
    /// <response code="404">El usuario no existe</response>
    [HttpPost("{id:int}/inhabilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Inhabilitar([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosEstado, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await _service.CambiarStatusUsuarioMacAsync(id, 2, ct),
            "Usuario inhabilitado.", "El usuario no existe.");
    }

    /// <summary>Reactiva al usuario de Mac31.</summary>
    [HttpPost("{id:int}/habilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Habilitar([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosEstado, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await _service.CambiarStatusUsuarioMacAsync(id, 1, ct),
            "Usuario habilitado.", "El usuario no existe.");
    }

    /// <summary>
    /// Guarda los almacenes del usuario, todos en una transaccion.
    ///
    /// SIN RENGLON EN UsuariosAlmacenes, al repartidor le sale VACIA la pantalla de
    /// "Mis pedidos" aunque el pedido si se haya guardado. Es la causa mas comun de
    /// ese sintoma, y por eso este endpoint no puede aplicarse a medias.
    /// </summary>
    [HttpPut("{id:int}/almacenes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GuardarAlmacenes(
        [FromRoute] int id, [FromBody] AlmacenesDeUsuarioRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        if (request.Almacenes.Count == 0)
            return BadRequest(new { ok = false, message = "No hay almacenes que guardar." });
        if (request.Almacenes.Any(x => x.IdAlmacen <= 0))
            return BadRequest(new { ok = false, message = "Hay un renglon sin almacen." });
        if (request.Almacenes.Any(x => x.IdStatus is not (1 or 2)))
            return BadRequest(new { ok = false, message = "El estatus de un almacen solo puede ser 1 o 2." });
        if (request.Almacenes.Select(x => x.IdAlmacen).Distinct().Count() != request.Almacenes.Count)
            return BadRequest(new { ok = false, message = "Hay un almacen repetido: no se sabria si dejarlo o quitarlo." });

        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return ResponderLote(
            await _service.GuardarAlmacenesDeUsuarioAsync(id, request, IdUsuarioLegacy(), Equipo(), ct),
            "Almacenes del usuario guardados.");
    }

    /// <summary>
    /// Guarda los procesos (botones de las formas de Mac31) del usuario, todos en
    /// una transaccion.
    ///
    /// Estas filas son la FUENTE de cuatro familias de permisos del web
    /// (reportes.*, ventas.*, empleados.*, prestamos.*): lo que se guarde aqui
    /// gobierna lo que el web muestra. Al contrario no funciona — lo que se dé para
    /// esas cuatro familias desde la pantalla de permisos del web se borra en el
    /// siguiente recalculo.
    /// </summary>
    [HttpPut("{id:int}/procesos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GuardarProcesos(
        [FromRoute] int id, [FromBody] ProcesosDeUsuarioRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        if (request.Procesos.Count == 0)
            return BadRequest(new { ok = false, message = "No hay procesos que guardar." });
        if (request.Procesos.Any(x => x.IdProceso <= 0))
            return BadRequest(new { ok = false, message = "Hay un renglon sin proceso." });
        if (request.Procesos.Any(x => x.IdStatus is not (1 or 2)))
            return BadRequest(new { ok = false, message = "El estatus de un proceso solo puede ser 1 o 2." });
        if (request.Procesos.Select(x => x.IdProceso).Distinct().Count() != request.Procesos.Count)
            return BadRequest(new { ok = false, message = "Hay un proceso repetido: no se sabria si darlo o quitarlo." });

        var sinPermiso = await ExigirPermisoAsync(PermisosProcesos, ct);
        if (sinPermiso != null) return sinPermiso;

        return ResponderLote(
            await _service.GuardarProcesosDeUsuarioAsync(id, request, IdUsuarioLegacy(), ct),
            "Procesos del usuario guardados.");
    }

    /// <summary>
    /// Guarda las fechas en las que el usuario puede operar, todas en una
    /// transaccion.
    /// </summary>
    [HttpPut("{id:int}/fechas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GuardarFechas(
        [FromRoute] int id, [FromBody] FechasDeUsuarioRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        if (request.Fechas.Count == 0)
            return BadRequest(new { ok = false, message = "No hay fechas que guardar." });
        if (request.Fechas.Any(x => x.Fecha == default))
            return BadRequest(new { ok = false, message = "Hay un renglon sin fecha." });
        if (request.Fechas.Any(x => x.IdStatus is not (1 or 2)))
            return BadRequest(new { ok = false, message = "El estatus de una fecha solo puede ser 1 o 2." });
        if (request.Fechas.Select(x => x.Fecha.Date).Distinct().Count() != request.Fechas.Count)
            return BadRequest(new { ok = false, message = "Hay una fecha repetida: no se sabria si dejarla o quitarla." });

        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return ResponderLote(
            await _service.GuardarFechasDeUsuarioAsync(id, request, IdUsuarioLegacy(), Equipo(), ct),
            "Fechas del usuario guardadas.");
    }

    // -----------------------------------------------------------------------

    private IActionResult Responder(SeguridadEscrituraResponse r, string mensajeExito)
        => Ok(new
        {
            ok = r.Guardo,
            message = r.Guardo
                ? mensajeExito
                : (string.IsNullOrWhiteSpace(r.Mensaje) ? "La base rechazo la operacion." : r.Mensaje),
            data = r
        });

    private IActionResult Responder(SeguridadEscrituraResponse? r, string mensajeExito, string noExiste)
        => r is null ? NotFound(new { ok = false, message = noExiste }) : Responder(r, mensajeExito);

    /// <summary>
    /// Respuesta de un lote. Cuando falla, `aplicados` viene en 0 y el mensaje dice
    /// en que renglon fue: no se aplico ninguno.
    /// </summary>
    private IActionResult ResponderLote(LoteSeguridadResponse r, string mensajeExito)
        => Ok(new
        {
            ok = r.Guardo,
            message = r.Guardo ? mensajeExito : r.Mensaje,
            data = r,
            aplicados = r.Aplicados
        });

    private static object? ValidaUsuario(CrearUsuarioMacRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Usuario))
            return new { ok = false, message = "El usuario es requerido." };
        if (r.Usuario.Trim().Length > 50)
            return new { ok = false, message = "El usuario no puede pasar de 50 caracteres." };
        if (string.IsNullOrWhiteSpace(r.Contrasena))
            return new { ok = false, message = "La contrasena es requerida." };
        if (r.Contrasena.Length > 400)
            return new { ok = false, message = "La contrasena no puede pasar de 400 caracteres." };
        if (string.IsNullOrWhiteSpace(r.Nombre))
            return new { ok = false, message = "El nombre es requerido." };
        if (r.Nombre.Trim().Length > 4000)
            return new { ok = false, message = "El nombre no puede pasar de 4000 caracteres." };
        if (r.IdPerfil <= 0)
            return new { ok = false, message = "El perfil es requerido." };
        if (r.AplicaContrasenaAutorizacion && string.IsNullOrWhiteSpace(r.ContrasenaAutorizacion))
            return new { ok = false, message = "Si aplica contrasena de autorizacion, hay que capturarla." };
        if (r.ContrasenaAutorizacion != null && r.ContrasenaAutorizacion.Length > 400)
            return new { ok = false, message = "La contrasena de autorizacion no puede pasar de 400 caracteres." };
        return null;
    }
}
