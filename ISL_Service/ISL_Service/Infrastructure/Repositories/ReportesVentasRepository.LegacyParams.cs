using System.Data;
using System.Globalization;
using ISL_Service.Application.DTOs.Reportes;
using ISL_Service.Application.Interfaces;
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

    private static int ResolveReporteCobranza(ReportesVentasCobranzaRequest request)
    {
        return ReporteVentasCobranzaDetalladoZara;
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
        return idReporte == ReporteVentasCobranzaDetalladoZara
            || idReporte == ReporteVentasCobranzaPagadasDetalladoZara
            || idReporte == ReporteVentasCobranzaPagadasTotalizadoZara
            || idReporte == ReporteVentasCobranzaDesglosado
            || idReporte == ReporteVentasCobranzaZaragoza;
    }

    private static int ResolveReporteLegacyVentas(ReportesVentasLegacyRequest request)
    {
        if (request.IDReporte > 0)
            return request.IDReporte;

        return (request.ReporteKey ?? "").Trim().ToLowerInvariant() switch
        {
            "motobaterias" => ResolveReporteMotobaterias(request.FormatoMotobaterias),
            "cobranza_detallado" => ReporteVentasCobranzaZaragoza,
            "cobranza_pagadas" => ResolveFormatoDetallado(request.Formato) ? ReporteVentasCobranzaPagadasDetalladoZara : ReporteVentasCobranzaPagadasTotalizadoZara,
            "estado_de_cuenta" => ResolveReporteEstadoCuenta(request.DineroCascos),
            "rutas_de_agentes" => ReporteVentasCobranzaDesglosado,
            "pagos" => ResolveFormatoDetallado(request.Formato) ? ReporteVentasPagosDetallado : ReporteVentasPagosTotalizado,
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
            "clientes_acum_moto_lub" => ReporteClientesAcumMotoLub,
            "clientes_acum_moto_lub_clarios" => ReporteClientesAcumMotoLubClarios,
            "compras_acumuladores_y_productos" => request.IDGrupoCategoria == GrupoCategoriaAcumuladores ? ReporteCompraAcumuladores : ReporteCompraProductos,
            "compras_facturas" => ReporteComprasFacturas,
            "inventario_actual" => ResolveReporteInventarioActual(request),
            "inventario_faltante" => ResolveReporteInventarioFaltante(request.IDGrupoCategoria),
            "ajustes_al_inventario" => ReporteAjustesInventario,
            "movimientos" => ReporteMovimientosAlmacen,
            "cascos" => ReporteVentasUsadosCreditos,
            "cascos_excedentes" => ReporteVentasCascosExcedentes,
            "liquidaciones" => ReporteVentasLiquidaciones,
            "cobros_dinero_y_cascos" => (request.DineroCascos ?? "").Trim().Equals("cascos", StringComparison.OrdinalIgnoreCase) ? ReporteVentasCobrosCascos : ReporteVentasCobrosDinero,
            "movimientos_centros" => ReporteCentrosRemisiones,
            "centros_de_servicio" => ReporteCentrosCompleto,
            "hoja_de_cobro_total_cobrado" => ResolveFormatoDetallado(request.Formato) ? ReporteHojaCobroTotalCobradoDetallado : ReporteHojaCobroTotalCobradoTotalizado,
            "hoja_de_cobro_total_cascos" => ReporteHojaCobroTotalCascos,
            "hoja_de_cobro_cheques" => ReporteHojaCobroCheques,
            "garantias" => ReporteGarantias,
            _ => throw new InvalidOperationException("Reporte de ventas no soportado.")
        };
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

    private static int ResolveReporteEstadoCuenta(string? formato)
    {
        return ReporteVentasEstadoCuenta;
    }

    private static int ResolveReporteInventarioActual(ReportesVentasLegacyRequest request)
    {
        var idGrupoCategoria = request.IDGrupoCategoria > 0 ? request.IDGrupoCategoria : GrupoCategoriaAcumuladores;
        if (request.InventarioConCostos)
        {
            return idGrupoCategoria switch
            {
                2 => ReporteInventarioFiltrosCosto,
                GrupoCategoriaAcumuladores => ReporteInventarioAcumuladoresCosto,
                _ => ReporteInventarioProductosCosto
            };
        }

        if (request.InventarioGenerarPedido)
        {
            return idGrupoCategoria == GrupoCategoriaAcumuladores
                ? ReportePedidoAcumuladores
                : ReportePedidoProductos;
        }

        return idGrupoCategoria switch
        {
            2 => ReporteInventarioFiltros,
            GrupoCategoriaAcumuladores => ReporteInventarioAcumuladores,
            _ => ReporteInventarioProductos
        };
    }

    private static int ResolveReporteInventarioFaltante(int idGrupoCategoria)
    {
        return idGrupoCategoria switch
        {
            2 => ReporteInventarioFaltanteFiltros,
            GrupoCategoriaAcumuladores => ReporteInventarioFaltanteAcumuladores,
            _ => ReporteInventarioFaltanteProductos
        };
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
            ReporteClientesAcumMotoLubClarios or
            ReporteCompraAcumuladores or
            ReporteCompraProductos or
            ReporteComprasFacturas or
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
            ReporteClientesFacturasRFC or
            ReporteVentasUtilidadAcumuladores or
            ReporteVentasUtilidadProductos or
            ReporteVentasPagosComparativo or
            ReporteVentasDescuentosAjustes or
            ReporteVentasUsadosCreditos or
            ReporteGarantias or
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
        return await BuildDocumentosVentaLegacyParamsAsync(conn, idReporte, request, ct);
    }

    private static async Task<LegacyReportParams> BuildDocumentosVentaLegacyParamsAsync(
        SqlConnection conn,
        int idReporte,
        ReportesVentasRemisionesRequest request,
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
            Param7 = ResolveStatusFolio(request.EstatusFolio).ToString(),
            Param8 = request.FechaInicial.ToString("yyyy-MM-dd"),
            Param9 = request.FechaFinal.ToString("yyyy-MM-dd"),
            Param10 = ResolvePresentacion(request.Salida).ToString(),
            Param11 = JoinIds(request.IDUsuarios),
            Param12 = formato.ToString(),
            Param13 = tipo.ToString(),
            Param14 = idReporte == ReporteVentasRemisiones && request.SoloServiciosDomicilio
                ? ProductoServicioDomicilio.ToString()
                : ""
        };
    }

    private static LegacyReportParams BuildConcentradosLegacyParams(ReportesVentasConcentradosRequest request)
    {
        return new LegacyReportParams
        {
            NombreEquipo = ResolveLegacyNombreEquipo(request),
            IDUsuario = ResolveLegacyIDUsuario(request),
            Param1 = BuildLegacyUsuarioParam(request),
            Param2 = JoinIds(request.IDRepartidores),
            Param3 = FormatLegacyStartDateTime(request.FechaInicial),
            Param4 = FormatLegacyEndDateTime(request.FechaFinal),
            Param5 = ResolvePresentacion(request.Salida).ToString()
        };
    }

    private static async Task<LegacyReportParams> BuildCobranzaLegacyParamsAsync(
        SqlConnection conn,
        ReportesVentasCobranzaRequest request,
        CancellationToken ct)
    {
        var idCliente = await ResolveClienteLegacyAsync(conn, request, ct);
        var idsCliente = ResolveCobranzaStatus(request.CobranzaStatus) == 4
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
            Param6 = ResolveCobranzaStatus(request.CobranzaStatus).ToString(),
            Param7 = "1",
            Param8 = FormatLegacyStartDateTime(request.FechaInicial),
            Param9 = FormatLegacyEndDateTime(request.FechaFinal),
            Param10 = ResolvePresentacion(request.Salida).ToString(),
            Param11 = idsCliente,
            Param12 = "0",
            Param13 = ""
        };
    }

    private static async Task<LegacyReportParams> BuildLegacyVentasParamsAsync(
        SqlConnection conn,
        int idReporte,
        ReportesVentasLegacyRequest request,
        CancellationToken ct)
    {
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
            case ReporteClientesAcumMotoLubClarios:
                legacy.Param2 = idsEmpresa;
                legacy.Param3 = fechaInicial;
                legacy.Param4 = fechaFinal;
                legacy.Param5 = idsAgente;
                legacy.Param6 = idCliente.ToString();
                legacy.Param7 = presentacion;
                if (idReporte == ReporteClientesAcumMotoLubClarios)
                {
                    legacy.Param8 = request.IDGrupoCategoria > 0 ? request.IDGrupoCategoria.ToString() : "1";
                    legacy.Param9 = "2025";
                }
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
                legacy.Param2 = (request.IDGrupoCategoria > 0 ? request.IDGrupoCategoria : GrupoCategoriaAcumuladores).ToString();
                legacy.Param3 = JoinIds(request.IDSubcategorias);
                legacy.Param4 = JoinIds(request.IDMarcas);
                legacy.Param5 = JoinIds(request.IDAlmacenes);
                legacy.Param6 = fechaInicial;
                legacy.Param7 = fechaFinal;
                legacy.Param8 = presentacion;
                break;

            case ReporteMovimientosAlmacen:
                legacy.Param2 = idsEmpresa;
                legacy.Param3 = (request.IDGrupoCategoria > 0 ? request.IDGrupoCategoria : GrupoCategoriaAcumuladores).ToString();
                legacy.Param4 = JoinIds(request.IDSubcategorias);
                legacy.Param5 = JoinIds(request.IDMarcas);
                legacy.Param6 = JoinIds(request.IDAlmacenes);
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
                legacy.Param4 = ResolveTipoLiquidacion(request.TipoReporte).ToString();
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

            case ReporteVentasEstadoCuenta:
            case ReporteVentasEstadoCuentaDinero:
            case ReporteVentasEstadoCuentaCascos:
                legacy.Param2 = idsEmpresa;
                legacy.Param3 = idCliente.ToString();
                legacy.Param4 = idsAgente;
                legacy.Param5 = fechaInicial;
                legacy.Param6 = fechaFinal;
                legacy.Param7 = ResolveFormatoEstadoCuenta(request.DineroCascos);
                legacy.Param8 = presentacion;
                legacy.Param9 = idsCliente;
                break;

            case ReporteCentrosRemisiones:
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

    private static string? ResolveAgentGroupColumn(DataTable table, ReportesVentasAcumuladoresProductosRequest request)
    {
        if (!IsAgentReport(request))
            return null;

        if (table.Columns.Contains("IDAgente"))
            return "IDAgente";
        if (table.Columns.Contains("Agente"))
            return "Agente";

        return null;
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

        cmd.Parameters.AddWithValue("@NombreEquipo", legacyParams.NombreEquipo);
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
        cmd.Parameters.AddWithValue("@Param15", "");
        cmd.Parameters.AddWithValue("@Param16", "");
        cmd.Parameters.AddWithValue("@Param17", "");
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
            Salida = ResolveSalida(ReadString(row, "Param10")),
            IDEmpresas = SplitLegacyIds(ReadString(row, "Param2")),
            IDAgentes = SplitLegacyIds(ReadString(row, "Param4")),
            IDClientes = SplitLegacyIds(ReadString(row, "Param11"))
        };
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
            ReporteCentrosRemisiones or ReporteCentrosCompleto => "Param2",
            ReporteVentasLiquidaciones => "Param2",
            ReporteCompraAcumuladores or ReporteCompraProductos => "Param8",
            ReporteInventarioAcumuladores or ReporteInventarioProductos or ReporteInventarioFiltros or ReporteInventarioAcumuladoresCosto or ReporteInventarioProductosCosto or ReporteInventarioFiltrosCosto or ReportePedidoAcumuladores or ReportePedidoProductos => "Param8",
            ReporteInventarioFaltanteAcumuladores or ReporteInventarioFaltanteProductos or ReporteInventarioFaltanteFiltros => "Param8",
            ReporteAjustesInventario => "Param6",
            ReporteMovimientosAlmacen => "Param10",
            ReporteHojaCobroTotalCobradoDetallado or ReporteHojaCobroTotalCobradoTotalizado or ReporteHojaCobroTotalCascos or ReporteHojaCobroCheques => "Param7",
            ReporteGarantias => "Param6",
            _ => "Param3"
        };
        var fechaFinalColumn = idReporte switch
        {
            ReporteVentasPagosTransferenciasDuplicadas => "Param5",
            ReporteTransferenciasClientes or ReporteTransferenciasEstatus => "Param11",
            ReporteTransferenciasFolios => "Param7",
            ReporteVentasCobrosDinero or ReporteVentasCobrosCascos => "Param3",
            ReporteCentrosRemisiones or ReporteCentrosCompleto => "Param3",
            ReporteVentasLiquidaciones => "Param3",
            ReporteCompraAcumuladores or ReporteCompraProductos => "Param9",
            ReporteInventarioAcumuladores or ReporteInventarioProductos or ReporteInventarioFiltros or ReporteInventarioAcumuladoresCosto or ReporteInventarioProductosCosto or ReporteInventarioFiltrosCosto or ReportePedidoAcumuladores or ReportePedidoProductos => "Param8",
            ReporteInventarioFaltanteAcumuladores or ReporteInventarioFaltanteProductos or ReporteInventarioFaltanteFiltros => "Param9",
            ReporteAjustesInventario => "Param7",
            ReporteMovimientosAlmacen => "Param11",
            ReporteHojaCobroTotalCobradoDetallado or ReporteHojaCobroTotalCobradoTotalizado or ReporteHojaCobroTotalCascos or ReporteHojaCobroCheques => "Param8",
            ReporteGarantias => "Param7",
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
            ReporteInventarioAcumuladores or ReporteInventarioProductos or ReporteInventarioFiltros or ReporteInventarioAcumuladoresCosto or ReporteInventarioProductosCosto or ReporteInventarioFiltrosCosto or ReportePedidoAcumuladores or ReportePedidoProductos => "Param7",
            ReporteInventarioFaltanteAcumuladores or ReporteInventarioFaltanteProductos or ReporteInventarioFaltanteFiltros => "Param7",
            ReporteAjustesInventario => "Param8",
            ReporteMovimientosAlmacen => "Param12",
            ReporteVentasCascosExcedentes => "Param7",
            ReporteVentasCobrosDinero or ReporteVentasCobrosCascos => "Param6",
            ReporteVentasEstadoCuenta or ReporteVentasEstadoCuentaDinero or ReporteVentasEstadoCuentaCascos => "Param8",
            ReporteCentrosRemisiones or ReporteCentrosCompleto => "Param5",
            ReporteVentasLiquidaciones => "Param6",
            ReporteHojaCobroTotalCobradoDetallado or ReporteHojaCobroTotalCobradoTotalizado or ReporteHojaCobroTotalCascos or ReporteHojaCobroCheques => "Param9",
            ReporteGarantias => "Param9",
            ReporteClientesGlobal => "Param5",
            ReporteClientesDescuentos or ReporteClientesDescuentosSin => "Param7",
            _ => "Param7"
        };

        return ResolveSalida(ReadString(row, column));
    }

    private static int ResolveTipoLiquidacion(string? tipoReporte)
    {
        return (tipoReporte ?? "").Trim().ToLowerInvariant() switch
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
