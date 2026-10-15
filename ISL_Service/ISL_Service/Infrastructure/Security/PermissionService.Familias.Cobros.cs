using System;
using System.Collections.Generic;

namespace ISL_Service.Infrastructure.Security;

/*
  COBROS Y COBRANZA: TRES FAMILIAS, PORQUE SON TRES PREFIJOS.

  Todo lo de abajo se midio el 2026-10-10 contra las DOS bases locales de
  LAP-JUAND (MacZ = Zaragoza, Produccion_svr = Tauro). Solo LECTURA.

  LA PREGUNTA QUE HABIA QUE CONTESTAR PRIMERO: `cobranza.` O `cobranzas.`
  -----------------------------------------------------------------------
  El catalogo del web trae DOS familias para lo que parece lo mismo:

    cobranza.*   9 claves   cobranza.ver, cobranza.cobros.{editar,cerrar,abrir,
                            cancelar}, cobranza.cobranzas.{editar,cancelar},
                            cobranza.concentrados.{editar,cancelar}
    cobranzas.*  6 claves   cobranzas.{ver_modulo,ver,crear,editar,cancelar},
                            cobranzas.pendientes.ver

  MEDIDO, y la respuesta es clara: LA QUE SE USA DE VERDAD ES `cobranza.*`
  (SINGULAR). Las DOS pantallas del front piden claves `cobranza.*`:

    paginas/cobros/CobrosPage.tsx:330      puede(autz, "cobranza.ver")
    paginas/cobros/CobrosPage.tsx:988-1008 cobranza.cobros.{cerrar,abrir,editar}
    paginas/cobranzas/CobranzasPage.tsx:180   puede(autz, "cobranza.ver")
    paginas/cobranzas/CobranzasPage.tsx:544   cobranza.cobranzas.editar
    paginas/cobranzas/CobranzasPage.tsx:555   cobranza.cobranzas.cancelar

  Y el nucleo del front pega contra /api/ventas/cobranza/*
  (nucleo/cobranzas.ts:198,373,395), o sea contra
  VentasCobranzaOperacionesController, que es el que exige `cobranza.*`.

  `cobranzas.*` lo exige SOLO CobranzasController (/api/cobranzas), y a ese
  controlador el front NO LE LLAMA NUNCA: no hay ni una referencia a
  "/api/cobranzas" en docs2/src. De sus seis claves, la unica que hoy sirve
  para algo es `cobranzas.ver_modulo`, que es la llave de la tarjeta COBRANZA
  del inicio (menuLegacy.ts:164 -> modulo "cobranzas").

  NO SE UNIFICA NADA. Las dos se dejan vivas, cada una con su familia, porque
  juntarlas es una migracion de datos y no estaba pedido. Lo que si se hace es
  ALIMENTAR LAS DOS desde la misma forma de Mac31 (ConsultarCobranzas), para
  que el controlador que nadie llama tambien quede con permisos de verdad el
  dia que alguien lo llame.

  POR QUE TRES FAMILIAS Y NO UNA
  ------------------------------
  `LaMandaLegacy` borra POR PREFIJO. Una familia solo puede gobernar UN
  prefijo, y los botones de estas ocho formas caen en tres prefijos distintos
  (`cobranza.`, `cobros.`, `cobranzas.`). Meterlos en una sola familia obligaria
  a producir claves fuera de su prefijo: se anadirian y NUNCA se borrarian, o
  sea una herencia que solo sabe conceder y no sabe quitar. Tres familias, tres
  prefijos, cada una se recalcula entera.

  LOS IDProceso NO SON IGUALES EN LAS DOS EMPRESAS
  ------------------------------------------------
  Aqui no se escribe ni uno. MEDIDO el 2026-10-10, con el MISMO IDForma:

    CobrosDetalle (4028) btnCobroCierre          5155 MacZ / 5153 Tauro
    CobrosDetalle (4028) btnNuevoUsadosExcedentes 5169 MacZ / 5178 Tauro
    CobrosDetalle (4028) btnNuevoPagoKilos       5330 MacZ / 5331 Tauro
    ConsultarCobros (4027) btnDinero             5310 MacZ / 5311 Tauro
    ConsultarVentasPagosUsados (4029) btnVerPago 5294 MacZ / 5299 Tauro
    ConsultarVentasPagosUsados (4029) btnIncobrable 6448 MacZ / 6457 Tauro
    ConsultarDineroUsadosCobros (4071) btnBuscar 5383 MacZ / 5385 Tauro
    ConsultarNuevosPagos (4075) btnNuevo         5401 MacZ / 5403 Tauro
    ReporteCobranza (4078)                       5427 MacZ / 5430 Tauro

  Las unicas que coinciden al numero son ConsultarCobranzas (4023, 5078-5083) y
  los seis primeros procesos de ConsultarCobros (5098-5104) y de CobrosDetalle
  (5105-5108). El resto NO. Por eso la llave del mapa es el NOMBRE del boton y
  el numero lo resuelve la conexion de cada empresa.

  LO QUE SE MIDIO Y SORPRENDIO
  ----------------------------
  * ConsultarCobros tiene 16 procesos activos, no 14.
  * En Tauro, de las formas de cobros (4027/4028/4029/4075) el UNICO usuario
    web con botones concedidos es DEMO. Los cinco usuarios con permisos de
    verdad alla (ADMIN, JANET, JESY, NAOLANY) solo tienen ConsultarCobranzas.
  * NADIE PIERDE NADA en ninguna de las dos bases. El unico usuario web con
    claves `cobros.*`/`cobranza.*`/`cobranzas.*` guardadas en WUsuarioPermiso es
    DEMO (35 filas, todas allow, en las dos bases), y Mac31 le concede todas las
    que estas familias gobiernan. Los demas solo GANAN.

  LO QUE NO SE PUDO HACER Y POR QUE (no se invento nada)
  ------------------------------------------------------
  * ReporteCobranza (IDForma 4078, un solo proceso) deberia encender
    `reportes.ventas.cobranza.ver` (nucleo/cobranza.ts:144). NO SE PUEDE desde
    aqui: el prefijo `reportes.` lo gobierna el codigo a mano de
    PermissionService.cs (`EsReporteQueMandaLegacy`, lineas 1684 y 2540), que
    ademas solo lee la forma "ReportesMenu" (IDForma 4030). Una familia nueva
    con prefijo `reportes.` pelearia con ese borrado. El arreglo correcto es
    ampliar el puente de reportes para que lea tambien la forma ReporteCobranza;
    queda fuera de esta entrega porque obliga a tocar PermissionService.cs.

  * ConsultarDineroUsadosCobros (4071, COBROS INGRESOS) y
    ConsultarDineroUsadosCobrosClientes (4072, COBROS TRABAJADOS) NO se
    heredan aqui. Sus claves del web no son `cobros.*`: son `usados.*`
    (`usados.ver`, `usados.cobros.*`, `usados.cobros_dinero.*`, segun
    paginas/dinero-usados/DineroUsadosPage.tsx:35-38,190 y
    nucleo/cobranza.ts:102-117). Para gobernar `usados.` haria falta una familia
    que cubriera ADEMAS las formas ConsultarTipoUsados (4025, 3 procesos),
    ConsultarUsadosMovimientos (4060, ALMACEN CASCOS, 6 procesos) y los botones
    de excedentes de CobrosDetalle, porque el borrado es por prefijo y
    declararla a medias le quitaria `usados.excedentes.*`,
    `usados.movimientos.*` y `usados.tipos.editar` a quien los tenga.

    Y hay una AMBIGUEDAD MEDIDA que no me toca resolver: `usados.cobros.editar`
    tiene DOS candidatos a origen en Mac31 y las dos fuentes del repo se
    contradicen. nucleo/cobranza.ts:109-116 dice que es "botones CASCOS y VER
    CASCOS de ConsultarCobros"; el nombre de la semilla en
    PermissionService.cs:1 dice "Cascos usados - Ingresos: registrar", que es
    btnEntrada de ConsultarDineroUsadosCobros ("INGRESAR DINERO Y CASCOS").
    Elegir mal le quita el permiso a quien hoy lo tiene por el otro camino. Eso
    se decide mirando Mac31, no la base.

  * La tarjeta COBROS INGRESOS (`dinero_usados.ver_modulo`) no se puede derivar
    por lo mismo: su permiso de origen seria `usados.ver`, que estas familias no
    gobiernan.
*/
public sealed partial class PermissionService
{
    /*
      CONSULTA DE HOJAS DE COBRO (Forma "ConsultarCobros", IDForma 4027, 16
      procesos en las dos empresas).

      SOLO TRES BOTONES SE MAPEAN A MANO, y los tres porque la derivacion NO
      daria la clave que el web ya comprueba:

        btnBuscar    -> cobranza.ver  (la derivacion daria ".consultar", y la
                                       clave que piden CobrosPage:330 y
                                       CobranzasPage:180 es "cobranza.ver")
        btnModificar -> cobranza.cobros.editar   (la derivacion daria
                                       "cobranza.cobros.editar" tambien, pero se
                                       deja explicito porque es el permiso del
                                       endpoint POST cobros/actualizar y conviene
                                       que se lea sin tener que ir a la tabla)
        btnCancelar  -> cobranza.cobros.cancelar (igual)

      LO QUE SE DERIVA SOLO con el Prefijo "cobranza.cobros": btnNuevo
      (".crear"), btnVer (".ver") y btnImprimir (".imprimir"). Esas TRES SON
      CLAVES NUEVAS: hoy el web no da de alta hojas de cobro (el controlador
      solo tiene abrir/cerrar/cancelar/actualizar/consultar) ni imprime. Se
      dejan nacer a proposito: el dia que el web tenga esos botones el permiso
      ya viene de Mac31 sin tocar nada. Son permisos que hoy nada comprueba, y
      eso se dice aqui para que nadie crea que si.

      LOS OCHO BOTONES QUE SE IGNORAN, Y POR QUE NO SE INVENTA SU CLAVE:

        btnDetalleMovimientos, btnPagosCascos   abren OTRA pantalla
        btnDevolucion, btnMultiPago             su clave es de la familia ventas.
                                                ("ventas.devolucion",
                                                 "ventas.pagos.multi")
        btnDinero, btnVerDinero                 su clave es "usados.cobros_dinero.*"
        btnCascos, btnVerCascos                 su clave es "usados.cobros.*"

      Ninguno esta en VerbosLegacy, asi que se ignoran solos. Las cuatro ultimas
      no se pueden heredar desde aqui sin producir claves de otro prefijo, que
      es justo lo que se dijo arriba que no se hace.

      OJO SI ALGUIEN LAS AGREGA MANANA: btnDetalleMovimientos y btnPagosCascos
      ABREN las pantallas CobrosDetalle y ConsultarVentasPagosUsados. En Mac31
      el boton que abre y el btnVer de ADENTRO son DOS permisos distintos; si se
      mapean a la misma clave se ensancha el permiso. El que abre lleva sufijo
      ".abrir", y el de dentro se queda con su ".ver" derivado.
    */
    private static readonly Dictionary<string, (string Key, string Name)> CobranzaCobrosMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnBuscar"] = ("cobranza.ver", "Cobranza - Consultar cobros y cobranzas"),
        ["btnModificar"] = ("cobranza.cobros.editar", "Cobranza - Cobros: modificar"),
        ["btnCancelar"] = ("cobranza.cobros.cancelar", "Cobranza - Cobros: cancelar")
    };

    /*
      HOJA DE COBRO MOVIMIENTOS (Forma "CobrosDetalle", IDForma 4028, 16
      procesos). Es el detalle de la hoja, y aqui viven los DOS botones que el
      web si comprueba y que NO estan en la pantalla de la lista:

        btnCobroCierre -> cobranza.cobros.cerrar  (cuadra el dinero del dia)
        btnCobroAbrir  -> cobranza.cobros.abrir   (DESHACE ese cuadre)

      Son dos claves y no una porque en el web son dos endpoints distintos
      (cobros/cerrar y cobros/abrir): quien puede cuadrar no necesariamente debe
      poder descuadrar.

      EL PREFIJO ES "cobranza.cobros.hoja" Y ESO NO ES CAPRICHO. Esta forma
      tiene su propio btnImprimir ("IMPRIMIR HOJA DE COBRO"), y ConsultarCobros
      tiene el suyo ("IMPRIMIR COBRO"): son DOS permisos distintos en Mac31,
      con IDProceso distintos (5103 y 5108). Si las dos formas derivaran con el
      mismo prefijo, las dos produciria "cobranza.cobros.imprimir", el catalogo
      se indexa por clave y guardaria UN solo IDProceso: el de la forma que
      entre despues. La otra quedaria sin efecto EN SILENCIO. Con prefijos
      distintos son dos claves, como son dos permisos.

      LOS ONCE BOTONES QUE SE IGNORAN: btnCancelarVenta, btnDevolucion y
      btnMultiPago son de la familia ventas.; btnNuevoUsadosExcedentes,
      btnCancelarUsadosExcedentes, btnVerUsadosExcedentes y
      btnNuevoUsadosExcedentesDinero son "usados.excedentes.*";
      btnNuevoPagoExcedente, btnVerPagoExcedente y btnCancelarPagosExcedentes no
      tienen clave web; btnPagosUsados ABRE otra pantalla. btnNuevoPagoKilos
      tampoco se mapea aqui: ver la nota de kilos mas abajo.
    */
    private static readonly Dictionary<string, (string Key, string Name)> CobranzaCobroDetalleMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnCobroCierre"] = ("cobranza.cobros.cerrar", "Cobranza - Cobros: cerrar"),
        ["btnCobroAbrir"] = ("cobranza.cobros.abrir", "Cobranza - Cobros: reabrir")
    };

    /*
      LA FAMILIA `cobranza.` — la que de verdad usan el front y los
      controladores.

      SIN MAPA EN ConsultarCobranzas, Y ESO ES LA PRUEBA DE QUE LA DERIVACION
      NO INVENTA NOMBRES: con Prefijo "cobranza.cobranzas", btnModificar deriva
      "cobranza.cobranzas.editar" y btnCancelar deriva
      "cobranza.cobranzas.cancelar", que son EXACTAMENTE las dos claves que ya
      exige VentasCobranzaOperacionesController (lineas 29-30) y que ya pide
      CobranzasPage (544, 555). Cero mapeo a mano.

      Los otros cuatro botones de esa forma derivan claves nuevas
      (".consultar", ".crear", ".ver", ".imprimir") que hoy nada comprueba. El
      web mete el "armar cobranza" (btnNuevo en Mac31) en la MISMA clave
      ".editar" que el modificar; aqui no se juntan, porque un IDProceso por
      clave es la regla y mapear dos botones a una clave deja al azar cual gana.
      Consecuencia honesta: quien en Mac31 tenga NUEVO pero no MODIFICAR no
      hereda ".editar". Si eso importa, se decide mirando Mac31.

      SEMILLAS DEL WEB: LAS CUATRO DE `cobranza.concentrados.*`.

      Viven bajo este prefijo pero las reparte OTRA familia, la de prefijo
      `cobranza.concentrados.` (PermissionService.Familias.Facturacion.cs), que
      lee CONSULTA DE CONCENTRADOS. Como esta familia manda sobre todo
      `cobranza.`, sin declararlas aqui `LaMandaLegacy` se las llevaria y quien
      las tenga se quedaria sin poder tocar concentrados, sin error y sin
      rastro. MEDIDO: DEMO las tiene en las dos bases.

      Las dos ultimas, `consultar` y `ver`, se sumaron el 2026-10-12: son las
      que ahora ABREN la pantalla de concentrados. Antes la abria
      "cobranza.ver", que es btnBuscar de CONSULTA DE COBROS —otra forma—, y
      por eso quien en Mac31 solo trabaja concentrados veia la tarjeta y la
      pantalla lo rebotaba.

      LA ENTRADA AL MODULO es `cobros.ver_modulo` y se DERIVA de `cobranza.ver`.
      Comprobado en EntradasDeModulo.cs: el prefijo `cobros.` abre con
      "cobros.ver_modulo" o "dinero_usados.ver_modulo", y el prefijo `cobranza.`
      NO esta en esa tabla a proposito (no tiene ".ver_modulo" propio). La
      tarjeta que lleva a esta pantalla es la de modulo "cobros"
      (menuLegacy.ts:168, "LIQUIDACIONES COBROS"). O sea: quien pueda consultar
      las hojas de cobro en Mac31 ve la tarjeta sin que nadie se la capture.
      La clave no se siembra aqui porque ya la siembra el catalogo del web
      (PermissionService.cs:849); se declara como semilla en la familia
      `cobros.` de mas abajo, que es la que la gobierna por prefijo.
    */
    private static readonly FamiliaLegacy CobranzaFamilia = new(
        "cobranza.", "cobranza", "Cobranza",
        "cobros.ver_modulo", "cobranza.ver",
        new List<PermissionSeed>
        {
            new("cobranza.concentrados.editar", "Cobranza - Concentrados: crear y modificar", "cobranza"),
            new("cobranza.concentrados.cancelar", "Cobranza - Concentrados: cancelar", "cobranza"),
            new("cobranza.concentrados.consultar", "Cobranza - Concentrados: buscar", "cobranza"),
            new("cobranza.concentrados.ver", "Cobranza - Concentrados: ver", "cobranza")
        },
        new List<LegacyModuleBinding>
        {
            new("cobranza", "Cobranza", "CONSULTA DE HOJAS DE COBRO", CobranzaCobrosMap,
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarCobros",
                Prefijo: "cobranza.cobros",
                /*
                  Desde la hoja de cobro se llega a pagos, multipago y
                  devolucion, y el web las tiene con sus propias claves
                  (VentasPagosController, VentasMultiPagoController,
                  VentasDevolucionController). En Mac31 esos botones viven aqui
                  y no en CONSULTA DE VENTAS, asi que quien cobra pero no
                  consulta ventas no heredaba ninguna. Medido en Zaragoza:
                  FERNANDA, NORMA, REBECA y RICARDO con btnPagosCascos y sin
                  btnPagos; NORMA y RICARDO con btnMultiPago y btnDevolucion.
                */
                ConcesionesExtra: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["btnPagosCascos"] = "ventas.pagos.ver",
                    ["btnMultiPago"] = "ventas.pagos.multi",
                    ["btnDevolucion"] = "ventas.devolucion"
                }),

            new("cobranza", "Cobranza", "HOJA DE COBRO MOVIMIENTOS", CobranzaCobroDetalleMap,
                Array.Empty<PermissionSeed>(),
                FormaExacta: "CobrosDetalle",
                Prefijo: "cobranza.cobros.hoja",
                /*
                  Lo mismo dentro del detalle, mas los cascos excedentes y la
                  cancelacion de la venta, que tambien estan en el web
                  (VentasUsadosOperacionesController, VentasCancelacionController).
                  Medido en Zaragoza: DEMO, FERNANDA, NORMA, REBECA y RICARDO
                  segun el boton.
                */
                ConcesionesExtra: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["btnPagosUsados"] = "ventas.pagos.ver",
                    ["btnMultiPago"] = "ventas.pagos.multi",
                    ["btnDevolucion"] = "ventas.devolucion",
                    ["btnCancelarVenta"] = "ventas.cancelar",
                    ["btnNuevoUsadosExcedentes"] = "usados.excedentes.editar",
                    ["btnCancelarUsadosExcedentes"] = "usados.excedentes.cancelar"
                }),

            // Sin Map: btnModificar y btnCancelar derivan las claves que el web
            // ya comprueba.
            new("cobranza", "Cobranza", "CONSULTA DE COBRANZAS",
                new Dictionary<string, (string Key, string Name)>(StringComparer.OrdinalIgnoreCase),
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarCobranzas",
                Prefijo: "cobranza.cobranzas")
        });

    /*
      CONSULTA DE NUEVOS PAGOS (Forma "ConsultarNuevosPagos", IDForma 4075, 3
      procesos). Es el "esquema nuevo" de pagos y empata UNO A UNO con los tres
      endpoints de VentasPagosOperacionesController:

        btnNuevo    POST /nuevos           -> cobros.pagos.editar
        btnAplicar  POST /nuevos/aplicar   -> cobros.pagos.aplicar
        btnCancelar POST /nuevos/cancelar  -> cobros.pagos.cancelar

      SOLO btnNuevo VA A MANO: la tabla de verbos lo derivaria a ".crear" y la
      clave del web se llama ".editar". btnAplicar y btnCancelar NO se mapean
      porque la derivacion con Prefijo "cobros.pagos" ya da
      "cobros.pagos.aplicar" y "cobros.pagos.cancelar", clavadas.

      Que esta forma sea la dueña de `cobros.pagos.editar` —y no la de la hoja
      de cobro, que tambien tiene un "NUEVO PAGO"— se decidio leyendo el
      controlador, no adivinando: el nombre de la semilla dice "Pagos del nuevo
      esquema" para ".aplicar" y los tres endpoints estan bajo la ruta /nuevos.
    */
    private static readonly Dictionary<string, (string Key, string Name)> CobrosPagosNuevosMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnNuevo"] = ("cobros.pagos.editar", "Cobros - Pagos del nuevo esquema: capturar")
    };

    /*
      HOJA DE COBRO PAGOS Y CASCOS (Forma "ConsultarVentasPagosUsados", IDForma
      4029, 8 procesos). De los ocho, DOS tienen clave web:

        btnIncobrable            -> cobros.pagos.incobrable
        btnPagoDiferenciaUsados  -> cobros.pagos.kilos

      POR QUE EL DE KILOS ES ESTE BOTON Y NO btnNuevoPagoKilos (que vive en
      CobrosDetalle y se llama igual que la clave): porque nucleo/cobranza.ts:
      130-133 dice que "PAGO CON CASCOS y PAGO CON KILOS son el mismo endpoint
      con la bandera al reves" y los dos van con `cobros.pagos.kilos`. Son DOS
      botones para UNA clave, y la regla es un boton por clave: si se mapearan
      los dos, el catalogo guardaria un solo IDProceso y cual ganara dependeria
      del orden de las filas. Se elige el de ESTA forma porque es la pantalla de
      pagos, que es donde el web pone el boton
      (paginas/cobros/RemisionesDeCobros.tsx:1232 "Pago con cascos o kilos").
      btnNuevoPagoKilos y btnCancelKilos se quedan sin heredar, y se dice.

      LOS OTROS: btnVerPago (clave "ventas.pagos.ver", otra familia),
      btnAgregarUsadoCredito y btnCancelarCascos (familia usados.) y
      btnCancelarPagoDiffUsados (sin clave web). Ninguno esta en VerbosLegacy,
      asi que se ignoran solos.

      EL PREFIJO ES "cobros.pagos.hoja" porque btnNuevo y btnCancelar SI se
      derivan y son el pago de la hoja de cobro, que NO es el pago del esquema
      nuevo de la forma anterior. Con el prefijo "cobros.pagos" los dos pares se
      pisarian ("cobros.pagos.cancelar" saldria de dos formas). Las claves
      "cobros.pagos.hoja.crear" y "cobros.pagos.hoja.cancelar" son NUEVAS y hoy
      nada las comprueba; nacen para que el permiso exista cuando el web haga
      esos dos botones.
    */
    private static readonly Dictionary<string, (string Key, string Name)> CobrosPagosHojaMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnIncobrable"] = ("cobros.pagos.incobrable", "Cobros - Declarar una remision incobrable"),
        ["btnPagoDiferenciaUsados"] = ("cobros.pagos.kilos", "Cobros - Pago con cascos o kilos")
    };

    /*
      LA FAMILIA `cobros.` — los permisos finos de pagos.

      NO DECLARA ClaveDeEntrada. La tarjeta del modulo "cobros" ya la deriva
      CobranzaFamilia (de `cobranza.ver`), que es el permiso que de verdad abre
      la pantalla. Derivarla tambien aqui, desde un permiso de captura, le daria
      la puerta a quien solo puede capturar un pago y no consultar nada.

      LAS TRES SEMILLAS DEL WEB SON OBLIGATORIAS, no decorativas. `LaMandaLegacy`
      borra TODO lo que empiece por "cobros." y no este aqui:

        cobros.ver_modulo              es la entrada al modulo. Sin esta linea
                                       el borrado se la lleva —incluida la que
                                       acaba de derivar CobranzaFamilia, porque
                                       las familias se recorren en orden— y la
                                       tarjeta desaparece del inicio aunque los
                                       endpoints contesten 200. Es la misma
                                       trampa que ya documentan ventas y
                                       empleados.
        cobros.reasignar               su pantalla (reasignacion) no entra en
                                       esta entrega.
        cobros.transferencias.cancelar es de Bancos Transferencias, otra forma.

      MEDIDO: DEMO tiene las tres en las dos bases. Sin declararlas, las tres se
      perderian.
    */
    private static readonly FamiliaLegacy CobrosPagosFamilia = new(
        "cobros.", "cobros", "Cobros", null, null,
        new List<PermissionSeed>
        {
            new("cobros.ver_modulo", "Cobros - Ver modulo (hojas de cobro)", "cobros"),
            new("cobros.reasignar", "Cobros - Reasignar remisiones de cliente o agente", "cobros"),
            new("cobros.transferencias.cancelar", "Cobros - Cancelar transferencias bancarias", "cobros")
        },
        new List<LegacyModuleBinding>
        {
            new("cobros", "Cobros", "CONSULTA DE NUEVOS PAGOS", CobrosPagosNuevosMap,
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarNuevosPagos",
                Prefijo: "cobros.pagos"),

            new("cobros", "Cobros", "HOJA DE COBRO PAGOS Y CASCOS", CobrosPagosHojaMap,
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarVentasPagosUsados",
                Prefijo: "cobros.pagos.hoja",
                /* Ver el pago y agregar un usado a credito ya estan en el web
                   (VentasPagosController, VentasUsadosController). Medido en
                   Zaragoza: FERNANDA, NORMA, REBECA y RICARDO con btnVerPago y
                   sin btnPagos; REBECA y RICARDO con btnAgregarUsadoCredito y
                   sin btnGuardar de consulta de ventas usados. */
                ConcesionesExtra: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["btnVerPago"] = "ventas.pagos.ver",
                    ["btnAgregarUsadoCredito"] = "ventas.usados.guardar",
                    ["btnCancelarPagoDiffUsados"] = "ventas.pagos.cancelar"
                })
        });

    /*
      LA FAMILIA `cobranzas.` — la que exige CobranzasController y que el front
      todavia no llama.

      SE DECLARA IGUAL, y por dos razones. La primera: `cobranzas.ver_modulo` es
      la llave de la tarjeta COBRANZA del inicio (menuLegacy.ts:164), asi que
      sin derivarla hay que capturarsela a mano a quien ya tiene la cobranza en
      Mac31 — la doble captura que este puente viene a quitar. La segunda: el
      controlador existe y exige esas claves; el dia que alguien le llame, los
      permisos ya estan.

      CERO MAPEO A MANO. Con Prefijo "cobranzas", la tabla de verbos deriva
      sobre ConsultarCobranzas:

        btnVer       -> cobranzas.ver        (ya existia en el catalogo)
        btnNuevo     -> cobranzas.crear      (ya existia)
        btnModificar -> cobranzas.editar     (ya existia)
        btnCancelar  -> cobranzas.cancelar   (ya existia)
        btnBuscar    -> cobranzas.consultar  (nueva)
        btnImprimir  -> cobranzas.imprimir   (nueva)

      Cuatro de las seis claves del catalogo salen solas, con su nombre exacto.

      UN DESAJUSTE QUE SE MIDIO Y SE DEJA DICHO: CobranzasController exige
      `cobranzas.ver` tanto para la LISTA como para el DETALLE, y la derivacion
      hace que esa clave venga de btnVer (VER = el detalle), mientras btnBuscar
      (la lista) produce `cobranzas.consultar`, que el controlador no pide. Quien
      en Mac31 tenga BUSCAR y no VER no heredaria la lista. Hoy no tiene efecto
      practico —el front no llama a ese controlador— y no se fuerza el mapeo
      porque elegir btnBuscar como origen de `cobranzas.ver` dejaria
      `cobranzas.consultar` sin dueño y se lo quitaria a quien lo tenga por VER.

      LAS DOS SEMILLAS: `cobranzas.ver_modulo` (la derivada, declarada ademas
      como semilla para que el borrado por prefijo no se la quite a quien la
      tenga capturada a mano SIN tener la cobranza en Mac31) y
      `cobranzas.pendientes.ver` (la pantalla de cobranza pendiente y vencida,
      que no es una forma de Mac31). MEDIDO: DEMO tiene las dos en las dos bases.

      OJO CON EL PREFIJO: "cobranzas." NO choca con "cobranza." porque el punto
      forma parte del prefijo ("cobranzas.ver" no empieza por "cobranza."). Por
      eso las dos familias pueden registrarse en cualquier orden sin que una se
      coma las claves de la otra.
    */
    private static readonly FamiliaLegacy CobranzasWebFamilia = new(
        "cobranzas.", "cobranzas", "Cobranzas",
        "cobranzas.ver_modulo", "cobranzas.ver",
        new List<PermissionSeed>
        {
            new("cobranzas.ver_modulo", "Cobranzas - Ver modulo", "cobranzas"),
            new("cobranzas.pendientes.ver", "Cobranzas - Pendiente y vencida", "cobranzas")
        },
        new List<LegacyModuleBinding>
        {
            new("cobranzas", "Cobranzas", "CONSULTA DE COBRANZAS",
                new Dictionary<string, (string Key, string Name)>(StringComparer.OrdinalIgnoreCase),
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarCobranzas",
                Prefijo: "cobranzas")
        });
}
