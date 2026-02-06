using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;

using ISL_Service.Infrastructure.Data;

using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Services;
using ISL_Service.Infrastructure.Repositories;

// Login/JWT + Middleware (AGREGADO)
using ISL_Service.Infrastructure.Security;
using ISL_Service.Infrastructure.Middleware;

using Microsoft.AspNetCore.Routing;

using ISL_Service.Application.DTOs.Requests;


var builder = WebApplication.CreateBuilder(args);

// -------------------- Services --------------------

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
});

// DbContext (recomendado: solo esta forma, scoped por request)
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Local")));

// Repositories / Services (YA TENIAS)
builder.Services.AddScoped<RecaudacionRepository>();
builder.Services.AddScoped<RecaudacionService>();

builder.Services.AddScoped<ProveedoresPagosRepository>();
builder.Services.AddScoped<ProveedoresPagosService>();

builder.Services.AddScoped<IPersonasRepository, PersonasRepository>();
builder.Services.AddScoped<IPersonasService, PersonasService>();

// -------------------- LOGIN (AGREGADO) --------------------
// Asegúrate de haber creado estos archivos/clases:
// - IUserRepository + UserRepository
// - IUserService + UserService
// - IJwtTokenGenerator + JwtTokenGenerator
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

// JWT Authentication (AGREGADO)
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"];
var jwtIssuer = jwtSection["Issuer"];
var jwtAudience = jwtSection["Audience"];

if (string.IsNullOrWhiteSpace(jwtKey) ||
    string.IsNullOrWhiteSpace(jwtIssuer) ||
    string.IsNullOrWhiteSpace(jwtAudience))
{
    // Si falta algo, el proyecto va a truonar al arrancar
    throw new InvalidOperationException("Faltan configuraciones Jwt:Key / Jwt:Issuer / Jwt:Audience en appsettings.json.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,

            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,

            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),

            // Para que tu MeController pueda leer sub como Id
            NameClaimType = "sub",

            // tolerancia pequeña
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

// CORS (DEV: abierto) (YA TENIAS)
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
        policy
            .WithOrigins(
                "http://127.0.0.1:5501",
                "http://localhost:5501"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
    );
});

var app = builder.Build();

// -------------------- Pipeline --------------------

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("DevCors");


// Middleware de errores (AGREGADO)
// Lo dejo además de tu UseExceptionHandler, sin quitar nada.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Auth (AGREGADO)
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
