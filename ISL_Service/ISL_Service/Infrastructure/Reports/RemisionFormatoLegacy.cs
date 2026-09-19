using System.Globalization;

namespace ISL_Service.Infrastructure.Reports;

/*
  EL FORMATO DE LOS NUMEROS DE LA REMISION, TAL CUAL LO HACE LEGACY

  Es la copia de Funciones.FormateaNumero de
  Legacy/MacServicios2/Mac3Servicios/Utils/Funciones.cs. Se copia en vez de
  "mejorarse" a proposito: la remision es el papel que se le entrega al
  cliente, y cualquier diferencia de redondeo o de separador se convierte en
  una diferencia con lo que imprime Mac31.

  Dos rarezas que PARECEN errores y no se corrigen:
    - un cero se imprime VACIO (no "0.00"), porque asi se ve en el papel de
      Mac31: las columnas sin importe salen en blanco.
    - -99999 es el "sin dato" de la base y se imprime como cero.

  LA CULTURA ES es-MX A PROPOSITO

  Legacy no fija cultura: toma la del servidor, que es Windows en espanol de
  Mexico. Si aqui se dejara la del proceso, en un servidor en ingles las fechas
  saldrian 09/19/2026 y los decimales con otro separador. Se fija explicita
  para que el papel no dependa de como quedo instalado el Windows.
*/
public static class RemisionFormatoLegacy
{
    public static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-MX");

    /// Convierte el valor crudo de una columna a texto igual que lo hace legacy.
    public static string ValorDeColumna(object? valor)
    {
        if (valor is null || valor is DBNull)
            return "";

        return valor switch
        {
            string s => s,
            IFormattable f => f.ToString(null, Cultura),
            _ => valor.ToString() ?? ""
        };
    }

    public static string FormateaNumero(string dataType, string valor)
    {
        return FormateaNumero(dataType, valor, false);
    }

    public static string FormateaNumero(string dataType, string valor, bool esTotales)
    {
        if (dataType == "Int32" && valor == "")
            return "0";

        if (dataType == "Decimal" && valor == "")
            return "0.00";

        if (valor == "-99999")
            return "0";
        if (valor == "-99999.00")
            return "0.00";

        if (valor == "0")
            return esTotales ? valor : "";

        if (valor == "0.00" || valor == "0.0000")
            return esTotales ? valor : "";

        if (dataType == "Int32")
            return int.Parse(valor, Cultura).ToString("###,##0", Cultura);

        if (dataType == "Single")
            return double.Parse(valor, Cultura).ToString("###,##0.00", Cultura);

        if (dataType == "Decimal")
        {
            var result = TruncateDecimal(decimal.Parse(valor, Cultura), 2);
            return result.ToString("###,##0.00", Cultura);
        }

        return valor;
    }

    /*
      TRUNCA, NO REDONDEA

      Es lo que hace legacy. Cambiarlo a Math.Round moveria centavos en el
      total de la remision.
    */
    public static decimal TruncateDecimal(decimal value, int precision)
    {
        var step = (decimal)Math.Pow(10, precision);
        var tmp = Math.Truncate(step * value);
        return tmp / step;
    }
}
