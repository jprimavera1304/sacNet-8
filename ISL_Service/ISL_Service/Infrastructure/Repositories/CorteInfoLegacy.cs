using System.Globalization;
using System.Text;
using ISL_Service.Application.DTOs.OperacionMac;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// El formato de la cadena CorteInfo que recibe sp_n_InsertarCorte.
///
/// POR QUE ESTO VIVE EN SU PROPIA CLASE PUBLICA
/// -------------------------------------------
/// Porque es la pieza mas fragil de todo el modulo de cortes y la unica que se
/// puede probar sin base de datos. Son VEINTE importes pegados en una sola cadena
/// que el procedimiento vuelve a partir con CHARINDEX, campo por campo, en un
/// orden fijo. Si sobra o falta un delimitador, el procedimiento no falla: lee
/// corrido y mete el efectivo donde iba el total de gastos. Un corte de caja con
/// los importes corridos un lugar cuadra en la pantalla y no cuadra en la
/// contabilidad, y eso se descubre semanas despues.
///
/// Estando aqui, <c>CorteInfoLegacyTests</c> puede comprobar el formato exacto sin
/// tocar la base. Es la misma razon por la que BaseDelCentro y SelloDeCentro son
/// publicos.
///
/// EL FORMATO, LEIDO DEL CUERPO DEL PROCEDIMIENTO
/// --------------------------------------------
/// Veinte campos separados por '@@@@', y dentro del campo 13 las dos tarjetas
/// separadas por '||||'. El ultimo campo va SIN delimitador: el procedimiento se
/// lleva el resto de la cadena como NoAplicadoDesglose.
///
/// El comentario de legacy dice '@@@@@' (cinco arrobas) y es un error del
/// comentario: su codigo y el procedimiento usan cuatro.
/// </summary>
public static class CorteInfoLegacy
{
    public const string Delimitador = "@@@@";
    public const string DelimitadorTarjetas = "||||";

    /// <summary>
    /// Arma la cadena.
    ///
    /// Los importes se escriben con cultura INVARIANTE y dos decimales. Legacy
    /// usaba .ToString() con la cultura del servidor: en una maquina configurada
    /// con coma decimal, un 12.5 se vuelve "12,5" — y como el procedimiento parte
    /// cadenas, un importe se puede leer como dos y correr todo el desglose un
    /// lugar.
    /// </summary>
    public static string Armar(DesgloseCorte d)
    {
        var sb = new StringBuilder();

        void Campo(decimal v) => sb.Append(Numero(v)).Append(Delimitador);

        Campo(d.SaeEsquina);
        Campo(d.SaeEsquinaDomingo);
        Campo(d.SaeLocalTauro);
        Campo(d.TotalSae);
        Campo(d.MacCamioneta);
        Campo(d.MacAgentes);
        Campo(d.TotalMac);
        Campo(d.Transferencias);
        Campo(d.TransferenciasTiendaEsquina);
        Campo(d.TransferenciasLocalTauro);
        Campo(d.DepositoCheque);
        Campo(d.DepositoEfectivo);

        // Campo 13: las dos tarjetas comparten lugar.
        sb.Append(Numero(d.Tarjetas)).Append(DelimitadorTarjetas)
          .Append(Numero(d.TarjetasTiendas)).Append(Delimitador);

        Campo(d.Cheque);
        Campo(d.Efectivo);
        Campo(d.Morraya);
        Campo(d.TotalGastos);
        Campo(d.TotalDesglose);
        Campo(d.GranTotal);

        // El ultimo sin delimitador.
        sb.Append(Numero(d.NoAplicadoDesglose));

        return sb.ToString();
    }

    private static string Numero(decimal valor) => valor.ToString("0.00", CultureInfo.InvariantCulture);
}
