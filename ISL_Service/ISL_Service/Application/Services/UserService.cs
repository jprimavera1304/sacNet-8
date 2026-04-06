using System.Security.Authentication;
using ISL_Service.Application.DTOs.Requests;
using ISL_Service.Application.DTOs.Responses;
using ISL_Service.Application.Interfaces;
using ISL_Service.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;

namespace ISL_Service.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _repo;
    private readonly IJwtTokenGenerator _jwt;
    private readonly IEmpresaRepository _empresas;
    private readonly IMemoryCache _cache;

    public UserService(IUserRepository repo, IJwtTokenGenerator jwt, IEmpresaRepository empresas, IMemoryCache cache)
    {
        _repo = repo;
        _jwt = jwt;
        _empresas = empresas;
        _cache = cache;
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

        return new LoginResponse
        {
            Token = token,
            UserId = user.Id,
            IdUsuario = login.LegacyUserId,
            Usuario = user.UsuarioNombre,
            Rol = user.Rol,
            EmpresaId = user.EmpresaId,
            CompanyKey = companyKey,
            DebeCambiarContrasena = user.DebeCambiarContrasena
        };
    }
}
