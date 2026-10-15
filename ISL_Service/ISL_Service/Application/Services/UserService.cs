using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using ISL_Service.Application.DTOs.Requests;
using ISL_Service.Application.DTOs.Responses;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using ISL_Service.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;

namespace ISL_Service.Application.Services;

public class UserService : IUserService
{
    // Vida del refresh token (rolling): se renueva en cada uso.
    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(60);

    private readonly IUserRepository _repo;
    private readonly IJwtTokenGenerator _jwt;
    private readonly IEmpresaRepository _empresas;
    private readonly IMemoryCache _cache;
    private readonly IRefreshTokenRepository _refresh;

    public UserService(IUserRepository repo, IJwtTokenGenerator jwt, IEmpresaRepository empresas, IMemoryCache cache, IRefreshTokenRepository refresh)
    {
        _repo = repo;
        _jwt = jwt;
        _empresas = empresas;
        _cache = cache;
        _refresh = refresh;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var usuario = request.Usuario.Trim();
        var companyKey = await _cache.GetOrCreateAsync("companyKey", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return await _empresas.GetCompanyKeyAsync(ct);
        }) ?? await _empresas.GetCompanyKeyAsync(ct);

        var esIsl = string.Equals(companyKey, "isl", StringComparison.OrdinalIgnoreCase);

        // 1) Intenta login WEB sin hash (rápido)
        var login = await _repo.LoginWithFallbackAsync(usuario, request.Contrasena, null, ct);
        if (login is null && !esIsl)
        {
            // 2) Solo si hace falta, genera hash para promoción legacy
            var hashParaPromocion = BCrypt.Net.BCrypt.HashPassword(request.Contrasena);
            login = await _repo.LoginWithFallbackAsync(usuario, request.Contrasena, hashParaPromocion, ct);
        }

        if (login is null) throw new AuthenticationException("Credenciales invalidas.");

        // Estados: 1=Activo, 2=Inactivo, 3=Bloqueado
        if (login.Estado != 1)
        {
            if (login.Estado == 2) throw new AuthenticationException("Usuario inactivo.");
            if (login.Estado == 3) throw new AuthenticationException("Usuario bloqueado.");
            throw new AuthenticationException("Usuario no autorizado.");
        }

        if (string.Equals(login.Source, "WEB", StringComparison.OrdinalIgnoreCase) &&
            !BCrypt.Net.BCrypt.Verify(request.Contrasena, login.ContrasenaHash))
        {
            throw new AuthenticationException("Credenciales invalidas.");
        }

        var user = new Usuario
        {
            Id = login.UserId,
            LegacyUserId = login.LegacyUserId,
            UsuarioNombre = login.Usuario,
            ContrasenaHash = login.ContrasenaHash,
            Rol = login.Rol,
            EmpresaId = login.EmpresaId,
            DebeCambiarContrasena = login.DebeCambiarContrasena,
            Estado = login.Estado
        };

        /*
          EL CANAL SE RESUELVE AQUI, EN EL SERVIDOR, Y SE FIRMA.

          Es el unico momento en que el cliente puede opinar sobre su canal, y
          opinar le CUESTA en las dos direcciones (ver CanalDeAcceso): "movil"
          le exige `app_movil.acceso`, "web" le apaga las claves `app_movil.*`.
          Por eso se puede aceptar lo que declare sin abrir nada.

          No se valida aqui si tiene la llave maestra: el token se emite igual.
          Hace falta que se emita para que la app pueda leer /api/me y pintar
          "Sin acceso a la app" en vez de un error de red. Lo que se cierra es
          TODO lo demas, y se cierra en AccesoMovilMiddleware.
        */
        // Ya viene normalizado por SesionController (que es quien ve el
        // User-Agent). Se vuelve a pasar por Normalizar y no se confia en el
        // texto tal cual por si algun dia entra un login por otro camino: lo
        // que se firma tiene que ser siempre una de las dos constantes.
        var canal = CanalDeAcceso.Normalizar(request.Canal);
        var token = _jwt.GenerateToken(user, companyKey, canal);

        // Solo la app pide refresh; el web no lo manda -> se comporta como hoy.
        string? refreshToken = null;
        if (request.IncluirRefresh)
        {
            var identity = new RefreshTokenIdentity(user.Id, login.LegacyUserId, user.UsuarioNombre, user.Rol, user.EmpresaId, companyKey, canal);
            refreshToken = await IssueRefreshAsync(identity, ct);
        }

        return new LoginResponse
        {
            Token = token,
            UserId = user.Id,
            IdUsuario = login.LegacyUserId,
            Usuario = user.UsuarioNombre,
            Rol = user.Rol,
            EmpresaId = user.EmpresaId,
            CompanyKey = companyKey,
            DebeCambiarContrasena = user.DebeCambiarContrasena,
            RefreshToken = refreshToken
        };
    }

    public async Task<LoginResponse> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new AuthenticationException("Sesión expirada.");

        var hash = Hash(refreshToken);
        var identity = await _refresh.ValidateAsync(hash, ct);
        if (identity is null)
            throw new AuthenticationException("Sesión expirada.");

        // Rotación: se revoca el usado y se emite uno nuevo (sliding 60 días).
        await _refresh.RevokeAsync(hash, ct);

        var user = new Usuario
        {
            Id = identity.UserId,
            LegacyUserId = identity.LegacyUserId,
            UsuarioNombre = identity.UsuarioNombre,
            Rol = identity.Rol,
            EmpresaId = identity.EmpresaId
        };
        /*
          EL CANAL SALE DEL RENGLON GUARDADO, NO DEL CLIENTE.

          Si se volviera a preguntar al renovar, el cierre no valdria nada:
          bastaria con pedir el refresh declarando "web" para salirse del canal
          movil. El cuerpo de /api/auth/refresh solo trae el refreshToken.

          Un renglon sin canal (los escritos antes de este cambio: el ALTER los
          deja en NULL) se renueva como MOVIL, no como web. Esta tabla la
          escribe SOLO la app -el web nunca manda incluirRefresh-, asi que
          "sin canal" aqui significa "app vieja", y leerlo como web le quitaria
          sus claves `app_movil.*` a cada repartidor con la app ya instalada.
        */
        var canal = identity.CanalSesion == Canal.NoDeclarado ? Canal.Movil : identity.CanalSesion;

        // El refresh nuevo se guarda YA con el canal resuelto, para que el
        // renglon deje de ser "sin canal" en cuanto la app renueve una vez.
        var nuevoRefresh = await IssueRefreshAsync(identity with { CanalSesion = canal }, ct);

        var token = _jwt.GenerateToken(user, identity.CompanyKey, canal);

        return new LoginResponse
        {
            Token = token,
            UserId = identity.UserId,
            IdUsuario = identity.LegacyUserId,
            Usuario = identity.UsuarioNombre,
            Rol = identity.Rol,
            EmpresaId = identity.EmpresaId,
            CompanyKey = identity.CompanyKey,
            DebeCambiarContrasena = false,
            RefreshToken = nuevoRefresh
        };
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;
        await _refresh.RevokeAsync(Hash(refreshToken), ct);
    }

    private async Task<string> IssueRefreshAsync(RefreshTokenIdentity identity, CancellationToken ct)
    {
        var token = GenerateRefreshToken();
        var expira = DateTime.UtcNow.Add(RefreshLifetime);
        await _refresh.CreateAsync(identity, Hash(token), expira, ct);
        return token;
    }

    // 256 bits aleatorios en Base64Url (sin padding) -> el token que ve el cliente.
    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    // Solo se guarda el hash del token, no el token.
    private static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
}
