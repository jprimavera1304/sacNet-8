using System.Security.Claims;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// EL GUARDIA QUE FALTABA EN EL SERVIDOR.
/// </summary>
/// <remarks>
/// Los tres metodos no devuelven nada: o dejan pasar, o tiran
/// <see cref="Application.Security.EscaladaDePermisosException"/> con el
/// mensaje que va a leer la persona. Se llaman ANTES de escribir.
///
/// POR QUE UN SERVICIO Y NO UNA POLITICA [Authorize]
/// -------------------------------------------------
/// Una politica contesta "¿tiene esta clave?" y aqui la pregunta es otra:
/// "¿lo que esta intentando guardar cabe dentro de lo que el mismo tiene, y
/// deja a alguien capaz de deshacerlo?". Eso depende del CUERPO de la peticion
/// y del estado de la base, no solo del token, asi que no hay politica que lo
/// pueda contestar. La politica sigue puesta y sigue hablando primero: estas
/// comprobaciones se suman, no la sustituyen.
///
/// POR QUE EN EL SERVIDOR, OTRA VEZ
/// --------------------------------
/// Las cuatro reglas de aqui ya existian en el front —ModalPermisosDeRol.tsx
/// no te deja quitarte a ti mismo, y nucleo/permisosWeb.ts marca los modulos
/// del sistema como no apagables—, y las cuatro se saltaban con un curl. El
/// front puede seguir escondiendo lo que quiera; la decision ya no vive alli.
/// </remarks>
public interface IGuardiaDePermisos
{
    /// <summary>
    /// Que el llamante puede dejar el rol <paramref name="roleCode"/> con
    /// exactamente las claves <paramref name="clavesPedidas"/>.
    /// </summary>
    Task ExigirPuedeEditarRolAsync(
        ClaimsPrincipal llamante,
        int empresaId,
        string roleCode,
        IReadOnlyCollection<string> clavesPedidas,
        CancellationToken ct);

    /// <summary>
    /// Que el llamante puede dejar los ajustes individuales de
    /// <paramref name="userId"/> con exactamente esos allow y deny.
    /// </summary>
    Task ExigirPuedeEditarOverridesAsync(
        ClaimsPrincipal llamante,
        int empresaId,
        Guid userId,
        IReadOnlyCollection<string> allow,
        IReadOnlyCollection<string> deny,
        CancellationToken ct);

    /// <summary>
    /// Que el llamante puede dejar a ese usuario con el rol
    /// <paramref name="rolNuevo"/>.
    /// </summary>
    /// <remarks>
    /// ES LA MISMA REGLA 1, APLICADA A LA OTRA PUERTA.
    ///
    /// Un rol es un paquete de claves, asi que asignarlo CONCEDE todo lo que
    /// el rol trae y RETIRA todo lo que el rol anterior traia y el nuevo no.
    /// Sin este freno la pantalla de usuarios era un atajo alrededor de la de
    /// permisos: quien no podia concederse una clave una por una se nombraba
    /// —o nombraba a un comodo— un rol que ya la trae, y el techo de
    /// ExigirPuedeEditarRolAsync no se enteraba porque ahi no se edita ningun
    /// rol.
    ///
    /// Se mira en los dos sentidos por el mismo motivo que en los overrides:
    /// degradar a alguien le quita claves, y quitar lo que no puedes devolver
    /// es la forma de desarmar a quien esta por encima.
    /// </remarks>
    Task ExigirPuedeAsignarRolAsync(
        ClaimsPrincipal llamante,
        int empresaId,
        string? rolActual,
        string rolNuevo,
        CancellationToken ct);

    /// <summary>
    /// Que apagar (o prender) ese modulo no deja la instalacion sin nadie
    /// capaz de administrar permisos. Prender nunca se bloquea.
    /// </summary>
    Task ExigirPuedeCambiarEstatusDeModuloAsync(
        ClaimsPrincipal llamante,
        int empresaId,
        string moduleKey,
        int idStatus,
        CancellationToken ct);
}
