using ISL_Service.Domain.Entities;

namespace ISL_Service.Application.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateToken(Usuario user, string companyKey);
}
