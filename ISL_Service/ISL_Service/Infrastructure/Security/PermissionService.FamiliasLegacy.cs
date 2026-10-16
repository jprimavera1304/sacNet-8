using ISL_Service.Application.DTOs.Responses;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Security;

/// <summary>
/// LAS FAMILIAS DE PERMISOS QUE MANDA MAC31, MAS ALLA DE LAS CUATRO ORIGINALES.
/// </summary>
/// <remarks>
/// POR QUE UN ARCHIVO APARTE Y NO MAS LINEAS EN PermissionService.cs
/// -----------------------------------------------------------------
/// Las cuatro familias que ya existian (reportes, ventas, empleados,
/// prestamos) estan escritas A MANO en cuatro lugares distintos del archivo
/// grande: el snapshot, los dos bootstraps y el catalogo. Son cuatro bloques
/// casi identicos por familia, o sea dieciseis bloques. Agregar siete familias
/// mas con ese molde serian veintiocho bloques nuevos de copia y pega, y la
/// siguiente persona tendria que acordarse de cuatro lugares por familia.
///
/// Aqui las familias son DATOS —una lista— y el puente es UN recorrido. Las
/// cuatro viejas se dejan exactamente como estaban: reescribirlas no es parte
/// de esta entrega y hoy funcionan.
///
/// LO QUE ESTE ARCHIVO *NO* HACE: ESCRIBIR EN MAC31
/// ------------------------------------------------
/// Solo LEE. El sentido Web -> Legacy (que si existe: va por
/// <c>dbo.sp_n_ActualizarUsuarioFormaProceso</c>, no por un INSERT suelto) se
/// deja intacto a proposito, porque lo esta trabajando otra persona. Las
/// familias de aqui no se registran en los Sync*ForUser/ForRole.
///
/// EL IDProceso NO ES EL MISMO EN LAS DOS EMPRESAS
/// -----------------------------------------------
/// Es el hallazgo que obliga a que todo esto se resuelva CONTRA LA CONEXION y
/// nunca con numeros a mano. Las formas viejas (IDProceso &lt; 3100) si
/// coinciden entre Zaragoza y Tauro, y de ahi vienen los comentarios del
/// archivo grande que dicen "los mismos numeros en las dos empresas". Para las
/// formas nuevas es FALSO, medido el 2026-10-09 en MacZ y Produccion_svr:
///
///   CONSULTA DE GARANTIAS / btnBuscar     -> 5142 en MacZ, 5139 en Tauro
///   CORTES / btnNuevo                     -> 5351 en MacZ, 5352 en Tauro
///   CONSULTA DE GASOLINAS / btnNuevo      -> 5174 en MacZ, 5183 en Tauro
///   CAMBIOS DE VIGENCIA / btnBuscar       -> 6438 en MacZ, 6441 en Tauro
///   REPORTE DE COBRANZA                   -> 5427 en MacZ, 5430 en Tauro
///
/// Por eso la llave del mapa es el NOMBRE DEL BOTON (p.Proceso) y el IDProceso
/// se resuelve leyendo n_Procesos por la conexion de la empresa en curso. Un
/// permiso de Zaragoza no puede conceder en Tauro porque nunca se cruzan: cada
/// empresa resuelve sus propios numeros con su propia conexion.
/// </remarks>
public sealed partial class PermissionService
{
    /// <summary>
    /// Una familia del web cuyos permisos finos VIVEN en Mac31, posiblemente
    /// repartidos en varias formas.
    /// </summary>
    /// <param name="Prefijo">
    /// El prefijo de las claves que esta familia GOBIERNA. Todo lo que empiece
    /// asi y no este en <paramref name="SemillasDelWeb"/> se borra del snapshot
    /// y se vuelve a calcular desde los botones de Mac31.
    /// </param>
    /// <param name="ClaveDeEntrada">
    /// El ".ver_modulo" del web a encender cuando Mac31 concede
    /// <paramref name="ClaveQueDaLaEntrada"/>, o null si la familia no tiene
    /// entrada propia. No existe en Mac31: se DERIVA, igual que ya se hacia
    /// para empleados y prestamos, para no obligar a capturarlo a mano a quien
    /// ya tiene el permiso en Mac31.
    /// </param>
    private sealed record FamiliaLegacy(
        string Prefijo,
        string ModuleKey,
        string ModuleName,
        string? ClaveDeEntrada,
        string? ClaveQueDaLaEntrada,
        IReadOnlyList<PermissionSeed> SemillasDelWeb,
        IReadOnlyList<LegacyModuleBinding> Formas);

    /*
      CONSTANTES  <-  "FECHA DE OPERACION" (IDForma 4021)

      El empate mas limpio de toda la auditoria: tres botones, tres claves, uno
      a uno, y con los MISMOS IDProceso en las dos empresas (5070, 5071, 5133).

      "constantes.ver" se queda del web: la forma de Mac31 no tiene un boton de
      buscar dado de alta en n_Procesos, asi que alla no hay nada que leer.
    */
    private static readonly Dictionary<string, (string Key, string Name)> ConstantesMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnGuardarFechaVentas"] = ("constantes.fechaventas.editar", "Constantes - Avanzar la fecha de operacion de ventas"),
        ["btnGuardarFechaPagos"] = ("constantes.fechapagos.editar", "Constantes - Mover la fecha de operacion de pagos"),
        ["btnGuardarDias"] = ("constantes.editar", "Constantes - Editar parametros de operacion")
    };

    private static readonly FamiliaLegacy ConstantesFamilia = new(
        "constantes.", "constantes", "Constantes", null, null,
        new List<PermissionSeed> { new("constantes.ver", "Constantes - Consultar parametros de operacion", "constantes") },
        new List<LegacyModuleBinding>
        {
            new("constantes", "Constantes", "FECHA DE OPERACIÓN", ConstantesMap, Array.Empty<PermissionSeed>(),
                /* La forma NO TIENE NI UN BOTON DE LECTURA en n_Procesos
                   -comprobado en MacZ y en Produccion_svr el 2026-10-12-, asi
                   que esta clave era inalcanzable desde Mac31 y habia que darla
                   a mano encima de los botones que Mac31 ya concede. Ver la
                   explicacion de ClaveDeLectura en PermissionService.cs. */
                ClaveDeLectura: "constantes.ver")
        });

    /*
      COMPRAS  <-  "CONSULTA DE COMPRAS" (IDForma 2020)

      Solo se mapean DOS de los seis botones, y es a proposito: el web no da de
      alta compras ni imprime la comprobacion, asi que btnNuevo, btnImprimir,
      btnComprobacionPantalla y btnComprobacionExcel no tienen clave web y no se
      inventa una. Un permiso que nada comprueba es ruido en la pantalla de
      permisos.

      "compras.ver" se queda del web por lo mismo que constantes: la forma no
      tiene boton de buscar en n_Procesos.

      OJO con los numeros: btnComprobacionPantalla es 5333 en MacZ y 5334 en
      Tauro. No se usan, pero es el ejemplo de por que no se guardan a mano.
    */
    private static readonly Dictionary<string, (string Key, string Name)> ComprasMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnModificar"] = ("compras.editar", "Compras - Marcar como factura"),
        ["btnCancelar"] = ("compras.cancelar", "Compras - Cancelar")
    };

    private static readonly FamiliaLegacy ComprasFamilia = new(
        "compras.", "compras", "Compras", "compras.ver_modulo", "compras.ver",
        new List<PermissionSeed>
        {
            new("compras.ver_modulo", "Compras - Ver modulo", "compras"),
            new("compras.ver", "Compras - Consultar", "compras")
        },
        new List<LegacyModuleBinding>
        {
            new("compras", "Compras", "CONSULTA DE COMPRAS", ComprasMap, Array.Empty<PermissionSeed>(),
                /* La forma NO TIENE NI UN BOTON DE LECTURA en n_Procesos
                   -comprobado en MacZ y en Produccion_svr el 2026-10-12-, asi
                   que esta clave era inalcanzable desde Mac31 y habia que darla
                   a mano encima de los botones que Mac31 ya concede. Ver la
                   explicacion de ClaveDeLectura en PermissionService.cs. */
                ClaveDeLectura: "compras.ver")
        });

    /*
      INVENTARIO  <-  "AJUSTES AL INVENTARIO" (2019) + "INVENTARIO EN TRANSITO" (4067)

      Dos pantallas de Mac31 bajo el mismo prefijo "inventario.", cada una con
      su forma. Se pueden poner juntas porque el prefijo que se borra es uno.

      LO QUE OBLIGA A ENUMERAR SEMILLAS CON CUIDADO: "inventario.pedidos.*" es
      una pantalla que SOLO existe en el web (el inventario de pedidos no
      existe en Mac31). Si no estuviera en las semillas, el borrado por prefijo
      se la llevaria y se perderia sin error y sin rastro — la misma trampa que
      ya documenta ventas.

      Las dos entradas de estas pantallas se llaman "inventario_ajustes" e
      "inventario_transito" (con guion bajo), no "inventario": por eso la
      familia declara ClaveDeEntrada = null y las entradas quedan donde estan,
      a cargo del web. Ver EntradasDeModulo.
    */
    private static readonly Dictionary<string, (string Key, string Name)> InventarioAjustesMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnNuevo"] = ("inventario.ajustes.crear", "Inventario - Ajustes: capturar"),
        ["btnCancelar"] = ("inventario.ajustes.cancelar", "Inventario - Ajustes: cancelar")
    };

    private static readonly Dictionary<string, (string Key, string Name)> InventarioTransitoMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnBuscar"] = ("inventario.transito.ver", "Inventario - Consultar transito"),
        ["btnAceptar"] = ("inventario.transito.procesar", "Inventario - Procesar transito")
    };

    private static readonly FamiliaLegacy InventarioFamilia = new(
        "inventario.", "inventario", "Inventario", null, null,
        new List<PermissionSeed>
        {
            new("inventario.ajustes.ver", "Inventario - Ajustes: consultar", "inventario"),
            new("inventario_ajustes.ver_modulo", "Inventario - Ajustes: ver modulo", "inventario"),
            new("inventario_transito.ver_modulo", "Inventario - En transito: ver modulo", "inventario"),
            new("inventario.pedidos.ver", "Inventario - Inventario de pedidos: consultar", "inventario"),
            new("inventario.pedidos.crear", "Inventario - Inventario de pedidos: capturar", "inventario"),
            new("inventario.pedidos.cancelar", "Inventario - Inventario de pedidos: cancelar", "inventario")
        },
        new List<LegacyModuleBinding>
        {
            /*
              CADA UNA ABRE SU PROPIA PANTALLA, Y POR ESO LAS CLAVES VAN AQUI
              Y NO ARRIBA EN LA FAMILIA.

              AJUSTES AL INVENTARIO tiene en n_Procesos btnNuevo y btnCancelar
              y NI UN BOTON DE LECTURA -comprobado en las dos empresas el
              2026-10-12, IDProceso 3064 y 3065 en las dos-, asi que
              "inventario.ajustes.ver", que es lo que exige
              InventarioOperacionesController.cs:31, no se podia heredar de
              ningun sitio. Se deriva de tener cualquiera de los dos botones,
              que en Mac31 es exactamente lo que pone la forma en tu menu.

              INVENTARIO EN TRANSITO si tiene su btnBuscar y ya mapea
              "inventario.transito.ver", asi que NO declara ClaveDeLectura: ese
              permiso tiene que seguir viniendo del boton y no regalarse a quien
              solo tenga btnAceptar.
            */
            new("inventario", "Inventario", "AJUSTES AL INVENTARIO", InventarioAjustesMap, Array.Empty<PermissionSeed>(),
                ClaveDeEntrada: "inventario_ajustes.ver_modulo",
                ClaveDeLectura: "inventario.ajustes.ver"),
            new("inventario", "Inventario", "INVENTARIO EN TRANSITO", InventarioTransitoMap, Array.Empty<PermissionSeed>(),
                ClaveDeEntrada: "inventario_transito.ver_modulo")
        });

    /*
      GARANTIAS  <-  "CONSULTA DE GARANTIAS" (3020) + "POR ACEPTAR" (4068) + "REGISTRO" (4079)

      De los catorce botones de la forma grande solo tres tienen clave web. Los
      otros once (GARANTIA, AJUSTE, DIAGNOSTICAR, DEVOLVER AL CLIENTE,
      REMISIONAR, ENVIAR AL PROVEEDOR y los tres de imprimir) son pasos del
      flujo que el web todavia no tiene; cuando los tenga, se agregan aqui y el
      permiso ya viene de Mac31 sin tocar nada mas.

      UN BOTON POR CLAVE, NUNCA DOS. btnNuevo y btnModificar hacen los dos lo
      que el web llama "garantias.editar", pero el catalogo se indexa por clave
      y solo guarda UN IDProceso por clave: si se mapearan los dos, cual gana
      dependeria del Orden de la fila, o sea del azar. Se elige btnModificar
      porque el web no da de alta la garantia, la modifica.
    */
    private static readonly Dictionary<string, (string Key, string Name)> GarantiasMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnBuscar"] = ("garantias.ver", "Garantias - Consultar"),
        ["btnModificar"] = ("garantias.editar", "Garantias - Registrar y modificar"),
        ["btnCancelar"] = ("garantias.cancelar", "Garantias - Cancelar")
    };

    private static readonly Dictionary<string, (string Key, string Name)> GarantiasAceptarMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnAceptada"] = ("garantias.aceptar", "Garantias - Aceptar en el centro de servicio")
    };

    private static readonly Dictionary<string, (string Key, string Name)> GarantiasRegistroMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnNuevo"] = ("garantias.registro.editar", "Garantias - Registro previo: crear"),
        ["btnCancelar"] = ("garantias.registro.cancelar", "Garantias - Registro previo: cancelar")
    };

    private static readonly FamiliaLegacy GarantiasFamilia = new(
        "garantias.", "garantias", "Garantias", "garantias.ver_modulo", "garantias.ver",
        new List<PermissionSeed> { new("garantias.ver_modulo", "Garantias - Ver modulo", "garantias") },
        new List<LegacyModuleBinding>
        {
            new("garantias", "Garantias", "CONSULTA DE GARANTÍAS", GarantiasMap, Array.Empty<PermissionSeed>(),
                /*
                  LOS SEIS PASOS DEL FLUJO, QUE EL WEB SI TIENE.

                  El comentario de arriba decia que estos botones son "pasos del
                  flujo que el web todavia no tiene". Ya no es cierto: la
                  pantalla de garantias los resuelve los seis con
                  POST api/ventas/garantias/actualizar, que exige
                  "garantias.editar" (VentasGarantiasController).

                  En Mac31 son botones aparte y quien tiene solo esos no
                  heredaba nada. Medido: JESY en Tauro tiene los seis y no
                  btnModificar, asi que podia diagnosticar, ajustar y devolver
                  al cliente alla y nada de eso aca.

                  Los TRES DE IMPRIMIR siguen fuera, y no por olvido: son los
                  reportes 1220, 1222 y 1224 de Mac31 y no estan en el catalogo
                  de reportes del web, asi que no hay accion que autorizar.
                */
                ConcesionesExtra: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["btnGarantia"] = "garantias.editar",
                    ["btnAjuste"] = "garantias.editar",
                    ["btnDiagnosticar"] = "garantias.editar",
                    ["btnRegresarCliente"] = "garantias.editar",
                    ["btnEnviarProveedor"] = "garantias.editar",
                    ["btnRemisionar"] = "garantias.editar"
                }),
            new("garantias", "Garantias", "CONSULTA DE GARANTIAS POR ACEPTAR", GarantiasAceptarMap, Array.Empty<PermissionSeed>()),
            new("garantias", "Garantias", "CONSULTA DE REGISTRO DE GARANTÍAS", GarantiasRegistroMap, Array.Empty<PermissionSeed>())
        });

    /*
      CORTES  <-  "CORTES" (4064) + "CORTES CENTRO DE SERVICIO" (4069)

      Las dos formas tienen botones con el MISMO nombre (btnVer, btnNuevo) y
      significan cosas distintas: una es el corte de caja y la otra el retiro
      del centro de servicio. No choca porque el mapa es POR FORMA: cada
      binding solo mira los procesos de su propia forma.

      "DETALLE CORTE CENTRO DE SERVICIO" (4070) se deja fuera: su btnGuardar es
      el mismo acto que el btnNuevo de 4069 y mapear los dos a
      "cortes.cs.crear" volveria a la ambiguedad del IDProceso unico.
    */
    private static readonly Dictionary<string, (string Key, string Name)> CortesMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnVer"] = ("cortes.ver", "Cortes - Consultar"),
        ["btnNuevo"] = ("cortes.crear", "Cortes - Guardar corte de caja"),
        ["btnCancelar"] = ("cortes.cancelar", "Cortes - Cancelar")
    };

    private static readonly Dictionary<string, (string Key, string Name)> CortesCsMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnVer"] = ("cortes.cs.ver", "Cortes - Consultar centros de servicio"),
        ["btnNuevo"] = ("cortes.cs.crear", "Cortes - Guardar retiro de centro de servicio")
    };

    private static readonly FamiliaLegacy CortesFamilia = new(
        "cortes.", "cortes", "Cortes", "cortes.ver_modulo", "cortes.ver",
        new List<PermissionSeed> { new("cortes.ver_modulo", "Cortes - Ver modulo", "cortes") },
        new List<LegacyModuleBinding>
        {
            new("cortes", "Cortes", "CORTES", CortesMap, Array.Empty<PermissionSeed>()),
            new("cortes", "Cortes", "CORTES CENTRO DE SERVICIO", CortesCsMap, Array.Empty<PermissionSeed>()),

            /*
              LA TERCERA FORMA, QUE ANTES SE QUEDO FUERA Y YA NO HACE FALTA.

              DETALLE CORTE CENTRO DE SERVICIO (forma CorteDiarioCS) se habia
              dejado fuera porque su btnGuardar choca con el btnNuevo de la
              forma anterior: las dos producian "cortes.cs.crear" y el catalogo
              guarda un solo IDProceso por clave, asi que una de las dos
              quedaba muerta en silencio.

              Con el `Prefijo` propio ya no chocan. Deriva
              "cortes.cs.detalle.guardar" y "cortes.cs.detalle.imprimir", las
              dos nuevas. Medido en las dos empresas: un solo usuario con
              cuenta web tiene esos botones en cada una y nadie los tiene
              guardados en el web, asi que gana uno por empresa y no pierde
              nadie.
            */
            new("cortes", "Cortes", "DETALLE CORTE CENTRO DE SERVICIO",
                new Dictionary<string, (string Key, string Name)>(StringComparer.OrdinalIgnoreCase),
                Array.Empty<PermissionSeed>(),
                FormaExacta: "CorteDiarioCS",
                Prefijo: "cortes.cs.detalle")
        });

    /*
      CAMBIOS DE VIGENCIA  <-  "CONSULTA CAMBIOS DE VIGENCIA" (4080)

      Cuatro de los ocho botones. btnVer, btnEntregar, btnRechazar y btnImprimir
      no tienen clave web propia hoy.

      LAS DOS ENTRADAS DUPLICADAS: el catalogo trae
      "cambiosvigencia.ver_modulo" y "cambios_vigencia.ver_modulo" (con guion
      bajo), de dos siembras distintas, para la MISMA pantalla. Se siembran las
      dos como semilla del web para que el borrado por prefijo no se lleve
      ninguna: borrar la que sobra es una migracion de datos, y mientras no se
      haga, quitarle la "mala" a quien la tiene lo deja fuera del modulo.
      Ojo: solo "cambiosvigencia.ver_modulo" cae bajo este prefijo; la otra
      empieza con "cambios_" y el borrado no la toca — se declara igual para
      que quede escrito que existe.
    */
    private static readonly Dictionary<string, (string Key, string Name)> CambiosVigenciaMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnBuscar"] = ("cambiosvigencia.ver", "Cambios de vigencia - Consultar"),
        ["btnRegistrar"] = ("cambiosvigencia.crear", "Cambios de vigencia - Registrar"),
        ["btnModificar"] = ("cambiosvigencia.editar", "Cambios de vigencia - Modificar / regresar"),
        ["btnCancelar"] = ("cambiosvigencia.cancelar", "Cambios de vigencia - Cancelar")
    };

    private static readonly FamiliaLegacy CambiosVigenciaFamilia = new(
        "cambiosvigencia.", "cambiosvigencia", "Cambios de vigencia",
        "cambiosvigencia.ver_modulo", "cambiosvigencia.ver",
        new List<PermissionSeed>
        {
            new("cambiosvigencia.ver_modulo", "Cambios de vigencia - Ver modulo", "cambiosvigencia"),
            new("cambios_vigencia.ver_modulo", "Cambios de vigencia - Ver modulo", "cambiosvigencia"),
            /* REGRESAR AL INVENTARIO, COMO SEMILLA DEL WEB.
               En Mac31 no hay un boton que de solo esto: btnModificar se
               llama "Modificar / regresar" y btnEntregar cuelga de el. Como
               no mapea a ningun boton, sin declararla aqui el borrado por
               prefijo se la llevaria en la siguiente lectura y el endpoint
               de regresar se quedaria en 403 permanente.
               Existe aparte de editar porque miden cosas distintas: editar
               cambia un texto y regresar MUEVE EXISTENCIAS. */
            new("cambiosvigencia.regresar", "Cambios de vigencia - Regresar al inventario", "cambiosvigencia")
        },
        new List<LegacyModuleBinding>
        {
            new("cambiosvigencia", "Cambios de vigencia", "CONSULTA CAMBIOS DE VIGENCIA", CambiosVigenciaMap, Array.Empty<PermissionSeed>(),
                /* "Entregar" es regresar la pieza al inventario, y el web lo
                   hace con POST api/cambios-vigencia/{id}/regresar, que exige
                   "cambiosvigencia.editar", la misma clave que btnModificar.
                   Medido: REBECA en Zaragoza tiene btnEntregar y no
                   btnModificar.
                   btnRechazar NO entra: esta muerto tambien en Mac31 -el
                   manejador no hace nada y el boton va oculto-, asi que no hay
                   permiso que heredar. */
                ConcesionesExtra: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    /* btnEntregar ES regresar: ahora da la clave de regresar y
                       no la de editar. Antes le regalaba MODIFICAR a quien solo
                       tiene entregar -el caso medido arriba: btnEntregar sin
                       btnModificar-, que es una autorizacion que Mac31 no le da. */
                    ["btnEntregar"] = "cambiosvigencia.regresar"
                })
        });

    /*
      ASISTENCIAS  <-  "ASISTENCIAS" (IDForma 6)

      Dos botones, pero solo btnGuardar tiene clave web: el web no borra
      asistencias, asi que btnEliminar no se mapea.

      NO TOCA EL MOVIL. Las claves del telefono son "app_movil.asistencias.*",
      otro prefijo, y el borrado de esta familia es por "asistencias." exacto.
      Se dice aqui porque es la familia donde seria mas facil equivocarse.
    */
    private static readonly Dictionary<string, (string Key, string Name)> AsistenciasMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnGuardar"] = ("asistencias.editar", "Asistencias - Editar")
    };

    private static readonly FamiliaLegacy AsistenciasFamilia = new(
        "asistencias.", "asistencias", "Asistencias", null, null,
        new List<PermissionSeed> { new("asistencias.ver", "Asistencias - Ver", "asistencias") },
        new List<LegacyModuleBinding>
        {
            new("asistencias", "Asistencias", "ASISTENCIAS", AsistenciasMap, Array.Empty<PermissionSeed>(),
                /* La forma NO TIENE NI UN BOTON DE LECTURA en n_Procesos
                   -comprobado en MacZ y en Produccion_svr el 2026-10-12-, asi
                   que esta clave era inalcanzable desde Mac31 y habia que darla
                   a mano encima de los botones que Mac31 ya concede. Ver la
                   explicacion de ClaveDeLectura en PermissionService.cs. */
                ClaveDeLectura: "asistencias.ver")
        });

    /// <summary>
    /// Las familias que este cambio agrega. Las cuatro viejas (reportes,
    /// ventas, empleados, prestamos) NO estan aqui: siguen con su codigo a
    /// mano en PermissionService.cs.
    /// </summary>
    private static readonly IReadOnlyList<FamiliaLegacy> FamiliasLegacyAmpliadas = new List<FamiliaLegacy>
    {
        ConstantesFamilia,
        ComprasFamilia,
        InventarioFamilia,
        GarantiasFamilia,
        CortesFamilia,
        CambiosVigenciaFamilia,
        AsistenciasFamilia,
        // Las dos de PermissionService.Familias.Proveedores.cs. Van al final a
        // proposito: ModuloDeFamilia devuelve la PRIMERA familia cuyo prefijo
        // empate, asi que agregar al final no cambia a quien pertenecia ya
        // ninguna clave.
        ProveedoresFamilia,
        ObraProveedoresFamilia,
        // De PermissionService.Familias.Facturacion.cs. El orden da igual:
        // manda el prefijo mas largo (ver EsLaFamiliaMasEspecifica).
        FacturasFamilia,
        ConcentradosFamilia,
        ReasignacionFamilia,
        ClientesRfcsFamilia,
        // De PermissionService.Familias.Cobros.cs. CobrosPagosFamilia usa el
        // prefijo "cobros." y ReasignacionFamilia "cobros.reasignar": se
        // solapan, y el que manda es el mas largo, no el que este antes.
        CobranzaFamilia,
        CobrosPagosFamilia,
        CobranzasWebFamilia,
        // De PermissionService.Familias.Clientes.cs. ClientesFamilia usa
        // "clientes." y ClientesRfcsFamilia (de facturacion) usa
        // "clientes.facturacion.": se solapan y manda el mas largo.
        ClientesFamilia,
        ProductosFamilia,
        UsuariosFamilia,
        AgentesFamilia,
        RepartidoresFamilia,
        // De PermissionService.Familias.Usados.cs
        AlmacenCascosFamilia,
        UsadosTiposFamilia,
        GasolinasFamilia,
        TransferenciasFamilia,
        DescuentosFamilia,
        ChecadorFamilia,
        // De PermissionService.Familias.Catalogos.cs
        CatalogosFamilia,
        RecordatoriosFamilia,
        // De PermissionService.Familias.DineroUsados.cs. Su prefijo
        // ("usados.cobros_dinero.") cuelga del de UsadosTiposFamilia solo en
        // apariencia: ese es "usados.tipos.", asi que no se solapan.
        DineroUsadosCobrosFamilia,
        /*
          De PermissionService.Familias.CobrosUsados.cs. Su prefijo es
          "usados.cobros." y NO se solapa con "usados.cobros_dinero.": el guion
          bajo separa, asi que "cobros_dinero" no empieza por "cobros.". Lee la
          misma forma que CobranzaFamilia (ConsultarCobros) con otro prefijo,
          igual que "cobranza." y "cobranzas." se reparten ConsultarCobranzas.
        */
        CobrosUsadosFamilia
    };

    /// <summary>
    /// Da de alta en WPermiso lo que Mac31 tenga de estas familias y devuelve
    /// el catalogo resuelto, familia por familia.
    /// </summary>
    /// <summary>
    /// El catalogo de TODAS las familias de Mac31, memorizado por base y empresa.
    /// </summary>
    /// <remarks>
    /// POR QUE AQUI Y NO BINDING A BINDING. Recorrer las 31 familias y sus ~70
    /// formas costaba cientos de viajes a SQL en CADA peticion de permisos
    /// -medido: era el grueso de los 5383 viajes de un bootstrap-, y ademas
    /// reescribia WModulo y WPermiso por el camino: una lectura que escribe.
    ///
    /// Memorizar cada forma por separado se probo y salio mal: la llave tenia
    /// que capturar todo lo que distingue un binding de otro y se perdieron dos
    /// permisos reales (`clientes.rfc.crear` y `.editar`). Con UNA llave para
    /// todo el conjunto no hay nada que confundir.
    ///
    /// EL PLAZO es `CatalogoCacheTtl`. Lo que se retrasa es que una forma NUEVA
    /// de Mac31 aparezca en la pantalla de permisos; lo que cada usuario puede
    /// se sigue leyendo siempre.
    /// </remarks>
    private async Task<Dictionary<string, Dictionary<string, VentasPermissionInfo>>> EnsureFamiliasLegacySyncedAsync(
        SqlConnection conn,
        int empresaId,
        CancellationToken ct)
    {
        var memorizado = await CatalogoMemorizadoAsync(
            $"familias|{conn.Database}|{empresaId}",
            () => EnsureFamiliasLegacySyncedSinMemoriaAsync(conn, empresaId, ct));

        /* Copia profunda: quien lo recibe junta catalogos encima, y escribir en
           el memorizado contaminaria las peticiones siguientes. */
        var copia = new Dictionary<string, Dictionary<string, VentasPermissionInfo>>(StringComparer.OrdinalIgnoreCase);
        foreach (var familia in memorizado)
            copia[familia.Key] = new Dictionary<string, VentasPermissionInfo>(familia.Value, StringComparer.OrdinalIgnoreCase);
        return copia;
    }

    private async Task<Dictionary<string, Dictionary<string, VentasPermissionInfo>>> EnsureFamiliasLegacySyncedSinMemoriaAsync(
        SqlConnection conn,
        int empresaId,
        CancellationToken ct)
    {
        var porFamilia = new Dictionary<string, Dictionary<string, VentasPermissionInfo>>(StringComparer.OrdinalIgnoreCase);

        foreach (var familia in FamiliasLegacyAmpliadas)
        {
            var catalogo = new Dictionary<string, VentasPermissionInfo>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < familia.Formas.Count; i++)
            {
                // Las semillas del web se cuelgan de la PRIMERA forma nada mas.
                // Si se pasaran a todas, cada forma volveria a sembrar las
                // mismas claves y el catalogo se escribiria N veces.
                var binding = i == 0
                    ? familia.Formas[i] with { WebSeeds = familia.SemillasDelWeb }
                    : familia.Formas[i];

                var parcial = await EnsureLegacyModulePermissionsSyncedAsync(conn, empresaId, binding, ct);
                foreach (var par in parcial)
                    catalogo[par.Key] = par.Value;
            }

            porFamilia[familia.Prefijo] = catalogo;
        }

        return porFamilia;
    }

    /// <summary>
    /// Lo que el usuario tiene encendido en Mac31 para una familia, juntando
    /// todas sus formas.
    /// </summary>
    private static async Task<(HashSet<string> Allow, HashSet<string> Deny)> LoadFamiliaLegacyForUserAsync(
        SqlConnection conn,
        int empresaId,
        Guid userId,
        FamiliaLegacy familia,
        IReadOnlyDictionary<string, VentasPermissionInfo> catalogo,
        CancellationToken ct)
    {
        var allow = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deny = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var binding in familia.Formas)
        {
            var parcial = await LoadLegacyModulePermissionsForUserAsync(conn, empresaId, userId, binding, catalogo, ct);
            allow.UnionWith(parcial.Allow);
            deny.UnionWith(parcial.Deny);

            /*
              LA ENTRADA Y LA LECTURA DE *ESTA* PANTALLA.

              La derivacion de la familia (en AplicarFamiliasLegacyAsync) sirve
              cuando todas sus formas abren la MISMA pantalla del web. Cuando no
              -"inventario." alimenta Ajustes y En transito, cada una con su
              propia entrada- la familia no puede nombrar una sola clave y
              declaraba null, y entonces nadie heredaba ninguna de las dos.

              La regla es la misma que ya se midio y se acordo para la entrada
              de familia: CUALQUIER boton de la forma vale. En Mac31 el boton
              vive dentro de la forma, asi que tenerlo significa que la forma
              esta en tu menu y la puedes abrir y leer. Lo que se pueda hacer
              adentro lo siguen decidiendo los permisos finos, uno por uno.

              Las dos claves se conceden solo si la forma le dio ALGO; con
              Allow vacio no se deriva nada.
            */
            if (parcial.Allow.Count == 0)
                continue;

            if (binding.ClaveDeEntrada is { Length: > 0 } entradaDeForma)
                allow.Add(entradaDeForma);

            if (binding.ClaveDeLectura is { Length: > 0 } lecturaDeForma)
                allow.Add(lecturaDeForma);
        }

        // Un boton concedido en una forma gana sobre el mismo nombre negado en
        // otra. Solo puede pasar si dos formas mapean a la misma clave, que es
        // justo lo que los mapas de arriba evitan; se deja por si algun dia
        // alguien agrega una forma mas y se le olvida.
        deny.ExceptWith(allow);
        return (allow, deny);
    }

    /// <summary>
    /// Cierto si esa clave la gobierna la familia y por tanto hay que borrarla
    /// del snapshot del web antes de poner lo de Mac31.
    /// </summary>
    private static bool LaMandaLegacy(FamiliaLegacy familia, string clave) =>
        clave.StartsWith(familia.Prefijo, StringComparison.OrdinalIgnoreCase)
        && !familia.SemillasDelWeb.Any(s => s.Key.Equals(clave, StringComparison.OrdinalIgnoreCase))
        && EsLaFamiliaMasEspecifica(familia, clave);

    /*
      MANDA EL PREFIJO MAS LARGO, Y POR ESO EL ORDEN DE LA LISTA YA NO IMPORTA.

      Hace falta desde que hay familias cuyo prefijo cuelga de otro:
      "cobros." y "cobros.reasignar", "clientes." y "clientes.facturacion.".
      Sin esto, el fallo era de ORDEN y silencioso, porque
      AplicarFamiliasLegacyAsync recorre la lista haciendo
      `RemoveAll(LaMandaLegacy(familia, x))` y luego agregando lo suyo:

        * si la ancha va DESPUES, le borra a la especifica las claves que la
          especifica acababa de poner, y no las vuelve a poner porque sus
          formas no las producen. La gente perdia el permiso sin que nada
          fallara.
        * si la ancha va ANTES, el bucle queda bien, pero entonces
          ModuloDeFamilia —que devolvia la PRIMERA familia cuyo prefijo
          empatara— le adjudicaba la clave al modulo equivocado.

      O sea que las dos funciones pedian ordenes contrarios y no habia orden
      bueno. Con esto, una familia solo gobierna una clave si NINGUNA otra
      familia tiene un prefijo mas largo que tambien le empate, asi que da
      igual como esten puestas en la lista.
    */
    private static bool EsLaFamiliaMasEspecifica(FamiliaLegacy familia, string clave)
    {
        foreach (var otra in FamiliasLegacyAmpliadas)
        {
            if (ReferenceEquals(otra, familia))
                continue;
            if (otra.Prefijo.Length > familia.Prefijo.Length
                && clave.StartsWith(otra.Prefijo, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Sustituye en <paramref name="permisos"/> todo lo que manden estas
    /// familias por lo que diga Mac31.
    /// </summary>
    /// <remarks>
    /// Si Mac31 no tiene la forma dada de alta, el catalogo de esa familia
    /// viene VACIO y no se borra nada: una base sin ese modulo se queda con lo
    /// que diga el web, en vez de quedarse sin permisos de golpe. Es la misma
    /// red de seguridad que ya tenian empleados y prestamos.
    /// </remarks>
    private async Task AplicarFamiliasLegacyAsync(
        SqlConnection conn,
        int empresaId,
        Guid userId,
        List<string> permisos,
        IReadOnlyDictionary<string, Dictionary<string, VentasPermissionInfo>> catalogos,
        CancellationToken ct)
    {
        /*
          UN MODULO APAGADO NO DEJA PASAR NI LO QUE VIENE DE MAC31.

          El filtro de modulos apagados vive en LoadEffectivePermissionsAsync,
          que corre ANTES que esto. Como las familias agregan sus claves
          despues, se saltaban el filtro enteras: con el modulo apagado, sus
          permisos heredados seguian saliendo en el snapshot, y la puerta
          derivada volvia a aparecer sola.

          Apagar el modulo tiene que cerrar la puerta de esta plataforma sin
          tocar NADA en Mac31, que es justo el escenario 4. Por eso se filtra
          aqui tambien, con el mismo criterio que usa el resto del snapshot.
        */
        var apagados = await LoadInactiveModuleKeysAsync(conn, empresaId, ct);

        foreach (var familia in FamiliasLegacyAmpliadas)
        {
            if (!catalogos.TryGetValue(familia.Prefijo, out var catalogo) || catalogo.Count == 0)
                continue;

            var legacy = await LoadFamiliaLegacyForUserAsync(conn, empresaId, userId, familia, catalogo, ct);
            permisos.RemoveAll(x => LaMandaLegacy(familia, x));
            // El `Where` de duplicados hace falta desde que los bindings derivan
            // su propia entrada y su lectura: esas claves son SEMILLAS DEL WEB,
            // y las semillas sobreviven al RemoveAll de arriba a proposito. Sin
            // esto, quien ya las tuviera concedidas por el web las veria dos
            // veces en /api/me.
            permisos.AddRange(legacy.Allow
                .Where(x => !EsDeModuloApagado(x, apagados))
                .Where(x => !permisos.Contains(x, StringComparer.OrdinalIgnoreCase))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

            // QUIEN VE LA FORMA EN MAC31 VE EL MODULO EN EL WEB.
            //
            // El ".ver_modulo" no existe en Mac31: es la llave del web que
            // decide si sale la tarjeta en el inicio. Sin derivarla habria que
            // darla A MANO a cada quien que ya tiene el permiso en Mac31, que
            // es justo la doble captura que este puente viene a quitar. Y
            // desde este cambio pesa el doble, porque ahora la entrada tambien
            // se exige en el backend (ver EntradasDeModulo): sin derivarla, el
            // puente habria dejado a la gente con permisos finos de Mac31 y sin
            // puerta por donde usarlos.
            // CUALQUIER boton de la familia abre la puerta, no uno concreto.
            //
            // Antes solo se derivaba si el usuario tenia exactamente
            // `ClaveQueDaLaEntrada`. Eso dejo FUERA a la familia de compras y
            // se midio el 2026-10-12: ComprasFamilia dice derivar de
            // "compras.ver", pero su mapa solo produce "compras.editar" y
            // "compras.cancelar", y la forma CONSULTA DE COMPRAS no tiene
            // ningun boton de buscar ni de ver en n_Procesos. O sea que
            // "compras.ver" es INALCANZABLE desde Mac31 y la entrada no se
            // derivaba nunca.
            //
            // No era teorico: 7 usuarios con cuenta web heredaban los botones
            // de compras y se quedaban sin la puerta -FERNANDA, NORMA,
            // QA_LEGWEB y RICARDO en MacZ; JANET, JESY y NAOLANY en
            // Produccion_svr-, asi que el modulo les contestaba 403 aunque
            // Mac31 se los autorizara.
            //
            // Tampoco servia elegir otra clave suelta: FERNANDA solo tiene
            // btnNuevo, QA_LEGWEB solo btnModificar y JESY ninguno de los dos.
            // Ninguna clave individual los cubre a todos.
            //
            // Por eso la condicion es "tiene ALGO de esta familia en Mac31".
            // Es lo que dice el titulo de arriba: si Mac31 le deja usar esa
            // pantalla, puede entrar a ese modulo en el web. La puerta no es
            // una capacidad; lo que se puede hacer adentro lo siguen decidiendo
            // los permisos finos, uno por uno.
            //
            // MEDIDO QUE NO ENSANCHA NADA MAS: en las dos empresas, las otras
            // dos familias con entrada -cortes y garantias- no tienen ni un
            // usuario con algun boton y sin el de origen (btnVer y btnBuscar
            // respectivamente), asi que para ellas el resultado es identico.
            //
            // `ClaveQueDaLaEntrada` se conserva porque sigue documentando de
            // donde nace la puerta de cada familia, y porque una familia sin
            // entrada propia (null) tiene que seguir sin derivar nada.
            if (familia.ClaveDeEntrada is { Length: > 0 } entrada
                && familia.ClaveQueDaLaEntrada is { Length: > 0 }
                && legacy.Allow.Count > 0
                && !EsDeModuloApagado(entrada, apagados)
                && !permisos.Any(x => string.Equals(x, entrada, StringComparison.OrdinalIgnoreCase)))
            {
                permisos.Add(entrada);
            }
        }
    }

    /// <summary>
    /// Lo mismo, pero sobre el Allow/Deny que la pantalla de permisos le ensena
    /// a un usuario.
    /// </summary>
    private async Task AplicarFamiliasLegacyEnOverridesAsync(
        SqlConnection conn,
        int empresaId,
        Guid userId,
        PermisosWebUserOverrideItem fila,
        IReadOnlyDictionary<string, Dictionary<string, VentasPermissionInfo>> catalogos,
        CancellationToken ct)
    {
        foreach (var familia in FamiliasLegacyAmpliadas)
        {
            if (!catalogos.TryGetValue(familia.Prefijo, out var catalogo) || catalogo.Count == 0)
                continue;

            var legacy = await LoadFamiliaLegacyForUserAsync(conn, empresaId, userId, familia, catalogo, ct);
            fila.Allow.RemoveAll(x => LaMandaLegacy(familia, x));
            fila.Deny.RemoveAll(x => LaMandaLegacy(familia, x));
            fila.Allow.AddRange(legacy.Allow.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            fila.Deny.AddRange(legacy.Deny.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// El catalogo de TODAS las familias nuevas en un solo diccionario, para
    /// marcar cada permiso con su IDForma/IDProceso en la pantalla de permisos.
    /// </summary>
    private static Dictionary<string, VentasPermissionInfo> JuntarCatalogosDeFamilias(
        IReadOnlyDictionary<string, Dictionary<string, VentasPermissionInfo>> catalogos)
    {
        var todo = new Dictionary<string, VentasPermissionInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var catalogo in catalogos.Values)
        {
            foreach (var par in catalogo)
                todo[par.Key] = par.Value;
        }
        return todo;
    }

    /// <summary>
    /// El nombre legible de la familia a la que pertenece una clave, para la
    /// pantalla de permisos. Vacio si no es de estas familias.
    /// </summary>
    private static (string ModuleKey, string ModuleName) ModuloDeFamilia(string clave)
    {
        // El prefijo MAS LARGO que empate, no el primero: "clientes.facturacion.x"
        // es del modulo de facturacion de clientes, no del de clientes, y cual
        // de las dos familias este antes en la lista no debe decidirlo.
        FamiliaLegacy? mejor = null;
        foreach (var familia in FamiliasLegacyAmpliadas)
        {
            if (!clave.StartsWith(familia.Prefijo, StringComparison.OrdinalIgnoreCase))
                continue;
            if (mejor is null || familia.Prefijo.Length > mejor.Prefijo.Length)
                mejor = familia;
        }
        return mejor is null ? (string.Empty, string.Empty) : (mejor.ModuleKey, mejor.ModuleName);
    }
}
