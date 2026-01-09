using Microsoft.EntityFrameworkCore;
using ISL_Service.Domain.Entities;

namespace ISL_Service.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Email único
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // Reglas básicas de columnas (opcional, pero recomendado)
        modelBuilder.Entity<User>()
            .Property(u => u.Email)
            .HasMaxLength(320)
            .IsRequired();

        modelBuilder.Entity<User>()
            .Property(u => u.PasswordHash)
            .HasMaxLength(500)
            .IsRequired();

        modelBuilder.Entity<User>()
            .Property(u => u.Role)
            .HasMaxLength(50)
            .IsRequired();

        base.OnModelCreating(modelBuilder);
    }
}