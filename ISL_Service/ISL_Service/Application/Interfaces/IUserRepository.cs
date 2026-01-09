using ISL_Service.Domain.Entities;

namespace ISL_Service.Application.Interfaces;

public interface IUserRepository
{
    Task<List<User>> GetAllAsync();
    Task<User?> FindByEmailAsync(string email);
    Task AddAsync(User user);
    Task SaveChangesAsync();
}