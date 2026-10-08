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
public sealed record ReporteCatalogoItem(string Clave, string Grupo, string Etiqueta, string? Permiso);

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
        Ventas("concentrados", "Concentrados"),
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
        new("cambios_vigencia", "ventas", "Cambios de Vigencia", SinPermiso),

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
