using System.Data;
using ISL_Service.Application.DTOs.Responses;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Models;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

using ISL_Service.Application.Security;

namespace ISL_Service.Infrastructure.Security;

public sealed partial class PermissionService : IPermissionService
{
    private const string FeatureKey = "autorizacion.capacidades";
    private const string SchemaCacheKey = "permissions:schema:v2";
    private const string OverrideTypeCacheKey = "permissions:override-type:v1";
    private const string UserPermColumnsCacheKey = "permissions:userperm-columns:v1";
    private const string ReportesModuleKey = "reportes";

    /*
      HAY DOS FORMAS LLAMADAS "REPORTES" EN n_Formas, Y FILTRAR POR EL NOMBRE
      VISIBLE EMPATABA CON LAS DOS.

      Medido el 2026-10-09 en las dos empresas, identico en MacZ y en
      Produccion_svr:

        IDForma 4030  Forma "ReportesMenu"      Descripcion "REPORTES"  -> 51 / 50 procesos
        IDForma 4081  Forma "ReportesAviacion"  Descripcion "REPORTES"  ->  0 procesos

      El catalogo de reportes se buscaba por `f.Descripcion = 'REPORTES'`, asi
      que leia las DOS. Hoy no se nota porque la de aviacion esta vacia, y por
      eso llevaba tiempo ahi sin molestar. Pero el dia que alguien de alta un
      proceso en ReportesAviacion, ese boton se colaria en el modulo
      "reportes" del web sin que nadie lo hubiera decidido —y, ahora que
      reportes tambien se ESCRIBE hacia Mac31, se volveria escribible desde la
      pantalla de permisos del web.

      Se filtra por `Forma` y no por `IDForma` a secas para no clavar un numero
      en el codigo: el nombre interno de la forma es el mismo en las dos
      empresas, los IDProceso NO (ver PermissionService.FamiliasLegacy.cs), y
      quien resuelve es siempre la conexion de la empresa en curso.
    */
    private const string ReportesLegacyForm = "ReportesMenu";
    private const string ReportesViewPermission = "reportes.ver_modulo";

    /*
      LAS CLAVES DE REPORTES QUE SON DEL WEB Y NO DE MAC31.

      Para `reportes.*` manda legacy: al leer se borra lo que diga el web y se
      pone lo de Mac31. Pero `reportes.ver_modulo` NO existe en Mac31 —es la
      llave del web, la que decide si sale la tarjeta de Reportes—, asi que el
      borrado tambien se la llevaba: se guardaba en WUsuarioPermiso y
      desaparecia en la siguiente lectura. Desde la pantalla se veia como
      "le doy Guardar, dice que si, y al volver a entrar no esta".

      Comprobado en produccion de Zaragoza: RICARDO tenia `reportes.ver_modulo`
      con Tipo=allow en la base y aun asi la casilla salia apagada, porque en
      Mac31 tiene CERO reportes encendidos y lo de Mac31 sustituia a lo del web.

      Ventas, Empleados y Prestamos ya exceptuaban sus llaves del web
      (VentasWebPermissionSeeds y compania). Reportes era la unica de las cuatro
      familias que no lo hacia. Esto la iguala.
    */
    /*
      LAS CLAVES DE `reportes.` QUE MANDA EL WEB Y NO MAC31.

      Todo lo que empiece con "reportes." y NO este aqui (ni sea de centros) se
      BORRA del snapshot y se vuelve a calcular desde los procesos de Mac31. O
      sea: si una clave de reportes es del web y no aparece en esta lista, darla
      desde la pantalla de permisos no sirve de nada — se guarda en
      WUsuarioPermiso, no da error, y desaparece al siguiente calculo.

      "reportes.parametros.ver" y ".editar" ESTABAN FUERA, Y ERA UN FALLO.
      Guardan los parametros de reportes de ConfiguracionMacController
      (PermisosParametrosVer / PermisosParametrosGuardar), que es una funcion
      del web: en Mac31 no hay ningun proceso equivalente, asi que el
      reemplazo por lo de legacy se las llevaba y nunca las devolvia.

      Medido: concedidas por la API a QA_LEGWEB (que SI existe en Mac31) junto
      con "reportes.administracion.cortes.ver". Las tres quedaron como `allow`
      en WUsuarioPermiso; en /api/me volvieron "reportes.ver_modulo" y
      "reportes.administracion.cortes.ver" —esta ultima ademas se escribio en
      Mac31, IDStatus=1— y "reportes.parametros.ver" NO volvio. Ese es el fallo
      silencioso, y se arregla haciendolas del web en vez de deshabilitar la
      casilla: la funcion existe y hay que poder concederla.
    */
    private static readonly HashSet<string> ReportesWebPermissionKeys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ReportesViewPermission,
            "reportes.parametros.ver",
            "reportes.parametros.editar"
        };

    /*
      LOS PERMISOS DE CENTROS DE SERVICIO TAMBIEN SON DEL WEB.

      'reportes.centros.cambiar' y 'reportes.centros.<numero>.ver' no existen en
      Mac31 —alla cada centro tiene su propia instalacion apuntada a su base, no
      hay nada que permitir—, asi que el reemplazo por lo de legacy tampoco debe
      llevarselos. Se comparan por PREFIJO y no uno a uno porque son tantos como
      centros haya, y aparecen solos cuando se da de alta uno nuevo.
    */
    private const string CentrosPermissionPrefix = PermisosDeCentros.Prefijo;

    /* True si esta clave de reportes la manda Mac31 y hay que reemplazarla. */
    private static bool EsReporteQueMandaLegacy(string clave) =>
        clave.StartsWith($"{ReportesModuleKey}.", StringComparison.OrdinalIgnoreCase)
        && !ReportesWebPermissionKeys.Contains(clave)
        && !clave.StartsWith(CentrosPermissionPrefix, StringComparison.OrdinalIgnoreCase);
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
    /// <param name="LegacyForm">
    /// La DESCRIPCION de la forma en n_Formas. Se deja como estaba porque las
    /// cinco ataduras que ya existian la usan asi.
    /// </param>
    /// <param name="FormaExacta">
    /// El NOMBRE de la forma (n_Formas.Forma) cuando la descripcion no sirve
    /// para distinguirla. Hace falta porque hay descripciones repetidas:
    /// medido en las DOS empresas el 2026-10-10, "CONSULTA DE PROVEEDORES" son
    /// dos formas distintas (ConsultarProveedores con 3 procesos y
    /// MCConsultarProveedores con 7), y "REPORTES" otras dos. Empatar por
    /// descripcion ahi mezcla los permisos de dos pantallas. Cuando viene con
    /// valor, manda sobre <paramref name="LegacyForm"/>.
    /// </param>
    /// <param name="Prefijo">
    /// Prefijo de las claves que se DERIVEN con la tabla de verbos, sin punto
    /// final. Si va en null se usa <paramref name="ModuleKey"/>. Hace falta
    /// cuando una familia tiene varias formas y cada una tiene su btnNuevo: sin
    /// prefijo propio las cuatro formas de proveedores derivarian la misma
    /// clave y se pisarian.
    /// </param>
    /// <param name="ClaveDeEntrada">
    /// El ".ver_modulo" que se concede a quien Mac31 le deje usar ESTA FORMA.
    /// Es la version por pantalla de FamiliaLegacy.ClaveDeEntrada, y existe
    /// porque hay familias cuyas formas abren pantallas DISTINTAS del web: la
    /// familia "inventario." alimenta dos, y sus entradas se llaman
    /// "inventario_ajustes.ver_modulo" e "inventario_transito.ver_modulo". Una
    /// sola clave por familia no podia expresarlo, asi que la familia declaraba
    /// null y NADIE heredaba la entrada: medido el 2026-10-12, cuatro usuarios
    /// de Zaragoza y tres de Tauro heredaban los botones de ajustes al
    /// inventario y el backend se los rechazaba con 403.
    /// </param>
    /// <param name="ClaveDeLectura">
    /// La clave de LEER esta pantalla, cuando es una semilla del web que Mac31
    /// no puede producir. En Mac31 un boton vive DENTRO de su forma: si la
    /// forma esta en tu menu la puedes leer, y esta en tu menu justamente
    /// porque tienes alguno de sus procesos. AJUSTES AL INVENTARIO es el caso:
    /// tiene btnNuevo y btnCancelar y ni un boton de buscar, asi que
    /// "inventario.ajustes.ver" -que es lo que exige
    /// InventarioOperacionesController- era inalcanzable desde Mac31.
    /// Se deja en null salvo que se compruebe que la forma no tiene ningun
    /// boton de lectura: donde si lo tiene, el permiso debe venir de ese boton
    /// y no regalarse.
    /// </param>
    /// <param name="ConcesionesExtra">
    /// Boton de ESTA forma -> clave del web QUE YA EXISTE y que ese boton
    /// tambien debe conceder. SOLO PARA LEER.
    ///
    /// POR QUE NO SE PONEN EN EL `Map`
    /// -------------------------------
    /// Porque el `Map` alimenta el CATALOGO, y el catalogo guarda UN IDProceso
    /// por clave: es el boton que el web enciende o apaga en Mac31 cuando se
    /// concede o se revoca desde la pantalla de permisos. Si una clave tuviera
    /// dos botones en el catalogo, revocarla desde el web apagaria uno y
    /// dejaria el otro encendido, y el usuario se quedaria con el permiso en
    /// Mac31 y sin el en el web -o al contrario-. Eso es justo lo que no puede
    /// pasar.
    ///
    /// Asi que esto es un puente de UNA SOLA DIRECCION, y se dice para que
    /// nadie lo confunda: la clave se HEREDA si Mac31 concede este boton, pero
    /// el boton que el web escribe sigue siendo el canonico del catalogo.
    ///
    /// EL CASO QUE LA OBLIGO A EXISTIR, medido el 2026-10-12 en las dos bases:
    /// el web ya tiene casi todas las acciones de estas pantallas, pero las
    /// protege con la clave que produce OTRO boton de la misma forma. Quien en
    /// Mac31 tiene solo el segundo boton podia hacer la operacion alla y no
    /// aca. Ejemplos reales: NORMA y RICARDO tienen btnPagosCascos de la hoja
    /// de cobro y no btnPagos de consulta de ventas, asi que no heredaban
    /// "ventas.pagos.ver"; REBECA tiene btnNuevaNotaCredito y no btnFacturar,
    /// asi que no heredaba "facturacion.timbrar"; JESY tiene los seis botones
    /// del flujo de garantias en Tauro y no btnModificar, asi que no heredaba
    /// "garantias.editar".
    ///
    /// NO INVENTA CLAVES: todas las de aqui existen ya en el catalogo y las
    /// exige un endpoint. Lo unico que cambia es DE QUE BOTON se heredan.
    /// </param>
    private sealed record LegacyModuleBinding(
        string ModuleKey,
        string ModuleName,
        string LegacyForm,
        IReadOnlyDictionary<string, (string Key, string Name)> Map,
        IReadOnlyList<PermissionSeed> WebSeeds,
        string? FormaExacta = null,
        string? Prefijo = null,
        string? ClaveDeEntrada = null,
        string? ClaveDeLectura = null,
        IReadOnlyDictionary<string, string>? ConcesionesExtra = null);

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

    /*
      EL BOTON GUARDAR DE USADOS VIVE EN UNA TERCERA FORMA DE MAC31.

      "ventas.usados" (btnUsados, en CONSULTA DE VENTAS) es el que deja ABRIR la
      pantalla de usados, y ya estaba arriba. Pero Mac31 tiene un permiso APARTE
      para el boton GUARDAR de esa pantalla, y vive en su propia forma. Hasta
      ahora el backend no lo usaba: quien podia abrir la pantalla podia guardar.

      VentasUsadosController lo tenia anotado como pendiente, con el motivo de
      que "el puente de permisos heredados esta amarrado a UNA forma por modulo
      y agregar una segunda obligaria a tocar el mecanismo entero". Ya no es
      cierto: el puente se parametrizo cuando se hizo el de pagos
      (LegacyModuleBinding), y esta es la tercera forma del mismo modulo, exacta-
      mente como la segunda. No hay mecanismo que tocar.

      Verificado en las DOS empresas y son identicas hasta el numero: IDForma
      4024, Forma "VentasUsados", Descripcion "CONSULTA DE VENTAS USADOS", con un
      solo proceso, btnGuardar (5085), el mismo IDProceso en Produccion_svr
      (Tauro) y en MacZ (Zaragoza).

      NO HAY CHOQUE DE IDProceso con las otras dos formas de ventas: la de
      consulta usa 1..18, 1031, 3063, 5096, 5151-5154, 5196 y 53xx; la de pagos
      1032 y 1033; esta, 5085. Importa porque el catalogo se indexa por
      IDProceso.

      NO LLEVA WebSeeds NI SU PROPIO ver_modulo, por lo mismo que pagos: el
      modulo es el MISMO ("ventas") y su ".ver_modulo" ya lo siembra
      VentasWebPermissionSeeds.
    */
    private const string VentasUsadosLegacyForm = "CONSULTA DE VENTAS USADOS";

    public const string UsadosGuardarPermission = "ventas.usados.guardar";

    private static readonly Dictionary<string, (string Key, string Name)> VentasUsadosLegacyPermissionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnGuardar"] = (UsadosGuardarPermission, "Ventas - Guardar usados")
    };

    private static readonly LegacyModuleBinding VentasUsadosBinding = new(
        VentasModuleKey, "Ventas", VentasUsadosLegacyForm, VentasUsadosLegacyPermissionMap, new List<PermissionSeed>());

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

    /*
      DOS MEMOS PARA PREGUNTAS QUE NO CAMBIAN DENTRO DE UNA PETICION.

      Medido con los DMV: un solo `/api/permisos-web/bootstrap` hacia 5383
      viajes a SQL, y 4160 de ellos eran estas dos preguntas repetidas (2080
      cada una). No son consultas lentas: son consultas TRIVIALES preguntadas
      miles de veces, una por usuario y por familia dentro de los bucles.

      En local no se notaba porque el motor esta en la misma maquina y cada
      viaje vale menos de un milisegundo. En produccion la API corre en el VPS y
      la base esta en otra maquina POR INTERNET: a ~50 ms el viaje, eso son 4.5
      minutos de espera. Por eso la pantalla de Usuarios tardaba 4 min 25 s y
      aqui nadie lo veia.

      POR QUE SE PUEDE GUARDAR CADA UNA:

      - EL ESQUEMA (que tabla y que columna existen) no cambia mientras el
        proceso corre: cambiarlo pide un despliegue. Aun asi caduca a los 15
        minutos, el mismo plazo que ya usaba `SchemaCacheTtl`, para que un
        cambio en caliente entre solo.

      - EL IDUSUARIO DE MAC31 de un usuario web se resuelve emparejando nombres
        (ver ResolveLegacyUserIdAsync). Cambia si alguien renombra o da de alta
        al usuario en Mac31, y entonces tarda como mucho lo que el permiso ya
        tardaba en refrescarse: se usa `ActiveCacheTtl`, el MISMO plazo que el
        propio snapshot de permisos. No se introduce ninguna clase de desfase
        que no existiera ya.

      Lo que NO se guarda aqui es ningun permiso: eso sigue como estaba.
    */
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (bool Existe, DateTime Hasta)>
        _memoDeEsquema = new(StringComparer.OrdinalIgnoreCase);



    /*
      EL CATALOGO DE MAC31, MEMORIZADO POR EMPRESA.

      Las rutinas `Ensure*SyncedAsync` hacen dos cosas: leen de Mac31 que formas,
      procesos y reportes existen, y los SINCRONIZAN contra WPermiso y WModulo.
      Lo segundo son escrituras: medido, 358 `UPDATE WPermiso` y 130
      `UPDATE WModulo` en UNA consulta de permisos. Una lectura que reescribe el
      catalogo entero, que es lo que CUIDADO-consultas-que-escriben.md prohibe.

      Y no hacia falta ninguna de las dos veces: el catalogo solo cambia si
      alguien agrega una forma o un reporte en Mac31, y de eso se encarga el
      sembrado del ARRANQUE (CatalogoDePermisosHostedService).

      Se guarda el RESULTADO por empresa y familia durante
      `CatalogoCacheTtl`. Consecuencia que hay que saber: una forma nueva de
      Mac31 tarda hasta ese plazo en aparecer en la pantalla de permisos, o lo
      que tarde el siguiente arranque. A cambio, la pantalla de Usuarios deja de
      tardar minutos.

      NO GUARDA PERMISOS DE NADIE: es el catalogo de que claves EXISTEN, no de
      quien las tiene. Lo que cada usuario puede sigue leyendose siempre.
    */
    /*
      LO QUE SE RECUERDA DURANTE **UNA** PETICION.

      Vive en un `AsyncLocal`, asi que acompana a la peticion por todo su arbol
      de `await` y muere con ella. Dos peticiones no comparten nada: la segunda
      vuelve a leer de la base, igual que antes de optimizar.

      Se probo con caducidad por tiempo (5 y 10 minutos) y las pruebas de
      permisos contra la base lo cazaron: quitar un boton en Mac31 y seguir
      viendo el permiso. Un cache que sobrevive a una revocacion no es una
      optimizacion, es un agujero.
    */
    private static readonly AsyncLocal<Dictionary<string, object>?> _memoDeLaPeticion = new();

    /// <summary>Abre el alcance de memoria de una peticion. Se cierra al soltarlo.</summary>
    private static IDisposable AbrirMemoriaDePeticion()
    {
        /* Si ya hay una abierta mas arriba, esta no la toca ni al cerrarse: el
           dueno es quien la creo. Sin esto, una llamada anidada vaciaria la
           memoria del que la envuelve justo a la mitad. */
        if (_memoDeLaPeticion.Value is not null) return new CierreDeMemoria(false);

        _memoDeLaPeticion.Value = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        return new CierreDeMemoria(true);
    }

    private sealed class CierreDeMemoria : IDisposable
    {
        private readonly bool _esMia;
        public CierreDeMemoria(bool esMia) => _esMia = esMia;
        public void Dispose() { if (_esMia) _memoDeLaPeticion.Value = null; }
    }

    /*
      EL CATALOGO SI SE RECUERDA ENTRE PETICIONES. LO DE CADA USUARIO NO.

      Son dos cosas distintas y se tratan distinto a proposito:

      - EL CATALOGO es "que claves EXISTEN", leido de las formas y procesos de
        Mac31. Es de la empresa, igual para todos, y solo cambia si alguien da
        de alta una forma o un reporte nuevo. Recordarlo unos minutos NO puede
        ensenar un permiso revocado, porque no dice quien tiene que.

      - LO QUE CADA USUARIO TIENE CONCEDIDO vive en el memo de la PETICION
        (_memoDeLaPeticion) y muere con ella. Aqui si estaria el riesgo: una
        prueba quita un boton en Mac31 y comprueba que el permiso desaparece —y
        lo cazo cuando esto duraba minutos—.

      El plazo es corto a proposito: una forma nueva de Mac31 tarda eso en
      salir en la pantalla de permisos, y el arranque la siembra igualmente.
    */
    private static readonly TimeSpan CatalogoCacheTtl = TimeSpan.FromMinutes(10);

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (object Valor, DateTime Hasta)>
        _memoDeCatalogo = new(StringComparer.OrdinalIgnoreCase);

    /*
      Y CUANDO VENCE, LO RECONSTRUYE UNO SOLO.

      La llave de este memo es de la EMPRESA, no del usuario, asi que vence para
      todos en el mismo instante. Sin candado, la rafaga que caia justo despues
      del vencimiento reconstruia el catalogo entero una vez por peticion: ~230
      viajes a SQL cada una, a 56 ms el viaje. Es el acantilado que explicaba
      las esperas de 11-27 s en los registros de IIS.

      El candado no cambia que se recuerda ni cuanto: solo evita hacerlo nueve
      veces. Ver Infrastructure/Concurrencia/UnaSolaVez.cs.
    */
    /*
      QUIEN REFRESCA DE VERDAD ES EL SERVICIO DE FONDO.

      Mientras esto vale `true`, `CatalogoMemorizadoAsync` reconstruye aunque
      tenga algo guardado. Lo pone `CatalogoDePermisosHostedService`, que tiene
      su propia conexion y puede tardar lo que haga falta sin que nadie espere.

      Es `AsyncLocal` para que solo afecte a la llamada del refrescador: una
      peticion normal que coincida en el tiempo sigue sirviendose de lo
      guardado.
    */
    private static readonly AsyncLocal<bool> _modoRefresco = new();

    internal static IDisposable AbrirRefrescoDeCatalogo()
    {
        _modoRefresco.Value = true;
        return new CierreDeRefresco();
    }

    private sealed class CierreDeRefresco : IDisposable
    {
        public void Dispose() => _modoRefresco.Value = false;
    }

    private static Task<T> CatalogoMemorizadoAsync<T>(
        string llave, Func<Task<T>> construir) where T : class
    {
        var hay = _memoDeCatalogo.TryGetValue(llave, out var guardado) && guardado.Valor is T;

        /*
          HAY ALGO GUARDADO: se devuelve, vigente o no.

          Esto es lo que quita el acantilado. El plazo vence para toda la
          empresa a la vez, asi que antes el primero que llegaba despues pagaba
          una reconstruccion de ~507 viajes a SQL -~28 s a 56 ms- y los demas se
          quedaban esperandolo. Ese era el pico de 11-36 s de los registros.

          POR QUE SE PUEDE SERVIR LO VENCIDO. Aqui vive QUE CLAVES EXISTEN en la
          empresa, no quien las tiene concedidas. Un catalogo con unos minutos
          de mas no puede ensenar un permiso revocado ni esconder uno dado: lo
          que cada usuario puede se lee SIEMPRE, en cada peticion. Lo unico que
          se retrasa es que una forma NUEVA de Mac31 aparezca en la pantalla de
          permisos, y de eso se encarga el refrescador de fondo.
        */
        if (hay && !_modoRefresco.Value)
            return Task.FromResult((T)guardado.Valor);

        /*
          NO HAY NADA -primer arranque- o es el refrescador quien pregunta.
          Entonces si se construye, con el candado para que lo haga uno solo.
        */
        return Concurrencia.UnaSolaVez.HazloAsync(
            llave + (_modoRefresco.Value ? "|refresco" : string.Empty),
            () => !_modoRefresco.Value
                  && _memoDeCatalogo.TryGetValue(llave, out var g)
                  && g.Valor is T vigente
                      ? (true, vigente)
                      : (false, null),
            valor => _memoDeCatalogo[llave] = (valor, DateTime.UtcNow.Add(CatalogoCacheTtl)),
            construir);
    }

    /// <summary>Un renglon de n_Procesos con lo que Mac31 le concede a un usuario.</summary>
    private sealed record ProcesoDeUsuario(
        string Forma, string FormaDescripcion, int IdProceso,
        string Proceso, string? ProcesoDescripcion, bool Permitido);

    /*
      TODO LO QUE MAC31 LE CONCEDE A UN USUARIO, EN UNA SOLA CONSULTA.

      Antes se preguntaba una vez POR FAMILIA: la misma consulta con otro filtro
      de forma, 71 veces por usuario. En el bootstrap, que recorre los 31
      usuarios, eso eran cientos de viajes para traer una tabla que cabe de
      sobra en una.

      Se guarda por usuario durante `ActiveCacheTtl`, el MISMO plazo que ya usa
      el snapshot de permisos: no se agrega ninguna clase de desfase que no
      existiera. Y cuando el web escribe permisos en Mac31 se olvida en el acto
      porque el memo no sobrevive a la peticion que lo creo.
    */
    private static async Task<List<ProcesoDeUsuario>> ProcesosDeUsuarioAsync(
        SqlConnection conn, int legacyUserId, CancellationToken ct)
    {
        var llave = $"procesos|{conn.Database}|{legacyUserId}";
        var memoria = _memoDeLaPeticion.Value;
        if (memoria is not null && memoria.TryGetValue(llave, out var guardado)
            && guardado is List<ProcesoDeUsuario> vigente)
        {
            return vigente;
        }

        var filas = new List<ProcesoDeUsuario>();

        await using (var cmd = new SqlCommand(@"
SELECT f.Forma,
       UPPER(LTRIM(RTRIM(f.Descripcion))) AS FormaDescripcion,
       p.IDProceso,
       p.Proceso,
       p.Descripcion AS ProcesoDescripcion,
       CASE WHEN ufp.IDStatus = 1 THEN 1 ELSE 0 END AS Permitido
FROM dbo.n_Formas f
INNER JOIN dbo.n_Procesos p
    ON p.IDForma = f.IDForma
LEFT JOIN dbo.[Usuario Forma Procesos] ufp
    ON ufp.IDProceso = p.IDProceso
   AND ufp.IDUsuario = @IDUsuario
WHERE ISNULL(p.IDStatus, 1) = 1;", conn))
        {
            cmd.Parameters.Add(new SqlParameter("@IDUsuario", SqlDbType.Int) { Value = legacyUserId });
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                filas.Add(new ProcesoDeUsuario(
                    Forma: reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                    FormaDescripcion: reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    IdProceso: reader.GetInt32(2),
                    Proceso: reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    ProcesoDescripcion: reader.IsDBNull(4) ? null : reader.GetString(4),
                    Permitido: reader.GetInt32(5) == 1));
            }
        }

        if (memoria is not null) memoria[llave] = filas;
        return filas;
    }




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

        /*
          LAS TRES PANTALLAS DE ACCESOS Y PERMISOS. ERA EL CANDADO QUE FALTABA.

          `IsAllowedByLegacy` acaba en `return true`: lo que no esta en este
          mapa PASA. Eso es deliberado y tiene que seguir siendo asi —un modulo
          nuevo no puede quedarse inaccesible en una instalacion sin el modelo
          de capacidades—, pero deja estas llaves abiertas, y no son un modulo
          de negocio cualquiera: son las que reparten los permisos de todos.

          El camino es este, y solo se recorre con PermissionsEnabled = false
          (PermissionAuthorizationHandler, el `else` del snapshot). Tres cosas
          lo encienden: que falten las tablas de capacidades, que la bandera
          'autorizacion.capacidades' de la empresa este en 0, y —la que
          preocupa— CUALQUIER excepcion al construir el snapshot sin copia en
          cache. O sea: un hipo de SQL y, antes de esto, cualquier usuario
          autenticado podia llamar

              PUT /api/permisos-web/modulos/{clave}/status

          y prender o apagar los modulos de la empresa entera. Sin un solo
          permiso. Medido leyendo el codigo, no con una peticion: por eso se
          cierra en vez de dejarlo anotado.

          `permisosweb.*` ya estaba cubierto arriba; estas son sus hermanas,
          con el mismo reparto para que las cinco digan lo mismo: ver con
          Admin, cambiar solo SuperAdmin.

          Lo que NO se toca: el `return true` del final y la exclusion de
          `app_movil.*` de la puerta de entrada (EntradasDeModulo). Ninguna de
          estas llaves es del canal movil.
        */
        ["permisos_roles.ver_modulo"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["permisos_roles.crear"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin" },
        ["permisos_roles.editar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin" },
        ["permisos_modulos.ver_modulo"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["permisos_modulos.ver"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["permisos_modulos.crear"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin" },
        ["permisos_modulos.editar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin" },
        ["permisos_modulos.activar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin" },
        ["permisos_usuarios.ver_modulo"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["permisos_usuarios.editar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin" },

        /*
          Y LA LLAVE DEL SUELDO, por la misma razon y en el mismo sitio.

          'empleados.sueldo.ver' tampoco estaba en el mapa, asi que en modo de
          respaldo cualquiera veia los importes — y ahora, con el candado de
          escritura puesto en EmpleadosService, tambien podria fijarlos. Se le
          da el reparto mas cerrado que hay: solo SuperAdmin, porque en Mac31
          ver sueldos no es un permiso de rol sino una contrasena, y no hay un
          rol legacy del que se pueda deducir honestamente que si.
        */
        ["empleados.sueldo.ver"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin" },
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
        ["cheques.activar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },

        /*
          El reparto por rol para las claves nuevas de cascos, temporadas y
          torneos. Esto SOLO manda en instalaciones sin el modelo de capacidades
          (PermissionsEnabled = false): donde si lo hay, decide la base.

          Se copia el reparto de los modulos hermanos —ver con "User", escribir
          solo "Admin"— porque es la regla que ya rige en todo el catalogo y
          porque es la decision conservadora: antes de este cambio cualquier
          token escribia; ahora, en una base sin capacidades, al menos hace
          falta ser Admin.
        */
        ["almacen_cascos.ver_modulo"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin", "User" },
        ["almacen_cascos.ver"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin", "User" },
        ["almacen_cascos.crear"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["almacen_cascos.editar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["almacen_cascos.cancelar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },

        ["temporadas.ver_modulo"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin", "User" },
        ["temporadas.ver"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin", "User" },
        ["temporadas.crear"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["temporadas.editar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["temporadas.cancelar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },

        ["torneos.ver_modulo"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin", "User" },
        ["torneos.ver"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin", "User" },
        ["torneos.crear"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["torneos.editar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["torneos.cancelar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" }
    };

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PermissionService> _logger;
    private readonly IHostEnvironment _hostEnvironment;
    /*
      QUITAR UN PERMISO NO QUITABA NADA, Y HASTA MEDIA HORA DESPUES.

      Lo que habia aqui era `private readonly ConcurrentDictionary<string, byte>
      _observedUserCacheKeys` —de INSTANCIA— y de el colgaba toda la
      invalidacion. PermissionService es Scoped: una instancia nueva por
      peticion, asi que ese diccionario nace VACIO cada vez. La IMemoryCache, en
      cambio, es Singleton y sobrevive a todas.

      Resultado: cuando un administrador cambiaba los permisos de un rol,
      InvalidateEmpresaPermissionCache recorria un diccionario que como mucho
      tenia la clave del propio administrador. No quitaba NI UNA clave de los
      demas. Quien ya tenia el permiso lo conservaba 5 minutos (TTL activo) y
      hasta 30 por el camino "stale" si la base fallaba al reconstruir. Y al
      contrario: conceder un permiso tampoco surtia efecto a tiempo.

      Encima, InvalidateUserPermissionCache compensaba con una lista fija de seis
      nombres de rol ("SuperAdmin", "Admin", "User"...). La clave de cache
      incluye el RolCodigo del tenant, asi que cualquier rol propio fuera de esa
      lista no se invalidaba NUNCA.

      COMO SE ARREGLA: con tokens de expiracion, no con una lista de claves. Cada
      renglon de cache se cuelga de DOS CancellationTokenSource compartidos (uno
      del usuario, uno de la empresa) que viven en este diccionario ESTATICO —
      estatico para igualar la vida de la IMemoryCache, igual que _candados de
      arriba. Invalidar es cancelar el token: la IMemoryCache tira de golpe todos
      los renglones colgados de el, sin importar de que peticion vinieran ni con
      que rol se guardaron, y sin tener que adivinar los nombres de rol.
    */
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> _invalidadores =
        new(StringComparer.Ordinal);

    private static string AmbitoEmpresa(int empresaId) => $"perm:{empresaId}";

    private static string AmbitoUsuario(Guid userId, int empresaId) => $"perm:{empresaId}:{userId:N}";

    private static CancellationTokenSource Invalidador(string ambito)
        => _invalidadores.GetOrAdd(ambito, _ => new CancellationTokenSource());

    /// <summary>
    /// Tira TODOS los renglones de cache colgados de ese ambito, en toda la
    /// aplicacion y para cualquier rol. El CancellationTokenSource no se libera
    /// a proposito: todavia hay renglones apuntando a su token y liberarlo haria
    /// que la IMemoryCache reventara al consultarlo. Cancelado y sin referencias
    /// lo recoge el GC solo.
    /// </summary>
    private static void DispararInvalidacion(string ambito)
    {
        if (_invalidadores.TryRemove(ambito, out var cts))
            cts.Cancel();
    }

    /// <summary>
    /// Las opciones con las que se guarda un renglon de permisos: su vigencia
    /// normal, mas los dos tokens que permiten tirarlo antes.
    /// </summary>
    private static MemoryCacheEntryOptions OpcionesDeCache(Guid userId, int empresaId, TimeSpan vigencia)
    {
        var opciones = new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = vigencia };
        opciones.AddExpirationToken(new CancellationChangeToken(Invalidador(AmbitoEmpresa(empresaId)).Token));
        opciones.AddExpirationToken(new CancellationChangeToken(Invalidador(AmbitoUsuario(userId, empresaId)).Token));
        return opciones;
    }

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
        // La exige UsuariosController para enseñar la contraseña de Mac31 en
        // claro. Estaba en el codigo y NO en esta lista, asi que nunca llegaba a
        // WPermiso: nadie salvo el SuperAdmin podia tenerla.
        new("usuarios.password.ver", "Usuarios - Ver contraseña de Mac31", "usuarios"),
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

        /*
          CONFIGURACION DE ROL DE TORNEO: la gemela de generacionroltorneo, y la
          unica de las dos que no estaba sembrada. Sus claves se exigen en
          ConfiguracionRolTorneoController:26-123 y no existian en WPermiso de
          NINGUNA de las dos bases, asi que la pantalla contestaba 403 a todo el
          mundo menos al SuperAdmin. Lo encontro la prueba
          ClavesDePermisoSembradasTests, no una revision a mano.
        */
        new("configuracionroltorneo.ver_modulo", "Configuración Rol Torneo - Ver módulo", "configuracionroltorneo"),
        new("configuracionroltorneo.ver", "Configuración Rol Torneo - Ver", "configuracionroltorneo"),
        new("configuracionroltorneo.crear", "Configuración Rol Torneo - Crear", "configuracionroltorneo"),
        new("configuracionroltorneo.editar", "Configuración Rol Torneo - Editar", "configuracionroltorneo"),
        new("configuracionroltorneo.activar", "Configuración Rol Torneo - Activar/Inactivar", "configuracionroltorneo"),

        /*
          EL TABLERO DE VENTAS COMPLETO COLGABA DE UNA CLAVE QUE NO EXISTIA.
          Los 12 endpoints de DashboardVentasController exigen `dashboardventas.ver`
          y esa fila no estaba en WPermiso ni en Tauro ni en Zaragoza: el tablero
          era visible solo para el SuperAdmin, en las dos empresas.
        */
        new("dashboardventas.ver_modulo", "Tablero de Ventas - Ver módulo", "dashboardventas"),
        new("dashboardventas.ver", "Tablero de Ventas - Consultar", "dashboardventas"),

        /*
          TARIMAS: cinco claves exigidas en TarimasController y cero filas en las
          dos bases.
        */
        new("tarimas.ver_modulo", "Tarimas - Ver módulo", "tarimas"),
        new("tarimas.ver", "Tarimas - Ver", "tarimas"),
        new("tarimas.crear", "Tarimas - Crear", "tarimas"),
        new("tarimas.editar", "Tarimas - Editar", "tarimas"),
        new("tarimas.estado.editar", "Tarimas - Activar/Inactivar", "tarimas"),

        /*
          LAS PANTALLAS DE PERMISOS DEL WEB. Estas si estaban en WPermiso de Tauro
          —script a mano— y no en la lista, asi que en una empresa nueva la
          pantalla que reparte permisos es la primera que no se puede abrir.
          `permisos_usuarios.*` se incluye por lo mismo aunque hoy nadie la exija
          en un [Authorize]: es la tercera hermana y WModulo ya la contempla.
        */
        new("permisos_roles.ver_modulo", "Permisos - Roles: Ver módulo", "permisos_roles"),
        new("permisos_roles.crear", "Permisos - Roles: Crear", "permisos_roles"),
        new("permisos_roles.editar", "Permisos - Roles: Editar", "permisos_roles"),
        new("permisos_modulos.ver_modulo", "Permisos - Módulos: Ver módulo", "permisos_modulos"),
        new("permisos_modulos.ver", "Permisos - Módulos: Ver", "permisos_modulos"),
        new("permisos_modulos.crear", "Permisos - Módulos: Crear", "permisos_modulos"),
        new("permisos_modulos.editar", "Permisos - Módulos: Editar", "permisos_modulos"),
        new("permisos_modulos.activar", "Permisos - Módulos: Prender/Apagar", "permisos_modulos"),
        new("permisos_usuarios.ver_modulo", "Permisos - Usuarios: Ver módulo", "permisos_usuarios"),
        new("permisos_usuarios.editar", "Permisos - Usuarios: Editar", "permisos_usuarios"),

        new("pagosproveedores.ver", "Pagos Proveedores - Ver", "pagosproveedores"),
        new("pagosproveedores.crear", "Pagos Proveedores - Crear", "pagosproveedores"),
        new("pagosproveedores.editar", "Pagos Proveedores - Modificar", "pagosproveedores"),
        new("pagosproveedores.cancelar", "Pagos Proveedores - Cancelar", "pagosproveedores"),
        new("cheques.ver_modulo", "Cheques - Ver módulo", "cheques"),
        new("cheques.ver", "Cheques - Ver", "cheques"),
        new("cheques.crear", "Cheques - Crear", "cheques"),
        new("cheques.editar", "Cheques - Editar", "cheques"),
        new("cheques.activar", "Cheques - Cambiar estatus", "cheques"),

        /*
          PERMISOS DE LOS CATALOGOS DE SOLO LECTURA (ola 1 de la migracion de MacServicios2).

          Son cinco y no uno porque el contenido no es igual de inocente:
          * `catalogos.ver` es el desplegable de toda la vida (estados, ciudades, puestos,
            unidades de medida...). Lo necesita cualquiera que capture algo.
          * `catalogos.fe.ver` son los catalogos del SAT: solo los ve quien factura.
          * `catalogos.galleros.ver` se separa porque el renglon de gallero trae su SALDO,
            y el dinero no se reparte con el resto de los desplegables.
          * `catalogos.recaudaciones.ver` es cajas, conceptos y auxiliares por economico:
            es el modulo de recaudaciones, que no todas las empresas tienen.
          * `catalogos.formas.ver` es el inventario de pantallas de Mac31. Quien lo consulta
            esta administrando seguridad; no se mezcla con los desplegables de captura.
          * `catalogos.marcas.ver` es la relacion categoria-marca del catalogo de productos.

          No se reuso `ventas.consultar` ni `permisosweb.bootstrap`, que estaban a mano: el
          primero lo manda Mac31 (seccion del puente con legacy) y darlo aqui lo borraria en el
          siguiente calculo; el segundo significa "administrar permisos", que no es esto.
          La familia `catalogos.*` es del WEB, no esta en la lista de las cuatro familias que
          gobierna Mac31, asi que lo que se de desde la pantalla de permisos si se respeta.
        */
        /*
          LA LLAVE DEL MODULO DE CATALOGOS.

          Faltaba, y sin ella el modulo de Catalogos del web no puede existir:
          `<modulo>.ver_modulo` es la clave con la que el inicio decide si pinta
          la tarjeta y con la que la pantalla decide si abre. Las otras doce
          claves de la familia dicen que se puede HACER adentro; ninguna dice si
          se puede ENTRAR, y usar `catalogos.ver` para las dos cosas obliga a dar
          el desplegable de estados a quien solo deberia poder abrir la pantalla
          (o al contrario, a cerrarle la pantalla a quien si captura).
        */
        new("catalogos.ver_modulo", "Catálogos - Ver módulo", "catalogos"),
        new("catalogos.ver", "Catálogos - Consultar", "catalogos"),
        new("catalogos.fe.ver", "Catálogos - Facturación electrónica", "catalogos"),
        new("catalogos.galleros.ver", "Catálogos - Galleros y su saldo", "catalogos"),
        new("catalogos.recaudaciones.ver", "Catálogos - Recaudaciones", "catalogos"),
        new("catalogos.formas.ver", "Catálogos - Formas de Mac31", "catalogos"),
        new("catalogos.marcas.ver", "Catálogos - Categorías y marcas", "catalogos"),

        /*
          LAS DOS LLAVES DE LAS CONSULTAS DE VENTAS QUE SE MIGRARON DE MacServicios2.

          POR QUE NO VAN EN LA FAMILIA `ventas.*`
          Porque para `ventas.*` manda Mac31: al construir el snapshot se BORRA
          del resultado todo lo que empiece por "ventas." y no este en
          VentasWebPermissionSeeds, y se vuelve a calcular desde los botones de la
          forma CONSULTA DE VENTAS. Una clave nueva ahi se guardaria en
          WUsuarioPermiso y desapareceria en el siguiente calculo, sin error y sin
          rastro — la misma cicatriz de "pendientes de autorizar".

          POR QUE NO SE REUSO `ventas.consultar`
          Es btnBuscar de la pantalla Consultar ventas. La cobranza son TRES
          pantallas distintas de Mac31 (Cobranza, Concentrado y Cobro) y el
          catalogo de descuentos es otra. Reusarlo ataria cosas que la gente
          necesita por separado: hay quien cobra y no consulta ventas.

          POR QUE NO SE AMARRARON AL PUENTE DE PERMISOS DE LEGACY
          El puente (`LegacyModuleBinding`) va por UNA forma por modulo, y la
          cobranza son tres. Amarrarlo obligaria a cambiar el puente, que es la
          pieza que mas rompe de todo este archivo.

          Asi que son del WEB, en familias nuevas, y por eso lo que se da desde
          la pantalla de permisos SI se respeta. Quedan sembradas en codigo: no
          se corrio ningun script contra la base; entran a WPermiso cuando
          alguien sincronice el catalogo desde la pantalla de permisos.
        */
        new("cobranza.ver", "Cobranza - Consultar cobros, cobranzas y concentrados", "cobranza"),
        // Un descuento es DINERO (el porcentaje que se le aplica a un cliente) y
        // su bitacora dice quien lo movio: no es un desplegable de captura, asi
        // que no cabe en `catalogos.ver` de la ola anterior.
        new("descuentos.ver", "Descuentos - Consultar catálogo y bitácora", "descuentos"),

        /*
          OLA 3 — LAS LECTURAS QUE NO ERAN CATALOGOS NI VENTAS.

          Todas estas familias son del WEB: ninguna empieza con `ventas.`,
          `reportes.`, `empleados.` ni `prestamos.`, que son las cuatro que
          gobierna Mac31 y donde lo que se guarda desde la pantalla de permisos se
          borra en el siguiente recalculo (ver la seccion del puente, mas arriba).
          O sea: lo que se de aqui desde el web SI se respeta.

          Dos endpoints de esta ola NO traen clave nueva a proposito:
          * GET api/huellas usa `empleados.huellas`, que ya existe y la deriva el
            puente del boton btnHuellas de Mac31. Inventar `huellas.ver` habria
            creado una llave que se borra sola al dia siguiente.
          * GET api/asistencias/diaria destapa los nombres con
            `empleados.sueldo.ver`, que ya esta en la lista blanca del web.

          Y los tres `catalogos.*` que usa CatalogosComplementosController
          (`catalogos.ver`, `catalogos.fe.ver`, `catalogos.marcas.ver`) tampoco se
          declaran aqui: los siembra la entrega de catalogos. Dos filas con la
          misma clave serian dos duenos de la misma llave.
        */

        // Obra publica (las tablas MC_* de Mac31). Modulo que la mayoria de las
        // empresas no tiene prendido (Constantes.EsAlcaldiaMC).
        new("obra.ver_modulo", "Obra - Ver módulo", "obra"),
        new("obra.contratos.ver", "Obra - Ver contratos, conceptos y estimaciones", "obra"),
        new("obra.catalogos.ver", "Obra - Ver catálogos", "obra"),
        // Separado de los catalogos porque el renglon trae RFC, CURP, teléfono y
        // correo: es un dato personal, no un desplegable.
        new("obra.proveedores.ver", "Obra - Ver proveedores", "obra"),

        // Ficha del cliente. NO se reusa `ventas.cliente.ver`: esa clave
        // significa "ver a quién le estoy vendiendo mientras capturo un pedido",
        // y aquí se publican datos fiscales y una bitácora de quién cambió qué.
        new("clientes.ver_modulo", "Clientes - Ver módulo", "clientes"),
        new("clientes.ver", "Clientes - Ver bonificaciones y centros de servicio", "clientes"),
        // Aparte porque es dinero: dice qué bonificación y qué RFC se le factura.
        new("clientes.facturacion.ver", "Clientes - Ver datos de facturación", "clientes"),
        // Aparte porque es auditoría: quién cambió el límite de crédito y cuándo.
        // Quien atiende al cliente no necesita verlo; quien revisa, sí.
        new("clientes.log.ver", "Clientes - Ver bitácora de cambios", "clientes"),

        // Catálogo de productos CON precios y existencias. No cae en
        // `catalogos.*` porque el renglón trae precio de lista, precio anterior y
        // la diferencia entre los dos: es el margen de la empresa.
        new("productos.ver_modulo", "Productos - Ver módulo", "productos"),

        /*
          LAS LLAVES DE MODULO QUE FALTABAN, Y POR QUE SIN ELLAS NO EXISTE NADA.

          `/api/modulos/disponibles` filtra por `{clave}.ver_modulo`. Si esa clave
          no esta sembrada, el modulo NO SE PUEDE OFRECER por mucho que su
          pantalla exista, su renglon este en `dbo.WModulo` y el permiso se le
          asigne a un rol: la tarjeta nunca sale en el Inicio y a la pantalla solo
          se llega escribiendo la direccion.

          Se vio midiendo: tras dar de alta los quince modulos del front nuevo, el
          API seguia publicando seis. Los otros diez no tenian esta llave.

          Es la misma forma del fallo que dejo 68 capacidades contestando 403:
          migradas y muertas a la vez porque su clave no existia.
        */
        new("inventario_ajustes.ver_modulo", "Inventario - Ajustes: ver módulo", "inventario"),
        new("inventario_transito.ver_modulo", "Inventario - En tránsito: ver módulo", "inventario"),

        new("cobros.ver_modulo", "Cobros - Ver módulo (hojas de cobro)", "cobros"),
        new("dinero_usados.ver_modulo", "Cobros - Dinero de usados: ver módulo", "cobros"),
        /*
          CONCENTRADOS ES UN MODULO APARTE, Y NO UNA VISTA DE COBROS.

          En Mac31 son dos botones distintos del menu —`btnConcentrados`
          (`Menu.cs:1785`, abre ConsultarConcentrados) y `btnCobrosDineroCascos`
          (`Menu.cs:2690`, abre ConsultarDineroUsadosCobros)— y dos formas
          distintas, asi que llevan dos llaves de mosaico.

          LA FAMILIA ES "cobros" Y NO "concentrados": el modulo de permisos
          agrupa las pantallas de la cobranza, y los permisos de ACCION de esta
          pantalla ya existen y viven en `cobranza.concentrados.{editar,
          cancelar}`. Inventar una familia `concentrados.*` partiria en dos lo
          que en la pantalla de Permisos se reparte junto.
        */
        new("concentrados.ver_modulo", "Concentrados - Ver módulo", "cobros"),

        new("garantias.ver_modulo", "Garantías - Ver módulo", "garantias"),
        new("cambios_vigencia.ver_modulo", "Cambios de vigencia - Ver módulo", "cambiosvigencia"),
        new("liquidaciones.ver_modulo", "Liquidaciones de descuento - Ver módulo", "descuentos"),

        /*
          `venta` es VENDER y `ventas` es "Pendientes de autorizar": dos modulos
          distintos cuyas claves se parecen. No se tocan.
        */
        new("venta.ver_modulo", "Vender - Ver módulo", "ventas"),
        new("facturas.ver_modulo", "Facturas - Ver módulo (consulta)", "facturacion"),
        new("reasignacion.ver_modulo", "Reasignación de remisiones - Ver módulo", "ventas"),
        new("productos.ver", "Productos - Ver catálogo con precios", "productos"),

        // Cortes de caja. `.cs` aparte: los de centro de servicio cruzan a las
        // bases de los centros y los ve otra gente (la matriz, no el centro).
        new("cortes.ver_modulo", "Cortes - Ver módulo", "cortes"),
        new("cortes.ver", "Cortes - Ver corte y su detalle", "cortes"),
        new("cortes.cs.ver", "Cortes - Ver cortes de centros de servicio", "cortes"),

        // Manifiesto de actualización de las aplicaciones de escritorio. Publica
        // rutas del servidor, así que es de quien administra la instalación.
        new("aplicaciones.actualizaciones.ver", "Aplicaciones - Ver actualizaciones", "aplicaciones"),

        /*
          ALMACEN DE CASCOS, TEMPORADAS Y TORNEOS.

          Las once escrituras de estos tres modulos estaban SOLO con [Authorize]
          de clase: cualquier usuario con token, de cualquier rol, podia crear,
          modificar y cancelar movimientos de casco, y abrir, cerrar y cancelar
          torneos y temporadas.

          LAS CLAVES NO SE INVENTAN AQUI: YA LAS PIDE EL FRONT.
          ----------------------------------------------------
          Estan escritas desde hace tiempo en
          Web/QR_Web/docs/shared/js/core/module-action-flags.js, que es con lo
          que el web decide si enciende o apaga cada boton:

            almacen_cascos: ver_modulo, ver, crear, editar, cancelar
            temporadas:     ver_modulo, ver, crear, editar, cancelar
            torneos:        ver_modulo, ver, crear, editar, cancelar

          O sea: el web ya apagaba los botones por estos permisos y el backend
          no los conocia. Las dos mitades opinaban distinto sobre la misma
          persona — justo lo que el proyecto ya evito en PermisoControllerBase.
          Se declaran TAL CUAL, con guion bajo en "almacen_cascos" incluido,
          porque cambiarles una letra las volveria otras claves.

          Ojo con el nombre: es "cancelar", no "activar" como en los modulos
          deportivos hermanos. Es lo que dice el front, y ademas describe mejor
          lo que hacen estas pantallas: un movimiento de casco no se activa ni
          se desactiva, se cancela.
        */
        new("almacen_cascos.ver_modulo", "Almacén de Cascos - Ver módulo", "almacen_cascos"),
        new("almacen_cascos.ver", "Almacén de Cascos - Ver", "almacen_cascos"),
        new("almacen_cascos.crear", "Almacén de Cascos - Crear movimiento", "almacen_cascos"),
        new("almacen_cascos.editar", "Almacén de Cascos - Modificar movimiento", "almacen_cascos"),
        new("almacen_cascos.cancelar", "Almacén de Cascos - Cancelar movimiento", "almacen_cascos"),

        new("temporadas.ver_modulo", "Temporadas - Ver módulo", "temporadas"),
        new("temporadas.ver", "Temporadas - Ver", "temporadas"),
        new("temporadas.crear", "Temporadas - Crear", "temporadas"),
        new("temporadas.editar", "Temporadas - Editar", "temporadas"),
        new("temporadas.cancelar", "Temporadas - Cancelar/Reactivar", "temporadas"),

        new("torneos.ver_modulo", "Torneos - Ver módulo", "torneos"),
        new("torneos.ver", "Torneos - Ver", "torneos"),
        new("torneos.crear", "Torneos - Crear", "torneos"),
        new("torneos.editar", "Torneos - Editar", "torneos"),
        new("torneos.cancelar", "Torneos - Cancelar/Activar/Cerrar/Reactivar", "torneos"),

        /*
          ESCRITURAS DE LOS CATALOGOS DE MAC31 (ola 6).

          La familia `catalogos.*` es del WEB, no de Mac31: no esta entre las
          cuatro que manda legacy (reportes, ventas, empleados, prestamos), asi
          que lo que se da desde la pantalla de permisos SE RESPETA y no lo borra
          el siguiente recalculo. Las lecturas (`catalogos.ver`,
          `catalogos.marcas.ver`, `catalogos.galleros.ver`...) ya las sembro la
          ola 1; aqui van las de escritura.

          POR QUE NO UN SOLO `catalogos.editar` PARA TODO
          ----------------------------------------------
          Porque no todos los catalogos pesan lo mismo:
           - galleros trae SALDO: es dinero;
           - empresas trae RFC, regimen fiscal, CLABE y el folio con el que se
             factura: es la identidad fiscal de la empresa;
           - marcas y categorias deciden las columnas del concentrado: cambiar
             una marca de columna cambia los reportes de toda la empresa;
           - territorial le mueve el domicilio a clientes y proveedores que ya
             estaban capturados;
           - autos trae poliza y monto asegurado.
          Quien captura un tipo de gasto no tiene por que poder cambiar el RFC
          con el que se factura.

          Territorial no tiene `.baja` a proposito: ciudades, municipios y
          colonias no tienen IDStatus en la base, asi que no hay baja que dar.
        */
        new("catalogos.crear", "Catálogos - Dar de alta", "catalogos"),
        new("catalogos.editar", "Catálogos - Modificar", "catalogos"),
        new("catalogos.baja", "Catálogos - Dar de baja / reactivar", "catalogos"),

        new("catalogos.marcas.crear", "Catálogos - Alta de marcas y categorías", "catalogos"),
        new("catalogos.marcas.editar", "Catálogos - Modificar marcas y categorías", "catalogos"),
        new("catalogos.marcas.baja", "Catálogos - Baja de marcas y categorías", "catalogos"),

        new("catalogos.galleros.crear", "Catálogos - Alta de galleros", "catalogos"),
        new("catalogos.galleros.editar", "Catálogos - Modificar galleros", "catalogos"),
        new("catalogos.galleros.baja", "Catálogos - Baja de galleros", "catalogos"),

        new("catalogos.empresas.crear", "Catálogos - Alta de empresas y series", "catalogos"),
        new("catalogos.empresas.editar", "Catálogos - Modificar empresas y series", "catalogos"),
        new("catalogos.empresas.baja", "Catálogos - Baja de empresas y series", "catalogos"),

        new("catalogos.territorial.crear", "Catálogos - Alta territorial", "catalogos"),
        new("catalogos.territorial.editar", "Catálogos - Modificar territorial", "catalogos"),

        new("catalogos.autos.crear", "Catálogos - Alta de autos", "catalogos"),
        new("catalogos.autos.editar", "Catálogos - Modificar autos", "catalogos"),
        new("catalogos.autos.baja", "Catálogos - Baja de autos", "catalogos"),

        /*
          LAS FICHAS MAESTRAS: CLIENTE Y PRODUCTO (ola 6).

          Proveedores NO aparece aqui porque sus cuatro claves ya existian arriba
          (proveedores.ver/crear/editar/estado.editar) y son las mismas: no hacia
          falta inventar otras.

          Clientes tiene tres claves de mas, y cada una tiene su razon:
           - `clientes.bonificaciones.editar` es DINERO: es lo que se le abona al
             cliente por cada casco que entrega.
           - `clientes.facturacion.editar` es el RFC y el regimen con el que se le
             factura. Quien captura un telefono no tiene por que poder cambiarlo.
           - `clientes.centros.editar` decide en que centros se le surte.
          Las claves de LECTURA (clientes.ver, clientes.facturacion.ver,
          clientes.log.ver, productos.ver) las sembro la ola 3.
        */
        new("clientes.crear", "Clientes - Dar de alta", "clientes"),
        new("clientes.editar", "Clientes - Modificar", "clientes"),
        new("clientes.estado.editar", "Clientes - Dar de baja / reactivar", "clientes"),
        new("clientes.bonificaciones.editar", "Clientes - Editar bonificaciones de usados", "clientes"),
        new("clientes.facturacion.editar", "Clientes - Editar datos de facturación", "clientes"),
        new("clientes.centros.editar", "Clientes - Editar centros de servicio permitidos", "clientes"),

        new("productos.crear", "Productos - Dar de alta", "productos"),
        new("productos.editar", "Productos - Modificar", "productos"),
        new("productos.estado.editar", "Productos - Dar de baja / reactivar", "productos"),

        /*
          OPERACION DIARIA (ola 6): compras, inventario en transito, cortes de
          caja, transferencias bancarias y cambios de vigencia.

          Las de transferencias estan separadas en cuatro porque son cuatro pasos
          con consecuencias muy distintas: CARGAR el estado de cuenta no mueve
          dinero, IDENTIFICAR solo dice de quien es un deposito, y APLICAR y
          CANCELAR si mueven dinero — tocan Ventas, [Ventas Pagos],
          [Ventas Pagos Usados] y el saldo del cliente. Quien sube el archivo del
          banco no tiene por que poder aplicar pagos.

          `inventario.transito.procesar` va aparte de compras porque mueve
          existencias y se usa desde otra pantalla.
        */
        new("compras.ver_modulo", "Compras - Ver módulo", "compras"),
        new("compras.ver", "Compras - Consultar", "compras"),
        new("compras.editar", "Compras - Marcar como factura", "compras"),
        new("compras.cancelar", "Compras - Cancelar", "compras"),

        // La lectura de lo mismo. Va aparte del procesar porque son dos
        // decisiones distintas: VER lo que viene en camino no mueve nada, y
        // ACEPTARLO mueve existencias. Quien planea la ruta necesita la primera;
        // solo quien recibe la mercancia necesita la segunda.
        new("inventario.transito.ver", "Inventario - Consultar tránsito", "inventario"),
        new("inventario.transito.procesar", "Inventario - Procesar tránsito", "inventario"),

        new("cortes.ver_modulo", "Cortes - Ver módulo", "cortes"),
        new("cortes.ver", "Cortes - Consultar", "cortes"),
        new("cortes.crear", "Cortes - Guardar corte de caja", "cortes"),
        new("cortes.cancelar", "Cortes - Cancelar", "cortes"),
        new("cortes.cs.ver", "Cortes - Consultar centros de servicio", "cortes"),
        new("cortes.cs.crear", "Cortes - Guardar retiro de centro de servicio", "cortes"),

        new("transferencias.ver_modulo", "Transferencias - Ver módulo", "transferencias"),
        new("transferencias.ver", "Transferencias - Consultar", "transferencias"),
        new("transferencias.cargar", "Transferencias - Cargar estado de cuenta", "transferencias"),
        new("transferencias.identificar", "Transferencias - Identificar cliente", "transferencias"),
        new("transferencias.aplicar", "Transferencias - Aplicar pagos", "transferencias"),
        new("transferencias.cancelar", "Transferencias - Cancelar movimiento", "transferencias"),

        new("cambiosvigencia.ver_modulo", "Cambios de vigencia - Ver módulo", "cambiosvigencia"),
        new("cambiosvigencia.ver", "Cambios de vigencia - Consultar", "cambiosvigencia"),
        new("cambiosvigencia.crear", "Cambios de vigencia - Registrar", "cambiosvigencia"),
        new("cambiosvigencia.editar", "Cambios de vigencia - Modificar / regresar", "cambiosvigencia"),
        new("cambiosvigencia.cancelar", "Cambios de vigencia - Cancelar", "cambiosvigencia"),

        /*
          CONFIGURACION, HUELLAS Y NOMINA (ola 6).

          POR QUE LA NOMINA NO USA `empleados.*` NI `prestamos.*`
          -----------------------------------------------------
          Porque esas dos familias las gobierna Mac31: el puente de permisos las
          recalcula desde los botones de sus formas y BORRA lo que haya guardado el
          web. Un `prestamos.descontar` dado desde la pantalla de permisos
          desapareceria en el siguiente calculo, sin error y sin rastro, y el boton
          quedaria apagado sin que nadie supiera por que. La familia `nomina.*` es
          del web y si se respeta.

          Los tres de nomina van separados porque son tres autoridades distintas:
          calcular los recibos de un periodo, crear los periodos, y DESCONTAR los
          prestamos de la nomina — este ultimo mueve dinero de la gente.

          El usuario de Mac31 reusa las claves `usuarios.*` que ya existian: es el
          mismo concepto visto desde la otra aplicacion, no uno nuevo.
        */
        /*
          LEER LAS CONSTANTES NO ES EDITARLAS.

          La pantalla de fecha de operacion ENSEÑA la fecha de ventas, la de
          pagos, quien las movio y desde que equipo, y los dos contadores de
          dias. Eso lo mira mucha mas gente que la que puede moverlos, y hasta
          ahora la unica llave de la familia era `constantes.editar`: pedirla
          para leer significa darle a cualquiera que quiera VER la fecha el
          permiso de CAMBIARLA.
        */
        new("constantes.ver", "Constantes - Consultar parámetros de operación", "constantes"),
        new("constantes.editar", "Constantes - Editar parámetros de operación", "constantes"),
        new("constantes.fechapagos.editar", "Constantes - Mover la fecha de operación de pagos", "constantes"),

        new("constantes.fechaventas.editar", "Constantes - Avanzar la fecha de operación de ventas", "constantes"),

        /*
          `facturacion.ver_modulo` ES LO QUE HACE QUE EL MODULO APAREZCA.

          `/api/modulos/disponibles` filtra los modulos por `{clave}.ver_modulo`
          (PermissionService:1675), asi que sin esta clave la familia
          `facturacion` existe en los permisos pero su tarjeta nunca se puede
          enseñar: la pantalla queda solo alcanzable escribiendo la direccion.

          No existe en Mac31 —alli la consulta de facturas cuelga del menu de
          VENTAS— y es de las que el inventario llama "capacidad del web": se
          siembra aqui y no se deriva de ningun boton.

          Falta ademas el renglon en `dbo.WModulo` y `dbo.WModuloEmpresa`, que es
          dato y no codigo. El script esta en
          `migracion/sql/alta-modulo-facturacion.sql`.
        */
        new("facturacion.ver_modulo", "Facturacion - Ver el modulo", "facturacion"),

        new("huellas.editar", "Huellas - Registrar y cancelar", "huellas"),
        new("seguridad.bitacora.registrar", "Seguridad - Registrar intentos de contraseña", "seguridad"),

        new("reportes.parametros.ver", "Reportes - Leer parámetros guardados", "reportes"),
        new("reportes.parametros.editar", "Reportes - Guardar parámetros", "reportes"),

        new("nomina.ver_modulo", "Nómina - Ver módulo", "nomina"),
        new("nomina.recibos.calcular", "Nómina - Calcular recibos del periodo", "nomina"),
        new("nomina.periodos.generar", "Nómina - Generar periodos", "nomina"),
        new("nomina.prestamos.descontar", "Nómina - Descontar préstamos de la nómina", "nomina"),

        /*
          LOS CUATRO SUELTOS (ola 6): recordatorios, servicios, proyectos de obra
          por consulta ciudadana y la cola de sincronizacion de cajas.

          Van con permiso propio por modulo y no colgados de otro, porque son
          pantallas de empresas distintas: proyectos y sincronizacion solo existen
          en algunas instalaciones (sus procedimientos no estan en Produccion_svr),
          y colgarlos de un permiso compartido le daria a todas las empresas
          casillas de modulos que no tienen.
        */
        new("recordatorios.ver_modulo", "Recordatorios - Ver módulo", "recordatorios"),
        new("recordatorios.ver", "Recordatorios - Consultar", "recordatorios"),
        new("recordatorios.crear", "Recordatorios - Crear", "recordatorios"),
        new("recordatorios.editar", "Recordatorios - Modificar", "recordatorios"),

        new("servicios.ver_modulo", "Servicios - Ver módulo", "servicios"),
        new("servicios.ver", "Servicios - Consultar", "servicios"),
        new("servicios.crear", "Servicios - Registrar", "servicios"),
        new("servicios.cancelar", "Servicios - Cancelar", "servicios"),

        new("proyectos.ver_modulo", "Proyectos - Ver módulo", "proyectos"),
        new("proyectos.ver", "Proyectos - Consultar", "proyectos"),
        new("proyectos.crear", "Proyectos - Crear", "proyectos"),
        new("proyectos.editar", "Proyectos - Modificar", "proyectos"),

        new("sincronizacion.ver", "Sincronización - Consultar la cola", "sincronizacion"),

        /*
          OBRA (MC), AVIACION, GASOLINAS Y COBRANZAS.
          Los modulos que faltaban de MacServicios2.

          NINGUNA DE ESTAS CLAVES CAE EN LAS CUATRO FAMILIAS QUE GOBIERNA MAC31
          (ventas.*, reportes.*, empleados.*, prestamos.*), y eso es a proposito,
          no casualidad. Para esas cuatro, el puente de permisos BORRA del snapshot
          lo que diga el web y lo recalcula desde los botones de la forma de Mac31
          (ver la seccion de EnsureVentasPermissionsSyncedAsync mas abajo): una
          clave `ventas.cobranzas.crear` se habria guardado en WUsuarioPermiso y
          habria desaparecido en el siguiente calculo, sin error y sin rastro.
          Cobranzas se llama `cobranzas.*` por eso.

          OJO AL MEZCLAR CON LA OLA 3: las cuatro claves marcadas abajo con
          "(tambien de la ola 3)" las siembra tambien esa entrega. Estan aqui
          porque las usan los endpoints de este archivo y sin la fila en WPermiso
          nadie puede tenerlas; sembrar dos veces la misma clave es inofensivo —
          SincronizarCatalogo salta la que ya existe (PermissionExistsAsync)— pero
          al mezclar las dos ramas hay que dejar UNA sola linea por clave.
        */

        // --- Obra (MC): las cuatro de lectura, compartidas con la ola 3 ---
        new("obra.ver_modulo", "Obra - Ver módulo", "obra"),                    // (tambien de la ola 3)
        new("obra.catalogos.ver", "Obra - Catálogos: Ver", "obra"),             // (tambien de la ola 3)
        new("obra.contratos.ver", "Obra - Contratos: Ver", "obra"),             // (tambien de la ola 3)
        new("obra.proveedores.ver", "Obra - Proveedores: Ver", "obra"),         // (tambien de la ola 3)

        // --- Obra (MC): escrituras de los catalogos ---
        new("obra.catalogos.crear", "Obra - Catálogos: Crear", "obra"),
        new("obra.catalogos.editar", "Obra - Catálogos: Editar", "obra"),
        /* Partida presupuestal: clave propia porque el renglon trae el monto
           autorizado del presupuesto, no solo un nombre. */
        new("obra.partidas.ver", "Obra - Partidas presupuestales: Ver", "obra"),
        new("obra.partidas.crear", "Obra - Partidas presupuestales: Crear", "obra"),
        new("obra.partidas.editar", "Obra - Partidas presupuestales: Editar", "obra"),
        /* Proveedores de obra: el renglon trae RFC, CURP, telefono y correo. */
        new("obra.proveedores.crear", "Obra - Proveedores: Crear", "obra"),
        new("obra.proveedores.editar", "Obra - Proveedores: Editar", "obra"),

        // --- Obra (MC): contratos y lo que les cuelga ---
        new("obra.contratos.crear", "Obra - Contratos: Crear", "obra"),
        new("obra.contratos.editar", "Obra - Contratos: Editar", "obra"),
        /* Cerrar un contrato o un convenio es aparte de editarlo: es lo que deja
           de admitir estimaciones. */
        new("obra.contratos.cerrar", "Obra - Contratos: Cerrar/Reabrir", "obra"),
        new("obra.convenios.ver", "Obra - Convenios: Ver", "obra"),
        new("obra.convenios.crear", "Obra - Convenios: Crear", "obra"),
        new("obra.convenios.editar", "Obra - Convenios: Editar", "obra"),
        /* Capturar cantidades estimadas es autorizar un pago; no lo hace quien
           mantiene el catalogo de conceptos. */
        new("obra.estimaciones.editar", "Obra - Estimaciones: Capturar", "obra"),

        // --- Aviacion ---
        new("aviacion.ver_modulo", "Aviación - Ver módulo", "aviacion"),
        new("aviacion.cajas.editar", "Aviación - Cajas: Editar", "aviacion"),
        new("aviacion.conceptos.ver", "Aviación - Conceptos: Ver", "aviacion"),
        new("aviacion.conceptos.crear", "Aviación - Conceptos: Crear", "aviacion"),
        new("aviacion.conceptos.editar", "Aviación - Conceptos: Editar", "aviacion"),
        new("aviacion.cuentas.ver", "Aviación - Cuentas: Ver", "aviacion"),
        new("aviacion.cuentas.crear", "Aviación - Cuentas: Crear", "aviacion"),
        new("aviacion.cuentas.editar", "Aviación - Cuentas: Editar", "aviacion"),
        /* "Aceptar" es lo que convierte un alta hecha en una caja en un renglon
           bueno del central: lo hace quien supervisa, no quien captura. */
        new("aviacion.cuentas.aceptar", "Aviación - Cuentas: Aceptar", "aviacion"),
        new("aviacion.economicos.ver", "Aviación - Económicos: Ver", "aviacion"),
        new("aviacion.economicos.crear", "Aviación - Económicos: Crear", "aviacion"),
        new("aviacion.economicos.editar", "Aviación - Económicos: Editar", "aviacion"),
        new("aviacion.economicos.aceptar", "Aviación - Económicos: Aceptar", "aviacion"),
        /* Reasignar cambia de dueno una unidad y deja rastro en el historico. */
        new("aviacion.economicos.reasignar", "Aviación - Económicos: Reasignar", "aviacion"),
        new("aviacion.fianzas.ver", "Aviación - Motivos de fianza: Ver", "aviacion"),
        new("aviacion.fianzas.crear", "Aviación - Motivos de fianza: Crear", "aviacion"),
        new("aviacion.fianzas.editar", "Aviación - Motivos de fianza: Editar", "aviacion"),
        new("aviacion.operadores.ver", "Aviación - Operadores: Ver", "aviacion"),
        new("aviacion.operadores.crear", "Aviación - Operadores: Crear", "aviacion"),
        new("aviacion.operadores.editar", "Aviación - Operadores: Editar", "aviacion"),
        new("aviacion.operadores.aceptar", "Aviación - Operadores: Aceptar", "aviacion"),
        new("aviacion.permisionarios.ver", "Aviación - Permisionarios: Ver", "aviacion"),
        new("aviacion.permisionarios.crear", "Aviación - Permisionarios: Crear", "aviacion"),
        new("aviacion.permisionarios.editar", "Aviación - Permisionarios: Editar", "aviacion"),
        new("aviacion.permisionarios.aceptar", "Aviación - Permisionarios: Aceptar", "aviacion"),
        new("aviacion.impresoras.ver", "Aviación - Impresoras: Ver", "aviacion"),
        new("aviacion.impresoras.editar", "Aviación - Impresoras: Configurar", "aviacion"),
        new("aviacion.polizas.ver", "Aviación - Pólizas: Ver", "aviacion"),
        /* Generar una poliza emite los cheques con los que se paga: no es editar. */
        new("aviacion.polizas.generar", "Aviación - Pólizas: Generar", "aviacion"),
        new("aviacion.polizas.cancelar", "Aviación - Pólizas: Cancelar", "aviacion"),
        new("aviacion.polizas.editar", "Aviación - Pólizas: Corregir cheques y encabezado", "aviacion"),
        new("aviacion.recaudaciones.ver", "Aviación - Recaudaciones: Ver", "aviacion"),
        /* En una caja son tres personas distintas: el cajero cobra, el supervisor
           cancela, y el corte lo hace quien entrega el dinero. */
        new("aviacion.recaudaciones.cobrar", "Aviación - Recaudaciones: Cobrar", "aviacion"),
        new("aviacion.recaudaciones.cancelar", "Aviación - Recaudaciones: Cancelar", "aviacion"),
        new("aviacion.recaudaciones.cortar", "Aviación - Recaudaciones: Cerrar el día", "aviacion"),

        // --- Gasolinas ---
        new("gasolinas.ver_modulo", "Gasolinas - Ver módulo", "gasolinas"),
        new("gasolinas.ver", "Gasolinas - Ver", "gasolinas"),
        new("gasolinas.crear", "Gasolinas - Capturar recibo", "gasolinas"),
        new("gasolinas.editar", "Gasolinas - Corregir recibo", "gasolinas"),
        new("gasolinas.cancelar", "Gasolinas - Cancelar recibo", "gasolinas"),

        // --- Cobranzas ---
        new("cobranzas.ver_modulo", "Cobranzas - Ver módulo", "cobranzas"),
        new("cobranzas.ver", "Cobranzas - Ver", "cobranzas"),
        new("cobranzas.crear", "Cobranzas - Armar cobranza", "cobranzas"),
        new("cobranzas.editar", "Cobranzas - Cambiar contenido", "cobranzas"),
        new("cobranzas.cancelar", "Cobranzas - Cancelar", "cobranzas"),
        new("cobranzas.pendientes.ver", "Cobranzas - Pendiente y vencida", "cobranzas"),
        // btnBuscar de CONSULTA DE COBRANZAS. Lo derivaba el puente desde
        // siempre y no estaba sembrado, asi que no salia en la pantalla de
        // Permisos. Desde que la lectura de esta pantalla deja de exigir
        // "cobranza.ver" -que es de CONSULTA DE COBROS, otra forma- un
        // controlador lo pide, y lo que un controlador pide tiene que estar
        // sembrado o contesta 403 a todos.
        new("cobranzas.consultar", "Cobranzas - Buscar", "cobranzas"),

        /*
          OLA 4 — LAS ESCRITURAS DE VENTAS, EN FAMILIAS QUE NO SON `ventas.*`.

          Esto no es capricho de nomenclatura. Para `ventas.*` manda Mac31: mas
          abajo, al armar el snapshot, se BORRA del usuario todo lo que empiece
          por "ventas." y no este en VentasWebPermissionSeeds, y se recalcula
          desde los botones de la forma CONSULTA DE VENTAS. Una clave nueva ahi
          se guarda en WUsuarioPermiso y desaparece en el siguiente calculo, sin
          error y sin rastro — la cicatriz de "pendientes de autorizar".

          Mac31 tiene UN boton por pantalla y aqui hace falta mas fino, porque
          dentro de la misma pantalla hay operaciones que mueven dinero y otras
          que no: guardar el reparto de un cobro de cascos no mueve nada,
          APLICARLO mueve los saldos de los clientes. Donde Mac31 si tiene una
          llave que vale, los endpoints la aceptan ADEMAS de la nueva
          (`usados.ver` o `ventas.usados`), para que nadie se quede sin acceso el
          primer dia.
        */
        new("usados.ver", "Cascos usados - Consultar", "usados"),
        new("usados.cobros_dinero.editar", "Cascos usados - Cobros de dinero: registrar y repartir", "usados"),
        new("usados.cobros_dinero.aplicar", "Cascos usados - Cobros de dinero: APLICAR a los saldos", "usados"),
        new("usados.cobros_dinero.cancelar", "Cascos usados - Cobros de dinero: cancelar aplicacion", "usados"),
        new("usados.cobros.editar", "Cascos usados - Ingresos: registrar", "usados"),
        new("usados.cobros.cancelar", "Cascos usados - Ingresos: cancelar", "usados"),
        new("usados.excedentes.editar", "Cascos usados - Excedentes: registrar", "usados"),
        new("usados.excedentes.cancelar", "Cascos usados - Excedentes: cancelar", "usados"),
        new("usados.movimientos.editar", "Cascos usados - Movimientos de almacen: registrar", "usados"),
        new("usados.movimientos.cancelar", "Cascos usados - Movimientos de almacen: cancelar", "usados"),
        new("usados.tipos.editar", "Cascos usados - Catalogo de tipos: crear y modificar", "usados"),

        new("cobranza.ver", "Cobranza - Consultar", "cobranza"),
        new("cobranza.cobranzas.editar", "Cobranza - Cobranzas: crear y modificar", "cobranza"),
        new("cobranza.cobranzas.cancelar", "Cobranza - Cobranzas: cancelar", "cobranza"),
        new("cobranza.concentrados.editar", "Cobranza - Concentrados: crear y modificar", "cobranza"),
        new("cobranza.concentrados.cancelar", "Cobranza - Concentrados: cancelar", "cobranza"),
        // Las dos de leer CONSULTA DE CONCENTRADOS, btnBuscar y btnVer. El
        // puente ya las derivaba; ahora ademas son las que abren la pantalla
        // de concentrados, en vez de "cobranza.ver", que es de la forma de
        // cobros y quien solo trabaja concentrados nunca recibe.
        new("cobranza.concentrados.consultar", "Cobranza - Concentrados: buscar", "cobranza"),
        new("cobranza.concentrados.ver", "Cobranza - Concentrados: ver", "cobranza"),
        new("cobranza.cobros.editar", "Cobranza - Cobros: modificar", "cobranza"),
        new("cobranza.cobros.cerrar", "Cobranza - Cobros: cerrar", "cobranza"),
        new("cobranza.cobros.abrir", "Cobranza - Cobros: reabrir", "cobranza"),
        new("cobranza.cobros.cancelar", "Cobranza - Cobros: cancelar", "cobranza"),

        new("garantias.ver", "Garantias - Consultar", "garantias"),
        new("garantias.editar", "Garantias - Registrar y modificar", "garantias"),
        new("garantias.cancelar", "Garantias - Cancelar", "garantias"),
        new("garantias.aceptar", "Garantias - Aceptar en el centro de servicio", "garantias"),
        new("garantias.registro.editar", "Garantias - Registro previo: crear", "garantias"),
        new("garantias.registro.cancelar", "Garantias - Registro previo: cancelar", "garantias"),

        new("descuentos.ver", "Descuentos - Consultar", "descuentos"),
        new("descuentos.editar", "Descuentos - Crear y modificar", "descuentos"),
        new("descuentos.eliminar", "Descuentos - Eliminar", "descuentos"),
        new("descuentos.simular", "Descuentos - Simular", "descuentos"),
        new("descuentos.ajustes.ver", "Descuentos - Ajustes de venta: consultar", "descuentos"),
        new("descuentos.ajustes.crear", "Descuentos - Ajustes de venta: solicitar", "descuentos"),
        new("descuentos.ajustes.autorizar", "Descuentos - Ajustes de venta: autorizar o rechazar", "descuentos"),
        new("descuentos.ajustes.aplicar", "Descuentos - Ajustes de venta: aplicar el descuento", "descuentos"),

        new("inventario.ajustes.ver", "Inventario - Ajustes: consultar", "inventario"),
        new("inventario.ajustes.crear", "Inventario - Ajustes: capturar", "inventario"),
        new("inventario.ajustes.cancelar", "Inventario - Ajustes: cancelar", "inventario"),
        new("inventario.pedidos.ver", "Inventario - Inventario de pedidos: consultar", "inventario"),
        new("inventario.pedidos.crear", "Inventario - Inventario de pedidos: capturar", "inventario"),
        new("inventario.pedidos.cancelar", "Inventario - Inventario de pedidos: cancelar", "inventario"),

        new("facturacion.ver", "Facturacion - Consultar facturas", "facturacion"),
        new("facturacion.timbrar", "Facturacion - Timbrar", "facturacion"),
        new("facturacion.cancelar", "Facturacion - Cancelar un CFDI", "facturacion"),
        new("facturacion.enviar", "Facturacion - Enviar la factura por correo", "facturacion"),

        new("cobros.pagos.editar", "Cobros - Pagos de remision: capturar", "cobros"),
        new("cobros.pagos.cancelar", "Cobros - Pagos de remision: cancelar", "cobros"),
        new("cobros.pagos.aplicar", "Cobros - Pagos del nuevo esquema: aplicar", "cobros"),
        new("cobros.pagos.incobrable", "Cobros - Declarar una remision incobrable", "cobros"),
        new("cobros.pagos.kilos", "Cobros - Pagos por kilo", "cobros"),
        new("cobros.transferencias.cancelar", "Cobros - Cancelar transferencias bancarias", "cobros"),
        new("cobros.reasignar", "Cobros - Reasignar remisiones de cliente o agente", "cobros"),

        /*
          LAS FAMILIAS QUE EXISTIAN EN LA BASE Y NO EN ESTA LISTA.

          QUE PASABA
          Cambio de Cascos, el Checador, Asistencias y TODO el canal movil exigen
          sus claves en el controlador âCascosCambioController:34-36,
          ChecadorController:23-25, AsistenciasController:24-25,
          EmpleadosController:23-29, PrestamosController:30-33,
          VentasPedidoCapturaController (app_movil.pedidos),
          VentasPedidosController:27-29 y VentasUsadosConsultaController:49â y
          ninguna estaba aqui. Llegaron a `WPermiso` de Tauro porque alguien
          corrio un script a mano cuando se desplegaron, no porque el backend las
          sembrara. En una base de empresa NUEVA no entran: comprobado en MacZ
          (Zaragoza), CERO filas `cascos_cambio.*` y cero `app_movil.ventas.usados`.

          Y una clave que no esta en WPermiso NO PUEDE TENER FILA DE CONCESION,
          asi que su endpoint contesta 403 a todo el mundo menos al SuperAdmin
          (PermissionAuthorizationHandler, primer if). O sea: desplegar este
          backend contra una empresa nueva dejaba Cambio de Cascos, el Checador y
          la app movil completa en 403 hasta que un humano abriera la pantalla de
          permisos y pulsara sincronizar. Es el mismo agujero que el arranque vino
          a cerrar, entrando por las familias que nadie anoto.

          POR QUE VAN AQUI Y NO EN UNA LISTA BLANCA DEL PUENTE DE LEGACY
          Ninguna empieza por `ventas.`, `reportes.`, `empleados.` ni
          `prestamos.`, que son las cuatro familias donde manda Mac31 y donde lo
          que se guarda desde el web se borra en el siguiente calculo. El prefijo
          `app_movil.` es justo lo que las salva: `app_movil.empleados.ver` NO
          empieza por "empleados.", asi que el RemoveAll por prefijo no se la
          lleva y lo que se de desde la pantalla de permisos se respeta. Esa es la
          razon por la que el canal movil lleva prefijo propio y no sufijo.

          LAS TRES CLAVES `ventas.*` QUE LA AUDITORIA CONTO EN ESTE HUECO NO VAN
          EN ESTA LISTA. `ventas.ver_modulo`, `ventas.usados` y
          `ventas.usados.guardar` las siembra EnsureVentasPermissionsSyncedAsync
          desde los botones de las tres formas de Mac31, y eso ya corre al
          arrancar (SembrarCatalogoCompletoAsync). Declararlas aqui crearia dos
          duenos de la misma llave y la de Mac31 ganaria en el siguiente calculo.

          SEMBRAR NO ES CONCEDER: esto solo crea la clave para poder repartirla.
          Quien no la tenga concedida sigue sin entrar.
        */
        new("cascos_cambio.ver_modulo", "Cambio de Cascos - Ver mÃ³dulo", "cascos_cambio"),
        new("cascos_cambio.ver", "Cambio de Cascos - Ver", "cascos_cambio"),
        new("cascos_cambio.crear", "Cambio de Cascos - Registrar cambio", "cascos_cambio"),
        new("cascos_cambio.cancelar", "Cambio de Cascos - Cancelar cambio", "cascos_cambio"),

        new("asistencias.ver", "Asistencias - Ver", "asistencias"),
        new("asistencias.editar", "Asistencias - Editar", "asistencias"),

        new("checador.ver", "Checador - Checar entrada/salida", "checador"),
        new("checador.huellas", "Checador - Administrar huellas", "checador"),
        new("checador.reportes", "Checador - Reportes del checador", "checador"),

        new("app_movil.pedidos", "App mÃ³vil - Capturar pedidos", "app_movil"),
        new("app_movil.ventas.ver", "App mÃ³vil - Ver pendientes de autorizar", "app_movil"),
        new("app_movil.ventas.autorizar", "App mÃ³vil - Autorizar pendientes", "app_movil"),
        new("app_movil.ventas.usados", "App mÃ³vil - Consultar usados", "app_movil"),
        new("app_movil.cascos_cambio.ver", "App mÃ³vil - Cambio de cascos: Ver", "app_movil"),
        new("app_movil.cascos_cambio.crear", "App mÃ³vil - Cambio de cascos: Registrar", "app_movil"),
        new("app_movil.cascos_cambio.cancelar", "App mÃ³vil - Cambio de cascos: Cancelar", "app_movil"),
        new("app_movil.asistencias.ver", "App mÃ³vil - Asistencias: Ver", "app_movil"),
        new("app_movil.asistencias.editar", "App mÃ³vil - Asistencias: Editar", "app_movil"),
        new("app_movil.empleados.ver", "App mÃ³vil - Empleados: Ver", "app_movil"),
        new("app_movil.empleados.crear", "App mÃ³vil - Empleados: Dar de alta", "app_movil"),
        new("app_movil.empleados.editar", "App mÃ³vil - Empleados: Modificar", "app_movil"),
        new("app_movil.empleados.sueldo.ver", "App mÃ³vil - Empleados: Ver sueldo y bonos", "app_movil"),
        new("app_movil.prestamos.ver", "App mÃ³vil - PrÃ©stamos: Ver", "app_movil"),
        new("app_movil.prestamos.crear", "App mÃ³vil - PrÃ©stamos: Crear", "app_movil"),
        new("app_movil.prestamos.editar", "App mÃ³vil - PrÃ©stamos: Editar", "app_movil"),
        new("app_movil.prestamos.cancelar", "App mÃ³vil - PrÃ©stamos: Cancelar", "app_movil"),

        /*
          LOS CINCO MODULOS NUEVOS DEL TELEFONO.

          Sus pantallas ya existen y ya estan dadas de alta en el registro
          de la app, pero sin su llave aqui el mosaico no le sale a NADIE
          salvo a un SuperAdmin: la lista de modulos del telefono se filtra
          por permiso, y un permiso que no esta en este catalogo no lo tiene
          nadie. Era el candado que faltaba, no un olvido de los modulos.

          Van en el canal app_movil. y no reusan el permiso del web A
          PROPOSITO: asi se puede dar el acceso en la computadora y negarlo
          en el telefono, que es justo lo que hace falta con un repartidor.

          OJO: esto solo los pone en el CATALOGO. Los endpoints siguen
          pidiendo sus permisos de siempre -ventas.consultar, clientes.ver,
          cortes.ver...- asi que a quien reciba uno de estos hay que darle
          tambien el del backend, o entrara a la pantalla y recibira un 403.
        */
        new("app_movil.ventas.consulta.ver", "App móvil - Consulta de ventas", "app_movil"),
        new("app_movil.clientes", "App móvil - Clientes", "app_movil"),
        new("app_movil.facturacion.ver", "App móvil - Facturas: Ver", "app_movil"),
        new("app_movil.facturacion.enviar", "App móvil - Facturas: Enviar por correo", "app_movil"),
        new("app_movil.productos", "App móvil - Productos", "app_movil"),
        new("app_movil.cortes.ver", "App móvil - Cortes: Ver", "app_movil"),
        new("app_movil.cortes.cancelar", "App móvil - Cortes: Cancelar", "app_movil"),

        /*
          LOS TRES MODULOS QUE COMPARTIAN LA LLAVE DEL WEB.

          Almacen de cascos, Ajustes de inventario e Inventario en transito
          eran los unicos modulos del telefono SIN canal propio: su pantalla
          pedia `almacen_cascos.ver`, `inventario.ajustes.ver` y
          `inventario.transito.ver`, los mismos codigos que usan el web y
          Mac31. La consecuencia estaba escrita en los tres archivos de la app
          -y se pidio arreglar-: quitarle el modulo a alguien EN EL TELEFONO
          se lo quitaba tambien en la computadora, porque no eran dos permisos
          parecidos, era el mismo.

          Las nueve claves de abajo son ese canal que faltaba. Con ellas:

            - negar en el telefono es solo el telefono (se le quita la clave
              `app_movil.` y conserva la del web);
            - conceder SOLO el telefono tambien funciona, porque los endpoints
              aceptan cualquiera de las dos (ver AlmacenCascosController,
              InventarioOperacionesController, InventarioTransitoConsultaController
              y el POST de transito en ComprasController).

          Los nombres de las claves NO son nuevos: son exactamente los que los
          tres archivos de la app ya proponian en sus comentarios
          (almacen_cascos.dart, inventario_ajustes.dart, inventario_transito.dart),
          para que lo que ahi esta escrito siga siendo verdad.

          `inventario.ajustes` y `inventario.transito` dejan la categoria en
          "inventario" -categoriaSegunLaClave se queda con el segundo tramo-,
          asi que en la pantalla de permisos salen juntos bajo Inventario. Es
          lo que se queria: en el telefono son dos mosaicos del mismo tema.
        */
        new("app_movil.almacen_cascos.ver", "App móvil - Almacén de cascos: Ver", "app_movil"),
        new("app_movil.almacen_cascos.crear", "App móvil - Almacén de cascos: Registrar", "app_movil"),
        new("app_movil.almacen_cascos.editar", "App móvil - Almacén de cascos: Modificar", "app_movil"),
        new("app_movil.almacen_cascos.cancelar", "App móvil - Almacén de cascos: Cancelar", "app_movil"),
        new("app_movil.inventario.ajustes.ver", "App móvil - Ajustes de inventario: Ver", "app_movil"),
        new("app_movil.inventario.ajustes.crear", "App móvil - Ajustes de inventario: Capturar", "app_movil"),
        new("app_movil.inventario.ajustes.cancelar", "App móvil - Ajustes de inventario: Cancelar", "app_movil"),
        new("app_movil.inventario.transito.ver", "App móvil - Inventario en tránsito: Ver", "app_movil"),
        new("app_movil.inventario.transito.procesar", "App móvil - Inventario en tránsito: Dar entrada", "app_movil"),

        /*
          LAS ACCIONES DE LOS CINCO MODULOS QUE SOLO TENIAN LLAVE DE MOSAICO.

          Consulta de ventas, Clientes, Facturas, Productos y Cortes ya tenian su
          `app_movil.X` para que el mosaico se pudiera apagar en el telefono. Pero
          sus ENDPOINTS seguian pidiendo la clave del web, asi que conceder solo
          el telefono no alcanzaba: la pantalla abria y el servidor contestaba
          403. Lo que faltaba son estas claves de ACCION, una por cada cosa que la
          pantalla del telefono sabe hacer.

          Se declaran solo las acciones QUE LA APP TIENE. No hay
          `app_movil.clientes.bonificaciones` ni `app_movil.cortes.crear` porque
          esas pantallas no existen en el telefono, y una casilla que no apaga
          nada es peor que no tenerla.

          IMPRIMIR / REIMPRIMIR / VER EN PANTALLA SE SEPARAN, y antes no lo
          estaban: las tres politicas aceptaban `app_movil.ventas.consulta.ver`,
          o sea que dar "ver" en el telefono daba tambien imprimir. Eran tres
          permisos distintos en Mac31 y en el web, y ahora tambien aqui. Como eso
          QUITA algo a quien hoy lo tiene por la clave de ver, el script de
          traspaso (Querrys/48) les da las tres: nadie pierde nada el dia del
          despliegue.
        */
        new("app_movil.ventas.consulta.pantalla", "App móvil - Consulta de ventas: Ver en pantalla", "app_movil"),
        new("app_movil.ventas.consulta.imprimir", "App móvil - Consulta de ventas: Imprimir", "app_movil"),
        new("app_movil.ventas.consulta.reimprimir", "App móvil - Consulta de ventas: Reimprimir", "app_movil"),
        new("app_movil.ventas.consulta.pagos", "App móvil - Consulta de ventas: Ver pagos", "app_movil"),
        new("app_movil.ventas.consulta.cancelar", "App móvil - Consulta de ventas: Cancelar remisión", "app_movil"),
        new("app_movil.ventas.consulta.ver_de_otros", "App móvil - Consulta de ventas: Ver las de otros usuarios", "app_movil"),
        new("app_movil.ventas.pedidos.procesar", "App móvil - Pedidos: Procesar", "app_movil"),
        new("app_movil.ventas.pedidos.cancelar", "App móvil - Pedidos: Cancelar", "app_movil"),
        new("app_movil.clientes.crear", "App móvil - Clientes: Dar de alta", "app_movil"),
        new("app_movil.clientes.editar", "App móvil - Clientes: Modificar", "app_movil"),
        new("app_movil.clientes.estado.editar", "App móvil - Clientes: Dar de baja y reactivar", "app_movil"),
        new("app_movil.clientes.facturacion.ver", "App móvil - Clientes: Ver los RFC para facturar", "app_movil"),
        new("app_movil.clientes.log.ver", "App móvil - Clientes: Ver la bitácora", "app_movil"),
        new("app_movil.cortes.cs.ver", "App móvil - Cortes de centro de servicio: Ver", "app_movil"),

        /*
          LAS CUATRO QUE LA APP PIDE Y QUE NADIE SEMBRABA. LA PRIMERA ES LA
          LLAVE MAESTRA.

          `app_movil.acceso` es lo que la app comprueba para dejar ENTRAR
          (mobile_access_gate.dart). No estaba en este catalogo: vivia solo en
          `Querrys/07 Permisos App Movil`, un script que alguien corrio a mano
          en su dia. En las dos bases de aqui existe por eso. En una base de
          empresa NUEVA no existiria, y entonces no entra NADIE a la app salvo
          un SuperAdmin -que se salta la comprobacion y por eso no lo nota quien
          lo prueba con su cuenta de administrador-.

          Es exactamente el agujero que este catalogo vino a cerrar, con la
          llave maestra de la app dentro. Las otras tres son el portal de
          oficina, el modulo de Reportes en el telefono y el boton Editar de
          "Mis pedidos".

          POR QUE NO LAS ATRAPABA LA PRUEBA: ClavesDePermisoSembradasTests
          recorre las claves que exigen los CONTROLADORES, y estas cuatro no las
          exige ningun endpoint: viven solo en el codigo Dart. Se agrega la
          prueba que cruza los literales de la app contra estas semillas.
        */
        new("app_movil.acceso", "App móvil - Entrar a la app", "app_movil"),
        new("app_movil.oficina", "App móvil - Portal de oficina", "app_movil"),
        new("app_movil.reportes", "App móvil - Reportes", "app_movil"),
        new("app_movil.pedidos.editar", "App móvil - Editar en «Mis pedidos»", "app_movil"),

        /*
          LAS CUATRO PANTALLAS DE ADMINISTRACION, QUE NO TENIAN NADA.

          Usuarios, Modulos, Roles y Permisos de usuarios eran las unicas del
          telefono sin canal propio: su mosaico colgaba de `<modulo>.ver_modulo`
          -del web- y sus botones de `usuarios.crear`, `permisos_modulos.activar`
          y companía. O sea que para dar la administracion en el telefono habia
          que darla tambien en la computadora.

          AVISO QUE NO SE DEBE PERDER: con estas claves se reparten permisos
          DESDE UN TELEFONO. Son exactamente igual de poderosas que sus parejas
          del web, ni mas ni menos; lo que cambia es que ahora se pueden dar por
          separado. Quien no quiera administracion movil, simplemente no las
          concede.

          `permisos_usuarios.ver` no existe como pareja porque esa pantalla se
          lee con el bootstrap (`permisosweb.bootstrap`), que ya lleva la suya.
        */
        new("app_movil.usuarios.ver", "App móvil - Usuarios: Ver", "app_movil"),
        new("app_movil.usuarios.crear", "App móvil - Usuarios: Dar de alta", "app_movil"),
        new("app_movil.usuarios.editar", "App móvil - Usuarios: Modificar", "app_movil"),
        new("app_movil.usuarios.estado.editar", "App móvil - Usuarios: Activar y desactivar", "app_movil"),
        new("app_movil.permisosweb.bootstrap", "App móvil - Permisos: Leer el catálogo", "app_movil"),
        new("app_movil.permisos_modulos.ver", "App móvil - Módulos: Ver", "app_movil"),
        new("app_movil.permisos_modulos.activar", "App móvil - Módulos: Encender y apagar", "app_movil"),
        new("app_movil.permisos_roles.ver", "App móvil - Roles: Ver", "app_movil"),
        new("app_movil.permisos_roles.editar", "App móvil - Roles: Repartir permisos", "app_movil"),
        new("app_movil.permisos_usuarios.editar", "App móvil - Permisos de usuarios: Repartir", "app_movil"),

        /*
          LO QUE LE FALTABA A VENTAS EN EL TELEFONO: LAS NUEVE ACCIONES QUE
          HASTA HOY SOLO EXISTIAN EN LA COMPUTADORA.

          QUE SE ESTA CERRANDO
          --------------------
          Facturar, Multi pago, Modificar pedido, Devolucion, Usados (abrir y
          guardar), los dos simuladores y Rechazar pedido son botones de
          CONSULTA DE VENTAS y de CONSULTA DE COBROS en Mac31, y sus claves del
          web ya existian (`ventas.facturar`, `ventas.pagos.multi`,
          `ventas.pedidos.modificar`, `ventas.devolucion`, `ventas.usados`,
          `ventas.usados.guardar`, `ventas.simular.con_remision`,
          `ventas.simular.sin_remision`, `ventas.pedidos.rechazar`). La app las
          va a estrenar, y sin una clave propia por accion la unica forma de
          dejar facturar desde el telefono era dejar facturar tambien en la
          computadora: UN permiso para los dos canales. Es exactamente el
          problema que ya se resolvio con Almacen de cascos, Ajustes y Transito
          (ver el bloque de `app_movil.almacen_cascos.*` mas arriba).

          MEDIDO, Y POR ESO ES REAL: el 2026-10-14, en las DOS bases (MacZ y
          Produccion_svr), de estas nueve parejas existia UNA SOLA
          -`app_movil.ventas.usados`- y las otras ocho tenian CERO filas en
          WPermiso. Para `ventas.pedidos.rechazar` no habia ni idea de pareja:
          `PedidosOperacionesController.PermisoRechazar` era la clave del web a
          secas, asi que rechazar en el telefono obligaba a poder rechazar en
          Mac31.

          `app_movil.ventas.usados` NO SE REPITE AQUI. Ya esta declarada entre
          las primeras del canal movil y declararla dos veces no rompe
          -SincronizarCatalogoDePermisosAsync compara por clave- pero deja dos
          renglones que hay que mantener de acuerdo. Lo que si es nuevo es
          `app_movil.ventas.usados.guardar`: abrir la pantalla de usados y
          guardar un casco son dos decisiones distintas en Mac31
          (btnUsados / btnAgregarUsadoCredito) y tenian que seguir siendolo en
          el telefono.

          POR QUE EL PREFIJO `app_movil.` Y NO UN SUFIJO: el puente de Legacy
          hace un RemoveAll POR PREFIJO sobre `ventas.` cada vez que calcula
          permisos —se buscan los RemoveAll que miran `VentasModuleKey` y
          `VentasWebPermissionSeeds`, en GetPermisosBootstrapAsync y en
          BuildSnapshotAsync; no se citan por numero de linea porque este
          archivo se mueve— quedandose solo con las semillas del web. Una clave
          llamada `ventas.facturar.movil` caeria en
          ese RemoveAll y se borraria sola en el siguiente calculo;
          `app_movil.ventas.facturar` no empieza por "ventas." y sobrevive. Esa
          es la razon tecnica de que el canal movil lleve prefijo y no sufijo, y
          es tambien lo que garantiza que marcar la casilla del telefono NO
          escriba en [Usuario Forma Procesos]: a Mac31 solo se le escribe por
          las claves que estan en `ventasCatalog`, que son las del web con su
          IDProceso, y ninguna de estas nueve lo tiene.

          SEMBRAR NO ES CONCEDER. Esto solo crea la clave para poder repartirla
          desde la pestaña Movil de Accesos y permisos. Y la siembra no basta
          por si sola: el endpoint tiene que ACEPTAR la clave nueva en su
          arreglo de permisos, o la casilla se marca y el servidor sigue
          contestando 403. Cada accion lo hace en su controlador.
        */
        new("app_movil.ventas.facturar", "App móvil - Facturar la remisión", "app_movil"),
        new("app_movil.ventas.pagos.multi", "App móvil - Multi pago", "app_movil"),
        new("app_movil.ventas.pedidos.modificar", "App móvil - Pedidos: Modificar", "app_movil"),
        new("app_movil.ventas.pedidos.rechazar", "App móvil - Pedidos: Rechazar", "app_movil"),
        new("app_movil.ventas.devolucion", "App móvil - Devolución", "app_movil"),
        new("app_movil.ventas.usados.guardar", "App móvil - Usados: Guardar", "app_movil"),
        new("app_movil.ventas.simular.con_remision", "App móvil - Simular con remisión", "app_movil"),
        new("app_movil.ventas.simular.sin_remision", "App móvil - Simular sin remisión", "app_movil")
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
            // Los dos renglones -el vigente y el "stale" de rescate- se cuelgan
            // de los tokens de invalidacion. Si no, revocar un permiso dejaria
            // vivo el de rescate hasta 30 minutos.
            _cache.Set(cacheKey, snapshot, OpcionesDeCache(userId, empresaId, ActiveCacheTtl));
            _cache.Set(staleCacheKey, snapshot, OpcionesDeCache(userId, empresaId, StaleCacheTtl));
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
        /* Memoria de ESTA peticion: lo que se pregunte dos veces aqui dentro se
           pregunta una. Se tira al salir (ver AbrirMemoriaDePeticion). */
        using var memoriaDeLaPeticion = AbrirMemoriaDePeticion();

        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);

        if (!await AreCapabilityTablesAvailableAsync(conn, ct))
            return null;

        var schema = await GetSchemaAsync(conn, ct);
        var reportCatalog = await EnsureLegacyReportPermissionsSyncedAsync(conn, empresaId, ct);
        var ventasCatalog = await EnsureVentasPermissionsSyncedAsync(conn, empresaId, ct);
        var empleadosCatalog = await EnsureLegacyModulePermissionsSyncedAsync(conn, empresaId, EmpleadosBinding, ct);
        var prestamosCatalog = await EnsureLegacyModulePermissionsSyncedAsync(conn, empresaId, PrestamosBinding, ct);
        await EnsureCentrosPermissionsSyncedAsync(conn, empresaId, ct);
        var catalogosDeFamilias = await EnsureFamiliasLegacySyncedAsync(conn, empresaId, ct);
        var familiasCatalog = JuntarCatalogosDeFamilias(catalogosDeFamilias);

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
                response.Permissions.Add(EnrichPermissionItem(code, reader.GetString(reader.GetOrdinal("Nombre")), reportCatalog, ventasCatalog, empleadosCatalog, prestamosCatalog, familiasCatalog));
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
            // Las familias nuevas se leen ANTES de decidir si el usuario se
            // salta: alguien que en Mac31 solo tenga Compras no dice nada en
            // las cuatro viejas, y con la condicion de abajo intacta su fila
            // nunca se habria tocado — el puente existiria y no se veria.
            var hayDeFamilias = false;
            foreach (var catalogo in catalogosDeFamilias.Values)
            {
                if (catalogo.Count > 0) { hayDeFamilias = true; break; }
            }

            if (!hayDeFamilias
                && legacyReports.Allow.Count == 0 && legacyReports.Deny.Count == 0
                && legacyVentas.Allow.Count == 0 && legacyVentas.Deny.Count == 0
                && legacyEmpleados.Allow.Count == 0 && legacyEmpleados.Deny.Count == 0
                && legacyPrestamos.Allow.Count == 0 && legacyPrestamos.Deny.Count == 0)
                continue;

            if (!overrides.TryGetValue(user.UserId, out var row))
            {
                row = new PermisosWebUserOverrideItem { UserId = user.UserId };
                overrides[user.UserId] = row;
            }

            row.Allow.RemoveAll(EsReporteQueMandaLegacy);
            row.Deny.RemoveAll(EsReporteQueMandaLegacy);
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
            await AplicarFamiliasLegacyEnOverridesAsync(conn, empresaId, user.UserId, row, catalogosDeFamilias, ct);
        }

        response.UserOverrides = overrides.Values.OrderBy(x => x.UserId).ToList();
        return response;
    }

    public async Task<PermisosWebRolesBootstrapResponse?> GetPermisosRolesBootstrapAsync(int empresaId, CancellationToken ct)
    {
        /* Memoria de ESTA peticion: lo que se pregunte dos veces aqui dentro se
           pregunta una. Se tira al salir (ver AbrirMemoriaDePeticion). */
        using var memoriaDeLaPeticion = AbrirMemoriaDePeticion();

        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);

        if (!await AreCapabilityTablesAvailableAsync(conn, ct))
            return null;

        var schema = await GetSchemaAsync(conn, ct);
        var reportCatalog = await EnsureLegacyReportPermissionsSyncedAsync(conn, empresaId, ct);
        var ventasCatalog = await EnsureVentasPermissionsSyncedAsync(conn, empresaId, ct);
        var empleadosCatalog = await EnsureLegacyModulePermissionsSyncedAsync(conn, empresaId, EmpleadosBinding, ct);
        var prestamosCatalog = await EnsureLegacyModulePermissionsSyncedAsync(conn, empresaId, PrestamosBinding, ct);
        var familiasCatalog = JuntarCatalogosDeFamilias(await EnsureFamiliasLegacySyncedAsync(conn, empresaId, ct));
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
                response.Permissions.Add(EnrichPermissionItem(code, reader.GetString(reader.GetOrdinal("Nombre")), reportCatalog, ventasCatalog, empleadosCatalog, prestamosCatalog, familiasCatalog));
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
        var familiasCatalog = JuntarCatalogosDeFamilias(await EnsureFamiliasLegacySyncedAsync(conn, empresaId, ct));
        var response = new List<PermisosWebPermissionItem>();
        await using var permsCmd = new SqlCommand(@"
SELECT Clave, Nombre
FROM dbo.WPermiso
WHERE EmpresaId = @EmpresaId
ORDER BY Clave;", conn);
        permsCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });

        /*
          LO QUE NO COMPRUEBA NADA NO SE OFRECE COMO ADMINISTRABLE.

          Medido el 2026-10-12: de las 606 claves de WPermiso en MacZ (611 en
          Produccion_svr), 179 no las mira ningun endpoint, ninguna puerta de
          modulo y ningun boton de Mac31. Enseñarlas es peor que no tenerlas,
          porque la pantalla promete una decision que no existe: se desmarca y
          el acceso sigue abierto.

          QUE CUENTA COMO COMPROBACION, Y SON TRES COSAS
          ----------------------------------------------
            1. Que algun endpoint la exija (ClavesQueProtegenAlgo, por
               reflexion sobre este mismo ensamblado).
            2. Que sea la puerta de un modulo o una clave de reportes: las
               decide ModulosController / ExigirPermisoReporteAsync armando la
               clave en tiempo de ejecucion.
            3. QUE CORRESPONDA A UN BOTON DE MAC31. Esta es la que no se puede
               deducir del codigo y hay que preguntarle a la base de cada
               empresa: si la clave esta en el catalogo de un puente, marcarla
               escribe IDStatus = 1 en [Usuario Forma Procesos] y desmarcarla
               escribe 2. Eso es una decision REAL -la de Mac31- aunque el web
               no tenga todavia endpoint que la exija. Dejarla fuera habria
               escondido justo las casillas que gobiernan el sistema viejo.

          Y SE SIGUE MANDANDO LO QUE ALGUIEN YA TIENE, aunque no proteja nada,
          con ProtegeAlgo = false. Esconder una clave concedida la volveria
          inborrable desde la pantalla: quedaria para siempre en la base sin
          que nadie pueda verla ni quitarla.
        */
        var tenidasPorAlguien = await ClavesConcedidasAAlguienAsync(conn, empresaId, ct);

        await using var reader = await permsCmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var code = reader.GetString(reader.GetOrdinal("Clave"));
            var item = EnrichPermissionItem(code, reader.GetString(reader.GetOrdinal("Nombre")), reportCatalog, ventasCatalog, empleadosCatalog, prestamosCatalog, familiasCatalog);

            var protege = ClavesQueProtegenAlgo.Protege(code)
                || item.IsLegacyReport
                || item.LegacyProcessId is not null;

            if (!protege && !tenidasPorAlguien.Contains(code))
                continue;

            item.ProtegeAlgo = protege;
            response.Add(item);
        }

        return response;
    }

    /// <summary>
    /// Las claves que algun usuario o algun rol tiene guardadas hoy.
    /// </summary>
    /// <remarks>
    /// Se preguntan las DOS tablas porque una clave concedida por rol no deja
    /// rastro en WUsuarioPermiso, y esconderla la volveria igual de inborrable.
    /// Los nombres de columna salen de GetSchemaAsync y no escritos a mano: las
    /// dos bases de produccion no nacieron iguales.
    /// </remarks>
    private async Task<HashSet<string>> ClavesConcedidasAAlguienAsync(
        SqlConnection conn, int empresaId, CancellationToken ct)
    {
        var claves = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var schema = await GetSchemaAsync(conn, ct);

        var sql = $@"
SELECT p.Clave
FROM dbo.WPermiso p
WHERE p.EmpresaId = @EmpresaId
  AND (EXISTS (SELECT 1 FROM dbo.WUsuarioPermiso up
               WHERE up.EmpresaId = p.EmpresaId
                 AND up.{Q(schema.UserPermPermissionIdColumn)} = p.{Q(schema.PermissionIdColumn)})
    OR EXISTS (SELECT 1 FROM dbo.WRolPermiso rp
               WHERE rp.EmpresaId = p.EmpresaId
                 AND rp.{Q(schema.RolePermPermissionIdColumn)} = p.{Q(schema.PermissionIdColumn)}));";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            if (!r.IsDBNull(0)) claves.Add(r.GetString(0).Trim());

        return claves;
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
          Y EL INTERRUPTOR POR EMPRESA, QUE ES EL QUE AHORA DECIDE DE VERDAD.

          Esta pantalla movia solo `WModulo.IdStatus` y dejaba `WModuloEmpresa`
          tal cual. Mientras el calculo de permisos ignoraba el segundo daba
          igual; desde que lo mira —que es lo que cierra el acceso por URL
          directa— dejarlo quieto convertiria el interruptor de la pantalla en
          un boton que no termina de hacer su trabajo: el modulo seguiria
          apagado para la empresa aunque aqui dijera "activo".

          Peor aun con el sembrador ya corregido, que da de alta los modulos
          nuevos en `Activo = 0`: sin esto no habria **ninguna** forma de
          encenderlos desde la aplicacion, y harian falta manos en la base.

          Se escriben los dos a la vez y en la MISMA transaccion, para que no
          pueda quedar uno encendido y el otro apagado.

          Toca todas las claves de empresa de este EmpresaId. Es lo coherente con
          la pantalla, que administra la empresa del token y no distingue entre
          'zaragoza' y 'aviacion'; el dia que se quiera por clave, el SP
          `sp_w_ModuloEmpresa_SetStatus` ya existe para eso.
        */
        /*
          No se pregunta antes si la tabla existe: `TableExistsAsync` abre su
          propio comando SIN la transaccion, y con una transaccion local
          pendiente ADO.NET lo rechaza en seco ("BeginExecuteReader requires the
          command to have a transaction"). Se intenta el UPDATE y se tolera el
          208, que es "nombre de objeto no valido" — el unico caso en el que
          esta tabla puede faltar es una base que todavia no la tiene.
        */
        try
        {
            await using var porEmpresa = new SqlCommand(@"
UPDATE dbo.WModuloEmpresa
SET Activo = @Activo,
    FechaActualizacion = SYSUTCDATETIME()
WHERE EmpresaId = @EmpresaId
  AND ModuloClave = @ModuloClave;", conn, (SqlTransaction)tx);
            porEmpresa.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            porEmpresa.Parameters.Add(new SqlParameter("@ModuloClave", SqlDbType.NVarChar, 120) { Value = key });
            porEmpresa.Parameters.Add(new SqlParameter("@Activo", SqlDbType.Bit) { Value = idStatus == 1 });
            await porEmpresa.ExecuteNonQueryAsync(ct);
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            // Base sin WModuloEmpresa: solo manda WModulo.IdStatus, como antes.
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

        /*
          APAGAR UN MODULO TAMBIEN TIRA LA CACHE, Y DE TODA LA EMPRESA.

          Faltaba. El estatus del modulo ya entra en el calculo de permisos
          (LoadEffectivePermissionsAsync lo mira), asi que apagar uno CAMBIA el
          juego de permisos de cualquiera que lo tuviera — pero esta funcion no
          invalidaba nada y el snapshot viejo seguia sirviendose.

          Medido en un backend en Production (no Development, donde la cache
          esta puenteada): QA_REPORTES con "reportes.ver_modulo" en /api/me, se
          apaga el modulo por PUT /api/permisos-web/modulos/reportes/status,
          responde 200 y WModulo queda en IdStatus=2 — y el SIGUIENTE /api/me
          del mismo usuario seguia trayendo "reportes.ver_modulo". Hasta 5
          minutos por el TTL activo y hasta 30 por el camino stale, y volver a
          entrar no lo arreglaba porque la clave de cache no lleva el token.

          Es de EMPRESA y no de usuario porque un modulo no es de nadie en
          particular: apagarlo afecta a todos los que tengan permisos suyos, y
          aqui no se sabe quienes son sin recorrer la tabla entera.
        */
        InvalidateEmpresaPermissionCache(empresaId);
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

    public async Task UpsertUserOverridesAsync(int empresaId, Guid userId, IReadOnlyCollection<string> allow, IReadOnlyCollection<string> deny, bool sincronizarConLegacy, CancellationToken ct)
    {
        /* Era el UNICO punto de entrada que no abria la memoria de la peticion
           (ver AbrirMemoriaDePeticion), y es el que mas veces pregunta lo
           mismo: cinco sembradores de catalogo y cinco write-backs al mismo
           usuario. Sin ella, el catalogo de WPermiso y el estado de Mac31 de
           este usuario se leen una vez por pasada en vez de una vez. Muere al
           salir del metodo: no es una cache, es no preguntar diez veces lo que
           no puede haber cambiado dentro de la misma llamada. */
        using var memoriaDeLaPeticion = AbrirMemoriaDePeticion();

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

        /*
          El write-back a Mac31 solo corre cuando quien llama DECIDIO el estado
          completo del usuario. Ver el comentario de IPermissionService:
          llamarlo con las listas vacias desde el alta de usuarios revocaba
          todos los permisos de Mac31 de quien ya existia en el escritorio.
        */
        if (sincronizarConLegacy)
        {
            var userRole = await GetUserRoleAsync(conn, empresaId, userId, ct);
            await SyncLegacyReportPermissionsForUserAsync(conn, schema, empresaId, userId, userRole, reportCatalog, ct);
            await SyncLegacyModulePermissionsForUserAsync(conn, schema, empresaId, userId, userRole, ventasCatalog, ct);
            await SyncLegacyModulePermissionsForUserAsync(conn, schema, empresaId, userId, userRole, empleadosCatalog, ct);
            await SyncLegacyModulePermissionsForUserAsync(conn, schema, empresaId, userId, userRole, prestamosCatalog, ct);

            /*
              Y LAS FAMILIAS NUEVAS, QUE HASTA AHORA SOLO LEIAN.

              Con esto la sincronizacion es bidireccional tambien para las 22
              familias: lo que el administrador desmarca en la pantalla del web
              se apaga en Mac31, igual que ya pasaba con reportes, ventas,
              empleados y prestamos.

              POR QUE ES SEGURO HACERLO AHORA Y NO ANTES. El metodo escribe
              IDStatus = 2 para todo lo que no este en `effective`, y
              `effective` es SOLO lo del web (rol + overrides). Eso es correcto
              unicamente si quien guardo estaba viendo el estado real de Mac31.
              Lo esta: GetPermisosWebBootstrapAsync llama a
              AplicarFamiliasLegacyEnOverridesAsync, que mete lo de Mac31 dentro
              del Allow/Deny de cada usuario para estas mismas familias. Y
              ademas sigue puesto el candado de SyncLegacyModulePermissionsForUserAsync:
              un permiso solo se apaga si su modulo esta ENCENDIDO, o sea si la
              pantalla llego a ensenarlo.

              Esto NO se hace en UpsertRolePermissionsAsync, a proposito: un rol
              es concepto del web, y escribirlo en las filas de Mac31 de cada
              usuario que lo tenga seria una sobreescritura masiva de legacy.

              Va DESPUES de las cuatro viejas para que, si alguna clave cayera
              en dos catalogos, mande el calculo de la familia, que es el que
              conoce el prefijo y la forma exacta.
            */
            var catalogosDeFamilias = await EnsureFamiliasLegacySyncedAsync(conn, empresaId, ct);
            await SyncLegacyModulePermissionsForUserAsync(
                conn, schema, empresaId, userId, userRole,
                JuntarCatalogosDeFamilias(catalogosDeFamilias), ct);
        }

        InvalidateUserPermissionCache(userId, empresaId);
    }

    /// <summary>
    /// Tira la cache de permisos de UN usuario de esa empresa, para todos los
    /// roles y para todas las peticiones en vuelo. Ver _invalidadores.
    /// </summary>
    private void InvalidateUserPermissionCache(Guid userId, int empresaId)
    {
        DispararInvalidacion(AmbitoUsuario(userId, empresaId));

        /*
          LOS MEMOS DE RENDIMIENTO SE TIRAN CON LA MISMA LLAVE.

           guarda lo que Mac31 le concede a cada
          usuario durante unos minutos. Si se cambia un permiso y no se olvida
          aqui, la siguiente lectura devuelve lo de ANTES: justo lo que no puede
          pasar, porque incluye seguir ensenando un permiso ya revocado.

          Se tiran los de TODOS y no solo los de este usuario: cambiar un rol o
          una forma de Mac31 afecta a mas gente, y vaciar un diccionario de
          treinta entradas cuesta nada frente a equivocarse.
        */
        /* Los memos de rendimiento no viven mas alla de la peticion que los
           creo (ver _memoDeLaPeticion), asi que no hay nada que olvidar. */
    }

    /// <summary>
    /// Tira la cache de permisos de TODOS los usuarios de esa empresa. Es lo que
    /// corre cuando se cambian los permisos de un rol: afecta a cualquiera que
    /// lo tenga, no solo a quien hizo el cambio.
    /// </summary>
    private void InvalidateEmpresaPermissionCache(int empresaId)
    {
        DispararInvalidacion(AmbitoEmpresa(empresaId));

        /* Ver la nota de InvalidateUserPermissionCache: los memos de
           rendimiento caducan con la misma llave que el snapshot. */
        /* Los memos de rendimiento no viven mas alla de la peticion que los
           creo (ver _memoDeLaPeticion), asi que no hay nada que olvidar. */
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

    /*
      SIEMBRA COMPLETA, LA QUE CORRE AL ARRANCAR.

      `SyncPermissionCatalogAsync` siembra la lista ESTATICA del codigo. Pero hay
      familias que no se pueden escribir a mano porque salen de la base: una
      clave por cada centro de servicio y una por cada reporte del catalogo de
      Mac31. Esas las arman `EnsureCentros…` y `EnsureLegacyReport…`, y hasta
      ahora solo se llamaban al abrir la pantalla de permisos.

      El resultado era el mismo agujero que se creyo cerrado, pero mas estrecho:
      `reportes.centros.*` tenia CERO filas en WPermiso con 10 centros activos, y
      el selector de centros estaba muerto para todos menos el SuperAdmin.

      Lo encontro la auditoria final. El arreglo anterior cubrio la lista
      estatica y dio por hecho que lo demas venia con ella.
    */
    public async Task SembrarCatalogoCompletoAsync(int empresaId, CancellationToken ct)
    {
        /* La misma memoria de corta vida que usan las peticiones. Sin esto, la
           siembra vuelve a preguntar por cada una de las ~70 formas lo que ya
           pregunto: el catalogo de WPermiso de la empresa, las columnas, los
           modulos. Medido: es la diferencia entre ~500 viajes y unos pocos
           cientos, y es lo que tarda la primera peticion tras un arranque,
           porque espera a que esto termine. */
        using var memoria = AbrirMemoriaDePeticion();

        await SyncPermissionCatalogAsync(empresaId, null, ct);

        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);

        if (!await AreCapabilityTablesAvailableAsync(conn, ct))
            return;

        await EnsureCentrosPermissionsSyncedAsync(conn, empresaId, ct);
        await EnsureLegacyReportPermissionsSyncedAsync(conn, empresaId, ct);
        await EnsureVentasPermissionsSyncedAsync(conn, empresaId, ct);
        await EnsureLegacyModulePermissionsSyncedAsync(conn, empresaId, EmpleadosBinding, ct);
        await EnsureLegacyModulePermissionsSyncedAsync(conn, empresaId, PrestamosBinding, ct);

        // Las familias nuevas tambien se siembran en el arranque. Si no, sus
        // claves solo aparecerian en WPermiso la primera vez que alguien
        // abriera la pantalla de permisos, y hasta entonces el modulo
        // contestaria 403 a todos menos al SuperAdmin — exactamente el agujero
        // invisible que describe ClavesDePermisoSembradasTests.
        await EnsureFamiliasLegacySyncedAsync(conn, empresaId, ct);
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

        var response = new PermisosWebSyncCatalogResponse
        {
            TotalSeeds = seeds.Count
        };

        /*
          QUE CLAVES HAY YA, DE UN TIRON.

          Aqui se preguntaba una por una, DENTRO de la transaccion: 390 viajes a
          56 ms = ~22 s con candados tomados sobre WPermiso. Como esto corre al
          arrancar, toda peticion de permisos de ese primer medio minuto se
          quedaba esperando. Medido: /api/me en 31 s con el servidor recien
          levantado y un solo usuario.

          Una lectura y una comparacion en memoria dan exactamente el mismo
          resultado. Es lo que ya hacia SincronizarCatalogoDePermisosAsync.
        */
        var existentes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var leer = new SqlCommand(
            "SELECT Clave FROM dbo.WPermiso WHERE EmpresaId = @EmpresaId;", conn))
        {
            leer.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            await using var lector = await leer.ExecuteReaderAsync(ct);
            while (await lector.ReadAsync(ct))
            {
                if (!lector.IsDBNull(0)) existentes.Add(lector.GetString(0).Trim());
            }
        }

        var faltantes = seeds.Where(x => !existentes.Contains(x.Key)).ToList();
        foreach (var seed in seeds.Where(x => existentes.Contains(x.Key)))
            response.SkippedPermissions.Add(seed.Key);

        /* En regimen normal no falta ninguna y no se abre transaccion siquiera:
           la siembra del arranque cuesta UN viaje. */
        if (faltantes.Count > 0)
        {
            await using var tx = await conn.BeginTransactionAsync(ct);
            foreach (var seed in faltantes)
            {
                await InsertPermissionSeedAsync(conn, (SqlTransaction)tx, permColumns, empresaId, seed, null, ct);
                response.InsertedPermissions.Add(seed.Key);
            }
            await tx.CommitAsync(ct);
        }

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
        /* Memoria de ESTA peticion: lo que se pregunte dos veces aqui dentro se
           pregunta una. Se tira al salir (ver AbrirMemoriaDePeticion). */
        using var memoriaDeLaPeticion = AbrirMemoriaDePeticion();

        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);

        if (!await AreCapabilityTablesAvailableAsync(conn, ct))
            return BuildLegacyFallback(userId, empresaId, rolLegacy);

        var feature = await GetFeatureStatusAsync(conn, empresaId, ct);
        if (!feature.Enabled)
            return BuildLegacyFallback(userId, empresaId, rolLegacy, feature.Version);

        var schema = await GetSchemaAsync(conn, ct);
        await EnsureCatalogoSembradoAsync(conn, empresaId, ct);
        var permissions = await LoadEffectivePermissionsAsync(conn, schema, userId, empresaId, rolLegacy, ct);

        /*
          UN MODULO APAGADO TAMPOCO DEJA PASAR LO QUE VIENE DE MAC31.

          LoadEffectivePermissionsAsync ya filtra los modulos apagados, pero
          filtra lo que sale de la BASE. Los cuatro bloques de abajo -reportes,
          ventas, empleados y prestamos- AGREGAN claves despues de ese filtro,
          leyendolas de Mac31, asi que se lo saltaban enteras: con el modulo
          apagado para la empresa, sus permisos heredados seguian en el
          snapshot. `AplicarFamiliasLegacyAsync` ya lo filtraba para las once
          familias nuevas (ver su comentario); estas cuatro se habian quedado
          sin esa red.

          Importa el doble desde que la entrada se deriva: sin esto, apagar
          Ventas para una empresa no apagaba nada para quien la tuviera
          concedida en Mac31, y encima le derivaria su "ventas.ver_modulo".
        */
        var modulosApagados = await LoadInactiveModuleKeysAsync(conn, empresaId, ct);
        bool Pasa(string clave) => !EsDeModuloApagado(clave, modulosApagados);

        var reportCatalog = await EnsureLegacyReportPermissionsSyncedAsync(conn, empresaId, ct);
        var ventasCatalog = await EnsureVentasPermissionsSyncedAsync(conn, empresaId, ct);
        var empleadosCatalog = await EnsureLegacyModulePermissionsSyncedAsync(conn, empresaId, EmpleadosBinding, ct);
        var prestamosCatalog = await EnsureLegacyModulePermissionsSyncedAsync(conn, empresaId, PrestamosBinding, ct);
        if (reportCatalog.Count > 0)
        {
            var legacyReports = await LoadLegacyReportPermissionsForUserAsync(conn, empresaId, userId, reportCatalog, ct);
            permissions.RemoveAll(EsReporteQueMandaLegacy);
            permissions.AddRange(legacyReports.Allow.Where(Pasa).OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

            /*
              QUIEN TIENE UN REPORTE EN MAC31 ENTRA A REPORTES EN EL WEB.

              "reportes.ver_modulo" es una llave del web -Mac31 no la tiene- y
              hasta ahora no se derivaba de nada: habia que darla a mano desde
              la pantalla de permisos, encima de los reportes que Mac31 ya
              concede. Es la misma doble captura que este puente viene a quitar,
              y desde que la entrada se exige en el backend
              (EntradasDeModulo.cs, "reportes." -> "reportes.ver_modulo") es
              ademas un 403 en todos los reportes heredados.

              HOY NO LE CAMBIA NADA A NADIE, y se comprobo antes de ponerlo: en
              las dos empresas, todos los que tienen reportes en Mac31 ya tenian
              la entrada concedida por el web. Lo que cierra es la migracion de
              los que vengan: un empleado nuevo de Mac31 con sus reportes
              autorizados entra a Reportes sin que nadie le configure nada.

              Lo que ve DENTRO sigue siendo solo lo que Mac31 le autorice: la
              lista de reportes se dibuja con sus claves finas, una por una.
            */
            if (legacyReports.Allow.Count > 0
                && Pasa(ReportesViewPermission)
                && !permissions.Any(x => string.Equals(x, ReportesViewPermission, StringComparison.OrdinalIgnoreCase)))
            {
                permissions.Add(ReportesViewPermission);
            }
        }
        if (ventasCatalog.Count > 0)
        {
            var legacyVentas = await LoadVentasPermissionsForUserAsync(conn, empresaId, userId, ventasCatalog, ct);
            permissions.RemoveAll(x => x.StartsWith($"{VentasModuleKey}.", StringComparison.OrdinalIgnoreCase) && !VentasWebPermissionSeeds.Any(seed => seed.Key.Equals(x, StringComparison.OrdinalIgnoreCase)));
            permissions.AddRange(legacyVentas.Allow.Where(Pasa).OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

            /*
              QUIEN VENDE EN MAC31 ENTRA A VENTAS EN EL WEB.

              Este era el agujero mas grande de toda la herencia, y el que trajo
              el caso de MARINA BERNAL. Empleados y prestamos derivan su
              ".ver_modulo" desde abajo (y las once familias nuevas tambien),
              pero VENTAS -la familia mas usada- no derivaba nada: su
              "ventas.ver_modulo" es una semilla del web que Mac31 no produce.

              El efecto no se veia en la pantalla de permisos, porque los
              permisos finos SI se heredaban. Se veia al usar el modulo: desde
              que la entrada se exige tambien en el backend
              (EntradasDeModulo.cs, "ventas." -> "ventas.ver_modulo"), heredar
              veinte botones de ventas sin la entrada significa 403 en todos.

              MEDIDO el 2026-10-12 con la auditoria de herencia, ocho personas
              reales con cuenta web:
                Tauro     JESY (15 claves), MARINA BERNAL (11), NAOLANY (16)
                Zaragoza  FERNANDA (12), NORMA (5), RICARDO (4), USRTMP (4),
                          USRTMP2 (5)
              Todas podian hacer esas operaciones en Mac31 y ninguna en el web.

              CUALQUIER boton de las tres formas de ventas vale, igual que en
              las familias nuevas y por la misma razon medida alla: no hay una
              sola clave que cubra a todos -NORMA y RICARDO solo tienen los
              botones de PAGOS, no el de buscar-, y en Mac31 tener un boton de
              la forma es tener la forma en el menu. Lo que se pueda hacer
              adentro lo siguen decidiendo los permisos finos.

              NO SE DERIVA "ventas.ver": no hace falta. La pantalla del web
              acepta las dos llaves (VentasPage.tsx:1564) y el backend tambien
              (VentasConsultaController.cs:19). Derivarla de mas seria regalar
              una clave que nadie pide.
            */
            if (legacyVentas.Allow.Count > 0
                && Pasa(VentasViewPermission)
                && !permissions.Any(x => string.Equals(x, VentasViewPermission, StringComparison.OrdinalIgnoreCase)))
            {
                permissions.Add(VentasViewPermission);
            }
        }
        // Empleados: lo mismo que ventas. Si Mac31 no tiene la forma dada de
        // alta, empleadosCatalog viene vacio y NO se borra nada — asi una base
        // sin ese modulo se queda con lo que diga el web, en vez de quedarse
        // sin permisos de empleados de golpe.
        if (empleadosCatalog.Count > 0)
        {
            var legacyEmpleados = await LoadLegacyModulePermissionsForUserAsync(conn, empresaId, userId, EmpleadosBinding, empleadosCatalog, ct);
            permissions.RemoveAll(x => x.StartsWith($"{EmpleadosModuleKey}.", StringComparison.OrdinalIgnoreCase) && !EmpleadosWebPermissionSeeds.Any(seed => seed.Key.Equals(x, StringComparison.OrdinalIgnoreCase)));
            permissions.AddRange(legacyEmpleados.Allow.Where(Pasa).OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

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
            // CUALQUIER boton de la forma, no solo el de ver. Se iguala con las
            // once familias nuevas, donde la regla se midio y se cambio por
            // esto mismo: en Mac31 tener un boton de la forma es tener la forma
            // en el menu, y no hay una sola clave que cubra a todo el mundo.
            // Medido el 2026-10-12: hoy no cambia a nadie en ninguna de las dos
            // empresas -no existe nadie con botones de empleados y sin el de ver-,
            // asi que esto no abre ninguna puerta nueva; cierra la que se
            // abriria el dia que alguien capture ese caso en Mac31.
            if (legacyEmpleados.Allow.Count > 0
                && Pasa(EmpleadosViewPermission)
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
            permissions.AddRange(legacyPrestamos.Allow.Where(Pasa).OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

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
            // CUALQUIER boton de la forma, no solo el de ver. Se iguala con las
            // once familias nuevas, donde la regla se midio y se cambio por
            // esto mismo: en Mac31 tener un boton de la forma es tener la forma
            // en el menu, y no hay una sola clave que cubra a todo el mundo.
            // Medido el 2026-10-12: hoy no cambia a nadie en ninguna de las dos
            // empresas -no existe nadie con botones de prestamos y sin el de ver-,
            // asi que esto no abre ninguna puerta nueva; cierra la que se
            // abriria el dia que alguien capture ese caso en Mac31.
            if (legacyPrestamos.Allow.Count > 0
                && Pasa(PrestamosViewPermission)
                && !permissions.Any(x => string.Equals(x, PrestamosViewPermission, StringComparison.OrdinalIgnoreCase)))
            {
                permissions.Add(PrestamosViewPermission);
            }
        }

        // Las familias que se agregaron despues (compras, garantias, cortes,
        // cambios de vigencia, inventario, constantes y asistencias). Van al
        // FINAL a proposito: si alguna llegara a compartir prefijo con las
        // cuatro de arriba, lo de Mac31 de esta vuelta tiene que ser lo
        // ultimo que se escriba, no lo primero que otro bloque borre.
        var catalogosDeFamilias = await EnsureFamiliasLegacySyncedAsync(conn, empresaId, ct);
        await AplicarFamiliasLegacyAsync(conn, empresaId, userId, permissions, catalogosDeFamilias, ct);

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

    /// Si una clave de permiso pertenece a alguno de los modulos apagados.
    ///
    /// Publica para poder comprobarla: la regla parece obvia y tiene una trampa.
    /// El modulo es lo que va ANTES DEL PRIMER PUNTO, y se compara completo. Si
    /// se comparara "empieza con", apagar `reportes` apagaria tambien
    /// `reportesx`, y apagar `ventas` se llevaria por delante `ventas_moviles`.
    public static bool EsDeModuloApagado(string clave, HashSet<string> apagados)
    {
        if (string.IsNullOrWhiteSpace(clave)) return false;
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

        /*
          DOS INTERRUPTORES, Y HASTA AHORA SOLO SE MIRABA UNO.

          `WModulo.IdStatus = 2` es "apagado en toda la base" y era lo unico que
          se leia aqui. `WModuloEmpresa.Activo = 0` es "esta empresa no usa este
          modulo", que es el que mueve la pantalla Permisos -> Modulos, y se
          ignoraba por completo.

          El agujero: apagar un modulo para la empresa le quitaba la tarjeta del
          Inicio y NO le quitaba nada al API. El permiso seguia en el juego del
          usuario, asi que una peticion directa —o una URL pegada en el
          navegador— seguia funcionando. Ocultar no es cerrar.

          El `HAVING MAX(...) = 0` pide que este apagado para TODAS las claves de
          empresa de ese EmpresaId, no para una. Es a proposito: `MacZ` tiene dos
          ('zaragoza' y 'aviacion') compartiendo EmpresaId, y aqui no llega la
          del token. Quitar el permiso cuando una de las dos lo tiene encendido
          se lo quitaria a la empresa equivocada, y ese error se paga en accesos
          perdidos; al reves solo se paga en una comprobacion de mas, que el
          endpoint vuelve a hacer igualmente.

          Si no hay tabla por empresa, se queda como antes: no se puede
          comprobar, y lo que no se puede comprobar no se recorta.
        */
        var hayTablaPorEmpresa = await TableExistsAsync(conn, "WModuloEmpresa", ct);

        await using var cmd = new SqlCommand(hayTablaPorEmpresa ? @"
SELECT ModuloClave
FROM dbo.WModulo
WHERE EmpresaId = @EmpresaId
  AND IdStatus = 2
UNION
SELECT me.ModuloClave
FROM dbo.WModuloEmpresa me
WHERE me.EmpresaId = @EmpresaId
GROUP BY me.ModuloClave
HAVING MAX(CAST(me.Activo AS int)) = 0;" : @"
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

    /*
      Las columnas de una tabla no cambian sin un despliegue, y esto se llamaba
      UNA VEZ POR FORMA de Mac31 -~70 por reconstruccion de catalogo-. Mismo
      plazo que el resto del esquema.
    */
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (HashSet<string> Columnas, DateTime Hasta)>
        _memoDeColumnas = new(StringComparer.OrdinalIgnoreCase);

    private static async Task<HashSet<string>> GetTableColumnsAsync(
        SqlConnection conn,
        string tableName,
        CancellationToken ct)
    {
        var llave = $"{conn.Database}|{tableName}";
        if (_memoDeColumnas.TryGetValue(llave, out var memorizado) && memorizado.Hasta > DateTime.UtcNow)
            return memorizado.Columnas;

        var leidas = await GetTableColumnsSinMemoriaAsync(conn, tableName, ct);
        _memoDeColumnas[llave] = (leidas, DateTime.UtcNow.Add(SchemaCacheTtl));
        return leidas;
    }

    private static async Task<HashSet<string>> GetTableColumnsSinMemoriaAsync(
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
        /* Mismo caso que ColumnExistsAsync: ver _memoDeEsquema. */
        var llave = $"tab|{conn.Database}|{tableName}";
        if (_memoDeEsquema.TryGetValue(llave, out var guardado) && guardado.Hasta > DateTime.UtcNow)
            return guardado.Existe;

        await using var cmd = new SqlCommand(@"
SELECT CASE WHEN OBJECT_ID(@TableName, 'U') IS NOT NULL THEN 1 ELSE 0 END;", conn);
        cmd.Parameters.Add(new SqlParameter("@TableName", SqlDbType.NVarChar, 260) { Value = $"dbo.{tableName}" });
        var value = await cmd.ExecuteScalarAsync(ct);

        var existe = value is not null && Convert.ToInt32(value) == 1;
        _memoDeEsquema[llave] = (existe, DateTime.UtcNow.Add(SchemaCacheTtl));
        return existe;
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

    /*
      DA DE ALTA LOS PERMISOS DE CENTROS DE SERVICIO, LEYENDOLOS DE LA TABLA.

      Son dos cosas distintas y a proposito:

        reportes.centros.cambiar        -> puede usar el selector. Sin esto no
                                           lo ve siquiera, y todo sale de la
                                           base principal como hasta hoy.
        reportes.centros.<numero>.ver   -> uno por centro: a cuales puede entrar.

      Separarlos es lo que permite "apagale el selector aunque tenga centros":
      quitando el primero se acabo, sin tocar los doce de abajo.

      Se generan desde dbo.CentrosServicio y no a mano en un script: el dia que
      den de alta el centro 2013 aparece solo en la pantalla de permisos. Se usa
      Numero y no IDCentro en la clave porque Numero es lo que la gente reconoce
      (2004 = ORIENTE) y es estable entre bases.

      Si la tabla no existe —empresa sin centros— no se crea nada y todo sigue
      igual.
    */
    /*
      LAS CLAVES NUEVAS TIENEN QUE LLEGAR A LA BASE SOLAS.

      PermissionCatalogSeeds es la lista de permisos que declara el codigo. Hasta
      ahora solo se escribia en WPermiso cuando alguien entraba a la pantalla de
      permisos y pulsaba sincronizar: BuildSnapshotAsync no la llamaba nunca.

      La consecuencia no se veia de frente. Un permiso que no esta en WPermiso no
      puede tener fila de concesion, asi que el endpoint que lo exige contesta 403
      a TODO EL MUNDO menos al SuperAdmin -que pasa antes de mirar nada-. O sea
      que el modulo quedaba escrito, probado y comprobado contra Legacy... y
      muerto para los usuarios de verdad. En esta base habia 0 filas para
      catalogos.*, obra.*, cobranza.* y productos.*, y 68 capacidades ya migradas
      caian ahi.

      Las demas familias ya se sembraban solas (reportes, ventas, empleados,
      prestamos, centros). Esto hace lo mismo con las del catalogo, que es lo que
      todo el mundo daba por hecho que pasaba.

      Sembrar NO es conceder: crea la clave para que se pueda repartir desde la
      pantalla de permisos. Quien no la tenga concedida sigue sin poder.
    */
    private static async Task EnsureCatalogoSembradoAsync(
        SqlConnection conn,
        int empresaId,
        CancellationToken ct)
    {
        await SincronizarCatalogoDePermisosAsync(
            conn, empresaId, PermissionCatalogSeeds, seed => seed.Module, ct);
    }

    private static async Task EnsureCentrosPermissionsSyncedAsync(
        SqlConnection conn,
        int empresaId,
        CancellationToken ct)
    {
        if (!await TableExistsAsync(conn, "CentrosServicio", ct))
            return;

        var semillas = new List<PermissionSeed>
        {
            new(PermisosDeCentros.Cambiar, "Reportes - Cambiar de centro", ReportesModuleKey)
        };

        await using (var cmd = new SqlCommand(@"
SELECT Numero, NombreCentro
FROM dbo.CentrosServicio
WHERE IDStatus = 1 AND Numero IS NOT NULL
ORDER BY Numero;", conn))
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var numero = reader.GetInt32(0);
                var nombre = (reader.IsDBNull(1) ? "" : reader.GetString(1)).Trim();
                if (numero <= 0) continue;
                semillas.Add(new PermissionSeed(
                    PermisosDeCentros.Ver(numero),
                    $"Reportes - Centro {(nombre.Length > 0 ? nombre : numero.ToString())}",
                    ReportesModuleKey));
            }
        }

        await SincronizarCatalogoDePermisosAsync(
            conn, empresaId, semillas, _ => "reportes.centros", ct);
    }

    /// <summary>
    /// El catalogo de reportes de Mac31, memorizado. Ver <see cref="CatalogoCacheTtl"/>.
    /// </summary>
    private async Task<Dictionary<string, ReportPermissionInfo>> EnsureLegacyReportPermissionsSyncedAsync(
        SqlConnection conn,
        int empresaId,
        CancellationToken ct)
        => new(await CatalogoMemorizadoAsync(
                $"reportes|{conn.Database}|{empresaId}",
                () => EnsureLegacyReportPermissionsSyncedSinMemoriaAsync(conn, empresaId, ct)),
            StringComparer.OrdinalIgnoreCase);

    private async Task<Dictionary<string, ReportPermissionInfo>> EnsureLegacyReportPermissionsSyncedSinMemoriaAsync(
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
-- Mismo arreglo que en WPermiso y por el mismo motivo: dos sembradores a la
-- vez (el de arranque y el de la primera peticion) insertaban los dos el
-- mismo modulo nuevo. UPDATE primero, INSERT solo si no habia.
UPDATE dbo.WModulo
   SET Nombre = @Nombre,
       IdStatus = CASE WHEN IdStatus = 2 THEN 2 ELSE 1 END,
       FechaActualizacion = CASE WHEN COL_LENGTH('dbo.WModulo','FechaActualizacion') IS NOT NULL THEN SYSUTCDATETIME() ELSE FechaActualizacion END
 WHERE EmpresaId = @EmpresaId
   AND ModuloClave = @ModuloClave;

IF @@ROWCOUNT = 0
BEGIN
    INSERT INTO dbo.WModulo (EmpresaId, ModuloClave, Nombre, IdStatus, FechaCreacion, FechaActualizacion)
    SELECT @EmpresaId, @ModuloClave, @Nombre, 1, SYSUTCDATETIME(), SYSUTCDATETIME()
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.WModulo WITH (UPDLOCK, HOLDLOCK)
        WHERE EmpresaId = @EmpresaId AND ModuloClave = @ModuloClave);
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
WHERE LTRIM(RTRIM(f.Forma)) = @FormaReportes
  AND ISNULL(p.IDStatus, 1) = 1
ORDER BY COALESCE(f.Orden, 0), COALESCE(p.Orden, 0), p.IDProceso;", conn);
        cmd.Parameters.Add(new SqlParameter("@FormaReportes", SqlDbType.NVarChar, 200) { Value = ReportesLegacyForm });

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

        /*
          DEL MISMO LOTE QUE LAS DEMAS FAMILIAS.

          Esta consulta pedia otra vez n_Formas/n_Procesos para UNA forma, la de
          reportes, y se repetia por usuario: 25 viajes mas en cada bootstrap.
           ya trae TODO lo del usuario en una sola
          consulta, asi que aqui solo hay que quedarse con la forma de reportes.

          El filtro imita al WHERE que sustituye: , y SQL compara sin distinguir mayusculas.
        */
        foreach (var fila in (await ProcesosDeUsuarioAsync(conn, legacyUserId.Value, ct))
                     .Where(f => string.Equals(f.Forma.Trim(), ReportesLegacyForm.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            if (!byProcessId.TryGetValue(fila.IdProceso, out var key))
                continue;
            if (fila.Permitido) allow.Add(key);
            else deny.Add(key);
        }

        return (allow, deny);
    }

    /*
      ================================================================
      EL WRITE-BACK A MAC31: SOLO LO QUE CAMBIA
      ================================================================

      EL PROBLEMA, MEDIDO
      -------------------
      Guardar permisos de UN usuario llamaba a
      `sp_n_ActualizarUsuarioFormaProceso` una vez por cada entrada del
      CATALOGO —no por cada cambio—: una vuelta desde
      SyncLegacyReportPermissionsForUserAsync y cuatro desde
      SyncLegacyModulePermissionsForUserAsync, cada vuelta un
      `ExecuteNonQueryAsync` propio con su BEGIN TRAN/COMMIT dentro del SP.
      Medido con Extended Events en las dos empresas: ~330 viajes por guardado
      (hay 330 procesos activos en MacZ y 332 en Produccion_svr). Mover UN
      permiso costaba lo mismo que mover un modulo entero, porque el bucle
      recorria el catalogo y no el cambio. Los necesarios son ~8.

      LA CORRECCION
      -------------
      Se lee UNA vez el estado que ya tiene el usuario en
      `[Usuario Forma Procesos]`, se calcula la diferencia contra lo que se
      quiere dejar, y se llama al procedimiento SOLO por los procesos que de
      verdad cambian — todos en un unico viaje.

      `sp_n_ActualizarUsuarioFormaProceso` NO SE TOCA: es un `sp_n_`. Lo que
      cambia es cuantas veces se le llama, no lo que hace.

      LO QUE **NO** SE HACE, Y ES LA PARTE IMPORTANTE
      ----------------------------------------------
      No se guarda de forma optimista. Cuando la pantalla diga que el permiso
      se guardo, la escritura esta CONFIRMADA: el lote se manda con
      `ExecuteNonQueryAsync` y se espera su resultado, igual que antes; lo que
      se quita son los viajes que no escribian nada, no la confirmacion.

      Tampoco hay cache nueva de permisos: el estado de Mac31 se recuerda
      durante ESTA peticion y nada mas (muere con ella, ver
      AbrirMemoriaDePeticion), y no participa en decidir si alguien tiene un
      permiso — solo en decidir si hace falta reescribir una fila que ya dice
      lo mismo. El esquema de autorizacion no se toca en absoluto.
    */

    /// <summary>
    /// El estado que el usuario YA tiene en `[Usuario Forma Procesos]`:
    /// IDProceso -> IDStatus. Se lee una vez por peticion y se mantiene al dia
    /// a mano cuando se escribe.
    /// </summary>
    private static async Task<Dictionary<int, int>> LeerEstadoLegacyDelUsuarioAsync(
        SqlConnection conn, int legacyUserId, CancellationToken ct)
    {
        var llave = $"estadoLegacyUsuario|{conn.Database}|{legacyUserId}";
        var memoria = _memoDeLaPeticion.Value;

        if (memoria is not null
            && memoria.TryGetValue(llave, out var guardado)
            && guardado is Dictionary<int, int> vigente)
        {
            return vigente;
        }

        var estado = new Dictionary<int, int>();
        await using (var cmd = new SqlCommand(
            "SELECT IDProceso, IDStatus FROM dbo.[Usuario Forma Procesos] WHERE IDUsuario = @IDUsuario;", conn))
        {
            cmd.Parameters.Add(new SqlParameter("@IDUsuario", SqlDbType.Int) { Value = legacyUserId });
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                if (reader.IsDBNull(0)) continue;
                /* Si el mismo proceso sale dos veces, gana el ACTIVO: es la
                   misma regla de "cualquier si gana" que usa el agrupado por
                   IDProceso de mas abajo, y equivocarse hacia el lado de
                   "ya esta activo" solo puede hacer que se escriba de mas,
                   nunca que se deje de conceder algo. */
                var proceso = reader.GetInt32(0);
                var status = reader.IsDBNull(1) ? 2 : reader.GetInt32(1);
                if (!estado.TryGetValue(proceso, out var previo) || status == 1 || previo != 1)
                    estado[proceso] = status;
            }
        }

        if (memoria is not null) memoria[llave] = estado;
        return estado;
    }

    /// <summary>
    /// QUE PROCESOS HAY QUE ESCRIBIR DE VERDAD. Es la decision entera del
    /// arreglo, y esta aparte para que se pueda probar sin base: es puramente
    /// comparar dos diccionarios.
    /// </summary>
    /// <param name="deseado">IDProceso -> IDStatus que se quiere dejar.</param>
    /// <param name="estadoActual">IDProceso -> IDStatus que ya hay en Mac31.</param>
    public static List<KeyValuePair<int, int>> CambiosQueHacenFalta(
        IReadOnlyDictionary<int, int> deseado,
        IReadOnlyDictionary<int, int> estadoActual)
    {
        var cambios = new List<KeyValuePair<int, int>>();

        foreach (var par in deseado)
        {
            var hayFila = estadoActual.TryGetValue(par.Key, out var actual);

            // Ya dice lo mismo: no se escribe. Es el 97% de los viajes viejos.
            if (hayFila && actual == par.Value) continue;

            /* SIN FILA Y SE PIDE BAJA: `sp_n_ActualizarUsuarioFormaProceso` no
               inserta nada con IDStatus = 2 y sin fila previa —esta
               documentado en SyncLegacyModulePermissionsForUserAsync y asi lo
               hace Mac31 en su propia pantalla—, asi que el viaje no haria
               absolutamente nada. No escribir aqui no es una decision nueva:
               es no pagar un viaje cuyo efecto ya era cero.

               OJO CON LA TENTACION DE SIMPLIFICARLO a `if (!hayFila && par.Value
               == 2)`: cualquier IDStatus que no sea 1 es una baja, y tratar un
               valor raro como alta SI insertaria una fila. Se compara contra 1,
               que es el unico que concede. */
            if (!hayFila && par.Value != 1) continue;

            cambios.Add(par);
        }

        return cambios;
    }

    /// <summary>
    /// Deja los procesos que CAMBIAN en el estado que se pide, en un solo
    /// viaje, y actualiza el estado recordado.
    /// </summary>
    /// <param name="deseado">IDProceso -> IDStatus que se quiere dejar (1 alta, 2 baja).</param>
    private static async Task<int> EscribirCambiosLegacyAsync(
        SqlConnection conn,
        int legacyUserId,
        int movementUserId,
        IReadOnlyDictionary<int, int> deseado,
        Dictionary<int, int> estadoActual,
        CancellationToken ct)
    {
        var cambios = CambiosQueHacenFalta(deseado, estadoActual);
        if (cambios.Count == 0) return 0;

        /*
          UN SOLO VIAJE PARA TODOS LOS CAMBIOS.

          Cada EXEC sigue siendo una llamada completa al procedimiento, con su
          propia transaccion: el lote no las junta en una sola transaccion ni
          las cambia en nada: solo ahorra la ida y vuelta por la red. Se
          numeran los parametros porque un lote no puede reusar el mismo
          nombre con dos valores.

          Y NO SE TROCEA EL AWAIT: se espera a que el lote entero termine antes
          de contestar, porque la pantalla va a decir "guardado" en cuanto esto
          vuelva.
        */
        var sql = new System.Text.StringBuilder();
        await using var cmd = new SqlCommand { Connection = conn, CommandType = CommandType.Text };
        cmd.Parameters.Add(new SqlParameter("@IDUsuario", SqlDbType.Int) { Value = legacyUserId });
        cmd.Parameters.Add(new SqlParameter("@IDUsuarioMovimiento", SqlDbType.Int) { Value = movementUserId });

        for (var i = 0; i < cambios.Count; i++)
        {
            sql.Append("EXEC dbo.sp_n_ActualizarUsuarioFormaProceso @IDUsuario, @P")
               .Append(i).Append(", @S").Append(i).Append(", @IDUsuarioMovimiento;\n");
            cmd.Parameters.Add(new SqlParameter($"@P{i}", SqlDbType.Int) { Value = cambios[i].Key });
            cmd.Parameters.Add(new SqlParameter($"@S{i}", SqlDbType.Int) { Value = cambios[i].Value });
        }

        cmd.CommandText = sql.ToString();
        await cmd.ExecuteNonQueryAsync(ct);

        /*
          EL ESTADO RECORDADO TIENE QUE QUEDAR AL DIA, Y NO ES UN DETALLE.

          A este write-back se entra CINCO veces en el mismo guardado, y un
          mismo IDProceso puede caer en dos catalogos —esta documentado arriba,
          en UpsertUserOverridesAsync: "si alguna clave cayera en dos
          catalogos, mande el calculo de la familia", que es el que va ultimo—.

          Si el estado recordado se quedara con la foto de antes de escribir,
          la quinta pasada compararia contra un valor que ya no esta en la base
          y podria decidir "no hace falta tocarlo" cuando si hace falta: se
          quedaria lo que escribio la primera pasada y ganaria la que no debe.
          Dejandolo al dia, la ultima pasada sigue mandando, que es como se
          comportaba antes de agrupar.
        */
        foreach (var cambio in cambios)
            estadoActual[cambio.Key] = cambio.Value;

        return cambios.Count;
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

        /*
          SE OTORGA Y TAMBIEN SE QUITA, PERO SOLO SI LA PANTALLA LO ENSEÑO.

          Antes esto solo otorgaba. La razon era buena —guardar desde el web le
          estaba borrando a la gente sus reportes de Mac31— pero dejaba una
          puerta de un solo sentido: dabas un reporte, se escribia en Mac31, y
          al volver a leer lo de Mac31 sustituia a lo del web, asi que la
          casilla volvia a salir encendida y ya no habia forma de quitarla
          desde ninguna de las dos pantallas.

          Medido en produccion de Zaragoza (28-sep-2026): tres guardados
          seguidos, uno con ocho reportes y dos sin ellos, los tres con
          respuesta 200, y los ocho seguian encendidos en Mac31.

          Que lo hace seguro ahora y no antes: el bootstrap ya mezcla lo de
          Mac31 dentro de las casillas, asi que quien desmarca esta viendo el
          estado de verdad y lo esta quitando a proposito. Antes el web no sabia
          lo que habia en Mac31 y lo pisaba sin enterarse.

          EL CANDADO: solo se toca si el modulo 'reportes' esta ENCENDIDO. Con
          el modulo apagado sus permisos se caen del catalogo y la pantalla no
          los enseña; sin este candado, guardar a alguien cualquier otra cosa le
          apagaria en Mac31 todos los reportes que nadie vio ni pidio quitar.
          Ese es exactamente el accidente viejo.
        */
        var activeModules = await GetActiveModulesAsync(conn, empresaId, ct);
        var puedeQuitar = IsPermissionInActiveModule(ReportesViewPermission, activeModules);

        /* Se arma lo que se QUIERE dejar y se escribe solo la diferencia. Ver
           el bloque de LeerEstadoLegacyDelUsuarioAsync: antes esto eran tantos
           viajes como reportes tuviera el catalogo. */
        var deseado = new Dictionary<int, int>();
        foreach (var report in reportCatalog.Values)
        {
            var loTiene = effective.Contains(report.Key);
            if (!loTiene && !puedeQuitar)
                continue;

            /* Si dos reportes comparten IDProceso, cualquier SI gana: la misma
               regla que el agrupado de SyncLegacyModulePermissionsForUserAsync,
               y por la misma razon (nunca convertir un detalle del catalogo en
               una revocacion de un permiso real de Mac31). */
            if (loTiene || !deseado.ContainsKey(report.LegacyProcessId))
                deseado[report.LegacyProcessId] = loTiene ? 1 : 2;
        }

        var estadoActual = await LeerEstadoLegacyDelUsuarioAsync(conn, legacyUserId.Value, ct);
        await EscribirCambiosLegacyAsync(
            conn, legacyUserId.Value, movementUserId, deseado, estadoActual, ct);
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
    /// <summary>
    /// El catalogo de VENTAS (sus tres formas), memorizado por base y empresa.
    /// </summary>
    /// <remarks>
    /// Mismo motivo que las familias: se reconstruia y RESINCRONIZABA en cada
    /// peticion de permisos. Se memoriza el conjunto -no forma por forma- y se
    /// devuelve copia, porque quien lo recibe puede juntarlo con otros.
    /// </remarks>
    private async Task<Dictionary<string, VentasPermissionInfo>> EnsureVentasPermissionsSyncedAsync(
        SqlConnection conn,
        int empresaId,
        CancellationToken ct)
        => new(await CatalogoMemorizadoAsync(
                $"ventas|{conn.Database}|{empresaId}",
                () => EnsureVentasPermissionsSyncedSinMemoriaAsync(conn, empresaId, ct)),
            StringComparer.OrdinalIgnoreCase);

    private async Task<Dictionary<string, VentasPermissionInfo>> EnsureVentasPermissionsSyncedSinMemoriaAsync(
        SqlConnection conn,
        int empresaId,
        CancellationToken ct)
    {
        var catalogo = await EnsureLegacyModulePermissionsSyncedAsync(conn, empresaId, VentasBinding, ct);
        var pagos = await EnsureLegacyModulePermissionsSyncedAsync(conn, empresaId, VentasPagosBinding, ct);
        var usados = await EnsureLegacyModulePermissionsSyncedAsync(conn, empresaId, VentasUsadosBinding, ct);
        foreach (var par in pagos)
            catalogo[par.Key] = par.Value;
        foreach (var par in usados)
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
        var usados = await LoadLegacyModulePermissionsForUserAsync(conn, empresaId, userId, VentasUsadosBinding, catalogo, ct);

        consulta.Allow.UnionWith(pagos.Allow);
        consulta.Allow.UnionWith(usados.Allow);
        consulta.Deny.UnionWith(pagos.Deny);
        consulta.Deny.UnionWith(usados.Deny);
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

        /*
          NO REESCRIBIR UN NOMBRE QUE YA ESTA BIEN.

          Esto corria por CADA forma -~70 por peticion de permisos- y escribia
          siempre, aunque el nombre del modulo fuera identico al que ya estaba.
          Setenta escrituras dentro de una lectura, sobre filas que comparten
          todos los usuarios de la empresa: ademas de los viajes, es contencion
          en SQL entre peticiones de agentes distintos.

          Se lee WModulo una vez por peticion y solo se escribe lo que de verdad
          difiere. En regimen normal eso son CERO escrituras.
        */
        var yaEstaBien = false;
        if (await TableExistsAsync(conn, "WModulo", ct))
        {
            var llaveModulos = $"catalogoWModulo|{conn.Database}|{empresaId}";
            var memoriaModulos = _memoDeLaPeticion.Value;
            Dictionary<string, string>? modulos = null;

            if (memoriaModulos is not null
                && memoriaModulos.TryGetValue(llaveModulos, out var guardadoMod)
                && guardadoMod is Dictionary<string, string> vigenteMod)
            {
                modulos = vigenteMod;
            }

            if (modulos is null)
            {
                modulos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                await using (var lectura = new SqlCommand(
                    "SELECT ModuloClave, ISNULL(Nombre, '') AS Nombre, ISNULL(IdStatus, 0) AS IdStatus FROM dbo.WModulo WHERE EmpresaId = @EmpresaId;", conn))
                {
                    lectura.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
                    await using var lector = await lectura.ExecuteReaderAsync(ct);
                    while (await lector.ReadAsync(ct))
                    {
                        /* El estado va en la llave del valor porque la escritura
                           tambien lo normaliza (1, salvo que sea 2). Sin esto, un
                           modulo con estado raro se quedaria sin corregir. */
                        if (!lector.IsDBNull(0))
                            modulos[lector.GetString(0).Trim()] = lector.GetString(1) + "\u0001" + lector.GetInt32(2);
                    }
                }
                if (memoriaModulos is not null) memoriaModulos[llaveModulos] = modulos;
            }

            var esperado = binding.ModuleName + "\u0001";
            yaEstaBien = modulos.TryGetValue(binding.ModuleKey, out var actualModulo)
                         && (actualModulo == esperado + "1" || actualModulo == esperado + "2");

            if (!yaEstaBien) modulos[binding.ModuleKey] = esperado + "1";
        }

        if (!yaEstaBien && await TableExistsAsync(conn, "WModulo", ct))
        {
            await using var moduleCmd = new SqlCommand(@"
-- Mismo arreglo que en WPermiso y por el mismo motivo: dos sembradores a la
-- vez (el de arranque y el de la primera peticion) insertaban los dos el
-- mismo modulo nuevo. UPDATE primero, INSERT solo si no habia.
UPDATE dbo.WModulo
   SET Nombre = @Nombre,
       IdStatus = CASE WHEN IdStatus = 2 THEN 2 ELSE 1 END,
       FechaActualizacion = CASE WHEN COL_LENGTH('dbo.WModulo','FechaActualizacion') IS NOT NULL THEN SYSUTCDATETIME() ELSE FechaActualizacion END
 WHERE EmpresaId = @EmpresaId
   AND ModuloClave = @ModuloClave;

IF @@ROWCOUNT = 0
BEGIN
    INSERT INTO dbo.WModulo (EmpresaId, ModuloClave, Nombre, IdStatus, FechaCreacion, FechaActualizacion)
    SELECT @EmpresaId, @ModuloClave, @Nombre, 1, SYSUTCDATETIME(), SYSUTCDATETIME()
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.WModulo WITH (UPDLOCK, HOLDLOCK)
        WHERE EmpresaId = @EmpresaId AND ModuloClave = @ModuloClave);
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

    /// <summary>
    /// Como se busca la forma en n_Formas: por nombre si la atadura lo dice,
    /// por descripcion si no. Las dos consultas tienen que usar EL MISMO
    /// filtro: el catalogo y los permisos del usuario se cruzan por IDProceso,
    /// y si una mirara una forma y la otra otra, el cruce quedaria vacio sin
    /// dar error.
    /// </summary>
    private static string FiltroDeForma(LegacyModuleBinding binding) =>
        binding.FormaExacta is null
            ? "UPPER(LTRIM(RTRIM(f.Descripcion))) = @Forma"
            : "f.Forma = @Forma";

    private static string ValorDeForma(LegacyModuleBinding binding) =>
        binding.FormaExacta ?? binding.LegacyForm;

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
WHERE " + FiltroDeForma(binding) + @"
  AND ISNULL(p.IDStatus, 1) = 1
ORDER BY COALESCE(p.Orden, 0), p.IDProceso;", conn);
        cmd.Parameters.Add(new SqlParameter("@Forma", SqlDbType.NVarChar, 200) { Value = ValorDeForma(binding) });

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
            // El mapa de la familia MANDA. Solo si no mapeo este boton a mano
            // se intenta derivar la clave con la tabla de verbos compartida
            // (VerbosLegacy), que es lo que evita escribir a mano los 226
            // botones que se llaman btnNuevo/btnVer/btnModificar. Si tampoco
            // esta ahi, el proceso se ignora igual que antes.
            if (!binding.Map.TryGetValue(processCode.Trim(), out var mapped))
            {
                var ordDesc = reader.GetOrdinal("ProcesoDescripcion");
                var derivada = DerivarClaveLegacy(
                    processCode.Trim(),
                    binding.Prefijo ?? binding.ModuleKey,
                    binding.ModuleName,
                    reader.IsDBNull(ordDesc) ? null : reader.GetString(ordDesc));
                if (derivada is null)
                    continue;
                mapped = derivada.Value;
            }

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

        /*
          DOS BOTONES DE MAC31 PUEDEN PRODUCIR LA MISMA CLAVE, Y ANTES SOLO
          CONTABA UNO.

          Aqui habia `ventasCatalog.Values.ToDictionary(x => x.LegacyProcessId)`,
          o sea: se miraba UN SOLO IDProceso por clave, el que el catalogo
          hubiera guardado. Y el catalogo guarda uno a proposito —lo necesita
          para saber que boton encender cuando se concede desde el web—, asi que
          cuando dos botones derivan a la misma clave, el OTRO boton quedaba
          invisible: quien lo tuviera concedido en Mac31 no heredaba el permiso,
          sin error y sin rastro.

          MEDIDO el 2026-10-12 con la auditoria de herencia, y no era teorico:

            garantias.ver        btnBuscar de CONSULTA DE GARANTIAS (mapeado)
                                 contra btnVer de CONSULTA DE REGISTRO DE
                                 GARANTIAS (derivado). Ganaba el segundo, que es
                                 la ultima forma de la familia -> perdian el
                                 permiso JESY y NAOLANY en Tauro y QA_LEGWEB en
                                 Zaragoza.
            cortes.imprimir      btnImprimir de CORTES contra btnImprimir de
                                 CORTES CENTRO DE SERVICIO, los dos derivados
                                 con el mismo prefijo -> perdian JESY y NAOLANY.
            cambiosvigencia.ver  btnBuscar (mapeado, Orden 1) contra btnVer
                                 (derivado, Orden 10) de la MISMA forma; aqui
                                 gana el primero -> perdia RICARDO, que tiene
                                 btnVer y no btnBuscar.

          LA REGLA CORRECTA: la clave se concede si Mac31 le concedio CUALQUIERA
          de los botones que la producen. Por eso la clave se resuelve POR
          PROCESO -con el mismo mapa y la misma tabla de verbos que el catalogo-
          en vez de leerse de un diccionario de un solo IDProceso.

          NO ENSANCHA NADA: solo se acepta la clave si el catalogo la publica
          (`ventasCatalog.ContainsKey`), asi que no puede aparecer una clave que
          esta familia no gobierne. Y el sentido de ESCRITURA sigue usando el
          IDProceso unico del catalogo, que es donde esa eleccion si hace falta.
        */
        /*
          DEL LOTE, NO DE UNA CONSULTA PROPIA.

          Esta consulta era identica para las 71 familias salvo el filtro de la
          forma, y se repetia por usuario: cientos de viajes en el bootstrap.
          Ahora se pide UNA VEZ todo lo del usuario (ProcesosDeUsuarioAsync) y
          aqui solo se queda con las filas de ESTA forma.

          El filtro en memoria es el mismo que hacia el WHERE: por `f.Forma`
          cuando el binding trae `FormaExacta`, y por la descripcion en
          mayusculas y sin espacios cuando no. Ver FiltroDeForma.
        */
        var valorDeForma = ValorDeForma(binding);
        /*
          SE COMPARA COMO LO COMPARA SQL, NO COMO LO COMPARA C#.

          El WHERE que esto sustituye lo resolvia SQL Server con la colacion de
          la base: SIN distinguir mayusculas y SIN mirar los espacios del final
          ('ConsultarMarcas' = 'consultarmarcas ' es verdadero alla). Un
           aqui es exacto y habria dejado fuera
          cualquier forma que difiera en una mayuscula o traiga un espacio de
          mas -y las hay: estos nombres se capturaron a mano durante anios-.

          Esa familia habria dejado de heredar permisos de Mac31 en silencio,
          que es la peor forma de romper esto. Por eso OrdinalIgnoreCase y
          TrimEnd.
        */
        var delLote = (await ProcesosDeUsuarioAsync(conn, legacyUserId.Value, ct))
            .Where(f => binding.FormaExacta is null
                ? string.Equals(f.FormaDescripcion.TrimEnd(), valorDeForma.Trim(), StringComparison.OrdinalIgnoreCase)
                : string.Equals(f.Forma.TrimEnd(), valorDeForma.TrimEnd(), StringComparison.OrdinalIgnoreCase));

        foreach (var fila in delLote)
        {
            var nombre = fila.Proceso.Trim();
            if (nombre.Length == 0)
                continue;

            var permitido = fila.Permitido;

            /*
              LAS CONCESIONES EXTRA, QUE NO PASAN POR EL CATALOGO.

              Van ANTES de resolver el mapa a proposito: casi todos estos
              botones no estan mapeados ni se pueden derivar -btnPagosCascos,
              btnNuevaNotaCredito, btnGarantia...-, asi que el `continue` de
              abajo los descartaria antes de llegar aqui. Y son claves de otra
              pantalla (o de otro boton de esta), asi que tampoco estan en el
              catalogo de la familia: por eso no pasan por su comprobacion. Ver
              la explicacion larga en LegacyModuleBinding.ConcesionesExtra.

              SOLO SUMAN, NUNCA NIEGAN. Si este boton esta apagado, la clave no
              se mete en `deny`: su dueno es el boton canonico, y negarla desde
              aqui le borraria el permiso a quien si lo tiene por la via normal.
              Un boton apagado aqui significa "este boton no la concede", no
              "esta clave esta prohibida".
            */
            if (permitido
                && binding.ConcesionesExtra is { Count: > 0 } extras
                && extras.TryGetValue(nombre, out var claveExtra))
            {
                allow.Add(claveExtra);
            }

            // EL MISMO orden que LoadLegacyModuleCatalogAsync: manda el mapa de
            // la forma y solo si no mapeo el boton se deriva con la tabla de
            // verbos. Si se resolviera distinto que alla, se concederian claves
            // que el catalogo no publica (o al reves) y la pantalla de permisos
            // dejaria de cuadrar con lo que el snapshot entrega.
            if (!binding.Map.TryGetValue(nombre, out var mapped))
            {
                var derivada = DerivarClaveLegacy(
                    nombre,
                    binding.Prefijo ?? binding.ModuleKey,
                    binding.ModuleName,
                    fila.ProcesoDescripcion);
                if (derivada is null)
                    continue;
                mapped = derivada.Value;
            }

            if (!ventasCatalog.ContainsKey(mapped.Key))
                continue;

            if (permitido) allow.Add(mapped.Key);
            else deny.Add(mapped.Key);
        }

        // Un boton concedido gana sobre el mismo nombre negado en otro boton que
        // produce la misma clave. Es la consecuencia directa de la regla de
        // arriba: "cualquiera de los que la producen" significa que basta uno.
        deny.ExceptWith(allow);
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

        /*
          SE OTORGA Y TAMBIEN SE QUITA, PERO SOLO SI LA PANTALLA LO ENSEÑO.

          Antes esto solo otorgaba, y por una razon buena: el 2026-08-26, en
          produccion, guardar permisos de un usuario desde la pantalla web le
          revoco 44 de sus 135 permisos de Mac31 —todos los botones de CONSULTA
          DE VENTAS y varios reportes— sin avisar y sin que nadie los hubiera
          tocado. La causa no fue escribir en legacy: fue tratar la AUSENCIA en
          "effective" como una negacion. El web no sabia lo que habia en Mac31,
          asi que "no lo tengo" y "no me lo ensenaron" eran la misma cosa.

          Pero dejarlo en solo-otorgar deja una puerta de un solo sentido:
          encendias un boton, se escribia en Mac31, y al volver a leer lo de
          Mac31 pisaba lo del web, asi que la casilla volvia a salir encendida y
          ya no habia forma de apagarla desde ninguna de las dos pantallas. Es
          el mismo sintoma que se midio en Zaragoza para `reportes.*` el
          28-sep-2026 y que ahi ya se arreglo con el candado de abajo; esto solo
          extiende ese arreglo —ya probado— a `ventas.`, `empleados.` y
          `prestamos.`.

          QUE LO HACE SEGURO AHORA Y NO EN AGOSTO: el bootstrap ya mezcla el
          estado real de Mac31 dentro de las casillas de estas tres familias
          (ver GetPermisosBootstrapAsync, donde a cada usuario se le cargan
          legacyVentas / legacyEmpleados / legacyPrestamos y se le quita del
          Allow/Deny todo lo que no sea semilla del web). O sea: quien desmarca
          esta viendo el estado de verdad y lo esta quitando a proposito.

          EL CANDADO: solo se toca un permiso si SU modulo esta ENCENDIDO. Con el
          modulo apagado sus permisos se caen del catalogo y la pantalla no los
          ensena; sin este candado, guardarle a alguien cualquier otra cosa le
          apagaria en Mac31 permisos que nadie vio ni pidio quitar. Ese es
          exactamente el accidente de agosto. Se pregunta por la clave de cada
          permiso —no por una sola del modulo— porque este mismo metodo lo usan
          las tres familias.

          Dar de baja es IDStatus = 2, NUNCA borrar la fila: asi lo hace Mac31 en
          su propia pantalla (UsuariosFormasProcesoPermisos manda
          enumStatus.Inactivo = 2) y asi lo entiende
          sp_n_ActualizarUsuarioFormaProceso, que con IDStatus = 2 y sin fila
          previa no inserta nada. El SP toca una sola fila, la de ESTE usuario y
          ESTE proceso, y trae su propia transaccion.
        */
        var activeModules = await GetActiveModulesAsync(conn, empresaId, ct);

        /*
          UN BOTON DE MAC31 PUEDE TENER DOS CLAVES, Y ANTES DECIDIA LA ULTIMA.

          Medido el 2026-10-12 en Produccion_svr: conceder
          `cobranza.cobranzas.consultar` dejaba el btnBuscar de CONSULTA DE
          COBRANZAS en IDStatus = 2, o sea que CONCEDER APAGABA. La causa es que
          dos claves distintas apuntan al MISMO IDProceso, a proposito y
          documentado: esa forma la alimentan las familias "cobranza." y
          "cobranzas." (ver PermissionService.Familias.Cobros.cs), y
          btnModificarRFC es "clientes.rfc.abrir" en una pantalla y
          "clientes.facturacion.editar" en la otra. Al recorrer el catalogo
          clave por clave, la primera escribia 1 y la segunda 2 sobre la misma
          fila: ganaba el orden del diccionario.

          Asi que se agrupa por IDProceso y se decide una sola vez. La regla es
          "CUALQUIER SI GANA": si alguna de las claves de ese boton esta
          concedida, el boton se enciende. Nunca al contrario, porque la
          alternativa -que cualquier NO apague- convierte un detalle de como
          esta partido el catalogo en una revocacion de un permiso real de
          Mac31, que es justo lo que no puede pasar.

          Y para APAGAR se exige que TODAS sus claves esten en modulos
          encendidos. Con una sola clave escondida -su modulo apagado- la
          pantalla no ensenó ese boton completo, asi que apagarlo seria quitar
          algo que nadie vio ni pidio quitar: el accidente de agosto otra vez,
          por la puerta de al lado.
        */
        /* El agrupado y las dos reglas de arriba NO cambian. Lo unico nuevo es
           que el resultado se junta y se escribe solo la diferencia, en un
           viaje (ver LeerEstadoLegacyDelUsuarioAsync). */
        var deseado = new Dictionary<int, int>();
        foreach (var grupo in ventasCatalog.Values.GroupBy(a => a.LegacyProcessId))
        {
            var loTiene = grupo.Any(a => effective.Contains(a.Key));
            if (!loTiene && !grupo.All(a => IsPermissionInActiveModule(a.Key, activeModules)))
                continue;

            deseado[grupo.Key] = loTiene ? 1 : 2;
        }

        var estadoActual = await LeerEstadoLegacyDelUsuarioAsync(conn, legacyUserId.Value, ct);
        await EscribirCambiosLegacyAsync(
            conn, legacyUserId.Value, movementUserId, deseado, estadoActual, ct);
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
        /*
          Ver _memoDeUsuarioLegacy: en el bootstrap esto se preguntaba 2080
          veces -una por usuario y por familia- y siempre contesta lo mismo.
        */
        var llave = $"usuariolegacy|{conn.Database}|{empresaId}|{userId}";
        var memoria = _memoDeLaPeticion.Value;
        if (memoria is not null && memoria.TryGetValue(llave, out var guardado) && guardado is int?[] caja)
            return caja[0];

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

        var id = raw is null || raw == DBNull.Value
            ? (int?)null
            : Convert.ToInt32(raw, CultureInfo.InvariantCulture);

        /* En una caja para poder guardar tambien el "no se encontro" (null) sin
           que el diccionario lo confunda con "no esta memorizado". */
        if (memoria is not null) memoria[llave] = new[] { id };
        return id;
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
        /* Ver _memoDeEsquema: esta pregunta se hacia 2080 veces por peticion. */
        var llave = $"col|{conn.Database}|{tableName}|{columnName}";
        if (_memoDeEsquema.TryGetValue(llave, out var guardado) && guardado.Hasta > DateTime.UtcNow)
            return guardado.Existe;

        await using var cmd = new SqlCommand(@"
SELECT 1
FROM sys.columns c
INNER JOIN sys.tables t ON t.object_id = c.object_id
WHERE t.name = @TableName
  AND c.name = @ColumnName;", conn);
        cmd.Parameters.Add(new SqlParameter("@TableName", SqlDbType.NVarChar, 128) { Value = tableName });
        cmd.Parameters.Add(new SqlParameter("@ColumnName", SqlDbType.NVarChar, 128) { Value = columnName });
        var raw = await cmd.ExecuteScalarAsync(ct);

        var existe = raw is not null && raw != DBNull.Value;
        _memoDeEsquema[llave] = (existe, DateTime.UtcNow.Add(SchemaCacheTtl));
        return existe;
    }

    private static PermisosWebPermissionItem EnrichPermissionItem(
        string code,
        string name,
        IReadOnlyDictionary<string, ReportPermissionInfo> reportCatalog,
        IReadOnlyDictionary<string, VentasPermissionInfo> ventasCatalog,
        IReadOnlyDictionary<string, VentasPermissionInfo> empleadosCatalog,
        IReadOnlyDictionary<string, VentasPermissionInfo> prestamosCatalog,
        IReadOnlyDictionary<string, VentasPermissionInfo> familiasCatalog)
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

        // Las familias que se agregaron despues. Se marcan con su
        // IDForma/IDProceso de Mac31 igual que empleados y prestamos, para que
        // la pantalla de permisos ensene de donde sale el permiso y no parezca
        // que se puede dar desde el web: darlo aqui no sirve de nada, se borra
        // en la siguiente lectura.
        if (familiasCatalog.TryGetValue(code, out var deFamilia))
        {
            var (moduleKey, moduleName) = ModuloDeFamilia(code);
            return new PermisosWebPermissionItem
            {
                Code = deFamilia.Key,
                Name = deFamilia.Name,
                ModuleKey = string.IsNullOrEmpty(moduleKey) ? ExtractModuleFromPermissionKey(code) : moduleKey,
                ModuleName = moduleName,
                CategoryKey = deFamilia.CategoryKey,
                CategoryName = deFamilia.CategoryName,
                LegacyFormId = deFamilia.LegacyFormId,
                LegacyProcessId = deFamilia.LegacyProcessId
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

        /*
          UN MODULO APAGADO PARA LA EMPRESA TAMPOCO VALE POR URL DIRECTA.

          Antes esto solo miraba `WModulo.IdStatus = 1`, o sea el interruptor
          GLOBAL de la base, e ignoraba por completo `WModuloEmpresa.Activo`,
          que es el de cada empresa. El agujero que dejaba: apagar un modulo
          desde Permisos -> Modulos le quitaba la tarjeta del Inicio y **no le
          quitaba nada al API**. Quien tuviera el permiso seguia usandolo con
          una peticion directa, que es exactamente lo que no debe pasar.

          Ahora hace falta lo de siempre Y que la empresa lo tenga encendido.

          EL `EXISTS` VA POR EmpresaId Y NO POR EmpresaClave, Y ES A SABIENDAS.
          Este metodo no recibe la clave de empresa del token, y `MacZ` tiene
          dos ('zaragoza' y 'aviacion') bajo el mismo EmpresaId. Asi que un
          modulo encendido para una de las dos cuenta como activo para las dos.
          Es mas flojo de lo ideal, pero es lo seguro: lo contrario —adivinar la
          clave aqui— le quitaria permisos a la empresa equivocada, y ese error
          se paga en accesos perdidos. Lo estricto por empresa ya lo hace la
          consulta del mosaico, que si recibe la clave.

          IMPACTO MEDIDO ANTES DE PONERLO, en las dos bases: de todos los
          usuarios con permisos de familias hoy apagadas, el unico es JUAN, que
          es SuperAdmin y no pasa por aqui. Ningun rol concede ninguna. O sea
          que nadie pierde un acceso que estuviera usando.
        */
        var hayTablaPorEmpresa = await TableExistsAsync(conn, "WModuloEmpresa", ct);
        var hayRenglonesPorEmpresa = false;
        if (hayTablaPorEmpresa)
        {
            await using var cuenta = new SqlCommand(
                "SELECT COUNT(*) FROM dbo.WModuloEmpresa WHERE EmpresaId = @EmpresaId;", conn);
            cuenta.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            hayRenglonesPorEmpresa = Convert.ToInt32(await cuenta.ExecuteScalarAsync(ct) ?? 0) > 0;
        }

        /*
          SIN RENGLONES NO SE PUEDE COMPROBAR, Y ENTONCES NO SE RECORTA.

          Una base a medio migrar, sin la tabla o sin un solo renglon para esta
          empresa, no esta diciendo "todo apagado": esta diciendo "aqui no hay
          nada que mirar". Tratarlo como un no dejaria a la empresa entera sin
          un permiso, y el sintoma seria todo el mundo con 403 de golpe.
        */
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = new SqlCommand(hayRenglonesPorEmpresa ? @"
SELECT m.ModuloClave
FROM dbo.WModulo m
WHERE m.EmpresaId = @EmpresaId
  AND m.IdStatus = 1
  AND EXISTS (
      SELECT 1
      FROM dbo.WModuloEmpresa me
      WHERE me.EmpresaId = m.EmpresaId
        AND me.ModuloClave = m.ModuloClave
        AND me.Activo = 1);" : @"
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

        /*
          UNA LECTURA POR PETICION, NO UNA POR FORMA.

          A este metodo se entra una vez por cada forma de Mac31 -son ~70- y
          las ~70 veces se pedia el catalogo ENTERO de la empresa. Es el mismo
          dato: lo que cambia entre llamadas son las semillas que se comparan
          contra el, no el catalogo.

          Se guarda en la memoria de la peticion y se mantiene al dia a mano
          mas abajo, cuando se escribe una clave. Asi la comparacion siguiente
          ve exactamente lo que acaba de quedar en la base.
        */
        var llaveDelCatalogo = $"catalogoWPermiso|{conn.Database}|{empresaId}";
        var memoria = _memoDeLaPeticion.Value;
        Dictionary<string, (string Nombre, string Descripcion)>? actuales = null;

        if (memoria is not null
            && memoria.TryGetValue(llaveDelCatalogo, out var guardado)
            && guardado is Dictionary<string, (string Nombre, string Descripcion)> vigente)
        {
            actuales = vigente;
        }

        if (actuales is null)
        {
            actuales = new Dictionary<string, (string Nombre, string Descripcion)>(StringComparer.OrdinalIgnoreCase);
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

            if (memoria is not null) memoria[llaveDelCatalogo] = actuales;
        }

        var escritos = 0;
        foreach (var seed in semillas)
        {
            var descripcion = descripcionDe(seed);

            /*
              YA ESTA IGUAL: NO SE TOCA. Escribir lo mismo no solo cuesta el
              viaje, ademas mueve FechaActualizacion y hace parecer que alguien
              cambio permisos cuando nadie los cambio.

              LA COMPARACION MIRA `Nombre` Y YA NO `Descripcion`. POR QUE
              ------------------------------------------------------------
              Porque `Descripcion` ESTABA EN DISPUTA, y la disputa se pagaba en
              cada peticion. Cuatro sembradores pasan un `descripcionDe`
              distinto PARA LA MISMA CLAVE:

                EnsureCatalogoSembradoAsync            seed => seed.Module    ("cobranza")
                EnsureLegacyModulePermissionsSyncedAsync  "cobranza.legacy" / "cobranza.web"
                EnsureLegacyReportPermissionsSyncedAsync  "reportes.legacy" / "reportes.web"
                EnsureCentrosPermissionsSyncedAsync       "reportes.centros"

              Con `Descripcion` dentro de la comparacion, cada pasada veia la
              descripcion que habia dejado la pasada anterior, la declaraba
              "distinta" y reescribia el renglon para dejar la suya; y la
              siguiente hacia lo mismo al reves. MEDIDO con Extended Events en
              las dos empresas: 359 escrituras a WPermiso sobre 164 claves,
              ~20 s de red, y el estado final IDENTICO —diff de WPermiso antes
              y despues: 0 filas—. Eran 359 escrituras para no cambiar nada.

              Es tambien de donde salia el pico de `/api/me` en frio (535
              viajes contra 10 en caliente) y el de `modulos/disponibles`, que
              no es otro evento: es el MISMO, porque hay un semaforo por
              usuario y la segunda peticion solo espera a la primera.

              POR QUE ESTO ES SEGURO: `WPermiso.Descripcion` NO SE LEE NUNCA.
              Comprobado clave por clave: los seis SELECT sobre dbo.WPermiso de
              este archivo piden `Clave`, `Nombre` o un `TOP 1`, ninguno pide
              `Descripcion`; el unico que la leia es la consulta de este mismo
              metodo, para esta comparacion. Y no sale a ningun cliente:
              `PermisosWebPermissionItem` no tiene ese campo. Es una etiqueta de
              procedencia que se escribia y nadie consultaba.

              LO QUE SE ESCRIBE NO CAMBIA. En los renglones que YA EXISTEN la
              descripcion se queda tal cual esta hoy en la base (el UPDATE de
              mas abajo ya no la toca), y lo que hay hoy es precisamente el
              valor con el que terminaba cada pasada. En una clave NUEVA la
              sigue escribiendo el INSERT, con la etiqueta del sembrador que
              llegue primero. Lo unico que desaparece son las reescrituras.
            */
            var yaExistia = actuales.TryGetValue(seed.Key, out var actual);
            if (yaExistia && string.Equals(actual.Nombre, seed.Name, StringComparison.Ordinal))
            {
                continue;
            }

            /*
              PRIMERO UPDATE Y SOLO SI NO HABIA, INSERT. NO AL CONTRARIO.

              Antes esto era `IF EXISTS ... UPDATE ... ELSE INSERT`, que por
              clave parece seguro y no lo es cuando hay DOS cosas sembrando a
              la vez. Y las hay: `CatalogoDePermisosHostedService` siembra al
              arrancar, y la primera peticion que entra siembra tambien al
              armar su snapshot. Las dos preguntan "existe?", las dos oyen
              "no", las dos insertan, y la segunda choca con UQ_WPermiso.

              Reventaba de verdad, no en teoria: el 2026-10-10, al registrar
              las familias heredadas nuevas, /api/me contesto
              "Violation of UNIQUE KEY constraint 'UQ_WPermiso' ...
              (1, recordatorios.consultar)" en la PRIMERA peticion
              (traceId ...:00000001), y el usuario se quedo con CERO permisos,
              porque la excepcion tumbo el armado del snapshot entero.

              La carrera llevaba aqui desde siempre, pero no disparaba porque
              no habia claves nuevas que insertar: con el catalogo ya sembrado,
              todas las vueltas eran UPDATE. Al heredar los modulos nuevos
              aparecieron ~100 claves nuevas y la ventana se lleno de INSERT.

              Con el UPDATE primero y el INSERT solo si `@@ROWCOUNT = 0`, y el
              NOT EXISTS tomando UPDLOCK/HOLDLOCK, la insercion es una sola
              sentencia atomica: el segundo en llegar no inserta y no rompe.
            */
            /*
              EL UPDATE YA NO TOCA `Descripcion`, Y ES LA OTRA MITAD DEL
              ARREGLO DE ARRIBA.

              Si solo se quitara de la comparacion, este UPDATE seguiria
              pisando la descripcion del sembrador anterior cada vez que de
              verdad cambiara un `Nombre`: la clave dejaria de reescribirse 359
              veces, pero seguiria en disputa. Dejandola fuera, la descripcion
              de un renglon que ya existe no vuelve a moverse nunca, y lo que
              hay en la base se queda como esta.

              En una clave NUEVA si se escribe: la pone el INSERT de abajo.
            */
            await using var cmd = new SqlCommand(@"
UPDATE dbo.WPermiso
   SET Nombre = @Nombre
 WHERE EmpresaId = @EmpresaId
   AND Clave = @Clave;

IF @@ROWCOUNT = 0
BEGIN
    INSERT INTO dbo.WPermiso (EmpresaId, Clave, Nombre, Descripcion, FechaCreacion)
    SELECT @EmpresaId, @Clave, @Nombre, @Descripcion, SYSUTCDATETIME()
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.WPermiso WITH (UPDLOCK, HOLDLOCK)
        WHERE EmpresaId = @EmpresaId AND Clave = @Clave);
END", conn);
            cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            cmd.Parameters.Add(new SqlParameter("@Clave", SqlDbType.NVarChar, 200) { Value = seed.Key });
            cmd.Parameters.Add(new SqlParameter("@Nombre", SqlDbType.NVarChar, 200) { Value = seed.Name });
            cmd.Parameters.Add(new SqlParameter("@Descripcion", SqlDbType.NVarChar, 500) { Value = descripcion });
            await cmd.ExecuteNonQueryAsync(ct);
            escritos++;

            /* El memo de arriba tiene que reflejar lo que acaba de quedar en la
               base: si no, la siguiente forma volveria a creer que esta clave
               esta desactualizada y la escribiria otra vez.

               La descripcion que se guarda en el memo es la que QUEDO en la
               base, no la que traia esta semilla: si el renglon ya existia, el
               UPDATE no la toco y sigue siendo la de antes. Solo cuando la
               clave es nueva la escribio el INSERT. Ya no se compara, pero un
               memo que miente sobre la base es una trampa para quien lo lea
               despues. */
            var descripcionQueQuedo = yaExistia ? actual.Descripcion : descripcion;
            actuales[seed.Key] = (seed.Name, descripcionQueQuedo);
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
