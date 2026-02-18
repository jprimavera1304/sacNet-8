using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;

using ISL_Service.Infrastructure.Data;

using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Services;
using ISL_Service.Infrastructure.Repositories;

// Login/JWT + Middleware
using ISL_Service.Infrastructure.Security;
using ISL_Service.Infrastructure.Middleware;
using Microsoft.OpenApi.Models;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

// -------------------- Services --------------------

// Controllers + Swagger (lo dejo porque dijiste "sin borrar")
//Prueba desde vs code para ver en vs studio
//holakkldklsadjdaskklj
//puedes borrar esto si quieres, no afecta nada, es solo para probar el despliegue desde vs code
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddMemoryCache();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ISL_Service",
        Version = "v1"
    });

    // Definicion de seguridad JWT
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Escribe: Bearer {tu token JWT}"
    });

    // Requerir JWT en endpoints protegidos
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});


// -------------------- DB CONNECTION (AGREGADO) --------------------
// Compatibilidad total:
// - Nuevo: Main
// - Legacy: Mac3 / Local / Default
var candidateConnections = new (string Name, string? Value)[]
{
    ("Main", builder.Configuration.GetConnectionString("Main")),
    ("Mac3", builder.Configuration.GetConnectionString("Mac3")),
    ("Local", builder.Configuration.GetConnectionString("Local")),
    ("Default", builder.Configuration.GetConnectionString("Default"))
};

var selectedConnection = candidateConnections.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Value));
var effectiveCs = selectedConnection.Value;
var effectiveCsName = selectedConnection.Name;

if (string.IsNullOrWhiteSpace(effectiveCs))
{
    throw new InvalidOperationException(
        "No hay cadena de conexion valida. Configura ConnectionStrings:Main, Mac3, Local o Default."
    );
}

// DbContext (ANTES: Local fijo; AHORA: Main si existe, si no Local)
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(effectiveCs));


// Repositories / Services (YA TENIAS)
builder.Services.AddScoped<RecaudacionRepository>();
builder.Services.AddScoped<RecaudacionService>();

builder.Services.AddScoped<ProveedoresPagosRepository>();
builder.Services.AddScoped<ProveedoresPagosService>();

builder.Services.AddScoped<IPersonasRepository, PersonasRepository>();
builder.Services.AddScoped<IPersonasService, PersonasService>();

// -------------------- LOGIN (YA TENIAS) --------------------
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

// -------------------- USER ADMIN (NUEVO) --------------------
builder.Services.AddScoped<IUserAdminService, UserAdminService>();

builder.Services.AddScoped<IEmpresaRepository, EmpresaRepository>();
builder.Services.AddScoped<IEmpresaService, EmpresaService>();


// -------------------- JWT Authentication --------------------
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"];
var jwtIssuer = jwtSection["Issuer"];
var jwtAudience = jwtSection["Audience"];

if (string.IsNullOrWhiteSpace(jwtKey) ||
    string.IsNullOrWhiteSpace(jwtIssuer) ||
    string.IsNullOrWhiteSpace(jwtAudience))
{
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

            // Si tu token mete "rol" en vez de ClaimTypes.Role, descomenta esta linea:
            // RoleClaimType = "rol",

            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();


// -------------------- CORS --------------------
// AllowedOrigins desde configuracion (Azure Variables de entorno)
// Ejemplo Azure:
// AllowedOrigins=https://mactauro.com,https://www.mactauro.com,https://sacmac.net,https://www.sacmac.net,https://integralsportsleague.net,https://www.integralsportsleague.net
var allowedOriginsRaw = builder.Configuration["AllowedOrigins"];
var configuredOrigins = (allowedOriginsRaw ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

var defaultOrigins = new[]
{
    "http://127.0.0.1:5501",
    "http://localhost:5501",
    "http://localhost:5173",
    "https://mactauro.com",
    "https://www.mactauro.com",
    "https://sacmac.net",
    "https://www.sacmac.net",
    "https://integralsportsleague.net",
    "https://www.integralsportsleague.net"
};

var allowedOrigins = configuredOrigins
    .Concat(defaultOrigins)
    .Where(x => !string.IsNullOrWhiteSpace(x))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
        policy
            .WithOrigins(allowedOrigins)
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

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// CORS
app.UseCors("DevCors");

// Middleware de errores
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Auth
app.UseAuthentication();
app.UseAuthorization();

// AGREGADO: endpoint simple para validar config por empresa
app.MapGet("/whoami", (IConfiguration config, IWebHostEnvironment env) =>
{
    // CompanyId lo configuras en Azure como variable de aplicacion por cada Web App
    var companyId = config["CompanyId"] ?? "missing";

    return Results.Ok(new
    {
        companyId,
        environment = env.EnvironmentName,
        corsAllowedOrigins = config["AllowedOrigins"] ?? "",
        db = effectiveCsName
    });
});

app.MapGet("/dbcheck", async () =>
{
    try
    {
        using var conn = new SqlConnection(effectiveCs);
        await conn.OpenAsync();

        return Results.Ok(new
        {
            connected = true,
            connection = effectiveCsName,
            server = conn.DataSource,
            database = conn.Database
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});


app.MapControllers();

app.Run();
