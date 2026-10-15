using ISL_Service.Application.Security;
using ISL_Service.Domain.Entities;

namespace ISL_Service.Application.Interfaces;

public interface IJwtTokenGenerator
{
    /// <summary>
    /// Emite el access token.
    /// </summary>
    /// <param name="canal">
    /// El canal que el SERVIDOR resolvio para esta sesion (ver
    /// <see cref="CanalDeAcceso"/>). Va aqui y no en una cabecera porque
    /// dentro del token lo protege la firma: el cliente no puede cambiarlo sin
    /// la llave del servidor. Es el parametro que convierte "desde que canal
    /// llega esto" de una afirmacion del cliente en un dato.
    /// </param>
    string GenerateToken(Usuario user, string companyKey, Canal canal = Canal.Web);
}
