using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using ISL_Service.Application.DTOs.Requests;
using ISL_Service.Application.DTOs.Responses;
using ISL_Service.Application.Interfaces;
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

        var token = _jwt.GenerateToken(user, companyKey);

        // Solo la app pide refresh; el web no lo manda -> se comporta como hoy.
        string? refreshToken = null;
        if (request.IncluirRefresh)
        {
            var identity = new RefreshTokenIdentity(user.Id, login.LegacyUserId, user.UsuarioNombre, user.Rol, user.EmpresaId, companyKey);
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
        var nuevoRefresh = await IssueRefreshAsync(identity, ct);

        var user = new Usuario
        {
            Id = identity.UserId,
            LegacyUserId = identity.LegacyUserId,
            UsuarioNombre = identity.UsuarioNombre,
            Rol = identity.Rol,
            EmpresaId = identity.EmpresaId
        };
        var token = _jwt.GenerateToken(user, identity.CompanyKey);

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
