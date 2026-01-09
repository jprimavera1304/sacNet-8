namespace ISL_Service.Application.Interfaces;

public interface IJwtTokenGenerator
{
    string Create(Guid userId, string email, string role);
}