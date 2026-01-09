using ISL_Service.Application.Interfaces;
using ISL_Service.Domain.Entities;
using ISL_Service.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ISL_Service.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db) => _db = db;

    public Task<List<User>> GetAllAsync() =>
        _db.Users.AsNoTracking().ToListAsync();

    public Task<User?> FindByEmailAsync(string email) =>
        _db.Users.FirstOrDefaultAsync(u => u.Email == email);

    public async Task AddAsync(User user) =>
        await _db.Users.AddAsync(user);

    public Task SaveChangesAsync() =>
        _db.SaveChangesAsync();
}