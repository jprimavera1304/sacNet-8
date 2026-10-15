using System;
using System.Collections.Generic;

namespace ISL_Service.Infrastructure.Security;

/*
  CLIENTES, PRODUCTOS, USUARIOS, AGENTES Y REPARTIDORES <- MAC31.

  Las cinco familias de los CATALOGOS MAESTROS. Se declaran juntas porque las
  cinco se miden en la misma tanda y porque comparten la misma sorpresa: en
  estas cinco formas de Mac31 NO EXISTE btnBuscar. El ".ver" de cada modulo
  sale de btnVer (el boton de "ver detalle"), no de un boton de buscar, asi
  que no hay nada que elegir entre dos candidatos como paso en garantias.

  MEDIDO EL 2026-10-10 en LAS DOS bases locales (MacZ = Zaragoza,
  Produccion_svr = Tauro), leyendo n_Formas + n_Procesos:

    ConsultarClientes      (IDForma 1017)  9 procesos en las dos
    ConsultarProductos     (IDForma   10)  7 en MacZ, 8 en Tauro
    ConsultarUsuarios      (IDForma    7)  7 en las dos
    ConsultarAgentes       (IDForma   17)  3 en las dos
    ConsultarRepartidores  (IDForma 3021)  3 en las dos
    ClientesRFCs           (IDForma 4063)  2 en las dos

  POR QUE TODO VA POR FormaExacta Y NUNCA POR IDProceso
  ----------------------------------------------------
  Los IDProceso NO coinciden entre las dos empresas, medido boton por boton:

    ConsultarClientes/btnListasPreciosAceites  5320 en MacZ, 5321 en Tauro
    ConsultarClientes/btnModificarRFC          5350 en MacZ, 5351 en Tauro
    ConsultarClientes/btnVentasCentros         5426 en MacZ, 5429 en Tauro
    ConsultarProductos/btnCargarPrecios        5364 en MacZ, 5366 en Tauro
    ConsultarProductos/btnDesCargar            5365 en MacZ, 5367 en Tauro
    ConsultarProductos/btnListaRoshfrans       5370 en MacZ, 5372 en Tauro
    ConsultarProductos/btnMovimientos          5382 en MacZ, 5384 en Tauro
    ConsultarUsuarios/btnFechasPago            5405 en MacZ, 5407 en Tauro
    ClientesRFCs/btnNuevoRFC                   5348 en MacZ, 5349 en Tauro
    ClientesRFCs/btnModificarRFC               5349 en MacZ, 5350 en Tauro

  Fijese en el ultimo par: el 5349 es btnModificarRFC en Zaragoza y btnNuevoRFC
  en Tauro. Un IDProceso escrito a mano aqui no solo fallaria, concederia EL
  BOTON EQUIVOCADO. Por eso la llave del mapa es el NOMBRE del boton y el
  numero lo resuelve la conexion de cada empresa.

  Las formas viejas si coinciden (btnNuevo/btnVer/btnModificar de clientes son
  2057/2058/2059, de productos 1034/1035/1036, de usuarios 1025/1026/1027, de
  agentes 1056/1057/1058 y de repartidores 4070/4071/4072 en las dos bases),
  pero eso no cambia nada: tampoco se escriben.

  btnComparar SOLO EXISTE EN TAURO (ConsultarProductos, 5421). No es un error:
  el catalogo se arma leyendo la base de cada empresa, asi que en Zaragoza esa
  clave simplemente no se da de alta hasta que alguien de de alta el boton.

  LAS CLAVES NO SE INVENTARON: SE BUSCARON PRIMERO
  ------------------------------------------------
  Antes de nombrar nada se leyo lo que ya comprueban el backend y el front.
  El web ya tenia 10 claves "clientes.", 8 "usuarios." y 5 "productos.", y las
  que coinciden con un boton de Mac31 se REUSAN tal cual, para que la herencia
  encienda el permiso que los controladores YA piden:

    clientes.crear / clientes.ver / clientes.editar        (ClientesAdminController, ClientesController)
    clientes.bonificaciones.editar                         (ClientesAdminController.PermisosBonificaciones)
    productos.crear / productos.ver / productos.editar     (ProductosAdminController, ProductosController)
    usuarios.ver                                           (UsuariosController, UsuariosAnexosController)

  Las tres primeras de clientes y las tres de productos salen DERIVADAS por la
  tabla de verbos (btnNuevo->crear, btnVer->ver, btnModificar->editar), sin una
  sola linea de mapa: es la prueba de que la derivacion no inventa nombres.
*/
public sealed partial class PermissionService
{
    /*
      CLIENTES: LOS SEIS BOTONES QUE NO SE DERIVAN.

      btnNuevo, btnVer y btnModificar no estan aqui a proposito: los deriva
      VerbosLegacy a clientes.crear / .ver / .editar, que son las claves que ya
      usa el web.

      btnBonificaciones ES LA EXCEPCION QUE REUSA UNA CLAVE DE ESCRITURA.
      Se mapea a "clientes.bonificaciones.editar", que ya existe en el web
      (ClientesAdminController.PermisosBonificaciones) y gobierna exactamente
      lo mismo: la bonificacion de usados del cliente. En Mac31 ese boton es el
      UNICO permiso de esa funcion —no hay una forma de adentro en n_Procesos,
      se busco: la unica forma hija de clientes es ClientesRFCs— asi que la
      pareja es uno a uno y no ensancha nada. La regla de "no inventes nombres
      de clave" pesa mas que la simetria con los ".abrir" de abajo.

      LOS CINCO ".abrir" SON CLAVES NUEVAS, Y SE LLAMAN ASI POR UNA RAZON.
      Huellas, listas de precios, listas de precios de aceites, RFCs y ventas
      de centros de servicio son pantallas que el web TODAVIA NO TIENE: se
      busco su clave en las semillas del catalogo, en las politicas de los
      controladores y en docs2, y no existe ninguna. Como no hay clave que
      reusar, se crea; y se nombra ".abrir" porque lo que el permiso de Mac31
      gobierna literalmente es que el boton que ABRE esa pantalla este
      habilitado. Llamarlas ".ver" o ".editar" afirmaria algo que no se midio
      (que adentro se puede leer, o escribir). Son claves INERTES hoy: ningun
      endpoint ni ninguna pantalla las comprueba todavia.

      btnModificarRFC ES LA TRAMPA DEL BOTON QUE ABRE OTRA PANTALLA.
      Se llama IGUAL en dos formas distintas: en ConsultarClientes (el que abre
      la pantalla de RFCs) y en ClientesRFCs (el de adentro, el que modifica el
      RFC). En Mac31 son DOS permisos distintos, con dos IDProceso distintos.
      Si los dos fueran a la misma clave, quien tuviera uno se quedaria con los
      dos. Asi que el de fuera es "clientes.rfc.abrir" y el de dentro
      "clientes.rfc.editar". No choca en el codigo porque el mapa es POR FORMA,
      pero si habria chocado en la CLAVE, que es lo que importa.
    */
    private static readonly Dictionary<string, (string Key, string Name)> ClientesMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnBonificaciones"] = ("clientes.bonificaciones.editar", "Clientes - Editar bonificaciones de usados"),
        ["btnHuellas"] = ("clientes.huellas.abrir", "Clientes - Entrar a huellas del cliente"),
        ["btnListasPrecios"] = ("clientes.listasprecios.abrir", "Clientes - Entrar a listas de precios y acumuladores"),
        ["btnListasPreciosAceites"] = ("clientes.listasprecios.aceites.abrir", "Clientes - Entrar a listas de precios de aceites"),
        ["btnModificarRFC"] = ("clientes.rfc.abrir", "Clientes - Entrar a los RFCs del cliente"),
        ["btnVentasCentros"] = ("clientes.ventascentros.abrir", "Clientes - Entrar a ventas de centros de servicio")
    };

    /*
      LA PANTALLA DE ADENTRO DE LOS RFCs (ClientesRFCs, IDForma 4063).

      Sus dos botones llevan nombre propio —btnNuevoRFC y btnModificarRFC— y
      ninguno esta en la tabla de verbos, asi que van a mano obligatoriamente.

      NO SE REUSA "clientes.facturacion.editar" PARA ESTOS DOS. Esa clave del
      web gobierna el PUT de los datos de facturacion de un cliente; esta
      pantalla de Mac31 administra la LISTA de RFCs del cliente. No se pudo
      comprobar que sean el mismo acto, y equivocarse aqui le daria escritura
      fiscal a quien hoy no la tiene. Asi que "clientes.facturacion.*" se queda
      como semilla del web, intacta.
    */
    private static readonly Dictionary<string, (string Key, string Name)> ClientesRfcMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnNuevoRFC"] = ("clientes.rfc.crear", "Clientes - Agregar RFC al cliente"),
        ["btnModificarRFC"] = ("clientes.rfc.editar", "Clientes - Modificar RFC del cliente")
    };

    private static readonly FamiliaLegacy ClientesFamilia = new(
        "clientes.", "clientes", "Clientes",
        // La entrada se DERIVA de clientes.ver, igual que en compras y
        // proveedores: quien puede ver clientes en Mac31 ve la tarjeta en el
        // inicio sin que nadie se la capture a mano. Se puede derivar porque
        // "clientes.ver_modulo" SI existe en el catalogo del web (lo exigen
        // seis endpoints) y porque EntradasDeModulo ya lo declara como la
        // puerta de "clientes.".
        "clientes.ver_modulo", "clientes.ver",
        new List<PermissionSeed>
        {
            // SIN ESTAS SEMILLAS SE PIERDEN SIN AVISO. LaMandaLegacy borra POR
            // PREFIJO todo lo que empiece con "clientes." y no este en esta
            // lista. Son las claves del web que NO tienen boton en Mac31:
            new("clientes.ver_modulo", "Clientes - Ver modulo", "clientes"),
            new("clientes.facturacion.ver", "Clientes - Ver datos de facturacion", "clientes"),
            new("clientes.facturacion.editar", "Clientes - Editar datos de facturacion", "clientes"),
            new("clientes.log.ver", "Clientes - Ver bitacora de cambios", "clientes"),
            new("clientes.estado.editar", "Clientes - Dar de baja / reactivar", "clientes"),
            new("clientes.centros.editar", "Clientes - Editar centros de servicio permitidos", "clientes"),

            /*
              "clientes.consultar" SE DECLARA AUNQUE NO ESTE SEMBRADA EN NINGUN
              LADO. Es la unica clave de este grupo que vive solo en el codigo:
              la piden ClientesConsultaController y la puerta de la pantalla de
              venta en docs2 (VentaPage / nucleo/venta/reglas.ts), pero NO esta
              en las semillas de PermissionService.cs y, medido el 2026-10-10,
              no existe como renglon de WPermiso en ninguna de las dos bases. Se
              declara aqui para que el dia que alguien la siembre el borrado por
              prefijo no se la lleve en silencio. Declararla no la concede a
              nadie: solo la hace existir y por tanto repartible.
            */
            new("clientes.consultar", "Clientes - Buscar cliente", "clientes")
        },
        new List<LegacyModuleBinding>
        {
            new("clientes", "Clientes", "CONSULTA DE CLIENTES", ClientesMap,
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarClientes",
                Prefijo: "clientes"),

            // Prefijo propio: sin el, un btnNuevo dado de alta manana en esta
            // forma acabaria en "clientes.crear" junto con el de
            // ConsultarClientes y se pisarian. Hoy los dos botones van
            // mapeados a mano, asi que el Prefijo no cambia nada todavia; se
            // declara para que la forma no pueda contaminar el prefijo del
            // modulo.
            new("clientes", "Clientes", "CLIENTES RFCs", ClientesRfcMap,
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ClientesRFCs",
                Prefijo: "clientes.rfc")
        });

    /*
      PRODUCTOS: CINCO BOTONES A MANO, TRES DERIVADOS.

      btnNuevo/btnVer/btnModificar derivan a productos.crear/.ver/.editar, las
      tres claves que ya piden ProductosAdminController y ProductosController.

      Los otros cinco son claves NUEVAS: se busco en las semillas, en los
      controladores y en docs2, y el web no tiene pantalla de carga de precios,
      ni de descarga, ni la lista de Roshfrans, ni movimientos, ni comparacion
      de precios. Como no hay clave que reusar, se crea una con el prefijo del
      modulo y se deja dicho que HOY NO LAS COMPRUEBA NADIE: solo sirven para
      que el estado de Mac31 se vea en la pantalla de permisos y para que el
      dia que se construya la pantalla el permiso ya venga heredado.

      Cargar y descargar NO llevan ".abrir": no abren una pantalla, ejecutan
      una accion (suben o bajan el archivo de precios). Roshfrans y movimientos
      si abren pantalla, y por eso llevan ".abrir".
    */
    private static readonly Dictionary<string, (string Key, string Name)> ProductosMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnCargarPrecios"] = ("productos.precios.cargar", "Productos - Cargar precios"),
        ["btnDesCargar"] = ("productos.precios.descargar", "Productos - Descargar precios"),
        ["btnListaRoshfrans"] = ("productos.precios.roshfrans.abrir", "Productos - Entrar a la lista de precios Roshfrans"),
        ["btnMovimientos"] = ("productos.movimientos.abrir", "Productos - Entrar a movimientos del producto"),
        // Solo existe en Produccion_svr (Tauro). En MacZ el boton no esta dado
        // de alta en n_Procesos y la clave no se crea alla.
        ["btnComparar"] = ("productos.precios.comparar", "Productos - Comparar precios")
    };

    private static readonly FamiliaLegacy ProductosFamilia = new(
        "productos.", "productos", "Productos",
        "productos.ver_modulo", "productos.ver",
        new List<PermissionSeed>
        {
            new("productos.ver_modulo", "Productos - Ver modulo", "productos"),
            // Mac31 no tiene boton de dar de baja / reactivar producto en
            // n_Procesos, asi que alla no hay nada que leer y la clave se
            // queda del web.
            new("productos.estado.editar", "Productos - Dar de baja / reactivar", "productos")
        },
        new List<LegacyModuleBinding>
        {
            new("productos", "Productos", "CONSULTA DE PRODUCTOS", ProductosMap,
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarProductos",
                Prefijo: "productos")
        });

    /*
      ================================================================
      USUARIOS: LO QUE AQUI *NO* SE HEREDA, Y POR QUE. LEASE COMPLETO.
      ================================================================

      Esta familia hereda UN SOLO permiso de verdad: "usuarios.ver", de btnVer.
      Los otros seis botones de CONSULTAR USUARIOS se quedan fuera o van a una
      clave aparte, A PROPOSITO.

      1) btnPermisos NO SE MAPEA A NADA.
         Es el boton con el que en Mac31 se reparten los permisos de otra
         persona. Mapearlo a cualquier clave del web seria repartir el poder de
         repartir permisos, y eso lo esta auditando otra persona. No hace falta
         bloquearlo: "btnPermisos" no esta en VerbosLegacy, asi que sin entrada
         en el mapa el proceso se ignora solo. Tampoco se toca nada de
         "permisosweb.*", "permisos_usuarios.*", "permisos_roles.*",
         "permisos_modulos.*" ni "seguridad.*": ninguna de esas claves aparece
         en este archivo.

      2) btnNuevo Y btnModificar *NO* VAN A usuarios.crear NI usuarios.editar.
         Esto es lo mas importante de todo el archivo. MEDIDO en el codigo:
         UpdateUserRequest y CreateUserRequest llevan el campo `Rol`
         ("User | Admin | SuperAdmin"), y UsuariosController.Update /
         PatchUpdate / UpdateWithBodyId solo piden "perm:usuarios.editar".
         O sea: en el web, "usuarios.editar" es poder cambiarle el ROL a
         cualquiera, incluido ponerlo SuperAdmin. Y el techo por rol de
         LegacyPolicyMap (que limita usuarios.crear/editar a SuperAdmin) NO
         protege aqui: solo corre en PermissionAuthorizationHandler cuando los
         permisos estan APAGADOS; con el snapshot encendido no se consulta.

         En Mac31, btnModificar de CONSULTAR USUARIOS es un boton comun que
         tiene mucha gente. Medido el 2026-10-10: en MacZ lo tienen NORMA y
         RICARDO, en Produccion_svr JANET. Derivar btnModificar a
         "usuarios.editar" les habria dado a los tres el poder de volverse
         SuperAdmin, que es exactamente la escalada que se pidio no abrir.

         Asi que los dos botones se mapean a claves PROPIAS e INERTES,
         "usuarios.mac31.crear" y "usuarios.mac31.editar", que no las comprueba
         ningun endpoint. Hace falta MAPEARLAS (no basta con no mapearlas)
         porque btnNuevo y btnModificar SI estan en VerbosLegacy: sin entrada en
         el mapa, la tabla de verbos las derivaria sola a usuarios.crear y
         usuarios.editar. El mapa SIEMPRE gana, y es la unica forma de apagar
         una derivacion. Con esto el estado real de Mac31 se VE en la pantalla
         de permisos sin CONCEDER nada en el web.

      3) LO QUE SI SE HEREDA ES SOLO LECTURA.
         "usuarios.ver" (de btnVer) lo piden GET /api/usuarios/{id} y
         GET /api/usuarios/fechas. De ahi se deriva "usuarios.ver_modulo", que
         es lo que piden GET /api/usuarios y GET /api/usuarios/roles: los dos
         son listados. Medido quien gana la entrada por esta derivacion: en
         MacZ NORMA y RICARDO, y NINGUNO de los dos tiene hoy usuarios.crear,
         usuarios.editar ni usuarios.estado.editar, asi que la puerta que se
         les abre no da a ninguna escritura. En Produccion_svr nadie gana la
         entrada (JANET no tiene btnVer).

         Se comprobo tambien el caso inverso, que era el peligroso: alguien con
         "usuarios.editar" ya concedido en el web pero SIN entrada al modulo, a
         quien esta derivacion le abriria la puerta. En MacZ ese alguien es la
         cuenta de pruebas QA_ESPECIFICOS (tiene crear y editar, no tiene
         ver_modulo) — y NO gana nada, porque no tiene btnVer en Mac31. En
         Produccion_svr no hay ningun caso.

      4) LO QUE SE PIERDE, DICHO SIN ADORNO.
         Dos cuentas de MacZ pierden "usuarios.ver": QA_AMBOS y QA_ESPECIFICOS.
         Las dos son cuentas SOLO DEL WEB, sin pareja en dbo.Usuarios de Mac31,
         y el borrado por prefijo se les aplica igual que en todas las familias
         ya existentes. Es la consecuencia directa de que Mac31 manda; no hay
         forma de heredar "usuarios.ver" y a la vez conservarselo a quien Mac31
         no se lo concede. En Produccion_svr nadie pierde nada.

      5) EL SENTIDO WEB -> MAC31 NO SE TOCA.
         Esta familia, como todas las de FamiliasLegacyAmpliadas, solo LEE. No
         se registra en ningun Sync*ForUser/ForRole, asi que guardar permisos
         desde la pantalla web sigue escribiendo en legacy exactamente como
         antes de este archivo. Ni mejor ni peor: igual.

      6) btnAlmacenes, btnHuellas y btnFechasPago son claves NUEVAS e inertes,
         con nombre propio y no derivables. btnAlmacenes abre la pantalla de
         UsuariosAlmacenes (la que un repartidor necesita tener llena para que
         "Mis pedidos" no salga vacio); no reparte permisos, asigna almacenes.
    */
    private static readonly Dictionary<string, (string Key, string Name)> UsuariosMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Las dos claves que APAGAN la derivacion peligrosa. Ver el punto 2.
        ["btnNuevo"] = ("usuarios.mac31.crear", "Usuarios - Alta en Mac31 (informativo, no concede el alta del web)"),
        ["btnModificar"] = ("usuarios.mac31.editar", "Usuarios - Modificar en Mac31 (informativo, no concede la edicion del web)"),

        ["btnAlmacenes"] = ("usuarios.almacenes.abrir", "Usuarios - Entrar a almacenes del usuario"),
        ["btnHuellas"] = ("usuarios.huellas.abrir", "Usuarios - Entrar a huellas del usuario"),
        ["btnFechasPago"] = ("usuarios.fechaspago.abrir", "Usuarios - Entrar a fechas de pago del usuario")

        // btnPermisos: AUSENTE A PROPOSITO. Ver el punto 1.
        // btnVer: ausente para que VerbosLegacy lo derive a "usuarios.ver".
    };

    private static readonly FamiliaLegacy UsuariosFamilia = new(
        "usuarios.", "usuarios", "Usuarios",
        "usuarios.ver_modulo", "usuarios.ver",
        new List<PermissionSeed>
        {
            // TODAS las claves de escritura de usuarios se quedan del WEB. No
            // es olvido: es el punto 2 de arriba. Si falta una de esta lista,
            // el borrado por prefijo se la lleva.
            new("usuarios.ver_modulo", "Usuarios - Ver modulo", "usuarios"),
            new("usuarios.crear", "Usuarios - Crear", "usuarios"),
            new("usuarios.editar", "Usuarios - Editar", "usuarios"),
            new("usuarios.estado.editar", "Usuarios - Activar/Inactivar", "usuarios"),
            new("usuarios.password.reset", "Usuarios - Reset Password", "usuarios"),
            new("usuarios.password.ver", "Usuarios - Ver contrasena de Mac31", "usuarios"),
            new("usuarios.empresa.editar", "Usuarios - Cambiar Empresa", "usuarios")
        },
        new List<LegacyModuleBinding>
        {
            // Por FormaExacta y no por Descripcion: la descripcion de esta
            // forma es "CONSULTAR USUARIOS" (no "CONSULTA DE..."), y empatar
            // por descripcion ya se demostro peligroso en proveedores.
            new("usuarios", "Usuarios", "CONSULTAR USUARIOS", UsuariosMap,
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarUsuarios",
                Prefijo: "usuarios")
        });

    /*
      AGENTES Y REPARTIDORES: DOS FAMILIAS QUE NO PUEDEN PISAR NADA.

      Las tres claves de cada una (crear/ver/editar) salen DERIVADAS, sin mapa:
      las dos formas tienen exactamente btnNuevo, btnVer y btnModificar, con los
      mismos IDProceso en las dos empresas.

      SON CLAVES NUEVAS, Y ESO LAS HACE LAS MAS SEGURAS DEL ARCHIVO: medido el
      2026-10-10, no existe NI UN renglon de WPermiso que empiece con "agentes."
      o "repartidores." en ninguna de las dos bases. El borrado por prefijo no
      tiene nada que borrar, asi que aqui es IMPOSIBLE que alguien pierda algo.

      POR QUE NO SE REUSAN LAS CLAVES QUE EL WEB SI COMPRUEBA HOY.
      En el web, agentes y repartidores no son modulos: son dos pestanas del
      catalogo (docs2/nucleo/menuLegacy.ts los declara con modulo "catalogos"),
      y sus endpoints piden las claves GENERICAS del modulo de catalogos:
      CatalogosAdminController usa "catalogos.crear" / "catalogos.editar" y
      CatalogosAdminController.Lecturas usa "catalogos.ver" para los veinte y
      tantos catalogos a la vez. Heredar btnNuevo de agentes hacia
      "catalogos.crear" le daria a esa persona el alta de empresas, series,
      autos, territorial y todo lo demas. Eso no es heredar, es ensanchar. Y
      declarar una familia con prefijo "catalogos." seria peor: el borrado por
      prefijo se llevaria las veinte claves "catalogos.*" del web de un golpe.

      Asi que estas dos familias dan claves propias, que HOY NO COMPRUEBA NADIE.
      Para que sirvan hay que cambiar CatalogosAdminController para que los
      endpoints de agentes y repartidores pidan su clave propia en vez de la
      generica. Eso es un cambio de autorizacion y no entra en esta entrega: se
      deja dicho aqui y en el informe.

      SIN ClaveDeEntrada, Y ESO TAMBIEN SE DECIDIO.
      La puerta de estas dos pantallas en el web es "catalogos.ver_modulo", que
      es de otro modulo. Derivarla desde el btnVer de agentes abriria el modulo
      ENTERO de catalogos a quien solo tiene agentes en Mac31. Y, como explica
      EntradasDeModulo, los "catalogos.*" estan fuera de la tabla de entradas a
      proposito porque alimentan los combos de media aplicacion. Asi que aqui
      ClaveDeEntrada = null y la entrada se queda donde esta, a cargo del web.
      Tampoco se siembra un "agentes.ver_modulo": una entrada que nadie
      comprueba solo seria ruido en la pantalla de permisos.

      EL WModulo QUE ESTO DA DE ALTA NO SACA NINGUNA TARJETA NUEVA.
      EnsureLegacyModulePermissionsSyncedAsync hace un upsert en WModulo con el
      ModuleKey de la familia, asi que apareceran los renglones "agentes" y
      "repartidores". Comprobado en GetAvailableModulesAsync: la consulta de
      modulos hace INNER JOIN con WModuloEmpresa (Activo = 1), y estos dos no
      tienen renglon ahi, asi que no salen en el inicio de nadie. Se les da
      ModuleKey propio —en vez de colgarlos de "catalogos"— justo para NO
      pisarle el Nombre al modulo de catalogos en ese mismo upsert.
    */
    private static readonly FamiliaLegacy AgentesFamilia = new(
        "agentes.", "agentes", "Agentes", null, null,
        Array.Empty<PermissionSeed>(),
        new List<LegacyModuleBinding>
        {
            new("agentes", "Agentes", "CONSULTA DE AGENTES",
                new Dictionary<string, (string Key, string Name)>(StringComparer.OrdinalIgnoreCase),
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarAgentes",
                Prefijo: "agentes")
        });

    private static readonly FamiliaLegacy RepartidoresFamilia = new(
        "repartidores.", "repartidores", "Repartidores", null, null,
        Array.Empty<PermissionSeed>(),
        new List<LegacyModuleBinding>
        {
            new("repartidores", "Repartidores", "CONSULTA DE REPARTIDORES",
                new Dictionary<string, (string Key, string Name)>(StringComparer.OrdinalIgnoreCase),
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarRepartidores",
                Prefijo: "repartidores")
        });
}
