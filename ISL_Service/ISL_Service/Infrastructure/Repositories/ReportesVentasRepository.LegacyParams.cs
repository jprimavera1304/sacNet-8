using System.Data;
using System.Globalization;
using ISL_Service.Application.DTOs.Reportes;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Reportes;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Infrastructure.Reports;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

public partial class ReportesVentasRepository
{
    private static List<int> NormalizeCatalogIds(IEnumerable<int>? ids)
    {
        return (ids ?? Enumerable.Empty<int>())
            .Where(x => x > 0)
            .Distinct()
            .ToList();
    }

    private static int ResolveReporte(ReportesVentasAcumuladoresProductosRequest request)
    {
        if (request.IDGrupoCategoria == GrupoCategoriaAcumuladores)
            return ReporteVentaAcumuladores;

        var categoria = (request.Categoria ?? "").Trim().ToLowerInvariant();
        return categoria == "acumuladores" || categoria == "acumulador"
            ? ReporteVentaAcumuladores
            : ReporteVentaProductos;
    }

    private static int ResolveReporteRemisiones(ReportesVentasRemisionesRequest request)
    {
        return ResolveStatusFolio(request.EstatusFolio) == 4
            ? ReporteVentasRemisionesSaldoFavor
            : ReporteVentasRemisiones;
    }

    private static int ResolveReporteFolios(ReportesVentasFoliosRequest request)
    {
        if (request.FoliosDineroCascos)
            return ReporteVentasFoliosDineroUsados;

        return ResolveStatusFolio(request.EstatusFolio) == 4
            ? ReporteVentasRemisionesSaldoFavor
            : ReporteVentasFoliosGlobal;
    }

    private static int ResolveReporteFacturas(ReportesVentasFacturasRequest request)
    {
        return ReporteVentasFacturas;
    }

    private static int ResolveReporteConcentrados(ReportesVentasConcentradosRequest request)
    {
        return ReporteVentasConcentradosDetalle;
    }

    /*
      EL NODO "Cobranza" ES EL 1160 EN TAURO Y EL 1161 EN ZARAGOZA.

      1160, 1161 y 1162 se llaman igual y comparten sp_n_rptVentasSaldosGlobales.
      El arbol de Mac31 reparte asi (ReportesMenu.cs:732-740):
        "Cobranza"           -> 1160  (esta clave)
        "Cobranza Detallado" -> 1172  (clave cobranza_detallado)
        "Cobranza Pagadas"   -> 1162 / 1166

      AQUI DECIA "el 1161 no lo usa ningun nodo: era el que estaba aqui por
      descarte". ERA FALSO, Y SOLO SE VE MIDIENDO CONTRA ZARAGOZA: Mac31 manda el
      1160 en las dos empresas, pero el SERVICIO de Legacy lo cambia por el 1161
      cuando la empresa es Zaragoza, antes de elegir la plantilla
      (MacServicios2/.../ReporteV2Service.cs:783):

          if ((funcionalidad.ToUpper() == "ZARA") &&
              (IDReporte == (int)enumReportes.VentasCobranzaDetallado))
          {
              reporteInputDTO.idReporte = (int)enumReportes.VentasCobranzaDetalladoZara;
              IDReporte = (int)enumReportes.VentasCobranzaDetalladoZara;
          }

      Es la UNICA rama por empresa que queda viva en todo ReporteV2Service.cs (la
      de Listas de Precios Tauro esta comentada), y era la unica que faltaba
      copiar. El SP devuelve el MISMO dato en los dos casos -no mira el id-, asi
      que lo que cambiaba era solo la PLANTILLA: la del 1160 tiene 4 columnas de
      importes ("Total a Pagar / Abonos / Desctos / Saldo") y la del 1161 tiene 11
      ("Saldo Dinero / Cargos / Abonos / Desctos / Pagos Dinero / Pagos Cascos /
      Pagos Diff Cascos / Saldo Cascos / Dias Venc"). Midiendo el mismo psp en
      Zaragoza, el nuestro imprimia 1 298 importes contra los 6 734 de Legacy, y
      46 de los nuestros eran numeros que Legacy no tiene: el dato correcto
      siempre estuvo ahi, se estaba vaciando en el molde equivocado.

      La condicion se copia TAL CUAL, con igualdad exacta a "ZARA" y no con un
      "no es Tauro": una tercera empresa (MacL, funcionalidad distinta) seguiria
      con el 1160, que es lo que hace Legacy.
    */
    private static int ResolveReporteCobranza(LegacyConstants constants)
    {
        return ReporteCobranzaPorEmpresa.Resolver(constants.Funcionalidad);
    }

    private static bool IsAcumuladoresProductosReporte(int idReporte)
    {
        return idReporte == ReporteVentaAcumuladores || idReporte == ReporteVentaProductos;
    }

    private static bool IsRemisionesReporte(int idReporte)
    {
        return idReporte == ReporteVentasRemisiones
            || idReporte == ReporteVentasRemisionesSaldoFavor
            || idReporte == ReporteVentasRemisionesImpuestos;
    }

    private static bool IsFoliosReporte(int idReporte)
    {
        return idReporte == ReporteVentasFoliosGlobal
            || idReporte == ReporteVentasFoliosDineroUsados;
    }

    private static bool IsFacturasReporte(int idReporte)
    {
        return idReporte == ReporteVentasFacturas;
    }

    private static bool IsConcentradosReporte(int idReporte)
    {
        return idReporte == ReporteVentasConcentradosDetalle;
    }

    private static bool IsCobranzaReporte(int idReporte)
    {
        return idReporte == ReporteVentasCobranzaDetallado
            || idReporte == ReporteVentasCobranzaDetalladoZara
            || idReporte == ReporteVentasCobranzaPagadasDetalladoZara
            || idReporte == ReporteVentasCobranzaPagadasTotalizadoZara
            || idReporte == ReporteVentasCobranzaDesglosado
            || idReporte == ReporteVentasCobranzaZaragoza;
    }

    /*
      DE LA CLAVE DEL CATALOGO AL IDReporte.

      Es async y recibe la conexion porque tres familias no se pueden resolver
      sin preguntarle a la base QUIEN ES ESTA EMPRESA: Pagos (71/75 contra 72/76
      contra 77) y Listas de Precios (1240 contra las cuatro de Tauro) eligen el
      id por Constantes.Funcionalidad y Constantes.EsCentroServicio, igual que
      Mac31. Resolverlo con una constante de C# seria correcto para una empresa y
      silenciosamente falso para la otra.
    */
    private static async Task<int> ResolveReporteLegacyVentasAsync(
        SqlConnection conn,
        ReportesVentasLegacyRequest request,
        CancellationToken ct)
    {
        if (request.IDReporte > 0)
            return request.IDReporte;

        var clave = (request.ReporteKey ?? "").Trim().ToLowerInvariant();

        /*
          "comprobante_de_cascos" VA AQUI, CON LAS CONSTANTES EN LA MANO, porque
          depende de la empresa: Zaragoza imprime el 1255 (un movimiento) y
          Tauro el 1260 (el diario de un dia). Es el mismo caso que "cobranza".
          Se mantiene la regla de la casa: el front manda la CLAVE y nunca el
          IDReporte, asi que es aqui —y solo aqui— donde se decide.
        */
        /*
          "inventario_actual" e "inventario_faltante" ENTRAN AQUI porque la
          variante de FILTROS es solo de Tauro.

          Mac31 manda la categoria FILTROS al reporte de Filtros unicamente
          cuando la instalacion es Tauro:

              if (Variables.funcionalidad.ToUpper().Contains("TAU"))
                  if (IDGrupoCategoria == ID_GRUPO_CATEGORIA_FILTROS)
                      this.IDReporte = InventarioFiltros;      // 212
                  else
                      this.IDReporte = InventarioProductos;    // 210
              else
                  this.IDReporte = InventarioProductos;        // 210

          (ReportesMenu.cs, el case InventarioAcumuladores). Nosotros mandabamos
          al 212 en cualquier empresa, asi que en Zaragoza -Funcionalidad =
          "ZARA", leido de Constantes en MacZ- el papel salia titulado
          "Inventario Actual de Filtros" en vez de "... de Productos" y con otra
          plantilla de totales (652 contra 942 caracteres en Template_Html). Es
          literalmente otro documento.
        */
        if (clave is "pagos" or "listas_de_precios" or "listas_de_precios_imp" or "cobranza"
            or "comprobante_de_cascos" or "inventario_actual" or "inventario_faltante")
        {
            var constants = await ConsultarConstantesAsync(conn, ct);
            if (clave == "cobranza")
                return ResolveReporteCobranza(constants);
            if (clave == "comprobante_de_cascos")
                return EsTauro(constants) ? ReporteComprobanteCascosTauro : ReporteComprobanteCascos;
            if (clave == "inventario_actual")
                return ResolveReporteInventarioActual(request, constants);
            if (clave == "inventario_faltante")
                return ResolveReporteInventarioFaltante(request.IDGrupoCategoria);
            return clave == "pagos"
                ? ResolveReportePagos(request.Formato, constants)
                : ResolveReporteListasPrecios(request, constants);
        }

        return clave switch
        {
            "acumuladores_y_productos" => ResolveReporte(request),
            "folios" => ResolveReporteFoliosLegacy(request),
            "remisiones" => ResolveStatusFolio(request.EstatusFolio) == 4 ? ReporteVentasRemisionesSaldoFavor : ReporteVentasRemisiones,
            "facturas" => ReporteVentasFacturas,
            "concentrados" => ReporteVentasConcentradosDetalle,
            // El DOCUMENTO de un concentrado, no el listado. Ver las constantes.
            "concentrado" => ReporteConcentrado,
            // "cobranza" NO esta aqui: depende de la empresa y se resuelve arriba,
            // con las constantes en la mano.
            "motobaterias" => ResolveReporteMotobaterias(request.FormatoMotobaterias),
            "cobranza_detallado" => ReporteVentasCobranzaZaragoza,
            "cobranza_pagadas" => ResolveFormatoDetallado(request.Formato) ? ReporteVentasCobranzaPagadasDetalladoZara : ReporteVentasCobranzaPagadasTotalizadoZara,
            // El MISMO 1162, con los parametros que le arma a mano Mac31 detras
            // de los recibos de Zaragoza. Ver BuildReciboDetalleZaraLegacyParamsAsync.
            "nomina_recibos_detalle_zara" => ReporteVentasCobranzaPagadasDetalladoZara,
            "estado_de_cuenta" => ResolveReporteEstadoCuenta(request.FormatoEstadoCuenta),
            "rutas_de_agentes" => ReporteVentasCobranzaDesglosado,
            "descuentos" => ReporteVentasPagosDescuentos,
            "transferencias_duplicadas" => ReporteVentasPagosTransferenciasDuplicadas,
            "transferencias_clientes" => ReporteTransferenciasClientes,
            "transferencias_estatus" => ReporteTransferenciasEstatus,
            "transferencias_folios" => ReporteTransferenciasFolios,
            "ventas_y_pagos" => ReporteVentasPagosComparativo,
            "utilidad" => request.IDGrupoCategoria == GrupoCategoriaAcumuladores ? ReporteVentasUtilidadAcumuladores : ReporteVentasUtilidadProductos,
            "clientes_globales" => ReporteClientesGlobal,
            "clientes_con_descuentos" => ReporteClientesDescuentos,
            "clientes_sin_descuentos" => ReporteClientesDescuentosSin,
            "clientes_compras" => ReporteClientesConSinCompra,
            "clientes_con_compra" => ReporteClientesConCompra,
            "clientes_con_compra_por_dia" => ReporteClientesConCompraPorDia,
            "clientes_sin_compra" => ReporteClientesSinCompra,
            "clientes_facturas_rfc" => ReporteClientesFacturasRFC,
            "clientes_acum_moto_lub" => ResolveReporteClientesAcumMotoLub(request),
            "clientes_acum_moto_lub_clarios" => ReporteClientesAcumMotoLubClarios,
            "compras_acumuladores_y_productos" => request.IDGrupoCategoria == GrupoCategoriaAcumuladores ? ReporteCompraAcumuladores : ReporteCompraProductos,
            "compras_facturas" => ReporteComprasFacturas,
            // La comprobacion de las facturas MARCADAS. Sus ids van en idsCompra.
            "compra_comprobacion" => ReporteComprasComprobacion,
            // El documento de UNA compra: IMPRIMIR y el doble clic. Su id va
            // tambien en idsCompra, pero uno solo (ver el case del 1015).
            "compra_detalle" => ReporteComprasDetalle,
            // "inventario_actual" e "inventario_faltante" se resuelven arriba,
            // con las Constantes en la mano: la variante de Filtros es de Tauro.
            "ajustes_al_inventario" => ReporteAjustesInventario,
            "movimientos" => ReporteMovimientosAlmacen,
            "cascos" => ReporteVentasUsadosCreditos,
            "cascos_excedentes" => ReporteVentasCascosExcedentes,
            "liquidaciones" => ReporteVentasLiquidaciones,
            "cobros_dinero_y_cascos" => (request.DineroCascos ?? "").Trim().Equals("cascos", StringComparison.OrdinalIgnoreCase) ? ReporteVentasCobrosCascos : ReporteVentasCobrosDinero,
            "movimientos_centros" => ReporteCentrosRemisiones,
            "centros_pagos_entradas_salidas" => ReporteCentrosPagosEntradasSalidas,
            "centros_garantias" => ReporteCentrosGarantias,
            "centros_de_servicio" => ReporteCentrosCompleto,
            "hoja_de_cobro_total_cobrado" => ResolveFormatoDetallado(request.Formato) ? ReporteHojaCobroTotalCobradoDetallado : ReporteHojaCobroTotalCobradoTotalizado,
            "hoja_de_cobro_total_cascos" => ReporteHojaCobroTotalCascos,
            "hoja_de_cobro_cheques" => ReporteHojaCobroCheques,
            // La hoja de UNA liquidacion. No es del periodo: es de las hojas
            // marcadas, y sus ids van en IDsConcentrado/IDsCobranza/IDsCobro.
            "hoja_de_cobro" => ReporteCobro,
            "garantias" => ReporteGarantias,
            "garantias_registradas" => ReporteGarantiasRegistradas,
            "gasolinas" => ResolveReporteGasolinas(request.Formato),
            "control_acumulado_de_estimaciones" => ReporteControlAcumuladoEstimaciones,
            "cortes" => ReporteCortes,
            /*
              LOS TRES DE LOS BOTONES DE CORTES. El porque de cada id, con la
              linea de Mac31 y los parametros leidos de su SP, esta en las
              constantes. Aqui solo la clave.
            */
            "corte_diario" => ReporteCorteDiario,
            "corte_diario_detalle_mac" => ReporteCorteDiarioDetalleMac,
            "corte_cs" => ReporteCorteCentroServicio,
            "gastos" => ResolveFormatoDetallado(request.Formato) ? ReporteGastosDetallado : ReporteGastosTotalizado,
            "impuestos" => ReporteVentasRemisionesImpuestos,
            "accesos" => ReporteAccesosMac,
            "asistencias" => ReporteAsistenciasZaragoza,
            // Los CINCO botones de ReporteEmpleadoAsistenciaRecibos, con la
            // tecla que les pone Mac31. Ver las constantes y ReportesCatalogo.
            "nomina_asistencias" => ReporteNominaAsistencias,              // F5
            "nomina_recibos" => ReporteNominaRecibos,                      // F6
            "nomina_inasistencias" => ReporteNominaInasistencias,          // F7
            "nomina_vacaciones" => ReporteNominaVacaciones,                // F8
            "nomina_solo_asistencias" => ReporteNominaAsistenciasCorto,    // F9
            // El reporte de la flotilla, que imprime ConsultarAutos de Mac31. No
            // lleva filtros: su sp_n_rptAutos recibe el @Params generico y ya.
            "autos" => ReporteAutos,
            // El documento de UN cambio de vigencia, el que imprime
            // ConsultarCambiosVigencia de Mac31. No es un listado: pide el id.
            "cambios_vigencia" => ReporteCambiosVigencia,
            // Las liquidaciones de descuento SELECCIONADAS, lo que imprime
            // ConsultarLiquidacionesDescuentos de Mac31.
            "liquidaciones_descuentos" => ReporteVentasDescuentosAjustes,
            /*
              La VENTA de cascos a un gallero (el radio GALLEROS de
              ConsultarUsadosMovimientos). No depende de la empresa —por eso no
              esta arriba con las constantes—: es una clase de movimiento
              distinta, no otro formato del comprobante.
            */
            "venta_de_cascos" => ReporteVentaCascos,
            _ => throw new InvalidOperationException("Reporte de ventas no soportado.")
        };
    }

    private static int ResolveReporteGasolinas(string? formato)
    {
        return (formato ?? "").Trim().Equals("mensual", StringComparison.OrdinalIgnoreCase)
            ? ReporteGasolinasMensual
            : ReporteGasolinasDetallado;
    }

    /*
      LISTAS DE PRECIOS: EL id SALE DE LA EMPRESA Y DEL COMBO, NO DE UNA CONSTANTE.

      Mac31 (ReportesMenu.cs:3925-3952): si la categoria no es acumuladores es
      siempre el 1245; si es acumuladores, el 1240 EXCEPTO cuando la empresa es
      Tauro, donde el combo cboTipoLista elige entre las cuatro listas
      personalizadas. Los cuatro SP piden los mismos 17 parametros que el 1240.
    */
    private static int ResolveReporteListasPrecios(ReportesVentasLegacyRequest request, LegacyConstants constants)
    {
        if (request.IDGrupoCategoria != GrupoCategoriaAcumuladores)
            return ReporteListasPreciosProductosZaragoza;

        if (!EsTauro(constants))
            return ReporteListasPreciosZaragoza;

        return request.TipoLista switch
        {
            2 => ReporteListasPreciosTauroTodasMenosTauro,
            3 => ReporteListasPreciosTauroCroGonXtLthClAmFpDy,
            4 => ReporteListasPreciosTauroFpAmLth,
            _ => ReporteListasPreciosTauroTodas
        };
    }

    /*
      PAGOS: 71/75 en Zaragoza, 77 en Tauro, 72/76 en un centro de servicio.

      Los cinco comparten sp_n_rptVentasPagosGlobales y los mismos 12
      parametros; cambia la plantilla. Mac31 elige igual
      (ReportesMenu.cs:3341-3397): el detallado se vuelve 77 cuando
      funcionalidad == "TAU", y los dos se vuelven 72/76 cuando
      EsCentroServicio == 1. El 76 no tiene renglon en dbo.Reportes en ninguna de
      las dos bases, asi que en un centro de servicio el totalizado se queda en
      el 75: es mejor imprimir el papel generico que un 500.
    */
    private static int ResolveReportePagos(string? formato, LegacyConstants constants)
    {
        if (!ResolveFormatoDetallado(formato))
            return ReporteVentasPagosTotalizado;

        if (constants.EsCentroServicio == 1)
            return ReporteVentasPagosDetalladoCS;

        return EsTauro(constants)
            ? ReporteVentasPagosDetalladoTauro
            : ReporteVentasPagosDetallado;
    }

    private static bool EsTauro(LegacyConstants constants)
        => (constants.Funcionalidad ?? "").Trim().ToUpperInvariant().Contains("TAU");

    /*
      CLIENTES ACUM MOTO LUB: 116 normal, 118 el mensual de todas las categorias,
      119 el mensual de una categoria. Un solo spV2 y un solo nodo en Mac31.
    */
    private static int ResolveReporteClientesAcumMotoLub(ReportesVentasLegacyRequest request)
    {
        if (!request.VentasMensual)
            return ReporteClientesAcumMotoLub;

        /* Igual que Mac31: el indice 0 ("TODOS (SOLO EXCEL)") es el 118 y
           cualquier otro es el 119. Ver ReportesMenu.cs, el case
           ClientesAcumMotoLub. */
        return ResolveCategoriaCortaLegacy(request) == 0
            ? ReporteClientesAcumMotoLubMensual
            : ReporteClientesAcumMotoLubMensualCategoria;
    }

    /// <summary>
    /// EL INDICE DEL COMBO CORTO DE CATEGORIAS (0..3) QUE ESPERAN EL 118, EL 119
    /// Y EL 121.
    ///
    /// 0 = todas, 1 = acumuladores, 2 = motobaterias, 3 = lubricantes. No es un
    /// IDGrupoCategoria: el porque, y el trozo del SP que lo demuestra, estan en
    /// <see cref="ReportesVentasLegacyRequest.CategoriaCorta"/>.
    ///
    /// Cuando `categoriaCorta` viene vacio se PASA `IDGrupoCategoria` TAL CUAL,
    /// que es lo que hacia este codigo desde el principio y lo que los dos
    /// clientes ya esperan: comprobado leyendolos. Para el 121, el web manda
    /// `OPTIONS.categoriasCortoClarios = [1 ACUMULADORES, 2 MOTOBATERIAS,
    /// 3 LUBRICANTES]` dentro de `idGrupoCategoria`
    /// (reportes-generation.js, buildLegacyVentasRequest) y el movil hace lo
    /// mismo (constructor_peticion.dart). O sea que ese numero YA es el indice
    /// del combo, no un id de grupo, y traducirlo lo estropearia.
    ///
    /// LA TRAMPA QUE SE QUEDA APUNTADA. Si algun dia un cliente manda aqui un
    /// IDGrupoCategoria de verdad -de la lista general de grupos, donde en MacZ
    /// FILTROS es el 2 y LUBRICANTES el 4- el reporte saldria mal y callado:
    /// el 2 se leeria como MOTOBATERIAS y el 4 no cae en NINGUNA rama del SP, o
    /// sea que el papel saldria sin filtro de categoria. Para eso esta
    /// `categoriaCorta`: pidiendolo por nombre no hay forma de equivocarse.
    /// </summary>
    private static int ResolveCategoriaCortaLegacy(ReportesVentasLegacyRequest request)
    {
        var elegida = (request.CategoriaCorta ?? "").Trim().ToLowerInvariant();
        if (elegida.Length > 0)
        {
            return elegida switch
            {
                "acumuladores" => 1,
                "motobaterias" or "moto" => 2,
                "lubricantes" => 3,
                _ => 0
            };
        }

        return request.IDGrupoCategoria > 0 ? request.IDGrupoCategoria : 0;
    }

    /// <summary>La misma eleccion que el endpoint propio de folios.</summary>
    private static int ResolveReporteFoliosLegacy(ReportesVentasLegacyRequest request)
    {
        if (request.FoliosDineroCascos)
            return ReporteVentasFoliosDineroUsados;

        return ResolveStatusFolio(request.EstatusFolio) == 4
            ? ReporteVentasRemisionesSaldoFavor
            : ReporteVentasFoliosGlobal;
    }

    private static int ResolveReporteMotobaterias(string? formato)
    {
        return (formato ?? "cliente").Trim().ToLowerInvariant() switch
        {
            "folio" => ReporteVentasMotoBateriasPorFolio,
            "codigo" => ReporteVentasMotoBateriasPorCodigo,
            "marca" => ReporteVentasMotoBateriasPorMarca,
            _ => ReporteVentasMotoBateriasPorCliente
        };
    }

    /*
      EL FORMATO ES EL QUE DECIDE, Y ANTES NO DECIDIA NADA.

      Esta funcion recibia su parametro y devolvia siempre 1370, asi que las
      constantes 1375 y 1380 existian sin que nadie pudiera alcanzarlas. Peor:
      ResolveFormatoEstadoCuenta si traducia "dinero" a Formato=1, de modo que al
      1370 se le pedia un formato que su plantilla no es.

      El reparto es el de Mac31 (ReportesMenu.cs:2495-2512), y los tres leen los
      mismos 9 parametros:
        "" / ambos  -> 1370  Estado de Cuenta          (sp_n_rptVentasEstadoCuenta)
        dinero      -> 1375  Estado de Cuenta DINERO   (sp_n_rptVentasEstadoCuentaGlobales)
        cascos      -> 1380  Estado de Cuenta CASCOS   (idem)

      Nota de herencia: en Mac31 el combo cboFormatoEdoCuenta esta COMENTADO
      (ReportesMenu.cs:1375), asi que alla siempre sale el 1370. 1375 y 1380 son
      papel que la base tiene y que Mac31 hoy no sabe pedir.
    */
    private static int ResolveReporteEstadoCuenta(string? formato)
    {
        return (formato ?? "").Trim().ToLowerInvariant() switch
        {
            "dinero" => ReporteVentasEstadoCuentaDinero,
            "cascos" => ReporteVentasEstadoCuentaCascos,
            _ => ReporteVentasEstadoCuenta
        };
    }

    /// <summary>El grupo FILTROS. Solo Tauro tiene reporte propio para el.</summary>
    private const int GrupoCategoriaFiltros = 2;

    private static int ResolveReporteInventarioActual(ReportesVentasLegacyRequest request, LegacyConstants constants)
    {
        var idGrupoCategoria = request.IDGrupoCategoria > 0 ? request.IDGrupoCategoria : GrupoCategoriaAcumuladores;

        if (request.InventarioConCostos)
        {
            /*
              NO HAY VARIANTE DE FILTROS CON COSTOS, Y NO ES UN OLVIDO: en Mac31
              esa rama esta COMENTADA (`////else if(IDGrupoCategoria ==
              ID_GRUPO_CATEGORIA_FILTROS) this.IDReporte = InventarioFiltrosCosto;`),
              asi que ni Tauro ni Zaragoza sacan nunca el 227. Pedirlo era
              imprimir un reporte que en Mac31 no existe.
            */
            return idGrupoCategoria == GrupoCategoriaAcumuladores
                ? ReporteInventarioAcumuladoresCosto
                : ReporteInventarioProductosCosto;
        }

        if (request.InventarioGenerarPedido)
        {
            return idGrupoCategoria == GrupoCategoriaAcumuladores
                ? ReportePedidoAcumuladores
                : ReportePedidoProductos;
        }

        if (idGrupoCategoria == GrupoCategoriaAcumuladores)
            return ReporteInventarioAcumuladores;

        // El 212 (Filtros) solo en Tauro; en Zaragoza FILTROS es el 210.
        return idGrupoCategoria == GrupoCategoriaFiltros && EsTauro(constants)
            ? ReporteInventarioFiltros
            : ReporteInventarioProductos;
    }

    /*
      EL FALTANTE NO TIENE VARIANTE DE FILTROS EN NINGUNA EMPRESA. Igual que el
      de costos: la rama de Mac31 esta comentada (`////else if
      (IDGrupoCategoria == ID_GRUPO_CATEGORIA_FILTROS) this.IDReporte =
      InventarioFaltanteFiltros;`), asi que el 1141 no lo imprime nadie. Antes lo
      pediamos para la categoria FILTROS y salia con otro titulo y otra plantilla
      de totales que el papel de Mac31.
    */
    private static int ResolveReporteInventarioFaltante(int idGrupoCategoria)
    {
        return idGrupoCategoria == GrupoCategoriaAcumuladores
            ? ReporteInventarioFaltanteAcumuladores
            : ReporteInventarioFaltanteProductos;
    }

    private static string ResolveFormatoEstadoCuenta(string? formato)
    {
        return (formato ?? "").Trim().ToLowerInvariant() switch
        {
            "dinero" => "1",
            "cascos" => "2",
            _ => "0"
        };
    }

    /*
      LOS ARGUMENTOS QUE RECIBE EL PROCEDIMIENTO DEL REPORTE.

      Casi todos los `sp_n_rpt*` reciben UNO: el numero de renglon de
      `dbo.Parametros`. `ExecuteLegacySpAsync` ya sabe pasar varios -los separa
      por "|" y los reparte en el orden en que los declara el procedimiento,
      igual que hace Legacy- pero hasta ahora todos los llamadores pasaban uno
      solo.

      HAY DOS QUE RECIBEN DOS, y no es un capricho suyo: lo dice sys.parameters.

          sp_n_ConsultaEmpleadosTipoSueldoRecibo
              @Params               varchar   (sin valor por omision)
              @IDPeriodoTipoSueldo  int       (sin valor por omision)

      Son los reportes 1024 y 1025. Con un solo argumento, el segundo le llegaba
      como cadena VACIA y reventaba antes de entrar al procedimiento:

          FormatException: Failed to convert parameter value from a String to
          an Int32. The input string '' was not in a correct format.

      Que es un 500 "Error interno" que no dice nada y que parece del motor
      cuando en realidad es una firma distinta. El periodo YA esta guardado en el
      renglon de parametros (Param3), asi que se vuelve a leer de ahi: no hace
      falta arrastrarlo por toda la pila.
    */
    private static string ArgumentosDelProcedimiento(int idReporte, int parametroId, DataRow? row)
    {
        /*
          EL 1015 NO RECIBE EL RENGLON DE PARAMETROS: RECIBE EL IDCompra.

          Sus tres procedimientos son de la generacion vieja y declaran
          `@IDCompra1, @IDCompra2, @IDCompra3`. Mac31 les manda `<id>||`, o sea
          el id en el primero y los otros dos VACIOS -no nulos-, y los otros
          dos existen porque los de usados si saben leer listas separadas por
          '~' aunque nadie se las mande.

          Pasarle `parametroId` seria pedirle la compra numero 313320, que es
          el numero de renglon de `dbo.Parametros`: no reventaria, devolveria
          CERO filas, y el papel saldria con "No se encontro informacion". Un
          reporte vacio sin ningun error es el peor resultado posible, asi que
          esto va antes que nada.
        */
        if (idReporte == ReporteComprasDetalle)
        {
            var idCompra = row is null ? 0 : ReadInt(row, "Param2");
            return string.Create(CultureInfo.InvariantCulture, $"{idCompra}||");
        }

        if (idReporte is not (ReporteNominaAsistencias or ReporteNominaAsistenciasCorto or ReporteNominaRecibos))
            return parametroId.ToString(CultureInfo.InvariantCulture);

        var periodo = row is null ? 0 : ReadInt(row, "Param3");
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{parametroId}|{periodo}");
    }

    private static bool IsGenericVentasReporte(int idReporte)
    {
        return idReporte is
            ReporteVentasMotoBateriasPorCliente or
            ReporteVentasMotoBateriasPorFolio or
            ReporteVentasMotoBateriasPorCodigo or
            ReporteVentasMotoBateriasPorMarca or
            ReporteVentasPagosDetallado or
            ReporteVentasPagosDetalladoCS or
            ReporteVentasPagosDescuentos or
            ReporteVentasPagosTransferenciasDuplicadas or
            ReporteVentasPagosTotalizado or
            ReporteVentasPagosTotalizadoCS or
            ReporteVentasPagosDetalladoTauro or
            ReporteClientesGlobal or
            ReporteClientesConCompra or
            ReporteClientesConCompraPorDia or
            ReporteClientesSinCompra or
            ReporteClientesConSinCompra or
            ReporteClientesDescuentos or
            ReporteClientesDescuentosSin or
            ReporteClientesAcumMotoLub or
            ReporteClientesAcumMotoLubMensual or
            ReporteClientesAcumMotoLubMensualCategoria or
            ReporteClientesAcumMotoLubClarios or
            ReporteCompraAcumuladores or
            ReporteCompraProductos or
            ReporteComprasFacturas or
            // 1017: plantilla rptTemplate y SP que recibe el @Params generico.
            ReporteComprasComprobacion or
            /*
              1015: NO recibe el @Params generico ni lo pinta la plantilla
              generica (lo arma CompraHtmlBuilder). Entra en esta lista solo
              por las otras dos cosas que decide: que el reporte esta
              SOPORTADO, y de que Param sale la presentacion.
            */
            ReporteComprasDetalle or
            ReporteInventarioAcumuladores or
            ReporteInventarioProductos or
            ReporteInventarioFiltros or
            ReporteInventarioAcumuladoresCosto or
            ReporteInventarioProductosCosto or
            ReporteInventarioFiltrosCosto or
            ReportePedidoAcumuladores or
            ReportePedidoProductos or
            ReporteInventarioFaltanteAcumuladores or
            ReporteInventarioFaltanteProductos or
            ReporteInventarioFaltanteFiltros or
            ReporteAjustesInventario or
            ReporteMovimientosAlmacen or
            ReporteGasolinasDetallado or
            ReporteGasolinasMensual or
            ReporteListasPreciosZaragoza or
            ReporteListasPreciosProductosZaragoza or
            ReporteListasPreciosTauroTodas or
            ReporteListasPreciosTauroTodasMenosTauro or
            ReporteListasPreciosTauroCroGonXtLthClAmFpDy or
            ReporteListasPreciosTauroFpAmLth or
            ReporteControlAcumuladoEstimaciones or
            ReporteCortes or
            // Los tres de los botones de cortes: plantilla rptTemplate y SP que
            // recibe el @Params generico, igual que el 1320.
            ReporteCorteDiario or
            ReporteCorteDiarioDetalleMac or
            ReporteCorteCentroServicio or
            ReporteGastosDetallado or
            ReporteGastosTotalizado or
            ReporteAccesosMac or
            ReporteAsistenciasZaragoza or
            // Los CINCO de nomina: su juego de parametros es propio (se arma
            // abajo) pero el camino de render y de Excel es el mismo generico.
            ReporteNominaAsistencias or
            ReporteNominaAsistenciasCorto or
            ReporteNominaInasistencias or
            ReporteNominaVacaciones or
            ReporteNominaRecibos or
            // El de la flotilla: mismo camino generico que los otros, su SP recibe
            // el @Params y devuelve la tabla que pinta la plantilla rptTemplate.
            ReporteAutos or
            // Cambios de Vigencia: su plantilla es rptTemplate y su SP recibe el
            // @Params generico, igual que los otros cincuenta y tres.
            ReporteCambiosVigencia or
            /*
              Los tres comprobantes de cascos. Sin estar en esta lista el POST
              arma el psp y el GET de ReportesV3W no sabe re-hidratarlo: el
              reporte "se genera" y al abrirlo no sale nada.
            */
            ReporteComprobanteCascos or
            ReporteVentaCascos or
            ReporteComprobanteCascosTauro or
            ReporteClientesFacturasRFC or
            ReporteVentasUtilidadAcumuladores or
            ReporteVentasUtilidadProductos or
            ReporteVentasPagosComparativo or
            ReporteVentasDescuentosAjustes or
            ReporteVentasUsadosCreditos or
            ReporteGarantias or
            ReporteGarantiasRegistradas or
            ReporteVentasCascosExcedentes or
            ReporteVentasLiquidaciones or
            ReporteVentasCobrosDinero or
            ReporteVentasCobrosCascos or
            ReporteVentasEstadoCuenta or
            ReporteVentasEstadoCuentaDinero or
            ReporteVentasEstadoCuentaCascos or
            ReporteTransferenciasClientes or
            ReporteTransferenciasEstatus or
            ReporteTransferenciasFolios or
            ReporteCentrosRemisiones or
            ReporteCentrosPagosEntradasSalidas or
            ReporteCentrosGarantias or
            ReporteCentrosCompleto or
            ReporteHojaCobroTotalCobradoDetallado or
            ReporteHojaCobroTotalCobradoTotalizado or
            ReporteHojaCobroTotalCascos or
            ReporteHojaCobroCheques;
    }

    private static async Task<LegacyReportParams> BuildLegacyParamsAsync(
        SqlConnection conn,
        int idReporte,
        ReportesVentasAcumuladoresProductosRequest request,
        CancellationToken ct)
    {
        var idGrupoCategoria = request.IDGrupoCategoria > 0 ? request.IDGrupoCategoria : GrupoCategoriaAcumuladores;
        var idCliente = await ResolveClienteAsync(conn, request, ct);

        return new LegacyReportParams
        {
            NombreEquipo = ResolveLegacyNombreEquipo(request),
            IDUsuario = ResolveLegacyIDUsuario(request),
            Param1 = BuildLegacyUsuarioParam(request),
            Param2 = JoinIds(request.IDEmpresas),
            Param3 = idGrupoCategoria.ToString(),
            Param4 = JoinIds(request.IDSubcategorias),
            Param5 = JoinIds(request.IDMarcas),
            Param6 = JoinIds(request.IDAlmacenes),
            Param7 = ResolveTipoDocumento(request.Documento).ToString(),
            Param8 = idCliente.ToString(),
            Param9 = JoinIds(request.IDAgentes),
            Param10 = request.FechaInicial.ToString("yyyy-MM-dd"),
            Param11 = request.FechaFinal.ToString("yyyy-MM-dd"),
            Param12 = ResolvePresentacion(request.Salida).ToString(),
            Param13 = idReporte == ReporteVentaAcumuladores && request.SoloServiciosDomicilio
                ? ProductoServicioDomicilio.ToString()
                : ""
        };
    }

    private static async Task<LegacyReportParams> BuildRemisionesLegacyParamsAsync(
        SqlConnection conn,
        ReportesVentasRemisionesRequest request,
        CancellationToken ct)
    {
        var idReporte = ResolveReporteRemisiones(request);
        return await BuildDocumentosVentaLegacyParamsAsync(conn, idReporte, request, request.EstatusFolio, request.IDUsuarios, ct);
    }

    /*
      Recibe estatusFolio e idUsuarios aparte en vez de pedir un
      ReportesVentasRemisionesRequest: asi lo pueden llamar tanto los endpoints
      propios (remisiones, folios, facturas) como la ruta generica
      legacy/generar, que trae los mismos campos en otro DTO. Antes esa ruta no
      podia, y las claves remisiones/folios/facturas del catalogo acababan en un
      500 "Reporte de ventas no soportado".
    */
    private static async Task<LegacyReportParams> BuildDocumentosVentaLegacyParamsAsync(
        SqlConnection conn,
        int idReporte,
        ReportesVentasAcumuladoresProductosRequest request,
        string? estatusFolio,
        List<int>? idUsuarios,
        CancellationToken ct,
        int formato = 1,
        int tipo = 0)
    {
        var idCliente = await ResolveClienteLegacyAsync(conn, request, ct);
        return new LegacyReportParams
        {
            NombreEquipo = ResolveLegacyNombreEquipo(request),
            IDUsuario = ResolveLegacyIDUsuario(request),
            Param1 = BuildLegacyUsuarioParam(request),
            Param2 = JoinIds(request.IDEmpresas),
            Param3 = JoinIds(request.IDAlmacenes),
            Param4 = ResolveTipoDocumento(request.Documento).ToString(),
            Param5 = idCliente.ToString(),
            Param6 = JoinIds(request.IDAgentes),
            Param7 = ResolveStatusFolio(estatusFolio).ToString(),
            Param8 = request.FechaInicial.ToString("yyyy-MM-dd"),
            Param9 = request.FechaFinal.ToString("yyyy-MM-dd"),
            Param10 = ResolvePresentacion(request.Salida).ToString(),
            Param11 = JoinIds(idUsuarios),
            Param12 = formato.ToString(),
            Param13 = tipo.ToString(),
            Param14 = idReporte == ReporteVentasRemisiones && request.SoloServiciosDomicilio
                ? ProductoServicioDomicilio.ToString()
                : ""
        };
    }

    private static LegacyReportParams BuildConcentradosLegacyParams(
        ReportesVentasAcumuladoresProductosRequest request,
        List<int>? idRepartidores)
    {
        return new LegacyReportParams
        {
            NombreEquipo = ResolveLegacyNombreEquipo(request),
            IDUsuario = ResolveLegacyIDUsuario(request),
            Param1 = BuildLegacyUsuarioParam(request),
            Param2 = JoinIds(idRepartidores),
            Param3 = FormatLegacyStartDateTime(request.FechaInicial),
            Param4 = FormatLegacyEndDateTime(request.FechaFinal),
            Param5 = ResolvePresentacion(request.Salida).ToString()
        };
    }

    private static async Task<LegacyReportParams> BuildCobranzaLegacyParamsAsync(
        SqlConnection conn,
        ReportesVentasAcumuladoresProductosRequest request,
        string? cobranzaStatus,
        CancellationToken ct)
    {
        var idCliente = await ResolveClienteLegacyAsync(conn, request, ct);
        var idsCliente = ResolveCobranzaStatus(cobranzaStatus) == 4
            ? JoinIds(request.IDClientes)
            : idCliente.ToString();
        if (string.IsNullOrWhiteSpace(idsCliente))
            idsCliente = idCliente.ToString();

        return new LegacyReportParams
        {
            NombreEquipo = ResolveLegacyNombreEquipo(request),
            IDUsuario = ResolveLegacyIDUsuario(request),
            Param1 = BuildLegacyUsuarioParam(request),
            Param2 = JoinIds(request.IDEmpresas),
            Param3 = idCliente.ToString(),
            Param4 = JoinIds(request.IDAgentes),
            Param5 = "0",
            Param6 = ResolveCobranzaStatus(cobranzaStatus).ToString(),
            Param7 = "1",
            Param8 = FormatLegacyStartDateTime(request.FechaInicial),
            Param9 = FormatLegacyEndDateTime(request.FechaFinal),
            Param10 = ResolvePresentacion(request.Salida).ToString(),
            Param11 = idsCliente,
            Param12 = "0",
            Param13 = ""
        };
    }

    /*
      LOS CUATRO PARAMETROS DE LA HOJA DE COBRO, Y NINGUNO MAS.

      Los enumera el propio #ParamIds de sus procedimientos, leido en MacZ, y
      los manda igual Mac31 (Funciones.cs:223-237 llamado desde
      ConsultarCobros.cs:2635):

          Param1 = Equipo[IDUsuario
          Param2 = IDsConcentrado
          Param3 = IDsCobranza
          Param4 = IDsCobro

      Del 5 al 7 Mac31 manda cadena vacia y aqui tambien: el reporte NO lleva
      rango de fechas ni presentacion. Mandarle las fechas del filtro de la
      pantalla —que el DTO generico si trae— no lo filtraria: le cambiaria el
      sentido a un parametro que el procedimiento lee como otra cosa.

      LAS TRES LISTAS SE UNEN SIN LIMPIAR. No se usa JoinIds, que es lo que
      usan los otros cincuenta reportes, y la razon esta escrita en el DTO: ahi
      los ids son un conjunto y aqui son tres listas que se cruzan por
      posicion. `JoinIds` tira los ceros y los repetidos, y las dos cosas
      CORREN la lista: la cobranza de la tercera hoja se le aplicaria a la
      segunda y el papel saldria con los folios de otro repartidor.
    */
    private static LegacyReportParams BuildCobroLegacyParams(ReportesVentasLegacyRequest request)
    {
        if (request.IDsConcentrado.Count == 0)
            throw new ArgumentException("La hoja de cobro necesita al menos una hoja: manda idsConcentrado, idsCobranza e idsCobro.");

        if (request.IDsCobranza.Count != request.IDsConcentrado.Count ||
            request.IDsCobro.Count != request.IDsConcentrado.Count)
        {
            /* Se rechaza en vez de rellenar con ceros a proposito: si las
               longitudes no cuadran, quien llamo no esta mandando triples y
               completarlas aqui daria un reporte con los folios mezclados en
               silencio. Un reporte de dinero equivocado es peor que un 400. */
            throw new ArgumentException("idsConcentrado, idsCobranza e idsCobro tienen que traer el mismo numero de elementos, uno por hoja y en el mismo orden.");
        }

        return new LegacyReportParams
        {
            NombreEquipo = ResolveLegacyNombreEquipo(request),
            IDUsuario = ResolveLegacyIDUsuario(request),
            Param1 = BuildLegacyUsuarioParam(request),
            Param2 = JoinIdsPorPosicion(request.IDsConcentrado),
            Param3 = JoinIdsPorPosicion(request.IDsCobranza),
            Param4 = JoinIdsPorPosicion(request.IDsCobro),
            Param5 = "",
            Param6 = "",
            Param7 = ""
        };
    }

    /* Une tal cual: mismo orden, con los ceros dentro. Ver arriba. */
    private static string JoinIdsPorPosicion(IEnumerable<int> ids)
    {
        return string.Join("~", ids.Select(x => x.ToString(CultureInfo.InvariantCulture)));
    }

    /*
      LA HOJA DE COBRO NO ES DE LA FAMILIA GENERICA, y por eso tiene su propio
      predicado de una sola linea en vez de entrar en IsGenericVentasReporte:
      ese `is ... or ...` es la lista de los que SI pinta la plantilla
      generica, y meterlo ahi lo mandaria por ese camino.
    */
    private static bool IsCobroReporte(int idReporte) => idReporte == ReporteCobro;

    /*
      EL REPORTE NO NECESITA NADA DEL RENGLON DE PARAMETROS PARA PINTARSE.

      Los otros reportes reconstruyen aqui su peticion —fechas, empresas,
      salida— porque la plantilla generica imprime esos filtros en la cabecera.
      La hoja de cobro no imprime filtros: su cabecera son el folio, la fecha y
      el repartidor, y todo eso lo traen los procedimientos. Lo unico que se
      lee de la peticion es la salida, y por omision es "pantalla", que es la
      que hace que se arme el HTML.
    */
    private static ReportesVentasAcumuladoresProductosRequest BuildCobroStoredRequest()
    {
        return new ReportesVentasAcumuladoresProductosRequest();
    }

    /*
      LOS PARAMETROS DEL CONCENTRADO: LOS IDS Y NADA MAS.

      Mac31 lo pide con `VerReporte(enumReportes.Concentrado, IDsConcentrado)`
      (ConsultarConcentrados.cs:1239), y ese VerReporte deja Param2 = los ids
      separados por `~` y Param3..Param7 vacios (Funciones.cs:223-236). No lleva
      fechas, ni empresas, ni presentacion: la cabecera del papel -folio, fecha,
      repartidor, observaciones- la traen los procedimientos.

      Se reusa `idsConcentrado`, que es el campo que ya existia para la hoja de
      cobro, en vez de inventar otro: es el mismo dato pedido desde otra
      pantalla.
    */
    private static LegacyReportParams BuildConcentradoLegacyParams(ReportesVentasLegacyRequest request)
    {
        if (request.IDsConcentrado.Count == 0)
            throw new ArgumentException("El concentrado necesita al menos un concentrado: manda idsConcentrado.");

        return new LegacyReportParams
        {
            NombreEquipo = ResolveLegacyNombreEquipo(request),
            IDUsuario = ResolveLegacyIDUsuario(request),
            Param1 = BuildLegacyUsuarioParam(request),
            Param2 = JoinIdsPorPosicion(request.IDsConcentrado),
            Param3 = "",
            Param4 = "",
            Param5 = "",
            Param6 = "",
            Param7 = ""
        };
    }

    /*
      TAMPOCO ES DE LA FAMILIA GENERICA, y por la misma razon que la hoja de
      cobro: sus plantillas se llaman `concentrado_*` y la generica buscaria un
      `cuerpo_n` que este reporte no tiene.
    */
    private static bool IsConcentradoDocumentoReporte(int idReporte) => idReporte == ReporteConcentrado;

    /* No imprime filtros en la cabecera, asi que no hay nada que reconstruir
       del renglon de parametros. Mismo caso que BuildCobroStoredRequest. */
    private static ReportesVentasAcumuladoresProductosRequest BuildConcentradoStoredRequest()
    {
        return new ReportesVentasAcumuladoresProductosRequest();
    }

    private static async Task<LegacyReportParams> BuildLegacyVentasParamsAsync(
        SqlConnection conn,
        int idReporte,
        ReportesVentasLegacyRequest request,
        CancellationToken ct)
    {
        /*
          LAS SEIS FAMILIAS QUE TIENEN ENDPOINT PROPIO.

          Sus claves estaban en el catalogo pero no aqui, asi que el POST pasaba
          la validacion y moria abajo con 500. Se delegan a los mismos
          constructores que usa su endpoint propio: un solo juego de parametros,
          no dos que puedan separarse.
        */
        /*
          EL SEGUNDO REPORTE QUE ZARAGOZA LANZA DETRAS DE LOS RECIBOS.

          Es el 1162 otra vez, pero NO con los parametros del nodo "Cobranza
          Pagadas" del arbol: Mac31 lo arma a mano en VerReporteDetalle
          (ReporteEmpleadoAsistenciaRecibos.cs:152-218) con otras fechas —las dos
          de `dbo.Constantes`, no las de la pantalla— y con los empleados
          marcados en Param13, que el nodo del arbol deja vacio.

          Por eso tiene clave propia y se desvia por la CLAVE y no por el id: el
          id es el mismo reporte, el juego de parametros no.
        */
        if (string.Equals(request.ReporteKey?.Trim(), "nomina_recibos_detalle_zara", StringComparison.OrdinalIgnoreCase))
            return await BuildReciboDetalleZaraLegacyParamsAsync(conn, request, ct);

        switch (idReporte)
        {
            case ReporteVentaAcumuladores:
            case ReporteVentaProductos:
                return await BuildLegacyParamsAsync(conn, idReporte, request, ct);

            case ReporteVentasRemisiones:
            case ReporteVentasRemisionesSaldoFavor:
            case ReporteVentasFoliosGlobal:
            case ReporteVentasFoliosDineroUsados:
                return await BuildDocumentosVentaLegacyParamsAsync(
                    conn, idReporte, request, request.EstatusFolio, request.IDUsuarios, ct);

            case ReporteVentasFacturas:
                return await BuildDocumentosVentaLegacyParamsAsync(
                    conn, idReporte, request, request.EstatusFolio, request.IDUsuarios, ct,
                    formato: 0, tipo: ResolveTipoFactura(request.TipoFactura));

            case ReporteVentasConcentradosDetalle:
                return BuildConcentradosLegacyParams(request, request.IDRepartidores);

            case ReporteVentasCobranzaDetallado:
            case ReporteVentasCobranzaDetalladoZara:
                return await BuildCobranzaLegacyParamsAsync(conn, request, request.CobranzaStatus, ct);

            case ReporteCobro:
                return BuildCobroLegacyParams(request);

            case ReporteConcentrado:
                return BuildConcentradoLegacyParams(request);
        }

        var idCliente = await ResolveClienteLegacyAsync(conn, request, ct);
        var idsCliente = JoinIds(request.IDClientes);
        if (string.IsNullOrWhiteSpace(idsCliente))
            idsCliente = idCliente.ToString();

        var idsEmpresa = JoinIds(request.IDEmpresas);
        var idsAgente = JoinIds(request.IDAgentes);
        var presentacion = ResolvePresentacion(request.Salida).ToString();
        var fechaInicial = request.FechaInicial.ToString("yyyy-MM-dd");
        var fechaFinal = request.FechaFinal.ToString("yyyy-MM-dd");
        var formato = ResolveFormatoDetallado(request.Formato) ? "1" : "0";

        var legacy = new LegacyReportParams
        {
            NombreEquipo = ResolveLegacyNombreEquipo(request),
            IDUsuario = ResolveLegacyIDUsuario(request),
            Param1 = BuildLegacyUsuarioParam(request)
        };

        switch (idReporte)
        {
            /*
              LOS CUATRO DE NOMINA. Su juego de parametros es PROPIO y no se
              parece al generico de ventas: no lleva empresas, ni almacenes, ni
              agentes, ni cliente. Lo dice su #ParamIds, leido en MacZ:

                  Param1 = Equipo[IDUsuario
                  Param2 = IDsEmpleado
                  Param3 = IDPeriodoTipoSueldo
                  Param4 = FechaInicial@00:00:00
                  Param5 = FechaFinal@23:59:59
                  Param6 = Presentacion

              Las fechas van con la hora pegada con arroba, como las manda Mac31
              (ReporteEmpleadoAsistenciaRecibos.cs:234-238): el SP hace
              REPLACE(FechaInicial,'@',' ') para volverlas datetime. Sin la hora,
              el BETWEEN se come el ultimo dia a las 00:00 y falta un dia de
              asistencias — que es el error que no se ve hasta que alguien cuenta.
            */
            case ReporteNominaAsistencias:
            case ReporteNominaAsistenciasCorto:
            case ReporteNominaInasistencias:
            case ReporteNominaVacaciones:
            case ReporteNominaRecibos:
                legacy.Param2 = JoinIdsPorComa(request.IDEmpleados);
                legacy.Param3 = request.IDPeriodoTipoSueldo > 0
                    ? request.IDPeriodoTipoSueldo.ToString(CultureInfo.InvariantCulture)
                    : "0";
                legacy.Param4 = FormatLegacyStartDateTime(request.FechaInicial);
                legacy.Param5 = FormatLegacyEndDateTime(request.FechaFinal);
                /*
                  VACACIONES ES SOLO EXCEL Y SE PIDE ASI.

                  No es una decision de presentacion: `sp_n_rptEmpleadosVacaciones`
                  hace `SELECT @Presentacion = 3` pisando lo que reciba, y lo que
                  devuelve es la tabla #VacacionesExcel. Mac31 tambien lo fuerza
                  (:285). Pedirle "pantalla" daria un PDF con la tabla de Excel
                  dentro, que no es lo que nadie espera.
                */
                legacy.Param6 = idReporte == ReporteNominaVacaciones ? "3" : presentacion;
                break;

            case ReporteVentasMotoBateriasPorCliente:
            case ReporteVentasMotoBateriasPorFolio:
            case ReporteVentasMotoBateriasPorCodigo:
            case ReporteVentasMotoBateriasPorMarca:
                legacy.Param2 = idsEmpresa;
                legacy.Param3 = JoinIds(request.IDAlmacenes);
                legacy.Param4 = idCliente.ToString();
                legacy.Param5 = idsAgente;
                legacy.Param6 = fechaInicial;
                legacy.Param7 = fechaFinal;
                legacy.Param8 = presentacion;
                break;

            case ReporteVentasPagosDetallado:
            case ReporteVentasPagosTotalizado:
            case ReporteVentasPagosDetalladoTauro:
            case ReporteVentasPagosDetalladoCS:
            case ReporteVentasPagosTotalizadoCS:
                legacy.Param2 = idsEmpresa;
                legacy.Param3 = ResolveTiposPago(request);
                legacy.Param4 = idsAgente;
                legacy.Param5 = idCliente.ToString();
                legacy.Param6 = fechaInicial;
                legacy.Param7 = fechaFinal;
                legacy.Param8 = "0";
                legacy.Param9 = formato;
                legacy.Param10 = "0";
                legacy.Param11 = presentacion;
                legacy.Param12 = request.PagosExcedentes ? "3" : "0";
                break;

            case ReporteVentasPagosDescuentos:
                legacy.Param2 = idsEmpresa;
                legacy.Param3 = "12";
                legacy.Param4 = idsAgente;
                legacy.Param5 = idCliente.ToString();
                legacy.Param6 = fechaInicial;
                legacy.Param7 = fechaFinal;
                legacy.Param8 = "0";
                legacy.Param9 = "1";
                legacy.Param10 = presentacion;
                break;

            case ReporteVentasPagosTransferenciasDuplicadas:
                legacy.Param2 = idsEmpresa;
                legacy.Param3 = idsAgente;
                legacy.Param4 = fechaInicial;
                legacy.Param5 = fechaFinal;
                legacy.Param6 = presentacion;
                break;

            case ReporteVentasCobranzaZaragoza:
                legacy.Param2 = idsEmpresa;
                legacy.Param3 = idCliente.ToString();
                legacy.Param4 = idsAgente;
                legacy.Param5 = "0";
                legacy.Param6 = "4";
                legacy.Param7 = "1";
                legacy.Param8 = FormatLegacyStartDateTime(request.FechaInicial);
                legacy.Param9 = FormatLegacyEndDateTime(request.FechaFinal);
                legacy.Param10 = presentacion;
                legacy.Param11 = idsCliente;
                legacy.Param12 = request.FiltrarFechas ? "1" : "0";
                legacy.Param13 = "";
                break;

            case ReporteVentasCobranzaPagadasDetalladoZara:
            case ReporteVentasCobranzaPagadasTotalizadoZara:
                legacy.Param2 = idsEmpresa;
                legacy.Param3 = idCliente.ToString();
                legacy.Param4 = idsAgente;
                legacy.Param5 = "0";
                legacy.Param6 = "2";
                legacy.Param7 = formato;
                legacy.Param8 = FormatLegacyStartDateTime(request.FechaInicial);
                legacy.Param9 = FormatLegacyEndDateTime(request.FechaFinal);
                legacy.Param10 = presentacion;
                legacy.Param11 = idsCliente;
                legacy.Param12 = request.FiltrarFechas ? "1" : "0";
                legacy.Param13 = "";
                break;

            case ReporteVentasCobranzaDesglosado:
                legacy.Param2 = idsEmpresa;
                legacy.Param3 = idCliente.ToString();
                legacy.Param4 = idsAgente;
                legacy.Param5 = "1";
                legacy.Param6 = presentacion;
                break;

            case ReporteTransferenciasClientes:
                legacy.Param2 = "0";
                legacy.Param3 = "0";
                legacy.Param4 = idsCliente;
                legacy.Param5 = "";
                legacy.Param6 = "0";
                legacy.Param7 = "0";
                legacy.Param8 = "0";
                legacy.Param9 = "0";
                legacy.Param10 = fechaInicial;
                legacy.Param11 = fechaFinal;
                legacy.Param12 = presentacion;
                break;

            case ReporteTransferenciasEstatus:
                legacy.Param2 = "0";
                legacy.Param3 = ResolveTransferenciaEstatus(request.TransferenciaEstatus).ToString();
                legacy.Param4 = idsCliente;
                legacy.Param5 = "";
                legacy.Param6 = "0";
                legacy.Param7 = "0";
                legacy.Param8 = "0";
                legacy.Param9 = "0";
                legacy.Param10 = fechaInicial;
                legacy.Param11 = fechaFinal;
                legacy.Param12 = presentacion;
                break;

            case ReporteTransferenciasFolios:
                legacy.Param2 = "0";
                legacy.Param3 = string.IsNullOrWhiteSpace(idsEmpresa) ? "1" : idsEmpresa;
                legacy.Param4 = idsCliente;
                legacy.Param5 = "0";
                legacy.Param6 = fechaInicial;
                legacy.Param7 = fechaFinal;
                legacy.Param8 = presentacion;
                break;

            case ReporteVentasPagosComparativo:
            case ReporteClientesConSinCompra:
            case ReporteClientesConCompra:
            case ReporteClientesSinCompra:
            case ReporteClientesFacturasRFC:
            case ReporteClientesAcumMotoLub:
            case ReporteClientesAcumMotoLubMensual:
            case ReporteClientesAcumMotoLubMensualCategoria:
            case ReporteClientesAcumMotoLubClarios:
                legacy.Param2 = idsEmpresa;
                legacy.Param3 = fechaInicial;
                legacy.Param4 = fechaFinal;
                legacy.Param5 = idsAgente;
                legacy.Param6 = idCliente.ToString();
                legacy.Param7 = presentacion;
                /*
                  LA CATEGORIA DE ESTOS TRES ES UN INDICE DE COMBO (0..3), NO UN
                  IDGrupoCategoria: lo decide el propio SP, que solo tiene ramas
                  para 0, 1, 2 y 3. Ver ResolveCategoriaCortaLegacy, que es
                  quien lo resuelve y donde esta apuntada la trampa.
                */
                if (idReporte == ReporteClientesAcumMotoLubClarios)
                {
                    /* El 121 no tiene renglon "todas": su combo arranca en
                       ACUMULADORES y Mac31 manda SelectedIndex + 1, o sea 1..3.
                       Por eso el 0 se trata como 1 y no se deja pasar. */
                    var cortaClarios = ResolveCategoriaCortaLegacy(request);
                    legacy.Param8 = (cortaClarios == 0 ? 1 : cortaClarios).ToString(CultureInfo.InvariantCulture);
                    legacy.Param9 = "2025";
                }
                // El mensual pide la categoria en Param8: 0 = todas (reporte 118),
                // 1 acumuladores / 2 moto / 3 lubricantes (reporte 119).
                if (idReporte == ReporteClientesAcumMotoLubMensual)
                    legacy.Param8 = "0";
                if (idReporte == ReporteClientesAcumMotoLubMensualCategoria)
                    legacy.Param8 = ResolveCategoriaCortaLegacy(request).ToString(CultureInfo.InvariantCulture);
                break;

            case ReporteClientesGlobal:
                legacy.Param2 = idsEmpresa;
                legacy.Param3 = idsAgente;
                legacy.Param4 = idCliente.ToString();
                legacy.Param5 = presentacion;
                break;

            case ReporteClientesDescuentos:
            case ReporteClientesDescuentosSin:
                legacy.Param2 = "1";
                legacy.Param3 = request.IDGrupoCategoria > 0 ? request.IDGrupoCategoria.ToString() : GrupoCategoriaAcumuladores.ToString();
                legacy.Param4 = idsAgente;
                legacy.Param5 = idCliente.ToString();
                legacy.Param6 = idReporte == ReporteClientesDescuentos ? "1" : "0";
                legacy.Param7 = presentacion;
                break;

            case ReporteClientesConCompraPorDia:
                legacy.Param2 = idsEmpresa;
                legacy.Param3 = fechaInicial;
                legacy.Param4 = fechaFinal;
                legacy.Param5 = idsAgente;
                legacy.Param6 = JoinIds(request.IDDiasSemana);
                legacy.Param7 = idCliente.ToString();
                legacy.Param8 = presentacion;
                break;

            case ReporteVentasUtilidadAcumuladores:
            case ReporteVentasUtilidadProductos:
                legacy.Param2 = idsEmpresa;
                legacy.Param3 = (request.IDGrupoCategoria > 0 ? request.IDGrupoCategoria : GrupoCategoriaAcumuladores).ToString();
                legacy.Param4 = JoinIds(request.IDSubcategorias);
                legacy.Param5 = JoinIds(request.IDMarcas);
                legacy.Param6 = JoinIds(request.IDAlmacenes);
                legacy.Param7 = "0";
                legacy.Param8 = idCliente.ToString();
                legacy.Param9 = idsAgente;
                legacy.Param10 = fechaInicial;
                legacy.Param11 = fechaFinal;
                legacy.Param12 = request.Gastos.ToString(CultureInfo.InvariantCulture);
                legacy.Param13 = presentacion;
                break;

            case ReporteCompraAcumuladores:
            case ReporteCompraProductos:
                legacy.Param2 = idsEmpresa;
                legacy.Param3 = (request.IDGrupoCategoria > 0 ? request.IDGrupoCategoria : GrupoCategoriaAcumuladores).ToString();
                legacy.Param4 = JoinIds(request.IDSubcategorias);
                legacy.Param5 = JoinIds(request.IDMarcas);
                legacy.Param6 = JoinIds(request.IDAlmacenes);
                legacy.Param7 = request.IDProveedor.ToString(CultureInfo.InvariantCulture);
                legacy.Param8 = fechaInicial;
                legacy.Param9 = fechaFinal;
                legacy.Param10 = presentacion;
                break;

            case ReporteComprasFacturas:
                legacy.Param2 = idsEmpresa;
                legacy.Param3 = JoinIds(request.IDAlmacenes);
                legacy.Param4 = request.IDProveedor.ToString(CultureInfo.InvariantCulture);
                legacy.Param5 = "0";
                legacy.Param6 = ResolveComprasFacturaStatus(request.EstatusComprasFacturas).ToString(CultureInfo.InvariantCulture);
                if (ResolveComprasTipoFecha(request.TipoFechaCompras) == "factura")
                {
                    legacy.Param9 = fechaInicial;
                    legacy.Param10 = fechaFinal;
                }
                else
                {
                    legacy.Param7 = fechaInicial;
                    legacy.Param8 = fechaFinal;
                }
                legacy.Param11 = "";
                legacy.Param12 = "";
                legacy.Param13 = presentacion;
                break;

            case ReporteInventarioAcumuladores:
            case ReporteInventarioProductos:
            case ReporteInventarioFiltros:
            case ReporteInventarioAcumuladoresCosto:
            case ReporteInventarioProductosCosto:
            case ReporteInventarioFiltrosCosto:
            case ReportePedidoAcumuladores:
            case ReportePedidoProductos:
                var constants = await ConsultarConstantesAsync(conn, ct);
                legacy.Param2 = (request.IDGrupoCategoria > 0 ? request.IDGrupoCategoria : GrupoCategoriaAcumuladores).ToString();
                legacy.Param3 = JoinIds(request.IDSubcategorias);
                legacy.Param4 = JoinIds(request.IDMarcas);
                legacy.Param5 = JoinIds(request.IDAlmacenes);
                legacy.Param6 = constants.IDDescuentoCompra.ToString(CultureInfo.InvariantCulture);
                legacy.Param7 = presentacion;
                legacy.Param8 = request.InventarioHistorico ? fechaInicial : "";
                break;

            case ReporteInventarioFaltanteAcumuladores:
            case ReporteInventarioFaltanteProductos:
            case ReporteInventarioFaltanteFiltros:
                legacy.Param2 = (request.IDGrupoCategoria > 0 ? request.IDGrupoCategoria : GrupoCategoriaAcumuladores).ToString();
                legacy.Param3 = JoinIdsPreserveOrder(request.IDSubcategorias);
                legacy.Param4 = JoinIds(request.IDMarcas);
                legacy.Param5 = JoinIds(request.IDAlmacenes);
                legacy.Param6 = idsEmpresa;
                legacy.Param7 = presentacion;
                legacy.Param8 = FormatLegacyStartDateTime(request.FechaInicial);
                legacy.Param9 = FormatLegacyEndDateTime(request.FechaFinal);
                break;

            case ReporteAjustesInventario:
                var ajustesTodos = request.IDGrupoCategoria <= 0;
                legacy.Param2 = ajustesTodos ? "0" : request.IDGrupoCategoria.ToString();
                legacy.Param3 = ajustesTodos ? "" : JoinIds(request.IDSubcategorias);
                legacy.Param4 = ajustesTodos ? "" : JoinIds(request.IDMarcas);
                legacy.Param5 = JoinIds(request.IDAlmacenes);
                legacy.Param6 = FormatLegacyStartDateTime(request.FechaInicial);
                legacy.Param7 = FormatLegacyEndDateTime(request.FechaFinal);
                legacy.Param8 = presentacion;
                break;

            case ReporteMovimientosAlmacen:
                legacy.Param2 = idsEmpresa;
                legacy.Param3 = (request.IDGrupoCategoria > 0 ? request.IDGrupoCategoria : GrupoCategoriaAcumuladores).ToString();
                legacy.Param4 = JoinIds(request.IDSubcategorias);
                legacy.Param5 = JoinIds(request.IDMarcas);
                legacy.Param6 = "";
                legacy.Param7 = "0";
                legacy.Param8 = "0";
                legacy.Param9 = idsAgente;
                legacy.Param10 = fechaInicial;
                legacy.Param11 = fechaFinal;
                legacy.Param12 = presentacion;
                break;

            case ReporteVentasCascosExcedentes:
                legacy.Param2 = idsEmpresa;
                legacy.Param3 = idCliente.ToString();
                legacy.Param4 = idsAgente;
                legacy.Param5 = fechaInicial;
                legacy.Param6 = fechaFinal;
                legacy.Param7 = presentacion;
                legacy.Param8 = JoinIds(request.IDUsuarios);
                legacy.Param9 = formato;
                break;

            case ReporteVentasUsadosCreditos:
                legacy.Param2 = idsEmpresa;
                legacy.Param3 = fechaInicial;
                legacy.Param4 = fechaFinal;
                legacy.Param5 = idsAgente;
                legacy.Param6 = idCliente.ToString();
                legacy.Param7 = "0";
                legacy.Param8 = "2";
                legacy.Param9 = presentacion;
                break;

            case ReporteVentasLiquidaciones:
                legacy.Param2 = fechaInicial;
                legacy.Param3 = fechaFinal;
                legacy.Param4 = ResolveTipoLiquidacion(request.TipoLiquidacion, request.TipoReporte).ToString();
                legacy.Param5 = JoinIds(request.IDRepartidores);
                legacy.Param6 = presentacion;
                break;

            case ReporteVentasCobrosDinero:
            case ReporteVentasCobrosCascos:
                legacy.Param2 = fechaInicial;
                legacy.Param3 = fechaFinal;
                legacy.Param4 = idReporte == ReporteVentasCobrosCascos ? "2" : "1";
                legacy.Param5 = "1";
                legacy.Param6 = presentacion;
                break;

            /*
              EL ESTADO DE CUENTA ES DE UN CLIENTE, Y POR ESO SE EXIGE UNO.

              sp_n_rptVentasEstadoCuenta ignora las fechas que se le pasan: trae
              TODAS las ventas desde 2024 ("La Fecha Inicial es SIEMPRE desde que
              se empezo") y luego las recorre con cursores anidados. Lo unico que
              recorta el trabajo es el filtro de cliente (@IDCliente <> 0).

              Medido en Produccion_svr: con un cliente concreto el reporte sale en
              3.5 s; sin cliente el SELECT seguia vivo pasados los 400 s, y Azure
              corta la peticion HTTP a los 230, asi que en produccion eso es un 502
              fijo. El SP es de legacy y no se toca; lo que se arregla es la
              llamada, y se arregla pidiendo lo mismo que pide Mac31, donde la
              forma del Estado de Cuenta FUERZA "por cliente" y esconde las otras
              dos opciones (ReportesMenu.cs:1378-1381).

              Se rebota con InvalidOperationException, que el controlador traduce a
              400: es mejor decir que falta el cliente que dejar al usuario
              esperando siete minutos un 502.
            */
            case ReporteVentasEstadoCuenta:
            case ReporteVentasEstadoCuentaDinero:
            case ReporteVentasEstadoCuentaCascos:
                if (idCliente <= 0)
                    throw new InvalidOperationException("El estado de cuenta es de un cliente: elige uno.");
                legacy.Param2 = idsEmpresa;
                legacy.Param3 = idCliente.ToString();
                legacy.Param4 = idsAgente;
                legacy.Param5 = fechaInicial;
                legacy.Param6 = fechaFinal;
                legacy.Param7 = ResolveFormatoEstadoCuenta(request.FormatoEstadoCuenta);
                legacy.Param8 = presentacion;
                legacy.Param9 = idsCliente;
                break;

            /*
              LOS CENTROS SON DOS FORMAS, NO UNA.

              El 1445 (sp_n_rptEntradasSalidasCentrosGlobales) mete IDsCentro en
              Param4 y corre la presentacion a Param5. El 1410, el 1415 y el 1420
              no filtran por centro: su #ParamIds declara Param4 como Presentacion
              (int). Mac31 les manda el MISMO renglon que al 1445, asi que alla la
              presentacion se les pierde y solo salen "a pantalla" por accidente
              (IDsCentro vacio convierte a 0, que es Pantalla). Aqui se les manda
              lo que su SP pide, y el Excel de los tres funciona.
            */
            case ReporteCentrosRemisiones:
            case ReporteCentrosPagosEntradasSalidas:
            case ReporteCentrosGarantias:
                legacy.Param2 = fechaInicial;
                legacy.Param3 = fechaFinal;
                legacy.Param4 = presentacion;
                break;

            case ReporteCentrosCompleto:
                legacy.Param2 = fechaInicial;
                legacy.Param3 = fechaFinal;
                legacy.Param4 = JoinIds(request.IDCentros);
                legacy.Param5 = presentacion;
                break;

            case ReporteHojaCobroTotalCobradoDetallado:
            case ReporteHojaCobroTotalCobradoTotalizado:
            case ReporteHojaCobroTotalCascos:
            case ReporteHojaCobroCheques:
                legacy.Param2 = "0";
                legacy.Param3 = "0";
                legacy.Param4 = "0";
                legacy.Param5 = "0";
                legacy.Param6 = "0";
                legacy.Param7 = fechaInicial;
                legacy.Param8 = fechaFinal;
                legacy.Param9 = presentacion;
                legacy.Param10 = formato;
                break;

            /*
              1236 Registro de Garantias. No es el molde del 1235: su SP
              (sp_n_rptConsultarGarantiasRegistradas) solo pide productos y fechas.
              Nota de herencia: el nodo que lo abria en Mac31 esta COMENTADO
              (ReportesMenu.cs:808-809), y su case de parametros tampoco existe
              alla, asi que hoy este reporte no sale en Mac31 aunque su SP y sus
              plantillas esten completos.
            */
            case ReporteGarantiasRegistradas:
                legacy.Param2 = JoinIds(request.IDProductos);
                legacy.Param3 = FormatLegacyStartDateTime(request.FechaInicial);
                legacy.Param4 = FormatLegacyEndDateTime(request.FechaFinal);
                legacy.Param5 = presentacion;
                break;

            /*
              1250 Control Acumulado de Estimaciones. En Mac31 se imprime desde la
              pantalla de estimaciones del modulo MC, con la estimacion y el
              contrato de la fila seleccionada; desde el arbol de reportes truena,
              porque alla el nodo existe y el case de parametros no
              (ReportesMenu.cs:966 sin case -> Param2..4 nulos -> "PARAMETROS NULL").
            */
            case ReporteControlAcumuladoEstimaciones:
                legacy.Param2 = request.IDContratoEstimacion.ToString(CultureInfo.InvariantCulture);
                legacy.Param3 = request.IDContrato.ToString(CultureInfo.InvariantCulture);
                legacy.Param4 = presentacion;
                break;

            case ReporteGarantias:
                legacy.Param2 = idsCliente;
                legacy.Param3 = JoinIds(request.IDRepartidores);
                legacy.Param4 = JoinIds(request.IDStatusGarantias);
                legacy.Param5 = "";
                legacy.Param6 = fechaInicial;
                legacy.Param7 = fechaFinal;
                legacy.Param8 = idsCliente;
                legacy.Param9 = presentacion;
                break;

            case ReporteGasolinasDetallado:
            case ReporteGasolinasMensual:
                legacy.Param2 = JoinIds(request.IDAutos);
                legacy.Param3 = JoinIds(request.IDRepartidores);
                legacy.Param4 = idReporte == ReporteGasolinasMensual
                    ? FormatLegacyStartDateTime(new DateTime(request.FechaInicial.Year, request.FechaInicial.Month, 1))
                    : FormatLegacyStartDateTime(request.FechaInicial);
                legacy.Param5 = idReporte == ReporteGasolinasMensual
                    ? FormatLegacyStartDateTime(new DateTime(request.FechaInicial.Year, request.FechaInicial.Month, 1).AddMonths(1).AddDays(-1))
                    : FormatLegacyEndDateTime(request.FechaFinal);
                legacy.Param6 = presentacion;
                legacy.Param7 = idReporte == ReporteGasolinasMensual ? "0" : "1";
                break;

            case ReporteGastosDetallado:
            case ReporteGastosTotalizado:
                legacy.Param2 = FormatLegacyStartDateTime(request.FechaInicial);
                legacy.Param3 = FormatLegacyEndDateTime(request.FechaFinal);
                legacy.Param4 = presentacion;
                legacy.Param5 = idReporte == ReporteGastosTotalizado ? "0" : "1";
                legacy.Param6 = request.IDTipoGasto > 0 ? request.IDTipoGasto.ToString(CultureInfo.InvariantCulture) : "0";
                break;

            case ReporteCortes:
            case ReporteAccesosMac:
                legacy.Param2 = fechaInicial;
                legacy.Param3 = fechaFinal;
                legacy.Param4 = presentacion;
                break;

            /*
              CORTE DIARIO (1315): UN CORTE, DOS PARAMETROS.

              sp_n_rptCorteDiario, leido de MacZ, nombra las posiciones de su
              #ParamIds `IDCorte` y `Presentacion`, asi que el orden no se
              supone. Las fechas que pide el DTO generico se ignoran aqui a
              proposito -el reporte es de UN corte, no de un rango-, igual que
              en Autos y en Cambios de Vigencia.

              EL 0 SE RECHAZA AQUI. El SP solo se protege de NULL
              (`IF @IDCorte IS NULL`), asi que con 0 corre los JOIN completos y
              devuelve cero renglones: un PDF en blanco en vez de un error.
            */
            case ReporteCorteDiario:
                if (request.IDCorte <= 0)
                    throw new ArgumentException("El reporte de corte diario necesita el idCorte del corte a imprimir.");
                legacy.Param2 = request.IDCorte.ToString(CultureInfo.InvariantCulture);
                legacy.Param3 = presentacion;
                break;

            /*
              CORTE DIARIO DETALLE MAC (1317): EL MISMO SP QUE EL 1319.

              Los dos son sp_n_rptVentasPagosLiquidacion con los MISMOS seis
              parametros; lo que cambia es la plantilla. Mac31 lo dispara desde
              VER DETALLE MAC del formulario del corte y manda LA FECHA DEL
              CORTE EN LAS DOS FECHAS, TipoLiquidacion "0" y la lista de
              repartidores vacia (CorteDiario.cs:1208-1215): o sea "todo el MAC
              de ese dia", camioneta y agentes juntos.

              Por eso aqui no se lee ni TipoLiquidacion ni IDRepartidores aunque
              el DTO los traiga: dejar que el cliente los mande convertiria este
              reporte en otro distinto del que imprime Mac31, y el papel ya no
              cuadraria con los tres importes de MAC que el corte muestra
              arriba. Quien quiera filtrar por repartidor tiene el 1319, que es
              el nodo "Liquidaciones" del arbol.
            */
            case ReporteCorteDiarioDetalleMac:
                legacy.Param2 = fechaInicial;
                legacy.Param3 = fechaFinal;
                legacy.Param4 = "0";
                legacy.Param5 = "";
                legacy.Param6 = presentacion;
                break;

            /*
              CORTE DE CENTRO DE SERVICIO (1345): ID, TIPO DE RETIRO Y LA FECHA
              FINAL CON HORA.

              sp_n_rptCortesCS nombra sus posiciones `IDCorte`, `IDTipoRetiro`,
              `FechaFinal` y `Presentacion`.

              LA FECHA VA CON ARROBA, NO CON ESPACIO, y esto es lo unico raro
              del reporte: el SP hace `REPLACE(FechaFinal, '@', ' ')` antes de
              usarla y `SUBSTRING(FechaFinal, 1, 10)` para mostrarla. Mac31 la
              pega con DELIMITADOR_PARAM_ARROBA_SP
              (ConsultarCortesCS.cs:725-726). Si viajara con un espacio, el
              parametro se partiria al leerlo y la hora se perderia: el corte
              saldria cortado a las 00:00 y sin los cobros del dia.

              SE FUERZA 23:59:59 como alla: el corte de retiro es del DIA
              completo, y los dos sitios de Mac31 que lo imprimen pegan esa hora
              a mano en vez de dejarla al reloj.
            */
            case ReporteCorteCentroServicio:
                if (request.IDCorte <= 0)
                    throw new ArgumentException("El reporte de corte de centro de servicio necesita el idCorte del corte a imprimir.");
                legacy.Param2 = request.IDCorte.ToString(CultureInfo.InvariantCulture);
                legacy.Param3 = request.IDTipoRetiro.ToString(CultureInfo.InvariantCulture);
                legacy.Param4 = request.FechaFinal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "@23:59:59";
                legacy.Param5 = presentacion;
                break;

            /*
              1255 y 1256: UN MOVIMIENTO, DOS PARAMETROS.

              -- Param 1 = Equipo [ IDUsuario
              -- Param 2 = IDUsadoMovimiento
              -- Param 3 = Presentacion

              Verificado leyendo el `#ParamIds` del propio
              `sp_n_rptUsadosMovimientos`, que nombra la segunda posicion
              `IDUsadoMovimiento` y la tercera `Presentacion` — no se dio por
              buenos los nombres solo porque Mac31 los mande en ese orden.

              SI EL ID VIENE EN 0 SE RECHAZA AQUI. El SP no dice "sin
              informacion": devuelve cero renglones y el motor arma un
              comprobante en blanco con el logo y la fecha de impresion, que es
              peor que un error porque parece un comprobante.
            */
            case ReporteComprobanteCascos:
            case ReporteVentaCascos:
                if (request.IDUsadoMovimiento <= 0)
                    throw new ArgumentException("El comprobante de cascos necesita el idUsadoMovimiento del movimiento a imprimir.");
                legacy.Param2 = request.IDUsadoMovimiento.ToString(CultureInfo.InvariantCulture);
                legacy.Param3 = presentacion;
                break;

            /*
              1260: EL DIARIO DE TAURO. Param2 es una FECHA, no un id.

              -- Param 1 = Equipo [ IDUsuario
              -- Param 2 = FechaInicial 'MM-dd-yyyy@00:00:00'
              -- Param 3 = Presentacion

              La hora va pegada con '@' y no con un espacio porque el SP parte
              el parametro por el delimitador; con un espacio la fecha se
              quedaria a medias. `FormatLegacyStartDateTime` ya emite
              exactamente el formato de `Constantes.FormatoFechaDB`
              ('MM-dd-yyyy' en las dos empresas, comprobado en la base), que es
              el que Mac31 usa en ConsultarUsadosMovimientos.cs:254.

              Aqui NO se exige el idUsadoMovimiento: pedirlo seria un error de
              lectura del reporte. Este imprime el dia entero.
            */
            case ReporteComprobanteCascosTauro:
                legacy.Param2 = FormatLegacyStartDateTime(request.FechaInicial);
                legacy.Param3 = presentacion;
                break;

            case ReporteAsistenciasZaragoza:
                legacy.Param2 = "";
                legacy.Param3 = FormatLegacyStartDateTime(request.FechaInicial);
                legacy.Param4 = FormatLegacyEndDateTime(request.FechaFinal);
                legacy.Param5 = presentacion;
                break;

            case ReporteVentasRemisionesImpuestos:
                legacy.Param2 = idsEmpresa;
                legacy.Param3 = JoinIds(request.IDAlmacenes);
                legacy.Param4 = ResolveTipoDocumento(request.Documento).ToString();
                legacy.Param5 = idCliente.ToString();
                legacy.Param6 = idsAgente;
                legacy.Param7 = ResolveStatusFolio(request.EstatusFolio).ToString();
                legacy.Param8 = fechaInicial;
                legacy.Param9 = fechaFinal;
                legacy.Param10 = presentacion;
                legacy.Param11 = JoinIds(request.IDUsuarios);
                legacy.Param12 = "1";
                legacy.Param13 = "0";
                break;

            case ReporteListasPreciosZaragoza:
            case ReporteListasPreciosProductosZaragoza:
            case ReporteListasPreciosTauroTodas:
            case ReporteListasPreciosTauroTodasMenosTauro:
            case ReporteListasPreciosTauroCroGonXtLthClAmFpDy:
            case ReporteListasPreciosTauroFpAmLth:
                var listaConstants = await ConsultarConstantesAsync(conn, ct);
                var esListaCosto = (request.ReporteKey ?? "").Trim().Equals("listas_de_precios_imp", StringComparison.OrdinalIgnoreCase);
                legacy.Param2 = "0";
                legacy.Param3 = "0";
                legacy.Param4 = "0";
                legacy.Param5 = (esListaCosto ? listaConstants.IDDescuentoCompra : listaConstants.IDDescuentoListaPrecios).ToString(CultureInfo.InvariantCulture);
                legacy.Param6 = presentacion;
                legacy.Param7 = request.Gastos.ToString(CultureInfo.InvariantCulture);
                legacy.Param8 = request.Redondear ? "1" : "0";
                legacy.Param9 = "1";
                legacy.Param10 = (request.IDGrupoCategoria > 0 ? request.IDGrupoCategoria : GrupoCategoriaAcumuladores).ToString();
                legacy.Param11 = JoinIds(request.IDSubcategorias);
                legacy.Param12 = JoinIds(request.IDMarcas);
                legacy.Param13 = (request.Comentarios ?? "").Trim().Replace(Environment.NewLine, "<br/>");
                legacy.Param14 = request.IncluirDescuentos ? "1" : "0";
                legacy.Param15 = (listaConstants.IDEmpresaCS > 0 ? listaConstants.IDEmpresaCS : 1).ToString(CultureInfo.InvariantCulture);
                legacy.Param16 = "0";
                // Param17 = tipo de lista. Lo leen las cuatro listas de Tauro y es
                // el mismo numero con el que se eligio el IDReporte: si aqui va "1"
                // fijo, el 1244 imprime el encabezado de "todas menos Tauro" con
                // los datos de "todas".
                legacy.Param17 = Math.Clamp(request.TipoLista <= 0 ? 1 : request.TipoLista, 1, 4)
                    .ToString(CultureInfo.InvariantCulture);
                break;

            /*
              AUTOS: EL UNICO REPORTE SIN FILTROS, Y POR ESO NO HACE FALTA NADA MAS.

              Mac31 lo dispara desde el boton IMPRIMIR de ConsultarAutos con dos
              parametros y nada mas: Param1 es el usuario y el equipo -que lo pone
              BuildLegacyUsuarioParam, igual que en los otros cincuenta y tres- y
              Param2 es la presentacion (pantalla o Excel). No hay fechas, ni
              empresa, ni almacen: el reporte es la flotilla entera.

              Las fechas que exige el endpoint generico se ignoran aqui a
              proposito; se piden arriba porque el DTO es el de ventas, y se
              mandan por no inventar un segundo contrato para un solo reporte.
            */
            case ReporteAutos:
                legacy.Param2 = presentacion;
                break;

            /*
              CAMBIOS DE VIGENCIA (1535): UN DOCUMENTO, DOS PARAMETROS.

              El propio sp_n_rptCambiosVigencia los enumera arriba del CREATE
              TABLE #ParamIds, leido de la base:

                  -- Param 1 =  Equipo ~ IDUsuario
                  -- Param 2 =  IDCambioVigencia
                  -- Param 3 =  Presentacion

              y su #ParamIds nombra esas dos posiciones `IDCambioVigencia` y
              `Presentacion` en vez de Param2 y Param3, asi que el orden no es
              una suposicion.

              SI EL ID VIENE EN 0, EL PROCEDIMIENTO NO DICE "SIN INFORMACION":
              hace `IF @IDCambioVigencia IS NULL` y si no es nulo sigue adelante,
              de modo que con 0 corre el JOIN completo, no encuentra nada y
              devuelve cero renglones; peor, antes de eso ya intento sellar la
              impresion del cambio de vigencia 0. Se rechaza aqui, con mensaje.

              Las fechas que pide el DTO generico se ignoran, igual que en Autos:
              este reporte no tiene rango.
            */
            case ReporteCambiosVigencia:
                if (request.IDCambioVigencia <= 0)
                    throw new ArgumentException("El reporte de cambios de vigencia necesita el idCambioVigencia del documento a imprimir.");
                legacy.Param2 = request.IDCambioVigencia.ToString(CultureInfo.InvariantCulture);
                legacy.Param3 = presentacion;
                break;

            /*
              LIQUIDACIONES DE DESCUENTO (1030): LOS FOLIOS SELECCIONADOS.

              Los once parametros, enumerados por el propio
              sp_n_rptVentasDescuentosAjustes arriba de su #ParamIds (leido de
              MacZ), que ademas nombra cada posicion en vez de decir ParamN:

                Param2 IDVentasDescuentosAjustes   Param3 IDTiposLiquidaciones
                Param4 IDsVenta                    Param5 IDsCliente
                Param6 IDsAgente                   Param7 FolioInicial
                Param8 FolioFinal                  Param9 FechaInicial
                Param10 FechaFinal                 Param11 Presentacion

              EL BOTON IMPRIMIR DE MAC31 MANDA SOLO EL PARAM2 Y PARAM11; los otros
              nueve van vacios o en 0 (ConsultarLiquidacionesDescuentos.cs:498-531,
              donde IDTiposLiquidaciones, IDVentas, IDClientes, IDAgentes,
              FechaInicial y FechaFinal se declaran "" y los folios 0, y nada se
              les asigna despues). Se copia eso: el reporte es de lo marcado en la
              rejilla, no del filtro de la busqueda. Sus fechas NO son el rango del
              reporte y mandarlas CAMBIARIA el papel, asi que no se mandan aunque
              el DTO generico las pida.

              LA LISTA SE UNE CON '[' Y NO CON '~', Y ESO NO ES UN DESCUIDO.
              Mac31 la junta con DELIMITADOR_PARAM_CORCHETE_SP ('[',
              ConsultarLiquidacionesDescuentos.cs:470) y el procedimiento hace
              REPLACE(IDVentasDescuentosAjustes, '[', '~') antes de pasarla a
              fn_DevuelveCadenaEnTabla. Si se manda con '~' directamente, el
              REPLACE no encuentra nada y la cadena llega igual, asi que esta
              funciona por casualidad -- pero el '[' es lo que esta escrito en los
              dos lados, y con el la cadena guardada en Parametros es la misma que
              deja Mac31, que es lo que se puede comparar renglon por renglon.

              SI NO HAY FOLIOS SELECCIONADOS NO SE IMPRIME, igual que Mac31, que
              dice "SELECCIONE UN FOLIO" y no llama al reporte. Con la lista vacia
              el procedimiento no filtra por folio y el papel saldria con TODAS las
              liquidaciones de la base.
            */
            case ReporteVentasDescuentosAjustes:
                var folios = (request.IDLiquidacionesDescuento ?? new List<int>())
                    .Where(x => x > 0)
                    .Distinct()
                    .Select(x => x.ToString(CultureInfo.InvariantCulture))
                    .ToList();
                if (folios.Count == 0)
                    throw new ArgumentException("El reporte de liquidaciones de descuento se imprime de los folios seleccionados: manda al menos uno en idLiquidacionesDescuento.");
                legacy.Param2 = string.Join("[", folios);
                legacy.Param3 = "";
                legacy.Param4 = "";
                legacy.Param5 = "";
                legacy.Param6 = "";
                legacy.Param7 = "0";
                legacy.Param8 = "0";
                legacy.Param9 = "";
                legacy.Param10 = "";
                legacy.Param11 = presentacion;
                break;

            /*
              COMPROBACION DE COMPRAS (1017): LAS FACTURAS MARCADAS.

              Los tres parametros, copiados de lo que manda Mac31 en
              `MuestraComprobacion` (ConsultarCompras.cs):

                  Param1 = Equipo[IDUsuario   (ya lo pone BuildLegacyUsuarioParam)
                  Param2 = IDsCompra
                  Param3 = Presentacion

              LA LISTA VA UNIDA CON '~' Y NO CON '[' . Mac31 la junta con
              DELIMITADOR_PARAM_TILDE_SP ('~', Globales.cs:79) y le quita el
              ultimo separador antes de mandarla. No es intercambiable con el
              '[' de las liquidaciones: alli el procedimiento hace un REPLACE
              que aqui no existe.

              SIN NADA MARCADO NO SE IMPRIME, igual que Mac31, que contesta
              "SELECCIONE UNA FACTURA" y no llama al reporte. Con la cadena
              vacia el procedimiento no filtra y el papel saldria con todas las
              compras de la base.

              LO QUE AQUI NO SE COMPRUEBA: que ninguna este cancelada. Mac31 lo
              hace en la pantalla, antes de llamar, y el web hace lo mismo en
              ComprasPage: es un aviso de interfaz, y repetirlo aqui obligaria a
              leer las compras otra vez para contestar un 400 que el usuario ya
              vio.
            */
            case ReporteComprasComprobacion:
                var compras = (request.IDsCompra ?? new List<int>())
                    .Where(x => x > 0)
                    .Distinct()
                    .Select(x => x.ToString(CultureInfo.InvariantCulture))
                    .ToList();
                if (compras.Count == 0)
                    throw new ArgumentException("La comprobación de compras se imprime de las facturas marcadas: manda al menos una en idsCompra.");
                legacy.Param2 = string.Join("~", compras);
                legacy.Param3 = presentacion;
                break;

            /*
              EL DOCUMENTO DE UNA COMPRA (1015): UNA SOLA, LA DEL CURSOR.

              Mac31 manda `dataGridView1.CurrentRow`, no las marcadas
              (ConsultarCompras.cs:702). Y no es un capricho de la pantalla:
              `sp_rptCompraDetalle` filtra con `WHERE C.IDCompra = @IDCompra1`,
              una IGUALDAD contra un varchar, asi que con dos ids unidos por
              '~' SQL Server reventaria al convertir la cadena a entero. Por eso
              si llegan varios se toma el PRIMERO en vez de juntarlos: es lo
              unico que puede imprimir, y es lo que imprime Mac31.

              El id se guarda aqui, en Param2, para que
              ArgumentosDelProcedimiento pueda volver a leerlo cuando el
              navegador pida el papel con `psp=`. Ese es el unico punto en que
              nos separamos de Mac31, que no guarda renglon y lleva el id en la
              URL; la razon esta en la nota del 1015 en Constants.
            */
            case ReporteComprasDetalle:
                var compra = (request.IDsCompra ?? new List<int>()).FirstOrDefault(x => x > 0);
                if (compra == 0)
                    throw new ArgumentException("El documento de la compra se imprime de una factura: manda su id en idsCompra.");
                legacy.Param2 = compra.ToString(CultureInfo.InvariantCulture);
                legacy.Param3 = presentacion;
                break;

            default:
                throw new InvalidOperationException("Reporte de ventas no soportado.");
        }

        return legacy;
    }

    private static int ResolveStatusFolio(string? estatusFolio)
    {
        return (estatusFolio ?? "todos").Trim().ToLowerInvariant() switch
        {
            "vigentes" => 1,
            "cancelados" or "canceladas" => 2,
            "saldo_favor" or "saldo favor" => 4,
            _ => 0
        };
    }

    private static async Task<int> ResolveClienteAsync(
        SqlConnection conn,
        ReportesVentasAcumuladoresProductosRequest request,
        CancellationToken ct)
    {
        if ((request.Salida ?? "").Trim().Equals("excel", StringComparison.OrdinalIgnoreCase))
            return -1;

        if ((request.TipoReporte ?? "").Trim().Equals("cliente", StringComparison.OrdinalIgnoreCase))
        {
            var candidate = (request.IDClientes ?? new List<int>()).FirstOrDefault(id => id > 0);
            if (candidate <= 0) return 0;

            var byId = await ConsultarClientesAsync(conn, null, candidate, ct);
            if (byId.Any(cliente => cliente.IDCliente == candidate))
                return candidate;

            var byNumero = await ConsultarClientesAsync(conn, candidate, null, ct);
            return byNumero.FirstOrDefault()?.IDCliente ?? candidate;
        }

        return 0;
    }

    private static async Task<int> ResolveClienteLegacyAsync(
        SqlConnection conn,
        ReportesVentasAcumuladoresProductosRequest request,
        CancellationToken ct)
    {
        if (!(request.TipoReporte ?? "").Trim().Equals("cliente", StringComparison.OrdinalIgnoreCase))
            return 0;

        var candidate = (request.IDClientes ?? new List<int>()).FirstOrDefault(id => id > 0);
        if (candidate <= 0) return 0;

        var byId = await ConsultarClientesAsync(conn, null, candidate, ct);
        if (byId.Any(cliente => cliente.IDCliente == candidate))
            return candidate;

        var byNumero = await ConsultarClientesAsync(conn, candidate, null, ct);
        return byNumero.FirstOrDefault()?.IDCliente ?? candidate;
    }

    private static bool IsAgentReport(ReportesVentasAcumuladoresProductosRequest request)
    {
        return (request.TipoReporte ?? "").Trim().Equals("agente", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReplaceAgentHeader(string html, DataTable? table, DataRow? row, ReportesVentasAcumuladoresProductosRequest request)
    {
        if (!IsAgentReport(request) || table is null || row is null)
        {
            html = html.Replace("#Agente#", "");
            html = html.Replace("#agente#", "");
            return html;
        }

        var idAgente = ReadString(row, "IDAgente");
        var nombreAgente = ReadString(row, "Nombre Agente", "NombreAgente");
        var agente = ReadString(row, "Agente");
        var textoAgente = "";

        if (!string.IsNullOrWhiteSpace(idAgente) && idAgente != "0")
            textoAgente = string.IsNullOrWhiteSpace(nombreAgente) ? idAgente : $"{idAgente} - {nombreAgente}";
        else if (!string.IsNullOrWhiteSpace(agente) && agente != "0")
            textoAgente = agente;

        var value = string.IsNullOrWhiteSpace(textoAgente) ? "" : $"Agente: <b>{textoAgente}</b>";
        html = html.Replace("#Agente#", value);
        html = html.Replace("#agente#", value);
        return html;
    }

    private static string ReplaceClientHeader(string html, DataTable? table, DataRow? row)
    {
        if (table is null || row is null)
        {
            html = html.Replace("#Cliente#", "");
            html = html.Replace("#cliente#", "");
            return html;
        }

        var idCliente = ReadString(row, "idCliente", "IDCliente");
        var cliente = ReadString(row, "cliente", "Cliente");
        var hasCliente = int.TryParse(idCliente, out var id) && id != 0 && !string.IsNullOrWhiteSpace(cliente);
        var value = hasCliente ? $"Cliente: <b>{cliente}</b>" : "";
        html = html.Replace("#Cliente#", value);
        html = html.Replace("#cliente#", value);
        return html;
    }

    private static int ResolvePresentacion(string? salida)
    {
        return (salida ?? "").Trim().Equals("excel", StringComparison.OrdinalIgnoreCase) ? 3 : 0;
    }

    private static int ResolveTipoDocumento(string? documento)
    {
        return (documento ?? "ventas").Trim().ToLowerInvariant() switch
        {
            "todos" => 0,
            "mayoristas" => 1,
            "locales" => 3,
            "ajustes" => 2,
            "devoluciones" => 4,
            _ => 10
        };
    }

    private static string ResolveDocumento(string? tipoDocumento)
    {
        return (tipoDocumento ?? "").Trim() switch
        {
            "1" => "mayoristas",
            "2" => "ajustes",
            "3" => "locales",
            "4" => "devoluciones",
            _ => "ventas"
        };
    }

    private static string ResolveSalida(string? presentacion)
    {
        return (presentacion ?? "").Trim() == "3" ? "excel" : "pantalla";
    }

    private static string JoinIds(IEnumerable<int>? ids)
    {
        var clean = (ids ?? Enumerable.Empty<int>())
            .Where(x => x > 0)
            .Distinct()
            .Select(x => x.ToString())
            .ToList();

        return clean.Count == 0 ? "" : string.Join("~", clean);
    }

    /*
      LA LISTA DE EMPLEADOS DE NOMINA VA POR COMA, NO POR TILDE.

      Todos los demas reportes separan sus ids con "~" y por eso existe JoinIds.
      Los de nomina NO, y no es una suposicion: su procedimiento lo parte con una
      coma —`fn_DevuelveCadenaEnTabla(@IDsEmpleado, ',')`, linea 306 de
      sp_n_rptAsistenciasGlobales— y Mac31 los arma igual: su propio
      `delimitadorParamValores` vale "," en ESE formulario y solo en ese
      (ReporteEmpleadoAsistenciaRecibos.cs:321).

      Con "~" el procedimiento no se queja de un formato raro: intenta convertir
      la cadena entera a int y revienta con

          Conversion failed when converting the varchar value '34~28~88~91~4'
          to data type int

      que es un 500 "Error de base de datos" y parece un defecto de la base
      cuando lo que esta mal es el separador que mandamos.
    */
    private static string JoinIdsPorComa(IEnumerable<int>? ids)
    {
        var limpios = (ids ?? Enumerable.Empty<int>())
            .Where(x => x > 0)
            .Distinct()
            .Select(x => x.ToString(CultureInfo.InvariantCulture))
            .ToList();

        return limpios.Count == 0 ? "" : string.Join(",", limpios);
    }

    private static string JoinIdsPreserveOrder(IEnumerable<int>? ids)
    {
        var clean = (ids ?? Enumerable.Empty<int>())
            .Where(x => x > 0)
            .Select(x => x.ToString())
            .ToList();

        return clean.Count == 0 ? "" : string.Join("~", clean);
    }

    private static int ResolveCobranzaStatus(string? cobranzaStatus)
    {
        return (cobranzaStatus ?? "pagadas").Trim().ToLowerInvariant() switch
        {
            "vigentes" or "pendientes" => 1,
            "vencidas" => 3,
            "vigentes_vencidas" or "vigentes y vencidas" or "pendientes_vencidas" => 4,
            _ => 2
        };
    }

    private static bool ResolveFormatoDetallado(string? formato)
    {
        return !(formato ?? "detallado").Trim().Equals("totalizado", StringComparison.OrdinalIgnoreCase);
    }

    private static int ResolveTipoPago(string? tipoPago)
    {
        return (tipoPago ?? "todos").Trim().ToLowerInvariant() switch
        {
            "efectivo" => 1,
            "cheque" => 2,
            "descuento" or "descuentos" => 3,
            "transferencia" => 5,
            "cargo" => 7,
            "tarjeta" => 9,
            "deposito_efectivo" or "deposito en efectivo" => 10,
            "excedente_usados" or "excedente de usados" => 11,
            "saldo_dinero" or "saldo dinero" => 13,
            "saldo_cascos" or "saldo cascos" => 14,
            "bonificacion" or "bonificación" => 15,
            "nota_credito" or "nota de credito" or "nota de crédito" => 16,
            "casco_kilo" or "casco kilo" => 17,
            _ => 0
        };
    }

    private static int ResolveComprasFacturaStatus(string? estatus)
    {
        return (estatus ?? "vigentes").Trim().ToLowerInvariant() switch
        {
            "vigentes" => 1,
            "canceladas" or "cancelados" => 2,
            _ => 0
        };
    }

    private static string ResolveComprasFacturaStatusKey(string? estatus)
    {
        return (estatus ?? "").Trim() switch
        {
            "1" => "vigentes",
            "2" => "canceladas",
            _ => "todos"
        };
    }

    private static string ResolveComprasTipoFecha(string? tipoFecha)
    {
        return (tipoFecha ?? "captura").Trim().ToLowerInvariant() switch
        {
            "factura" or "fecha_factura" or "fecha factura" => "factura",
            _ => "captura"
        };
    }

    private static string ResolveTiposPago(ReportesVentasLegacyRequest request)
    {
        if (request.IDTiposPago?.Count > 0)
        {
            return string.Join("|", request.IDTiposPago.Where(id => id != 0).Distinct());
        }

        var raw = (request.TipoPago ?? "").Trim();
        if (raw.Length > 0 && raw.Any(char.IsDigit) && raw.All(ch => char.IsDigit(ch) || ch == '|' || ch == ',' || ch == ';' || char.IsWhiteSpace(ch)))
        {
            return string.Join("|", raw
                .Replace(",", "|", StringComparison.Ordinal)
                .Replace(";", "|", StringComparison.Ordinal)
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => int.TryParse(value, out var id) ? id : 0)
                .Where(id => id != 0)
                .Distinct());
        }

        return ResolveTipoPago(raw).ToString(CultureInfo.InvariantCulture);
    }

    private static int ResolveTransferenciaEstatus(string? transferenciaEstatus)
    {
        return (transferenciaEstatus ?? "todas").Trim().ToLowerInvariant() switch
        {
            "liquidadas" => -2,
            "saldo_pendiente" or "con saldo pendiente" => -3,
            _ => -1
        };
    }

    private static string ResolveLegacyNombreEquipo(ReportesVentasAcumuladoresProductosRequest request)
    {
        return string.IsNullOrWhiteSpace(request.LegacyNombreEquipo)
            ? "WEB"
            : request.LegacyNombreEquipo.Trim();
    }

    private static int ResolveLegacyIDUsuario(ReportesVentasAcumuladoresProductosRequest request)
    {
        return request.LegacyIDUsuario > 0 ? request.LegacyIDUsuario : 0;
    }

    private static string BuildLegacyUsuarioParam(ReportesVentasAcumuladoresProductosRequest request)
    {
        return $"{ResolveLegacyNombreEquipo(request)}[{ResolveLegacyIDUsuario(request)}";
    }

    private static string FormatLegacyStartDateTime(DateTime date)
    {
        return $"{date.ToString("MM-dd-yyyy", CultureInfo.InvariantCulture)}@00:00:00";
    }

    private static string FormatLegacyEndDateTime(DateTime date)
    {
        return $"{date.ToString("MM-dd-yyyy", CultureInfo.InvariantCulture)}@23:59:59";
    }

    /*
      DOS PERSONAS PIDIENDO UN REPORTE A LA VEZ VEIAN EL REPORTE DE LA OTRA.

      `sp_n_ActualizarParametro` inserta el renglon de parametros y despues
      recupera su id asi (lo dice su propia definicion, leida en la base):

          SELECT @IDParametros = MAX(IDParametros) FROM Parametros
          WHERE  NombreEquipo = @NombreEquipo AND @IDUsuario = IDUsuario

      O sea que el id que te devuelve es el ULTIMO renglon de esa pareja
      (NombreEquipo, IDUsuario), no el tuyo. En Mac31 eso funciona porque
      NombreEquipo es el nombre de la maquina y ahi hay una persona sentada
      enfrente. En el web NO: el backend mandaba "WEB" para todos, y para los
      usuarios solo-web, que no tienen id de legacy, IDUsuario es 0. Comprobado en
      Produccion_svr: todos los renglones de Parametros dicen
      NombreEquipo='WEB', IDUsuario=0. La llave era la MISMA para todo el mundo.

      Consecuencia: dos usuarios pidiendo un reporte en el mismo instante, y el
      MAX() le entrega a uno el renglon del otro -> abre el PDF con los filtros
      del otro: su cliente, su agente, sus importes. El pase firmado se firma
      sobre el id equivocado, asi que no salva nada. Y no hace falta mala
      intencion: basta con dos pestanas.

      COMO SE ARREGLA SIN TOCAR EL SP (prohibido editar un sp_n_): la llave la
      elegimos nosotros. Se le pone a @NombreEquipo un sufijo unico por llamada,
      asi que el MAX() se calcula sobre un conjunto de UN solo renglon: el propio.

      Por que es seguro:
      - Nadie lee Parametros por NombreEquipo. Los reportes reciben el
        IDParametros y lo resuelven con sp_n_ConsultaParametros; la unica linea
        que leia NombreEquipo en los sp_n_rpt* esta comentada.
      - Cabe: la columna es varchar(1000) y el #ParamIds de los reportes la
        declara varchar(400); "WEB-" + 32 hex son 36 caracteres.
      - El texto que SI se imprime (Param1 = "WEB[<idUsuario>") no se toca: lo
        sigue armando BuildLegacyUsuarioParam con el nombre limpio, sin sufijo.
    */
    private static string EquipoUnicoPorLlamada(string nombreEquipo)
    {
        var baseNombre = string.IsNullOrWhiteSpace(nombreEquipo) ? "WEB" : nombreEquipo.Trim();
        if (baseNombre.Length > 60) baseNombre = baseNombre[..60];
        return $"{baseNombre}-{Guid.NewGuid():N}";
    }

    /*
      LOS PARAMETROS DEL REPORTE ENCADENADO DE ZARAGOZA, UNO POR UNO.

      Copiados de VerReporteDetalle (ReporteEmpleadoAsistenciaRecibos.cs:159-194),
      que los comenta en el propio codigo:

          Param1  = Equipo[IDUsuario
          Param2  = IDsEmpresa          ("")
          Param3  = IDCliente           ("0")
          Param4  = IDsAgente           ("")
          Param5  = DiasVencimiento     ("0")
          Param6  = SaldosStatus        (Pagadas)
          Param7  = Formato             ("1" = Detallado)
          Param8  = FechaComisionesZaraInicialMMDDYY  <- de dbo.Constantes
          Param9  = FechaComisionesZaraFinalMMDDYY    <- de dbo.Constantes
          Param10 = Presentacion
          Param11 = IDsCliente          ("0")
          Param12 = FiltrarFechas       ("0")
          Param13 = IDsEmpleado         <- los marcados en la rejilla

      LAS DOS FECHAS NO SON LAS DE LA PANTALLA. Son las dos columnas de
      `dbo.Constantes` que Mac31 lee al arrancar (Globales.cs:532) y que mandan
      el periodo de comisiones de Zaragoza. Pasarle las del periodo de nomina
      daria otro papel, parecido y equivocado.
    */
    private static async Task<LegacyReportParams> BuildReciboDetalleZaraLegacyParamsAsync(
        SqlConnection conn,
        ReportesVentasLegacyRequest request,
        CancellationToken ct)
    {
        var (fechaInicial, fechaFinal) = await ConsultarFechasComisionesZaraAsync(conn, ct);

        return new LegacyReportParams
        {
            NombreEquipo = ResolveLegacyNombreEquipo(request),
            IDUsuario = ResolveLegacyIDUsuario(request),
            Param1 = BuildLegacyUsuarioParam(request),
            Param2 = "",
            Param3 = "0",
            Param4 = "",
            Param5 = "0",
            // El mismo valor que ya usa el nodo del arbol para "pagadas".
            Param6 = "2",
            Param7 = "1",
            Param8 = fechaInicial,
            Param9 = fechaFinal,
            Param10 = ResolvePresentacion(request.Salida).ToString(CultureInfo.InvariantCulture),
            Param11 = "0",
            Param12 = "0",
            Param13 = JoinIdsPorComa(request.IDEmpleados)
        };
    }

    private static async Task<(string Inicial, string Final)> ConsultarFechasComisionesZaraAsync(
        SqlConnection conn,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand(
            "SELECT TOP 1 ISNULL(FechaComisionesZaraInicialMMDDYY,'') AS Inicial, " +
            "ISNULL(FechaComisionesZaraFinalMMDDYY,'') AS Final FROM dbo.Constantes",
            conn)
        {
            CommandType = CommandType.Text,
            CommandTimeout = 120
        };

        var tabla = await ExecuteFirstTableAsync(cmd, ct);
        if (tabla.Rows.Count == 0)
            throw new InvalidOperationException("dbo.Constantes no tiene las fechas de comisiones de Zaragoza.");

        return (ReadString(tabla.Rows[0], "Inicial"), ReadString(tabla.Rows[0], "Final"));
    }

    private static async Task<int> GuardarParametrosAsync(
        SqlConnection conn,
        int idReporte,
        LegacyReportParams legacyParams,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ActualizarParametro", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };

        cmd.Parameters.AddWithValue("@NombreEquipo", EquipoUnicoPorLlamada(legacyParams.NombreEquipo));
        cmd.Parameters.AddWithValue("@IDUsuario", legacyParams.IDUsuario);
        cmd.Parameters.AddWithValue("@IDReporte", idReporte);
        cmd.Parameters.AddWithValue("@Param1", legacyParams.Param1);
        cmd.Parameters.AddWithValue("@Param2", legacyParams.Param2);
        cmd.Parameters.AddWithValue("@Param3", legacyParams.Param3);
        cmd.Parameters.AddWithValue("@Param4", legacyParams.Param4);
        cmd.Parameters.AddWithValue("@Param5", legacyParams.Param5);
        cmd.Parameters.AddWithValue("@Param6", legacyParams.Param6);
        cmd.Parameters.AddWithValue("@Param7", legacyParams.Param7);
        cmd.Parameters.AddWithValue("@Param8", legacyParams.Param8);
        cmd.Parameters.AddWithValue("@Param9", legacyParams.Param9);
        cmd.Parameters.AddWithValue("@Param10", legacyParams.Param10);
        cmd.Parameters.AddWithValue("@Param11", legacyParams.Param11);
        cmd.Parameters.AddWithValue("@Param12", legacyParams.Param12);
        cmd.Parameters.AddWithValue("@Param13", legacyParams.Param13);
        cmd.Parameters.AddWithValue("@Param14", legacyParams.Param14);
        cmd.Parameters.AddWithValue("@Param15", legacyParams.Param15);
        cmd.Parameters.AddWithValue("@Param16", legacyParams.Param16);
        cmd.Parameters.AddWithValue("@Param17", legacyParams.Param17);
        cmd.Parameters.AddWithValue("@Param18", "");
        cmd.Parameters.AddWithValue("@Param19", "");
        cmd.Parameters.AddWithValue("@Param20", "");

        var ds = await FillDataSetAsync(cmd, ct);
        if (ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
            throw new InvalidOperationException("sp_n_ActualizarParametro no regreso ID de parametros.");

        var row = ds.Tables[0].Rows[0];
        if (ds.Tables[0].Columns.Contains("ID") && int.TryParse(Convert.ToString(row["ID"]), out var id) && id > 0)
            return id;

        throw new InvalidOperationException("No se pudo leer el ID de parametros legacy.");
    }

    private static async Task<DataRow> ConsultarParametrosRowAsync(
        SqlConnection conn,
        int parametroId,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaParametros", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };
        cmd.Parameters.AddWithValue("@ID", parametroId);

        var ds = await FillDataSetAsync(cmd, ct);
        if (ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
            throw new InvalidOperationException("No se encontraron parametros legacy para el psp.");

        return ds.Tables[0].Rows[0];
    }

    private static async Task<LegacyStoredParams> ConsultarParametrosAsync(
        SqlConnection conn,
        int parametroId,
        CancellationToken ct)
    {
        var row = await ConsultarParametrosRowAsync(conn, parametroId, ct);
        var idReporte = ReadInt(row, "IDReporte");
        if (!IsAcumuladoresProductosReporte(idReporte))
            throw new InvalidOperationException("El psp no corresponde a Acumuladores y Productos.");

        return BuildAcumuladoresProductosStoredParams(row);
    }

    private static LegacyStoredParams BuildAcumuladoresProductosStoredParams(DataRow row)
    {
        var idReporte = ReadInt(row, "IDReporte");
        return new LegacyStoredParams
        {
            IDReporte = idReporte,
            Request = new ReportesVentasAcumuladoresProductosRequest
            {
                Categoria = idReporte == ReporteVentaAcumuladores ? "acumuladores" : "productos",
                IDGrupoCategoria = ReadInt(row, "Param3"),
                TipoReporte = ResolveTipoReporteFromParams(row),
                Documento = ResolveDocumento(ReadString(row, "Param7")),
                FechaInicial = ReadLegacyDate(row, "Param10"),
                FechaFinal = ReadLegacyDate(row, "Param11"),
                Salida = ResolveSalida(ReadString(row, "Param12")),
                SoloServiciosDomicilio = ReadString(row, "Param13") == ProductoServicioDomicilio.ToString(),
                IDEmpresas = SplitLegacyIds(ReadString(row, "Param2")),
                IDAlmacenes = SplitLegacyIds(ReadString(row, "Param6")),
                IDSubcategorias = SplitLegacyIds(ReadString(row, "Param4")),
                IDMarcas = SplitLegacyIds(ReadString(row, "Param5")),
                IDAgentes = SplitLegacyIds(ReadString(row, "Param9")),
                IDClientes = SplitLegacyIds(ReadString(row, "Param8"))
            }
        };
    }

    private static ReportesVentasRemisionesRequest BuildRemisionesStoredRequest(DataRow row)
    {
        return new ReportesVentasRemisionesRequest
        {
            TipoReporte = ResolveTipoReporteFromRemisionesParams(row),
            Documento = ResolveDocumento(ReadString(row, "Param4")),
            EstatusFolio = ResolveStatusFolioKey(ReadString(row, "Param7")),
            FechaInicial = ReadLegacyDate(row, "Param8"),
            FechaFinal = ReadLegacyDate(row, "Param9"),
            Salida = ResolveSalida(ReadString(row, "Param10")),
            SoloServiciosDomicilio = ReadString(row, "Param14") == ProductoServicioDomicilio.ToString(),
            IDEmpresas = SplitLegacyIds(ReadString(row, "Param2")),
            IDAlmacenes = SplitLegacyIds(ReadString(row, "Param3")),
            IDUsuarios = SplitLegacyIds(ReadString(row, "Param11")),
            IDAgentes = SplitLegacyIds(ReadString(row, "Param6")),
            IDClientes = SplitLegacyIds(ReadString(row, "Param5"))
        };
    }

    private static ReportesVentasConcentradosRequest BuildConcentradosStoredRequest(DataRow row)
    {
        return new ReportesVentasConcentradosRequest
        {
            FechaInicial = ReadLegacyDate(row, "Param3"),
            FechaFinal = ReadLegacyDate(row, "Param4"),
            Salida = ResolveSalida(ReadString(row, "Param5")),
            IDRepartidores = SplitLegacyIds(ReadString(row, "Param2"))
        };
    }

    private static ReportesVentasCobranzaRequest BuildCobranzaStoredRequest(DataRow row)
    {
        return new ReportesVentasCobranzaRequest
        {
            TipoReporte = ResolveTipoReporteFromCobranzaParams(row),
            CobranzaStatus = ResolveCobranzaStatusKey(ReadString(row, "Param6")),
            FechaInicial = ReadLegacyDate(row, "Param8"),
            FechaFinal = ReadLegacyDate(row, "Param9"),
            Salida = ResolveCobranzaSalida(ReadInt(row, "IDReporte"), row),
            IDEmpresas = SplitLegacyIds(ReadString(row, "Param2")),
            IDAgentes = SplitLegacyIds(ReadString(row, "Param4")),
            IDClientes = SplitLegacyIds(ReadString(row, "Param11"))
        };
    }

    /*
      LA PRESENTACION DE LA FAMILIA DE COBRANZA: Param10, SALVO EL 1170.

      Cinco de los seis reportes de cobranza comparten el juego largo de trece
      parametros y su presentacion cae en Param10. El 1170 (Rutas de Agentes)
      NO: solo tiene seis parametros y la presentacion es el ultimo. Lo arma asi
      Mac31 en el case VentasCobranzaDesglosado (ReportesMenu.cs:3264-3281):

          // Param 2 = IDsEmpresa      // Param 5 = Formato
          // Param 3 = IDCliente       // Param 6 = Presentacion
          // Param 4 = IDsAgente

      y `BuildLegacyVentasParamsAsync` lo GUARDA bien (su case pone Param5 = "1"
      y Param6 = presentacion). Lo que estaba mal era LEERLO de vuelta: al leer
      Param10 —que en este reporte viene VACIO— salia "pantalla" siempre.

      DEFECTO MEDIDO (2026-10-09, en MacZ y en Produccion_svr): pedir el 1170
      con salida = excel devolvia un PDF. No fallaba ni avisaba, porque
      ReportesV3W intenta primero el Excel, ve "pantalla", y se cae al PDF por
      su rama de respaldo. Es el mismo defecto que ya estaba contado para el
      1300 (Autos) en ResolveGenericVentasSalida, y con el mismo sintoma: un
      fichero con nombre .xlsx y un PDF dentro.

      Las columnas de Excel de este reporte ya estaban dadas de alta
      (DataHelpers.cs:202, que lo manda al mismo juego que el 1172), asi que no
      faltaba nada mas que leer el parametro correcto. Comprobado despues del
      arreglo: 7 910 B de xlsx en Zaragoza y 155 708 B en Tauro.
    */
    private static string ResolveCobranzaSalida(int idReporte, DataRow row)
    {
        return ResolveSalida(
            ReadString(row, idReporte == ReporteVentasCobranzaDesglosado ? "Param6" : "Param10"));
    }

    private static ReportesVentasAcumuladoresProductosRequest BuildGenericVentasStoredRequest(DataRow row)
    {
        var idReporte = ReadInt(row, "IDReporte");
        var fechaInicialColumn = idReporte switch
        {
            ReporteVentasPagosTransferenciasDuplicadas => "Param4",
            ReporteTransferenciasClientes or ReporteTransferenciasEstatus => "Param10",
            ReporteTransferenciasFolios => "Param6",
            ReporteVentasCobrosDinero or ReporteVentasCobrosCascos => "Param2",
            ReporteCentrosRemisiones or ReporteCentrosPagosEntradasSalidas or ReporteCentrosGarantias or ReporteCentrosCompleto => "Param2",
            ReporteVentasLiquidaciones or ReporteCorteDiarioDetalleMac => "Param2",
            ReporteCompraAcumuladores or ReporteCompraProductos => "Param8",
            ReporteInventarioAcumuladores or ReporteInventarioProductos or ReporteInventarioFiltros or ReporteInventarioAcumuladoresCosto or ReporteInventarioProductosCosto or ReporteInventarioFiltrosCosto or ReportePedidoAcumuladores or ReportePedidoProductos => "Param8",
            ReporteInventarioFaltanteAcumuladores or ReporteInventarioFaltanteProductos or ReporteInventarioFaltanteFiltros => "Param8",
            ReporteAjustesInventario => "Param6",
            ReporteMovimientosAlmacen => "Param10",
            ReporteHojaCobroTotalCobradoDetallado or ReporteHojaCobroTotalCobradoTotalizado or ReporteHojaCobroTotalCascos or ReporteHojaCobroCheques => "Param7",
            ReporteGarantias => "Param6",
            ReporteGasolinasDetallado or ReporteGasolinasMensual => "Param4",
            ReporteGastosDetallado or ReporteGastosTotalizado => "Param2",
            ReporteCortes or ReporteAccesosMac => "Param2",
            ReporteAsistenciasZaragoza => "Param3",
            // Nomina: Param4 y Param5, como dice su #ParamIds.
            ReporteNominaAsistencias or ReporteNominaAsistenciasCorto
                or ReporteNominaInasistencias or ReporteNominaVacaciones
                or ReporteNominaRecibos => "Param4",
            ReporteVentasRemisionesImpuestos => "Param8",
            _ => "Param3"
        };
        var fechaFinalColumn = idReporte switch
        {
            ReporteVentasPagosTransferenciasDuplicadas => "Param5",
            ReporteTransferenciasClientes or ReporteTransferenciasEstatus => "Param11",
            ReporteTransferenciasFolios => "Param7",
            ReporteVentasCobrosDinero or ReporteVentasCobrosCascos => "Param3",
            ReporteCentrosRemisiones or ReporteCentrosPagosEntradasSalidas or ReporteCentrosGarantias or ReporteCentrosCompleto => "Param3",
            ReporteVentasLiquidaciones or ReporteCorteDiarioDetalleMac => "Param3",
            ReporteCompraAcumuladores or ReporteCompraProductos => "Param9",
            ReporteInventarioAcumuladores or ReporteInventarioProductos or ReporteInventarioFiltros or ReporteInventarioAcumuladoresCosto or ReporteInventarioProductosCosto or ReporteInventarioFiltrosCosto or ReportePedidoAcumuladores or ReportePedidoProductos => "Param8",
            ReporteInventarioFaltanteAcumuladores or ReporteInventarioFaltanteProductos or ReporteInventarioFaltanteFiltros => "Param9",
            ReporteAjustesInventario => "Param7",
            ReporteMovimientosAlmacen => "Param11",
            ReporteHojaCobroTotalCobradoDetallado or ReporteHojaCobroTotalCobradoTotalizado or ReporteHojaCobroTotalCascos or ReporteHojaCobroCheques => "Param8",
            ReporteGarantias => "Param7",
            ReporteGasolinasDetallado or ReporteGasolinasMensual => "Param5",
            ReporteGastosDetallado or ReporteGastosTotalizado => "Param3",
            ReporteCortes or ReporteAccesosMac => "Param3",
            ReporteAsistenciasZaragoza => "Param4",
            ReporteNominaAsistencias or ReporteNominaAsistenciasCorto
                or ReporteNominaInasistencias or ReporteNominaVacaciones
                or ReporteNominaRecibos => "Param5",
            ReporteVentasRemisionesImpuestos => "Param9",
            _ => "Param4"
        };

        if (idReporte == ReporteVentasLiquidaciones)
        {
            return new ReportesVentasLegacyRequest
            {
                TipoReporte = ResolveTipoReporteFromLiquidacionesParams(row),
                FechaInicial = ReadLegacyDate(row, fechaInicialColumn),
                FechaFinal = ReadLegacyDate(row, fechaFinalColumn),
                Salida = ResolveGenericVentasSalida(idReporte, row),
                IDRepartidores = SplitLegacyIds(ReadString(row, "Param5"))
            };
        }

        if (idReporte == ReporteGarantias)
        {
            return new ReportesVentasLegacyRequest
            {
                TipoReporte = ResolveTipoReporteFromGarantiasParams(row),
                FechaInicial = ReadLegacyDate(row, fechaInicialColumn),
                FechaFinal = ReadLegacyDate(row, fechaFinalColumn),
                Salida = ResolveGenericVentasSalida(idReporte, row),
                IDClientes = SplitLegacyIds(ReadString(row, "Param8")),
                IDRepartidores = SplitLegacyIds(ReadString(row, "Param3")),
                IDStatusGarantias = SplitLegacyIds(ReadString(row, "Param4"))
            };
        }

        if (idReporte == ReporteCompraAcumuladores || idReporte == ReporteCompraProductos)
        {
            return new ReportesVentasLegacyRequest
            {
                TipoReporte = "empresa",
                Categoria = idReporte == ReporteCompraAcumuladores ? "acumuladores" : "productos",
                IDGrupoCategoria = ReadInt(row, "Param3"),
                FechaInicial = ReadLegacyDate(row, fechaInicialColumn),
                FechaFinal = ReadLegacyDate(row, fechaFinalColumn),
                Salida = ResolveGenericVentasSalida(idReporte, row),
                IDEmpresas = SplitLegacyIds(ReadString(row, "Param2")),
                IDAlmacenes = SplitLegacyIds(ReadString(row, "Param6")),
                IDSubcategorias = SplitLegacyIds(ReadString(row, "Param4")),
                IDMarcas = SplitLegacyIds(ReadString(row, "Param5")),
                IDProveedor = ReadInt(row, "Param7")
            };
        }

        if (idReporte == ReporteComprasFacturas)
        {
            var tipoFecha = string.IsNullOrWhiteSpace(ReadString(row, "Param9")) ? "captura" : "factura";
            return new ReportesVentasLegacyRequest
            {
                TipoReporte = "empresa",
                FechaInicial = ReadLegacyDate(row, tipoFecha == "factura" ? "Param9" : "Param7"),
                FechaFinal = ReadLegacyDate(row, tipoFecha == "factura" ? "Param10" : "Param8"),
                Salida = ResolveGenericVentasSalida(idReporte, row),
                IDEmpresas = SplitLegacyIds(ReadString(row, "Param2")),
                IDAlmacenes = SplitLegacyIds(ReadString(row, "Param3")),
                IDProveedor = ReadInt(row, "Param4"),
                TipoFechaCompras = tipoFecha,
                EstatusComprasFacturas = ResolveComprasFacturaStatusKey(ReadString(row, "Param6"))
            };
        }

        if (idReporte is ReporteInventarioAcumuladores or ReporteInventarioProductos or ReporteInventarioFiltros or ReporteInventarioAcumuladoresCosto or ReporteInventarioProductosCosto or ReporteInventarioFiltrosCosto or ReportePedidoAcumuladores or ReportePedidoProductos)
        {
            return new ReportesVentasLegacyRequest
            {
                TipoReporte = "empresa",
                IDGrupoCategoria = ReadInt(row, "Param2"),
                FechaInicial = ReadLegacyDate(row, fechaInicialColumn),
                FechaFinal = ReadLegacyDate(row, fechaFinalColumn),
                Salida = ResolveGenericVentasSalida(idReporte, row),
                IDAlmacenes = SplitLegacyIds(ReadString(row, "Param5")),
                IDSubcategorias = SplitLegacyIds(ReadString(row, "Param3")),
                IDMarcas = SplitLegacyIds(ReadString(row, "Param4")),
                InventarioConCostos = idReporte is ReporteInventarioAcumuladoresCosto or ReporteInventarioProductosCosto or ReporteInventarioFiltrosCosto,
                InventarioGenerarPedido = idReporte is ReportePedidoAcumuladores or ReportePedidoProductos,
                InventarioHistorico = !string.IsNullOrWhiteSpace(ReadString(row, "Param8"))
            };
        }

        if (idReporte is ReporteInventarioFaltanteAcumuladores or ReporteInventarioFaltanteProductos or ReporteInventarioFaltanteFiltros)
        {
            return new ReportesVentasLegacyRequest
            {
                TipoReporte = "empresa",
                IDGrupoCategoria = ReadInt(row, "Param2"),
                FechaInicial = ReadLegacyDate(row, "Param8"),
                FechaFinal = ReadLegacyDate(row, "Param9"),
                Salida = ResolveGenericVentasSalida(idReporte, row),
                IDEmpresas = SplitLegacyIds(ReadString(row, "Param6")),
                IDAlmacenes = SplitLegacyIds(ReadString(row, "Param5")),
                IDSubcategorias = SplitLegacyIds(ReadString(row, "Param3")),
                IDMarcas = SplitLegacyIds(ReadString(row, "Param4"))
            };
        }

        if (idReporte == ReporteAjustesInventario || idReporte == ReporteMovimientosAlmacen)
        {
            return new ReportesVentasLegacyRequest
            {
                TipoReporte = "empresa",
                IDGrupoCategoria = ReadInt(row, idReporte == ReporteMovimientosAlmacen ? "Param3" : "Param2"),
                FechaInicial = ReadLegacyDate(row, fechaInicialColumn),
                FechaFinal = ReadLegacyDate(row, fechaFinalColumn),
                Salida = ResolveGenericVentasSalida(idReporte, row),
                IDEmpresas = SplitLegacyIds(ReadString(row, "Param2")),
                IDAlmacenes = SplitLegacyIds(ReadString(row, idReporte == ReporteMovimientosAlmacen ? "Param6" : "Param5")),
                IDSubcategorias = SplitLegacyIds(ReadString(row, idReporte == ReporteMovimientosAlmacen ? "Param4" : "Param3")),
                IDMarcas = SplitLegacyIds(ReadString(row, idReporte == ReporteMovimientosAlmacen ? "Param5" : "Param4"))
            };
        }

        if (idReporte is ReporteGasolinasDetallado or ReporteGasolinasMensual)
        {
            return new ReportesVentasLegacyRequest
            {
                TipoReporte = "empresa",
                FechaInicial = ReadLegacyDate(row, fechaInicialColumn),
                FechaFinal = ReadLegacyDate(row, fechaFinalColumn),
                Salida = ResolveGenericVentasSalida(idReporte, row),
                Formato = idReporte == ReporteGasolinasMensual ? "mensual" : "detallado",
                IDAutos = SplitLegacyIds(ReadString(row, "Param2")),
                IDRepartidores = SplitLegacyIds(ReadString(row, "Param3"))
            };
        }

        if (idReporte is ReporteGastosDetallado or ReporteGastosTotalizado)
        {
            return new ReportesVentasLegacyRequest
            {
                TipoReporte = "empresa",
                FechaInicial = ReadLegacyDate(row, fechaInicialColumn),
                FechaFinal = ReadLegacyDate(row, fechaFinalColumn),
                Salida = ResolveGenericVentasSalida(idReporte, row),
                Formato = idReporte == ReporteGastosTotalizado ? "totalizado" : "detallado",
                IDTipoGasto = ReadInt(row, "Param6")
            };
        }

        if (idReporte is ReporteListasPreciosZaragoza or ReporteListasPreciosProductosZaragoza
            or ReporteListasPreciosTauroTodas or ReporteListasPreciosTauroTodasMenosTauro
            or ReporteListasPreciosTauroCroGonXtLthClAmFpDy or ReporteListasPreciosTauroFpAmLth)
        {
            return new ReportesVentasLegacyRequest
            {
                TipoReporte = "empresa",
                IDGrupoCategoria = ReadInt(row, "Param10"),
                FechaInicial = DateTime.Today,
                FechaFinal = DateTime.Today,
                Salida = ResolveGenericVentasSalida(idReporte, row),
                IDSubcategorias = SplitLegacyIds(ReadString(row, "Param11")),
                IDMarcas = SplitLegacyIds(ReadString(row, "Param12")),
                Comentarios = ReadString(row, "Param13"),
                Redondear = ReadString(row, "Param8") == "1",
                TipoLista = Math.Max(1, ReadInt(row, "Param17"))
            };
        }

        return new ReportesVentasAcumuladoresProductosRequest
        {
            TipoReporte = ResolveTipoReporteFromParams(row),
            FechaInicial = ReadLegacyDate(row, fechaInicialColumn),
            FechaFinal = ReadLegacyDate(row, fechaFinalColumn),
            Salida = ResolveGenericVentasSalida(idReporte, row),
            IDEmpresas = SplitLegacyIds(ReadString(row, "Param2")),
            IDAgentes = SplitLegacyIds(ReadString(row, "Param4", "Param5")),
            IDClientes = SplitLegacyIds(ReadString(row, "Param3", "Param4", "Param5", "Param6"))
        };
    }

    private static string ResolveGenericVentasSalida(int idReporte, DataRow row)
    {
        var column = idReporte switch
        {
            ReporteVentasPagosDetallado or ReporteVentasPagosTotalizado or ReporteVentasPagosDetalladoCS or ReporteVentasPagosTotalizadoCS or ReporteVentasPagosDetalladoTauro => "Param11",
            ReporteTransferenciasClientes or ReporteTransferenciasEstatus => "Param12",
            ReporteTransferenciasFolios => "Param8",
            ReporteClientesConCompraPorDia => "Param8",
            ReporteVentasUtilidadAcumuladores or ReporteVentasUtilidadProductos => "Param13",
            ReporteCompraAcumuladores or ReporteCompraProductos => "Param10",
            ReporteComprasFacturas => "Param13",
            // 1017: Param2 lleva los ids marcados y la presentacion queda en
            // Param3. Sin esto el GET la leeria del Param7 por omision y el
            // reporte saldria como Excel al re-abrirlo.
            ReporteComprasComprobacion => "Param3",
            // 1015: Param2 lleva el IDCompra y la presentacion queda en Param3.
            ReporteComprasDetalle => "Param3",
            ReporteInventarioAcumuladores or ReporteInventarioProductos or ReporteInventarioFiltros or ReporteInventarioAcumuladoresCosto or ReporteInventarioProductosCosto or ReporteInventarioFiltrosCosto or ReportePedidoAcumuladores or ReportePedidoProductos => "Param7",
            ReporteInventarioFaltanteAcumuladores or ReporteInventarioFaltanteProductos or ReporteInventarioFaltanteFiltros => "Param7",
            ReporteAjustesInventario => "Param8",
            ReporteMovimientosAlmacen => "Param12",
            ReporteVentasCascosExcedentes => "Param7",
            ReporteVentasCobrosDinero or ReporteVentasCobrosCascos => "Param6",
            ReporteVentasEstadoCuenta or ReporteVentasEstadoCuentaDinero or ReporteVentasEstadoCuentaCascos => "Param8",
            ReporteCentrosCompleto => "Param5",
            // 1410, 1415 y 1420 leen la presentacion en Param4; solo el 1445
            // mete IDsCentro en medio y la corre a Param5 (sus #ParamIds lo dicen).
            ReporteCentrosRemisiones or ReporteCentrosPagosEntradasSalidas or ReporteCentrosGarantias => "Param4",
            ReporteGarantiasRegistradas => "Param5",
            ReporteControlAcumuladoEstimaciones => "Param4",
            // 1535: Param2 es el IDCambioVigencia y la presentacion queda en Param3.
            ReporteCambiosVigencia => "Param3",
            /*
              1255/1256/1260: Param2 lleva el id (o la fecha, en el 1260) y la
              presentacion queda en Param3. Si esta entrada faltara, el GET
              leeria la presentacion de Param7 —el valor por omision— y el
              comprobante saldria como Excel o se caeria al re-abrirlo.
            */
            ReporteComprobanteCascos or ReporteVentaCascos or ReporteComprobanteCascosTauro => "Param3",
            // 1030: diez parametros antes de la presentacion (su #ParamIds la
            // nombra `Presentacion` en la posicion de Param11).
            ReporteVentasDescuentosAjustes => "Param11",
            ReporteVentasLiquidaciones => "Param6",
            ReporteHojaCobroTotalCobradoDetallado or ReporteHojaCobroTotalCobradoTotalizado or ReporteHojaCobroTotalCascos or ReporteHojaCobroCheques => "Param9",
            ReporteGarantias => "Param9",
            ReporteGasolinasDetallado or ReporteGasolinasMensual => "Param6",
            ReporteGastosDetallado or ReporteGastosTotalizado => "Param4",
            ReporteCortes or ReporteAccesosMac => "Param4",
            // 1315: Param2 es el IDCorte y la presentacion queda en Param3.
            ReporteCorteDiario => "Param3",
            // 1317: mismos seis parametros que el 1319, presentacion en Param6.
            ReporteCorteDiarioDetalleMac => "Param6",
            // 1345: IDCorte, IDTipoRetiro y FechaFinal antes de la presentacion.
            ReporteCorteCentroServicio => "Param5",
            ReporteAsistenciasZaragoza => "Param5",
            // Nomina: la presentacion es Param6 en los cuatro.
            ReporteNominaAsistencias or ReporteNominaAsistenciasCorto
                or ReporteNominaInasistencias or ReporteNominaVacaciones
                or ReporteNominaRecibos => "Param6",
            ReporteVentasRemisionesImpuestos => "Param10",
            ReporteListasPreciosZaragoza or ReporteListasPreciosProductosZaragoza
                or ReporteListasPreciosTauroTodas or ReporteListasPreciosTauroTodasMenosTauro
                or ReporteListasPreciosTauroCroGonXtLthClAmFpDy or ReporteListasPreciosTauroFpAmLth => "Param6",
            ReporteClientesGlobal => "Param5",
            ReporteClientesDescuentos or ReporteClientesDescuentosSin => "Param7",
            /*
              1300 (Autos): LA PRESENTACION ES Param2, Y SIN ESTA LINEA EL EXCEL
              NO EXISTIA.

              Autos es el reporte mas corto del catalogo: su unico parametro
              ademas del equipo es la presentacion, asi que va en Param2
              -lo arma asi Mac31 (ConsultarAutos.cs, Imprimir): "Param 2 =
              Presentacion"- y asi lo guarda BuildLegacyVentasParamsAsync.

              Al faltar aqui caia en el `_ => "Param7"`, que en este reporte
              viene vacio, o sea "pantalla". Consecuencia medida: pedir el
              reporte con salida = excel devolvia un PDF. No fallaba ni avisaba
              -ReportesV3W intenta primero el Excel, ve "pantalla", y se cae al
              PDF por la rama de respaldo-, asi que el fichero llegaba con
              nombre .xlsx y un PDF dentro.

              En Mac31 ese boton SI funciona: ConsultarAutos tiene btnExcel con
              cuerpo (llama Imprimir(enumPresentacion.Excel)), y
              sp_w_rptAutos con Presentacion = 3 devuelve la tabla plana de 12
              columnas. Comprobado en MacZ.
            */
            ReporteAutos => "Param2",
            _ => "Param7"
        };

        return ResolveSalida(ReadString(row, column));
    }

    /// <summary>
    /// CAMIONETA (1) O AGENTE (2) para el 1319.
    ///
    /// Mira primero `TipoLiquidacion`, que es el campo propio, y solo si viene
    /// vacio cae a `TipoReporte`, que es de donde se leia antes. El porque de
    /// los dos campos esta en el DTO: con `TipoReporte = "agente"` la
    /// validacion general exigia agentes y la liquidacion por agente no se
    /// podia pedir.
    /// </summary>
    private static int ResolveTipoLiquidacion(string? tipoLiquidacion, string? tipoReporte)
    {
        var elegido = (tipoLiquidacion ?? "").Trim();
        if (elegido.Length == 0) elegido = tipoReporte ?? "";

        return elegido.Trim().ToLowerInvariant() switch
        {
            "liquidacion_agente" or "agente" => 2,
            _ => 1
        };
    }

    private static string ResolveTipoReporteFromLiquidacionesParams(DataRow row)
    {
        return ReadString(row, "Param4").Trim() == "2" ? "liquidacion_agente" : "camioneta";
    }

    private static string ResolveTipoReporteFromGarantiasParams(DataRow row)
    {
        var clientes = SplitLegacyIds(ReadString(row, "Param8"));
        if (clientes.Any(id => id > 0))
            return "cliente";

        return "empresa";
    }

    private static string ResolveTipoReporteFromParams(DataRow row)
    {
        var clientes = SplitLegacyIds(ReadString(row, "Param8"));
        if (clientes.Any(id => id > 0))
            return "cliente";

        var agentes = SplitLegacyIds(ReadString(row, "Param9"));
        if (agentes.Any(id => id > 0))
            return "agente";

        return "empresa";
    }

    private static string ResolveTipoReporteFromRemisionesParams(DataRow row)
    {
        var clientes = SplitLegacyIds(ReadString(row, "Param5"));
        if (clientes.Any(id => id > 0))
            return "cliente";

        var agentes = SplitLegacyIds(ReadString(row, "Param6"));
        if (agentes.Any(id => id > 0))
            return "agente";

        return "empresa";
    }

    private static string ResolveTipoReporteFromCobranzaParams(DataRow row)
    {
        var clientes = SplitLegacyIds(ReadString(row, "Param11"));
        if (clientes.Any(id => id > 0))
            return "cliente";

        var agentes = SplitLegacyIds(ReadString(row, "Param4"));
        if (agentes.Any(id => id > 0))
            return "agente";

        return "empresa";
    }

    private static string ResolveStatusFolioKey(string? statusFolio)
    {
        return (statusFolio ?? "").Trim() switch
        {
            "1" => "vigentes",
            "2" => "cancelados",
            "4" => "saldo_favor",
            _ => "todos"
        };
    }

    private static string ResolveCobranzaStatusKey(string? status)
    {
        return (status ?? "").Trim() switch
        {
            "1" => "vigentes",
            "3" => "vencidas",
            "4" => "vigentes_vencidas",
            _ => "pagadas"
        };
    }

    private static int ResolveTipoFactura(string? tipoFactura)
    {
        return (tipoFactura ?? "factura").Trim().ToLowerInvariant() switch
        {
            "nota_credito" or "nota credito" => 1,
            "complemento_pago" or "complemento pago" => 4,
            _ => 0
        };
    }
}
