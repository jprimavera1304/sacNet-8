using ISL_Service.Application.DTOs.Requests;
using ISL_Service.Application.DTOs.Responses;
using ISL_Service.Application.Security;

namespace ISL_Service.Application.Interfaces;

public interface IUserService
{
    /// <summary>
    /// Inicia sesion y firma el token.
    /// </summary>
    /// <param name="canal">
    /// EL CANAL YA RESUELTO, Y POR ESO ES EL TIPO Y NO UN TEXTO.
    ///
    /// Antes esto no era un parametro: el servicio volvia a derivar el canal de
    /// `request.Canal` con <c>CanalDeAcceso.Normalizar</c>, que sin la cabecera
    /// `X-Cliente-App` ni el User-Agent solo puede contestar WEB. Funcionaba de
    /// milagro —SesionController ya habia sobreescrito `request.Canal` antes de
    /// llamar, y era el unico camino de login vivo—, pero dejaba cargada la
    /// trampa: el dia que entre un segundo camino que llame aqui directo, TODA
    /// sesion que salga de el seria canal web, y a cada usuario de telefono se
    /// le apagarian en silencio sus claves `app_movil.*` (CanalDeAcceso.Filtrar)
    /// —dejaria de poder capturar pedidos, sin un 403 que lo explique—.
    ///
    /// Pidiendolo como <see cref="Canal"/> ya resuelto, derivarlo mal deja de
    /// ser posible: quien llame tiene que haberlo decidido antes, y el
    /// compilador no deja olvidarlo.
    /// </param>
    Task<LoginResponse> LoginAsync(LoginRequest request, Canal canal, CancellationToken ct);
    // Canjea un refresh token por un nuevo access token (y rota el refresh).
    Task<LoginResponse> RefreshAsync(string refreshToken, CancellationToken ct);
    // Revoca un refresh token (logout).
    Task LogoutAsync(string refreshToken, CancellationToken ct);
}
