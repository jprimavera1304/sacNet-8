using ISL_Service.Application.DTOs.EscriturasLegacy;
using ISL_Service.Application.Exceptions;
using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Application.Services;

/*
  LAS DOS REGLAS QUE COMPARTEN TODAS LAS ESCRITURAS DE OBRA Y AVIACION.

  1. EL -1 NO ES UN DATO, ES UN ERROR.
     Estos sp_n_ no lanzan RAISERROR: cuando algo sale mal contestan
     `SELECT -1 AS result, 'ERROR' AS mensaje` y devuelven 200. Legacy pasaba ese
     renglon tal cual al cliente, asi que una alta que no se guardo llegaba al
     front como una respuesta exitosa con un -1 adentro, y la pantalla tenia que
     acordarse de mirarlo. La mitad de las veces no se acordaba.
     Aqui el -1 se vuelve un 409 con el texto del procedimiento.

  2. EL STATUS SOLO PUEDE SER 1 O 2.
     La convencion de toda la base nueva es 1 = Activo, 2 = Cancelado/Inactivo.
     Legacy mandaba al SP el IDStatus que viniera en el cuerpo sin mirarlo, y
     estos SP hacen `SET IDStatus = @IDStatus` a pelo: un 0 por omision dejaba el
     renglon en un status que no existe, invisible en todas las pantallas (que
     filtran por 1 o por 2) y sin forma de reactivarlo desde la aplicacion. Eso
     se vio en la ruta /conceptos/eliminar de Legacy, que ni siquiera exige el
     status y aun asi actualiza el renglon.
*/
public static class EscrituraLegacyReglas
{
    /// <summary>
    /// Traduce el renglon (result, mensaje) del SP. Si el SP dijo -1, lanza 409.
    /// </summary>
    /// <param name="sp">Lo que contesto el procedimiento.</param>
    /// <param name="queSeIntentaba">
    /// Para el mensaje de error: "dar de alta el puesto", "cancelar la poliza".
    /// </param>
    public static ResultadoEscrituraDto Traducir(ResultadoSp sp, string queSeIntentaba)
    {
        if (sp.Result < 0)
        {
            var detalle = string.IsNullOrWhiteSpace(sp.Mensaje) ? null : sp.Mensaje;
            throw new ConflictException($"No se pudo {queSeIntentaba}.", detalle);
        }

        return new ResultadoEscrituraDto { Id = sp.Result, Mensaje = sp.Mensaje };
    }

    /// <summary>
    /// 1 o 2; cualquier otra cosa es 400. El 0 por omision de Legacy no pasa.
    /// </summary>
    public static int ExigirStatus(int idStatus)
    {
        if (idStatus != 1 && idStatus != 2)
            throw new ArgumentException("idStatus solo puede ser 1 (activo) o 2 (inactivo).");
        return idStatus;
    }

    /// <summary>Un id de renglon tiene que venir y ser positivo.</summary>
    public static int ExigirId(int id, string nombre)
    {
        if (id <= 0) throw new ArgumentException($"{nombre} es requerido.");
        return id;
    }

    /// <summary>Un texto obligatorio, ya recortado.</summary>
    public static string ExigirTexto(string? valor, string nombre, int maximo)
    {
        var limpio = (valor ?? string.Empty).Trim();
        if (limpio.Length == 0) throw new ArgumentException($"{nombre} es requerido.");
        if (limpio.Length > maximo) throw new ArgumentException($"{nombre} no puede pasar de {maximo} caracteres.");
        return limpio;
    }
}
