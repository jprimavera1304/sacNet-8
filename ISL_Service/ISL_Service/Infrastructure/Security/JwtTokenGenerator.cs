using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ISL_Service.Application.Interfaces;
using ISL_Service.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace ISL_Service.Infrastructure.Security;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _config;
    public JwtTokenGenerator(IConfiguration config) => _config = config;

    public string GenerateToken(Usuario user, string companyKey)
    {
        var key = _config["Jwt:Key"]!;
        var issuer = _config["Jwt:Issuer"]!;
        var audience = _config["Jwt:Audience"]!;

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var normalizedCompanyKey = (companyKey ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedCompanyKey))
            throw new InvalidOperationException("Configuracion invalida: companyKey vacia al emitir JWT.");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("username", user.UsuarioNombre),
            new(ClaimTypes.Role, user.Rol),
            new("empresaId", user.EmpresaId.ToString()),
            new("companyKey", normalizedCompanyKey)
        };

        var token = new JwtSecurityToken(issuer, audience, claims,
            expires: DateTime.UtcNow.AddHours(4),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
