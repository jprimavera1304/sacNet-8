namespace ISL_Service.Application.Reportes;

/// <summary>
/// EL NODO "Cobranza" NO ES EL MISMO REPORTE EN LAS DOS EMPRESAS.
///
/// Mac31 manda el <b>1160</b> en las dos, pero el servicio de Legacy lo cambia
/// por el <b>1161</b> cuando la empresa es Zaragoza, justo antes de elegir la
/// plantilla (<c>MacServicios2/.../ReporteV2Service.cs:783</c>):
///
/// <code>
/// if ((funcionalidad.ToUpper() == "ZARA") &amp;&amp;
///     (IDReporte == (int)enumReportes.VentasCobranzaDetallado))
/// {
///     reporteInputDTO.idReporte = (int)enumReportes.VentasCobranzaDetalladoZara;
///     IDReporte = (int)enumReportes.VentasCobranzaDetalladoZara;
/// }
/// </code>
///
/// Es la unica rama por empresa que le queda viva a <c>ReporteV2Service.cs</c>
/// (la de Listas de Precios Tauro esta comentada), y era la unica que faltaba
/// copiar al backend nuevo. El procedimiento devuelve lo MISMO en los dos casos
/// —no mira el id—, asi que lo unico que cambiaba era la PLANTILLA: la del 1160
/// tiene cuatro columnas de importes y la del 1161 tiene once. Midiendo el mismo
/// renglon de <c>dbo.Parametros</c> en Zaragoza, el backend nuevo imprimia 1 298
/// importes contra los 6 734 de Legacy, y 46 de los suyos eran numeros que
/// Legacy no tiene: el dato correcto siempre estuvo ahi, se estaba vaciando en el
/// molde equivocado.
///
/// Esta aparte del repositorio para poder probarla sin levantar DI ni base, igual
/// que <see cref="FiltrosObligatoriosDeReporte"/>.
/// </summary>
public static class ReporteCobranzaPorEmpresa
{
    /// El 1160: el nodo "Cobranza" de Tauro.
    public const int Tauro = 1160;

    /// El 1161: el mismo nodo en Zaragoza, con su propia plantilla.
    public const int Zaragoza = 1161;

    /// <summary>
    /// El <c>IDReporte</c> que le toca a la clave <c>cobranza</c> segun
    /// <c>Constantes.Funcionalidad</c>.
    /// </summary>
    /// <remarks>
    /// LA COMPARACION ES POR IGUALDAD EXACTA CON "ZARA", NO "no es Tauro" NI
    /// "contiene ZARA", porque asi la hace Legacy. Con un "no es Tauro" una
    /// tercera empresa —MacL, con otra funcionalidad— caeria del lado de Zaragoza
    /// sin que Legacy lo haga, y se imprimiria con la plantilla equivocada por el
    /// otro lado. Lo que si se perdona es el espacio de sobra y la caja, porque
    /// el valor sale de una columna de texto escrita a mano.
    /// </remarks>
    public static int Resolver(string? funcionalidad)
    {
        var f = (funcionalidad ?? string.Empty).Trim().ToUpperInvariant();
        return f == "ZARA" ? Zaragoza : Tauro;
    }
}
