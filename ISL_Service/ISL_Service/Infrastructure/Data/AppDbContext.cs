using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Data;
using ISL_Service.Domain.Entities;

namespace ISL_Service.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public readonly string _connectionString;

    public AppDbContext(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IDbConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        _connectionString = Database.GetConnectionString() ?? string.Empty;
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("Usuarios", "dbo");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.UsuarioNombre).HasColumnName("Usuario").IsRequired();
            entity.Property(x => x.ContrasenaHash).HasColumnName("ContrasenaHash").IsRequired();
            entity.Property(x => x.Rol).HasColumnName("Rol").IsRequired();
            entity.Property(x => x.EmpresaId).HasColumnName("EmpresaId").IsRequired();
            entity.Property(x => x.DebeCambiarContrasena).HasColumnName("DebeCambiarContrasena").IsRequired();
            entity.Property(x => x.Estado).HasColumnName("Estado").IsRequired();
            entity.Property(x => x.FechaCreacion).HasColumnName("FechaCreacion");
            entity.Property(x => x.FechaActualizacion).HasColumnName("FechaActualizacion");
        });
    }
}
