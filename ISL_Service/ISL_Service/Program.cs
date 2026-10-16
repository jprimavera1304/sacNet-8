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

/*
  EL SUELO DE HILOS, QUE NO ES EL ARREGLO PERO SI LA RED DE SEGURIDAD.

  El ThreadPool de .NET arranca con un hilo de trabajo por nucleo. El servidor
  de produccion tiene CUATRO, y cuando esos cuatro se ocupan el pool inyecta
  aproximadamente UNO POR SEGUNDO. Para una aplicacion que pasa el tiempo
  esperando a una base que esta a 56 ms de distancia, cuatro es un suelo muy
  bajo: basta una rafaga de agentes para que el quinto espere un segundo antes
  de empezar, el sexto dos, y el noveno cinco.

  Esto NO sustituye al arreglo de verdad, que fue dejar de bloquear hilos
  (LlenadoAsincrono) y dejar de repetir el mismo trabajo (UnaSolaVez). Subir el
  minimo sin aquello solo habria escondido el sintoma. Se pone porque el coste
  es nulo -son hilos que solo existen si hacen falta- y porque evita que un pico
  que no hayamos previsto se vuelva otra vez medio minuto de espera.

  El maximo no se toca: ese si seria tapar un problema en vez de verlo.
*/
{
    ThreadPool.GetMinThreads(out _, out var minimoDeIo);
    ThreadPool.SetMinThreads(Math.Max(Environment.ProcessorCount * 8, 32), minimoDeIo);
}

/*
  SI EL PROCESO SE MUERE, QUE QUEDE ESCRITO.

  No habia ningun enganche global, y eso significa que una excepcion no
  controlada se llevaba el proceso SIN DEJAR RASTRO: stdout esta apagado en el
  web.config, la carpeta de logs no existe, y lo que viva en memoria se va con
  el proceso. Averiguar que mataba a w3wp costo descartar hipotesis una por una
  en vez de leer un stack trace.

  Se escribe en el Visor de eventos de Windows porque es el unico sitio que
  demostro sobrevivir al reinicio. Con la fuente ".NET Runtime", que ya existe:
  registrar una propia exige consola elevada la primera vez -el pool corre como
  ApplicationPoolIdentity- y seria un arreglo que falla justo el dia que hace
  falta.

  Todo va envuelto en try/catch vacio a proposito: en este punto el host puede
  estar ya medio desmontado, y un fallo al ANOTAR el fallo no debe cambiar como
  muere el proceso.
*/
{
    static void AnotarEnElVisor(string titulo, Exception? ex)
    {
        try
        {
            // El Visor de eventos solo existe en Windows, y aqui siempre es
            // Windows (IIS); la comprobacion esta para que compile limpio y
            // para que en un contenedor no se caiga al intentar anotar.
            if (!OperatingSystem.IsWindows())
                return;

            System.Diagnostics.EventLog.WriteEntry(
                ".NET Runtime",
                $"ISL_Service: {titulo}{Environment.NewLine}" +
                $"PID {Environment.ProcessId}{Environment.NewLine}{Environment.NewLine}{ex}",
                System.Diagnostics.EventLogEntryType.Error);
        }
        catch
        {
            // Si ni esto se puede, no hay nada mas que hacer.
        }
    }

    AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        AnotarEnElVisor("EXCEPCION NO CONTROLADA (el proceso se esta muriendo)", e.ExceptionObject as Exception);

    TaskScheduler.UnobservedTaskException += (_, e) =>
    {
        AnotarEnElVisor("TAREA DE FONDO SIN OBSERVAR", e.Exception);
        e.SetObserved(); // Que no escale: ya quedo anotado.
    };
}

var builder = WebApplication.CreateBuilder(args);

/*
  EL APAGADO TIENE QUE CABER.

  Por defecto el host da 30 s al trabajo en vuelo, y el modulo de ASP.NET Core
  le concede 70 (shutdownTimeLimit, en el web.config). El host debe rendirse
  ANTES que el modulo: si se pasa, IIS lo mata a la fuerza y eso es exactamente
  el "exit code 0xffffffff" que llenaba el visor de eventos en cada despliegue.

  60 deja diez segundos de holgura entre los dos.
*/
builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(60));

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

/*
  EL GUARDIA DE ESCALADA DE PERMISOS.

  Scoped y no Singleton porque depende de IPermissionService, que es Scoped
  (lleva el AppDbContext de la peticion dentro). Registrarlo como Singleton
  haria que capturara un DbContext ya cerrado y el guardia reventaria — y un
  guardia que revienta es un guardia que, con el catch equivocado, deja pasar.
*/
builder.Services.AddScoped<IGuardiaDePermisos, GuardiaDePermisos>();

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

// -------------------- CAPACIDADES DE LA EMPRESA (lo que el front necesita para no implementar solo una) --------------------
builder.Services.AddScoped<ICapacidadesEmpresaRepository, CapacidadesEmpresaRepository>();
builder.Services.AddScoped<ICapacidadesEmpresaService, CapacidadesEmpresaService>();

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

/*
  LA CAPA DE CENTROS. UN solo sitio resuelve a que base se le pregunta.

  Alcance de PETICION (AddScoped) porque el centro es un dato de la peticion, no
  del proceso: dos usuarios en dos sucursales distintas estan dentro del mismo
  w3wp al mismo tiempo. Ver Infrastructure/Centros/ConexionDelCentro.cs.

  IHttpContextAccessor hace falta para leer la cabecera X-Centro desde fuera de
  un controlador: la capa la usan los REPOSITORIOS, que no heredan de
  ControllerBase. No estaba registrado porque hasta ahora nadie lo necesitaba.
*/
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IIdentidadDeEmpresaRepository, IdentidadDeEmpresaRepository>();
builder.Services.AddScoped<IConexionDelCentro, ISL_Service.Infrastructure.Centros.ConexionDelCentro>();

builder.Services.AddScoped<IVentasPedidosRepository, VentasPedidosRepository>();
builder.Services.AddScoped<IVentasPedidosService, VentasPedidosService>();
builder.Services.AddSingleton<IAutorizarPedidosAsyncCoordinator, AutorizarPedidosAsyncCoordinator>();
/* LA MISMA INSTANCIA como servicio del host, para que StopAsync espere a las
   operaciones que ella misma lanzo. Registrar el tipo otra vez crearia una
   segunda instancia, que no sabria nada de las tareas en vuelo de la primera. */
builder.Services.AddHostedService(sp =>
    (AutorizarPedidosAsyncCoordinator)sp.GetRequiredService<IAutorizarPedidosAsyncCoordinator>());
builder.Services.AddScoped<IVentasConsultaRepository, VentasConsultaRepository>();
builder.Services.AddScoped<IRemisionImpresionRepository, RemisionImpresionRepository>();
builder.Services.AddScoped<IRemisionImpresionService, RemisionImpresionService>();
builder.Services.AddScoped<ISimulacionImpresionService, SimulacionImpresionService>();
builder.Services.AddScoped<ISimulacionDatosService, SimulacionDatosService>();
builder.Services.AddScoped<ICascosAFacturarRepository, CascosAFacturarRepository>();
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
// La LECTURA de la pantalla de reasignar cartera. La escritura sigue siendo
// VentasPagosOperaciones: aqui no se escribe nada.
builder.Services.AddScoped<IVentasReasignacionRepository, VentasReasignacionRepository>();
builder.Services.AddScoped<IVentasReasignacionService, VentasReasignacionService>();
builder.Services.AddScoped<IPedidosOperacionesRepository, PedidosOperacionesRepository>();
builder.Services.AddScoped<IPedidosOperacionesService, PedidosOperacionesService>();
builder.Services.AddScoped<IVentasConsultasLegacyRepository, VentasConsultasLegacyRepository>();
builder.Services.AddScoped<IVentasConsultasLegacyService, VentasConsultasLegacyService>();
builder.Services.AddScoped<IVentasDescuentosRepository, VentasDescuentosRepository>();
builder.Services.AddScoped<IVentasDescuentosService, VentasDescuentosService>();
builder.Services.AddScoped<IVentasFacturacionRepository, VentasFacturacionRepository>();
builder.Services.AddScoped<IVentasFacturacionService, VentasFacturacionService>();

/*
  EL DOCUMENTO DEL CFDI: EL PASO QUE FALTABA DEL REPORTE 1003.

  El 1003 de Legacy no dibuja nada —su rama no arma una sola linea de HTML—:
  es un DESCARGADOR CON CACHE EN DISCO. Lee donde va el archivo, lo baja de
  Facturapi si no esta, y lo escribe creando la carpeta del mes.

  Sin ese paso, un comprobante timbrado desde el web o el telefono nunca
  dejaba archivo, y `POST /facturas/enviar` —que adjunta DESDE DISCO, igual
  que legacy— contestaba 409 para siempre. En Mac31 funciona porque la
  pantalla dispara el 1003 antes de mandar el correo.

  Van aqui y no en otro sitio porque `VentasFacturacionController` las pide por
  el constructor: sin registrarlas, el controlador no se puede instanciar y
  TODOS sus endpoints contestan 500, no solo los del documento.
*/
builder.Services.AddScoped<IFacturaDocumentoAlmacen, FacturaDocumentoAlmacen>();
builder.Services.AddScoped<IFacturaDocumentoService, FacturaDocumentoService>();

// ARMAR EL COMPROBANTE ANTES DE TIMBRARLO. Va aparte del servicio que timbra a
// proposito: este solo LEE y cuenta, no habla con Facturapi y no escribe. En una
// familia donde el error vale un CFDI real, que la pantalla de revision no
// dependa de la clase que puede emitir vale mas que una clase menos.
builder.Services.AddScoped<IVentasFacturacionPreparacionService, VentasFacturacionPreparacionService>();

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

// -------------------- CONSULTA DE CLIENTES Y DE PROVEEDORES --------------------
// Los dos GET raiz que faltaban: api/clientes y api/proveedores. Son las
// pantallas ConsultarClientes y de proveedores de Mac31, con sus sp_n_ de
// siempre.
//
// El de CLIENTES acota el resultado al agente del usuario (FiltroDeAgente), y si
// no puede resolverlo NO contesta: 409. El repositorio toma IMemoryCache del
// contenedor para eso, igual que CatalogosRepository.
builder.Services.AddScoped<IClientesConsultaRepository, ClientesConsultaRepository>();
builder.Services.AddScoped<IClientesConsultaService, ClientesConsultaService>();
builder.Services.AddScoped<IProveedoresConsultaRepository, ProveedoresConsultaRepository>();
builder.Services.AddScoped<IProveedoresConsultaService, ProveedoresConsultaService>();

// -------------------- CONSULTA DE INVENTARIO EN TRANSITO --------------------
// La lectura que le faltaba al POST api/inventario/transito: lo que va en camino
// a los centros de servicio y nadie ha aceptado.
builder.Services.AddScoped<IInventarioTransitoConsultaRepository, InventarioTransitoConsultaRepository>();
builder.Services.AddScoped<IInventarioTransitoConsultaService, InventarioTransitoConsultaService>();

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

/*
  LA LLAVE MAESTRA DE LA APP MOVIL, DEL LADO DEL SERVIDOR.

  Mismo hueco en el pipeline y por las mismas dos razones que el de arriba:
  necesita el ClaimsPrincipal ya armado (va DESPUES de UseAuthentication, para
  poder leer el claim `canal`) y tiene que cortar ANTES de que cualquier
  endpoint corra (va ANTES de UseAuthorization).

  Y va DESPUES de CompanyKeyMiddleware a proposito: primero se comprueba que el
  token sea de ESTA empresa y luego lo que puede hacer. Al reves se resolverian
  permisos contra la base equivocada para un token ajeno.

  Lo que hace y que deja pasar lo explica AccesoMovilMiddleware.
*/
app.UseMiddleware<AccesoMovilMiddleware>();

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


