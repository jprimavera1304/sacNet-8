using System;
using System.Collections.Generic;

namespace ISL_Service.Infrastructure.Security;

/*
  FACTURAS, FACTURACION, REASIGNACION, CONCENTRADOS Y LOS RFCs DEL CLIENTE.

  Las cinco formas de Mac31 que se puentean aqui, medidas en MacZ (Zaragoza) y
  en Produccion_svr (Tauro) el 2026-10-10, con 37 procesos activos en cada
  base:

    ConsultarVentasFacturas  (CONSULTA DE FACTURAS)             19 procesos
    VentasFacturacion        (FACTURACION)                       6 procesos
    ConsultarConcentrados    (CONSULTA DE CONCENTRADOS)          6 procesos
    VentasReasignacion       (CONSULTA DE VENTAS REASIGNACION)   4 procesos
    ClientesRFCs             (CLIENTES RFCs)                     2 procesos

  LA REGLA QUE MANDA EN TODO ESTE ARCHIVO: NO SE INVENTA NINGUNA CLAVE.
  --------------------------------------------------------------------
  Las claves de abajo no se escogieron: se BUSCARON en lo que el web ya
  comprueba, para que la herencia encienda el permiso que el front y el
  backend YA miran. Las que se encontraron, una por una:

    facturacion.ver          VentasFacturacionController:36 y el front en
                             nucleo/ventasFacturas.ts:539 (ver detalle),
                             :708 (imprimir), :718 (descargar XML) y
                             FacturasPage.tsx:208 (abre la pantalla).
    facturacion.timbrar      VentasFacturacionController:37,
                             VentasNotaCreditoCapturaController:86 y las dos
                             pantallas vivas, NotaDeCreditoPage y
                             ComplementoDePagoPage (nucleo/complementoDePago.ts:67).
    facturacion.cancelar     VentasFacturacionController:38, front :676.
    facturacion.enviar       VentasFacturacionController:39, front :728.
    cobros.reasignar         VentasPagosOperacionesController:46 y
                             ReasignacionPage.tsx:133; los TRES modos de
                             nucleo/ventasReasignacion.ts (:70, :81, :92)
                             piden esta misma clave.
    cobranza.concentrados.editar     VentasCobranzaOperacionesController:31,
                             nucleo/cobranza.ts:79.
    cobranza.concentrados.cancelar   VentasCobranzaOperacionesController:32,
                             nucleo/cobranza.ts:80.
    clientes.facturacion.editar      ClientesAdminController:38 (los tres
                             endpoints de /facturacion), sembrada en
                             PermissionService.cs:1007.
    clientes.facturacion.ver         ClientesAnexosController:123, sembrada
                             en PermissionService.cs:821.

  NUNCA SE ESCRIBE UN IDProceso. Medido en las dos bases: estas formas NO
  tienen los mismos numeros. En ConsultarVentasFacturas TODOS los 19 estan
  corridos (btnBuscar es 5322 en MacZ y 5323 en Tauro, btnFacturaComision 6445
  contra 6455) y ademas btnFacturaMayorista cambia de sitio dentro de la forma
  (5347 en MacZ, 5342 en Tauro). VentasFacturacion esta corrida un numero
  (btnFacturar 5314 / 5315) y ClientesRFCs tambien (btnNuevoRFC 5348 / 5349).
  Las unicas dos que coinciden son ConsultarConcentrados (5072..5077) y
  VentasReasignacion (5090..5093). El catalogo se arma leyendo n_Procesos por
  la conexion de cada empresa, asi que nada de esto se guarda a mano.

  IMPACTO MEDIDO EL 2026-10-10 EN LAS DOS BASES (ver el detalle al final).
  NADIE PIERDE NINGUNA CLAVE. El unico usuario web con alguna de estas claves
  guardada es DEMO, en las dos empresas, y DEMO tiene en Mac31 todos los
  botones de los que se derivan. WRolPermiso no concede ninguna de estas
  claves en ninguna de las dos bases (0 renglones).
*/
public sealed partial class PermissionService
{
    /*
      CONSULTA DE FACTURAS: 3 CLAVES DE 19 BOTONES, Y LAS 16 QUE NO SE MAPEAN.

      El web tiene CUATRO claves para toda la facturacion y Mac31 tiene
      diecinueve botones solo en esta forma. No se puede mapear uno a uno, y
      forzarlo seria inventar claves que nadie comprueba (el mismo motivo por
      el que compras solo mapea dos de seis botones). Asi que se mapea lo que
      el web SI comprueba y lo demas se deja fuera, a proposito:

      * btnBuscar -> facturacion.ver. A MANO, y no derivada: la tabla de verbos
        daria "facturacion.consultar", que no existe en el web. "facturacion.ver"
        es la clave que abre la pantalla (FacturasPage.tsx:208) y la que piden
        imprimir y descargar XML.

      * btnCancelarFactura -> facturacion.cancelar.
      * btnEnviarCorreoFactura -> facturacion.enviar.

      * btnImprimirFactura y btnDescargarFactura NO se mapean. En el web esas
        dos acciones piden "facturacion.ver" (nucleo/ventasFacturas.ts:708 y
        :718), que ya es de btnBuscar. Mapearlas tambien dejaria tres botones
        apuntando a una clave, y el catalogo guarda UN IDProceso por clave: cual
        gana dependeria del Orden de la fila. Es la misma decision que ya
        documenta garantias con btnNuevo/btnModificar.

      * btnVerNota (IMPRIMIR REMISION) no se mapea: en el web esa accion pide
        "ventas.pantalla" (nucleo/ventasFacturas.ts:739), que es de la familia
        de ventas y la gobierna otro puente.

      * Los cinco que TIMBRAN —btnNuevaFactura, btnFacturaMayorista,
        btnFacturaComision, btnNuevaNotaCredito, btnNuevoComplementoPago— no se
        mapean AQUI. En el web los cinco piden "facturacion.timbrar", asi que
        otra vez serian cinco botones para una clave. Esa clave se hereda de un
        boton solo, btnFacturar de la OTRA forma, y el porque esta abajo.

      * Los nueve de las vistas de nota de credito y complemento de pago
        (btnImprimir/Descargar/EnviarCorreo/Cancelar NotaCredito y lo mismo de
        ComplementoPago) tampoco: el web no tiene claves propias por vista, las
        tres pestañas reusan facturacion.{ver,enviar,cancelar}
        (nucleo/ventasFacturas.ts, campo "vistas" de cada accion), que ya son de
        los botones de la vista de facturas.

      Cuando el web separe esas claves, se agregan aqui y el permiso ya viene de
      Mac31 sin tocar nada mas.
    */
    private static readonly Dictionary<string, (string Key, string Name)> FacturasConsultaMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnBuscar"] = ("facturacion.ver", "Facturacion - Consultar facturas"),
        ["btnCancelarFactura"] = ("facturacion.cancelar", "Facturacion - Cancelar un CFDI"),
        ["btnEnviarCorreoFactura"] = ("facturacion.enviar", "Facturacion - Enviar la factura por correo")
    };

    /*
      FACTURACION (la forma que TIMBRA): UN SOLO BOTON, Y ES EL QUE MANDA.

      "facturacion.timbrar" es la clave mas ancha del modulo —la piden las dos
      pantallas vivas del web (nota de credito y complemento de pago) y el
      endpoint POST api/ventas/facturas— y en Mac31 hay SEIS botones candidatos
      repartidos en dos formas. Se eligio btnFacturar de VentasFacturacion por
      dos razones medidas:

      1. Es el unico cuyo significado es EXACTAMENTE "timbrar": la forma
         FACTURACION es la que emite el CFDI de una remision. Los cinco de la
         consulta ("NUEVA FACTURA", "NUEVA FACTURA MAYORISTA", ...) abren la
         CAPTURA de una venta nueva, que en el web es otra pantalla y hoy no
         existe (nucleo/ventasFacturas.ts lo deja escrito en
         "pendientePorPantalla": esos botones no se dibujan).

      2. Es UNO. Cualquiera de los otros cinco habria metido la ambiguedad del
         IDProceso unico por clave.

      Y no deja a nadie fuera, porque el controlador acepta DOS claves para
      timbrar: "facturacion.timbrar" o "ventas.facturar"
      (VentasFacturacionController:37). "ventas.facturar" es btnFacturar de
      CONSULTA DE VENTAS y ya lo hereda la familia de ventas desde antes de este
      cambio. O sea que quien factura hoy sigue facturando por los dos caminos.

      LOS OTROS CINCO BOTONES DE ESTA FORMA NO SE MAPEAN. btnVerNota,
      btnImprimirFactura, btnDescargarFactura, btnEnviarCorreo y
      btnCancelarFactura son los MISMOS actos que los de la consulta, vistos
      desde la otra pantalla, y en el web no hay claves separadas por pantalla.
      Mapearlos daria dos dueños de facturacion.{ver,enviar,cancelar}.

      POR ESO ESTA FORMA LLEVA SU PROPIO Prefijo. No deriva nada hoy (ninguno de
      sus seis nombres de control esta en VerbosLegacy), pero comparte nombres
      con la consulta —btnImprimirFactura, btnDescargarFactura,
      btnCancelarFactura— y si mañana alguien los agrega a la tabla de verbos,
      sin prefijo propio las dos formas pisarian la misma clave.
    */
    private static readonly Dictionary<string, (string Key, string Name)> FacturacionRemisionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnFacturar"] = ("facturacion.timbrar", "Facturacion - Timbrar")
    };

    /// <summary>
    /// Facturas y facturacion electronica. Las dos formas de Mac31 cuelgan de
    /// la misma familia porque el web tiene UN solo juego de claves
    /// "facturacion.*" para las dos pantallas.
    /// </summary>
    /*
      LA ENTRADA ES "facturas.ver_modulo" Y NO "facturacion.ver_modulo", Y ESO
      SE AVERIGUO ANTES DE DECIDIRLO.

      Application/Security/EntradasDeModulo.cs dice que el catalogo trae las dos
      "duplicadas para la misma pantalla". Leido entero y comprobado contra el
      front: NO son la misma pantalla. Son dos modulos distintos del web, los
      dos en la lista MODULOS de paginas/inicio/InicioPage.tsx:83-84:

        "facturas"     -> paginas/facturas/FacturasPage.tsx, la consulta de
                          facturas. Es la pantalla VIVA, la que esta en el menu
                          (nucleo/menuLegacy.ts:220, { texto: "FACTURAS",
                          modulo: "facturas" }).
        "facturacion"  -> paginas/facturacion/, nota de credito y complemento de
                          pago. Su renglon del menu SE QUITO a proposito
                          (nucleo/menuLegacy.ts:221-231): esas dos pantallas se
                          abren DESDE Facturas, porque hacen falta la factura
                          delante.

      O sea que la llave que de verdad enciende el mosaico por donde se entra es
      "facturas.ver_modulo", y es la que se deriva. "facturacion.ver_modulo" no
      se toca: queda como semilla del web (ver abajo) porque cae bajo este
      prefijo y el borrado se la llevaria.

      Las dos siguen valiendo en el backend: EntradasDeModulo acepta cualquiera
      de las dos para las claves "facturacion.*", asi que derivar
      "facturas.ver_modulo" abre la puerta igual. NO SE UNIFICA NADA aqui: cual
      de las dos sobra, si sobra alguna, es una decision de datos.
    */
    private static readonly FamiliaLegacy FacturasFamilia = new(
        "facturacion.", "facturacion", "Facturacion",
        "facturas.ver_modulo", "facturacion.ver",
        new List<PermissionSeed>
        {
            /*
              SE SIEMBRA PORQUE CAE BAJO EL PREFIJO Y NO SE HEREDA DE NADA.
              Mac31 no tiene un boton de "entrar al modulo": alla la consulta de
              facturas cuelga del menu de VENTAS. Sin este renglon,
              LaMandaLegacy se la quitaria a quien la tenga guardada —medido:
              DEMO la tiene en las dos empresas— y lo dejaria fuera del modulo
              sin error y sin rastro.
            */
            new("facturacion.ver_modulo", "Facturacion - Ver el modulo", "facturacion")
        },
        new List<LegacyModuleBinding>
        {
            /*
              VA PRIMERA porque es la pantalla viva del web y porque las semillas
              de la familia se cuelgan de la primera forma.

              ModuleKey "facturas" (y no "facturacion") A PROPOSITO: el puente
              hace un upsert en dbo.WModulo con esta clave, y
              /api/modulos/disponibles solo publica los modulos que tengan
              renglon ahi. Medido el 2026-10-10: en MacZ existen los renglones
              'facturas' y 'facturacion'; en Produccion_svr (Tauro) NO EXISTE
              NINGUNO DE LOS DOS, asi que alla la tarjeta de Facturas no se
              puede enseñar aunque el permiso se herede. Con esta clave, el
              primer recalculo del snapshot da de alta el renglon que le falta.
            */
            new("facturas", "Facturas", "CONSULTA DE FACTURAS", FacturasConsultaMap,
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarVentasFacturas",
                Prefijo: "facturacion",
                /*
                  Las notas de credito y los complementos de pago SI existen en
                  el web (NotaDeCreditoPage y ComplementoDePagoPage) y los
                  protegen las mismas claves que la factura: timbrar, enviar,
                  cancelar y ver el documento. En Mac31 son botones aparte, y
                  quien tenia solo esos no heredaba nada. Medido en Zaragoza:
                  REBECA (btnNuevaNotaCredito, btnNuevoComplementoPago y los dos
                  de enviar por correo, sin btnFacturar) y NORMA, PENELOPE y
                  RICARDO (btnVerNota sin btnPantalla de consulta de ventas).
                */
                ConcesionesExtra: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["btnNuevaNotaCredito"] = "facturacion.timbrar",
                    ["btnNuevoComplementoPago"] = "facturacion.timbrar",
                    ["btnEnviarCorreoNotaCredito"] = "facturacion.enviar",
                    ["btnEnviarCorreoComplementoPago"] = "facturacion.enviar",
                    ["btnCancelarNotaCredito"] = "facturacion.cancelar",
                    ["btnCancelarComplementoPago"] = "facturacion.cancelar",
                    ["btnImprimirFactura"] = "facturacion.ver",
                    ["btnDescargarFactura"] = "facturacion.ver",
                    ["btnImprimirNotaCredito"] = "facturacion.ver",
                    ["btnDescargarNotaCredito"] = "facturacion.ver",
                    ["btnImprimirComplementoPago"] = "facturacion.ver",
                    ["btnDescargarComplementoPago"] = "facturacion.ver",
                    /* "Ver nota" es imprimir la remision, y en el web eso lo
                       pide POST api/ventas/consulta/reporte, con
                       "ventas.pantalla" (VentasConsultaController). */
                    ["btnVerNota"] = "ventas.pantalla"
                }),

            new("facturacion", "Facturacion", "FACTURACION", FacturacionRemisionMap,
                Array.Empty<PermissionSeed>(),
                FormaExacta: "VentasFacturacion",
                Prefijo: "facturacion.remision",
                /* La misma pantalla de facturar una remision: imprimir,
                   descargar, enviar y cancelar ya estan en el web con sus
                   claves. Medido: NORMA y RICARDO tienen btnVerNota aqui y no
                   btnPantalla de consulta de ventas. */
                ConcesionesExtra: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["btnImprimirFactura"] = "facturacion.ver",
                    ["btnDescargarFactura"] = "facturacion.ver",
                    ["btnEnviarCorreo"] = "facturacion.enviar",
                    ["btnCancelarFactura"] = "facturacion.cancelar",
                    ["btnVerNota"] = "ventas.pantalla"
                })
        });

    /*
      CONCENTRADOS: LA UNICA FAMILIA DE ESTE ARCHIVO QUE SE HEREDA SOLA.

      No lleva Map. Los seis botones de ConsultarConcentrados se llaman
      btnBuscar, btnNuevo, btnVer, btnModificar, btnCancelar y btnImprimir: los
      seis estan en VerbosLegacy, asi que la tabla compartida los deriva sin una
      sola linea de mapa.

      Y la derivacion SE COMPRUEBA CONTRA EL WEB, que es la prueba de que no
      inventa nombres: con el prefijo "cobranza.concentrados", btnModificar da
      "cobranza.concentrados.editar" y btnCancelar da
      "cobranza.concentrados.cancelar", que son EXACTAMENTE las dos claves que
      ya comprueban VentasCobranzaOperacionesController:31-32 y
      nucleo/cobranza.ts:79-80.

      LAS CUATRO CLAVES NUEVAS QUE ESTO CREA, dichas sin adornos:
      "cobranza.concentrados.{consultar,crear,ver,imprimir}" no las comprueba
      nadie hoy. No se pudieron evitar: el catalogo deriva todo nombre de
      control que este en VerbosLegacy y no hay forma de decirle "este botón
      ignoralo". Quedan como permisos que la pantalla de Permisos enseña y que
      el dia que el web separe esas acciones ya vienen de Mac31.

      EL PREFIJO ES "cobranza.concentrados." Y NO "cobranza."
      Es lo mas importante de esta familia. "cobranza.ver",
      "cobranza.cobranzas.*" y "cobranza.cobros.*" son de OTRAS pantallas de la
      cobranza y Mac31 no las reparte por esta forma: con el prefijo corto,
      LaMandaLegacy las borraria del snapshot de todo el mundo.

      LO QUE ESTA FAMILIA NO PUEDE DAR: "cobranza.ver".
      La pantalla del web se abre con "cobranza.ver"
      (ConcentradosPage.tsx:225, nucleo/cobranza.ts:76), no con una clave de
      concentrados. btnBuscar de esta forma seria el candidato natural, pero
      "cobranza.ver" abre TODA la cobranza y heredarla desde el buscar de
      concentrados ensancharia el permiso. Asi que btnBuscar deriva a
      "cobranza.concentrados.consultar" y quien herede sigue necesitando
      "cobranza.ver" capturada en el web.
    */
    private static readonly FamiliaLegacy ConcentradosFamilia = new(
        "cobranza.concentrados.", "cobranza", "Cobranza",
        // "concentrados.ver_modulo" ya lo siembra el catalogo del web
        // (PermissionService.cs:865), con su nota de por que es un modulo
        // aparte y no una vista de cobros; aqui solo se dice de que permiso de
        // Mac31 se deriva. Ademas no cae bajo este prefijo, asi que el borrado
        // no la toca.
        "concentrados.ver_modulo", "cobranza.concentrados.consultar",
        Array.Empty<PermissionSeed>(),
        new List<LegacyModuleBinding>
        {
            new("cobranza", "Cobranza", "CONSULTA DE CONCENTRADOS",
                new Dictionary<string, (string Key, string Name)>(StringComparer.OrdinalIgnoreCase),
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarConcentrados",
                Prefijo: "cobranza.concentrados")
        });

    /*
      REASIGNACION: TRES BOTONES DE MAC31 CONTRA UNA CLAVE DEL WEB.

      Mac31 reparte la reasignacion en tres permisos —btnReasignarNotasCliente
      (notas Y cliente), btnReasignarNotas (solo notas) y btnReasignarCliente
      (solo cliente)— y el web tiene UNA sola clave, "cobros.reasignar", para
      los tres modos: los tres renglones de MODOS_DE_REASIGNACION
      (nucleo/ventasReasignacion.ts:70, :81, :92) la piden, y el endpoint
      /api/ventas/reasignar tambien (VentasPagosOperacionesController:46).

      SE HEREDA DEL MAS ANCHO, btnReasignarNotasCliente, y es la eleccion
      conservadora: heredarla de "solo notas" le daria a ese usuario los tres
      modos, o sea mas de lo que Mac31 le concede. Al contrario, quien en Mac31
      solo tenga uno de los dos modos estrechos no la hereda. Medido el
      2026-10-10 no afecta a nadie: en MacZ los tres usuarios con algun modo
      tienen los tres, y en Tauro el unico que tiene alguno tiene los tres.
      Si algun dia el web separa los tres modos, aqui se agregan los otros dos.

      btnBuscar lo deriva VerbosLegacy a "cobros.reasignar.consultar" (clave
      nueva, hoy no la comprueba nadie: la pantalla se abre con
      "cobros.reasignar", ReasignacionPage.tsx:133).

      EL PREFIJO ES "cobros.reasignar", SIN PUNTO FINAL, Y ES DELIBERADO.
      Tiene que cubrir la clave "cobros.reasignar" EXACTA —que es la que se
      hereda— y su derivada ".consultar", y NADA MAS. Con "cobros." el borrado
      se llevaria cobros.pagos.*, cobros.transferencias.* y el resto del modulo
      de cobros, que Mac31 no reparte por esta forma. Comprobado que ninguna
      otra clave del catalogo empieza por "cobros.reasignar".
    */
    private static readonly Dictionary<string, (string Key, string Name)> ReasignacionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnReasignarNotasCliente"] = ("cobros.reasignar", "Cobros - Reasignar remisiones de cliente o agente")
    };

    private static readonly FamiliaLegacy ReasignacionFamilia = new(
        "cobros.reasignar", "cobros", "Cobros",
        // "reasignacion.ver_modulo" ya lo siembra el web
        // (PermissionService.cs:877) y no cae bajo este prefijo.
        //
        // YA ABRE LAS DOS PUERTAS (2026-10-10). Antes, derivar esta entrada
        // encendia el MOSAICO del inicio pero NO la del backend: EntradasDeModulo
        // mandaba "cobros." -> { cobros.ver_modulo, dinero_usados.ver_modulo } y
        // "cobros.reasignar" se seguia exigiendo detras de "cobros.ver_modulo",
        // asi que la tarjeta se veia y daba 403. Ya tiene su renglon propio
        // ("cobros.reasignar" -> reasignacion.ver_modulo) y, por prefijo mas
        // largo, gana sobre el generico.
        "reasignacion.ver_modulo", "cobros.reasignar",
        Array.Empty<PermissionSeed>(),
        new List<LegacyModuleBinding>
        {
            new("cobros", "Cobros", "CONSULTA DE VENTAS REASIGNACION", ReasignacionMap,
                Array.Empty<PermissionSeed>(),
                FormaExacta: "VentasReasignacion",
                Prefijo: "cobros.reasignar")
        });

    /*
      LOS RFCs DEL CLIENTE: DOS BOTONES, UNA CLAVE, Y SE MIDIO QUE NO IMPORTA.

      ClientesRFCs tiene exactamente dos procesos, btnNuevoRFC y
      btnModificarRFC, y ninguno esta en VerbosLegacy (no son btnNuevo ni
      btnModificar), asi que los dos necesitan mapa propio.

      El web tiene UNA clave de escritura, "clientes.facturacion.editar", y la
      piden los TRES endpoints de datos de facturacion —POST /facturacion, PUT
      /facturacion/{id} y PUT /{id}/facturacion-mostrador,
      ClientesAdminController:38— o sea que cubre el alta y la modificacion
      juntas. Dos botones para una clave serian la ambiguedad del IDProceso
      unico otra vez, asi que se hereda de UNO: btnModificarRFC, porque es el
      que coincide con el verbo de la clave.

      Y SE COMPROBO QUE LA ELECCION NO LE QUITA NADA A NADIE. Medido el
      2026-10-10 los dos botones tienen EL MISMO conjunto de usuarios en las dos
      bases: DEMO, FERNANDA y NORMA en MacZ; DEMO, JESY y NAOLANY en Tauro.
      Nadie tiene uno sin el otro, asi que heredar de btnModificarRFC da el
      mismo resultado que heredar de btnNuevoRFC.

      "clientes.facturacion.ver" SE QUEDA DEL WEB: esta forma de Mac31 no tiene
      boton de buscar dado de alta en n_Procesos (son dos procesos y ya), asi
      que alla no hay nada que leer y el borrado por prefijo se la llevaria.

      NO DECLARA ClaveDeEntrada, Y ES LO CORRECTO. La entrada de estas claves es
      "clientes.ver_modulo" (EntradasDeModulo), que es la puerta de TODO el
      modulo de clientes: la pantalla de RFCs vive dentro de Clientes. Derivarla
      desde btnModificarRFC le abriria el modulo completo a quien solo puede
      tocar RFCs. Consecuencia, dicha claro: quien herede
      "clientes.facturacion.editar" sigue necesitando "clientes.ver_modulo"
      capturado en el web.
    */
    private static readonly Dictionary<string, (string Key, string Name)> ClientesRfcsMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnModificarRFC"] = ("clientes.facturacion.editar", "Clientes - Editar datos de facturacion")
    };

    private static readonly FamiliaLegacy ClientesRfcsFamilia = new(
        "clientes.facturacion.", "clientes", "Clientes", null, null,
        new List<PermissionSeed>
        {
            new("clientes.facturacion.ver", "Clientes - Ver datos de facturacion", "clientes")
        },
        new List<LegacyModuleBinding>
        {
            new("clientes", "Clientes", "CLIENTES RFCs", ClientesRfcsMap,
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ClientesRFCs",
                Prefijo: "clientes.facturacion",
                /* btnNuevoRFC y btnModificarRFC, sin boton de lectura: igual
                   que compras y constantes. Comprobado en las dos empresas. */
                ClaveDeLectura: "clientes.facturacion.ver")
        });

    /*
      EL IMPACTO, MEDIDO EL 2026-10-10 EN LAS DOS BASES LOCALES.

      Usuarios web que GANAN la clave por herencia (tienen el boton encendido en
      [Usuario Forma Procesos] y una fila en UsuarioWeb que empata por nombre
      con dbo.Usuarios). 24 usuarios web con cuenta de Mac31 en MacZ, 6 en
      Produccion_svr:

                                          MacZ   Tauro
        facturacion.ver                      6       3
        facturacion.timbrar                  4       2
        facturacion.enviar                   4       3
        facturacion.cancelar                 3       2
        cobranza.concentrados.consultar      3       5
        cobranza.concentrados.crear          3       5
        cobranza.concentrados.ver            3       5
        cobranza.concentrados.editar         3       5
        cobranza.concentrados.cancelar       3       3
        cobranza.concentrados.imprimir       3       5
        cobros.reasignar                     3       1
        cobros.reasignar.consultar           3       1
        clientes.facturacion.editar          3       3

      Usuarios que PIERDEN algo: NINGUNO, en ninguna de las dos bases. El unico
      usuario web con alguna de estas claves guardada en WUsuarioPermiso es
      DEMO —las once claves, en las dos empresas— y DEMO tiene en Mac31 todos
      los botones de los que se derivan. WRolPermiso no concede ninguna de estas
      claves en ninguna de las dos bases (0 renglones), asi que por rol tampoco
      se pierde nada.

      LO QUE NO SE PUDO COMPROBAR AQUI: esto se midio con SQL replicando la
      consulta de LoadLegacyModulePermissionsForUserAsync y el empate de
      ResolveLegacyUserIdAsync. En las dos bases locales dbo.UsuarioWeb NO tiene
      la columna LegacyUserId (comprobado), asi que el empate es por nombre de
      usuario, que es la segunda rama del codigo. Donde esa columna exista, los
      numeros pueden cambiar. Y no se levanto el servicio para ver /api/me: lo
      de arriba es la cuenta de la base, no una lectura del snapshot.
    */
}
