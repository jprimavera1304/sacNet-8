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
}
