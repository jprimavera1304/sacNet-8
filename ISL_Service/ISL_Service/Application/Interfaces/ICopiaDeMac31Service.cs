using ISL_Service.Application.DTOs.Responses;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// COPIAR UNA VEZ LO QUE MAC31 LE DA A UNA PERSONA, EN VEZ DE HEREDARLO SIEMPRE.
/// </summary>
/// <remarks>
/// EL PROBLEMA QUE VIENE A RESOLVER
/// --------------------------------
/// El puente con Mac31 hereda EN VIVO: en cada calculo borra del web todo lo
/// que empiece por los prefijos de las ~26 familias de Mac31 y lo repone
/// leyendo [Usuario Forma Procesos]. Por eso, medido el 2026-11-04 en Tauro:
///
///   * marcar "quitado" en la pantalla web no pega -Mac31 lo devuelve-;
///   * conceder una clave de esas familias con un INSERT queda escrito y sin
///     efecto;
///   * no hay forma de decir "esta persona SI puede esto en Mac31 pero NO en
///     el web".
///
/// Este servicio ofrece la salida: COPIAR una vez a una persona lo que Mac31
/// le da hoy, dejandolo como excepciones propias del web. Desde ese momento esa
/// persona deja de heredar en vivo y manda el web. Son dos sistemas: quitarle
/// algo en el web NO se lo quita en Mac31.
///
/// POR QUE ES UNA INTERFAZ APARTE Y NO DOS METODOS MAS EN IPermissionService
/// ------------------------------------------------------------------------
/// Por dos razones practicas y una de fondo. Las practicas: IPermissionService
/// la implementan a mano siete dobles de prueba, y agregarle metodos obliga a
/// tocar los siete por algo que no prueban; y hay otra persona trabajando en
/// ese archivo. La de fondo: esto no es "una capacidad mas del calculo de
/// permisos", es la operacion de administracion que corta la herencia. Tenerla
/// aparte deja claro quien la llama (una pantalla de administracion) y quien
/// no (el calculo de cada peticion).
/// </remarks>
public interface ICopiaDeMac31Service
{
    /// <summary>
    /// Quien de esta empresa ya tiene sus permisos copiados, o sea quien ya NO
    /// hereda en vivo de Mac31.
    /// </summary>
    /// <remarks>
    /// Vacio tambien cuando las tablas de la copia no existen todavia en la
    /// base: una base sin instalar significa "nadie esta copiado", nunca un
    /// error. Es lo que mantiene el comportamiento de hoy intacto.
    /// </remarks>
    Task<IReadOnlyList<CopiaDeMac31Item>> ListarCopiasDeMac31Async(int empresaId, CancellationToken ct);

    /// <summary>
    /// Copia a esa persona lo que Mac31 le da HOY y la saca de la herencia en
    /// vivo. Si ya estaba copiada, no hace nada y lo dice.
    /// </summary>
    Task<CopiaDeMac31Resultado> CopiarDeMac31Async(int empresaId, Guid userId, string? copiadoPor, CancellationToken ct);

    /// <summary>
    /// Deshace la copia: devuelve los ajustes del web al estado anterior y esa
    /// persona vuelve a heredar en vivo de Mac31.
    /// </summary>
    /// <returns>Falso si no habia copia que deshacer.</returns>
    Task<bool> DeshacerCopiaDeMac31Async(int empresaId, Guid userId, CancellationToken ct);
}
