using System.Data;
using ISL_Service.Application.DTOs.Responses;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Models;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace ISL_Service.Infrastructure.Security;

public sealed class PermissionService : IPermissionService
{
    private const string FeatureKey = "autorizacion.capacidades";
    private const string SchemaCacheKey = "permissions:schema:v2";
    private const string OverrideTypeCacheKey = "permissions:override-type:v1";
    private const string UserPermColumnsCacheKey = "permissions:userperm-columns:v1";
    private const string ReportesModuleKey = "reportes";
    private const string ReportesViewPermission = "reportes.ver_modulo";
    private const string VentasModuleKey = "ventas";
    private const string VentasViewPermission = "ventas.ver_modulo";
    private const string VentasLegacyForm = "CONSULTA DE VENTAS";
    private static readonly Dictionary<string, (string Key, string Name)> VentasLegacyPermissionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnBuscar"] = ("ventas.consultar", "Ventas - Consultar"),
        ["btnNuevoPedido"] = ("ventas.pedidos.crear", "Ventas - Crear pedido"),
        ["btnAceites"] = ("ventas.aceites", "Ventas - Aceites"),
        ["btnAjustePedido"] = ("ventas.garantias", "Ventas - Garantias"),
        ["btnProcesarPedido"] = ("ventas.pedidos.procesar", "Ventas - Procesar pedido"),
        ["btnModificarPedido"] = ("ventas.pedidos.modificar", "Ventas - Modificar pedido"),
        ["btnCancelarPedido"] = ("ventas.pedidos.cancelar", "Ventas - Cancelar pedido"),
        ["btnPantallaPedido"] = ("ventas.pedidos.pantalla", "Ventas - Pantalla pedido"),
        ["btnAutorizarPedido"] = ("ventas.pedidos.autorizar", "Ventas - Autorizar pedido"),
        ["btnRechazarPedido"] = ("ventas.pedidos.rechazar", "Ventas - Rechazar pedido"),
        ["btnCancelarVenta"] = ("ventas.cancelar", "Ventas - Cancelar"),
        ["btnDevolucion"] = ("ventas.devolucion", "Ventas - Devolucion"),
        ["btnUsados"] = ("ventas.usados", "Ventas - Usados"),
        ["btnPagos"] = ("ventas.pagos.ver", "Ventas - Pagos"),
        ["btnMultiPago"] = ("ventas.pagos.multi", "Ventas - Multi pago"),
        ["btnDescargar"] = ("ventas.descargar", "Ventas - Descargar"),
        ["btnPantalla"] = ("ventas.pantalla", "Ventas - Pantalla"),
        ["btnReimprimir"] = ("ventas.reimprimir", "Ventas - Reimprimir"),
        ["btnImprimir"] = ("ventas.imprimir", "Ventas - Imprimir"),
        ["btnSimular"] = ("ventas.simular.con_remision", "Ventas - Simular con remision"),
        ["btnSimularSin"] = ("ventas.simular.sin_remision", "Ventas - Simular sin remision"),
        ["btnFacturar"] = ("ventas.facturar", "Ventas - Facturar"),
        ["btnPedidosFaltantes"] = ("ventas.pedidos.faltantes", "Ventas - Pedidos faltantes")
    };
    // Permisos de ventas que manda el WEB, no legacy.
    //
    // Todo lo que empiece con "ventas." y no este en esta lista se BORRA del
    // snapshot y se vuelve a calcular desde los botones de Mac31 (ver mas
    // abajo, donde se limpian Allow/Deny). O sea: si un permiso de ventas no
    // aparece aqui, darlo desde la pantalla de permisos web NO sirve de nada
    // — se asigna en la base y al siguiente calculo desaparece, sin error y
    // sin rastro. Es exactamente lo que pasaba con "pendientes de autorizar".
    private static readonly List<PermissionSeed> VentasWebPermissionSeeds = new()
    {
        new(VentasViewPermission, "Ventas - Ver modulo", VentasModuleKey),
        new("ventas.ver", "Ventas - Ver", VentasModuleKey),
        new("ventas.cliente.ver", "Ventas - Ver cliente", VentasModuleKey),
        // Pendientes de autorizar: ver la pantalla y soltar el pedido son
        // permisos distintos a proposito, para poder dar uno sin el otro.
        new(PendientesViewPermission, "Ventas - Ver pendientes de autorizar", VentasModuleKey),
        new(PendientesAuthorizePermission, "Ventas - Autorizar pendientes", VentasModuleKey)
    };
    public const string PendientesViewPermission = "ventas.pendientes.ver";
    public const string PendientesAuthorizePermission = "ventas.pendientes.autorizar";

    private const string EmpleadosModuleKey = "empleados";
    private const string EmpleadosViewPermission = "empleados.ver_modulo";

    // La forma de Mac31 se busca por su DESCRIPCION, no por su nombre, porque
    // asi lo hace ya el puente de ventas y porque es lo que ensena la pantalla
    // de permisos de legacy. Verificado igual en las dos empresas: IDForma 2,
    // Forma "ConsultaEmpleados", Descripcion "CONSULTAR EMPLEADOS", tanto en
    // Produccion_svr (Tauro) como en MacZ (Zaragoza).
    private const string EmpleadosLegacyForm = "CONSULTAR EMPLEADOS";

    // Los cuatro botones que Mac31 tiene dados de alta en n_Procesos para la
    // forma ConsultaEmpleados (IDProceso 1012, 1013, 1014 y 5136; los mismos
    // numeros en las dos empresas). Ese es el universo completo: BUSCAR,
    // LIMPIAR, EXCEL, CERRAR y la casilla VER SUELDOS NO estan en n_Procesos,
    // asi que no son permisos y no se inventan aqui — legacy las controla de
    // otra forma (ver ConsultaEmpleados.cs, contrasena rotatoria).
    private static readonly Dictionary<string, (string Key, string Name)> EmpleadosLegacyPermissionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnVer"] = ("empleados.ver", "Empleados - Ver"),
        ["btnNuevo"] = ("empleados.crear", "Empleados - Dar de alta"),
        ["btnModificar"] = ("empleados.editar", "Empleados - Modificar"),
        ["btnHuellas"] = ("empleados.huellas", "Empleados - Huellas")
    };

    // Permisos de empleados que manda el WEB, no legacy. Misma trampa que en
    // ventas: lo que empiece con "empleados." y no este aqui se borra del
    // snapshot y se recalcula desde los botones de Mac31.
    //
    // "empleados.sueldo.ver" TIENE que vivir aqui. En Mac31 ver los sueldos no
    // es un permiso: es la casilla VER SUELDOS, que pide una contrasena que se
    // regenera cada minuto (ConfirmarContrasena.cs), mas una lista de
    // IDUsuario escritos a mano en el codigo para Zaragoza
    // (ConsultaEmpleados.cs, SetColumnas). Nada de eso existe como fila en
    // n_Procesos, asi que si se dejara heredar se borraria en cada calculo y
    // NADIE podria ver un sueldo desde el web, nunca.
    //
    // "empleados.ver_modulo" tambien, y no por gusto: el RemoveAll es por
    // prefijo y se lo llevaria. Ventas lo resolvio con esta misma lista blanca;
    // reportes lo resolvio con el parche de PermissionAuthorizationHandler, que
    // NO aplica a ModulosController — o sea, sin esta linea el modulo
    // desapareceria del menu de inicio aunque sus endpoints contestaran 200.
    private static readonly List<PermissionSeed> EmpleadosWebPermissionSeeds = new()
    {
        new(EmpleadosViewPermission, "Empleados - Ver modulo", EmpleadosModuleKey),
        new("empleados.sueldo.ver", "Empleados - Ver sueldo y bonos", EmpleadosModuleKey)
    };

    // Un modulo del web cuyos permisos VIVEN en una forma de Mac31. Se saco
    // como parametro para que empleados no fuera una tercera copia del mismo
    // SQL: el puente de ventas y el de empleados son identicos salvo estos
    // cinco datos. Reportes sigue aparte porque su clave se DEDUCE del texto
    // del proceso, en vez de estar mapeada a mano.
    private sealed record LegacyModuleBinding(
        string ModuleKey,
        string ModuleName,
        string LegacyForm,
        IReadOnlyDictionary<string, (string Key, string Name)> Map,
        IReadOnlyList<PermissionSeed> WebSeeds);

    private static readonly LegacyModuleBinding VentasBinding = new(
        VentasModuleKey, "Ventas", VentasLegacyForm, VentasLegacyPermissionMap, VentasWebPermissionSeeds);

    private static readonly LegacyModuleBinding EmpleadosBinding = new(
        EmpleadosModuleKey, "Empleados", EmpleadosLegacyForm, EmpleadosLegacyPermissionMap, EmpleadosWebPermissionSeeds);

    /*
      LOS BOTONES DE LA PANTALLA DE PAGOS VIVEN EN OTRA FORMA DE MAC31.

      "ventas.pagos.ver" (btnPagos) vive en CONSULTA DE VENTAS y ya estaba
      arriba: es el que deja ABRIR la pantalla. Pero los dos botones de ADENTRO
      —Nuevo pago y Cancelar pago— estan dados de alta en otra forma,
      ConsultarVentasPagos, y por eso hace falta un segundo puente.

      Verificado en las DOS empresas y son identicas hasta el numero: IDForma 9,
      Forma "ConsultarVentasPagos", Descripcion "CONSULTA DE VENTAS PAGOS", con
      exactamente DOS procesos, btnNuevo (1032) y btnCancelar (1033), los mismos
      IDProceso en Produccion_svr (Tauro) y en MacZ (Zaragoza). Ese es el
      universo completo: btnCerrar no esta en n_Procesos —en el Designer lleva
      Tag = "1", que en ValidaOperadorProcesos significa "encendido para
      todos"— asi que no es un permiso y no se inventa aqui.

      NO LLEVA WebSeeds NI SU PROPIO ver_modulo. El modulo es el MISMO
      ("ventas"), y su ".ver_modulo" ya lo siembra VentasWebPermissionSeeds;
      volver a declararlo crearia dos duenos de la misma llave.
    */
    private const string VentasPagosLegacyForm = "CONSULTA DE VENTAS PAGOS";

    private static readonly Dictionary<string, (string Key, string Name)> VentasPagosLegacyPermissionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnNuevo"] = ("ventas.pagos.nuevo", "Ventas - Nuevo pago"),
        ["btnCancelar"] = ("ventas.pagos.cancelar", "Ventas - Cancelar pago")
    };

    private static readonly LegacyModuleBinding VentasPagosBinding = new(
        VentasModuleKey, "Ventas", VentasPagosLegacyForm, VentasPagosLegacyPermissionMap, new List<PermissionSeed>());

    private const string PrestamosModuleKey = "prestamos";
    private const string PrestamosViewPermission = "prestamos.ver_modulo";

    // La forma se busca por su DESCRIPCION, igual que ventas y empleados.
    // Verificado en las DOS empresas y son identicas hasta el byte: IDForma 3,
    // Forma "ConsultaPrestamos", Descripcion "CONSULTAR PRESTAMOS" (con E
    // acentuada, 0xC9 en la codificacion de la columna varchar), tanto en
    // Produccion_svr (Tauro) como en MacZ (Zaragoza).
    private const string PrestamosLegacyForm = "CONSULTAR PRÉSTAMOS";

    // Los CUATRO botones que Mac31 tiene dados de alta en n_Procesos para la
    // forma ConsultaPrestamos. Los IDProceso son los MISMOS numeros en las dos
    // empresas (1015, 1016, 2055, 2056), asi que el puente vale igual para
    // Tauro y para Zaragoza sin un solo "if empresa".
    //
    // Ese es el universo completo: btnBuscar, btnLimpiar y btnCerrar NO estan
    // en n_Procesos —en el Designer llevan Tag = "1", que en
    // ValidaOperadorProcesos significa "encendido para todos"— asi que no son
    // permisos y no se inventan aqui.
    private static readonly Dictionary<string, (string Key, string Name)> PrestamosLegacyPermissionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnVer"] = ("prestamos.ver", "Prestamos - Ver"),
        ["btnNuevo"] = ("prestamos.crear", "Prestamos - Registrar"),
        ["btnModificar"] = ("prestamos.editar", "Prestamos - Cambiar descuento semanal"),
        ["btnCancelar"] = ("prestamos.cancelar", "Prestamos - Cancelar")
    };

    // Permisos de prestamos que manda el WEB, no legacy. Misma trampa que en
    // ventas y empleados: el RemoveAll de abajo es POR PREFIJO y se llevaria
    // "prestamos.ver_modulo" —que no existe en Mac31— dejando el modulo fuera
    // del menu de inicio aunque sus endpoints contestaran 200.
    private static readonly List<PermissionSeed> PrestamosWebPermissionSeeds = new()
    {
        new(PrestamosViewPermission, "Prestamos - Ver modulo", PrestamosModuleKey)
    };

    private static readonly LegacyModuleBinding PrestamosBinding = new(
        PrestamosModuleKey, "Prestamos", PrestamosLegacyForm, PrestamosLegacyPermissionMap, PrestamosWebPermissionSeeds);
    /* Estatico A PROPOSITO: PermissionService es Scoped, o sea una instancia
       nueva por peticion. Un candado de instancia no sincronizaria nada, que es
       justo lo que hay que evitar aqui. */
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> _candados = new();

    private static readonly TimeSpan ActiveCacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StaleCacheTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan SchemaCacheTtl = TimeSpan.FromMinutes(15);

    private static readonly Dictionary<string, HashSet<string>> LegacyPolicyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["usuarios.ver_modulo"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["usuarios.ver"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["usuarios.crear"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin" },
        ["usuarios.editar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin" },
        ["usuarios.estado.editar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin" },
        ["usuarios.password.reset"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin" },
        ["usuarios.empresa.editar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin" },
        ["empresas.ver"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["permisosweb.bootstrap"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["permisosweb.roles.editar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin" },
        ["permisosweb.overrides.editar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin" },
        ["permisosweb.catalogo.editar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin" },
        ["proveedores.ver"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin", "User" },
        ["proveedores.crear"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["proveedores.editar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["proveedores.estado.editar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["categorias.ver_modulo"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin", "User" },
        ["categorias.ver"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin", "User" },
        ["categorias.crear"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["categorias.editar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["categorias.activar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["profesores.ver_modulo"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin", "User" },
        ["profesores.ver"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin", "User" },
        ["profesores.crear"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["profesores.editar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["profesores.activar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["equipos.ver_modulo"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin", "User" },
        ["equipos.ver"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin", "User" },
        ["equipos.crear"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["equipos.editar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["equipos.activar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["inscripciones.ver_modulo"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin", "User" },
        ["inscripciones.ver"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin", "User" },
        ["inscripciones.crear"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["inscripciones.editar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["inscripciones.activar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["jornadas.ver_modulo"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin", "User" },
        ["jornadas.ver"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin", "User" },
        ["jornadas.crear"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["jornadas.editar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["jornadas.activar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["generacionroltorneo.ver_modulo"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin", "User" },
        ["generacionroltorneo.ver"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin", "User" },
        ["generacionroltorneo.crear"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["generacionroltorneo.editar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["generacionroltorneo.activar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["cheques.ver_modulo"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin", "User" },
        ["cheques.ver"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin", "User" },
        ["cheques.crear"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["cheques.editar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["cheques.activar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" }
    };

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PermissionService> _logger;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ConcurrentDictionary<string, byte> _observedUserCacheKeys = new(StringComparer.Ordinal);

    public PermissionService(AppDbContext db, IMemoryCache cache, ILogger<PermissionService> logger, IHostEnvironment hostEnvironment)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
        _hostEnvironment = hostEnvironment;
    }

    private static readonly List<PermissionSeed> PermissionCatalogSeeds = new()
    {
        new("usuarios.ver_modulo", "Usuarios - Ver módulo", "usuarios"),
        new("usuarios.ver", "Usuarios - Ver", "usuarios"),
        new("usuarios.crear", "Usuarios - Crear", "usuarios"),
        new("usuarios.editar", "Usuarios - Editar", "usuarios"),
        new("usuarios.estado.editar", "Usuarios - Activar/Inactivar", "usuarios"),
        new("usuarios.password.reset", "Usuarios - Reset Password", "usuarios"),
        new("usuarios.empresa.editar", "Usuarios - Cambiar Empresa", "usuarios"),

        new("empresas.ver", "Empresas - Ver", "empresas"),

        new("permisosweb.bootstrap", "Permisos Web - Ver Administración", "permisosweb"),
        new("permisosweb.roles.editar", "Permisos Web - Editar Roles", "permisosweb"),
        new("permisosweb.overrides.editar", "Permisos Web - Editar Overrides", "permisosweb"),
        new("permisosweb.catalogo.editar", "Permisos Web - Editar Catálogo", "permisosweb"),

        new("proveedores.ver", "Proveedores - Ver", "proveedores"),
        new("proveedores.crear", "Proveedores - Crear", "proveedores"),
        new("proveedores.editar", "Proveedores - Editar", "proveedores"),
        new("proveedores.estado.editar", "Proveedores - Activar/Inactivar", "proveedores"),

        new("categorias.ver_modulo", "Categorías - Ver módulo", "categorias"),
        new("categorias.ver", "Categorías - Ver", "categorias"),
        new("categorias.crear", "Categorías - Crear", "categorias"),
        new("categorias.editar", "Categorías - Editar", "categorias"),
        new("categorias.activar", "Categorías - Activar/Inactivar", "categorias"),

        new("profesores.ver_modulo", "Profesores - Ver módulo", "profesores"),
        new("profesores.ver", "Profesores - Ver", "profesores"),
        new("profesores.crear", "Profesores - Crear", "profesores"),
        new("profesores.editar", "Profesores - Editar", "profesores"),
        new("profesores.activar", "Profesores - Activar/Inactivar", "profesores"),

        new("equipos.ver_modulo", "Equipos - Ver módulo", "equipos"),
        new("equipos.ver", "Equipos - Ver", "equipos"),
        new("equipos.crear", "Equipos - Crear", "equipos"),
        new("equipos.editar", "Equipos - Editar", "equipos"),
        new("equipos.activar", "Equipos - Activar/Inactivar", "equipos"),

        new("inscripciones.ver_modulo", "Inscripciones Torneo - Ver módulo", "inscripciones"),
        new("inscripciones.ver", "Inscripciones Torneo - Ver", "inscripciones"),
        new("inscripciones.crear", "Inscripciones Torneo - Crear", "inscripciones"),
        new("inscripciones.editar", "Inscripciones Torneo - Editar", "inscripciones"),
        new("inscripciones.activar", "Inscripciones Torneo - Activar/Inactivar", "inscripciones"),

        new("jornadas.ver_modulo", "Jornadas - Ver módulo", "jornadas"),
        new("jornadas.ver", "Jornadas - Ver", "jornadas"),
        new("jornadas.crear", "Jornadas - Crear", "jornadas"),
        new("jornadas.editar", "Jornadas - Editar", "jornadas"),
        new("jornadas.activar", "Jornadas - Activar/Inactivar", "jornadas"),

        new("generacionroltorneo.ver_modulo", "Generación Rol Torneo - Ver módulo", "generacionroltorneo"),
        new("generacionroltorneo.ver", "Generación Rol Torneo - Ver", "generacionroltorneo"),
        new("generacionroltorneo.crear", "Generación Rol Torneo - Crear", "generacionroltorneo"),
        new("generacionroltorneo.editar", "Generación Rol Torneo - Editar", "generacionroltorneo"),
        new("generacionroltorneo.activar", "Generación Rol Torneo - Activar/Inactivar", "generacionroltorneo"),

        new("pagosproveedores.ver", "Pagos Proveedores - Ver", "pagosproveedores"),
        new("pagosproveedores.crear", "Pagos Proveedores - Crear", "pagosproveedores"),
        new("pagosproveedores.editar", "Pagos Proveedores - Modificar", "pagosproveedores"),
        new("pagosproveedores.cancelar", "Pagos Proveedores - Cancelar", "pagosproveedores"),
        new("cheques.ver_modulo", "Cheques - Ver módulo", "cheques"),
        new("cheques.ver", "Cheques - Ver", "cheques"),
        new("cheques.crear", "Cheques - Crear", "cheques"),
        new("cheques.editar", "Cheques - Editar", "cheques"),
        new("cheques.activar", "Cheques - Cambiar estatus", "cheques")
    };

    public bool IsAllowedByLegacy(string rolLegacy, string permission)
    {
        if (string.Equals(rolLegacy, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
            return true;

        if (LegacyPolicyMap.TryGetValue(permission, out var allowedRoles))
            return allowedRoles.Contains(rolLegacy ?? string.Empty);

        return true;
    }

    public async Task<PermissionSnapshot> GetPermissionsAsync(Guid userId, int empresaId, string rolLegacy, CancellationToken ct)
    {
        // En local/dev preferimos siempre dato fresco para evitar "permisos no aplicados"
        // por cache en pruebas funcionales de roles y overrides.
        if (_hostEnvironment.IsDevelopment())
        {
            return await BuildSnapshotAsync(userId, empresaId, rolLegacy, ct);
        }

        var cacheKey = BuildCacheKey(userId, empresaId, rolLegacy);
        var staleCacheKey = $"{cacheKey}:stale";
        TrackObservedUserCacheKey(cacheKey);

        if (_cache.TryGetValue<PermissionSnapshot>(cacheKey, out var cached) && cached is not null)
            return cached;

        /*
          UNA SOLA RECONSTRUCCION A LA VEZ, POR USUARIO.

          Al abrir la app salen varias peticiones casi a la vez (/api/me sale
          dos veces, y /api/modulos/disponibles pide lo mismo). Si la cache
          acaba de vencer, TODAS fallan el intento a la vez y TODAS se ponen a
          reconstruir lo mismo. En los registros de produccion se ven tres
          respuestas de 8 segundos en el mismo par de segundos: no eran tres
          usuarios, era uno abriendo la app.

          Con esto, la primera reconstruye y las demas esperan y se llevan el
          mismo resultado. Ademas de ir mas rapido, deja de multiplicarse el
          trabajo contra la base justo en el momento de mas prisa.

          El candado es POR USUARIO: dos personas distintas no se estorban.
        */
        var candado = _candados.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await candado.WaitAsync(ct);
        try
        {
            /* Puede haberla dejado quien iba delante mientras se esperaba. */
            if (_cache.TryGetValue<PermissionSnapshot>(cacheKey, out var reciente) && reciente is not null)
                return reciente;

            var snapshot = await BuildSnapshotAsync(userId, empresaId, rolLegacy, ct);
            _cache.Set(cacheKey, snapshot, ActiveCacheTtl);
            _cache.Set(staleCacheKey, snapshot, StaleCacheTtl);
            return snapshot;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo al calcular permisos para userId={UserId} empresaId={EmpresaId}. Se usará fallback.", userId, empresaId);
            if (_cache.TryGetValue<PermissionSnapshot>(staleCacheKey, out var stale) && stale is not null)
                return stale;

            return BuildLegacyFallback(userId, empresaId, rolLegacy);
        }
        finally
        {
            candado.Release();
        }
    }

    public async Task<PermisosWebBootstrapResponse?> GetPermisosWebBootstrapAsync(int empresaId, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);

        if (!await AreCapabilityTablesAvailableAsync(conn, ct))
            return null;

        var schema = await GetSchemaAsync(conn, ct);
        var reportCatalog = await EnsureLegacyReportPermissionsSyncedAsync(conn, empresaId, ct);
        var ventasCatalog = await EnsureVentasPermissionsSyncedAsync(conn, empresaId, ct);
        var empleadosCatalog = await EnsureLegacyModulePermissionsSyncedAsync(conn, empresaId, EmpleadosBinding, ct);
        var prestamosCatalog = await EnsureLegacyModulePermissionsSyncedAsync(conn, empresaId, PrestamosBinding, ct);

        var response = new PermisosWebBootstrapResponse { PermissionsEnabled = true };
        var activeModules = await GetActiveModulesAsync(conn, empresaId, ct);

        await using (var rolesCmd = new SqlCommand(@"
SELECT Codigo, Nombre
FROM dbo.WRol
WHERE EmpresaId = @EmpresaId
ORDER BY Codigo;", conn))
        {
            rolesCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            await using var reader = await rolesCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                response.Roles.Add(new PermisosWebRoleItem
                {
                    Code = reader.GetString(reader.GetOrdinal("Codigo")),
                    Name = reader.GetString(reader.GetOrdinal("Nombre"))
                });
            }
        }

        await using (var permsCmd = new SqlCommand(@"
SELECT Clave, Nombre
FROM dbo.WPermiso
WHERE EmpresaId = @EmpresaId
ORDER BY Clave;", conn))
        {
            permsCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            await using var reader = await permsCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var code = reader.GetString(reader.GetOrdinal("Clave"));
                if (!IsPermissionInActiveModule(code, activeModules)) continue;
                response.Permissions.Add(EnrichPermissionItem(code, reader.GetString(reader.GetOrdinal("Nombre")), reportCatalog, ventasCatalog, empleadosCatalog, prestamosCatalog));
            }
        }

        var allowedCodes = new HashSet<string>(response.Permissions.Select(p => p.Code), StringComparer.OrdinalIgnoreCase);

        var rolePermsSql = $@"
SELECT r.Codigo AS RoleCode, p.Clave AS Permission
FROM dbo.WRolPermiso rp
INNER JOIN dbo.WRol r
    ON r.EmpresaId = rp.EmpresaId
   AND r.{Q(schema.RoleIdColumn)} = rp.{Q(schema.RolePermRoleIdColumn)}
INNER JOIN dbo.WPermiso p
    ON p.EmpresaId = rp.EmpresaId
   AND p.{Q(schema.PermissionIdColumn)} = rp.{Q(schema.RolePermPermissionIdColumn)}
WHERE rp.EmpresaId = @EmpresaId
ORDER BY r.Codigo, p.Clave;";

        await using (var rolePermsCmd = new SqlCommand(rolePermsSql, conn))
        {
            rolePermsCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            await using var reader = await rolePermsCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var permission = reader.GetString(reader.GetOrdinal("Permission"));
                if (!allowedCodes.Contains(permission)) continue;
                response.RolePermissions.Add(new PermisosWebRolePermissionItem
                {
                    RoleCode = reader.GetString(reader.GetOrdinal("RoleCode")),
                    Permission = permission
                });
            }
        }

        await using (var usersCmd = new SqlCommand(@"
SELECT Id, Usuario, Rol
FROM dbo.UsuarioWeb
WHERE EmpresaId = @EmpresaId
ORDER BY Usuario;", conn))
        {
            usersCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            await using var reader = await usersCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                response.Users.Add(new PermisosWebUserItem
                {
                    UserId = reader.GetGuid(reader.GetOrdinal("Id")),
                    Username = reader.GetString(reader.GetOrdinal("Usuario")),
                    RoleLegacy = reader.GetString(reader.GetOrdinal("Rol"))
                });
            }
        }

        var overridesSql = $@"
SELECT up.{Q(schema.UserPermUserIdColumn)} AS UsuarioWebId, LOWER(up.Tipo) AS Tipo, p.Clave
FROM dbo.WUsuarioPermiso up
INNER JOIN dbo.WPermiso p
    ON p.EmpresaId = up.EmpresaId
   AND p.{Q(schema.PermissionIdColumn)} = up.{Q(schema.UserPermPermissionIdColumn)}
WHERE up.EmpresaId = @EmpresaId;";

        var overrides = new Dictionary<Guid, PermisosWebUserOverrideItem>();
        await using (var overridesCmd = new SqlCommand(overridesSql, conn))
        {
            overridesCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            await using var reader = await overridesCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var rawUserId = reader["UsuarioWebId"];
                if (!TryReadGuid(rawUserId, out var userId))
                    continue;

                var tipo = reader.GetString(reader.GetOrdinal("Tipo"));
                var clave = reader.GetString(reader.GetOrdinal("Clave"));
                if (!allowedCodes.Contains(clave)) continue;

                if (!overrides.TryGetValue(userId, out var row))
                {
                    row = new PermisosWebUserOverrideItem { UserId = userId };
                    overrides[userId] = row;
                }

                if (string.Equals(tipo, "permit", StringComparison.OrdinalIgnoreCase))
                    row.Allow.Add(clave);
                else if (string.Equals(tipo, "allow", StringComparison.OrdinalIgnoreCase))
                    row.Allow.Add(clave);
                else if (string.Equals(tipo, "a", StringComparison.OrdinalIgnoreCase))
                    row.Allow.Add(clave);
                else if (string.Equals(tipo, "deny", StringComparison.OrdinalIgnoreCase))
                    row.Deny.Add(clave);
                else if (string.Equals(tipo, "block", StringComparison.OrdinalIgnoreCase))
                    row.Deny.Add(clave);
                else if (string.Equals(tipo, "d", StringComparison.OrdinalIgnoreCase))
                    row.Deny.Add(clave);
            }
        }

        foreach (var user in response.Users)
        {
            var legacyReports = await LoadLegacyReportPermissionsForUserAsync(conn, empresaId, user.UserId, reportCatalog, ct);
            var legacyVentas = await LoadVentasPermissionsForUserAsync(conn, empresaId, user.UserId, ventasCatalog, ct);
            var legacyEmpleados = await LoadLegacyModulePermissionsForUserAsync(conn, empresaId, user.UserId, EmpleadosBinding, empleadosCatalog, ct);
            var legacyPrestamos = await LoadLegacyModulePermissionsForUserAsync(conn, empresaId, user.UserId, PrestamosBinding, prestamosCatalog, ct);
            if (legacyReports.Allow.Count == 0 && legacyReports.Deny.Count == 0
                && legacyVentas.Allow.Count == 0 && legacyVentas.Deny.Count == 0
                && legacyEmpleados.Allow.Count == 0 && legacyEmpleados.Deny.Count == 0
                && legacyPrestamos.Allow.Count == 0 && legacyPrestamos.Deny.Count == 0)
                continue;

            if (!overrides.TryGetValue(user.UserId, out var row))
            {
                row = new PermisosWebUserOverrideItem { UserId = user.UserId };
                overrides[user.UserId] = row;
            }

            row.Allow.RemoveAll(x => x.StartsWith($"{ReportesModuleKey}.", StringComparison.OrdinalIgnoreCase));
            row.Deny.RemoveAll(x => x.StartsWith($"{ReportesModuleKey}.", StringComparison.OrdinalIgnoreCase));
            row.Allow.AddRange(legacyReports.Allow.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            row.Deny.AddRange(legacyReports.Deny.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            row.Allow.RemoveAll(x => x.StartsWith($"{VentasModuleKey}.", StringComparison.OrdinalIgnoreCase) && !VentasWebPermissionSeeds.Any(seed => seed.Key.Equals(x, StringComparison.OrdinalIgnoreCase)));
            row.Deny.RemoveAll(x => x.StartsWith($"{VentasModuleKey}.", StringComparison.OrdinalIgnoreCase) && !VentasWebPermissionSeeds.Any(seed => seed.Key.Equals(x, StringComparison.OrdinalIgnoreCase)));
            row.Allow.AddRange(legacyVentas.Allow.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            row.Deny.AddRange(legacyVentas.Deny.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            row.Allow.RemoveAll(x => x.StartsWith($"{EmpleadosModuleKey}.", StringComparison.OrdinalIgnoreCase) && !EmpleadosWebPermissionSeeds.Any(seed => seed.Key.Equals(x, StringComparison.OrdinalIgnoreCase)));
            row.Deny.RemoveAll(x => x.StartsWith($"{EmpleadosModuleKey}.", StringComparison.OrdinalIgnoreCase) && !EmpleadosWebPermissionSeeds.Any(seed => seed.Key.Equals(x, StringComparison.OrdinalIgnoreCase)));
            row.Allow.AddRange(legacyEmpleados.Allow.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            row.Deny.AddRange(legacyEmpleados.Deny.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            row.Allow.RemoveAll(x => x.StartsWith($"{PrestamosModuleKey}.", StringComparison.OrdinalIgnoreCase) && !PrestamosWebPermissionSeeds.Any(seed => seed.Key.Equals(x, StringComparison.OrdinalIgnoreCase)));
            row.Deny.RemoveAll(x => x.StartsWith($"{PrestamosModuleKey}.", StringComparison.OrdinalIgnoreCase) && !PrestamosWebPermissionSeeds.Any(seed => seed.Key.Equals(x, StringComparison.OrdinalIgnoreCase)));
            row.Allow.AddRange(legacyPrestamos.Allow.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            row.Deny.AddRange(legacyPrestamos.Deny.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        }

        response.UserOverrides = overrides.Values.OrderBy(x => x.UserId).ToList();
        return response;
    }

    public async Task<PermisosWebRolesBootstrapResponse?> GetPermisosRolesBootstrapAsync(int empresaId, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);

        if (!await AreCapabilityTablesAvailableAsync(conn, ct))
            return null;

        var schema = await GetSchemaAsync(conn, ct);
        var reportCatalog = await EnsureLegacyReportPermissionsSyncedAsync(conn, empresaId, ct);
        var ventasCatalog = await EnsureVentasPermissionsSyncedAsync(conn, empresaId, ct);
        var empleadosCatalog = await EnsureLegacyModulePermissionsSyncedAsync(conn, empresaId, EmpleadosBinding, ct);
        var prestamosCatalog = await EnsureLegacyModulePermissionsSyncedAsync(conn, empresaId, PrestamosBinding, ct);
        var response = new PermisosWebRolesBootstrapResponse { PermissionsEnabled = true };
        var activeModules = await GetActiveModulesAsync(conn, empresaId, ct);

        await using (var rolesCmd = new SqlCommand(@"
SELECT Codigo, Nombre
FROM dbo.WRol
WHERE EmpresaId = @EmpresaId
ORDER BY Codigo;", conn))
        {
            rolesCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            await using var reader = await rolesCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                response.Roles.Add(new PermisosWebRoleItem
                {
                    Code = reader.GetString(reader.GetOrdinal("Codigo")),
                    Name = reader.GetString(reader.GetOrdinal("Nombre"))
                });
            }
        }

        await using (var permsCmd = new SqlCommand(@"
SELECT Clave, Nombre
FROM dbo.WPermiso
WHERE EmpresaId = @EmpresaId
ORDER BY Clave;", conn))
        {
            permsCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            await using var reader = await permsCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var code = reader.GetString(reader.GetOrdinal("Clave"));
                if (!IsPermissionInActiveModule(code, activeModules)) continue;
                response.Permissions.Add(EnrichPermissionItem(code, reader.GetString(reader.GetOrdinal("Nombre")), reportCatalog, ventasCatalog, empleadosCatalog, prestamosCatalog));
            }
        }

        var allowedCodes = new HashSet<string>(response.Permissions.Select(p => p.Code), StringComparer.OrdinalIgnoreCase);

        var rolePermsSql = $@"
SELECT r.Codigo AS RoleCode, p.Clave AS Permission
FROM dbo.WRolPermiso rp
INNER JOIN dbo.WRol r
    ON r.EmpresaId = rp.EmpresaId
   AND r.{Q(schema.RoleIdColumn)} = rp.{Q(schema.RolePermRoleIdColumn)}
INNER JOIN dbo.WPermiso p
    ON p.EmpresaId = rp.EmpresaId
   AND p.{Q(schema.PermissionIdColumn)} = rp.{Q(schema.RolePermPermissionIdColumn)}
WHERE rp.EmpresaId = @EmpresaId
ORDER BY r.Codigo, p.Clave;";

        await using (var rolePermsCmd = new SqlCommand(rolePermsSql, conn))
        {
            rolePermsCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            await using var reader = await rolePermsCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var permission = reader.GetString(reader.GetOrdinal("Permission"));
                if (!allowedCodes.Contains(permission)) continue;
                response.RolePermissions.Add(new PermisosWebRolePermissionItem
                {
                    RoleCode = reader.GetString(reader.GetOrdinal("RoleCode")),
                    Permission = permission
                });
            }
        }

        return response;
    }

    public async Task<IReadOnlyList<PermisosWebPermissionItem>> GetPermissionCatalogAsync(int empresaId, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);

        if (!await AreCapabilityTablesAvailableAsync(conn, ct))
            return Array.Empty<PermisosWebPermissionItem>();

        var reportCatalog = await EnsureLegacyReportPermissionsSyncedAsync(conn, empresaId, ct);
        var ventasCatalog = await EnsureVentasPermissionsSyncedAsync(conn, empresaId, ct);
        var empleadosCatalog = await EnsureLegacyModulePermissionsSyncedAsync(conn, empresaId, EmpleadosBinding, ct);
        var prestamosCatalog = await EnsureLegacyModulePermissionsSyncedAsync(conn, empresaId, PrestamosBinding, ct);
        var response = new List<PermisosWebPermissionItem>();
        await using var permsCmd = new SqlCommand(@"
SELECT Clave, Nombre
FROM dbo.WPermiso
WHERE EmpresaId = @EmpresaId
ORDER BY Clave;", conn);
        permsCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
        await using var reader = await permsCmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var code = reader.GetString(reader.GetOrdinal("Clave"));
            response.Add(EnrichPermissionItem(code, reader.GetString(reader.GetOrdinal("Nombre")), reportCatalog, ventasCatalog, empleadosCatalog, prestamosCatalog));
        }

        return response;
    }

    public async Task<IReadOnlyList<ModuloDisponibleResponse>> GetAvailableModulesAsync(int empresaId, string companyKey, bool includeAllTenants, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);

        if (!await AreCapabilityTablesAvailableAsync(conn, ct))
            return includeAllTenants ? BuildAllModulesFallback(companyKey) : Array.Empty<ModuloDisponibleResponse>();

        var hasWModulo = await TableExistsAsync(conn, "WModulo", ct);
        var hasWModuloEmpresa = await TableExistsAsync(conn, "WModuloEmpresa", ct);
        if (!hasWModulo)
            return includeAllTenants ? BuildAllModulesFallback(companyKey) : Array.Empty<ModuloDisponibleResponse>();
        if (!includeAllTenants && !hasWModuloEmpresa)
            return Array.Empty<ModuloDisponibleResponse>();

        var normalizedCompanyKey = (companyKey ?? string.Empty).Trim().ToLowerInvariant();
        if (!includeAllTenants && string.IsNullOrWhiteSpace(normalizedCompanyKey))
            return Array.Empty<ModuloDisponibleResponse>();

        var modules = new List<ModuloDisponibleResponse>();
        var sql = includeAllTenants
            ? hasWModuloEmpresa
                // "Ver todos" sigue sin filtrar por la empresa del token (ese es su punto:
                // el superadmin ve los modulos de todas las empresas de esta base), pero ya
                // no inventa modulos: solo salen los que alguna empresa tiene dados de alta
                // y activos en WModuloEmpresa. Con el LEFT JOIN anterior aparecia cualquier
                // renglon de WModulo aunque ninguna empresa lo tuviera, y por eso los modulos
                // de ISL (equipos, jornadas, partidos, cedula, registros, roles_juego...) se
                // colaban en Tauro. Asi, dar de baja un modulo de una empresa alcanza para
                // que desaparezca tambien de "Ver todos".
                // El GROUP BY evita el duplicado que producia el join cuando la base tiene
                // mas de una empresa (un renglon por EmpresaClave del mismo modulo).
                ? @"
SELECT
    m.ModuloClave,
    COALESCE(NULLIF(m.Nombre,''), m.ModuloClave) AS Nombre,
    MIN(me.EmpresaClave) AS EmpresaClave
FROM dbo.WModulo m
INNER JOIN dbo.WModuloEmpresa me
    ON me.EmpresaId = m.EmpresaId
   AND me.ModuloClave = m.ModuloClave
   AND me.Activo = 1
WHERE m.EmpresaId = @EmpresaId
GROUP BY m.ModuloClave, COALESCE(NULLIF(m.Nombre,''), m.ModuloClave)
ORDER BY CASE WHEN m.ModuloClave = 'inicio' THEN 0 ELSE 1 END, m.ModuloClave;"
                : @"
SELECT
    m.ModuloClave,
    COALESCE(NULLIF(m.Nombre,''), m.ModuloClave) AS Nombre,
    @EmpresaClave AS EmpresaClave
FROM dbo.WModulo m
WHERE m.EmpresaId = @EmpresaId
ORDER BY CASE WHEN m.ModuloClave = 'inicio' THEN 0 ELSE 1 END, m.ModuloClave;"
            : @"
SELECT
    m.ModuloClave,
    COALESCE(NULLIF(m.Nombre,''), m.ModuloClave) AS Nombre,
    me.EmpresaClave
FROM dbo.WModulo m
INNER JOIN dbo.WModuloEmpresa me
    ON me.EmpresaId = m.EmpresaId
   AND me.ModuloClave = m.ModuloClave
WHERE m.EmpresaId = @EmpresaId
  AND m.IdStatus = 1
  AND me.Activo = 1
  AND LOWER(me.EmpresaClave) = @EmpresaClave
  AND EXISTS (
      SELECT 1
      FROM dbo.WPermiso p
      WHERE p.EmpresaId = m.EmpresaId
        AND LOWER(p.Clave) = LOWER(CONCAT(m.ModuloClave, '.ver_modulo'))
  )
ORDER BY CASE WHEN m.ModuloClave = 'inicio' THEN 0 ELSE 1 END, m.ModuloClave;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
        cmd.Parameters.Add(new SqlParameter("@EmpresaClave", SqlDbType.NVarChar, 200) { Value = normalizedCompanyKey });

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var moduleKey = NormalizeModuleKey(reader.GetString(reader.GetOrdinal("ModuloClave")));
            if (string.IsNullOrWhiteSpace(moduleKey)) continue;
            var empresaClaveOrdinal = reader.GetOrdinal("EmpresaClave");
            var empresaClave = reader.IsDBNull(empresaClaveOrdinal)
                ? normalizedCompanyKey
                : reader.GetString(empresaClaveOrdinal);
            modules.Add(new ModuloDisponibleResponse
            {
                ModuloClave = moduleKey,
                Nombre = reader.GetString(reader.GetOrdinal("Nombre")),
                Ruta = ResolveModuleRoute(moduleKey),
                CapabilityVer = $"{moduleKey}.ver_modulo",
                EmpresaClave = empresaClave
            });
        }

        if (includeAllTenants && modules.Count == 0)
            return BuildAllModulesFallback(companyKey ?? string.Empty);

        return modules;
    }

    private static IReadOnlyList<ModuloDisponibleResponse> BuildAllModulesFallback(string companyKey)
    {
        var empresaClave = (companyKey ?? string.Empty).Trim().ToLowerInvariant();
        // Lista de emergencia (base sin tablas de capacidades). Solo modulos comunes a
        // cualquier empresa: "profesores" salio de aqui porque es de ISL y aparecia en
        // Tauro/Zaragoza sin que esas empresas lo tengan.
        var keys = new[]
        {
            "inicio",
            "cheques",
            "pagosproveedores",
            "permisos_modulos",
            "permisos_roles",
            "permisos_usuarios",
            "proveedores",
            "usuarios"
        };

        return keys
            .Select(k => new ModuloDisponibleResponse
            {
                ModuloClave = k,
                Nombre = k,
                Ruta = ResolveModuleRoute(k),
                CapabilityVer = $"{k}.ver_modulo",
                EmpresaClave = empresaClave
            })
            .ToList();
    }

    public async Task<IReadOnlyList<PermisosWebModuleItem>> GetModuleCatalogAsync(int empresaId, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);

        if (!await AreCapabilityTablesAvailableAsync(conn, ct))
            return Array.Empty<PermisosWebModuleItem>();

        await EnsureLegacyReportPermissionsSyncedAsync(conn, empresaId, ct);
        await EnsureVentasPermissionsSyncedAsync(conn, empresaId, ct);
        await EnsureLegacyModulePermissionsSyncedAsync(conn, empresaId, EmpleadosBinding, ct);
        await EnsureLegacyModulePermissionsSyncedAsync(conn, empresaId, PrestamosBinding, ct);

        if (!await TableExistsAsync(conn, "WModulo", ct))
            return Array.Empty<PermisosWebModuleItem>();

        try
        {
            await using var syncCmd = new SqlCommand("dbo.sp_w_Modulo_SincronizarDesdePermiso", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            syncCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            await syncCmd.ExecuteNonQueryAsync(ct);
        }
        catch (SqlException ex) when (ex.Number == 2812)
        {
            // SP no disponible en algunas bases; se lee estado directo.
        }

        var modules = new List<PermisosWebModuleItem>();
        await using var cmd = new SqlCommand(@"
SELECT ModuloClave, COALESCE(NULLIF(Nombre,''), ModuloClave) AS Nombre, IdStatus
FROM dbo.WModulo
WHERE EmpresaId = @EmpresaId
ORDER BY CASE WHEN ModuloClave = 'inicio' THEN 0 ELSE 1 END, ModuloClave;", conn);
        cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            modules.Add(new PermisosWebModuleItem
            {
                ModuloClave = reader.GetString(reader.GetOrdinal("ModuloClave")),
                Nombre = reader.GetString(reader.GetOrdinal("Nombre")),
                IdStatus = reader.GetInt32(reader.GetOrdinal("IdStatus")) == 2 ? 2 : 1
            });
        }

        return modules;
    }

    public async Task<PermisosWebModuleItem> SetModuleStatusAsync(int empresaId, string moduleKey, int idStatus, CancellationToken ct)
    {
        var key = NormalizeModuleKey(moduleKey);
        if (idStatus is not (1 or 2))
            throw new ArgumentException("IdStatus inválido. Use 1 (Activo) o 2 (Inactivo).");

        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);

        if (!await AreCapabilityTablesAvailableAsync(conn, ct))
            throw new KeyNotFoundException("Capacidades no disponibles para este tenant/base.");
        if (!await TableExistsAsync(conn, "WModulo", ct))
            throw new KeyNotFoundException("Falta dbo.WModulo. Ejecuta 02_Modulos_EmpresaClave_Idempotente.sql.");

        await using var tx = await conn.BeginTransactionAsync(ct);

        await using (var updateCmd = new SqlCommand(@"
UPDATE dbo.WModulo
SET IdStatus = @IdStatus,
    FechaActualizacion = SYSUTCDATETIME()
WHERE EmpresaId = @EmpresaId
  AND ModuloClave = @ModuloClave;", conn, (SqlTransaction)tx))
        {
            updateCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            updateCmd.Parameters.Add(new SqlParameter("@ModuloClave", SqlDbType.NVarChar, 120) { Value = key });
            updateCmd.Parameters.Add(new SqlParameter("@IdStatus", SqlDbType.Int) { Value = idStatus });
            var rows = await updateCmd.ExecuteNonQueryAsync(ct);
            if (rows == 0)
                throw new KeyNotFoundException($"Modulo no encontrado: {key}");
        }

        /*
          APAGAR YA NO BORRA NADA.

          Aqui se borraban todas las filas de WRolPermiso y de WUsuarioPermiso
          del modulo. Era necesario mientras el calculo de permisos no miraba el
          estatus: sin ese borrado, apagar no quitaba el acceso.

          Ahora lo mira (ver LoadEffectivePermissionsAsync), asi que el borrado
          sobra — y era irreversible: apagar un modulo por equivocacion obligaba
          a repartir a mano lo que tenia cada rol y cada persona, sin ninguna
          forma de saber que era.

          Apagar y volver a prender deja las cosas como estaban.
        */

        await using var readCmd = new SqlCommand(@"
SELECT TOP 1 ModuloClave, COALESCE(NULLIF(Nombre,''), ModuloClave) AS Nombre, IdStatus
FROM dbo.WModulo
WHERE EmpresaId = @EmpresaId
  AND ModuloClave = @ModuloClave;", conn, (SqlTransaction)tx);
        readCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
        readCmd.Parameters.Add(new SqlParameter("@ModuloClave", SqlDbType.NVarChar, 120) { Value = key });
        await using var r = await readCmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct))
            throw new KeyNotFoundException($"Modulo no encontrado: {key}");

        var updated = new PermisosWebModuleItem
        {
            ModuloClave = r.GetString(r.GetOrdinal("ModuloClave")),
            Nombre = r.GetString(r.GetOrdinal("Nombre")),
            IdStatus = r.GetInt32(r.GetOrdinal("IdStatus")) == 2 ? 2 : 1
        };
        await r.DisposeAsync();
        await tx.CommitAsync(ct);
        return updated;
    }

    public async Task<PermisosWebRoleItem> CreateRoleAsync(int empresaId, string roleCode, string name, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);

        if (!await AreCapabilityTablesAvailableAsync(conn, ct))
            throw new KeyNotFoundException("Capacidades no disponibles para este tenant/base.");

        var code = NormalizeRoleCodeForCreate(roleCode);
        var displayName = NormalizeRoleNameForCreate(name, code);

        var existsSql = @"
SELECT TOP 1 1
FROM dbo.WRol
WHERE EmpresaId = @EmpresaId
  AND UPPER(Codigo) = UPPER(@Codigo);";
        await using (var existsCmd = new SqlCommand(existsSql, conn))
        {
            existsCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            existsCmd.Parameters.Add(new SqlParameter("@Codigo", SqlDbType.NVarChar, 80) { Value = code });
            var exists = await existsCmd.ExecuteScalarAsync(ct);
            if (exists is not null)
                throw new ArgumentException($"El rol ya existe: {code}");
        }

        var roleColumns = await GetTableColumnsAsync(conn, "WRol", ct);
        var insertColumns = new List<string> { "EmpresaId", "Codigo", "Nombre" };
        var insertValues = new List<string> { "@EmpresaId", "@Codigo", "@Nombre" };

        if (roleColumns.Contains("EsSistema"))
        {
            insertColumns.Add("EsSistema");
            insertValues.Add("0");
        }
        if (roleColumns.Contains("FechaCreacion"))
        {
            insertColumns.Add("FechaCreacion");
            insertValues.Add("SYSUTCDATETIME()");
        }

        var insertSql = $@"
INSERT INTO dbo.WRol ({string.Join(", ", insertColumns.Select(Q))})
VALUES ({string.Join(", ", insertValues)});";
        await using (var insertCmd = new SqlCommand(insertSql, conn))
        {
            insertCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            insertCmd.Parameters.Add(new SqlParameter("@Codigo", SqlDbType.NVarChar, 80) { Value = code });
            insertCmd.Parameters.Add(new SqlParameter("@Nombre", SqlDbType.NVarChar, 120) { Value = displayName });
            await insertCmd.ExecuteNonQueryAsync(ct);
        }

        return new PermisosWebRoleItem
        {
            Code = code,
            Name = displayName
        };
    }

    public async Task UpsertRolePermissionsAsync(int empresaId, string roleCode, IReadOnlyCollection<string> permissions, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);

        if (!await AreCapabilityTablesAvailableAsync(conn, ct))
            throw new KeyNotFoundException("Capacidades no disponibles para este tenant/base.");

        var schema = await GetSchemaAsync(conn, ct);
        var reportCatalog = await EnsureLegacyReportPermissionsSyncedAsync(conn, empresaId, ct);
        var ventasCatalog = await EnsureVentasPermissionsSyncedAsync(conn, empresaId, ct);
        var empleadosCatalog = await EnsureLegacyModulePermissionsSyncedAsync(conn, empresaId, EmpleadosBinding, ct);
        var prestamosCatalog = await EnsureLegacyModulePermissionsSyncedAsync(conn, empresaId, PrestamosBinding, ct);
        var normalizedPermissions = permissions
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        EnsurePermissionsInActiveModules(normalizedPermissions, await GetActiveModulesAsync(conn, empresaId, ct));

        try
        {
            await using var spCmd = new SqlCommand("dbo.sp_w_RolPermisos_ReemplazarPorClaves", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            spCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            spCmd.Parameters.Add(new SqlParameter("@RolCodigo", SqlDbType.NVarChar, 80) { Value = roleCode?.Trim() ?? string.Empty });
            spCmd.Parameters.Add(new SqlParameter("@ClavesCsv", SqlDbType.NVarChar, -1) { Value = string.Join(",", normalizedPermissions) });
            await spCmd.ExecuteNonQueryAsync(ct);
            await SyncLegacyReportPermissionsForRoleAsync(conn, schema, empresaId, roleCode ?? string.Empty, reportCatalog, ct);
            await SyncLegacyModulePermissionsForRoleAsync(conn, schema, empresaId, roleCode ?? string.Empty, ventasCatalog, ct);
            await SyncLegacyModulePermissionsForRoleAsync(conn, schema, empresaId, roleCode ?? string.Empty, empleadosCatalog, ct);
            await SyncLegacyModulePermissionsForRoleAsync(conn, schema, empresaId, roleCode ?? string.Empty, prestamosCatalog, ct);
            InvalidateEmpresaPermissionCache(empresaId);
            return;
        }
        catch (SqlException ex) when (ex.Number == 2812)
        {
        }

        var rolePermColumns = await GetTableColumnsAsync(conn, "WRolPermiso", ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var roleId = await GetRoleIdByCodeAsync(conn, (SqlTransaction)tx, schema, empresaId, roleCode, ct);
        if (roleId is null)
            throw new KeyNotFoundException($"Rol no encontrado: {roleCode}");

        var permissionIds = await GetPermissionIdsByKeysAsync(conn, (SqlTransaction)tx, schema, empresaId, normalizedPermissions, ct);
        if (permissionIds.Count != normalizedPermissions.Count)
        {
            var missing = normalizedPermissions.Where(x => !permissionIds.Keys.Contains(x, StringComparer.OrdinalIgnoreCase));
            throw new ArgumentException($"Permisos inválidos: {string.Join(", ", missing)}");
        }

        var deleteSql = $@"
DELETE FROM dbo.WRolPermiso
WHERE EmpresaId = @EmpresaId
  AND {Q(schema.RolePermRoleIdColumn)} = @RoleId;";
        await using (var deleteCmd = new SqlCommand(deleteSql, conn, (SqlTransaction)tx))
        {
            deleteCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            deleteCmd.Parameters.Add(new SqlParameter("@RoleId", roleId));
            await deleteCmd.ExecuteNonQueryAsync(ct);
        }

        var insertColumns = new List<string> { "EmpresaId", schema.RolePermRoleIdColumn, schema.RolePermPermissionIdColumn };
        var insertValues = new List<string> { "@EmpresaId", "@RoleId", "@PermissionId" };
        if (rolePermColumns.Contains("FechaCreacion"))
        {
            insertColumns.Add("FechaCreacion");
            insertValues.Add("SYSUTCDATETIME()");
        }
        var insertSql = $@"
INSERT INTO dbo.WRolPermiso ({string.Join(", ", insertColumns.Select(Q))})
VALUES ({string.Join(", ", insertValues)});";
        foreach (var permissionId in permissionIds.Values)
        {
            await using var insertCmd = new SqlCommand(insertSql, conn, (SqlTransaction)tx);
            insertCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            insertCmd.Parameters.Add(new SqlParameter("@RoleId", roleId));
            insertCmd.Parameters.Add(new SqlParameter("@PermissionId", permissionId));
            await insertCmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        await SyncLegacyReportPermissionsForRoleAsync(conn, schema, empresaId, roleCode ?? string.Empty, reportCatalog, ct);
        await SyncLegacyModulePermissionsForRoleAsync(conn, schema, empresaId, roleCode ?? string.Empty, ventasCatalog, ct);
        await SyncLegacyModulePermissionsForRoleAsync(conn, schema, empresaId, roleCode ?? string.Empty, empleadosCatalog, ct);
        await SyncLegacyModulePermissionsForRoleAsync(conn, schema, empresaId, roleCode ?? string.Empty, prestamosCatalog, ct);
        InvalidateEmpresaPermissionCache(empresaId);
    }

    public async Task UpsertUserOverridesAsync(int empresaId, Guid userId, IReadOnlyCollection<string> allow, IReadOnlyCollection<string> deny, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);

        if (!await AreCapabilityTablesAvailableAsync(conn, ct))
            throw new KeyNotFoundException("Capacidades no disponibles para este tenant/base.");

        var schema = await GetSchemaAsync(conn, ct);
        var reportCatalog = await EnsureLegacyReportPermissionsSyncedAsync(conn, empresaId, ct);
        var ventasCatalog = await EnsureVentasPermissionsSyncedAsync(conn, empresaId, ct);
        var empleadosCatalog = await EnsureLegacyModulePermissionsSyncedAsync(conn, empresaId, EmpleadosBinding, ct);
        var prestamosCatalog = await EnsureLegacyModulePermissionsSyncedAsync(conn, empresaId, PrestamosBinding, ct);
        var overrideTypes = await GetOverrideTypeTokensAsync(conn, ct);
        var userPermCols = await GetUserPermColumnsAsync(conn, null, ct);

        var allowSet = allow.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var denySet = deny.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var activeModules = await GetActiveModulesAsync(conn, empresaId, ct);
        EnsurePermissionsInActiveModules(allowSet, activeModules);
        EnsurePermissionsInActiveModules(denySet, activeModules);

        await using var tx = await conn.BeginTransactionAsync(ct);

        await EnsureUserExistsAsync(conn, (SqlTransaction)tx, empresaId, userId, ct);

        var duplicated = allowSet.Intersect(denySet, StringComparer.OrdinalIgnoreCase).ToList();
        if (duplicated.Count > 0)
            throw new ArgumentException($"Un permiso no puede estar en allow y deny: {string.Join(", ", duplicated)}");

        var combined = allowSet.Concat(denySet).ToList();
        var permissionIds = await GetPermissionIdsByKeysAsync(conn, (SqlTransaction)tx, schema, empresaId, combined, ct);
        if (permissionIds.Count != combined.Count)
        {
            var missing = combined.Where(x => !permissionIds.Keys.Contains(x, StringComparer.OrdinalIgnoreCase));
            throw new ArgumentException($"Permisos inválidos: {string.Join(", ", missing)}");
        }

        var deleteSql = $@"
DELETE FROM dbo.WUsuarioPermiso
WHERE EmpresaId = @EmpresaId
  AND {Q(schema.UserPermUserIdColumn)} = @UsuarioWebId;";
        await using (var deleteCmd = new SqlCommand(deleteSql, conn, (SqlTransaction)tx))
        {
            deleteCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            deleteCmd.Parameters.Add(new SqlParameter("@UsuarioWebId", SqlDbType.UniqueIdentifier) { Value = userId });
            await deleteCmd.ExecuteNonQueryAsync(ct);
        }

        foreach (var key in allowSet)
            await InsertUserOverrideAsync(conn, (SqlTransaction)tx, schema, userPermCols, empresaId, userId, permissionIds[key], overrideTypes.AllowToken, ct);

        foreach (var key in denySet)
            await InsertUserOverrideAsync(conn, (SqlTransaction)tx, schema, userPermCols, empresaId, userId, permissionIds[key], overrideTypes.DenyToken, ct);

        await tx.CommitAsync(ct);
        var userRole = await GetUserRoleAsync(conn, empresaId, userId, ct);
        await SyncLegacyReportPermissionsForUserAsync(conn, schema, empresaId, userId, userRole, reportCatalog, ct);
        await SyncLegacyModulePermissionsForUserAsync(conn, schema, empresaId, userId, userRole, ventasCatalog, ct);
        await SyncLegacyModulePermissionsForUserAsync(conn, schema, empresaId, userId, userRole, empleadosCatalog, ct);
        await SyncLegacyModulePermissionsForUserAsync(conn, schema, empresaId, userId, userRole, prestamosCatalog, ct);
        InvalidateUserPermissionCache(userId, empresaId);
    }

    private void InvalidateUserPermissionCache(Guid userId, int empresaId)
    {
        var prefix = $"perm:{empresaId}:{userId:N}:";
        foreach (var key in _observedUserCacheKeys.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)))
        {
            _cache.Remove(key);
            _cache.Remove($"{key}:stale");
            _observedUserCacheKeys.TryRemove(key, out _);
        }

        var roleCandidates = new[] { "SuperAdmin", "Admin", "User", "SUPER_ADMIN", "ADMIN", "USER" };
        foreach (var role in roleCandidates)
        {
            var key = BuildCacheKey(userId, empresaId, role);
            _cache.Remove(key);
            _cache.Remove($"{key}:stale");
            _observedUserCacheKeys.TryRemove(key, out _);
        }
    }

    private void InvalidateEmpresaPermissionCache(int empresaId)
    {
        var prefix = $"perm:{empresaId}:";
        foreach (var key in _observedUserCacheKeys.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)))
        {
            _cache.Remove(key);
            _cache.Remove($"{key}:stale");
            _observedUserCacheKeys.TryRemove(key, out _);
        }
    }

    private void TrackObservedUserCacheKey(string cacheKey)
    {
        _observedUserCacheKeys.TryAdd(cacheKey, 1);
    }

    private async Task<OverrideTypeTokens> GetOverrideTypeTokensAsync(SqlConnection conn, CancellationToken ct)
    {
        // Modelo nuevo: Tipo CHAR/NCHAR(1) con valores A/D.
        await using (var tipoLenCmd = new SqlCommand(@"
SELECT c.max_length
FROM sys.tables t
INNER JOIN sys.columns c ON c.object_id = t.object_id
WHERE t.name = 'WUsuarioPermiso'
  AND c.name = 'Tipo';", conn))
        {
            var lenObj = await tipoLenCmd.ExecuteScalarAsync(ct);
            if (lenObj is not null && lenObj is not DBNull)
            {
                var maxLen = Convert.ToInt32(lenObj);
                if (maxLen == 1 || maxLen == 2) // nchar(1)=2 bytes
                {
                    return new OverrideTypeTokens("A", "D");
                }
            }
        }

        var sql = @"
SELECT cc.definition
FROM sys.check_constraints cc
INNER JOIN sys.tables t ON t.object_id = cc.parent_object_id
WHERE t.name = 'WUsuarioPermiso';";

        await using var cmd = new SqlCommand(sql, conn);
        var definitions = new List<string>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                if (!reader.IsDBNull(0))
                    definitions.Add(reader.GetString(0));
            }
        }
        var definition = string.Join(" ", definitions).ToLowerInvariant();

        var hasA = definition.Contains("'a'");
        var hasD = definition.Contains("'d'");
        if (hasA && hasD)
        {
            return new OverrideTypeTokens("A", "D");
        }

        var allowToken = definition.Contains("'allow'") ? "allow" : "permit";
        var denyToken = definition.Contains("'block'") && !definition.Contains("'deny'") ? "block" : "deny";
        return new OverrideTypeTokens(allowToken, denyToken);
    }

    public async Task<PermisosWebSyncCatalogResponse> SyncPermissionCatalogAsync(int empresaId, IReadOnlyCollection<string>? modules, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);

        if (!await AreCapabilityTablesAvailableAsync(conn, ct))
            throw new KeyNotFoundException("Capacidades no disponibles para este tenant/base.");

        var moduleFilter = (modules ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var seeds = PermissionCatalogSeeds
            .Where(x => moduleFilter.Count == 0 || moduleFilter.Contains(x.Module))
            .ToList();

        if (seeds.Count == 0)
            throw new ArgumentException("No hay permisos semilla para los módulos solicitados.");

        var permColumns = await GetTableColumnsAsync(conn, "WPermiso", ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var response = new PermisosWebSyncCatalogResponse
        {
            TotalSeeds = seeds.Count
        };

        foreach (var seed in seeds)
        {
            if (await PermissionExistsAsync(conn, (SqlTransaction)tx, empresaId, seed.Key, ct))
            {
                response.SkippedPermissions.Add(seed.Key);
                continue;
            }

            await InsertPermissionSeedAsync(conn, (SqlTransaction)tx, permColumns, empresaId, seed, null, ct);
            response.InsertedPermissions.Add(seed.Key);
        }

        await tx.CommitAsync(ct);
        response.InsertedCount = response.InsertedPermissions.Count;
        return response;
    }

    public async Task<bool> CreatePermissionAsync(int empresaId, string key, string name, string? description, CancellationToken ct)
    {
        var normalizedKey = NormalizePermissionKey(key);
        var normalizedName = string.IsNullOrWhiteSpace(name) ? normalizedKey : name.Trim();
        var normalizedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);

        if (!await AreCapabilityTablesAvailableAsync(conn, ct))
            throw new KeyNotFoundException("Capacidades no disponibles para este tenant/base.");

        var permColumns = await GetTableColumnsAsync(conn, "WPermiso", ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        if (await PermissionExistsAsync(conn, (SqlTransaction)tx, empresaId, normalizedKey, ct))
        {
            await tx.CommitAsync(ct);
            return false;
        }

        try
        {
            await using var spCmd = new SqlCommand("dbo.sp_w_Permiso_Guardar", conn, (SqlTransaction)tx)
            {
                CommandType = CommandType.StoredProcedure
            };
            spCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            spCmd.Parameters.Add(new SqlParameter("@Clave", SqlDbType.NVarChar, 150) { Value = normalizedKey });
            spCmd.Parameters.Add(new SqlParameter("@Nombre", SqlDbType.NVarChar, 150) { Value = normalizedName });
            spCmd.Parameters.Add(new SqlParameter("@Descripcion", SqlDbType.NVarChar, 300) { Value = (object?)normalizedDescription ?? DBNull.Value });
            await spCmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
            return true;
        }
        catch (SqlException ex) when (ex.Number == 2812)
        {
        }

        await InsertPermissionSeedAsync(
            conn,
            (SqlTransaction)tx,
            permColumns,
            empresaId,
            new PermissionSeed(normalizedKey, normalizedName, ModuleFromKey(normalizedKey)),
            normalizedDescription,
            ct);
        await tx.CommitAsync(ct);
        return true;
    }

    private async Task<PermissionSnapshot> BuildSnapshotAsync(Guid userId, int empresaId, string rolLegacy, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);

        if (!await AreCapabilityTablesAvailableAsync(conn, ct))
            return BuildLegacyFallback(userId, empresaId, rolLegacy);

        var feature = await GetFeatureStatusAsync(conn, empresaId, ct);
        if (!feature.Enabled)
            return BuildLegacyFallback(userId, empresaId, rolLegacy, feature.Version);

        var schema = await GetSchemaAsync(conn, ct);
        var permissions = await LoadEffectivePermissionsAsync(conn, schema, userId, empresaId, rolLegacy, ct);
        var reportCatalog = await EnsureLegacyReportPermissionsSyncedAsync(conn, empresaId, ct);
        var ventasCatalog = await EnsureVentasPermissionsSyncedAsync(conn, empresaId, ct);
        var empleadosCatalog = await EnsureLegacyModulePermissionsSyncedAsync(conn, empresaId, EmpleadosBinding, ct);
        var prestamosCatalog = await EnsureLegacyModulePermissionsSyncedAsync(conn, empresaId, PrestamosBinding, ct);
        if (reportCatalog.Count > 0)
        {
            var legacyReports = await LoadLegacyReportPermissionsForUserAsync(conn, empresaId, userId, reportCatalog, ct);
            permissions.RemoveAll(x => x.StartsWith($"{ReportesModuleKey}.", StringComparison.OrdinalIgnoreCase));
            permissions.AddRange(legacyReports.Allow.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        }
        if (ventasCatalog.Count > 0)
        {
            var legacyVentas = await LoadVentasPermissionsForUserAsync(conn, empresaId, userId, ventasCatalog, ct);
            permissions.RemoveAll(x => x.StartsWith($"{VentasModuleKey}.", StringComparison.OrdinalIgnoreCase) && !VentasWebPermissionSeeds.Any(seed => seed.Key.Equals(x, StringComparison.OrdinalIgnoreCase)));
            permissions.AddRange(legacyVentas.Allow.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        }
        // Empleados: lo mismo que ventas. Si Mac31 no tiene la forma dada de
        // alta, empleadosCatalog viene vacio y NO se borra nada — asi una base
        // sin ese modulo se queda con lo que diga el web, en vez de quedarse
        // sin permisos de empleados de golpe.
        if (empleadosCatalog.Count > 0)
        {
            var legacyEmpleados = await LoadLegacyModulePermissionsForUserAsync(conn, empresaId, userId, EmpleadosBinding, empleadosCatalog, ct);
            permissions.RemoveAll(x => x.StartsWith($"{EmpleadosModuleKey}.", StringComparison.OrdinalIgnoreCase) && !EmpleadosWebPermissionSeeds.Any(seed => seed.Key.Equals(x, StringComparison.OrdinalIgnoreCase)));
            permissions.AddRange(legacyEmpleados.Allow.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

            // QUIEN VE LA FORMA EN MAC31 VE EL MODULO EN EL WEB.
            //
            // "empleados.ver_modulo" no existe en legacy —es una llave del web,
            // la que decide si la tarjeta sale en el inicio—, asi que sin esta
            // linea habria que darla A MANO desde la pantalla de permisos a
            // cada quien que ya tiene el permiso en Mac31. Eso es justo la
            // doble captura que este puente viene a quitar.
            //
            // Medido en la base de Tauro: JAZMIN tiene los cuatro botones de
            // ConsultaEmpleados en Mac31, pero su rol web (EMPLEADOS) no tiene
            // "empleados.ver_modulo"; sin esta derivacion entraria por URL pero
            // no veria la tarjeta en el inicio.
            //
            // No sirve el parche de PermissionAuthorizationHandler (".ver"
            // satisface ".ver_modulo"): ese solo corre al autorizar un endpoint.
            // ModulosController compara la clave EXACTA contra el snapshot, asi
            // que la llave tiene que estar aqui dentro.
            if (legacyEmpleados.Allow.Contains("empleados.ver")
                && !permissions.Any(x => string.Equals(x, EmpleadosViewPermission, StringComparison.OrdinalIgnoreCase)))
            {
                permissions.Add(EmpleadosViewPermission);
            }
        }
        // Prestamos: exactamente el mismo puente que empleados. Si Mac31 no
        // tiene la forma dada de alta, prestamosCatalog viene vacio y NO se
        // borra nada — una base sin ese modulo se queda con lo que diga el web
        // en vez de quedarse sin permisos de prestamos de golpe.
        if (prestamosCatalog.Count > 0)
        {
            var legacyPrestamos = await LoadLegacyModulePermissionsForUserAsync(conn, empresaId, userId, PrestamosBinding, prestamosCatalog, ct);
            permissions.RemoveAll(x => x.StartsWith($"{PrestamosModuleKey}.", StringComparison.OrdinalIgnoreCase) && !PrestamosWebPermissionSeeds.Any(seed => seed.Key.Equals(x, StringComparison.OrdinalIgnoreCase)));
            permissions.AddRange(legacyPrestamos.Allow.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

            // QUIEN VE LA FORMA EN MAC31 VE EL MODULO EN EL WEB.
            //
            // Mismo motivo que en empleados: "prestamos.ver_modulo" no existe en
            // legacy —es la llave del web que decide si la tarjeta sale en el
            // inicio—, asi que sin esta linea habria que darla A MANO a cada
            // quien que ya tiene el boton en Mac31, que es justo la doble
            // captura que este puente viene a quitar.
            //
            // Se deriva de "prestamos.ver" y no de los de escribir: quien puede
            // MODIFICAR pero no VER no existe en Mac31 (btnVer y btnModificar
            // son permisos aparte, pero el que solo tiene modificar igual abre
            // la misma forma). Ver el modulo es el minimo comun.
            if (legacyPrestamos.Allow.Contains("prestamos.ver")
                && !permissions.Any(x => string.Equals(x, PrestamosViewPermission, StringComparison.OrdinalIgnoreCase)))
            {
                permissions.Add(PrestamosViewPermission);
            }
        }
        return new PermissionSnapshot
        {
            UserId = userId,
            EmpresaId = empresaId,
            RolLegacy = rolLegacy,
            PermissionsEnabled = true,
            Permissions = permissions,
            PermissionsVersion = feature.Version
        };
    }

    private static async Task<bool> AreCapabilityTablesAvailableAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(@"
SELECT CASE
    WHEN OBJECT_ID('dbo.WRol', 'U') IS NOT NULL
     AND OBJECT_ID('dbo.WPermiso', 'U') IS NOT NULL
     AND OBJECT_ID('dbo.WRolPermiso', 'U') IS NOT NULL
     AND OBJECT_ID('dbo.WUsuarioPermiso', 'U') IS NOT NULL
    THEN 1 ELSE 0 END;", conn);
        var value = await cmd.ExecuteScalarAsync(ct);
        return value is not null && Convert.ToInt32(value) == 1;
    }

    private static async Task<(bool Enabled, string Version)> GetFeatureStatusAsync(SqlConnection conn, int empresaId, CancellationToken ct)
    {
        await using (var existsCmd = new SqlCommand("SELECT CASE WHEN OBJECT_ID('dbo.WConfiguracionEmpresa', 'U') IS NOT NULL THEN 1 ELSE 0 END;", conn))
        {
            var exists = await existsCmd.ExecuteScalarAsync(ct);
            if (exists is null || Convert.ToInt32(exists) != 1)
                return (true, DateTime.UtcNow.ToString("O"));
        }

        await using var cmd = new SqlCommand(@"
SELECT TOP 1
    Activo,
    FechaActualizacion
FROM dbo.WConfiguracionEmpresa
WHERE EmpresaId = @EmpresaId
  AND Clave = @Clave
ORDER BY FechaActualizacion DESC;", conn);
        cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
        cmd.Parameters.Add(new SqlParameter("@Clave", SqlDbType.NVarChar, 200) { Value = FeatureKey });

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return (true, DateTime.UtcNow.ToString("O"));

        var enabled = reader.GetBoolean(reader.GetOrdinal("Activo"));
        var version = reader.IsDBNull(reader.GetOrdinal("FechaActualizacion"))
            ? DateTime.UtcNow.ToString("O")
            : reader.GetDateTime(reader.GetOrdinal("FechaActualizacion")).ToUniversalTime().ToString("O");
        return (enabled, version);
    }

    private static async Task<List<string>> LoadEffectivePermissionsAsync(
        SqlConnection conn,
        CapabilitySchema schema,
        Guid userId,
        int empresaId,
        string rolLegacy,
        CancellationToken ct)
    {
        var rolCodigo = ToRoleCode(rolLegacy);

        var sql = $@"
;WITH RolePermisos AS (
    SELECT p.Clave
    FROM dbo.WRol r
    INNER JOIN dbo.WRolPermiso rp
        ON rp.EmpresaId = r.EmpresaId
       AND rp.{Q(schema.RolePermRoleIdColumn)} = r.{Q(schema.RoleIdColumn)}
    INNER JOIN dbo.WPermiso p
        ON p.EmpresaId = rp.EmpresaId
       AND p.{Q(schema.PermissionIdColumn)} = rp.{Q(schema.RolePermPermissionIdColumn)}
    WHERE r.EmpresaId = @EmpresaId
      AND r.Codigo = @RolCodigo
),
Permits AS (
    SELECT p.Clave
    FROM dbo.WUsuarioPermiso up
    INNER JOIN dbo.WPermiso p
        ON p.EmpresaId = up.EmpresaId
       AND p.{Q(schema.PermissionIdColumn)} = up.{Q(schema.UserPermPermissionIdColumn)}
    WHERE up.EmpresaId = @EmpresaId
      AND up.{Q(schema.UserPermUserIdColumn)} = @UsuarioWebId
      AND LOWER(up.Tipo) IN ('permit','allow','a')
),
Denies AS (
    SELECT p.Clave
    FROM dbo.WUsuarioPermiso up
    INNER JOIN dbo.WPermiso p
        ON p.EmpresaId = up.EmpresaId
       AND p.{Q(schema.PermissionIdColumn)} = up.{Q(schema.UserPermPermissionIdColumn)}
    WHERE up.EmpresaId = @EmpresaId
      AND up.{Q(schema.UserPermUserIdColumn)} = @UsuarioWebId
      AND LOWER(up.Tipo) IN ('deny','block','d')
),
PermisosUnidos AS (
    SELECT Clave FROM RolePermisos
    UNION
    SELECT Clave FROM Permits
)
SELECT pu.Clave
FROM PermisosUnidos pu
LEFT JOIN Denies d ON d.Clave = pu.Clave
WHERE d.Clave IS NULL
ORDER BY pu.Clave;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
        cmd.Parameters.Add(new SqlParameter("@UsuarioWebId", SqlDbType.UniqueIdentifier) { Value = userId });
        cmd.Parameters.Add(new SqlParameter("@RolCodigo", SqlDbType.NVarChar, 30) { Value = rolCodigo });

        var permissions = new List<string>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var clave = reader.GetString(reader.GetOrdinal("Clave"));
                if (!string.IsNullOrWhiteSpace(clave))
                    permissions.Add(clave);
            }
        }

        /*
          UN MODULO APAGADO NO DA NINGUN PERMISO.

          Antes esto no se miraba aqui, y por eso apagar un modulo tenia que
          BORRAR los permisos de los roles y los ajustes de cada usuario: era lo
          unico que de verdad quitaba el acceso. El precio era que apagarlo no se
          podia deshacer — al volver a prenderlo habia que repartir todo otra vez
          a mano, y nadie se acordaba de que tenia quien.

          Mirandolo aqui, apagar un modulo deja de destruir nada: los permisos se
          quedan guardados, simplemente no cuentan mientras este apagado. Volver a
          prenderlo devuelve las cosas como estaban.

          SE EXCLUYE SOLO LO QUE ESTA APAGADO A PROPOSITO, nunca "lo que no esta
          encendido". Si dbo.WModulo no existe, o esta vacia, o no tiene renglon
          para un modulo, no se quita nada: una base sin ese catalogo seguiria
          funcionando igual. Al reves —exigir que cada modulo este listado— una
          tabla vacia dejaria a todo el mundo sin permisos.
        */
        var apagados = await LoadInactiveModuleKeysAsync(conn, empresaId, ct);
        if (apagados.Count == 0)
            return permissions;

        return permissions
            .Where(clave => !EsDeModuloApagado(clave, apagados))
            .ToList();
    }

    private static bool EsDeModuloApagado(string clave, HashSet<string> apagados)
    {
        var punto = clave.IndexOf('.');
        if (punto <= 0) return false;
        return apagados.Contains(clave.Substring(0, punto));
    }

    /// Las claves de los modulos marcados como inactivos para esta empresa.
    /// Vacio si la tabla no existe: ver la razon arriba.
    private static async Task<HashSet<string>> LoadInactiveModuleKeysAsync(
        SqlConnection conn,
        int empresaId,
        CancellationToken ct)
    {
        var apagados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!await TableExistsAsync(conn, "WModulo", ct))
            return apagados;

        await using var cmd = new SqlCommand(@"
SELECT ModuloClave
FROM dbo.WModulo
WHERE EmpresaId = @EmpresaId
  AND IdStatus = 2;", conn);
        cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var clave = reader.GetString(0);
            if (!string.IsNullOrWhiteSpace(clave))
                apagados.Add(clave.Trim());
        }
        return apagados;
    }

    private async Task<CapabilitySchema> GetSchemaAsync(SqlConnection conn, CancellationToken ct)
    {
        if (_cache.TryGetValue<CapabilitySchema>(SchemaCacheKey, out var cached) && cached is not null)
            return cached;

        await using var cmd = new SqlCommand(@"
SELECT t.name AS TableName, c.name AS ColumnName
FROM sys.tables t
INNER JOIN sys.columns c ON c.object_id = t.object_id
WHERE t.name IN ('WRol','WPermiso','WRolPermiso','WUsuarioPermiso');", conn);

        var columnsByTable = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var table = reader.GetString(reader.GetOrdinal("TableName"));
                var col = reader.GetString(reader.GetOrdinal("ColumnName"));
                if (!columnsByTable.TryGetValue(table, out var set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    columnsByTable[table] = set;
                }
                set.Add(col);
            }
        }

        string Pick(string table, params string[] candidates)
        {
            if (!columnsByTable.TryGetValue(table, out var cols))
                throw new InvalidOperationException($"Tabla no disponible: {table}");
            var chosen = candidates.FirstOrDefault(cols.Contains);
            if (string.IsNullOrWhiteSpace(chosen))
                throw new InvalidOperationException($"No se encontro columna esperada en {table}. Candidatas: {string.Join(", ", candidates)}");
            return chosen;
        }

        var schema = new CapabilitySchema(
            RoleIdColumn: Pick("WRol", "Id", "WRolId", "RolId"),
            PermissionIdColumn: Pick("WPermiso", "Id", "WPermisoId", "PermisoId"),
            RolePermRoleIdColumn: Pick("WRolPermiso", "WRolId", "RolId"),
            RolePermPermissionIdColumn: Pick("WRolPermiso", "WPermisoId", "PermisoId"),
            UserPermUserIdColumn: Pick("WUsuarioPermiso", "UsuarioWebId", "UsuarioId", "UserId", "WUsuarioId"),
            UserPermPermissionIdColumn: Pick("WUsuarioPermiso", "WPermisoId", "PermisoId")
        );

        _cache.Set(SchemaCacheKey, schema, SchemaCacheTtl);
        return schema;
    }

    private static string ToRoleCode(string rolLegacy)
    {
        if (string.Equals(rolLegacy, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
            return "SUPER_ADMIN";
        if (string.Equals(rolLegacy, "Admin", StringComparison.OrdinalIgnoreCase))
            return "ADMIN";
        return NormalizeRoleCode(rolLegacy, fallback: "USER");
    }

    private static string NormalizeRoleCodeForCreate(string? roleCode)
    {
        var normalized = NormalizeRoleCode(roleCode, fallback: string.Empty);
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("Codigo de rol requerido.");
        if (normalized.Length < 3 || normalized.Length > 30)
            throw new ArgumentException("Código de rol inválido. Longitud permitida: 3 a 30.");
        return normalized;
    }

    private static string NormalizeRoleNameForCreate(string? name, string code)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length > 80)
            trimmed = trimmed[..80];
        if (!string.IsNullOrWhiteSpace(trimmed))
            return trimmed;

        return string.Join(" ",
            code.Split('_', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x[..1] + x[1..].ToLowerInvariant()));
    }

    private static string NormalizeRoleCode(string? raw, string fallback)
    {
        var value = (raw ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var normalizedChars = value
            .Select(ch =>
            {
                if (char.IsLetterOrDigit(ch)) return ch;
                if (ch == '_' || ch == '-' || char.IsWhiteSpace(ch)) return '_';
                return '\0';
            })
            .Where(ch => ch != '\0')
            .ToArray();

        var compact = new string(normalizedChars);
        while (compact.Contains("__", StringComparison.Ordinal))
            compact = compact.Replace("__", "_", StringComparison.Ordinal);
        compact = compact.Trim('_').ToUpperInvariant();

        return string.IsNullOrWhiteSpace(compact) ? fallback : compact;
    }

    private static PermissionSnapshot BuildLegacyFallback(Guid userId, int empresaId, string rolLegacy, string? version = null)
    {
        return new PermissionSnapshot
        {
            UserId = userId,
            EmpresaId = empresaId,
            RolLegacy = rolLegacy,
            PermissionsEnabled = false,
            Permissions = new List<string>(),
            PermissionsVersion = version ?? DateTime.UtcNow.ToString("O")
        };
    }

    private static string BuildCacheKey(Guid userId, int empresaId, string rolLegacy)
        => $"perm:{empresaId}:{userId:N}:{rolLegacy}";

    private static async Task<object?> GetRoleIdByCodeAsync(
        SqlConnection conn,
        SqlTransaction tx,
        CapabilitySchema schema,
        int empresaId,
        string roleCode,
        CancellationToken ct)
    {
        var sql = $@"
SELECT TOP 1 {Q(schema.RoleIdColumn)}
FROM dbo.WRol
WHERE EmpresaId = @EmpresaId
  AND UPPER(Codigo) = UPPER(@Codigo);";
        await using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
        cmd.Parameters.Add(new SqlParameter("@Codigo", SqlDbType.NVarChar, 30) { Value = roleCode.Trim() });
        var value = await cmd.ExecuteScalarAsync(ct);
        return value is DBNull ? null : value;
    }

    private static async Task<Dictionary<string, object>> GetPermissionIdsByKeysAsync(
        SqlConnection conn,
        SqlTransaction tx,
        CapabilitySchema schema,
        int empresaId,
        IReadOnlyCollection<string> keys,
        CancellationToken ct)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var sql = $@"
SELECT TOP 1 {Q(schema.PermissionIdColumn)}
FROM dbo.WPermiso
WHERE EmpresaId = @EmpresaId
  AND Clave = @Clave;";

        foreach (var key in keys)
        {
            await using var cmd = new SqlCommand(sql, conn, tx);
            cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            cmd.Parameters.Add(new SqlParameter("@Clave", SqlDbType.NVarChar, 200) { Value = key });
            var value = await cmd.ExecuteScalarAsync(ct);
            if (value is null || value is DBNull) continue;
            result[key] = value;
        }

        return result;
    }

    private static async Task EnsureUserExistsAsync(
        SqlConnection conn,
        SqlTransaction tx,
        int empresaId,
        Guid userId,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand(@"
SELECT TOP 1 1
FROM dbo.UsuarioWeb
WHERE EmpresaId = @EmpresaId
  AND Id = @Id;", conn, tx);
        cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
        cmd.Parameters.Add(new SqlParameter("@Id", SqlDbType.UniqueIdentifier) { Value = userId });
        var exists = await cmd.ExecuteScalarAsync(ct);
        if (exists is null)
            throw new KeyNotFoundException($"Usuario no encontrado: {userId}");
    }

    private async Task InsertUserOverrideAsync(
        SqlConnection conn,
        SqlTransaction tx,
        CapabilitySchema schema,
        UserPermColumns userPermCols,
        int empresaId,
        Guid userId,
        object permissionId,
        string tipo,
        CancellationToken ct)
    {
        var safeMotivo = NormalizeForColumn("actualizacion.permisosweb", userPermCols.MotivoMaxChars);
        var rawTipo = (tipo ?? string.Empty).Trim();
        var tipoCandidates = BuildTipoCandidates(rawTipo);

        var sql = $@"
INSERT INTO dbo.WUsuarioPermiso (EmpresaId, {Q(schema.UserPermUserIdColumn)}, {Q(schema.UserPermPermissionIdColumn)}, Tipo, Motivo)
VALUES (@EmpresaId, @UsuarioWebId, @WPermisoId, @Tipo, @Motivo);";
        SqlException? lastCheckEx = null;

        foreach (var candidate in tipoCandidates)
        {
            var safeTipo = NormalizeForColumn(candidate, userPermCols.TipoMaxChars);
            try
            {
                await using var cmd = new SqlCommand(sql, conn, tx);
                cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
                cmd.Parameters.Add(new SqlParameter("@UsuarioWebId", SqlDbType.UniqueIdentifier) { Value = userId });
                cmd.Parameters.Add(new SqlParameter("@WPermisoId", permissionId));
                cmd.Parameters.Add(new SqlParameter("@Tipo", SqlDbType.NVarChar, Math.Max(1, safeTipo.Length)) { Value = safeTipo });
                cmd.Parameters.Add(new SqlParameter("@Motivo", SqlDbType.NVarChar, Math.Max(1, safeMotivo.Length)) { Value = safeMotivo });
                await cmd.ExecuteNonQueryAsync(ct);
                return;
            }
            catch (SqlException ex) when (IsTipoCheckConstraintViolation(ex))
            {
                lastCheckEx = ex;
            }
        }

        if (lastCheckEx is not null) throw lastCheckEx;
        throw new InvalidOperationException("No se pudo insertar override de usuario.");
    }

    private static bool IsTipoCheckConstraintViolation(SqlException ex)
        => ex.Number == 547 &&
           ex.Message.Contains("CK_WUsuarioPermiso_Tipo", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> BuildTipoCandidates(string rawTipo)
    {
        var token = rawTipo.Trim();
        if (string.IsNullOrWhiteSpace(token))
            return new[] { "deny", "block", "D", "d" };

        if (string.Equals(token, "A", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "allow", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "permit", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { "permit", "allow", "A", "a" };
        }

        return new[] { "deny", "block", "D", "d" };
    }

    private async Task<UserPermColumns> GetUserPermColumnsAsync(SqlConnection conn, SqlTransaction? tx, CancellationToken ct)
    {
        if (_cache.TryGetValue<UserPermColumns>(UserPermColumnsCacheKey, out var cached) && cached is not null)
            return cached;

        await using var cmd = new SqlCommand(@"
SELECT c.name, c.max_length
FROM sys.tables t
INNER JOIN sys.columns c ON c.object_id = t.object_id
WHERE t.name = 'WUsuarioPermiso'
  AND c.name IN ('Tipo','Motivo');", conn, tx);

        var tipoMaxChars = 20;
        var motivoMaxChars = 300;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var name = reader.GetString(0);
            var maxLengthBytes = reader.GetInt16(1);
            var maxChars = maxLengthBytes < 0 ? 4000 : Math.Max(1, maxLengthBytes / 2); // nvarchar/nchar stored in bytes

            if (string.Equals(name, "Tipo", StringComparison.OrdinalIgnoreCase))
                tipoMaxChars = maxChars;
            else if (string.Equals(name, "Motivo", StringComparison.OrdinalIgnoreCase))
                motivoMaxChars = maxChars;
        }

        var cols = new UserPermColumns(tipoMaxChars, motivoMaxChars);
        _cache.Set(UserPermColumnsCacheKey, cols, SchemaCacheTtl);
        return cols;
    }

    private static string NormalizeForColumn(string? value, int maxChars)
    {
        var text = value ?? string.Empty;
        if (maxChars <= 0) return string.Empty;
        return text.Length <= maxChars ? text : text.Substring(0, maxChars);
    }

    private static async Task<HashSet<string>> GetTableColumnsAsync(
        SqlConnection conn,
        string tableName,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand(@"
SELECT c.name
FROM sys.tables t
INNER JOIN sys.columns c ON c.object_id = t.object_id
WHERE t.name = @TableName;", conn);
        cmd.Parameters.Add(new SqlParameter("@TableName", SqlDbType.NVarChar, 128) { Value = tableName });

        var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            cols.Add(reader.GetString(0));
        }
        return cols;
    }

    private static async Task<bool> PermissionExistsAsync(
        SqlConnection conn,
        SqlTransaction tx,
        int empresaId,
        string key,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand(@"
SELECT TOP 1 1
FROM dbo.WPermiso
WHERE EmpresaId = @EmpresaId
  AND Clave = @Clave;", conn, tx);
        cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
        cmd.Parameters.Add(new SqlParameter("@Clave", SqlDbType.NVarChar, 200) { Value = key });
        var exists = await cmd.ExecuteScalarAsync(ct);
        return exists is not null;
    }

    private static async Task<bool> TableExistsAsync(SqlConnection conn, string tableName, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(@"
SELECT CASE WHEN OBJECT_ID(@TableName, 'U') IS NOT NULL THEN 1 ELSE 0 END;", conn);
        cmd.Parameters.Add(new SqlParameter("@TableName", SqlDbType.NVarChar, 260) { Value = $"dbo.{tableName}" });
        var value = await cmd.ExecuteScalarAsync(ct);
        return value is not null && Convert.ToInt32(value) == 1;
    }

    private static async Task InsertPermissionSeedAsync(
        SqlConnection conn,
        SqlTransaction tx,
        HashSet<string> permColumns,
        int empresaId,
        PermissionSeed seed,
        string? explicitDescription,
        CancellationToken ct)
    {
        var insertColumns = new List<string> { "EmpresaId", "Clave", "Nombre" };
        var insertValues = new List<string> { "@EmpresaId", "@Clave", "@Nombre" };

        if (permColumns.Contains("Descripcion"))
        {
            insertColumns.Add("Descripcion");
            insertValues.Add("@Descripcion");
        }
        if (permColumns.Contains("FechaCreacion"))
        {
            insertColumns.Add("FechaCreacion");
            insertValues.Add("SYSUTCDATETIME()");
        }

        var sql = $@"
INSERT INTO dbo.WPermiso ({string.Join(", ", insertColumns.Select(Q))})
VALUES ({string.Join(", ", insertValues)});";

        await using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
        cmd.Parameters.Add(new SqlParameter("@Clave", SqlDbType.NVarChar, 200) { Value = seed.Key });
        cmd.Parameters.Add(new SqlParameter("@Nombre", SqlDbType.NVarChar, 200) { Value = seed.Name });
        if (permColumns.Contains("Descripcion"))
            cmd.Parameters.Add(new SqlParameter("@Descripcion", SqlDbType.NVarChar, 500) { Value = explicitDescription ?? $"{seed.Module}.seed" });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<Dictionary<string, ReportPermissionInfo>> EnsureLegacyReportPermissionsSyncedAsync(
        SqlConnection conn,
        int empresaId,
        CancellationToken ct)
    {
        var reportCatalog = await LoadLegacyReportCatalogAsync(conn, ct);
        if (reportCatalog.Count == 0 || !await TableExistsAsync(conn, "WPermiso", ct))
            return reportCatalog.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);

        if (await TableExistsAsync(conn, "WModulo", ct))
        {
            await using var moduleCmd = new SqlCommand(@"
IF EXISTS (SELECT 1 FROM dbo.WModulo WHERE EmpresaId = @EmpresaId AND ModuloClave = @ModuloClave)
BEGIN
    UPDATE dbo.WModulo
       SET Nombre = @Nombre,
           IdStatus = CASE WHEN IdStatus = 2 THEN 2 ELSE 1 END,
           FechaActualizacion = CASE WHEN COL_LENGTH('dbo.WModulo','FechaActualizacion') IS NOT NULL THEN SYSUTCDATETIME() ELSE FechaActualizacion END
     WHERE EmpresaId = @EmpresaId
       AND ModuloClave = @ModuloClave;
END
ELSE
BEGIN
    INSERT INTO dbo.WModulo (EmpresaId, ModuloClave, Nombre, IdStatus, FechaCreacion, FechaActualizacion)
    VALUES (@EmpresaId, @ModuloClave, @Nombre, 1, SYSUTCDATETIME(), SYSUTCDATETIME());
END", conn);
            moduleCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            moduleCmd.Parameters.Add(new SqlParameter("@ModuloClave", SqlDbType.NVarChar, 120) { Value = ReportesModuleKey });
            moduleCmd.Parameters.Add(new SqlParameter("@Nombre", SqlDbType.NVarChar, 200) { Value = "Reportes" });
            await moduleCmd.ExecuteNonQueryAsync(ct);
        }

        var permissionRows = reportCatalog
            .Select(x => new PermissionSeed(x.Key, x.Name, ReportesModuleKey))
            .Prepend(new PermissionSeed(ReportesViewPermission, "Reportes - Ver modulo", ReportesModuleKey))
            .ToList();
        await SincronizarCatalogoDePermisosAsync(
            conn,
            empresaId,
            permissionRows,
            seed => seed.Key == ReportesViewPermission ? "reportes.web" : "reportes.legacy",
            ct);

        return reportCatalog.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<List<ReportPermissionInfo>> LoadLegacyReportCatalogAsync(SqlConnection conn, CancellationToken ct)
    {
        if (!await TableExistsAsync(conn, "n_Formas", ct) || !await TableExistsAsync(conn, "n_Procesos", ct))
            return new List<ReportPermissionInfo>();

        await using var cmd = new SqlCommand(@"
SELECT
    f.IDForma,
    p.IDProceso,
    f.Descripcion AS FormaDescripcion,
    p.Proceso,
    p.Descripcion AS ProcesoDescripcion,
    COALESCE(f.Orden, 0) AS FormaOrden,
    COALESCE(p.Orden, 0) AS ProcesoOrden
FROM dbo.n_Formas f
INNER JOIN dbo.n_Procesos p
    ON p.IDForma = f.IDForma
WHERE f.Descripcion = 'REPORTES'
  AND ISNULL(p.IDStatus, 1) = 1
ORDER BY COALESCE(f.Orden, 0), COALESCE(p.Orden, 0), p.IDProceso;", conn);

        var list = new List<ReportPermissionInfo>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var processDescription = reader.IsDBNull(reader.GetOrdinal("ProcesoDescripcion"))
                ? string.Empty
                : reader.GetString(reader.GetOrdinal("ProcesoDescripcion"));
            var processCode = reader.IsDBNull(reader.GetOrdinal("Proceso"))
                ? string.Empty
                : reader.GetString(reader.GetOrdinal("Proceso"));
            var (categoryKey, categoryName, reportName) = SplitLegacyReportName(processDescription, processCode);
            var reportSlug = Slug(reportName);
            if (string.IsNullOrWhiteSpace(categoryKey) || string.IsNullOrWhiteSpace(reportSlug))
                continue;

            var info = new ReportPermissionInfo(
                Key: $"{ReportesModuleKey}.{categoryKey}.{reportSlug}.ver",
                Name: $"{categoryName} - {reportName}",
                CategoryKey: categoryKey,
                CategoryName: categoryName,
                LegacyFormId: reader.GetInt32(reader.GetOrdinal("IDForma")),
                LegacyProcessId: reader.GetInt32(reader.GetOrdinal("IDProceso")),
                SortOrder: reader.GetInt32(reader.GetOrdinal("ProcesoOrden"))
            );
            if (!list.Any(x => x.Key.Equals(info.Key, StringComparison.OrdinalIgnoreCase)))
                list.Add(info);
        }

        return list;
    }

    private static async Task<(HashSet<string> Allow, HashSet<string> Deny)> LoadLegacyReportPermissionsForUserAsync(
        SqlConnection conn,
        int empresaId,
        Guid userId,
        IReadOnlyDictionary<string, ReportPermissionInfo> reportCatalog,
        CancellationToken ct)
    {
        var allow = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deny = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (reportCatalog.Count == 0)
            return (allow, deny);

        var legacyUserId = await ResolveLegacyUserIdAsync(conn, empresaId, userId, ct);
        if (legacyUserId is null)
            return (allow, deny);

        var byProcessId = reportCatalog.Values.ToDictionary(x => x.LegacyProcessId, x => x.Key);
        await using var cmd = new SqlCommand(@"
SELECT p.IDProceso, CASE WHEN ufp.IDStatus = 1 THEN 1 ELSE 0 END AS Permitido
FROM dbo.n_Formas f
INNER JOIN dbo.n_Procesos p
    ON p.IDForma = f.IDForma
LEFT JOIN dbo.[Usuario Forma Procesos] ufp
    ON ufp.IDProceso = p.IDProceso
   AND ufp.IDUsuario = @IDUsuario
WHERE f.Descripcion = 'REPORTES'
  AND ISNULL(p.IDStatus, 1) = 1;", conn);
        cmd.Parameters.Add(new SqlParameter("@IDUsuario", SqlDbType.Int) { Value = legacyUserId.Value });
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var processId = reader.GetInt32(reader.GetOrdinal("IDProceso"));
            if (!byProcessId.TryGetValue(processId, out var key))
                continue;
            var permitted = reader.GetInt32(reader.GetOrdinal("Permitido")) == 1;
            if (permitted) allow.Add(key);
            else deny.Add(key);
        }

        return (allow, deny);
    }

    private async Task SyncLegacyReportPermissionsForUserAsync(
        SqlConnection conn,
        CapabilitySchema schema,
        int empresaId,
        Guid userId,
        string rolLegacy,
        IReadOnlyDictionary<string, ReportPermissionInfo> reportCatalog,
        CancellationToken ct)
    {
        if (reportCatalog.Count == 0)
            return;

        var legacyUserId = await ResolveLegacyUserIdAsync(conn, empresaId, userId, ct);
        if (legacyUserId is null)
            return;

        var effective = await LoadEffectivePermissionsFromWebAsync(conn, schema, userId, empresaId, rolLegacy, ct);
        var movementUserId = await ResolveMovementLegacyUserIdAsync(conn, ct);
        foreach (var report in reportCatalog.Values)
        {
            // Solo se OTORGA. Nunca se revoca: mismo motivo que en el sync de
            // ventas (ver SyncLegacyVentasPermissionsForUserAsync). Guardar
            // permisos desde el web le estaba borrando a la gente sus reportes
            // de Mac31.
            if (!effective.Contains(report.Key))
                continue;

            await using var cmd = new SqlCommand("dbo.sp_n_ActualizarUsuarioFormaProceso", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.Add(new SqlParameter("@IDUsuario", SqlDbType.Int) { Value = legacyUserId.Value });
            cmd.Parameters.Add(new SqlParameter("@IDProceso", SqlDbType.Int) { Value = report.LegacyProcessId });
            cmd.Parameters.Add(new SqlParameter("@IDStatus", SqlDbType.Int) { Value = 1 });
            cmd.Parameters.Add(new SqlParameter("@IDUsuarioMovimiento", SqlDbType.Int) { Value = movementUserId });
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    private async Task SyncLegacyReportPermissionsForRoleAsync(
        SqlConnection conn,
        CapabilitySchema schema,
        int empresaId,
        string roleCode,
        IReadOnlyDictionary<string, ReportPermissionInfo> reportCatalog,
        CancellationToken ct)
    {
        if (reportCatalog.Count == 0)
            return;

        await using var cmd = new SqlCommand(@"
SELECT Id, Rol
FROM dbo.UsuarioWeb
WHERE EmpresaId = @EmpresaId
  AND UPPER(Rol) = @Rol;", conn);
        cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
        cmd.Parameters.Add(new SqlParameter("@Rol", SqlDbType.NVarChar, 80) { Value = ToRoleCode(roleCode) });
        var users = new List<(Guid UserId, string Rol)>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
                users.Add((reader.GetGuid(reader.GetOrdinal("Id")), reader.GetString(reader.GetOrdinal("Rol"))));
        }

        foreach (var user in users)
            await SyncLegacyReportPermissionsForUserAsync(conn, schema, empresaId, user.UserId, user.Rol, reportCatalog, ct);
    }

    /*
      VENTAS SON DOS FORMAS DE MAC31, NO UNA.

      Los botones de la consulta viven en CONSULTA DE VENTAS y los dos de la
      pantalla de pagos en CONSULTA DE VENTAS PAGOS. Para el web es UN solo
      modulo ("ventas") y un solo conjunto de permisos, asi que se juntan aqui
      y no en cada uno de los ocho sitios que piden el catalogo: repetir el par
      de llamadas ocho veces es como se acaba con un sitio que sincroniza una
      forma y se olvida de la otra.

      NO HAY CHOQUE DE IDProceso entre las dos formas: la consulta usa 1..18,
      1031, 3063, 5096, 5151-5154, 5196 y 53xx; la de pagos usa 1032 y 1033.
      Verificado en Produccion_svr (Tauro) y en MacZ (Zaragoza). Importa porque
      el catalogo se indexa por IDProceso al leer los permisos del usuario: dos
      formas con el mismo numero se pisarian.
    */
    private async Task<Dictionary<string, VentasPermissionInfo>> EnsureVentasPermissionsSyncedAsync(
        SqlConnection conn,
        int empresaId,
        CancellationToken ct)
    {
        var catalogo = await EnsureLegacyModulePermissionsSyncedAsync(conn, empresaId, VentasBinding, ct);
        var pagos = await EnsureLegacyModulePermissionsSyncedAsync(conn, empresaId, VentasPagosBinding, ct);
        foreach (var par in pagos)
            catalogo[par.Key] = par.Value;
        return catalogo;
    }

    /// <summary>
    /// Lo que el usuario tiene encendido en las DOS formas de ventas, en un
    /// solo par Allow/Deny.
    /// </summary>
    private static async Task<(HashSet<string> Allow, HashSet<string> Deny)> LoadVentasPermissionsForUserAsync(
        SqlConnection conn,
        int empresaId,
        Guid userId,
        IReadOnlyDictionary<string, VentasPermissionInfo> catalogo,
        CancellationToken ct)
    {
        var consulta = await LoadLegacyModulePermissionsForUserAsync(conn, empresaId, userId, VentasBinding, catalogo, ct);
        var pagos = await LoadLegacyModulePermissionsForUserAsync(conn, empresaId, userId, VentasPagosBinding, catalogo, ct);

        consulta.Allow.UnionWith(pagos.Allow);
        consulta.Deny.UnionWith(pagos.Deny);
        return consulta;
    }

    private async Task<Dictionary<string, VentasPermissionInfo>> EnsureLegacyModulePermissionsSyncedAsync(
        SqlConnection conn,
        int empresaId,
        LegacyModuleBinding binding,
        CancellationToken ct)
    {
        var ventasCatalog = await LoadLegacyModuleCatalogAsync(conn, binding, ct);
        if (!await TableExistsAsync(conn, "WPermiso", ct))
            return ventasCatalog.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);

        if (await TableExistsAsync(conn, "WModulo", ct))
        {
            await using var moduleCmd = new SqlCommand(@"
IF EXISTS (SELECT 1 FROM dbo.WModulo WHERE EmpresaId = @EmpresaId AND ModuloClave = @ModuloClave)
BEGIN
    UPDATE dbo.WModulo
       SET Nombre = @Nombre,
           IdStatus = CASE WHEN IdStatus = 2 THEN 2 ELSE 1 END,
           FechaActualizacion = CASE WHEN COL_LENGTH('dbo.WModulo','FechaActualizacion') IS NOT NULL THEN SYSUTCDATETIME() ELSE FechaActualizacion END
     WHERE EmpresaId = @EmpresaId
       AND ModuloClave = @ModuloClave;
END
ELSE
BEGIN
    INSERT INTO dbo.WModulo (EmpresaId, ModuloClave, Nombre, IdStatus, FechaCreacion, FechaActualizacion)
    VALUES (@EmpresaId, @ModuloClave, @Nombre, 1, SYSUTCDATETIME(), SYSUTCDATETIME());
END", conn);
            moduleCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            moduleCmd.Parameters.Add(new SqlParameter("@ModuloClave", SqlDbType.NVarChar, 120) { Value = binding.ModuleKey });
            moduleCmd.Parameters.Add(new SqlParameter("@Nombre", SqlDbType.NVarChar, 200) { Value = binding.ModuleName });
            await moduleCmd.ExecuteNonQueryAsync(ct);
        }

        var permissionRows = ventasCatalog
            .Select(x => new PermissionSeed(x.Key, x.Name, binding.ModuleKey))
            .Concat(binding.WebSeeds)
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();

        await SincronizarCatalogoDePermisosAsync(
            conn,
            empresaId,
            permissionRows,
            seed => binding.WebSeeds.Any(x => x.Key.Equals(seed.Key, StringComparison.OrdinalIgnoreCase))
                ? $"{binding.ModuleKey}.web"
                : $"{binding.ModuleKey}.legacy",
            ct);

        return ventasCatalog.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<List<VentasPermissionInfo>> LoadLegacyModuleCatalogAsync(SqlConnection conn, LegacyModuleBinding binding, CancellationToken ct)
    {
        if (!await TableExistsAsync(conn, "n_Formas", ct) || !await TableExistsAsync(conn, "n_Procesos", ct))
            return new List<VentasPermissionInfo>();

        await using var cmd = new SqlCommand(@"
SELECT
    f.IDForma,
    p.IDProceso,
    p.Proceso,
    p.Descripcion AS ProcesoDescripcion,
    COALESCE(p.Orden, 0) AS ProcesoOrden
FROM dbo.n_Formas f
INNER JOIN dbo.n_Procesos p
    ON p.IDForma = f.IDForma
WHERE UPPER(LTRIM(RTRIM(f.Descripcion))) = @Forma
  AND ISNULL(p.IDStatus, 1) = 1
ORDER BY COALESCE(p.Orden, 0), p.IDProceso;", conn);
        cmd.Parameters.Add(new SqlParameter("@Forma", SqlDbType.NVarChar, 200) { Value = binding.LegacyForm });

        var list = new List<VentasPermissionInfo>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var processCode = reader.IsDBNull(reader.GetOrdinal("Proceso"))
                ? string.Empty
                : reader.GetString(reader.GetOrdinal("Proceso"));
            // El Trim() es lo UNICO que cambia respecto de como estaba antes
            // solo para ventas: un Proceso guardado en n_Procesos con un
            // espacio al final ("btnBuscar ") antes no empataba con el mapa y
            // se perdia el permiso en silencio. Se deja porque el espacio es
            // un error de captura, no una forma de negar un permiso.
            if (!binding.Map.TryGetValue(processCode.Trim(), out var mapped))
                continue;

            var info = new VentasPermissionInfo(
                Key: mapped.Key,
                Name: mapped.Name,
                CategoryKey: "acciones",
                CategoryName: "Acciones",
                LegacyFormId: reader.GetInt32(reader.GetOrdinal("IDForma")),
                LegacyProcessId: reader.GetInt32(reader.GetOrdinal("IDProceso")),
                SortOrder: reader.GetInt32(reader.GetOrdinal("ProcesoOrden"))
            );
            if (!list.Any(x => x.Key.Equals(info.Key, StringComparison.OrdinalIgnoreCase)))
                list.Add(info);
        }

        return list;
    }

    private static async Task<(HashSet<string> Allow, HashSet<string> Deny)> LoadLegacyModulePermissionsForUserAsync(
        SqlConnection conn,
        int empresaId,
        Guid userId,
        LegacyModuleBinding binding,
        IReadOnlyDictionary<string, VentasPermissionInfo> ventasCatalog,
        CancellationToken ct)
    {
        var allow = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deny = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (ventasCatalog.Count == 0)
            return (allow, deny);

        var legacyUserId = await ResolveLegacyUserIdAsync(conn, empresaId, userId, ct);
        if (legacyUserId is null)
            return (allow, deny);

        var byProcessId = ventasCatalog.Values.ToDictionary(x => x.LegacyProcessId, x => x.Key);
        await using var cmd = new SqlCommand(@"
SELECT p.IDProceso, CASE WHEN ufp.IDStatus = 1 THEN 1 ELSE 0 END AS Permitido
FROM dbo.n_Formas f
INNER JOIN dbo.n_Procesos p
    ON p.IDForma = f.IDForma
LEFT JOIN dbo.[Usuario Forma Procesos] ufp
    ON ufp.IDProceso = p.IDProceso
   AND ufp.IDUsuario = @IDUsuario
WHERE UPPER(LTRIM(RTRIM(f.Descripcion))) = @Forma
  AND ISNULL(p.IDStatus, 1) = 1;", conn);
        cmd.Parameters.Add(new SqlParameter("@IDUsuario", SqlDbType.Int) { Value = legacyUserId.Value });
        cmd.Parameters.Add(new SqlParameter("@Forma", SqlDbType.NVarChar, 200) { Value = binding.LegacyForm });
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var processId = reader.GetInt32(reader.GetOrdinal("IDProceso"));
            if (!byProcessId.TryGetValue(processId, out var key))
                continue;
            var permitted = reader.GetInt32(reader.GetOrdinal("Permitido")) == 1;
            if (permitted) allow.Add(key);
            else deny.Add(key);
        }

        return (allow, deny);
    }

    private async Task SyncLegacyModulePermissionsForUserAsync(
        SqlConnection conn,
        CapabilitySchema schema,
        int empresaId,
        Guid userId,
        string rolLegacy,
        IReadOnlyDictionary<string, VentasPermissionInfo> ventasCatalog,
        CancellationToken ct)
    {
        if (ventasCatalog.Count == 0)
            return;

        var legacyUserId = await ResolveLegacyUserIdAsync(conn, empresaId, userId, ct);
        if (legacyUserId is null)
            return;

        var effective = await LoadEffectivePermissionsFromWebAsync(conn, schema, userId, empresaId, rolLegacy, ct);
        var movementUserId = await ResolveMovementLegacyUserIdAsync(conn, ct);
        foreach (var action in ventasCatalog.Values)
        {
            // Solo se OTORGA en legacy. NUNCA se revoca.
            //
            // Antes esta linea mandaba IDStatus=2 para todo lo que el web no
            // tuviera, tratando al web como la fuente de la verdad. No lo es:
            // los permisos de ventas VIVEN en legacy (se leen de los botones
            // de Mac31) y no existen como filas en las tablas web, asi que
            // "effective" casi nunca los contiene.
            //
            // Consecuencia real, medida en produccion el 2026-08-26: guardar
            // permisos de un usuario desde la pantalla web le revoco 44 de sus
            // 135 permisos de Mac31 —todos los botones de CONSULTA DE VENTAS y
            // varios reportes— sin avisar y sin que nadie los hubiera tocado.
            //
            // Quitar un permiso de legacy se hace en Mac31, que es donde vive.
            if (!effective.Contains(action.Key))
                continue;

            await using var cmd = new SqlCommand("dbo.sp_n_ActualizarUsuarioFormaProceso", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.Add(new SqlParameter("@IDUsuario", SqlDbType.Int) { Value = legacyUserId.Value });
            cmd.Parameters.Add(new SqlParameter("@IDProceso", SqlDbType.Int) { Value = action.LegacyProcessId });
            cmd.Parameters.Add(new SqlParameter("@IDStatus", SqlDbType.Int) { Value = 1 });
            cmd.Parameters.Add(new SqlParameter("@IDUsuarioMovimiento", SqlDbType.Int) { Value = movementUserId });
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    private async Task SyncLegacyModulePermissionsForRoleAsync(
        SqlConnection conn,
        CapabilitySchema schema,
        int empresaId,
        string roleCode,
        IReadOnlyDictionary<string, VentasPermissionInfo> ventasCatalog,
        CancellationToken ct)
    {
        if (ventasCatalog.Count == 0)
            return;

        await using var cmd = new SqlCommand(@"
SELECT Id, Rol
FROM dbo.UsuarioWeb
WHERE EmpresaId = @EmpresaId
  AND UPPER(Rol) = @Rol;", conn);
        cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
        cmd.Parameters.Add(new SqlParameter("@Rol", SqlDbType.NVarChar, 80) { Value = ToRoleCode(roleCode) });
        var users = new List<(Guid UserId, string Rol)>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
                users.Add((reader.GetGuid(reader.GetOrdinal("Id")), reader.GetString(reader.GetOrdinal("Rol"))));
        }

        foreach (var user in users)
            await SyncLegacyModulePermissionsForUserAsync(conn, schema, empresaId, user.UserId, user.Rol, ventasCatalog, ct);
    }

    private static async Task<HashSet<string>> LoadEffectivePermissionsFromWebAsync(
        SqlConnection conn,
        CapabilitySchema schema,
        Guid userId,
        int empresaId,
        string rolLegacy,
        CancellationToken ct)
    {
        var permissions = await LoadEffectivePermissionsAsync(conn, schema, userId, empresaId, rolLegacy, ct);
        return permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<int?> ResolveLegacyUserIdAsync(SqlConnection conn, int empresaId, Guid userId, CancellationToken ct)
    {
        var hasLegacyColumn = await ColumnExistsAsync(conn, "UsuarioWeb", "LegacyUserId", ct);
        var sql = hasLegacyColumn
            ? @"
SELECT TOP 1
    COALESCE(TRY_CONVERT(int, uw.LegacyUserId), u.IDUsuario) AS IDUsuario
FROM dbo.UsuarioWeb uw
LEFT JOIN dbo.Usuarios u
    ON UPPER(LTRIM(RTRIM(u.Usuario))) = UPPER(LTRIM(RTRIM(uw.Usuario)))
WHERE uw.EmpresaId = @EmpresaId
  AND uw.Id = @UsuarioWebId;"
            : @"
SELECT TOP 1 u.IDUsuario
FROM dbo.UsuarioWeb uw
INNER JOIN dbo.Usuarios u
    ON UPPER(LTRIM(RTRIM(u.Usuario))) = UPPER(LTRIM(RTRIM(uw.Usuario)))
WHERE uw.EmpresaId = @EmpresaId
  AND uw.Id = @UsuarioWebId;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
        cmd.Parameters.Add(new SqlParameter("@UsuarioWebId", SqlDbType.UniqueIdentifier) { Value = userId });
        var raw = await cmd.ExecuteScalarAsync(ct);
        if (raw is null || raw == DBNull.Value)
            return null;
        return Convert.ToInt32(raw, CultureInfo.InvariantCulture);
    }

    private static async Task<int> ResolveMovementLegacyUserIdAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(@"
SELECT TOP 1 IDUsuario
FROM dbo.Usuarios
WHERE UPPER(LTRIM(RTRIM(Usuario))) IN ('JUAN','ADMIN')
ORDER BY CASE WHEN UPPER(LTRIM(RTRIM(Usuario))) = 'JUAN' THEN 0 ELSE 1 END, IDUsuario;", conn);
        var raw = await cmd.ExecuteScalarAsync(ct);
        return raw is null || raw == DBNull.Value ? 0 : Convert.ToInt32(raw, CultureInfo.InvariantCulture);
    }

    private static async Task<string> GetUserRoleAsync(SqlConnection conn, int empresaId, Guid userId, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(@"
SELECT TOP 1 Rol
FROM dbo.UsuarioWeb
WHERE EmpresaId = @EmpresaId
  AND Id = @UsuarioWebId;", conn);
        cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
        cmd.Parameters.Add(new SqlParameter("@UsuarioWebId", SqlDbType.UniqueIdentifier) { Value = userId });
        var raw = await cmd.ExecuteScalarAsync(ct);
        return raw is null || raw == DBNull.Value ? "User" : Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "User";
    }

    private static async Task<bool> ColumnExistsAsync(SqlConnection conn, string tableName, string columnName, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(@"
SELECT 1
FROM sys.columns c
INNER JOIN sys.tables t ON t.object_id = c.object_id
WHERE t.name = @TableName
  AND c.name = @ColumnName;", conn);
        cmd.Parameters.Add(new SqlParameter("@TableName", SqlDbType.NVarChar, 128) { Value = tableName });
        cmd.Parameters.Add(new SqlParameter("@ColumnName", SqlDbType.NVarChar, 128) { Value = columnName });
        var raw = await cmd.ExecuteScalarAsync(ct);
        return raw is not null && raw != DBNull.Value;
    }

    private static PermisosWebPermissionItem EnrichPermissionItem(
        string code,
        string name,
        IReadOnlyDictionary<string, ReportPermissionInfo> reportCatalog,
        IReadOnlyDictionary<string, VentasPermissionInfo> ventasCatalog,
        IReadOnlyDictionary<string, VentasPermissionInfo> empleadosCatalog,
        IReadOnlyDictionary<string, VentasPermissionInfo> prestamosCatalog)
    {
        if (reportCatalog.TryGetValue(code, out var report))
        {
            return new PermisosWebPermissionItem
            {
                Code = report.Key,
                Name = report.Name,
                ModuleKey = ReportesModuleKey,
                ModuleName = "Reportes",
                CategoryKey = report.CategoryKey,
                CategoryName = report.CategoryName,
                LegacyFormId = report.LegacyFormId,
                LegacyProcessId = report.LegacyProcessId,
                IsLegacyReport = true
            };
        }

        if (ventasCatalog.TryGetValue(code, out var venta))
        {
            return new PermisosWebPermissionItem
            {
                Code = venta.Key,
                Name = venta.Name,
                ModuleKey = VentasModuleKey,
                ModuleName = "Ventas",
                CategoryKey = venta.CategoryKey,
                CategoryName = venta.CategoryName,
                LegacyFormId = venta.LegacyFormId,
                LegacyProcessId = venta.LegacyProcessId
            };
        }

        // Empleados: se marca con su IDForma/IDProceso de Mac31 para que la
        // pantalla de permisos ensene de donde sale y no parezca un permiso
        // que se pueda dar desde el web.
        if (empleadosCatalog.TryGetValue(code, out var empleado))
        {
            return new PermisosWebPermissionItem
            {
                Code = empleado.Key,
                Name = empleado.Name,
                ModuleKey = EmpleadosModuleKey,
                ModuleName = "Empleados",
                CategoryKey = empleado.CategoryKey,
                CategoryName = empleado.CategoryName,
                LegacyFormId = empleado.LegacyFormId,
                LegacyProcessId = empleado.LegacyProcessId
            };
        }

        // Prestamos: igual que empleados, se marca con su IDForma/IDProceso de
        // Mac31 para que la pantalla de permisos ensene de donde sale.
        if (prestamosCatalog.TryGetValue(code, out var prestamo))
        {
            return new PermisosWebPermissionItem
            {
                Code = prestamo.Key,
                Name = prestamo.Name,
                ModuleKey = PrestamosModuleKey,
                ModuleName = "Prestamos",
                CategoryKey = prestamo.CategoryKey,
                CategoryName = prestamo.CategoryName,
                LegacyFormId = prestamo.LegacyFormId,
                LegacyProcessId = prestamo.LegacyProcessId
            };
        }

        return new PermisosWebPermissionItem
        {
            Code = code,
            Name = name,
            ModuleKey = ExtractModuleFromPermissionKey(code),
            ModuleName = string.Empty
        };
    }

    private static (string CategoryKey, string CategoryName, string ReportName) SplitLegacyReportName(string description, string processCode)
    {
        var text = (description ?? string.Empty).Trim();
        var separator = text.IndexOf(" - ", StringComparison.Ordinal);
        if (separator > 0)
        {
            var category = text[..separator].Trim();
            var report = text[(separator + 3)..].Trim();
            return (Slug(category), ToTitle(category), ToTitle(report));
        }

        var code = (processCode ?? string.Empty).Trim();
        var dash = code.IndexOf('-', StringComparison.Ordinal);
        if (dash > 0)
        {
            var category = code[..dash].Trim();
            var report = code[(dash + 1)..].Replace("-", " ").Trim();
            return (Slug(category), ToTitle(category), ToTitle(report));
        }

        return ("reportes", "Reportes", ToTitle(text));
    }

    private static string Slug(string value)
    {
        var normalized = RemoveDiacritics(value ?? string.Empty).ToLowerInvariant();
        var sb = new StringBuilder(normalized.Length);
        var pendingSeparator = false;
        foreach (var ch in normalized)
        {
            if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
            {
                if (pendingSeparator && sb.Length > 0)
                    sb.Append('_');
                sb.Append(ch);
                pendingSeparator = false;
            }
            else
            {
                pendingSeparator = true;
            }
        }
        return sb.ToString().Trim('_');
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string ToTitle(string value)
    {
        var text = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;
        return CultureInfo.GetCultureInfo("es-MX").TextInfo.ToTitleCase(text);
    }

    private static bool TryReadGuid(object? raw, out Guid value)
    {
        value = Guid.Empty;
        if (raw is Guid g)
        {
            value = g;
            return true;
        }
        if (raw is string s && Guid.TryParse(s, out var parsed))
        {
            value = parsed;
            return true;
        }
        return false;
    }

    private static async Task<HashSet<string>?> GetActiveModulesAsync(SqlConnection conn, int empresaId, CancellationToken ct)
    {
        if (!await TableExistsAsync(conn, "WModulo", ct))
            return null;

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = new SqlCommand(@"
SELECT ModuloClave
FROM dbo.WModulo
WHERE EmpresaId = @EmpresaId
  AND IdStatus = 1;", conn);
        cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var key = NormalizeModuleKey(reader.GetString(0));
            if (!string.IsNullOrWhiteSpace(key))
                result.Add(key);
        }
        return result;
    }

    private static bool IsPermissionInActiveModule(string permissionKey, HashSet<string>? activeModules)
    {
        if (activeModules is null)
            return true;
        if (activeModules.Count == 0)
            return false;
        var module = ExtractModuleFromPermissionKey(permissionKey);
        if (string.IsNullOrWhiteSpace(module))
            return false;
        return activeModules.Contains(module);
    }

    private static void EnsurePermissionsInActiveModules(IEnumerable<string> permissions, HashSet<string>? activeModules)
    {
        if (activeModules is null) return;
        var invalid = permissions
            .Where(p => !IsPermissionInActiveModule(p, activeModules))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (invalid.Count > 0)
            throw new ArgumentException($"Permisos en módulos inactivos: {string.Join(", ", invalid)}");
    }

    private static string ExtractModuleFromPermissionKey(string key)
    {
        var normalized = (key ?? string.Empty).Trim().ToLowerInvariant();
        var idx = normalized.IndexOf('.');
        var module = idx > 0 ? normalized[..idx] : normalized;
        try
        {
            return NormalizeModuleKey(module);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string NormalizePermissionKey(string key)
    {
        var value = (key ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("La clave del permiso es requerida.");
        if (value.Length > 200)
            throw new ArgumentException("La clave del permiso excede el maximo permitido.");

        foreach (var ch in value)
        {
            var valid = (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '.' || ch == '_';
            if (!valid)
                throw new ArgumentException("La clave del permiso solo permite a-z, 0-9, punto y guión bajo.");
        }

        if (!value.Contains('.', StringComparison.Ordinal))
            throw new ArgumentException("La clave debe tener formato módulo.acción");

        return value;
    }

    private static string NormalizeModuleKey(string key)
    {
        var value = (key ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("ModuloClave es obligatorio.");
        if (value.Length > 120)
            throw new ArgumentException("ModuloClave excede el maximo permitido.");

        foreach (var ch in value)
        {
            var valid = (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '_';
            if (!valid)
                throw new ArgumentException("ModuloClave solo permite a-z, 0-9 y guión bajo.");
        }

        return value;
    }

    private static string ModuleFromKey(string key)
    {
        var idx = key.IndexOf('.');
        return idx > 0 ? key.Substring(0, idx) : "general";
    }

    private static string ResolveModuleRoute(string moduleKey)
    {
        return moduleKey switch
        {
            "inicio" => "/inicio/index.html",
            "usuarios" => "/usuarios-internos/index.html",
            "proveedores" => "/proveedores/index.html",
            "pagosproveedores" => "/pagos-proveedores/index.html",
            "cheques" => "/cheques/index.html",
            "permisos_modulos" => "/permisos-modulos/index.html",
            "permisos_roles" => "/permisos-roles/index.html",
            "permisos_usuarios" => "/permisos-usuarios/index.html",
            // Modulos del front NUEVO (docs2). Ahi no hay una carpeta con su
            // index.html por modulo: es UNA aplicacion, y su direccion publica
            // se reescribe a /v2/index.html en docs/staticwebapp.config.json.
            // Devolver "/cascos_cambio/index.html" daba "Cannot GET": la
            // carpeta no existe ni va a existir.
            "cascos_cambio" => "/cascos-cambio",
            _ => $"/{moduleKey}/index.html"
        };
    }

    private static string Q(string identifier) => $"[{identifier}]";

    private sealed record CapabilitySchema(
        string RoleIdColumn,
        string PermissionIdColumn,
        string RolePermRoleIdColumn,
        string RolePermPermissionIdColumn,
        string UserPermUserIdColumn,
        string UserPermPermissionIdColumn);

    private sealed record OverrideTypeTokens(string AllowToken, string DenyToken);
    private sealed record UserPermColumns(int TipoMaxChars, int MotivoMaxChars);

    /*
      SEMBRAR EL CATALOGO SIN REESCRIBIRLO ENTERO EN CADA PETICION

      Esto asegura que cada permiso de legacy exista en WPermiso y tenga el
      nombre al dia. Antes lo hacia con un upsert POR PERMISO, siempre, hubiera
      cambiado algo o no.

      Lo que costaba, medido: una sola llamada a /api/me disparaba 219 consultas
      a la base, y 182 de ellas eran este bucle —91 "IF EXISTS" y 91 "UPDATE"—
      escribiendo exactamente los mismos valores que ya estaban.

      En local no se notaba porque la base esta en la misma maquina y cada ida y
      vuelta cuesta casi cero. Contra la base de la oficina, cada una cuesta
      decenas de milisegundos, y 182 seguidas son los segundos que el usuario
      veia al abrir la app. El trabajo no estaba en la base: estaba en el viaje,
      repetido 182 veces.

      Ahora se pregunta PRIMERO —una sola consulta— como esta el catalogo, y se
      escribe unicamente lo que falta o lo que de verdad cambio. En el caso
      normal, que es que no haya cambiado nada, queda en UNA consulta y CERO
      escrituras.

      POR QUE COMPARAR CONTRA LA BASE Y NO GUARDARLO EN MEMORIA: un recordatorio
      en memoria se equivoca en cuanto alguien toca la tabla por fuera —o cuando
      hay mas de una instancia, o cuando el proceso reinicia—, y entonces el
      permiso que falta no se vuelve a crear. Preguntando, se arregla solo.
    */
    private static async Task<int> SincronizarCatalogoDePermisosAsync(
        SqlConnection conn,
        int empresaId,
        IReadOnlyList<PermissionSeed> semillas,
        Func<PermissionSeed, string> descripcionDe,
        CancellationToken ct)
    {
        if (semillas.Count == 0)
            return 0;

        var columnas = await GetTableColumnsAsync(conn, "WPermiso", ct);
        var hayDescripcion = columnas.Contains("Descripcion");

        /* Como esta hoy. Se piden los de la empresa entera y no los 91 por
           nombre: es la misma ida y vuelta y no hay que armar 91 parametros. */
        var actuales = new Dictionary<string, (string Nombre, string Descripcion)>(StringComparer.OrdinalIgnoreCase);
        await using (var lectura = new SqlCommand(
            hayDescripcion
                ? "SELECT Clave, ISNULL(Nombre, '') AS Nombre, ISNULL(Descripcion, '') AS Descripcion FROM dbo.WPermiso WHERE EmpresaId = @EmpresaId;"
                : "SELECT Clave, ISNULL(Nombre, '') AS Nombre, '' AS Descripcion FROM dbo.WPermiso WHERE EmpresaId = @EmpresaId;",
            conn))
        {
            lectura.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            await using var reader = await lectura.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var clave = reader.GetString(0);
                if (!string.IsNullOrWhiteSpace(clave))
                    actuales[clave.Trim()] = (reader.GetString(1), reader.GetString(2));
            }
        }

        var escritos = 0;
        foreach (var seed in semillas)
        {
            var descripcion = descripcionDe(seed);

            /* Ya esta igual: no se toca. Escribir lo mismo no solo cuesta el
               viaje, ademas mueve FechaActualizacion y hace parecer que alguien
               cambio permisos cuando nadie los cambio. */
            if (actuales.TryGetValue(seed.Key, out var actual)
                && string.Equals(actual.Nombre, seed.Name, StringComparison.Ordinal)
                && (!hayDescripcion || string.Equals(actual.Descripcion, descripcion, StringComparison.Ordinal)))
            {
                continue;
            }

            await using var cmd = new SqlCommand(@"
IF EXISTS (SELECT 1 FROM dbo.WPermiso WHERE EmpresaId = @EmpresaId AND Clave = @Clave)
BEGIN
    UPDATE dbo.WPermiso
       SET Nombre = @Nombre,
           Descripcion = CASE WHEN COL_LENGTH('dbo.WPermiso','Descripcion') IS NOT NULL THEN @Descripcion ELSE Descripcion END
     WHERE EmpresaId = @EmpresaId
       AND Clave = @Clave;
END
ELSE
BEGIN
    INSERT INTO dbo.WPermiso (EmpresaId, Clave, Nombre, Descripcion, FechaCreacion)
    VALUES (@EmpresaId, @Clave, @Nombre, @Descripcion, SYSUTCDATETIME());
END", conn);
            cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            cmd.Parameters.Add(new SqlParameter("@Clave", SqlDbType.NVarChar, 200) { Value = seed.Key });
            cmd.Parameters.Add(new SqlParameter("@Nombre", SqlDbType.NVarChar, 200) { Value = seed.Name });
            cmd.Parameters.Add(new SqlParameter("@Descripcion", SqlDbType.NVarChar, 500) { Value = descripcion });
            await cmd.ExecuteNonQueryAsync(ct);
            escritos++;
        }

        return escritos;
    }

    private sealed record PermissionSeed(string Key, string Name, string Module);
    private sealed record ReportPermissionInfo(
        string Key,
        string Name,
        string CategoryKey,
        string CategoryName,
        int LegacyFormId,
        int LegacyProcessId,
        int SortOrder);
    private sealed record VentasPermissionInfo(
        string Key,
        string Name,
        string CategoryKey,
        string CategoryName,
        int LegacyFormId,
        int LegacyProcessId,
        int SortOrder);
}
