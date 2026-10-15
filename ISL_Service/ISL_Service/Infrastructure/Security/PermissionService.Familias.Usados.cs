using System;
using System.Collections.Generic;

namespace ISL_Service.Infrastructure.Security;

/*
  USADOS / CASCOS, GASOLINAS, BANCOS-TRANSFERENCIAS, DESCUENTOS Y CHECADOR.

  Seis familias que heredan de Mac31 lo que hoy se captura DOS veces: una en
  Mac31 y otra en la pantalla de permisos del web. Todas las claves de abajo se
  buscaron primero en el catalogo del web (PermissionService.cs), en las
  politicas de los controladores y en lo que pide el front de docs2; las que son
  nuevas estan marcadas con NUEVA y dichas en el informe.

  MEDIDO EN LAS DOS EMPRESAS EL 2026-10-10 (MacZ = Zaragoza,
  Produccion_svr = Tauro), y aqui NO se escribe ningun IDProceso:

    ALMACEN CASCOS / btnBuscar       -> 5303 en MacZ, 5304 en Tauro
    CONSULTA DE GASOLINAS / btnNuevo -> 5174 en MacZ, 5183 en Tauro
    CONSULTA DE BANCO TRANSFERENCIAS / btnBuscar -> 5395 / 5397
    VER BANCO TRANSFERENCIAS / btnIdentificar    -> 5399 / 5401
    DETALLE CORTE CENTRO DE SERVICIO / btnGuardar-> 5379 / 5381
    REPORTE ASISTENCIAS Y RECIBOS / btnVacaciones-> 5151 / 5148

  Las formas viejas (IDForma 4, 5, 8, 15, 16, 1019, 4025, 4031) si tienen los
  mismos numeros en las dos; las nuevas no. El catalogo se arma leyendo
  n_Procesos por la conexion de la empresa en curso, asi que da igual.

  LO QUE *NO* SE HEREDA, Y POR QUE. Esta lista es parte de la entrega:

  1. CONSULTA DE ALMACENES (ConsultarAlmacenes, IDForma 15) y CONSULTA DE
     BANCOS (ConsultarBancos, IDForma 16) NO SE ATAN A NADA.
     En el web esas dos pantallas no tienen claves propias: son dos renglones
     del modulo generico de Catalogos y sus botones los gobiernan
     `catalogos.crear`, `catalogos.editar` y `catalogos.baja`, que son
     COMPARTIDOS por ~40 catalogos (y `catalogos.ver` lo exigen ~40 endpoints
     de lectura que llenan los combos de Compras, Ventas, Garantias y
     Cobranza). Atarlas tendria dos efectos malos y ninguno bueno:
       * una familia con prefijo "catalogos." borraria del snapshot las 25
         claves `catalogos.*` de todo el mundo (LaMandaLegacy borra por
         prefijo) y las recalcularia desde UNA sola forma de Mac31;
       * y daria o quitaria el alta de marcas, galleros, empresas y autos
         segun quien pueda dar de alta un banco.
     Inventar `catalogos.bancos.*` tampoco sirve: nada lo comprueba. Si algun
     dia los catalogos se parten por catalogo, estas dos formas entran solas.

  2. REPORTE ASISTENCIAS Y RECIBOS (ReporteEmpleadoAsistenciaRecibos,
     IDForma 8) NO SE HEREDA. Son las dos razones del informe:
       * btnRecibos es el recibo de nomina, o sea IMPORTES DE SUELDO, y en el
         web los importes los protege `empleados.sueldo.ver`
         (NominaBatchController: sin esa clave el batch corre y los importes
         vuelven en null). Heredar cualquier clave desde este boton le daria
         el recibo a quien Mac31 autorizo a verlo en SU pantalla, que no es lo
         mismo que tener `empleados.sueldo.ver` en el web. Hay una auditoria
         abierta sobre fuga de importes: esto se PARA y se reporta.
       * y los otros cuatro botones (btnAsistencias, btnInasistencias,
         btnVacaciones, btnAsistenciasCorto) caerian en `asistencias.*` o en
         `reportes.*`, dos prefijos que ya tienen dueno (AsistenciasFamilia y
         el puente viejo de reportes). Un segundo dueno del mismo prefijo hace
         que quien borra y quien recalcula dependan del orden de la lista.

  3. DETALLE CORTE CENTRO DE SERVICIO (CorteDiarioCS, IDForma 4070) NO LLEVA
     FAMILIA AQUI. "cortes." ya lo gobierna CortesFamilia y una segunda
     familia con el mismo prefijo competiria con ella. Es una TERCERA forma
     del mismo asunto y va dentro de CortesFamilia; en el informe queda el
     renglon exacto que hay que agregarle.

  4. LIQUIDACIONES DESCUENTOS MOVIMIENTOS (LiquidacionesDescuento, IDForma 5)
     no se ata: ver la nota larga en la familia de descuentos.
*/
public sealed partial class PermissionService
{
    /*
      ============================================================
      ALMACEN DE CASCOS  <-  "ALMACEN CASCOS" (ConsultarUsadosMovimientos)
      ============================================================

      QUE PANTALLA ES, PORQUE EL NOMBRE DE LA FORMA ENGANA.
      La forma se llama ConsultarUsadosMovimientos pero su Descripcion es
      "ALMACEN CASCOS" y es la que abre btnCascosPiezas del menu de Mac31
      (Menu.cs:1873): las SALIDAS y ENTRADAS de cascos vacios con el
      repartidor. En el web eso es el modulo `almacen_cascos` —asi lo dice el
      renglon del menu en docs2 (nucleo/menuLegacy.ts) y asi esta en
      dbo.WModulo de MacZ—, NO `cascos_cambio` (la cuenta corriente de cascos
      entre empresas, que no tiene forma en Mac31) ni los `usados.*` de la
      pantalla de dinero de usados.

      POR QUE btnBuscar VA A MANO Y NO DERIVADO.
      La tabla de verbos manda btnBuscar a ".consultar", y el web no conoce
      "almacen_cascos.consultar": su clave de lectura es `almacen_cascos.ver`,
      la que exige GET api/almacen-cascos/existencias y la que el front pide
      para dibujarse. Derivarla habria creado una clave que nadie comprueba y
      habria dejado la de verdad sin heredar. Mismo criterio que garantias.

      LA TRAMPA DE LOS DOS BOTONES PARA UNA CLAVE: ENTRADA Y SALIDA.
      En el web `almacen_cascos.crear` cubre POST /salidas y POST /entradas;
      en Mac31 son DOS permisos (btnSalida y btnEntrada). El catalogo guarda UN
      IDProceso por clave, asi que mapear los dos a `.crear` haria que el
      permiso que manda dependa del Orden de la fila. Se elige btnSalida,
      porque crear el movimiento ES la salida (la entrada solo ACEPTA una
      salida ya registrada), y btnEntrada se queda con clave propia.
      CONSECUENCIA MEDIDA Y DICHA EN EL INFORME: quien en Mac31 tenga ENTRADA
      y no SALIDA no hereda `almacen_cascos.crear`. Medido el 2026-10-10 en las
      dos empresas: NINGUN usuario con cuenta web esta en ese caso, asi que
      hoy la eleccion no le quita el permiso a nadie.

      LO QUE SI LE QUITA A ALGUIEN, Y ES EL AVISO GRANDE DE ESTA FAMILIA.
      En MacZ el rol EMPLEADO tiene concedidas `almacen_cascos.ver`, `.crear`,
      `.editar` y `.cancelar` por ROL, y FERNANDA (ese rol, IDUsuario legacy
      1064) NO tiene ni una fila en [Usuario Forma Procesos] para esta forma.
      Al heredar, pierde `.ver`, `.crear` y `.cancelar` —`.editar` se salva
      porque es semilla—. Eso no es un fallo del puente: es lo que significa
      "manda Mac31". Si se quiere que lo conserve, hay que darle los botones en
      Mac31, que es el unico lugar donde a partir de ahora se reparten.

      btnVenta tambien lleva clave propia: en Mac31 vende los cascos del
      almacen y el web todavia no tiene ese paso.
    */
    private static readonly Dictionary<string, (string Key, string Name)> AlmacenCascosLegacyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnBuscar"] = ("almacen_cascos.ver", "Almacen de Cascos - Consultar"),
        ["btnSalida"] = ("almacen_cascos.crear", "Almacen de Cascos - Registrar salida con el repartidor"),
        // NUEVA: el web acepta la entrada con `almacen_cascos.crear`, pero
        // Mac31 reparte ENTRADA aparte y juntarlas ensancharia el permiso.
        ["btnEntrada"] = ("almacen_cascos.entrada.aceptar", "Almacen de Cascos - Aceptar la entrada de la salida"),
        // NUEVA: no existe en el web; se declara para no perder el permiso de
        // Mac31 el dia que la pantalla tenga el paso de venta.
        ["btnVenta"] = ("almacen_cascos.venta", "Almacen de Cascos - Vender cascos del almacen")
    };

    /// <summary>
    /// El almacen de cascos vacios (salidas y entradas con el repartidor).
    /// </summary>
    private static readonly FamiliaLegacy AlmacenCascosFamilia = new(
        "almacen_cascos.", "almacen_cascos", "Almacen de Cascos",
        // La entrada ya existe en el catalogo del web y en EntradasDeModulo
        // ("almacen_cascos." -> almacen_cascos.ver_modulo); aqui solo se dice
        // de que permiso de Mac31 se deriva, para no pedirla a mano a quien ya
        // puede consultar el almacen alla.
        "almacen_cascos.ver_modulo", "almacen_cascos.ver",
        new List<PermissionSeed>
        {
            // Se declara como semilla ADEMAS de derivarse: el borrado por
            // prefijo se la llevaria antes de que la derivacion la vuelva a
            // poner, y quien la tenga del web sin tener btnBuscar en Mac31
            // perderia la puerta del modulo.
            new("almacen_cascos.ver_modulo", "Almacen de Cascos - Ver modulo", "almacen_cascos"),
            // `almacen_cascos.editar` (corregir kilos, repartidor y
            // observaciones) NO tiene boton en n_Procesos: en Mac31 esa
            // correccion se hace dentro de la misma pantalla sin permiso
            // propio. Se queda del web.
            new("almacen_cascos.editar", "Almacen de Cascos - Modificar movimiento", "almacen_cascos")
        },
        new List<LegacyModuleBinding>
        {
            // btnCancelar -> almacen_cascos.cancelar  (derivado, clave que el
            // web YA comprueba) y btnImprimir -> almacen_cascos.imprimir
            // (derivado, NUEVA: el web todavia no imprime el movimiento).
            new("almacen_cascos", "Almacen de Cascos", "ALMACEN CASCOS",
                AlmacenCascosLegacyMap,
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarUsadosMovimientos",
                Prefijo: "almacen_cascos")
        });

    /*
      ============================================================
      TIPOS DE USADOS  <-  "CONSULTA DE TIPOS DE USADOS" (ConsultarTipoUsados)
      ============================================================

      SIN MAPA: los tres botones son btnNuevo, btnVer y btnModificar, y la
      tabla de verbos los deriva. Con Prefijo "usados.tipos" el btnModificar
      cae justo en `usados.tipos.editar`, que es la clave que ya exige
      InventarioOperacionesController. Es la prueba de que la derivacion no
      inventa: la clave del web salio sola.

      EL PREFIJO ES "usados.tipos." Y NO "usados.", A PROPOSITO.
      El modulo `usados.*` del web tiene once claves y DIEZ son de otras
      pantallas (cobros de dinero, ingresos, excedentes, movimientos del
      almacen de usados). Una familia con prefijo "usados." borraria esas diez
      del snapshot de todo el mundo —LaMandaLegacy borra por prefijo— y
      obligaria a declararlas aqui una por una, en un archivo que no es su
      dueno. Con el prefijo largo esta familia solo manda sobre lo suyo.

      NO LLEVA ClaveDeEntrada: no existe ninguna `usados.ver_modulo` en el
      catalogo del web, y EntradasDeModulo no tiene renglon "usados.", asi que
      estas claves no viven detras de ninguna puerta. Inventar la entrada seria
      sembrar una llave que nadie reparte.
    */
    private static readonly FamiliaLegacy UsadosTiposFamilia = new(
        "usados.tipos.", "usados", "Cascos usados", null, null,
        Array.Empty<PermissionSeed>(),
        new List<LegacyModuleBinding>
        {
            // btnNuevo -> usados.tipos.crear (NUEVA) y btnVer ->
            // usados.tipos.ver (NUEVA): el web resuelve el alta y la
            // modificacion con una sola clave, `usados.tipos.editar`. Se
            // dejan derivar porque en Mac31 SI son tres permisos distintos y
            // juntarlos los ensancharia.
            new("usados", "Cascos usados", "CONSULTA DE TIPOS DE USADOS",
                new Dictionary<string, (string Key, string Name)>(StringComparer.OrdinalIgnoreCase),
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarTipoUsados",
                Prefijo: "usados.tipos")
        });

    /*
      ============================================================
      GASOLINAS  <-  "CONSULTA DE GASOLINAS" (ConsultarGasolinas)
      ============================================================

      SIN MAPA Y EMPATE PERFECTO: btnNuevo -> gasolinas.crear,
      btnVer -> gasolinas.ver y btnCancelar -> gasolinas.cancelar, las tres
      claves que ya exige GasolinasController y que ya pide el front.

      LO QUE NO SE PUEDE HEREDAR Y HAY QUE SABER: `gasolinas.editar`.
      En Mac31 el boton de MODIFICAR de esta pantalla SE LLAMA btnVer. Medido
      en las dos empresas: la forma tiene DOS procesos llamados btnVer, uno con
      Descripcion "VER" (Orden 5) y otro con "MODIFICAR" (Orden 10). La llave
      del mapa es el NOMBRE del control, asi que los dos derivan a
      "gasolinas.ver" y el catalogo se queda con el primero por Orden, que es
      el de VER —el correcto—. El de MODIFICAR queda inalcanzable: no hay forma
      de distinguirlos sin escribir un IDProceso, y los IDProceso no son
      iguales entre empresas (5176 en MacZ, 5185 en Tauro). Por eso
      `gasolinas.editar` se queda como semilla del web.
    */
    private static readonly FamiliaLegacy GasolinasFamilia = new(
        "gasolinas.", "gasolinas", "Gasolinas",
        "gasolinas.ver_modulo", "gasolinas.ver",
        new List<PermissionSeed>
        {
            new("gasolinas.ver_modulo", "Gasolinas - Ver modulo", "gasolinas"),
            // Ver la nota de arriba: el boton de MODIFICAR de Mac31 se llama
            // btnVer y no se puede separar del de VER.
            new("gasolinas.editar", "Gasolinas - Corregir recibo", "gasolinas")
        },
        new List<LegacyModuleBinding>
        {
            new("gasolinas", "Gasolinas", "CONSULTA DE GASOLINAS",
                new Dictionary<string, (string Key, string Name)>(StringComparer.OrdinalIgnoreCase),
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarGasolinas",
                Prefijo: "gasolinas")
        });

    /*
      ============================================================
      BANCOS Y TRANSFERENCIAS  <-  dos formas
        "CONSULTA DE BANCO TRANSFERENCIAS" (ConsultarBancosTransferencias)
        "VER BANCO TRANSFERENCIAS"         (VerBancosTransferencias)
      ============================================================

      LA CLAVE DEL MODULO ES `transferencias` Y NO `bancos_transferencias`:
      asi esta en dbo.WModulo, asi son sus cinco permisos y asi lo dice el
      renglon del menu en docs2.

      LA TRAMPA DEL BOTON QUE ABRE OTRA PANTALLA, OTRA VEZ.
      Las DOS formas tienen un btnVer con Descripcion "VER MOVIMIENTOS", y no
      son el mismo permiso: el de la pantalla de consulta ABRE la pantalla de
      movimientos, y el de dentro de la pantalla de movimientos ensena el
      movimiento. Si los dos derivaran a la misma clave, quien tuviera uno se
      quedaria con los dos. Asi que el de fuera lleva sufijo `.abrir` y el de
      dentro se queda con el `.ver` derivado bajo su propio prefijo
      ("transferencias.movimientos"). Es el mismo reparto que ya se midio en
      los domicilios de proveedores de obra.

      Y por eso cada forma necesita su `Prefijo`: con el del modulo, el btnVer
      de dentro tambien pediria "transferencias.ver" y le pisaria la clave al
      btnBuscar de la consulta.
    */
    private static readonly Dictionary<string, (string Key, string Name)> TransferenciasConsultaMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Derivado daria "transferencias.consultar", que el web no conoce: su
        // clave de lectura es `transferencias.ver`.
        ["btnBuscar"] = ("transferencias.ver", "Transferencias - Consultar"),
        ["btnCargarArchivo"] = ("transferencias.cargar", "Transferencias - Cargar estado de cuenta"),
        // El boton que ABRE la pantalla de movimientos. NUEVA, y aparte del
        // `.ver` de dentro a proposito: en Mac31 son dos permisos.
        ["btnVer"] = ("transferencias.movimientos.abrir", "Transferencias - Entrar a los movimientos")
    };

    private static readonly Dictionary<string, (string Key, string Name)> TransferenciasMovimientosMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnIdentificar"] = ("transferencias.identificar", "Transferencias - Identificar cliente"),
        ["btnAplicado"] = ("transferencias.aplicar", "Transferencias - Aplicar pagos"),
        // NUEVA: aplicar la transferencia contra CASCOS en vez de dinero. En
        // el web los dos caminos caen hoy en `transferencias.aplicar`, pero en
        // Mac31 son dos permisos y el de cascos mueve otro saldo.
        ["btnAplicadoCascos"] = ("transferencias.cascos.aplicar", "Transferencias - Aplicar a cascos"),
        // NUEVA: el archivo de comprobacion no existe todavia en el web.
        ["btnComprobacion"] = ("transferencias.comprobacion", "Transferencias - Archivo de comprobacion")
    };

    private static readonly FamiliaLegacy TransferenciasFamilia = new(
        "transferencias.", "transferencias", "Transferencias",
        "transferencias.ver_modulo", "transferencias.ver",
        new List<PermissionSeed>
        {
            new("transferencias.ver_modulo", "Transferencias - Ver modulo", "transferencias"),
            // `transferencias.cancelar` se queda del web: cancelar el
            // movimiento bancario no es un boton de estas dos formas (en
            // Mac31 se cancela desde la pantalla de cobros, cuyo permiso es
            // `cobros.transferencias.cancelar` y vive en otra familia).
            new("transferencias.cancelar", "Transferencias - Cancelar movimiento", "transferencias")
        },
        new List<LegacyModuleBinding>
        {
            new("transferencias", "Transferencias", "CONSULTA DE BANCO TRANSFERENCIAS",
                TransferenciasConsultaMap,
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarBancosTransferencias",
                Prefijo: "transferencias"),

            // btnVer -> transferencias.movimientos.ver (derivado, NUEVA): es
            // el "ver movimiento" de DENTRO, distinto del `.abrir` de fuera.
            new("transferencias", "Transferencias", "VER BANCO TRANSFERENCIAS",
                TransferenciasMovimientosMap,
                Array.Empty<PermissionSeed>(),
                FormaExacta: "VerBancosTransferencias",
                Prefijo: "transferencias.movimientos")
        });

    /*
      ============================================================
      DESCUENTOS Y LIQUIDACIONES  <-  dos formas
        "DESCUENTOS"                        (DetalleDescuentos)
        "CONSULTAR LIQUIDACIONES DESCUENTOS"(ConsultarLiquidacionesDescuentos)
      ============================================================

      DE DONDE SALEN ESTAS CLAVES: no se eligieron aqui. El nucleo de
      liquidaciones del front (docs2, nucleo/liquidaciones.ts) dice textual que
      en Legacy el control es ValidaPermisos sobre LiquidacionesDescuento +
      btnAutorizar/btnRechazar/btnAplicar, y declara
      `descuentos.ajustes.{ver,crear,autorizar}` para la lista de
      liquidaciones y `descuentos.{ver,editar,eliminar}` para el esquema de
      descuento. O sea: el empate con Mac31 ya estaba escrito, solo faltaba
      heredarlo.

      NO LLEVA ClaveDeEntrada, Y ESO ESTA DOCUMENTADO FUERA DE AQUI.
      EntradasDeModulo deja `descuentos.*` expresamente fuera: tiene dos
      entradas candidatas —`liquidaciones.ver_modulo`, que esta declarada
      DENTRO del modulo "descuentos", y ninguna llamada
      `descuentos.ver_modulo`— y tres pantallas colgando del mismo prefijo.
      Mientras no se sepa cual gobierna cual, derivar una entrada apagaria un
      modulo entero. Asi que null, y `liquidaciones.ver_modulo` ni se toca:
      empieza por "liquidaciones." y el borrado por prefijo no la alcanza.

      POR QUE "LIQUIDACIONES DESCUENTOS MOVIMIENTOS" (IDForma 5) NO SE ATA.
      Sus tres botones son btnAutorizar, btnRechazar y btnAplicar, y los dos
      primeros son en el web UNA sola clave ("Ajustes de venta: autorizar o
      rechazar"). Ademas btnAutorizar y btnAplicar se llaman IGUAL que los de
      la forma de arriba, asi que atar las dos formas al mismo prefijo haria
      que el IDProceso que manda dependa del orden de la lista de formas (en
      EnsureFamiliasLegacySyncedAsync la ultima forma sobreescribe la clave).
      Y medido en las dos empresas, los dos botones de autorizar y los dos de
      aplicar los tiene LA MISMA gente (7 y 7 en MacZ, 4 y 4 en Tauro, los
      mismos usuarios), asi que atar solo la forma de arriba no le quita el
      permiso a nadie. btnRechazar no se pierde: en el web rechazar es la
      misma clave que autorizar, y esa si se hereda.
    */
    private static readonly Dictionary<string, (string Key, string Name)> DescuentosDetalleMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // NUEVA. En el web dar de alta y modificar un descuento son la misma
        // clave (`descuentos.editar`); en Mac31 son dos botones. La clave del
        // web se la queda btnModificar por derivacion —modificar es lo que
        // gobierna el front— y el alta lleva clave propia para no ensanchar
        // `descuentos.editar` a quien alla solo puede agregar.
        ["btnAgregar"] = ("descuentos.crear", "Descuentos - Agregar descuento al esquema")
    };

    private static readonly Dictionary<string, (string Key, string Name)> LiquidacionesMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // btnAutorizar no esta en la tabla de verbos (no es un nombre
        // generico), asi que va a mano a la clave que el front ya pide.
        ["btnAutorizar"] = ("descuentos.ajustes.autorizar", "Descuentos - Ajustes de venta: autorizar o rechazar")
    };

    private static readonly FamiliaLegacy DescuentosFamilia = new(
        "descuentos.", "descuentos", "Descuentos", null, null,
        new List<PermissionSeed>
        {
            // La pantalla de DESCUENTOS de Mac31 no tiene boton de buscar ni
            // de ver dado de alta en n_Procesos: alla se entra y se ve. La
            // lectura del esquema se queda del web.
            new("descuentos.ver", "Descuentos - Consultar", "descuentos"),
            // Simular un descuento solo existe en el web.
            new("descuentos.simular", "Descuentos - Simular", "descuentos")
        },
        new List<LegacyModuleBinding>
        {
            // btnModificar -> descuentos.editar y btnEliminar ->
            // descuentos.eliminar: las dos derivadas y las dos son claves que
            // el web YA comprueba.
            new("descuentos", "Descuentos", "DESCUENTOS",
                DescuentosDetalleMap,
                Array.Empty<PermissionSeed>(),
                FormaExacta: "DetalleDescuentos",
                Prefijo: "descuentos",
                /* btnAgregar, btnModificar y btnEliminar: ni un boton de
                   lectura, comprobado en las dos empresas. La semilla de abajo
                   ya lo decia ("alla se entra y se ve") y aun asi la clave no
                   se heredaba, asi que quien podia capturar descuentos en Mac31
                   recibia 403 al abrir la pantalla en el web.
                   OJO: esto NO vale para CONSULTAR LIQUIDACIONES DESCUENTOS,
                   que si tiene btnVer y ya deriva "descuentos.ajustes.ver".
                   Son dos pantallas distintas y no se mezclan. */
                ClaveDeLectura: "descuentos.ver"),

            // Derivadas con prefijo propio: btnNuevo -> .crear,
            // btnVer -> .ver y btnAplicar -> .aplicar son las tres claves que
            // el front ya pide; btnImprimir -> .imprimir es NUEVA (el front
            // dice que hoy no hay boton de imprimir porque el reporte 1030 no
            // esta en el motor nuevo).
            new("descuentos", "Descuentos", "CONSULTAR LIQUIDACIONES DESCUENTOS",
                LiquidacionesMap,
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarLiquidacionesDescuentos",
                Prefijo: "descuentos.ajustes")
        });

    /*
      ============================================================
      CHECADOR  <-  "CHECADOR ASISTENCIA" (LectorHuella)
      ============================================================

      UN SOLO BOTON, btnBuscarHuella, y va a mano porque su nombre no es
      generico: la tabla de verbos no lo conoce y si no se mapeara, el proceso
      se ignoraria y la familia quedaria vacia.

      La clave es `checador.ver` —"Checar entrada/salida", la que exige
      ChecadorController— y no se inventa ninguna otra: las otras dos del
      modulo (`checador.huellas` y `checador.reportes`) no tienen boton en esta
      forma. Administrar huellas es otra pantalla de Mac31 y sus reportes van
      por el puente de reportes.

      LA ENTRADA SI EXISTE, PERO NO DONDE SE BUSCA PRIMERO.
      `checador.ver_modulo` NO esta en las semillas de PermissionService.cs ni
      tiene renglon "checador." en EntradasDeModulo —o sea, el backend no
      exige la entrada para este modulo—, pero SI esta en dbo.WPermiso de las
      DOS empresas y hay un usuario (DEMO) que la tiene concedida: la pide el
      Inicio del front para pintar la tarjeta. Asi que:
        * se declara como semilla del web, porque el borrado por prefijo se la
          llevaria y la tarjeta desapareceria. MEDIDO: sin esta linea, DEMO la
          pierde en las dos bases.
        * y se DERIVA de `checador.ver` para no tener que capturarla a mano a
          quien Mac31 ya deja checar. No se inventa nada: la clave ya existe
          en el catalogo de las dos empresas.
    */
    private static readonly Dictionary<string, (string Key, string Name)> ChecadorMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnBuscarHuella"] = ("checador.ver", "Checador - Checar entrada/salida")
    };

    private static readonly FamiliaLegacy ChecadorFamilia = new(
        "checador.", "checador", "Checador",
        "checador.ver_modulo", "checador.ver",
        new List<PermissionSeed>
        {
            // Las tres se quedan del web: ningun boton de CHECADOR ASISTENCIA
            // las representa, y el borrado por prefijo se las llevaria.
            new("checador.ver_modulo", "Checador - Ver modulo", "checador"),
            new("checador.huellas", "Checador - Administrar huellas", "checador"),
            new("checador.reportes", "Checador - Reportes del checador", "checador")
        },
        new List<LegacyModuleBinding>
        {
            new("checador", "Checador", "CHECADOR ASISTENCIA",
                ChecadorMap,
                Array.Empty<PermissionSeed>(),
                FormaExacta: "LectorHuella",
                Prefijo: "checador")
        });
}
