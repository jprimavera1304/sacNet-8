namespace ISL_Service.Application.Services;

/*
  LAS TABLAS DE AVIACION QUE HAY QUE SINCRONIZAR, Y CUALES DESPUES DE CADA CAMBIO.

  Los numeros son los de Enums.enumAviacionTablas de Legacy
  (Utils/Enums.cs:11-45). No son correlativos ni empiezan en 1: son los ids con
  los que sp_n_SincronizarDownload identifica cada tabla, y se copian tal cual
  porque son datos de la base, no una eleccion de este backend.

  La LISTA por operacion tambien se copia de Legacy, una por una, porque no es
  deducible: cambiar una cuenta arrastra las amortizaciones de los economicos
  (que dependen del importe de la cuenta) y dar de alta un economico arrastra el
  catalogo de permisionarios (porque el alta puede crear el renglon del
  permisionario en la replica). Si se sincroniza de menos, la caja cobra con el
  catalogo viejo; si se sincroniza de mas, se tarda. Las dos cosas se notan.

  Y se hace SOLO si esta instalacion es una caja (Constantes.AviacionIDCaja <> 0),
  igual que Legacy: en el central no hay replica a la que bajar nada.
*/
public static class TablasDeAviacion
{
    public const int CatalogoPermisionarios = 3;
    public const int CatalogoCuentas = 5;
    public const int AviacionTarjetaPorcentajes = 6;
    public const int CatalogoEconomicos = 7;
    public const int CatalogoEconomicosHis = 8;
    public const int EconomicosAmortizaciones = 9;
    public const int CatalogoMotivosFianza = 10;
    public const int OperadoresFianzas = 11;
    public const int OperadoresCuentas = 12;
    public const int EconomicosAuxiliares = 13;
    public const int CatalogoOperadores = 14;

    // --- Cuentas ---
    public static readonly int[] AltaDeCuenta = { CatalogoCuentas, EconomicosAmortizaciones, EconomicosAuxiliares };
    public static readonly int[] CambioDeCuenta = { CatalogoCuentas, EconomicosAmortizaciones };
    public static readonly int[] AceptacionDeCuenta = { CatalogoCuentas };
    public static readonly int[] CambioDeTarjetaPorcentajes = { AviacionTarjetaPorcentajes };

    // --- Economicos ---
    public static readonly int[] AltaDeEconomico = { CatalogoPermisionarios, CatalogoEconomicos, CatalogoEconomicosHis, EconomicosAmortizaciones };
    public static readonly int[] CambioDeEconomico = { CatalogoPermisionarios, CatalogoEconomicos, EconomicosAmortizaciones };
    public static readonly int[] CambioDeAmortizacion = { EconomicosAmortizaciones };
    public static readonly int[] ReasignacionDeEconomico = { CatalogoEconomicos, CatalogoEconomicosHis, CatalogoPermisionarios, EconomicosAmortizaciones };
    public static readonly int[] AceptacionDeEconomico = { CatalogoEconomicos };

    // --- Motivos de fianza ---
    public static readonly int[] CambioDeMotivoFianza = { CatalogoMotivosFianza };

    // --- Operadores ---
    public static readonly int[] AltaDeOperador = { CatalogoOperadores, OperadoresFianzas };
    public static readonly int[] CambioDeOperador = { CatalogoOperadores };
    public static readonly int[] AceptacionDeOperador = { CatalogoOperadores };
    public static readonly int[] CambioDeFianzaDeOperador = { OperadoresFianzas };
    public static readonly int[] CambioDeCuentaDeOperador = { OperadoresCuentas };

    // --- Permisionarios ---
    public static readonly int[] CambioDePermisionario = { CatalogoPermisionarios };

    /// <summary>
    /// Conceptos, cajas e impresoras NO se sincronizan: sus servicios de Legacy
    /// no llaman a sp_n_SincronizarDownload ni una vez (comprobado). Se deja
    /// escrito para que nadie "complete la simetria" anadiendolo.
    /// </summary>
    public static readonly int[] NoSeSincroniza = Array.Empty<int>();
}
