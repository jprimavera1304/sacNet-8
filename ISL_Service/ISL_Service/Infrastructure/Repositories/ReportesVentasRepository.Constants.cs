using System.Data;
using ISL_Service.Application.DTOs.Reportes;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Infrastructure.Reports;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

public partial class ReportesVentasRepository
{
    private const int ReporteVentaAcumuladores = 10;
    private const int ReporteVentaProductos = 15;
    private const int ReporteVentasRemisionesSaldoFavor = 19;
    private const int ReporteVentasFoliosGlobal = 20;
    private const int ReporteVentasRemisiones = 21;
    private const int ReporteVentasRemisionesImpuestos = 22;
    private const int ReporteVentasFoliosDineroUsados = 23;
    private const int ReporteVentasFacturas = 24;
    private const int ReporteVentasPagosDetallado = 71;
    private const int ReporteVentasPagosDetalladoCS = 72;
    private const int ReporteVentasPagosDescuentos = 73;
    private const int ReporteVentasPagosTransferenciasDuplicadas = 74;
    private const int ReporteVentasPagosTotalizado = 75;
    private const int ReporteVentasPagosTotalizadoCS = 76;
    private const int ReporteVentasPagosDetalladoTauro = 77;
    private const int ReporteClientesGlobal = 103;
    private const int ReporteClientesConCompra = 105;
    private const int ReporteClientesConCompraPorDia = 106;
    private const int ReporteClientesSinCompra = 110;
    private const int ReporteClientesConSinCompra = 112;
    private const int ReporteClientesDescuentos = 113;
    private const int ReporteClientesDescuentosSin = 114;
    private const int ReporteClientesAcumMotoLub = 116;
    private const int ReporteCompraAcumuladores = 120;
    private const int ReporteClientesAcumMotoLubClarios = 121;
    private const int ReporteCompraProductos = 125;
    private const int ReporteComprasFacturas = 130;
    private const int ReporteVentasUtilidadAcumuladores = 140;
    private const int ReporteVentasUtilidadProductos = 145;
    private const int ReporteVentasUsadosCreditos = 150;
    private const int ReporteInventarioAcumuladores = 205;
    private const int ReporteInventarioProductos = 210;
    private const int ReporteInventarioFiltros = 212;
    private const int ReporteInventarioAcumuladoresCosto = 220;
    private const int ReporteInventarioProductosCosto = 225;
    private const int ReporteInventarioFiltrosCosto = 227;
    private const int ReporteAjustesInventario = 230;
    private const int ReportePedidoAcumuladores = 235;
    private const int ReportePedidoProductos = 240;
    private const int ReporteVentasPagosComparativo = 493;
    private const int ReporteVentasDescuentosAjustes = 1030;
    private const int ReporteVentasCobranzaDetalladoZara = 1161;
    private const int ReporteVentasCobranzaPagadasDetalladoZara = 1162;
    private const int ReporteVentasCobranzaPagadasTotalizadoZara = 1166;
    private const int ReporteVentasCobranzaDesglosado = 1170;
    private const int ReporteVentasCobranzaZaragoza = 1172;
    private const int ReporteVentasConcentradosDetalle = 1185;
    private const int ReporteInventarioFaltanteAcumuladores = 1135;
    private const int ReporteInventarioFaltanteProductos = 1140;
    private const int ReporteInventarioFaltanteFiltros = 1141;
    private const int ReporteGarantias = 1235;
    private const int ReporteVentasCascosExcedentes = 1305;
    private const int ReporteVentasLiquidaciones = 1319;
    private const int ReporteMovimientosAlmacen = 1325;
    private const int ReporteVentasCobrosDineroCascos = 1355;
    private const int ReporteVentasCobrosDinero = 1360;
    private const int ReporteVentasCobrosCascos = 1365;
    private const int ReporteVentasEstadoCuenta = 1370;
    private const int ReporteVentasEstadoCuentaDinero = 1375;
    private const int ReporteVentasEstadoCuentaCascos = 1380;
    private const int ReporteTransferenciasClientes = 1405;
    private const int ReporteTransferenciasEstatus = 1406;
    private const int ReporteTransferenciasFolios = 1407;
    private const int ReporteCentrosRemisiones = 1410;
    private const int ReporteCentrosCompleto = 1445;
    private const int ReporteClientesFacturasRFC = 1440;
    private const int ReporteHojaCobroTotalCobradoDetallado = 1465;
    private const int ReporteHojaCobroTotalCobradoTotalizado = 1466;
    private const int ReporteHojaCobroTotalCascos = 1470;
    private const int ReporteVentasMotoBateriasPorCliente = 1475;
    private const int ReporteVentasMotoBateriasPorFolio = 1480;
    private const int ReporteVentasMotoBateriasPorCodigo = 1485;
    private const int ReporteVentasMotoBateriasPorMarca = 1490;
    private const int ReporteHojaCobroCheques = 1495;
    private const int GrupoCategoriaAcumuladores = 1;
    private const int ProductoServicioDomicilio = 11807;
    private const string FallbackLogoPath = "Assets/Logos/Logo_Zaragoza.png";
    private static readonly Lazy<string> FallbackLogoDataUri = new(() => TryImageFileToDataUri(Path.Combine(AppContext.BaseDirectory, FallbackLogoPath)) ?? "");
    private static readonly LegacyExcelColumn[] VentaAcumuladoresExcelColumns =
    {
        new("No", typeof(int)),
        new("FechaInicial", typeof(string)),
        new("FechaFinal", typeof(string)),
        new("Empresa", typeof(string)),
        new("Almacen", typeof(string)),
        new("Documento", typeof(string)),
        new("Agente", typeof(string)),
        new("Numero", typeof(int)),
        new("Cliente", typeof(string)),
        new("Categoria", typeof(string)),
        new("Marca", typeof(string)),
        new("FechaEmision", typeof(string)),
        new("HoraEmision", typeof(string)),
        new("Folio", typeof(string)),
        new("Clave", typeof(string)),
        new("UnidadMedida", typeof(string)),
        new("Ventas", typeof(decimal)),
        new("Garantias", typeof(decimal)),
        new("Devolucion", typeof(decimal)),
        new("Logistica", typeof(string)),
        new("Servicios", typeof(string)),
        new("Garantia", typeof(string)),
        new("CentroServicio", typeof(string))
    };
    private static readonly LegacyExcelColumn[] VentaProductosExcelColumns =
    {
        new("No", typeof(int)),
        new("FechaInicial", typeof(string)),
        new("FechaFinal", typeof(string)),
        new("Empresa", typeof(string)),
        new("Almacen", typeof(string)),
        new("Documento", typeof(string)),
        new("Agente", typeof(string)),
        new("Numero", typeof(int)),
        new("Cliente", typeof(string)),
        new("Categoria", typeof(string)),
        new("SubCategoria", typeof(string)),
        new("Marca", typeof(string)),
        new("FechaEmision", typeof(string)),
        new("HoraEmision", typeof(string)),
        new("Folio", typeof(string)),
        new("Clave", typeof(string)),
        new("Descripcion", typeof(string)),
        new("UnidadMedida", typeof(string)),
        new("TotalLitros", typeof(decimal)),
        new("Ventas", typeof(decimal)),
        new("Mayorista", typeof(decimal)),
        new("Local", typeof(decimal)),
        new("Ajuste", typeof(decimal)),
        new("Devolucion", typeof(decimal))
    };
    private static readonly LegacyExcelColumn[] VentasRemisionesExcelColumns =
    {
        new("No", typeof(int)),
        new("FechaInicial", typeof(string)),
        new("FechaFinal", typeof(string)),
        new("Documento", typeof(string)),
        new("Agente", typeof(string)),
        new("Empresa", typeof(string)),
        new("Usuario", typeof(string)),
        new("Fecha", typeof(string)),
        new("Folio", typeof(string)),
        new("Numero", typeof(int)),
        new("Nombre", typeof(string)),
        new("Acumuladores", typeof(int)),
        new("Productos", typeof(int)),
        new("Cascos", typeof(int)),
        new("ImporteDinero", typeof(decimal)),
        new("ImporteCascos", typeof(decimal)),
        new("TotalPagar", typeof(decimal)),
        new("Cancelada", typeof(string)),
        new("Ajuste", typeof(string))
    };
    private static readonly LegacyExcelColumn[] VentasFoliosGlobalExcelColumns =
    {
        new("No", typeof(int)),
        new("FechaInicial", typeof(string)),
        new("FechaFinal", typeof(string)),
        new("Empresa", typeof(string)),
        new("Documento", typeof(string)),
        new("Usuario", typeof(string)),
        new("Agente", typeof(string)),
        new("Folio", typeof(string)),
        new("FechaEmision", typeof(string)),
        new("Numero", typeof(int)),
        new("Nombre", typeof(string)),
        new("Acumuladores", typeof(int)),
        new("Productos", typeof(int)),
        new("Usados", typeof(int)),
        new("TotalPagar", typeof(decimal)),
        new("Cancelada", typeof(string)),
        new("Ajuste", typeof(string)),
        new("FolioConcentrado", typeof(string)),
        new("Repartidor", typeof(string))
    };
    private static readonly LegacyExcelColumn[] VentasFoliosDineroUsadosExcelColumns =
    {
        new("No", typeof(int)),
        new("FechaInicial", typeof(string)),
        new("FechaFinal", typeof(string)),
        new("Empresa", typeof(string)),
        new("Documento", typeof(string)),
        new("Usuario", typeof(string)),
        new("Agente", typeof(string)),
        new("Folio", typeof(string)),
        new("FechaEmision", typeof(string)),
        new("Numero", typeof(int)),
        new("Nombre", typeof(string)),
        new("Dinero", typeof(decimal)),
        new("Cascos", typeof(decimal)),
        new("TotalPagar", typeof(decimal)),
        new("Cancelada", typeof(string))
    };
    private static readonly LegacyExcelColumn[] VentasRemisionesSaldoFavorExcelColumns =
    {
        new("No", typeof(int)),
        new("FechaInicial", typeof(string)),
        new("FechaFinal", typeof(string)),
        new("Empresa", typeof(string)),
        new("Documento", typeof(string)),
        new("Agente", typeof(string)),
        new("Folio", typeof(string)),
        new("FechaEmision", typeof(string)),
        new("Numero", typeof(int)),
        new("Nombre", typeof(string)),
        new("Dinero", typeof(decimal)),
        new("Cascos", typeof(decimal)),
        new("SaldoFavorDinero", typeof(decimal)),
        new("SaldoFavorCascos", typeof(decimal))
    };
    private static readonly LegacyExcelColumn[] VentasRemisionesImpuestosExcelColumns =
    {
        new("No", typeof(int)),
        new("FechaInicial", typeof(string)),
        new("FechaFinal", typeof(string)),
        new("Empresa", typeof(string)),
        new("Agente", typeof(string)),
        new("Folio", typeof(string)),
        new("FechaEmision", typeof(string)),
        new("Numero", typeof(int)),
        new("Nombre", typeof(string)),
        new("ImporteConIva", typeof(decimal)),
        new("ImporteCostoConIva", typeof(decimal)),
        new("Impuestos", typeof(decimal))
    };
    private static readonly LegacyExcelColumn[] HojaCobroTotalCascosExcelColumns =
    {
        new("FechaInicial", typeof(string)),
        new("FechaFinal", typeof(string)),
        new("FechaCobro", typeof(string)),
        new("FolioCobro", typeof(int)),
        new("Repartidor", typeof(string)),
        new("CantidadG1", typeof(int)),
        new("CantidadG2", typeof(int)),
        new("CantidadG3", typeof(int)),
        new("CantidadG4", typeof(int)),
        new("CantidadG5", typeof(int)),
        new("CantidadG6", typeof(int)),
        new("CantidadG7", typeof(int)),
        new("CantidadMoto", typeof(int)),
        new("TotalCascos", typeof(decimal)),
        new("Cancelada", typeof(string))
    };
    private static readonly LegacyExcelColumn[] VentasFacturasExcelColumns =
    {
        new("No", typeof(int)),
        new("FechaInicial", typeof(string)),
        new("FechaFinal", typeof(string)),
        new("Usuario", typeof(string)),
        new("Tipo", typeof(string)),
        new("Folio", typeof(string)),
        new("Uuid", typeof(string)),
        new("Fecha", typeof(string)),
        new("Numero", typeof(int)),
        new("NombreCliente", typeof(string)),
        new("NombreFacturacion", typeof(string)),
        new("RFC", typeof(string)),
        new("RegimenFiscal", typeof(string)),
        new("UsoCFDI", typeof(string)),
        new("FormaPago", typeof(string)),
        new("MetodoPago", typeof(string)),
        new("SubTotal", typeof(decimal)),
        new("Iva", typeof(decimal)),
        new("TotalFacturado", typeof(decimal)),
        new("DineroRemision", typeof(decimal)),
        new("CascosRemision", typeof(decimal)),
        new("TotalRemision", typeof(decimal)),
        new("FoliosRelacionados", typeof(string)),
        new("DineroRelacionados", typeof(string)),
        new("CascosRelacionados", typeof(string)),
        new("TotalPagarRelacionados", typeof(string)),
        new("TotalRemisionesDinero", typeof(decimal)),
        new("TotalRemisionesCascos", typeof(decimal)),
        new("TotalRemisionesTotalPagar", typeof(decimal)),
        new("NumRemisiones", typeof(int)),
        new("TotalComplementos", typeof(decimal)),
        new("FechaCancelacion", typeof(string)),
        new("UsuarioCancelacion", typeof(string)),
        new("EquipoCancelacion", typeof(string))
    };
    private static readonly LegacyExcelColumn[] CompraAcumuladoresExcelColumns =
    {
        new("No", typeof(int)),
        new("FechaInicial", typeof(string)),
        new("FechaFinal", typeof(string)),
        new("Empresa", typeof(string)),
        new("Almacen", typeof(string)),
        new("Proveedor", typeof(string)),
        new("Categoria", typeof(string)),
        new("Marca", typeof(string)),
        new("Clave", typeof(string)),
        new("UnidadMedida", typeof(string)),
        new("Compras", typeof(decimal))
    };
    private static readonly LegacyExcelColumn[] CompraProductosExcelColumns =
    {
        new("No", typeof(int)),
        new("FechaInicial", typeof(string)),
        new("FechaFinal", typeof(string)),
        new("Empresa", typeof(string)),
        new("Almacen", typeof(string)),
        new("Proveedor", typeof(string)),
        new("Categoria", typeof(string)),
        new("SubCategoria", typeof(string)),
        new("Marca", typeof(string)),
        new("Clave", typeof(string)),
        new("UnidadMedida", typeof(string)),
        new("Compras", typeof(decimal))
    };
    private static readonly LegacyExcelColumn[] ComprasFacturasExcelColumns =
    {
        new("No", typeof(int)),
        new("PorFecha", typeof(string)),
        new("FechaInicial", typeof(string)),
        new("FechaFinal", typeof(string)),
        new("Estatus", typeof(string)),
        new("Empresa", typeof(string)),
        new("Usuario", typeof(string)),
        new("FechaCaptura", typeof(string)),
        new("FechaDocumento", typeof(string)),
        new("Factura", typeof(string)),
        new("Proveedor", typeof(string)),
        new("Productos", typeof(int)),
        new("Importe", typeof(decimal)),
        new("Bonificacion", typeof(decimal)),
        new("Subtotal", typeof(decimal)),
        new("Iva", typeof(decimal)),
        new("TotalPagar", typeof(decimal)),
        new("Cancelada", typeof(string))
    };
    private static readonly LegacyExcelColumn[] VentasConcentradosDetalleExcelColumns =
    {
        new("No", typeof(int)),
        new("FechaInicial", typeof(string)),
        new("FechaFinal", typeof(string)),
        new("FolioConcentrado", typeof(int)),
        new("UsuarioConcentrado", typeof(string)),
        new("FechaConcentrado", typeof(string)),
        new("Repartidor", typeof(string)),
        new("Empresa", typeof(string)),
        new("FolioNota", typeof(string)),
        new("FechaNota", typeof(string)),
        new("Cliente", typeof(string)),
        new("NumProductos", typeof(int)),
        new("PesoKg", typeof(decimal)),
        new("TotalPagar", typeof(decimal)),
        new("Saldo", typeof(decimal)),
        new("DiasVencidos", typeof(int)),
        new("FechaCancelacionConcentrado", typeof(string))
    };
    private static readonly LegacyExcelColumn[] InventarioActualExcelColumns =
    {
        new("No", typeof(int)),
        new("Fecha", typeof(string)),
        new("Almacen", typeof(string)),
        new("Categoria", typeof(string)),
        new("SubCategoria", typeof(string)),
        new("Marca", typeof(string)),
        new("Clave", typeof(string)),
        new("ClaveClarios", typeof(string)),
        new("UnidadMedida", typeof(string)),
        new("Existencia", typeof(decimal)),
        new("TipoUsado", typeof(string))
    };
    private static readonly LegacyExcelColumn[] InventarioActualCostoExcelColumns =
    {
        new("No", typeof(int)),
        new("Fecha", typeof(string)),
        new("Almacen", typeof(string)),
        new("Categoria", typeof(string)),
        new("SubCategoria", typeof(string)),
        new("Marca", typeof(string)),
        new("Clave", typeof(string)),
        new("UnidadMedida", typeof(string)),
        new("Existencia", typeof(decimal)),
        new("PrecioLista", typeof(decimal)),
        new("CostoUnitario", typeof(decimal)),
        new("CostoTotal", typeof(decimal))
    };
    private static readonly LegacyExcelColumn[] InventarioFaltanteExcelColumns =
    {
        new("No", typeof(int)),
        new("FechaInicial", typeof(string)),
        new("FechaFinal", typeof(string)),
        new("Almacen", typeof(string)),
        new("Categoria", typeof(string)),
        new("SubCategoria", typeof(string)),
        new("Marca", typeof(string)),
        new("Clave", typeof(string)),
        new("UnidadMedida", typeof(string)),
        new("Inventario", typeof(decimal)),
        new("Ventas", typeof(decimal)),
        new("Faltante", typeof(decimal)),
        new("Litros", typeof(decimal)),
        new("Prioridad", typeof(string)),
        new("PesoKg", typeof(decimal)),
        new("ProductosPesoKg", typeof(decimal)),
        new("TotalKg", typeof(decimal)),
        new("FechaUltimaVenta", typeof(string)),
        new("FechaPenultimaVenta", typeof(string))
    };
    private static readonly LegacyExcelColumn[] AjustesInventarioExcelColumns =
    {
        new("No", typeof(int)),
        new("FechaInicial", typeof(string)),
        new("FechaFinal", typeof(string)),
        new("Fecha", typeof(string)),
        new("Almacen", typeof(string)),
        new("Categoria", typeof(string)),
        new("SubCategoria", typeof(string)),
        new("Marca", typeof(string)),
        new("Clave", typeof(string)),
        new("UnidadMedida", typeof(string)),
        new("Moviento", typeof(string)),
        new("ExistenciaInicial", typeof(decimal)),
        new("Entrada", typeof(decimal)),
        new("Salida", typeof(decimal)),
        new("ExistenciaFinal", typeof(decimal)),
        new("Motivo", typeof(string)),
        new("FechaCancelacion", typeof(string))
    };
    private static readonly LegacyExcelColumn[] MovimientosAlmacenExcelColumns =
    {
        new("FechaInicial", typeof(string)),
        new("FechaFinal", typeof(string)),
        new("Categoria", typeof(string)),
        new("SubCategoria", typeof(string)),
        new("Marca", typeof(string)),
        new("Clave", typeof(string)),
        new("PrecioLista", typeof(decimal)),
        new("PesoKg", typeof(decimal)),
        new("TipoCasco", typeof(string)),
        new("Compras", typeof(int)),
        new("Ventas", typeof(int)),
        new("Garantias", typeof(int)),
        new("Devoluciones", typeof(int)),
        new("AjustesEntrada", typeof(int)),
        new("AjustesSalida", typeof(int))
    };
    private static readonly LegacyExcelColumn[] PedidoInventarioExcelColumns =
    {
        new("No", typeof(int)),
        new("Fecha", typeof(string)),
        new("Almacen", typeof(string)),
        new("Categoria", typeof(string)),
        new("SubCategoria", typeof(string)),
        new("Marca", typeof(string)),
        new("Clave", typeof(string)),
        new("UnidadMedida", typeof(string)),
        new("Inventario", typeof(decimal)),
        new("Ventas", typeof(decimal)),
        new("Faltante", typeof(decimal)),
        new("Litros", typeof(decimal)),
        new("Prioridad", typeof(string)),
        new("Pedido", typeof(decimal))
    };
    private static readonly LegacyExcelColumn[] VentasCobranzaDetalladoZaraExcelColumns =
    {
        new("No", typeof(int)),
        new("FechaInicial", typeof(string)),
        new("FechaFinal", typeof(string)),
        new("Agente", typeof(string)),
        new("Estatus", typeof(string)),
        new("Empresa", typeof(string)),
        new("Folio", typeof(string)),
        new("FechaEmision", typeof(string)),
        new("FechaVencimiento", typeof(string)),
        new("Numero", typeof(int)),
        new("Nombre", typeof(string)),
        new("Dinero", typeof(decimal)),
        new("Cargos", typeof(decimal)),
        new("Abonos", typeof(decimal)),
        new("Descuentos", typeof(decimal)),
        new("SaldoDinero", typeof(decimal)),
        new("Cascos", typeof(decimal)),
        new("PagosCascos", typeof(decimal)),
        new("PagosDiferenciaCascos", typeof(decimal)),
        new("SaldoCascos", typeof(decimal)),
        new("DiasVencimiento", typeof(int)),
        new("FechaPago", typeof(string))
    };
    private static readonly LegacyExcelColumn[] VentasCobranzaPagadasDetalladoZaraExcelColumns =
    {
        new("No", typeof(int)),
        new("FechaInicial", typeof(string)),
        new("FechaFinal", typeof(string)),
        new("Agente", typeof(string)),
        new("Estatus", typeof(string)),
        new("Empresa", typeof(string)),
        new("Folio", typeof(string)),
        new("FechaEmision", typeof(string)),
        new("FechaVencimiento", typeof(string)),
        new("Numero", typeof(int)),
        new("Nombre", typeof(string)),
        new("DineroConIva", typeof(decimal)),
        new("DineroSinIva", typeof(decimal)),
        new("FechaPago", typeof(string))
    };
    private static readonly LegacyExcelColumn[] VentasCobranzaPagadasTotalizadoZaraExcelColumns =
    {
        new("FechaInicial", typeof(string)),
        new("FechaFinal", typeof(string)),
        new("NumeroAgente", typeof(int)),
        new("NombreAgente", typeof(string)),
        new("Dinero", typeof(decimal)),
        new("DineroSinIva", typeof(decimal))
    };
    private static readonly LegacyExcelColumn[] VentasCobranzaDesglosadoExcelColumns =
    {
        new("No", typeof(int)),
        new("FechaReporte", typeof(string)),
        new("Agente", typeof(string)),
        new("Empresa", typeof(string)),
        new("FechaEmision", typeof(string)),
        new("FechaVencimiento", typeof(string)),
        new("Folio", typeof(string)),
        new("Cliente", typeof(string)),
        new("Dinero", typeof(decimal)),
        new("Cargos", typeof(decimal)),
        new("Abonos", typeof(decimal)),
        new("Descuentos", typeof(decimal)),
        new("SaldoDinero", typeof(decimal)),
        new("Cascos", typeof(decimal)),
        new("PagosCascos", typeof(decimal)),
        new("PagosDiffCascos", typeof(decimal)),
        new("SaldoCasco", typeof(decimal)),
        new("SaldoTotal", typeof(decimal)),
        new("DiasVencimientoCredito", typeof(int)),
        new("Estatus", typeof(string))
    };
}
