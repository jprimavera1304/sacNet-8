using System.Security.Claims;
using ISL_Service.Application.DTOs.Requests;
using ISL_Service.Application.DTOs.Responses;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Models;
using ISL_Service.Application.Services;
using ISL_Service.Controllers;
using ISL_Service.Domain.Entities;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Infrastructure.Repositories;
using ISL_Service.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ISL_Service.Tests;

public class CompanyKeyTests
{
    [Fact]
    public async Task LoginAsync_IncluyeCompanyKeyEnRespuesta()
    {
        var userRepo = new FakeUserRepository
        {
            LoginResult = new WebLoginFallbackResult
            {
                ResultCode = 1,
                Source = "WEB",
                UserId = Guid.NewGuid(),
                Usuario = "JUAN",
                ContrasenaHash = BCrypt.Net.BCrypt.HashPassword("AB123456"),
                Rol = "User",
                EmpresaId = 1,
                DebeCambiarContrasena = false,
                Estado = 1
            }
        };

        var empresaRepo = new FakeEmpresaRepository("tauro");
        var jwt = new CapturingJwtTokenGenerator();
        var service = new UserService(userRepo, jwt, empresaRepo);

        var response = await service.LoginAsync(new LoginRequest
        {
            Usuario = "JUAN",
            Contrasena = "AB123456"
        }, CancellationToken.None);

        Assert.Equal("tauro", response.CompanyKey);
        Assert.Equal("tauro", jwt.LastCompanyKey);
        Assert.False(string.IsNullOrWhiteSpace(response.Token));
    }

    [Fact]
    public void JwtTokenGenerator_IncluyeClaimCompanyKey()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "A9F7Q3Z6Vw2R8eKJcUT4NBydH5sYpM3aXWmL",
            ["Jwt:Issuer"] = "isl-service",
            ["Jwt:Audience"] = "isl-frontend"
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var generator = new JwtTokenGenerator(config);
        var token = generator.GenerateToken(new Usuario
        {
            Id = Guid.NewGuid(),
            UsuarioNombre = "JUAN",
            Rol = "User",
            EmpresaId = 1
        }, "tauro");

        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token);
        var companyKeyClaim = jwt.Claims.FirstOrDefault(c => c.Type == "companyKey")?.Value;

        Assert.Equal("tauro", companyKeyClaim);
    }

    [Fact]
    public async Task MeController_IncluyeCompanyKeyEnRespuesta()
    {
        var userId = Guid.NewGuid();
        var users = new FakeUserRepository
        {
            UserById = new Usuario
            {
                Id = userId,
                UsuarioNombre = "JUAN",
                Rol = "User",
                EmpresaId = 1,
                DebeCambiarContrasena = false
            }
        };

        var controller = new MeController(users, new FakeUserAdminService(), new FakeEmpresaRepository("tauro"));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim("sub", userId.ToString())
                }, "test"))
            }
        };

        var result = await controller.GetMe(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<MeResponse>(ok.Value);

        Assert.Equal("tauro", payload.CompanyKey);
        Assert.Equal(1, payload.EmpresaId);
    }

    [Fact]
    public async Task EmpresaRepository_LanzaError_SiClaveFalta()
    {
        await using var conn = new SqliteConnection("DataSource=:memory:");
        await conn.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(conn)
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.EmpresasWeb.Add(new EmpresaWeb
        {
            Id = 1,
            Clave = "   ",
            Nombre = "Tauro",
            Estado = 1
        });
        await db.SaveChangesAsync();

        var repo = new EmpresaRepository(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.GetCompanyKeyAsync(CancellationToken.None));
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public WebLoginFallbackResult? LoginResult { get; set; }
        public Usuario? UserById { get; set; }

        public Task<Usuario?> GetByUsuarioAsync(string usuario, CancellationToken ct) => Task.FromResult<Usuario?>(null);
        public Task<Usuario?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(UserById);
        public Task<WebLoginFallbackResult?> LoginWithFallbackAsync(string usuario, string contrasenaPlano, string contrasenaHashWeb, CancellationToken ct) => Task.FromResult(LoginResult);
        public Task<bool> ExistsByUsuarioAsync(string usuario, CancellationToken ct) => Task.FromResult(false);
        public Task<List<Usuario>> ListAsync(int? empresaId, CancellationToken ct) => Task.FromResult(new List<Usuario>());
        public Task AddAsync(Usuario user, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(Usuario user, CancellationToken ct) => Task.CompletedTask;
        public Task<Usuario> UpsertWebAndLegacyAsync(string usuario, string contrasenaPlano, string contrasenaHashWeb, string nombre, string rol, bool debeCambiarContrasena, int estado, CancellationToken ct) => Task.FromResult(new Usuario());
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeEmpresaRepository : IEmpresaRepository
    {
        private readonly string _companyKey;

        public FakeEmpresaRepository(string companyKey)
        {
            _companyKey = companyKey;
        }

        public Task<List<int>> ListEmpresaIdsAsync(CancellationToken ct) => Task.FromResult(new List<int> { 1 });
        public Task<string> GetCompanyKeyAsync(CancellationToken ct) => Task.FromResult(_companyKey);
    }

    private sealed class CapturingJwtTokenGenerator : IJwtTokenGenerator
    {
        public string LastCompanyKey { get; private set; } = string.Empty;

        public string GenerateToken(Usuario user, string companyKey)
        {
            LastCompanyKey = companyKey;
            return "fake-token";
        }
    }

    private sealed class FakeUserAdminService : IUserAdminService
    {
        public Task<CreateUserResponse> CreateUserAsync(CreateUserRequest req, ClaimsPrincipal actor, CancellationToken ct) => throw new NotImplementedException();
        public Task<List<UserResponse>> ListUsersAsync(int? empresaId, ClaimsPrincipal actor, CancellationToken ct) => throw new NotImplementedException();
        public Task<UserResponse> GetUserByIdAsync(Guid userId, ClaimsPrincipal actor, CancellationToken ct) => throw new NotImplementedException();
        public Task<UserResponse> UpdateEstadoAsync(Guid userId, UpdateUserEstadoRequest req, ClaimsPrincipal actor, CancellationToken ct) => throw new NotImplementedException();
        public Task<ResetPasswordResponse> ResetPasswordAsync(Guid userId, ClaimsPrincipal actor, CancellationToken ct) => throw new NotImplementedException();
        public Task<UserResponse> UpdateEmpresaAsync(Guid userId, UpdateUserEmpresaRequest req, ClaimsPrincipal actor, CancellationToken ct) => throw new NotImplementedException();
        public Task ChangeMyPasswordAsync(ChangePasswordRequest req, ClaimsPrincipal actor, CancellationToken ct) => throw new NotImplementedException();
    }
}
