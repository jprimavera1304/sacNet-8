namespace ISL_Service.Application.Reportes;

/// <summary>
/// LOS FILTROS QUE EL SP NO SABE DAR VACIOS, Y QUE MAC31 SI EXIGE.
///
/// Dos reportes del menu vivo figuraban como "roto en la base" y no lo estaban:
/// estaban mal pedidos. Su <c>sp_n_</c> arma SQL dinamico con la lista que le
/// llega y, si llega vacia, se rompe solo:
///
/// <list type="bullet">
///   <item>
///     <b>106 Clientes CON Compra Por DIA</b> sin dias:
///     <c>SUBSTRING(@DiasSemana, 1, LEN(@DiasSemana)-2)</c> con
///     <c>@DiasSemana = ''</c> -> <i>"Invalid length parameter passed to the LEFT
///     or SUBSTRING function"</i> (sp_n_rptClienteConCompraPorDiaGlobales, la
///     linea del SUBSTRING).
///   </item>
///   <item>
///     <b>1245 Listas de Precios (productos)</b> sin subcategorias:
///     <c>AND C.IDCategoria IN ( )</c> -> <i>"Incorrect syntax near ')'"</i>
///     (sp_n_rptListasPreciosProductos, el INSERT dinamico a
///     #ListasPreciosDetalleTmp).
///   </item>
/// </list>
///
/// Mac31 nunca llega ahi porque su forma lo impide ANTES de pedir el reporte:
/// <c>"SELECCIONE LOS DÍAS."</c> (ReportesMenu.cs, el case
/// ClientesConCompraPorDia) y <c>"SELECCIONE UNA SUBCATEGORÍA."</c>
/// (ReportesMenu.cs, <c>Seleccionados(ref IDsCategoria, ...)</c> dentro de
/// GenerarReporte).
///
/// Sin esta guarda el backend contestaba <b>500 "Error de base de datos"</b>, que
/// le echa la culpa a la base de un dato que falta en la peticion, y por eso los
/// dos quedaron apuntados como defecto heredado en la auditoria. Con el filtro
/// puesto los dos entregan PDF e IGUALAN a Legacy renglon por renglon.
///
/// EL 1245 ES EL UNICO DE SU FAMILIA QUE LO NECESITA: el 1240 y las cuatro listas
/// personalizadas de Tauro si saben contestar con la lista vacia, asi que la
/// guarda mira la CATEGORIA (acumuladores o no), que es lo mismo que mira el
/// resolver para elegir entre el 1245 y los otros, y no la clave.
/// </summary>
public static class FiltrosObligatoriosDeReporte
{
    /// <summary>La categoria "acumuladores", la que NO cae en el 1245.</summary>
    private const int GrupoCategoriaAcumuladores = 1;

    /// <summary>
    /// El mensaje de por que no se puede pedir asi, o <c>null</c> si si se puede.
    /// </summary>
    /// <param name="reporteKey">La clave del catalogo que mando el front.</param>
    /// <param name="idGrupoCategoria">La categoria pedida (1 = acumuladores).</param>
    /// <param name="diasSemana">Cuantos dias de la semana vienen marcados.</param>
    /// <param name="subcategorias">Cuantas subcategorias vienen marcadas.</param>
    public static string? QueFalta(
        string? reporteKey,
        int idGrupoCategoria,
        int diasSemana,
        int subcategorias)
    {
        var clave = (reporteKey ?? "").Trim().ToLowerInvariant();

        if (clave == "clientes_con_compra_por_dia" && diasSemana == 0)
        {
            return "Selecciona los dias: este reporte es por dia de la semana. "
                 + "Manda al menos uno en idDiasSemana (1 lunes - 6 sabado).";
        }

        if ((clave == "listas_de_precios" || clave == "listas_de_precios_imp")
            && idGrupoCategoria != GrupoCategoriaAcumuladores
            && subcategorias == 0)
        {
            return "Selecciona una subcategoria: la lista de precios de productos "
                 + "se arma por subcategoria. Manda al menos una en idSubcategorias.";
        }

        return null;
    }
}
