namespace ISL_Service.Application.Models;

/// <summary>
/// El cambio que TODAVIA NO SE HA HECHO, descrito para poder preguntarle a la
/// base "¿quien quedaria administrando permisos si esto se guardara?".
/// </summary>
/// <remarks>
/// POR QUE SIMULAR Y NO GUARDAR-Y-COMPROBAR
/// ----------------------------------------
/// La alternativa era escribir el cambio, volver a calcular y deshacer si
/// quedaba mal. No sirve por dos razones medidas en este mismo codigo:
///
///   * UpsertRolePermissionsAsync no es una sola transaccion: su camino
///     normal llama al procedimiento sp_w_RolPermisos_ReemplazarPorClaves y
///     DESPUES sincroniza cuatro familias de legacy (reportes, ventas,
///     empleados, prestamos) fuera de la transaccion. Un "deshacer" posterior
///     no puede devolver eso al estado anterior.
///   * SetModuleStatusAsync invalida la cache de permisos de TODA la empresa al
///     terminar. Entre el guardado y el deshacer, cualquier peticion de
///     cualquier usuario ya se habria decidido con los permisos equivocados.
///
/// Un cambio que deja la instalacion sin administradores no se puede permitir
/// "un ratito". Se decide ANTES.
///
/// Todos los campos son opcionales: cada llamada describe UN cambio y deja el
/// resto en null, que significa "esto no se toca".
/// </remarks>
/// <param name="RolCodigoReemplazado">
/// El codigo del rol cuyas claves se van a reemplazar, o null.
/// </param>
/// <param name="ClavesDelRol">
/// Las claves que tendria ese rol DESPUES del cambio. Lista vacia = el rol se
/// queda sin ninguna.
/// </param>
/// <param name="UsuarioReemplazado">
/// El usuario web cuyos ajustes individuales se van a reemplazar, o null.
/// </param>
/// <param name="AllowDelUsuario">Los "allow" que tendria despues del cambio.</param>
/// <param name="DenyDelUsuario">Los "deny" que tendria despues del cambio.</param>
/// <param name="ModuloApagado">
/// La clave del modulo que quedaria APAGADO con este cambio, o null. Un modulo
/// apagado no da ninguno de sus permisos (ver LoadEffectivePermissionsAsync),
/// asi que apagarlo cuenta como quitarle a todo el mundo todas sus claves.
/// </param>
public sealed record SimulacionDePermisos(
    string? RolCodigoReemplazado = null,
    IReadOnlyCollection<string>? ClavesDelRol = null,
    Guid? UsuarioReemplazado = null,
    IReadOnlyCollection<string>? AllowDelUsuario = null,
    IReadOnlyCollection<string>? DenyDelUsuario = null,
    string? ModuloApagado = null);

/// <summary>
/// Quien seguiria pudiendo administrar permisos despues del cambio simulado.
/// </summary>
/// <remarks>
/// Las dos capacidades se cuentan POR SEPARADO a proposito: ver el porque en
/// <see cref="ISL_Service.Application.Security.LlavesDeGobierno.Conceder"/>.
///
/// <paramref name="NoSePudoComprobar"/> existe porque este calculo lee tablas
/// que una instalacion puede no tener (WRol, WRolPermiso, UsuarioWeb...). Si
/// no se puede contestar la pregunta, NO se bloquea: una base sin el modelo de
/// capacidades no tiene este problema —no hay permisos que repartir— y negar
/// ahi dejaria muerta una instalacion que hoy funciona. Lo que no se pudo
/// comprobar no se cierra, igual que en EntradasDeModulo.
/// </remarks>
public sealed record AdministracionRestante(
    IReadOnlyList<string> PuedenConceder,
    IReadOnlyList<string> PuedenReactivarModulos,
    bool NoSePudoComprobar);
