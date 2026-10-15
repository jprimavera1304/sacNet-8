using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using ISL_Service.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace ISL_Service.Infrastructure.Security;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _config;
    public JwtTokenGenerator(IConfiguration config) => _config = config;

    public string GenerateToken(Usuario user, string companyKey, Canal canal = Canal.Web)
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
            new("rolLegacy", user.Rol),
            new("empresaId", user.EmpresaId.ToString()),
            new("companyKey", normalizedCompanyKey)
        };
        if (user.LegacyUserId > 0)
        {
            claims.Add(new Claim("idUsuario", user.LegacyUserId.ToString()));
        }

        /*
          EL CANAL, FIRMADO.

          Aqui estaba el hueco: el token no decia desde que canal se emitio
          (llevaba sub, username, role, rolLegacy, empresaId, companyKey e
          idUsuario y nada mas), asi que el servidor no tenia forma NO
          FALSIFICABLE de saber si una peticion venia del telefono o del
          navegador. Sin eso, las dos claves del canal movil
          -`app_movil.acceso` y `app_movil.pedidos.editar`- solo las podia
          aplicar Flutter, y Flutter no es un control de acceso: con un token
          valido se saltaban llamando a la API a mano.

          Va DENTRO del token a proposito. Una cabecera o un campo del cuerpo
          los reescribe el atacante en cada peticion; este claim viaja dentro
          de la firma HMAC-SHA256 y re-firmarlo exige `Jwt:Key`, que solo tiene
          el servidor.

          NoDeclarado no escribe claim: es el estado de los tokens de antes de
          este cambio, y se trata como "comportate como ayer" (ver Canal).
        */
        var claimCanal = CanalDeAcceso.AClaim(canal);
        if (claimCanal is not null)
        {
            claims.Add(new Claim(CanalDeAcceso.Claim, claimCanal));
        }

        var token = new JwtSecurityToken(issuer, audience, claims,
            expires: DateTime.UtcNow.AddHours(4),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
