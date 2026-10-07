using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;

using ISL_Service.Infrastructure.Data;

using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Services;
using ISL_Service.Infrastructure.Repositories;
using ISL_Service.Infrastructure.Services;
using ISL_Service.Infrastructure.Reports;

// Login/JWT + Middleware
using ISL_Service.Infrastructure.Security;
using ISL_Service.Infrastructure.Middleware;
using Microsoft.OpenApi.Models;
using Microsoft.Data.SqlClient;
using ISL_Service.Application.Security;

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
        Version = "v1",
        Description = "API ISL. Tarimas: IdStatus 1=Activo, 2=Cancelado. Almacén de Cascos: movimientos (salidas/entradas), catálogos repartidores/tarimas/tipos-casco. Errores: 409 duplicado o conflicto de negocio."
    });
    var xmlPath = Path.Combine(AppContext.BaseDirectory, "ISL_Service.xml");
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);

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
        "No hay cadena de conexión válida. Configura ConnectionStrings:Main, Mac3, Local o Default."
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
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddSingleton<ICurrentUserAccessor, CurrentUserAccessor>();

// -------------------- USER ADMIN (NUEVO) --------------------
builder.Services.AddScoped<IUserAdminService, UserAdminService>();
builder.Services.AddScoped<IUsuarioModuloFavoritoRepository, UsuarioModuloFavoritoRepository>();
builder.Services.AddScoped<IUsuarioModuloFavoritoService, UsuarioModuloFavoritoService>();

builder.Services.AddScoped<IEmpresaRepository, EmpresaRepository>();
builder.Services.AddScoped<IEmpresaService, EmpresaService>();

// -------------------- TARIMAS + CATALOGOS --------------------
builder.Services.AddScoped<ITarimaRepository, TarimaRepository>();
builder.Services.AddScoped<ITarimaService, TarimaService>();
builder.Services.AddScoped<ICatalogosRepository, CatalogosRepository>();
// Los 28 catalogos de solo lectura que venian de MacServicios2 (ver CatalogosGeneralesController).
builder.Services.AddScoped<ICatalogosGeneralesRepository, CatalogosGeneralesRepository>();
builder.Services.AddScoped<ICatalogosGeneralesService, CatalogosGeneralesService>();


// -------------------- ESCRITURAS DE CATALOGOS DE MAC31 (ola 6) --------------------
builder.Services.AddScoped<ICatalogosAdminRepository, CatalogosAdminRepository>();
builder.Services.AddScoped<ICatalogosAdminService, CatalogosAdminService>();

// -------------------- FICHAS MAESTRAS DE MAC31: CLIENTE, PRODUCTO, PROVEEDOR --------------------
builder.Services.AddScoped<IMaestrosRepository, MaestrosRepository>();
builder.Services.AddScoped<IMaestrosService, MaestrosService>();

// -------------------- OPERACION DIARIA: COMPRAS, CORTES, TRANSFERENCIAS, VIGENCIAS --------------------
builder.Services.AddScoped<IOperacionMacRepository, OperacionMacRepository>();
builder.Services.AddScoped<IOperacionMacService, OperacionMacService>();

// -------------------- USUARIOS DE MAC31, HUELLAS, CONSTANTES Y PARAMETROS --------------------
builder.Services.AddScoped<ISeguridadMacRepository, SeguridadMacRepository>();
builder.Services.AddScoped<ISeguridadMacService, SeguridadMacService>();

// -------------------- BATCHES DE NOMINA (calcular recibos, generar periodos, descontar prestamos) --------------------
builder.Services.AddScoped<INominaBatchRepository, NominaBatchRepository>();
builder.Services.AddScoped<INominaBatchService, NominaBatchService>();

// -------------------- SUELTOS: PROYECTOS, RECORDATORIOS, SERVICIOS, SINCRONIZACION --------------------
builder.Services.AddScoped<ISueltosRepository, SueltosRepository>();
builder.Services.AddScoped<ISueltosService, SueltosService>();
builder.Services.AddScoped<ICategoriasRepository, CategoriasRepository>();
builder.Services.AddScoped<ICategoriasService, CategoriasService>();
builder.Services.AddScoped<IProfesoresRepository, ProfesoresRepository>();
builder.Services.AddScoped<IProfesoresService, ProfesoresService>();
builder.Services.AddScoped<IEquiposRepository, EquiposRepository>();
builder.Services.AddScoped<IEquiposService, EquiposService>();
builder.Services.AddScoped<IInscripcionesTorneoRepository, InscripcionesTorneoRepository>();
builder.Services.AddScoped<IInscripcionesTorneoService, InscripcionesTorneoService>();
builder.Services.AddScoped<IJornadasRepository, JornadasRepository>();
builder.Services.AddScoped<IJornadasService, JornadasService>();
builder.Services.AddScoped<IGeneracionRolTorneoRepository, GeneracionRolTorneoRepository>();
builder.Services.AddScoped<IGeneracionRolTorneoService, GeneracionRolTorneoService>();
builder.Services.AddScoped<IConfiguracionRolTorneoRepository, ConfiguracionRolTorneoRepository>();
builder.Services.AddScoped<IConfiguracionRolTorneoService, ConfiguracionRolTorneoService>();
builder.Services.AddScoped<IChequesRepository, ChequesRepository>();
builder.Services.AddScoped<IChequesService, ChequesService>();
builder.Services.AddScoped<ITemporadasRepository, TemporadasRepository>();
builder.Services.AddScoped<ITemporadasService, TemporadasService>();

/* -------------------- OBRA (MC) Y AVIACION --------------------
   Los dos modulos de MacServicios2 que faltaban: obra publica (contratos,
   convenios, estimaciones, partidas) y aviacion (recaudaciones, permisionarios,
   economicos, polizas), mas cobranzas y gasolinas.

   La conexion de aviacion se registra aparte porque NO es la de la empresa:
   sale de dbo.Constantes.AviacionDBServer. El porque esta en
   Infrastructure/Data/ConexionDeAviacion.cs. */
builder.Services.AddScoped<ISL_Service.Infrastructure.Data.IConexionDeAviacion, ISL_Service.Infrastructure.Data.ConexionDeAviacion>();

builder.Services.AddScoped<IObraCatalogosEscrituraRepository, ObraCatalogosEscrituraRepository>();
builder.Services.AddScoped<IObraCatalogosEscrituraService, ObraCatalogosEscrituraService>();
builder.Services.AddScoped<IObraContratosRepository, ObraContratosRepository>();
builder.Services.AddScoped<IObraContratosService, ObraContratosService>();

builder.Services.AddScoped<IAviacionCatalogosRepository, AviacionCatalogosRepository>();
builder.Services.AddScoped<IAviacionCatalogosService, AviacionCatalogosService>();
builder.Services.AddScoped<IAviacionPolizasRepository, AviacionPolizasRepository>();
builder.Services.AddScoped<IAviacionPolizasService, AviacionPolizasService>();
builder.Services.AddScoped<IAviacionRecaudacionesRepository, AviacionRecaudacionesRepository>();
builder.Services.AddScoped<IAviacionRecaudacionesService, AviacionRecaudacionesService>();

builder.Services.AddScoped<IGasolinasRepository, GasolinasRepository>();
builder.Services.AddScoped<IGasolinasService, GasolinasService>();
builder.Services.AddScoped<ICobranzasRepository, CobranzasRepository>();
builder.Services.AddScoped<ICobranzasService, CobranzasService>();

builder.Services.AddScoped<IDashboardVentasRepository, DashboardVentasRepository>();
builder.Services.AddScoped<IDashboardVentasService, DashboardVentasService>();
builder.Services.AddScoped<IDashboardVentasReportService, DashboardVentasReportService>();
builder.Services.AddScoped<IDashboardVentasReportRenderer, DashboardVentasPdfReportRenderer>();
builder.Services.AddScoped<IDashboardVentasReportRenderer, DashboardVentasXlsxReportRenderer>();
builder.Services.AddHostedService<ReportEngineWarmupHostedService>();
// Siembra en WPermiso las claves que declara el codigo. Sin esto, un modulo
// migrado contesta 403 a todos menos al SuperAdmin. Ver el comentario del
// servicio: la causa no se ve de frente.
builder.Services.AddHostedService<ISL_Service.Infrastructure.Security.CatalogoDePermisosHostedService>();

builder.Services.AddScoped<IReportesVentasRepository, ReportesVentasRepository>();
/* El catalogo de centros de servicio: de el sale a que base apunta un reporte. */
builder.Services.AddScoped<ICentrosServicioRepository, CentrosServicioRepository>();
builder.Services.AddScoped<IReportesVentasService, ReportesVentasService>();

builder.Services.AddScoped<IVentasPedidosRepository, VentasPedidosRepository>();
builder.Services.AddScoped<IVentasPedidosService, VentasPedidosService>();
builder.Services.AddSingleton<IAutorizarPedidosAsyncCoordinator, AutorizarPedidosAsyncCoordinator>();
builder.Services.AddScoped<IVentasConsultaRepository, VentasConsultaRepository>();
builder.Services.AddScoped<IRemisionImpresionRepository, RemisionImpresionRepository>();
builder.Services.AddScoped<IRemisionImpresionService, RemisionImpresionService>();
builder.Services.AddScoped<IVentasConsultaService, VentasConsultaService>();
builder.Services.AddScoped<IVentasCancelacionRepository, VentasCancelacionRepository>();
builder.Services.AddScoped<IVentasCancelacionService, VentasCancelacionService>();
// Devolucion de una remision (la pantalla VentasDevolucion de Mac31).
builder.Services.AddScoped<IVentasDevolucionRepository, VentasDevolucionRepository>();
builder.Services.AddScoped<IVentasDevolucionService, VentasDevolucionService>();
// Consultar pagos de una remision (la pantalla ConsultarVentasPagos de Mac31).
builder.Services.AddScoped<IVentasPagosRepository, VentasPagosRepository>();
builder.Services.AddScoped<IVentasPagosService, VentasPagosService>();
// Multi pago: cobrar varias remisiones de un golpe (btnMultiPago de Mac31).
builder.Services.AddScoped<IVentasMultiPagoRepository, VentasMultiPagoRepository>();
builder.Services.AddScoped<IVentasMultiPagoService, VentasMultiPagoService>();
builder.Services.AddScoped<IVentasSaldosRepository, VentasSaldosRepository>();
builder.Services.AddScoped<IVentasSaldosService, VentasSaldosService>();
builder.Services.AddScoped<IVentasPedidoCapturaRepository, VentasPedidoCapturaRepository>();
builder.Services.AddScoped<IVentasPedidoCapturaService, VentasPedidoCapturaService>();
// Modificacion de usados de una remision (btnUsados de Mac31).
builder.Services.AddScoped<IVentasUsadosRepository, VentasUsadosRepository>();
builder.Services.AddScoped<IVentasUsadosService, VentasUsadosService>();

/*
  CONSULTAS DE VENTAS MIGRADAS DE MacServicios2 (ola 2, solo lectura).

  Tres modulos: la cobranza (cobros, cobranzas y concentrados), los detalles de
  usados y el catalogo de descuentos. No tocan nada: todos sus procedimientos se
  comprobaron contra sys.sql_modules y ninguno escribe en una tabla real.
*/
builder.Services.AddScoped<IVentasCobranzaConsultaRepository, VentasCobranzaConsultaRepository>();
builder.Services.AddScoped<IVentasCobranzaConsultaService, VentasCobranzaConsultaService>();
builder.Services.AddScoped<IVentasUsadosConsultaRepository, VentasUsadosConsultaRepository>();
builder.Services.AddScoped<IVentasUsadosConsultaService, VentasUsadosConsultaService>();
builder.Services.AddScoped<IVentasDescuentosConsultaRepository, VentasDescuentosConsultaRepository>();
builder.Services.AddScoped<IVentasDescuentosConsultaService, VentasDescuentosConsultaService>();

// Ola 4: las escrituras de ventas (cascos usados, cobranza, garantias,
// descuentos, pedidos, pagos, inventario y facturacion). Todas llaman a los
// mismos sp_n_ que Mac31; ninguno se toco.
builder.Services.AddScoped<IVentasUsadosOperacionesRepository, VentasUsadosOperacionesRepository>();
builder.Services.AddScoped<IVentasUsadosOperacionesService, VentasUsadosOperacionesService>();
builder.Services.AddScoped<IVentasCobranzaOperacionesRepository, VentasCobranzaOperacionesRepository>();
builder.Services.AddScoped<IVentasCobranzaOperacionesService, VentasCobranzaOperacionesService>();
builder.Services.AddScoped<IVentasGarantiasRepository, VentasGarantiasRepository>();
builder.Services.AddScoped<IVentasGarantiasService, VentasGarantiasService>();
builder.Services.AddScoped<IInventarioOperacionesRepository, InventarioOperacionesRepository>();
builder.Services.AddScoped<IInventarioOperacionesService, InventarioOperacionesService>();
builder.Services.AddScoped<IVentasPagosOperacionesRepository, VentasPagosOperacionesRepository>();
builder.Services.AddScoped<IVentasPagosOperacionesService, VentasPagosOperacionesService>();
builder.Services.AddScoped<IPedidosOperacionesRepository, PedidosOperacionesRepository>();
builder.Services.AddScoped<IPedidosOperacionesService, PedidosOperacionesService>();
builder.Services.AddScoped<IVentasConsultasLegacyRepository, VentasConsultasLegacyRepository>();
builder.Services.AddScoped<IVentasConsultasLegacyService, VentasConsultasLegacyService>();
builder.Services.AddScoped<IVentasDescuentosRepository, VentasDescuentosRepository>();
builder.Services.AddScoped<IVentasDescuentosService, VentasDescuentosService>();
builder.Services.AddScoped<IVentasFacturacionRepository, VentasFacturacionRepository>();
builder.Services.AddScoped<IVentasFacturacionService, VentasFacturacionService>();

// El cliente de Facturapi. Va por HttpClient con nombre y NO por el SDK de
// legacy (Facturapi.dll es de .NET Framework y no se puede referenciar desde
// net8.0). El timeout es generoso porque timbrar un CFDI pasa por el PAC y por
// el SAT, y NO se reintenta nunca: un reintento emitiria un segundo comprobante
// fiscal real.
builder.Services.AddHttpClient("facturapi", cliente =>
{
    cliente.Timeout = TimeSpan.FromSeconds(120);
});
builder.Services.AddScoped<ISL_Service.Infrastructure.Facturacion.FacturapiCliente>();

// -------------------- ALMACEN CASCOS --------------------
builder.Services.AddScoped<IAlmacenCascosRepository, AlmacenCascosRepository>();
builder.Services.AddScoped<IAlmacenCascosService, AlmacenCascosService>();

// -------------------- CHECADOR (HORA DE COMIDA) --------------------
builder.Services.AddScoped<IChecadorRepository, ChecadorRepository>();
builder.Services.AddScoped<IChecadorService, ChecadorService>();

// -------------------- NOMINA: EMPLEADOS, ASISTENCIAS Y PRESTAMOS --------------------
// Los tres van juntos porque comparten origen: son las pantallas de nomina de
// Mac31 (empleados, asistencias por periodo y prestamos) llevadas al web.
// Todo lo que escriben lo escribe legacy con sus propios sp_n_.
builder.Services.AddScoped<IEmpleadosRepository, EmpleadosRepository>();
builder.Services.AddScoped<IEmpleadosService, EmpleadosService>();
builder.Services.AddScoped<IAsistenciasRepository, AsistenciasRepository>();
builder.Services.AddScoped<IAsistenciasService, AsistenciasService>();
builder.Services.AddScoped<IPrestamosRepository, PrestamosRepository>();
builder.Services.AddScoped<IPrestamosService, PrestamosService>();

// -------------------- CASCOS A CAMBIO --------------------
// La cuenta de cascos y dinero con la otra empresa (hoy un Excel a mano).
// Escribe solo en tablas propias (WCascoCambio*) con sp_w_; de legacy nada mas
// lee [Catalogo TiposUsados], que es de donde salen los precios.
builder.Services.AddScoped<ICascosCambioRepository, CascosCambioRepository>();
builder.Services.AddScoped<ICascosCambioService, CascosCambioService>();

// Version minima/recomendada de la app movil (endpoint anonimo api/app/version).
builder.Services.AddScoped<IAppVersionRepository, AppVersionRepository>();

// -------------------- OBRA (las tablas MC_* de Mac31) --------------------
// Contratos de obra publica, sus grupos, conceptos y estimaciones, y los
// catalogos que los alimentan. SOLO LECTURA: los sp_n_Insertar_MC* y
// sp_n_Actualizar_MC* existen en la base y a proposito no se exponen.
builder.Services.AddScoped<IObraRepository, ObraRepository>();
builder.Services.AddScoped<IObraService, ObraService>();

// -------------------- CATALOGOS COMPLEMENTARIOS --------------------
// Los catalogos de lectura que no entraron en la primera ola (tipos de
// domicilio, condiciones de credito, series de empresa, los tres del SAT y
// marcas). Comparten el prefijo api/catalogos y los permisos catalogos.*.
builder.Services.AddScoped<ICatalogosComplementosRepository, CatalogosComplementosRepository>();
builder.Services.AddScoped<ICatalogosComplementosService, CatalogosComplementosService>();

// -------------------- ANEXOS DE CLIENTE Y DE PROVEEDOR --------------------
// Las pestanas de la ficha: bonificaciones por tipo de usado, centros de
// servicio permitidos, datos de facturacion y bitacora de cambios del cliente;
// apoderados, domicilios y documentos del proveedor. Todo lectura.
builder.Services.AddScoped<IClientesAnexosRepository, ClientesAnexosRepository>();
builder.Services.AddScoped<IClientesAnexosService, ClientesAnexosService>();
builder.Services.AddScoped<IProveedoresAnexosRepository, ProveedoresAnexosRepository>();
builder.Services.AddScoped<IProveedoresAnexosService, ProveedoresAnexosService>();

// -------------------- PRODUCTOS (catalogo con precios y existencias) --------------------
// No es un desplegable: trae precio de lista, stock y la lista de descuentos
// Roshfrans. Por eso tiene su propio permiso y no cae en catalogos.*.
builder.Services.AddScoped<IProductosRepository, ProductosRepository>();
builder.Services.AddScoped<IProductosService, ProductosService>();

// -------------------- HUELLAS Y ANEXOS DE USUARIO --------------------
// Huellas registradas (usuario / empleado / cliente) y las tres lecturas
// sueltas de la pantalla de usuarios de Mac31.
builder.Services.AddScoped<IHuellasRepository, HuellasRepository>();
builder.Services.AddScoped<IHuellasService, HuellasService>();
builder.Services.AddScoped<IUsuariosAnexosRepository, UsuariosAnexosRepository>();
builder.Services.AddScoped<IUsuariosAnexosService, UsuariosAnexosService>();

// -------------------- CORTES DE CAJA --------------------
// Las cuatro lecturas de corte que NO pasan por sp_n_ConsultaConstantes y por
// tanto son lecturas de verdad (ver CortesRepository).
builder.Services.AddScoped<ICortesRepository, CortesRepository>();
builder.Services.AddScoped<ICortesService, CortesService>();

// Manifiesto de actualizacion de las aplicaciones de escritorio (no es la
// version de la app movil: eso es IAppVersionRepository, mas arriba).
builder.Services.AddScoped<IActualizacionesAplicacionRepository, ActualizacionesAplicacionRepository>();
builder.Services.AddScoped<IActualizacionesAplicacionService, ActualizacionesAplicacionService>();

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
// AllowedOrigins=https://mactauro.com,https://www.mactauro.com,https://zaragozamac.com,https://www.zaragozamac.com,https://sacmac.net,https://www.sacmac.net,https://integralsportsleague.net,https://www.integralsportsleague.net
var allowedOriginsRaw = builder.Configuration["AllowedOrigins"];
var configuredOrigins = (allowedOriginsRaw ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

var localFallbackOrigins = new[]
{
    "http://127.0.0.1:5500",
    "http://localhost:5500",
    "http://127.0.0.1:5501",
    "http://localhost:5501",
    "http://localhost:5173"
};

// Prefijos permitidos para entornos preview de Azure Static Web Apps.
// Formato esperado del host:
//   {prefix}{id}.centralus.4.azurestaticapps.net
// Ejemplo:
//   green-beach-06e94f810-41.centralus.4.azurestaticapps.net
var previewPrefixesRaw = builder.Configuration["AllowedAzurePreviewPrefixes"];
var configuredPreviewPrefixes = (previewPrefixesRaw ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(x => x.ToLowerInvariant())
    .ToArray();

var defaultPreviewPrefixes = new[]
{
    "brave-pond-06a013a10-",
    "nice-pond-0f5acc210-",
    "green-beach-06e94f810-",
    "green-ground-038056f10-"
};

var allowedPreviewPrefixes = configuredPreviewPrefixes
    .Concat(defaultPreviewPrefixes)
    .Where(x => !string.IsNullOrWhiteSpace(x))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

var allowedOrigins = configuredOrigins
    .Where(x => !string.IsNullOrWhiteSpace(x))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

if (builder.Environment.IsDevelopment())
{
    allowedOrigins = allowedOrigins
        .Concat(localFallbackOrigins)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

/*
  EN DESARROLLO SE ACEPTA CUALQUIER PUERTO DE ESTA MISMA MAQUINA.

  La lista de arriba nombra puertos uno por uno —5500, 5501, 5173— y cada vez
  que el front se muda de puerto hay que acordarse de venir aqui. No nos
  acordamos: el front nuevo paso a servirse en 5180, y con el se mudaron las
  paginas del front ANTERIOR, que no pasan por el proxy de Vite sino que llaman
  directas a localhost:5041. Todas sus llamadas empezaron a morir por CORS.

  Y morian de la peor forma: /api/me fallaba, la pagina se quedaba sin permisos
  y devolvia al inicio de golpe. Ni un error a la vista — parecia que el modulo
  estuviera prohibido.

  La comprobacion se queda dentro de IsDevelopment(): en produccion mandan los
  origenes configurados y nada mas. Ahi Uri.IsLoopback no da nunca verdadero
  para un dominio publico.
*/
static bool EsDeEstaMaquina(string? origin)
{
    if (string.IsNullOrWhiteSpace(origin)) return false;
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
    if (!string.IsNullOrEmpty(uri.UserInfo)) return false;
    if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;
    return uri.IsLoopback;
}

var enDesarrollo = builder.Environment.IsDevelopment();

var allowedOriginsSet = new HashSet<string>(allowedOrigins, StringComparer.OrdinalIgnoreCase);

static bool IsAllowedAzurePreviewOrigin(string? origin, string[] allowedPrefixes)
{
    if (string.IsNullOrWhiteSpace(origin)) return false;
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
    if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return false;
    if (uri.IsDefaultPort == false && uri.Port != 443) return false;
    if (!string.IsNullOrEmpty(uri.UserInfo)) return false;

    var host = uri.Host.ToLowerInvariant();
    if (!host.EndsWith(".azurestaticapps.net", StringComparison.OrdinalIgnoreCase)) return false;

    return allowedPrefixes.Any(prefix =>
        host.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
        policy
            .SetIsOriginAllowed(origin =>
                allowedOriginsSet.Contains(origin) ||
                (enDesarrollo && EsDeEstaMaquina(origin)) ||
                IsAllowedAzurePreviewOrigin(origin, allowedPreviewPrefixes))
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

/*
  EL TOKEN TIENE QUE SER DE ESTA EMPRESA.

  Va entre UseAuthentication y UseAuthorization a proposito: necesita el
  ClaimsPrincipal ya armado (asi que despues de autenticar) y tiene que cortar
  ANTES de que cualquier politica de permisos lea el rol del token, porque el rol
  viaja dentro de el. Lo demas lo explica CompanyKeyMiddleware.
*/
app.UseMiddleware<CompanyKeyMiddleware>();

app.UseAuthorization();

/*
  LOS DOS ENDPOINTS DE DIAGNOSTICO VAN CON CANDADO.

  QUE PASABA
  ----------
  /whoami y /dbcheck estaban anonimos, y no decian poca cosa:

    GET /dbcheck -> {"connected":true,"connection":"Main",
                     "server":"LAP-JUAND","database":"Produccion_svr"}
    GET /whoami  -> {"companyId":"local","environment":"Production", ...}

  Nombre del servidor SQL y nombre de la base, de regalo, a quien pase por la
  URL. En produccion eso es reconocimiento de infraestructura: el primer paso
  de cualquiera que luego pruebe a conectarse.

  QUE SE DECIDIO Y POR QUE
  ------------------------
  NO se dejan "solo en Development". Para lo que sirven —comprobar que una Web
  App de Azure quedo apuntando a la empresa y a la base correctas despues de un
  despliegue— hay que poder llamarlos EN produccion; si solo existen en local no
  comprueban nada de lo que se despliega. Asi que siguen existiendo en los dos
  entornos, pero PIDEN TOKEN: sin cabecera Authorization contestan 401.

  Basta con estar autenticado, sin permiso fino: no revelan datos de nadie, y
  quien tiene que comprobar un despliegue no siempre es administrador del
  sistema de permisos.

  El candado es RequireAuthorization() sobre el endpoint, que corre dentro del
  mismo UseAuthorization() ya registrado arriba.
*/

// AGREGADO: endpoint simple para validar config por empresa
app.MapGet("/whoami", (IConfiguration config, IWebHostEnvironment env) =>
{
    // CompanyId lo configuras en Azure como variable de aplicación por cada Web App
    var companyId = config["CompanyId"] ?? "missing";

    return Results.Ok(new
    {
        companyId,
        environment = env.EnvironmentName,
        corsAllowedOrigins = config["AllowedOrigins"] ?? "",
        db = effectiveCsName
    });
}).RequireAuthorization();

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
}).RequireAuthorization();


app.MapControllers();

app.Run();


