using System.Security.Authentication;
using ISL_Service.Application.DTOs.Requests;
using ISL_Service.Application.DTOs.Responses;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _repo;
    private readonly IJwtTokenGenerator _jwt;

    public UserService(IUserRepository repo, IJwtTokenGenerator jwt)
    {
        _repo = repo;
        _jwt = jwt;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await _repo.GetByUsuarioAsync(request.Usuario.Trim(), ct);
        if (user is null) throw new AuthenticationException("Credenciales inválidas.");
        if (user.Estado != 1) throw new AuthenticationException("Usuario inactivo.");

        if (!BCrypt.Net.BCrypt.Verify(request.Contrasena, user.ContrasenaHash))
            throw new AuthenticationException("Credenciales inválidas.");

        var token = _jwt.GenerateToken(user);

        return new LoginResponse
        {
            Token = token,
            UserId = user.Id,
            Usuario = user.UsuarioNombre,
            Rol = user.Rol,
            EmpresaId = user.EmpresaId,
            DebeCambiarContrasena = user.DebeCambiarContrasena
        };
    }
}
