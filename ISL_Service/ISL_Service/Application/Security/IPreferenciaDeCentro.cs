namespace ISL_Service.Application.Security;

/// <summary>
/// EL CENTRO QUE CADA QUIEN ELIGIO LA ULTIMA VEZ, GUARDADO EN EL SERVIDOR.
/// </summary>
/// <remarks>
/// POR QUE EN EL SERVIDOR Y NO EN EL TELEFONO
/// ------------------------------------------
/// Porque el centro activo decide DE QUE BASE salen los datos. Un dato que
/// decide eso no se guarda donde el usuario lo puede editar: la preferencia es
/// una comodidad, pero si fuera la unica copia, un valor cambiado a mano en el
/// almacenamiento del telefono seria una peticion contra otra base. El servidor
/// la guarda, la cruza contra los permisos al leerla
/// (<see cref="CentroInicial"/>) y nunca se la cree a ciegas.
///
/// El telefono tambien la recuerda (contrato §6, KeyValueStorage), pero como
/// cache de arranque para no esperar al catalogo. Cuando las dos difieren, manda
/// esta.
///
/// FALLA BLANDO A PROPOSITO
/// ------------------------
/// Si la tabla no existe —una base que todavia no corrio el script—, leer
/// contesta <c>null</c> y guardar no hace nada, sin tirar la peticion. Quedarse
/// sin preferencia significa abrir en la matriz, que es exactamente lo que pasa
/// hoy; tirar la peticion por no poder guardar una comodidad seria cambiar un
/// detalle por una caida.
/// </remarks>
public interface IPreferenciaDeCentro
{
    /// <summary>
    /// El centro guardado de ese usuario, o <c>null</c> si nunca eligio o si no
    /// se pudo leer. <c>0</c> es un valor guardado valido y significa la matriz.
    /// </summary>
    Task<int?> LeerAsync(int empresaId, Guid usuarioWebId, CancellationToken ct);

    /// <summary>
    /// Guarda el centro elegido. <c>true</c> si quedo guardado.
    /// </summary>
    /// <remarks>
    /// Quien llame tiene que haber comprobado el permiso ANTES
    /// (con <c>CapacidadesDeCentros.PuedeVer</c>): este metodo guarda lo que le
    /// digan. Guardar aqui una preferencia no autorizada no abre ninguna puerta
    /// —la lectura la vuelve a cruzar contra los permisos— pero deja un valor
    /// que no significa nada y confunde al que lo lea en la base.
    /// </remarks>
    Task<bool> GuardarAsync(int empresaId, Guid usuarioWebId, int idCentro, CancellationToken ct);

    /// <summary>Borra la preferencia. Se usa al dejar de autorizar un centro.</summary>
    Task<bool> OlvidarAsync(int empresaId, Guid usuarioWebId, CancellationToken ct);
}
