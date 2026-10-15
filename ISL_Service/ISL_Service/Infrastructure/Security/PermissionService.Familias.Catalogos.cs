using System;
using System.Collections.Generic;

namespace ISL_Service.Infrastructure.Security;

/*
  LOS CATALOGOS: DOCE FORMAS DE MAC31 CONTRA UNA FAMILIA DEL WEB.

  Es el grupo con mas formas de toda la auditoria y, a la vez, el que menos
  claves nuevas necesita: once de las doce formas son exactamente
  btnBuscar/btnNuevo/btnVer/btnModificar, asi que la tabla de verbos
  (VerbosLegacy) acierta la clave SIEMPRE. No hay ni un solo boton cuya clave
  haya que decidir a mano.

  LO QUE SE MIDIO EL 2026-10-10 EN LAS DOS BASES (MacZ y Produccion_svr)
  ---------------------------------------------------------------------
  Las doce formas existen en las dos empresas con los mismos procesos activos.
  Los IDProceso NO coinciden en seis de ellas, y por eso aqui no se escribe
  ninguno:

    ConsultaDiasFestivos       btnBuscar  5366 en MacZ  /  5368 en Tauro
    ConsultarAutos             btnNuevo   5171          /  5180
    ConsultarGalleros          btnBuscar  5299          /  5300
    ConsultarRecordatorios     btnBuscar  5413          /  5415
    ConsultarTipoGastos        btnBuscar  5357          /  5359
    ConsultarTipoRecordatorios btnBuscar  5409          /  5411

  Las otras seis (IDForma < 3100: ConsultarMarcas 13, ConsultarGrupoCategorias
  11, ConsultarCategorias 12, RelacionGrupoCategoriaMarcas 14,
  ConsultarCondicionesCredito 18, ConsultarEmpresas 1016) si coinciden, que es
  justo lo que ya decia PermissionService.FamiliasLegacy.cs: las formas viejas
  coinciden y las nuevas no. Da igual: el catalogo se arma leyendo n_Procesos
  por la conexion de la empresa en curso.

  CADA FORMA LLEVA SU PROPIO `Prefijo`, Y AQUI ES VITAL
  -----------------------------------------------------
  Las doce formas tienen btnNuevo y once tienen btnVer/btnModificar. Si todas
  derivaran con el prefijo del modulo, las doce producirian "catalogos.crear" y
  se pisarian —el catalogo se indexa por clave y gana la primera que entra—, de
  modo que quien pudiera dar de alta una marca podria dar de alta un dia
  festivo. Con un prefijo por forma eso no puede pasar, y se comprobo contra
  los datos: en Produccion_svr, JANET tiene btnNuevo en marcas y en empresas y
  NO lo tiene en autos ni en galleros ni en dias festivos, y JESY lo tiene solo
  en autos. Despues de este cambio JANET gana "catalogos.marcas.crear" y
  "catalogos.empresas.crear" y NO gana "catalogos.autos.crear"; JESY gana solo
  los de autos. Los catalogos no se cruzan.

  `FormaExacta` SIEMPRE, NUNCA LA DESCRIPCION
  -------------------------------------------
  Por lo mismo que en proveedores: hay descripciones repetidas en n_Formas.
  Ademas aqui la descripcion es trampa por otro motivo: "CONSULTA DE
  CATEGORIAS" es ConsultarGrupoCategorias (los GRUPOS, lo que el web llama
  "categorias") y "CONSULTA DE SUB CATEGORIAS" es ConsultarCategorias (lo que
  el web llama "subcategorias"). Los nombres estan cruzados entre Mac31 y el
  web, asi que empatar por un texto en castellano era pedir el error.
  Y "CONSULTA DE CONDIDCIONES DE CREDITO" esta mal escrita en la base.

  POR QUE HAY `Map` SI LA CLAVE SE DERIVA SOLA
  --------------------------------------------
  El mapa NO esta aqui para decidir la clave: se comprobo que las 41 claves que
  derivaria la tabla de verbos son exactamente las que estan escritas abajo.
  El mapa esta por el NOMBRE, y por dos razones medidas:

  1. `SincronizarCatalogoDePermisosAsync` hace UPDATE de WPermiso.Nombre. Diez
     de estas claves YA existen en WPermiso con su nombre en castellano
     ("Catalogos - Alta de marcas y categorias"). La derivacion habria puesto
     el que arma `DerivarClaveLegacy`, que para estas formas no es el verbo
     generico sino n_Procesos.Descripcion —medido: "NUEVO", "VER", "MODIFICAR",
     "BUSCAR"— o sea habria dejado "Catalogos - NUEVO" en lugar del nombre
     bueno. Era una regresion visible en la pantalla de permisos.

  2. `DerivarClaveLegacy` arma el nombre con el `ModuleName` de la atadura, que
     es el del MODULO. Las doce ataduras comparten modulo ("catalogos") porque
     comparten la fila de WModulo, asi que las doce habrian quedado como
     "Catalogos - NUEVO": doce renglones iguales y ninguno distinguible. Darle
     a cada atadura un ModuleName distinto lo arreglaria en WPermiso pero
     romperia WModulo, donde cada atadura hace UPSERT del nombre del modulo y
     ganaria la ultima: el modulo "catalogos" acabaria llamandose "Catalogos -
     Empresas".

  El `Prefijo` se declara igual en todas las ataduras aunque el mapa las cubra
  hoy: es la red para manana. Si alguien da de alta un boton mas en Mac31 —un
  btnBuscar en ConsultarMarcas, por ejemplo—, con prefijo deriva a
  "catalogos.marcas.consultar" y sin prefijo derivaria a "catalogos.consultar",
  que es una clave del modulo entero.

  LA ENTRADA AL MODULO: `catalogos.*` VA SIN ENTRADA, A PROPOSITO
  ---------------------------------------------------------------
  `ClaveDeEntrada` y `ClaveQueDaLaEntrada` van en null para los catalogos.
  Esta decidido y escrito en Application\Security\EntradasDeModulo.cs, en el
  apartado "LO QUE NO SE PUERTEA, Y POR QUE": los catalogos "NO son una
  pantalla, son el diccionario que leen las demas. 'catalogos.ver' lo exigen
  ~40 endpoints de lectura que consumen Compras, Ventas, Garantias y Cobranza
  para llenar sus combos. Exigir 'catalogos.ver_modulo' para entrar a ellos
  dejaria sin combos a quien no administra catalogos". Por eso "catalogos." no
  esta en la tabla de EntradasDeModulo y el backend no exige esa puerta.

  Derivarla igualmente seria peor que inutil: "catalogos.ver_modulo" es la
  llave con la que el inicio pinta la tarjeta de Catalogos, y derivarla de un
  btnVer de autos le ensenaria la pantalla de administracion de catalogos a
  quien en Mac31 solo puede ver la ficha de un coche. Asi que la entrada se
  queda donde esta: la reparte el web a mano, y se siembra abajo para que el
  borrado por prefijo no se la lleve.

  RECORDATORIOS VA APARTE Y SI LLEVA ENTRADA. Su familia esta al final de este
  archivo y ahi se explica por que.

  LO QUE *NO* SE HEREDA Y POR QUE (todo esto queda como SemillasDelWeb)
  ---------------------------------------------------------------------
  * `catalogos.crear`, `catalogos.editar`, `catalogos.baja` y `catalogos.ver`
    son GENERICAS: una sola clave gobierna agentes, almacenes, bancos,
    unidades de medida, tipos de domicilio, condiciones de credito,
    documentos, repartidores, tipos de recordatorio, tipos de gasto y dias
    festivos (medido en CatalogosAdminController: todos esos endpoints piden
    `PermisosCrear` / `PermisosEditar` / `PermisosBaja`). Atarlas a un boton de
    Mac31 seria el error mas caro posible: si "catalogos.crear" se derivara,
    digamos, del btnNuevo de dias festivos, quien hoy da de alta bancos en el
    web y no tiene ese boton en Mac31 PERDERIA el alta de bancos, y siete
    catalogos que no tienen forma en Mac31 se quedarian colgando de una que si.
    Se dejan del web, enteras.

  * `catalogos.fe.ver`, `catalogos.recaudaciones.ver`, `catalogos.formas.ver`,
    `catalogos.territorial.crear` y `catalogos.territorial.editar`: sus
    pantallas no tienen forma en Mac31 dentro de este alcance.

  * los cuatro `.baja` (`marcas`, `galleros`, `empresas`, `autos`): ninguna de
    estas formas tiene un boton de baja ni de eliminar dado de alta en
    n_Procesos —se comprobo proceso por proceso—, asi que alla no hay nada que
    leer.

  * `btnExcel` de ConsultarAutos y `btnNuevoSerie` / `btnModificarSerie` de
    ConsultarEmpresas: no estan en la tabla de verbos y no se mapean, asi que
    se ignoran. Las series no se separan a proposito: el web gatea POST
    empresas-series con la MISMA `catalogos.empresas.crear` que la empresa, asi
    que separarlas pediria una clave que ningun endpoint comprueba. Es el mismo
    criterio de Compras: un permiso que nada comprueba es ruido en la pantalla.

  * `categorias.*` NO es de aqui, y es la confusion mas facil de cometer. Esas
    cinco claves (`categorias.ver_modulo/.ver/.crear/.editar/.activar`) las usa
    CategoriasController, que lee dbo.WCategoria —una tabla SOLO del web, con
    Id uniqueidentifier, la de equipos y torneos— y no las categorias de
    productos de Mac31. Esas viven en `catalogos.gruposcategoria` y
    `catalogos.categoriasmac`, aqui abajo. `categorias.` se queda intacta: no
    es el prefijo de ninguna de estas dos familias.

  * `empresas.ver` tampoco es de aqui. Es del modulo "empresas" (el selector de
    empresa, repartido por rol a SuperAdmin y Admin) y EntradasDeModulo lo pone
    entre "las de plomeria" que no se gatean. El catalogo de empresas del web
    es `catalogos.empresas.*`, que es lo que si se hereda.
*/
public sealed partial class PermissionService
{
    // -----------------------------------------------------------------------
    // MARCAS, CATEGORIAS Y SUBCATEGORIAS: CUATRO FORMAS, UN SOLO JUEGO DE
    // CLAVES EN EL WEB.
    //
    // Es el unico sitio donde Mac31 es MAS FINO que el web, y hay que decirlo
    // claro: el web gobierna las tres pantallas (marcas, grupos-categoria y
    // categorias-mac) con las MISMAS `catalogos.marcas.*`, mientras Mac31
    // tiene un btnNuevo por pantalla. Como el catalogo guarda un solo
    // IDProceso por clave, solo UNA forma puede mandar en esas claves, y se
    // eligio ConsultarMarcas porque es la que le da nombre a la clave.
    //
    // Las otras tres llevan prefijo propio y por tanto claves propias
    // (`catalogos.gruposcategoria.*`, `catalogos.categoriasmac.*`,
    // `catalogos.relacioncategoriasmarcas.editar`). Hoy ningun endpoint las
    // comprueba: se crean para que el permiso de Mac31 quede leido y nombrado
    // el dia que el controlador separe las tres pantallas, y mientras tanto
    // quien manda de verdad es `catalogos.marcas.*`. Se midio lo que cuesta
    // elegir: en las dos empresas, TODO el que tiene un boton en una de las
    // tres formas lo tiene en las tres (DEMO, NORMA y RICARDO en MacZ; DEMO y
    // JANET en Tauro), asi que hoy la eleccion no le quita nada a nadie.
    // -----------------------------------------------------------------------
    private static readonly Dictionary<string, (string Key, string Name)> CatalogosMarcasMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnVer"] = ("catalogos.marcas.ver", "Catálogos - Categorías y marcas"),
        ["btnNuevo"] = ("catalogos.marcas.crear", "Catálogos - Alta de marcas y categorías"),
        ["btnModificar"] = ("catalogos.marcas.editar", "Catálogos - Modificar marcas y categorías")
    };

    private static readonly Dictionary<string, (string Key, string Name)> CatalogosGruposCategoriaMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnVer"] = ("catalogos.gruposcategoria.ver", "Catálogos - Categorías: consultar"),
        ["btnNuevo"] = ("catalogos.gruposcategoria.crear", "Catálogos - Categorías: alta"),
        ["btnModificar"] = ("catalogos.gruposcategoria.editar", "Catálogos - Categorías: modificar")
    };

    private static readonly Dictionary<string, (string Key, string Name)> CatalogosCategoriasMacMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnVer"] = ("catalogos.categoriasmac.ver", "Catálogos - Subcategorías: consultar"),
        ["btnNuevo"] = ("catalogos.categoriasmac.crear", "Catálogos - Subcategorías: alta"),
        ["btnModificar"] = ("catalogos.categoriasmac.editar", "Catálogos - Subcategorías: modificar")
    };

    // La forma de la relacion tiene UN solo proceso (btnModificar, IDProceso
    // 1046 en las dos empresas) y ningun boton de ver ni de alta.
    private static readonly Dictionary<string, (string Key, string Name)> CatalogosRelacionCategoriasMarcasMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnModificar"] = ("catalogos.relacioncategoriasmarcas.editar", "Catálogos - Relación de categorías y marcas: modificar")
    };

    // -----------------------------------------------------------------------
    // LOS CUATRO QUE EN EL WEB CUELGAN DE LAS CLAVES GENERICAS.
    //
    // Condiciones de credito, dias festivos, tipos de recordatorio y tipos de
    // gasto se administran en el web con `catalogos.crear/.editar/.baja`, las
    // genericas que comparten con otros siete catalogos. No se pueden atar a
    // Mac31 sin quitarle el alta de bancos a quien la tiene (ver la cabecera),
    // asi que cada forma recibe sus propias claves y las genericas se quedan
    // como semilla del web.
    //
    // CUIDADO AL LEER LA PANTALLA DE PERMISOS mientras esto sea asi: estas
    // claves se ENSENAN y se reparten desde Mac31, pero HOY NO LAS COMPRUEBA
    // NINGUN ENDPOINT. Quien tenga `catalogos.crear` sigue dando de alta dias
    // festivos aunque "Catalogos - Dias festivos: alta" aparezca apagado. Se
    // crean para que el dia que CatalogosAdminController separe las genericas
    // por catalogo el permiso ya venga heredado y nombrado, no para cerrar
    // nada hoy.
    // -----------------------------------------------------------------------
    private static readonly Dictionary<string, (string Key, string Name)> CatalogosCondicionesCreditoMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnVer"] = ("catalogos.condicionescredito.ver", "Catálogos - Condiciones de crédito: ver detalle"),
        ["btnNuevo"] = ("catalogos.condicionescredito.crear", "Catálogos - Condiciones de crédito: alta"),
        ["btnModificar"] = ("catalogos.condicionescredito.editar", "Catálogos - Condiciones de crédito: modificar")
    };

    private static readonly Dictionary<string, (string Key, string Name)> CatalogosDiasFestivosMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnBuscar"] = ("catalogos.diasfestivos.consultar", "Catálogos - Días festivos: consultar"),
        ["btnVer"] = ("catalogos.diasfestivos.ver", "Catálogos - Días festivos: ver detalle"),
        ["btnNuevo"] = ("catalogos.diasfestivos.crear", "Catálogos - Días festivos: alta"),
        ["btnModificar"] = ("catalogos.diasfestivos.editar", "Catálogos - Días festivos: modificar")
    };

    private static readonly Dictionary<string, (string Key, string Name)> CatalogosTiposRecordatorioMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnBuscar"] = ("catalogos.tiposrecordatorio.consultar", "Catálogos - Tipos de recordatorio: consultar"),
        ["btnVer"] = ("catalogos.tiposrecordatorio.ver", "Catálogos - Tipos de recordatorio: ver detalle"),
        ["btnNuevo"] = ("catalogos.tiposrecordatorio.crear", "Catálogos - Tipos de recordatorio: alta"),
        ["btnModificar"] = ("catalogos.tiposrecordatorio.editar", "Catálogos - Tipos de recordatorio: modificar")
    };

    private static readonly Dictionary<string, (string Key, string Name)> CatalogosTiposGastoMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnBuscar"] = ("catalogos.tiposgasto.consultar", "Catálogos - Tipos de gasto: consultar"),
        ["btnVer"] = ("catalogos.tiposgasto.ver", "Catálogos - Tipos de gasto: ver detalle"),
        ["btnNuevo"] = ("catalogos.tiposgasto.crear", "Catálogos - Tipos de gasto: alta"),
        ["btnModificar"] = ("catalogos.tiposgasto.editar", "Catálogos - Tipos de gasto: modificar")
    };

    // -----------------------------------------------------------------------
    // LOS TRES QUE YA TIENEN CLAVE PROPIA EN EL WEB: GALLEROS, AUTOS Y
    // EMPRESAS. Aqui la herencia si enciende permisos que el backend comprueba
    // hoy, y son los tres casos que de verdad quitan doble captura.
    //
    // GALLEROS es el empate perfecto: btnVer -> `catalogos.galleros.ver`,
    // btnNuevo -> `.crear`, btnModificar -> `.editar`, las tres claves ya
    // sembradas y comprobadas por CatalogosAdminController. btnBuscar no tiene
    // clave web y se queda con la derivada `.consultar`.
    //
    // EL btnBuscar Y EL btnVer SON DOS PERMISOS, Y NO SE JUNTAN. Es la misma
    // trampa que ya se documento en proveedores con el boton que ABRE la
    // pantalla: en Mac31 buscar y ver el detalle se reparten por separado, asi
    // que juntarlos en `catalogos.galleros.ver` habria ensanchado el permiso —
    // quien solo puede buscar acabaria con el detalle, que es el que trae el
    // SALDO del gallero. El btnVer se queda con el `.ver` que el web ya
    // comprueba y el btnBuscar recibe su propia clave.
    // -----------------------------------------------------------------------
    private static readonly Dictionary<string, (string Key, string Name)> CatalogosGallerosMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnBuscar"] = ("catalogos.galleros.consultar", "Catálogos - Galleros: consultar"),
        ["btnVer"] = ("catalogos.galleros.ver", "Catálogos - Galleros y su saldo"),
        ["btnNuevo"] = ("catalogos.galleros.crear", "Catálogos - Alta de galleros"),
        ["btnModificar"] = ("catalogos.galleros.editar", "Catálogos - Modificar galleros")
    };

    /*
      AUTOS: btnNuevo y btnModificar caen en claves que el web ya comprueba;
      btnVer y btnImprimir no, porque el listado de autos se lee con la
      generica `catalogos.ver` (medido en CatalogosAdminController.Lecturas:
      pide `PermisosVer`) y el web no imprime la ficha. Se les da clave propia
      igualmente, con la misma advertencia de arriba: se ensenan pero todavia
      no cierran nada.

      OJO CON LOS DUPLICADOS DE TAURO. Medido el 2026-10-10: en
      Produccion_svr, ConsultarAutos tiene btnImprimir DOS veces (5197 y 5199)
      y btnExcel DOS veces (5198 y 5200); en MacZ aparece una vez cada uno. El
      catalogo deduplica por clave y se queda con el primero por Orden, o sea
      el 5197, asi que quien tuviera encendido solo el 5199 no contaria. No se
      arregla desde aqui —es un renglon repetido en n_Procesos de esa base— y
      hoy no tiene consecuencia porque `catalogos.autos.imprimir` no la
      comprueba nadie. Queda escrito para cuando la comprueben.
    */
    private static readonly Dictionary<string, (string Key, string Name)> CatalogosAutosMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnVer"] = ("catalogos.autos.ver", "Catálogos - Autos: ver detalle"),
        ["btnNuevo"] = ("catalogos.autos.crear", "Catálogos - Alta de autos"),
        ["btnModificar"] = ("catalogos.autos.editar", "Catálogos - Modificar autos"),
        ["btnImprimir"] = ("catalogos.autos.imprimir", "Catálogos - Autos: imprimir")
    };

    /*
      EMPRESAS: la ficha trae RFC, regimen fiscal, CLABE y el folio con el que
      se factura, y por eso el web ya le dio claves propias
      (`catalogos.empresas.crear/.editar/.baja`) en vez de dejarla con las
      genericas. btnNuevo y btnModificar empatan con las dos primeras.

      btnNuevoSerie y btnModificarSerie NO se mapean: el web gatea las series
      con la misma clave que la empresa, asi que una clave aparte no la
      comprobaria nadie. Al no estar en la tabla de verbos, se ignoran solas.
    */
    private static readonly Dictionary<string, (string Key, string Name)> CatalogosEmpresasMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnVer"] = ("catalogos.empresas.ver", "Catálogos - Empresas: ver detalle"),
        ["btnNuevo"] = ("catalogos.empresas.crear", "Catálogos - Alta de empresas y series"),
        ["btnModificar"] = ("catalogos.empresas.editar", "Catálogos - Modificar empresas y series")
    };

    /// <summary>
    /// Los once catalogos que viven bajo el prefijo "catalogos." del web, cada
    /// uno con su forma de Mac31 y su propio prefijo de clave.
    /// </summary>
    /// <remarks>
    /// UNA SOLA FAMILIA Y NO ONCE, porque lo que `LaMandaLegacy` borra es UN
    /// prefijo: "catalogos.". Once familias con el mismo prefijo borrarian
    /// once veces lo mismo y cada una tendria que repetir las catorce semillas
    /// del web, que es la lista que de verdad hay que mantener bien.
    ///
    /// LAS CATORCE SEMILLAS SON LO MAS DELICADO DE ESTE ARCHIVO. `LaMandaLegacy`
    /// borra del snapshot TODO lo que empiece por "catalogos." y no este en
    /// esta lista. Medido: en WPermiso de las dos bases hay exactamente 24
    /// claves "catalogos.*"; diez las hereda Mac31 por las ataduras de abajo y
    /// las catorce restantes son estas. Si a alguna le faltara el renglon, se
    /// perderia sin error y sin rastro —la misma cicatriz que ya documentan
    /// ventas e inventario—, y una de ellas es `catalogos.ver`, que exigen unos
    /// 40 endpoints de lectura de medio sistema.
    /// </remarks>
    private static readonly FamiliaLegacy CatalogosFamilia = new(
        "catalogos.", "catalogos", "Catalogos",
        // Sin entrada al modulo a proposito. Ver la cabecera del archivo y el
        // apartado "LO QUE NO SE PUERTEA" de EntradasDeModulo.cs.
        null, null,
        new List<PermissionSeed>
        {
            // La entrada la sigue repartiendo el web: no se deriva de ningun
            // boton, pero SI se siembra aqui para que el borrado por prefijo
            // no se la lleve.
            new("catalogos.ver_modulo", "Catálogos - Ver módulo", "catalogos"),

            // Las genericas. Una sola clave para once catalogos del web, de
            // los cuales solo cuatro tienen forma en Mac31: no se atan.
            new("catalogos.ver", "Catálogos - Consultar", "catalogos"),
            new("catalogos.crear", "Catálogos - Dar de alta", "catalogos"),
            new("catalogos.editar", "Catálogos - Modificar", "catalogos"),
            new("catalogos.baja", "Catálogos - Dar de baja / reactivar", "catalogos"),

            // Pantallas del web sin forma de Mac31 en este alcance.
            new("catalogos.fe.ver", "Catálogos - Facturación electrónica", "catalogos"),
            new("catalogos.recaudaciones.ver", "Catálogos - Recaudaciones", "catalogos"),
            new("catalogos.formas.ver", "Catálogos - Formas de Mac31", "catalogos"),
            new("catalogos.territorial.crear", "Catálogos - Alta territorial", "catalogos"),
            new("catalogos.territorial.editar", "Catálogos - Modificar territorial", "catalogos"),

            // Las cuatro bajas: ninguna de estas formas tiene boton de baja ni
            // de eliminar dado de alta en n_Procesos, asi que alla no hay nada
            // que leer y se quedan del web.
            new("catalogos.marcas.baja", "Catálogos - Baja de marcas y categorías", "catalogos"),
            new("catalogos.galleros.baja", "Catálogos - Baja de galleros", "catalogos"),
            new("catalogos.empresas.baja", "Catálogos - Baja de empresas y series", "catalogos"),
            new("catalogos.autos.baja", "Catálogos - Baja de autos", "catalogos")
        },
        new List<LegacyModuleBinding>
        {
            // El grupo de marcas / categorias / subcategorias. ConsultarMarcas
            // va PRIMERA porque es la que manda en "catalogos.marcas.*" y,
            // siendo la primera, es la que carga las semillas del web.
            new("catalogos", "Catalogos", "CONSULTA DE MARCAS", CatalogosMarcasMap,
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarMarcas",
                Prefijo: "catalogos.marcas"),

            // "CONSULTA DE CATEGORIAS" en Mac31 = los GRUPOS = lo que el web
            // publica en /api/catalogos/grupos-categoria y llama "categorias".
            new("catalogos", "Catalogos", "CONSULTA DE CATEGORIAS", CatalogosGruposCategoriaMap,
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarGrupoCategorias",
                Prefijo: "catalogos.gruposcategoria"),

            // "CONSULTA DE SUB CATEGORIAS" = /api/catalogos/categorias-mac =
            // lo que el web llama "subcategorias".
            new("catalogos", "Catalogos", "CONSULTA DE SUB CATEGORIAS", CatalogosCategoriasMacMap,
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarCategorias",
                Prefijo: "catalogos.categoriasmac"),

            new("catalogos", "Catalogos", "RELACION DE CATEGORIAS Y MARCAS", CatalogosRelacionCategoriasMarcasMap,
                Array.Empty<PermissionSeed>(),
                FormaExacta: "RelacionGrupoCategoriaMarcas",
                Prefijo: "catalogos.relacioncategoriasmarcas"),

            // La descripcion de esta forma esta mal escrita en la base
            // ("CONDIDCIONES"). Da igual: se empata por FormaExacta y la
            // descripcion solo queda aqui como referencia para quien la busque.
            new("catalogos", "Catalogos", "CONSULTA DE CONDIDCIONES DE CREDITO", CatalogosCondicionesCreditoMap,
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarCondicionesCredito",
                Prefijo: "catalogos.condicionescredito"),

            // Ojo con el nombre: la forma es "ConsultaDiasFestivos", sin la "r"
            // que llevan todas las demas.
            new("catalogos", "Catalogos", "CONSULTA DE DIAS FESTIVOS", CatalogosDiasFestivosMap,
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultaDiasFestivos",
                Prefijo: "catalogos.diasfestivos"),

            new("catalogos", "Catalogos", "CONSULTA DE TIPOS DE RECORDATORIO", CatalogosTiposRecordatorioMap,
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarTipoRecordatorios",
                Prefijo: "catalogos.tiposrecordatorio"),

            new("catalogos", "Catalogos", "CONSULTA DE TIPOS DE GASTOS", CatalogosTiposGastoMap,
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarTipoGastos",
                Prefijo: "catalogos.tiposgasto"),

            new("catalogos", "Catalogos", "CONSULTA DE GALLEROS", CatalogosGallerosMap,
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarGalleros",
                Prefijo: "catalogos.galleros"),

            new("catalogos", "Catalogos", "CONSULTA DE AUTOS", CatalogosAutosMap,
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarAutos",
                Prefijo: "catalogos.autos"),

            new("catalogos", "Catalogos", "CONSULTA DE EMPRESAS", CatalogosEmpresasMap,
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarEmpresas",
                Prefijo: "catalogos.empresas")
        });

    /*
      RECORDATORIOS  <-  "CONSULTA DE RECORDATORIOS" (ConsultarRecordatorios,
      IDForma 4077)

      POR QUE NO ESTA EN LA FAMILIA DE ARRIBA. Porque el prefijo de sus claves
      en el web NO es "catalogos.": es "recordatorios.", un modulo propio con
      su pantalla y sus endpoints en SueltosController. Meterlo arriba habria
      hecho que `LaMandaLegacy` no lo reconociera (borra por "catalogos.") y la
      sustitucion no se habria aplicado nunca; y al reves, declarar
      "recordatorios.*" bajo el prefijo "catalogos." dejaria dos duenos para la
      misma clave.

      ESTA SI LLEVA ENTRADA AL MODULO, y es la diferencia que importa frente a
      los catalogos. "recordatorios." SI esta en la tabla de EntradasDeModulo
      (-> "recordatorios.ver_modulo"), asi que el backend EXIGE esa puerta: sin
      derivarla, quien heredara "recordatorios.crear" de Mac31 y no tuviera la
      entrada repartida a mano en el web recibiria 403 en todos los endpoints,
      que es exactamente el agujero que describe el comentario de
      AplicarFamiliasLegacyAsync. Se deriva de "recordatorios.ver" (el btnVer),
      que es la clave que el controlador pide para leer, igual que proveedores.

      "recordatorios.ver_modulo" ADEMAS SE SIEMBRA, porque empieza por
      "recordatorios." y sin el renglon el borrado por prefijo se la llevaria a
      quien ya la tuviera repartida.

      LO QUE SE DERIVA Y LO QUE SE CREA: btnVer, btnNuevo y btnModificar caen
      en las tres claves que el web ya comprueba. btnBuscar y btnCancelar no
      tienen clave web (el web no cancela recordatorios todavia) y reciben
      "recordatorios.consultar" y "recordatorios.cancelar", que hoy no
      comprueba nadie.
    */
    private static readonly Dictionary<string, (string Key, string Name)> RecordatoriosMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnBuscar"] = ("recordatorios.consultar", "Recordatorios - Consultar listado"),
        ["btnVer"] = ("recordatorios.ver", "Recordatorios - Consultar"),
        ["btnNuevo"] = ("recordatorios.crear", "Recordatorios - Crear"),
        ["btnModificar"] = ("recordatorios.editar", "Recordatorios - Modificar"),
        ["btnCancelar"] = ("recordatorios.cancelar", "Recordatorios - Cancelar")
    };

    private static readonly FamiliaLegacy RecordatoriosFamilia = new(
        "recordatorios.", "recordatorios", "Recordatorios",
        "recordatorios.ver_modulo", "recordatorios.ver",
        new List<PermissionSeed>
        {
            new("recordatorios.ver_modulo", "Recordatorios - Ver módulo", "recordatorios")
        },
        new List<LegacyModuleBinding>
        {
            new("recordatorios", "Recordatorios", "CONSULTA DE RECORDATORIOS", RecordatoriosMap,
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarRecordatorios",
                Prefijo: "recordatorios")
        });
}
