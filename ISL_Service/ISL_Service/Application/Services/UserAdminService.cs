using System.Security.Claims;
using Microsoft.Extensions.Logging;
using ISL_Service.Application.DTOs.Requests;
using ISL_Service.Application.DTOs.Responses;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using ISL_Service.Domain.Entities;

namespace ISL_Service.Application.Services;

public class UserAdminService : IUserAdminService
{
    // Estados segun regla:
    // 1 = Activo, 2 = Inactivo, 3 = Bloqueado
    private const int ESTADO_ACTIVO = 1;
    private const int ESTADO_INACTIVO = 2;
    private const int ESTADO_BLOQUEADO = 3;

    private readonly IUserRepository _repo;
    private readonly IPermissionService _permissionService;
    private readonly IGuardiaDePermisos _guardia;

    private readonly ILogger<UserAdminService> _logger;

    public UserAdminService(
        IUserRepository repo,
        IPermissionService permissionService,
        IGuardiaDePermisos guardia,
        ILogger<UserAdminService> logger)
    {
        _repo = repo;
        _permissionService = permissionService;
        _guardia = guardia;
        _logger = logger;
    }

    public async Task<CreateUserResponse> CreateUserAsync(CreateUserRequest req, ClaimsPrincipal actor, CancellationToken ct)
    {
        var usuarioNombre = NormalizeUsuario(req.Usuario);
        var rol = await NormalizeRolAsync(req.Rol, rolActual: null, actor, ct);

        if (await _repo.ExistsByUsuarioAsync(usuarioNombre, ct))
            throw new ConflictException("El usuario ya existe.");

        var password = NormalizePassword(req.ResolverPassword());
        var hash = BCrypt.Net.BCrypt.HashPassword(password);

        // Sin cambio forzado: la contrasena que escribio el administrador es la
        // definitiva, y el usuario entra con ella a la primera.
        // Si no mandan nombre se usa el usuario, como se hacia antes: asi una
        // peticion vieja sigue funcionando igual.
        var nombrePersona = string.IsNullOrWhiteSpace(req.Nombre)
            ? usuarioNombre
            : req.Nombre!.Trim();

        var entity = await _repo.UpsertWebAndLegacyAsync(
            usuarioNombre,
            password,
            hash,
            nombrePersona,
            rol,
            debeCambiarContrasena: false,
            estado: ESTADO_ACTIVO,
            ct);

        /*
          Usuario nuevo debe iniciar con permisos efectivos del rol (sin
          overrides heredados/previos).

          ANTES ESTE catch NO TENIA FILTRO NI REGISTRO.
          ---------------------------------------------
          El bloque existe por UNA razon concreta: en una base donde el esquema
          de capacidades todavia no esta instalado, limpiar overrides no se
          puede y no debe impedir dar de alta a nadie. Eso es correcto.

          Pero el catch se comia TODO. Si la limpieza fallaba por cualquier otro
          motivo —un error de la base, un interbloqueo, un permiso de SQL— el
          usuario quedaba creado CON LOS OVERRIDES QUE HUBIERA, y la API
          contestaba 200 como si nada. O sea: alguien recien dado de alta podia
          nacer con permisos que nadie le dio, sin una sola linea en el registro.

          Ahora:
            - Si el esquema no esta (KeyNotFoundException, que es lo que lanza
              PermissionService cuando faltan las tablas de capacidades), se
              sigue adelante, pero QUEDA ESCRITO.
            - Cualquier otro fallo se registra y SE RELANZA. Crear a medias un
              usuario con permisos residuales es peor que fallar: fallar se ve.
        */
        try
        {
            await _permissionService.UpsertUserOverridesAsync(
                entity.EmpresaId,
                entity.Id,
                Array.Empty<string>(),
                Array.Empty<string>(),
                // NO sincronizar con Mac31: aqui solo se limpian overrides
                // residuales del usuario web. Con true, dar de alta la cuenta
                // web de alguien que ya trabajaba en Mac31 le apagaba todos
                // sus permisos del escritorio.
                sincronizarConLegacy: false,
                ct);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex,
                "El esquema de capacidades no está disponible en esta base: el usuario {Usuario} (id={UserId}, empresa={EmpresaId}) se creó sin limpiar overrides. No hay overrides que limpiar mientras el esquema no exista.",
                usuarioNombre, entity.Id, entity.EmpresaId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Falló la limpieza de overrides del usuario nuevo {Usuario} (id={UserId}, empresa={EmpresaId}). Se corta aquí: dejarlo pasar lo dejaría con permisos residuales y contestando 200.",
                usuarioNombre, entity.Id, entity.EmpresaId);
            throw;
        }

        return new CreateUserResponse
        {
            User = Map(entity),
            Password = password
        };
    }

    public async Task<List<UserResponse>> ListUsersAsync(ClaimsPrincipal actor, CancellationToken ct)
    {
        var empresaId = ResolveEmpresaId(actor);
        var list = await _repo.ListAsync(empresaId, ct);

        /* Si esto falla, la lista se entrega igual sin la columna: no saber si
           alguien esta en Mac31 es una molestia, no poder ver los usuarios es
           quedarse sin la pantalla. */
        Dictionary<string, int> enlaces;
        try
        {
            enlaces = await _repo.ListLegacyLinksAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo leer el enlace con Mac31; la lista va sin esa columna.");
            enlaces = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        return list.Select(u =>
        {
            var respuesta = Map(u);
            if (enlaces.TryGetValue(u.UsuarioNombre.Trim(), out var idLegacy))
            {
                respuesta.ExisteEnMac31 = true;
                respuesta.IdUsuarioMac31 = idLegacy;
            }
            return respuesta;
        }).ToList();
    }

    public async Task<List<UserRoleOptionResponse>> ListRolesCatalogAsync(ClaimsPrincipal actor, CancellationToken ct)
    {
        var empresaId = ResolveEmpresaId(actor);
        var roles = await _repo.ListRolesCatalogAsync(empresaId, ct);
        return roles
            .Select(r =>
            {
                var code = NormalizeRoleCode(r.Code);
                return new UserRoleOptionResponse
                {
                    Code = code,
                    Name = string.IsNullOrWhiteSpace(r.Name) ? code : r.Name.Trim(),
                    Value = ToStoredRolValue(code)
                };
            })
            .Where(r => !string.IsNullOrWhiteSpace(r.Code))
            .GroupBy(r => r.Code, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(r => r.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<UserResponse> UpdateUserAsync(Guid userId, UpdateUserRequest req, ClaimsPrincipal actor, CancellationToken ct)
    {
        var user = await _repo.GetByIdAsync(userId, ct) ?? throw new NotFoundException("Usuario no encontrado.");

        EnsureActorCanManageTarget(actor, user);

        var usuarioNuevo = NormalizeUsuario(req.Usuario);
        var rolNuevo = await NormalizeRolAsync(req.Rol, rolActual: user.Rol, actor, ct);

        var existing = await _repo.GetByUsuarioAsync(usuarioNuevo, ct);
        if (existing is not null && existing.Id != userId)
            throw new ConflictException("El usuario ya existe.");

        var updated = await _repo.UpdateUsuarioAndRolAsync(userId, usuarioNuevo, rolNuevo, ct);

        // La contrasena es opcional al editar: dejarla vacia significa "no la toques".
        if (!string.IsNullOrWhiteSpace(req.Password))
        {
            var password = NormalizePassword(req.Password!);
            var hash = BCrypt.Net.BCrypt.HashPassword(password);
            var nombrePersona = string.IsNullOrWhiteSpace(req.Nombre)
                ? usuarioNuevo
                : req.Nombre!.Trim();

            updated = await _repo.UpsertWebAndLegacyAsync(
                usuarioNuevo,
                password,
                hash,
                nombrePersona,
                rolNuevo,
                debeCambiarContrasena: false,
                estado: updated.Estado,
                ct);
        }

        return Map(updated);
    }

    public async Task<UserResponse> UpdateEstadoAsync(Guid userId, UpdateUserEstadoRequest req, ClaimsPrincipal actor, CancellationToken ct)
    {
        var user = await _repo.GetByIdAsync(userId, ct) ?? throw new NotFoundException("Usuario no encontrado.");

        EnsureActorCanManageTarget(actor, user);
        var updated = await _repo.UpdateEstadoWithLegacyAsync(userId, req.Estado, ct);
        return Map(updated);
    }

    public async Task<ResetPasswordResponse> ResetPasswordAsync(Guid userId, ClaimsPrincipal actor, CancellationToken ct)
    {
        var user = await _repo.GetByIdAsync(userId, ct) ?? throw new NotFoundException("Usuario no encontrado.");

        EnsureActorCanManageTarget(actor, user);

        // Sin contrasena a la mano se genera una, pero ya no es "temporal": queda
        // como la definitiva y el administrador la puede ver con el ojito.
        var generada = PasswordGenerator.Generate(12);
        return await AplicarPasswordAsync(user, generada, ct);
    }

    public async Task<ResetPasswordResponse> SetPasswordAsync(Guid userId, SetUserPasswordRequest req, ClaimsPrincipal actor, CancellationToken ct)
    {
        var user = await _repo.GetByIdAsync(userId, ct) ?? throw new NotFoundException("Usuario no encontrado.");

        EnsureActorCanManageTarget(actor, user);

        return await AplicarPasswordAsync(user, NormalizePassword(req.ResolverPassword()), ct);
    }

    public async Task<UserPasswordResponse> GetPasswordAsync(Guid userId, ClaimsPrincipal actor, CancellationToken ct)
    {
        var user = await _repo.GetByIdAsync(userId, ct) ?? throw new NotFoundException("Usuario no encontrado.");

        EnsureActorCanManageTarget(actor, user);

        var password = await _repo.GetLegacyPasswordAsync(user.UsuarioNombre, ct);

        // El hash de UsuarioWeb no se puede revertir. Si legacy no tiene al usuario
        // no hay contrasena que mostrar, y eso se dice claro en vez de tronar.
        if (string.IsNullOrEmpty(password))
        {
            return new UserPasswordResponse
            {
                UserId = user.Id,
                Usuario = user.UsuarioNombre,
                Password = null,
                Disponible = false,
                Mensaje = "No hay contrasena guardada en legacy para este usuario. Asigne una nueva para poder verla."
            };
        }

        return new UserPasswordResponse
        {
            UserId = user.Id,
            Usuario = user.UsuarioNombre,
            Password = password,
            Disponible = true
        };
    }

    private async Task<ResetPasswordResponse> AplicarPasswordAsync(Usuario user, string password, CancellationToken ct)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(password);

        // Un solo camino para las dos escrituras: el SP deja el hash en UsuarioWeb
        // y la contrasena en claro en legacy. Si esto se partiera en dos, los dos
        // lados se desincronizarian a la primera falla.
        var updated = await _repo.UpsertWebAndLegacyAsync(
            user.UsuarioNombre,
            password,
            hash,
            user.UsuarioNombre,
            user.Rol,
            debeCambiarContrasena: false,
            estado: user.Estado,
            ct);

        return new ResetPasswordResponse
        {
            UserId = updated.Id,
            Password = password,
            DebeCambiarContrasena = false
        };
    }

    private static string NormalizePassword(string password)
    {
        var value = (password ?? string.Empty).Trim();
        if (value.Length < 8 || value.Length > 100)
            throw new ArgumentException("Contrasena invalida. Longitud permitida: 8 a 100.");
        return value;
    }

    /*
      CAMBIAR LA EMPRESA DE UN USUARIO: POR QUE CONTESTABA 500 Y QUE CONTESTA
      AHORA.

      Esto decia:

          return Task.FromException<UserResponse>(
              new InvalidOperationException("En este modelo por base de datos, cada base maneja una sola empresa."));

      `InvalidOperationException` no esta en la lista de
      ExceptionHandlingMiddleware, asi que caia en el catch general y salia como
      **500 "Error interno."** — SIEMPRE, con cualquier cuerpo. Una capacidad
      retirada a proposito disfrazada de averia del servidor: el que llama no
      puede distinguir "esto no se hace aqui" de "el backend esta roto", y
      reintentar no sirve de nada. En Swagger el endpoint solo declaraba 200.

      LA REGLA DEL MODELO: una base = una empresa (ver README_EMPRESAS_DB.md y
      la seccion 4 del inventario). El `EmpresaId` del usuario no es una
      eleccion suya: es la empresa de la base en la que vive. Mover a alguien a
      otra empresa seria moverlo a OTRA BASE, que es un alta alla y una baja
      aqui, no un PATCH.

      Asi que ahora el endpoint contesta de verdad:

        * Piden la empresa que ya tiene -> 200 con el usuario, sin tocar nada.
          Es idempotente y es lo que un cliente que reenvia su formulario
          completo necesita: no se le revienta la pantalla por mandar el valor
          que ya estaba.
        * Piden otra empresa -> 409 CONFLICTO, con el motivo escrito. No es un
          error del servidor: es una peticion que este modelo no puede cumplir.
        * Piden una empresa <= 0 -> 400, que es lo que es.

      No se borro la ruta ni el permiso `usuarios.empresa.editar`: la pantalla
      de usuarios existe, el campo se ensena, y dejarla sin ruta cambiaria el
      404 por otro mensaje igual de mudo. Lo que se arregla es que diga la
      verdad con el codigo correcto.
    */
    public async Task<UserResponse> UpdateEmpresaAsync(Guid userId, UpdateUserEmpresaRequest req, ClaimsPrincipal actor, CancellationToken ct)
    {
        if (req is null || req.EmpresaId <= 0)
            throw new ArgumentException("EmpresaId es obligatorio y debe ser mayor que cero.");

        var user = await _repo.GetByIdAsync(userId, ct) ?? throw new NotFoundException("Usuario no encontrado.");

        EnsureActorCanManageTarget(actor, user);

        if (user.EmpresaId == req.EmpresaId)
            return Map(user);

        throw new ConflictException(
            "No se puede cambiar la empresa de un usuario: en este modelo cada base de datos maneja una sola empresa, "
            + $"y este usuario pertenece a la empresa {user.EmpresaId} porque es la de esta base. "
            + "Para que exista en otra empresa hay que darlo de alta en la base de esa empresa.",
            $"EmpresaId actual: {user.EmpresaId}. EmpresaId solicitado: {req.EmpresaId}.");
    }

    public async Task ChangeMyPasswordAsync(ChangePasswordRequest req, ClaimsPrincipal actor, CancellationToken ct)
    {
        var myId = CurrentUser.GetUserId(actor);
        var user = await _repo.GetByIdAsync(myId, ct) ?? throw new NotFoundException("Usuario no encontrado.");

        if (user.Estado != ESTADO_ACTIVO)
        {
            if (user.Estado == ESTADO_INACTIVO) throw new UnauthorizedAccessException("Usuario inactivo.");
            if (user.Estado == ESTADO_BLOQUEADO) throw new UnauthorizedAccessException("Usuario bloqueado.");
            throw new UnauthorizedAccessException("Usuario no autorizado.");
        }

        if (!BCrypt.Net.BCrypt.Verify(req.ContrasenaActual, user.ContrasenaHash))
            throw new InvalidOperationException("Contrasena actual incorrecta.");

        var hashNueva = BCrypt.Net.BCrypt.HashPassword(req.NuevaContrasena);

        await _repo.UpsertWebAndLegacyAsync(
            user.UsuarioNombre,
            req.NuevaContrasena,
            hashNueva,
            user.UsuarioNombre,
            user.Rol,
            debeCambiarContrasena: false,
            estado: user.Estado,
            ct);
    }

    private static void EnsureActorCanManageTarget(ClaimsPrincipal actor, Usuario target)
    {
        var isSuper = CurrentUser.IsSuperAdmin(actor);
        if (isSuper) return;

        var actorEmpresaId = CurrentUser.GetEmpresaId(actor);
        if (actorEmpresaId != target.EmpresaId)
            throw new UnauthorizedAccessException("No puedes administrar usuarios fuera de tu empresa.");

        // Evita que Admin administre a SuperAdmin
        if (string.Equals(target.Rol, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("No puedes administrar usuarios SuperAdmin.");
    }

    private static UserResponse Map(Usuario u) => new()
    {
        Id = u.Id,
        Usuario = u.UsuarioNombre,
        Rol = u.Rol,
        EmpresaId = u.EmpresaId,
        Estado = u.Estado,
        DebeCambiarContrasena = u.DebeCambiarContrasena,
        FechaCreacion = u.FechaCreacion,
        FechaActualizacion = u.FechaActualizacion
    };

    public async Task<UserResponse> GetUserByIdAsync(Guid userId, ClaimsPrincipal actor, CancellationToken ct)
    {
        var user = await _repo.GetByIdAsync(userId, ct) ?? throw new NotFoundException("Usuario no encontrado.");

        EnsureActorCanManageTarget(actor, user);

        return Map(user);
    }

    private static string NormalizeUsuario(string usuario)
    {
        var normalized = (usuario ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized.Length < 4 || normalized.Length > 60)
            throw new ArgumentException("Usuario inválido. Longitud permitida: 4 a 60.");

        foreach (var ch in normalized)
        {
            var isValid = (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') || ch == '_';
            if (!isValid)
                throw new ArgumentException("Usuario inválido. Usa A-Z, 0-9 y _.");
        }

        return normalized;
    }

    private async Task<string> NormalizeRolAsync(string rol, string? rolActual, ClaimsPrincipal actor, CancellationToken ct)
    {
        var input = (rol ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Rol inválido. Seleccione un rol.");

        var inputCode = NormalizeRoleCode(input);
        var roles = await _repo.ListRolesCatalogAsync(ResolveEmpresaId(actor), ct);
        var match = roles.FirstOrDefault(r =>
            string.Equals(NormalizeRoleCode(r.Code), inputCode, StringComparison.OrdinalIgnoreCase) ||
            string.Equals((r.Name ?? string.Empty).Trim(), input, StringComparison.OrdinalIgnoreCase));

        if (match is null)
            throw new ArgumentException("Rol inválido. No existe en catálogo de roles.");

        var codigoFinal = NormalizeRoleCode(match.Code);

        /*
          SOLO UN SuperAdmin PUEDE NOMBRAR A OTRO SuperAdmin.

          Sin esto habia una escalada total, y se comprobo que las tres piezas
          estaban puestas el 2026-10-10:

            1. UpdateUserRequest y CreateUserRequest llevan el campo `Rol`, y
               los PUT/PATCH de UsuariosController solo exigen la politica
               `perm:usuarios.editar` (crear, `perm:usuarios.crear`).
            2. "SUPER_ADMIN / SuperAdmin" SI esta en el catalogo de roles de
               las DOS empresas (WRol de MacZ y de Produccion_svr), asi que la
               validacion de arriba lo aceptaba sin mas.
            3. PermissionAuthorizationHandler, en cuanto el rol del token es
               "SuperAdmin", hace context.Succeed(requirement) y SE VA, sin
               mirar el snapshot de permisos.

          Encadenadas significan que cualquiera con `usuarios.editar` podia
          mandar su propio renglon con Rol = "SuperAdmin" y quedarse con TODO,
          saltandose el modelo de permisos entero.

          EnsureActorCanManageTarget no lo tapaba y conviene entender por que:
          esa comprobacion mira el rol que el destino tiene YA ("no puedes
          administrar usuarios SuperAdmin"), no el que se le va a poner. Con el
          destino siendo un usuario normal —o uno mismo— pasaba limpio.

          La regla es la misma que ya gobierna el reparto de permisos (ver
          GuardiaDePermisos): nadie concede una autoridad que no tiene. Esta de
          aqui cubre el unico rol que SALTA el modelo —SuperAdmin pasa la
          autorizacion sin mirar el snapshot, asi que su paquete de claves no
          describe lo que puede—; para todos los demas el techo general lo pone
          ExigirPuedeAsignarRolAsync, unas lineas mas abajo.
        */
        if (string.Equals(codigoFinal, "SUPER_ADMIN", StringComparison.OrdinalIgnoreCase)
            && !CurrentUser.IsSuperAdmin(actor))
        {
            throw new UnauthorizedAccessException(
                "Solo un SuperAdmin puede asignar el rol SuperAdmin.");
        }

        /*
          EL TECHO GENERAL: NADIE REPARTE UN ROL QUE LE QUEDE GRANDE.

          Sin esto la pantalla de usuarios era un atajo alrededor de la de
          permisos. Un rol es un paquete de claves, asi que asignarlo concede
          todas las que trae: quien no podia concederse una clave suelta se
          nombraba —o nombraba a alguien de confianza— un rol que ya la
          incluye, y el guardia de la pantalla de permisos no se enteraba
          porque ahi no se estaba editando ningun rol.

          Se traduce a UnauthorizedAccessException para que salga el 403 con su
          mensaje por el mismo camino que la regla de arriba.
        */
        try
        {
            await _guardia.ExigirPuedeAsignarRolAsync(
                actor, ResolveEmpresaId(actor), rolActual, codigoFinal, ct);
        }
        catch (EscaladaDePermisosException ex)
        {
            throw new UnauthorizedAccessException(ex.Message);
        }

        return ToStoredRolValue(codigoFinal);
    }

    private static string NormalizeRoleCode(string roleCode)
    {
        return string.Concat((roleCode ?? string.Empty)
                .Trim()
                .ToUpperInvariant()
                .Select(ch =>
                {
                    if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9')) return ch;
                    if (ch == '_' || ch == '-' || char.IsWhiteSpace(ch)) return '_';
                    return '\0';
                })
                .Where(ch => ch != '\0'))
            .Trim('_');
    }

    private static string ToStoredRolValue(string roleCode)
    {
        if (string.Equals(roleCode, "SUPER_ADMIN", StringComparison.OrdinalIgnoreCase)) return "SuperAdmin";
        if (string.Equals(roleCode, "ADMIN", StringComparison.OrdinalIgnoreCase)) return "Admin";
        if (string.Equals(roleCode, "USER", StringComparison.OrdinalIgnoreCase)) return "User";
        return roleCode;
    }

    private static int ResolveEmpresaId(ClaimsPrincipal? actor = null)
    {
        var id = actor is null ? 0 : CurrentUser.GetEmpresaId(actor);
        return id > 0 ? id : 1;
    }
}
