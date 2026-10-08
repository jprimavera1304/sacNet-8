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
    public void Catalogo_Tiene_Los_60_Reportes_Con_Clave_Unica()
    {
        // 53 + los cuatro que la ola 7 saco del hueco: garantias_registradas (1236),
        // centros_pagos_entradas_salidas (1415), centros_garantias (1420) y
        // control_acumulado_de_estimaciones (1250).
        //
        // + autos (1300), que no vive en el arbol de Reportes de Mac31 sino en el
        // boton IMPRIMIR de la pantalla de catalogo ConsultarAutos. Es el mismo
        // camino -sp_ConsultaReportes da el SP y la plantilla- asi que entra aqui y
        // no en un segundo catalogo.
        //
        // + cambios_vigencia (1535), por lo mismo que autos: lo imprime el boton
        // IMPRIMIR de ConsultarCambiosVigencia y no un nodo del arbol.
        //
        // + liquidaciones_descuentos (1030), por lo mismo: boton IMPRIMIR de
        // ConsultarLiquidacionesDescuentos.
        Assert.Equal(60, ReportesCatalogo.Reportes.Count);
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
    // Y el sexto, que entro con la ola 7: su permiso por regla seria
    // reportes.administracion.control_acumulado_de_estimaciones.ver y tampoco esta.
    [InlineData("control_acumulado_de_estimaciones")]
    // Y cambios_vigencia (1535): comprobado en MacZ el 07-10-2026, en WPermiso
    // solo existen cambiosvigencia.{ver_modulo, ver, crear, editar, cancelar} -los
    // de la pantalla- y ninguna clave de reportes que lo mencione. No se le cuelga
    // cambiosvigencia.ver: exigiria los dos permisos a la vez.
    [InlineData("cambios_vigencia")]
    [InlineData("liquidaciones_descuentos")]
    public void Reportes_Sin_Permiso_No_Se_Bloquean(string clave)
    {
        Assert.NotNull(ReportesCatalogo.Buscar(clave));
        Assert.Null(ReportesCatalogo.PermisoDe(clave));
    }

    [Fact]
    public void Un_Permiso_Solo_Se_Comparte_Cuando_MAC31_Comparte_El_NODO()
    {
        /*
          Regla: un permiso por reporte. La unica excepcion es el nodo
          "Movimientos Centros" de Mac31, que al imprimirse genera TRES reportes
          (1410, 1415 y 1420) con el mismo clic; inventarles dos permisos nuevos
          seria poder apagar en el web algo que alla no se puede apagar aparte.
        */
        var compartidos = ReportesCatalogo.Reportes
            .Where(x => x.Permiso is not null)
            .GroupBy(x => x.Permiso!, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(new[] { "reportes.ventas.movimientos_centros.ver" }, compartidos);
        Assert.Equal(
            3,
            ReportesCatalogo.Reportes.Count(x => x.Permiso == "reportes.ventas.movimientos_centros.ver"));
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
