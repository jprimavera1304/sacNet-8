using ISL_Service.Application.Reportes;
using Xunit;

namespace ISL_Service.Tests;

/// <summary>
/// El catalogo de reportes es ahora el unico dueño del mapeo reporte->permiso.
/// Si alguien le cambia una clave sin querer, el web y la app quedan ofreciendo
/// reportes que la API va a rebotar con 403; estas pruebas son el seguro.
/// </summary>
public class ReportesCatalogoTests
{
    [Fact]
    public void Catalogo_Tiene_Los_53_Reportes_Con_Clave_Unica()
    {
        Assert.Equal(53, ReportesCatalogo.Reportes.Count);
        Assert.Equal(
            ReportesCatalogo.Reportes.Count,
            ReportesCatalogo.Reportes.Select(x => x.Clave).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Solo_Existen_Los_Cuatro_Grupos_Del_Menu()
    {
        var grupos = ReportesCatalogo.Reportes.Select(x => x.Grupo).Distinct().OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "administracion", "compras", "inventario", "ventas" }, grupos);
    }

    [Theory]
    // La regla general: reportes.{grupo}.{clave}.ver
    [InlineData("acumuladores_y_productos", "reportes.ventas.acumuladores_y_productos.ver")]
    [InlineData("folios", "reportes.ventas.folios.ver")]
    [InlineData("garantias", "reportes.ventas.garantias.ver")]
    [InlineData("asistencias", "reportes.administracion.asistencias.ver")]
    [InlineData("movimientos", "reportes.inventario.movimientos.ver")]
    // Las excepciones, cada una con su evidencia en ReportesMenu.cs (legacy).
    [InlineData("transferencias_duplicadas", "reportes.ventas.pagos_transferencias_duplicadas.ver")]
    [InlineData("transferencias_folios", "reportes.ventas.transferencias_folio.ver")]
    [InlineData("clientes_con_descuentos", "reportes.ventas.clientes_descuentos.ver")]
    [InlineData("compras_acumuladores_y_productos", "reportes.compras.acumuladores_y_productos.ver")]
    [InlineData("compras_facturas", "reportes.compras.facturas.ver")]
    [InlineData("inventario_actual", "reportes.inventario.actual.ver")]
    [InlineData("inventario_faltante", "reportes.inventario.faltante.ver")]
    public void PermisoDe_Devuelve_La_Clave_Que_Existe_En_WPermiso(string clave, string permiso)
    {
        Assert.Equal(permiso, ReportesCatalogo.PermisoDe(clave));
    }

    [Theory]
    // Estos cinco NO tienen permiso en WPermiso y no se les invento uno:
    // quedan abiertos a quien tenga reportes.ver_modulo, como estaban antes.
    [InlineData("cobranza_pagadas")]
    [InlineData("transferencias_clientes")]
    [InlineData("clientes_sin_descuentos")]
    [InlineData("liquidaciones")]
    [InlineData("cobros_dinero_y_cascos")]
    public void Reportes_Sin_Permiso_No_Se_Bloquean(string clave)
    {
        Assert.NotNull(ReportesCatalogo.Buscar(clave));
        Assert.Null(ReportesCatalogo.PermisoDe(clave));
    }

    [Fact]
    public void Ningun_Permiso_Se_Usa_En_Dos_Reportes()
    {
        var permisos = ReportesCatalogo.Reportes
            .Where(x => x.Permiso is not null)
            .Select(x => x.Permiso!)
            .ToArray();

        Assert.Equal(permisos.Length, permisos.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Ningun_Reporte_Exige_El_Permiso_Maestro_Del_Modulo()
    {
        // ver_modulo es el portero de entrada, no el candado de un reporte.
        Assert.DoesNotContain(
            ReportesCatalogo.PermisoModulo,
            ReportesCatalogo.Reportes.Select(x => x.Permiso));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("reporte_que_no_existe")]
    public void Clave_Vacia_O_Desconocida_No_Encuentra_Reporte_Ni_Permiso(string? clave)
    {
        Assert.Null(ReportesCatalogo.Buscar(clave));
        Assert.Null(ReportesCatalogo.PermisoDe(clave));
    }

    [Fact]
    public void La_Clave_Se_Busca_Sin_Importar_Mayusculas_Ni_Espacios()
    {
        Assert.Equal(
            "reportes.ventas.folios.ver",
            ReportesCatalogo.PermisoDe("  FOLIOS "));
    }
}
