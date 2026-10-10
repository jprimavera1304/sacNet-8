namespace ISL_Service.Application.Reportes;

/// <summary>
/// Un reporte del modulo Reportes y el permiso que lo enciende.
/// </summary>
/// <param name="Clave">La clave que mandan los fronts en <c>reporteKey</c>.</param>
/// <param name="Grupo">ventas | compras | inventario | administracion.</param>
/// <param name="Etiqueta">Como se lee en el menu.</param>
/// <param name="Permiso">
/// Clave en <c>WPermiso</c>. <c>null</c> significa "no hay permiso para este
/// reporte todavia": NO se bloquea. Ver la nota de SIN_PERMISO en
/// <see cref="ReportesCatalogo"/>.
/// </param>
/// <param name="EnArbol">
/// Si este reporte es un NODO del arbol de Reportes de Mac31 (true, lo normal)
/// o un documento que sale de un BOTON de otra pantalla (false).
///
/// POR QUE HACE FALTA DISTINGUIRLOS. El arbol del web y del movil se pinta con
/// lo que devuelve GET /reportes/ventas/permisos, o sea con este catalogo. Los
/// documentos de boton no tienen filtros que pedir -piden el ID del documento
/// que esta seleccionado en SU pantalla-, asi que al colgarlos del arbol el
/// front dibuja un nodo que al pulsarlo no sabe que campos ensenar. Eso le
/// pasaba a `cambios_vigencia`: aparecia en el arbol y contestaba honestamente
/// que no sabia que pedirle.
///
/// Con EnArbol = false el reporte SIGUE en el catalogo y
/// POST legacy/generar lo sigue aceptando -es como se imprime desde su
/// pantalla-, pero el arbol no lo ofrece. Un cliente viejo que no lea el campo
/// se comporta igual que hoy, asi que el movil no se rompe: deja de ensenar el
/// nodo cuando lo lea.
/// </param>
public sealed record ReporteCatalogoItem(
    string Clave,
    string Grupo,
    string Etiqueta,
    string? Permiso,
    bool EnArbol = true);

/// <summary>
/// EL CATALOGO DE REPORTES Y SUS PERMISOS, EN UN SOLO LUGAR.
///
/// POR QUE VIVE AQUI Y NO EN CADA FRONT
/// ------------------------------------
/// El catalogo de reportes ya estaba escrito a mano dos veces: en el web
/// (docs/modules/verticals/reportes/.../acumuladores-productos.config.js) y en
/// la app (shared_mobile/.../reportes_catalogo.dart). Si cada uno guardara
/// ADEMAS su propio mapeo reporte->permiso, el dia que alguien renombre un
/// permiso habria tres verdades y dos de ellas equivocadas. Aqui esta la unica:
/// el backend la aplica al generar (403) y la publica para que los menus
/// pinten lo mismo que la API va a aceptar.
///
/// COMO SE ARMA LA CLAVE DEL PERMISO
/// ---------------------------------
/// Por regla, <c>reportes.{grupo}.{clave}.ver</c>. Es la misma regla con la que
/// se generaron los permisos en WPermiso a partir del catalogo legacy
/// (Querrys/04 Permisos Reportes): legacy arma el nombre del proceso como
/// ("{Categoria} " + texto del nodo) y el script lo vuelve slug. Comprobado
/// contra Legacy/Mac31/Mac31/Forms/Reportes/ReportesMenu.cs, que es quien hoy
/// decide bien.
///
/// LAS EXCEPCIONES (y por que no se adivinaron)
/// --------------------------------------------
/// Hay filas en WPermiso mas viejas que el texto actual del nodo en legacy, asi
/// que la regla no da la clave que existe. Cada una se resolvio con la
/// evidencia de ReportesMenu.cs (nombre de la variable del nodo y del valor de
/// enumReportes, que son los que no se renombraron), no por parecido de nombre:
///
/// - transferencias_duplicadas -> reportes.ventas.pagos_transferencias_duplicadas.ver
///   nodeEmpresasVentasPagosTransferenciasDuplicadas / enumReportes.VentasPagosTransferenciasDuplicadas
/// - transferencias_folios     -> reportes.ventas.transferencias_folio.ver
///   mismo reporte, el permiso quedo en singular (enumReportes.TransferenciasFolios)
/// - clientes_con_descuentos   -> reportes.ventas.clientes_descuentos.ver
///   nodo "Clientes CON Descuentos" con enumReportes.ClientesDescuentos
/// - compras_*, inventario_*   -> el grupo ya venia dentro de la clave del front
///   ("Compras " + "Facturas" -> reportes.compras.facturas.ver)
///
/// SIN_PERMISO: LO QUE NO SE BLOQUEA
/// ---------------------------------
/// Cinco reportes no tienen permiso en WPermiso, y no se les invento uno.
/// Bloquear por una duda deja a alguien sin su reporte sin poder explicarle por
/// que; con <c>null</c> siguen abiertos a quien tenga reportes.ver_modulo, que
/// es exactamente como estaban antes de este cambio. Quien decida crearles
/// permiso solo tiene que cambiar el null por la clave.
/// </summary>
public static class ReportesCatalogo
{
    /// <summary>El maestro: sin esto no se entra al modulo.</summary>
    public const string PermisoModulo = "reportes.ver_modulo";

    /// <summary>No hay permiso para este reporte. No se bloquea.</summary>
    private const string? SinPermiso = null;

    private static readonly ReporteCatalogoItem[] _reportes =
    [
        // ----- VENTAS -----
        Ventas("acumuladores_y_productos", "Acumuladores y Productos"),
        Ventas("motobaterias", "Motobaterias"),
        Ventas("folios", "Folios"),
        Ventas("remisiones", "Remisiones"),
        Ventas("facturas", "Facturas"),
        /*
          EL 1003 NO ESTA AQUI, Y NO SE DEBE AGREGAR. MEDIDO.

          El 1003 ("Factura", sp_n_rptFacturaElectronica) es el PDF de un
          comprobante timbrado. Parece que le toca entrar al catalogo —una nota
          de credito timbrada desde el web no tenia papel— y se probo. NO
          FUNCIONA, y la razon no es un permiso ni un parametro:

          `sp_n_rptFacturaElectronica` NO DEVUELVE LOS DATOS DEL CFDI. Devuelve
          la RUTA del fichero que ya existe en disco, y los datos del correo con
          el que se manda. Comprobado en MacZ con el comprobante 71875, con las
          dos presentaciones que acepta:

              Param3 = 2 (PDF) -> IssueDate | FolioFtm | Presentacion |
                                  Archivo = C:\macZ\Facturas\07-2026\
                                            ComplementoPago 0 71875 .pdf | ...
              Param3 = 0        -> lo mismo, con .zip en Archivo

          Ni una columna de emisor, receptor, conceptos o sellos. Su plantilla
          `rptTemplate` pide 42 tags (#IssuerRfc#, #Uuid#, #OriginalString#,
          #SatSign#...) y el motor los deja TAL CUAL: el PDF que sale son 44 KB
          de una pagina con los nombres de los tags impresos. Un papel roto en el
          catalogo es peor que un reporte que no esta, porque alguien lo manda a
          un cliente.

          EL CAMINO BUENO YA EXISTE Y ES OTRO: el comprobante no se vuelve a
          dibujar, se sirve el fichero que genero el timbrado.

              GET /api/ventas/facturas/{id}/documento?formato=pdf

          Ese endpoint resuelve la misma ruta y entrega el fichero. Medido con el
          71875 en esta maquina: contesta 409 con la ruta exacta
          ("NO SE ENCONTRO EL ARCHIVO PDF. (C:\macZ\Facturas\07-2026\...)")
          porque los PDF viven en el servidor que timbro, no en el equipo de
          desarrollo. O sea que el endpoint esta bien y lo que falta no es un
          reporte: es que el fichero exista donde el timbrado lo deja.
        */
        Ventas("concentrados", "Concentrados"),
        /*
          EL CONCENTRADO DE UN REPARTIDOR (1005), QUE NO ES EL DE ARRIBA.

          `concentrados` es el 1185: el LISTADO de los concentrados de un rango
          de fechas, el nodo del arbol de Reportes. Este es el DOCUMENTO de UN
          concentrado -la hoja que firma el repartidor- y faltaba por completo:
          no estaba en el catalogo, no tenia id y no tenia armador, asi que
          desde el web no habia forma de imprimirlo.

          FUERA DEL ARBOL, como los demas documentos de boton: pide los
          IDConcentrado marcados en la rejilla, no un rango, y un nodo del arbol
          no sabria que pedirle. Sale del boton IMPRIMIR de
          ConsultarConcentrados de Mac31 y de la pantalla Concentrado.

          SIN PERMISO PROPIO, y es la misma decision que ya se tomo para
          `hoja_de_cobro`, no una nueva. Su clave por la regla seria
          `reportes.ventas.concentrado.ver` y NO existe en WPermiso -comprobado
          en MacZ: las unicas que mencionan concentrados son
          `reportes.ventas.concentrados.ver`, que es la del NODO del arbol (el
          1185), y `cobranza.concentrados.{editar, cancelar}`, que son las de la
          pantalla-. Declararla seria un candado que nadie tiene: el boton no se
          dibujaria para NADIE.

          Y NO SE LE CUELGA la del listado. Se probo y el propio juego de
          pruebas lo rechaza: la regla de la casa es un permiso por reporte, y la
          unica excepcion es el nodo "Movimientos Centros", que dispara tres
          reportes con UN clic. Aqui son dos clics en dos pantallas distintas
          -el nodo del arbol de Reportes y el boton IMPRIMIR de la pantalla de
          concentrados-, asi que compartir el permiso obligaria a tener el
          listado de Reportes para imprimir el papel del repartidor, que es
          justo lo que se decidio NO hacer con la hoja de cobro.

          Lo que lo protege es poder entrar al modulo (reportes.ver_modulo, que
          el controlador exige siempre) y a la pantalla de concentrados. El dia
          que se de de alta la clave, se cambia esta linea y nada mas.
        */
        new("concentrado", "ventas", "Concentrado", SinPermiso, EnArbol: false),
        Ventas("cobranza", "Cobranza"),
        Ventas("cobranza_detallado", "Cobranza Detallado"),
        // Legacy pide el proceso ("Ventas Pagadas" + "Cobranza Pagadas"), una
        // concatenacion sin espacio que no coincide con ninguna fila de
        // WPermiso: hoy en Mac31 este reporte solo lo ve el Administrador.
        // Ver ReportesMenu.cs, nodeEmpresasVentasCobranzaPagos.
        new("cobranza_pagadas", "ventas", "Cobranza Pagadas", SinPermiso),
        Ventas("estado_de_cuenta", "Estado de Cuenta"),
        Ventas("rutas_de_agentes", "Rutas de Agentes"),
        Ventas("pagos", "Pagos"),
        Ventas("descuentos", "Descuentos"),
        new("transferencias_duplicadas", "ventas", "Transferencias Duplicadas", "reportes.ventas.pagos_transferencias_duplicadas.ver"),
        new("transferencias_clientes", "ventas", "Transferencias Clientes", SinPermiso),
        Ventas("transferencias_estatus", "Transferencias Estatus"),
        new("transferencias_folios", "ventas", "Transferencias Folios", "reportes.ventas.transferencias_folio.ver"),
        Ventas("ventas_y_pagos", "Ventas y Pagos"),
        Ventas("utilidad", "Utilidad"),
        Ventas("clientes_globales", "Clientes Globales"),
        new("clientes_con_descuentos", "ventas", "Clientes CON Descuentos", "reportes.ventas.clientes_descuentos.ver"),
        new("clientes_sin_descuentos", "ventas", "Clientes SIN Descuentos", SinPermiso),
        Ventas("clientes_compras", "Clientes Compras"),
        Ventas("clientes_con_compra", "Clientes Con Compra"),
        Ventas("clientes_con_compra_por_dia", "Clientes Con Compra Por Dia"),
        Ventas("clientes_sin_compra", "Clientes Sin Compra"),
        Ventas("clientes_acum_moto_lub", "Clientes Acum Moto Lub"),
        Ventas("clientes_acum_moto_lub_clarios", "Clientes Acum Moto Lub CLARIOS"),
        Ventas("clientes_facturas_rfc", "Clientes Facturas RFC"),
        Ventas("cascos", "Cascos"),
        Ventas("garantias", "Garantias"),
        // El permiso reportes.ventas.garantias_registradas.ver YA existe en
        // WPermiso (lo genero el script del catalogo legacy), aunque el nodo que
        // lo abria en Mac31 este comentado hoy.
        Ventas("garantias_registradas", "Registro de Garantias"),
        Ventas("cascos_excedentes", "Cascos Excedentes"),
        new("liquidaciones", "ventas", "Liquidaciones", SinPermiso),
        new("cobros_dinero_y_cascos", "ventas", "Cobros Dinero y Cascos", SinPermiso),
        Ventas("movimientos_centros", "Movimientos Centros"),
        // 1415 y 1420 no tienen nodo propio en Mac31: los dispara el mismo boton
        // de "Movimientos Centros" (ReportesMenu.cs:4048-4058), asi que llevan su
        // permiso. Aqui son claves aparte para poder pedir y comprobar cada uno.
        new("centros_pagos_entradas_salidas", "ventas", "Centros Pagos Entradas y Salidas", "reportes.ventas.movimientos_centros.ver"),
        new("centros_garantias", "ventas", "Centros Garantias", "reportes.ventas.movimientos_centros.ver"),
        Ventas("centros_de_servicio", "Centros de Servicio"),
        Ventas("hoja_de_cobro_total_cobrado", "Hoja de Cobro Total Cobrado"),
        Ventas("hoja_de_cobro_total_cascos", "Hoja de Cobro Total Cascos"),
        Ventas("hoja_de_cobro_cheques", "Hoja de Cobro Cheques"),
        /*
          LA HOJA DE COBRO DE UNA LIQUIDACION, Y POR QUE VA SIN PERMISO PROPIO.

          Los tres de arriba tienen su clave en WPermiso
          (reportes.ventas.hoja_de_cobro_total_cobrado.ver y sus hermanas);
          `reportes.ventas.hoja_de_cobro.ver` NO existe, comprobado en MacZ. Si
          se declarara segun la regla, el candado seria un permiso que nadie
          tiene y el boton no se dibujaria para NADIE: el reporte quedaria
          escrito y apagado, que es peor que el hueco que vino a tapar.

          Con SinPermiso queda igual que en Mac31, donde IMPRIMIR COBRO no pide
          un permiso suyo: lo que lo protege es poder entrar al modulo
          (reportes.ver_modulo, que el controlador exige siempre) y a la
          pantalla de cobros. El dia que se de de alta la clave, se cambia esta
          linea por `Ventas("hoja_de_cobro", ...)` y nada mas.
        */
        /*
          FUERA DEL ARBOL: se imprime de las hojas MARCADAS en la rejilla de
          Cobros, no de un rango de fechas. Con EnArbol = true el nodo se
          pintaba y, desde la pantalla de Reportes, contestaba siempre:

            "La hoja de cobro necesita al menos una hoja: manda idsConcentrado,
             idsCobranza e idsCobro."

          O sea un boton que no podia funcionar nunca -- el mismo defecto que ya
          se corrigio en `concentrado` y `cambios_vigencia`. El reporte SIGUE en
          el catalogo y se genera igual desde su propia pantalla; lo unico que
          cambia es que deja de ofrecerse donde no se puede pedir.
          Reproducido el 2026-10-10 contra el backend local.
        */
        new("hoja_de_cobro", "ventas", "Hoja de Cobro", SinPermiso, EnArbol: false),
        /*
          CAMBIOS DE VIGENCIA (1535): el documento de UN cambio de vigencia.

          Lo imprime el boton IMPRIMIR de ConsultarCambiosVigencia de Mac31, no un
          nodo del arbol de Reportes -- el mismo caso que `autos`, que sale del
          boton de ConsultarAutos. Por eso su permiso por la regla seria
          reportes.ventas.cambios_vigencia.ver y NO existe en WPermiso: comprobado
          en MacZ el 07-10-2026, ahi solo hay cambiosvigencia.{ver_modulo, ver,
          crear, editar, cancelar}, que son los de la pantalla, y ninguna clave de
          reportes que lo mencione. El script del catalogo legacy no lo genero
          porque el nodo no vive en el arbol.

          Entra ABIERTO a quien tenga reportes.ver_modulo, como los otros siete sin
          permiso, y no se le inventa uno: bloquear por una duda deja a alguien sin
          su documento sin poder explicarle por que. Lo que NO se hace es colgarle
          `cambiosvigencia.ver`: eso exigiria los dos permisos a la vez (este
          endpoint ya pide reportes.ver_modulo por politica) y dejaria sin imprimir
          a quien tiene el modulo de reportes completo.
        */
        // FUERA DEL ARBOL: pide el id de UN documento, no un rango. Ver EnArbol.
        // Se imprime desde la pantalla de Cambios de Vigencia, por el registro
        // seleccionado, que es de donde sale en Mac31 (el boton IMPRIMIR de
        // ConsultarCambiosVigencia). En el arbol era un nodo que al pulsarlo no
        // sabia que campos pedir.
        new("cambios_vigencia", "ventas", "Cambios de Vigencia", SinPermiso, EnArbol: false),
        /*
          LOS COMPROBANTES DE CASCOS (1255 / 1256 / 1260).

          FUERA DEL ARBOL, y por la misma razon que cambios_vigencia: son el
          papel de UN movimiento, no un listado de un rango. `comprobante_de_cascos`
          pide el idUsadoMovimiento (y en Tauro, donde se resuelve al 1260, una
          fecha); `venta_de_cascos` pide el idUsadoMovimiento. Un nodo del arbol
          no sabria que pedirle. Salen del boton IMPRIMIR de la pantalla de
          movimientos de cascos, que es de donde salen en Mac31
          (ConsultarUsadosMovimientos.btnImprimir).

          SinPermiso, y comprobado, no supuesto: en MacZ el 08-10-2026 no existe
          ninguna clave `reportes.ventas.comprobante_de_cascos.ver` ni
          `...venta_de_cascos.ver` en WPermiso -- las unicas de reportes que
          mencionan cascos son reportes.ventas.cascos.ver,
          reportes.ventas.cascos_excedentes.ver y
          reportes.ventas.hoja_de_cobro_total_cascos.ver, que son otros tres
          reportes. Igual que los otros ocho sin permiso, entran abiertos a quien
          tenga reportes.ver_modulo; no se les cuelga `usados.ver` porque eso
          exigiria dos permisos a la vez y dejaria sin su comprobante a quien
          tiene el modulo de reportes completo.
        */
        new("comprobante_de_cascos", "ventas", "Comprobante de Cascos", SinPermiso, EnArbol: false),
        new("venta_de_cascos", "ventas", "Venta de Cascos", SinPermiso, EnArbol: false),
        /*
          LIQUIDACIONES DE DESCUENTO (1030): el papel de una liquidacion.

          Mismo caso que autos y cambios_vigencia: lo imprime el boton IMPRIMIR de
          ConsultarLiquidacionesDescuentos, no un nodo del arbol de Reportes, asi
          que reportes.ventas.liquidaciones_descuentos.ver no existe en WPermiso
          (comprobado en MacZ el 07-10-2026; las claves que si existen son
          descuentos.{ver, editar, eliminar, simular} y
          descuentos.ajustes.{ver, crear, autorizar}, que son las de la pantalla).

          NO se le cuelga descuentos.ajustes.ver por la misma razon que a los
          otros dos: este endpoint ya exige reportes.ver_modulo por politica, y
          sumarle una segunda clave le quitaria el papel a quien tiene el modulo
          de reportes completo.
        */
        /*
          FUERA DEL ARBOL, por lo mismo que la hoja de cobro: se imprime de los
          folios SELECCIONADOS en su pantalla. Desde Reportes contestaba:

            "El reporte de liquidaciones de descuento se imprime de los folios
             seleccionados: manda al menos uno en idLiquidacionesDescuento."

          Reproducido el 2026-10-10 contra el backend local.
        */
        new("liquidaciones_descuentos", "ventas", "Liquidaciones Descuentos", SinPermiso, EnArbol: false),

        // ----- COMPRAS -----
        new("compras_acumuladores_y_productos", "compras", "Acumuladores y Productos", "reportes.compras.acumuladores_y_productos.ver"),
        new("compras_facturas", "compras", "Facturas", "reportes.compras.facturas.ver"),

        // ----- INVENTARIO -----
        new("inventario_actual", "inventario", "Actual", "reportes.inventario.actual.ver"),
        new("inventario_faltante", "inventario", "Faltante", "reportes.inventario.faltante.ver"),
        Grupo("inventario", "ajustes_al_inventario", "Ajustes al Inventario"),
        Grupo("inventario", "movimientos", "Movimientos"),

        // ----- ADMINISTRACION -----
        Grupo("administracion", "gasolinas", "Gasolinas"),
        Grupo("administracion", "listas_de_precios", "Listas de Precios"),
        Grupo("administracion", "listas_de_precios_imp", "Listas de Precios Imp"),
        Grupo("administracion", "cortes", "Cortes"),
        /*
          LOS TRES REPORTES DE CORTES QUE SALEN DE UN BOTON, NO DEL ARBOL.

          El nodo de arriba es el 1320: el LISTADO de cortes por rango de
          fechas, el unico que el arbol de Reportes de Mac31 ofrece y el unico
          con clave en WPermiso (reportes.administracion.cortes.ver, comprobado
          en MacZ el 08-10-2026).

          Estos tres son el PAPEL de un corte concreto y los imprime un boton de
          pantalla:

            corte_diario (1315)              IMPRIMIR de ConsultarCortes
            corte_diario_detalle_mac (1317)  VER DETALLE MAC del corte abierto
            corte_cs (1345)                  IMPRIMIR de ConsultarCortesCS y del
                                             propio CorteDiarioCS

          VAN SIN PERMISO PROPIO, por la misma razon medida que `autos`,
          `cambios_vigencia` y `liquidaciones_descuentos`: su clave por la regla
          seria reportes.administracion.corte_diario.ver y hermanas, y NINGUNA
          existe en WPermiso -el script del catalogo legacy no las genero porque
          el nodo no vive en el arbol-. Declararlas seria poner un candado que
          nadie tiene: el boton no se dibujaria para NADIE y el reporte quedaria
          escrito y apagado.

          Y NO SE LES CUELGA `cortes.ver`, que es la clave de la pantalla:
          este endpoint ya exige reportes.ver_modulo por politica, asi que
          sumarle una segunda clave exigiria las dos a la vez y le quitaria el
          papel a quien tiene el modulo de reportes completo. Es la decision que
          ya se tomo para los otros tres documentos de boton.

          Lo que SI los protege del lado del web: el boton solo se dibuja en la
          pantalla de cortes, y a esa pantalla se entra con cortes.ver_modulo /
          cortes.ver (y cortes.cs.ver para el de centros).
        */
        new("corte_diario", "administracion", "Corte Diario", SinPermiso, EnArbol: false),
        new("corte_diario_detalle_mac", "administracion", "Corte Diario Detalle MAC", SinPermiso, EnArbol: false),
        new("corte_cs", "administracion", "Corte Centro de Servicio", SinPermiso, EnArbol: false),
        Grupo("administracion", "gastos", "Gastos"),
        Grupo("administracion", "impuestos", "Impuestos"),
        Grupo("administracion", "accesos", "Accesos"),
        /*
          AUTOS: el unico documento del modulo de Catalogos.

          Es el reporte que imprime `ConsultarAutos` de Mac31 (enumReportes.Autos =
          1300). Entra en "administracion" porque es la flotilla de la empresa, no
          una venta. Su permiso por la regla seria
          reportes.administracion.autos.ver y NO esta en WPermiso -el script del
          catalogo legacy no lo genero, porque el nodo no vive en el arbol de
          Reportes sino en el boton de una pantalla de catalogo-, asi que entra
          abierto a quien tenga reportes.ver_modulo, como los otros seis sin
          permiso. No se invento uno: bloquear por una duda deja a alguien sin su
          reporte sin poder explicarle por que.
        */
        new("autos", "administracion", "Autos", SinPermiso),
        /*
          LOS CUATRO DE `ReporteEmpleadoAsistenciaRecibos`.

          Entran en "administracion" porque es donde Mac31 cuelga el nodo
          Asistencias (y Zaragoza lo agrega a ese grupo, ReportesMenu.cs:929),
          pero NO salen del arbol de ReportesMenu: salen de otro formulario, con
          sus dos rejillas y sus propios parametros.

          LOS CUATRO ENTRAN SIN PERMISO PROPIO, y es una decision medida, no un
          olvido. El permiso de legacy es `ReporteEmpleadoAsistenciaRecibos.acceso`
          y NO hay ninguna clave `reportes.nomina.*` ni
          `reportes.administracion.*asistencia*` en WPermiso mas que
          `reportes.administracion.asistencias.ver`, que es la del nodo del arbol
          (el 1026) y no la de este formulario.

          Inventarles una clave nueva los dejaria a los cuatro contestando 403 a
          todo el que no fuera SuperAdmin, que es exactamente lo que ya paso una
          vez con 68 capacidades. Contado en MacZ: de las 65 claves `reportes.*`
          que existen en WPermiso, las 65 tienen CERO tenedores -ni un rol en
          WRolPermiso ni un usuario en WUsuarioPermiso-. Asi que entran abiertos a
          quien tenga reportes.ver_modulo, como los otros siete sin permiso, y el
          dia que se siembren las claves solo hay que cambiar el null.
        */
        /*
          LOS CINCO BOTONES DE `ReporteEmpleadoAsistenciaRecibos`, COMPLETOS.

          Son los cinco que tiene Mac31, con su nombre y su tecla:

              ASISTENCIAS F5          1025   nomina_asistencias
              RECIBOS DE NOMINA F6    1020   nomina_recibos
              INASISTENCIAS F7        1190   nomina_inasistencias
              VACACIONES F8           1027   nomina_vacaciones
              SOLO ASISTENCIAS F9     1024   nomina_solo_asistencias

          LOS TRES QUE FALTABAN YA ESTAN, y lo que los tenia fuera era una
          conclusion equivocada, no una dependencia perdida:

          · 1025 y 1024 salian con las siete columnas de dias en crudo
            (#asistenciaDia1#..7, #fechaDia1#..7, #minsRetardo#, #totalPagado#)
            porque esos tags no los devuelve `sp_n_ConsultaEmpleadosTipoSueldoRecibo`.
            Pero SI los devuelve `sp_n_ConsultaEmpleadosPeriodoAsistencia`, que
            existe en MacZ y en Produccion_svr y que ya usa la rejilla del modulo
            de Asistencias; los encabezados salen de
            `sp_n_ConsultaPeriodosTipoSueldoDias`. La "rama dedicada" de legacy
            esta portada en NominaAsistenciasHtmlBuilder.
          · 1020 sigue sin renglon en `dbo.Reportes`, y resulta que no lo
            necesita: MacReportes no arma el recibo, solo pasa a PDF un HTML que
            arma MacServicios con las plantillas 1020 de `Template_Html` —las
            cuatro estan en las dos bases— y estos mismos procedimientos. Ese
            armador esta portado en ReciboNominaHtmlBuilder.

          LOS CINCO ENTRAN SIN PERMISO PROPIO, por lo dicho arriba: el permiso de
          legacy es `ReporteEmpleadoAsistenciaRecibos.acceso` y no hay ninguna
          clave `reportes.nomina.*` en WPermiso.

          NO SALEN DEL ARBOL (EnArbol = false): salen de la pantalla ASISTENCIAS
          Y RECIBOS, que tiene su rejilla de periodos y su rejilla de empleados.
          Colgarlos del arbol dibujaria cinco nodos que al pulsarlos no sabrian
          pedir ni el periodo ni los empleados marcados.
        */
        new("nomina_asistencias", "administracion", "Asistencias", SinPermiso, EnArbol: false),
        /*
          EL RECIBO DE NOMINA LLEVA SUELDOS, ASI QUE LO PROTEGE LA LLAVE DE LOS
          SUELDOS.

          Estaba con `SinPermiso`, y eso aqui no significa "publico sin querer":
          significa que el unico candado era `reportes.ver_modulo`. O sea que
          cualquiera que pudiera entrar a Reportes podia pedir
          `POST /api/reportes/ventas/legacy/generar` con esta clave y recibir un
          papel con los IMPORTES POR EMPLEADO -lo arma ReciboNominaHtmlBuilder
          desde `sp_n_ConsultaEmpleadosTipoSueldoRecibo`-.

          Y la pantalla de Empleados si lo protege: el servidor ANULA los cinco
          importes antes de responder cuando falta `empleados.sueldo.ver`
          (EmpleadosService.BorrarDinero). Tapar la puerta y dejar la ventana
          abierta no es proteger: es creer que se protege.

          Se usa `empleados.sueldo.ver` y no una clave nueva a proposito. Esa ya
          existe en WPermiso -id 188 en MacZ, comprobado- y ya esta repartida a
          quien debe ver sueldos. Inventar
          `reportes.administracion.nomina_recibos.ver` daria un candado que
          nadie tiene: el reporte quedaria escrito y apagado para todos, que es
          peor que el hueco que viene a tapar.
        */
        new("nomina_recibos", "administracion", "Recibos de nómina", "empleados.sueldo.ver", EnArbol: false),
        new("nomina_inasistencias", "administracion", "Inasistencias", SinPermiso, EnArbol: false),
        new("nomina_vacaciones", "administracion", "Vacaciones", SinPermiso, EnArbol: false),
        new("nomina_solo_asistencias", "administracion", "Solo asistencias", SinPermiso, EnArbol: false),
        /*
          Y EL SEXTO, QUE NO ES UN BOTON: el reporte que Zaragoza lanza SOLA
          detras de los recibos (ReporteEmpleadoAsistenciaRecibos.cs:145). Es el
          1162 con las fechas de comisiones de `dbo.Constantes` y los empleados
          marcados; su juego de parametros esta en
          BuildReciboDetalleZaraLegacyParamsAsync. No sale del arbol ni tiene
          boton propio: lo dispara el de RECIBOS DE NOMINA cuando la empresa es
          Zaragoza, igual que en Mac31.
        */
        new("nomina_recibos_detalle_zara", "administracion", "Cobranza pagadas de los recibos", SinPermiso, EnArbol: false),
        Grupo("administracion", "asistencias", "Asistencias"),
        // Nodo del arbol del modulo MC (ReportesMenu.cs:966). Su permiso seria
        // reportes.administracion.control_acumulado_de_estimaciones.ver y NO esta
        // en WPermiso, asi que entra abierto, como los otros cinco: no se invento.
        new("control_acumulado_de_estimaciones", "administracion", "Control Acumulado de Estimaciones", SinPermiso)
    ];

    private static readonly Dictionary<string, ReporteCatalogoItem> _porClave =
        _reportes.ToDictionary(x => x.Clave, StringComparer.OrdinalIgnoreCase);

    /// <summary>Los 53 reportes, en el orden del menu.</summary>
    public static IReadOnlyList<ReporteCatalogoItem> Reportes => _reportes;

    /// <summary>El reporte, o null si la clave no existe.</summary>
    public static ReporteCatalogoItem? Buscar(string? clave)
    {
        if (string.IsNullOrWhiteSpace(clave))
            return null;
        return _porClave.TryGetValue(clave.Trim(), out var item) ? item : null;
    }

    /// <summary>
    /// El permiso que exige ese reporte, o null si no tiene (y entonces no se
    /// bloquea). Tambien devuelve null para una clave desconocida: el
    /// repositorio ya rebota esas con "Reporte de ventas no soportado".
    /// </summary>
    public static string? PermisoDe(string? clave) => Buscar(clave)?.Permiso;

    /// <summary>Con la regla: reportes.{grupo}.{clave}.ver</summary>
    private static ReporteCatalogoItem Grupo(string grupo, string clave, string etiqueta)
        => new(clave, grupo, etiqueta, $"reportes.{grupo}.{clave}.ver");

    private static ReporteCatalogoItem Ventas(string clave, string etiqueta)
        => Grupo("ventas", clave, etiqueta);
}
