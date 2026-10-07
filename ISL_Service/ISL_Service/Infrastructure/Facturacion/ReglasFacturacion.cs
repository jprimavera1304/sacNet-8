using System.Globalization;

namespace ISL_Service.Infrastructure.Facturacion;

/*
  LAS REGLAS DE FACTURACION QUE VIVEN EN C# Y NO EN NINGUN PROCEDIMIENTO

  Todo lo de este archivo son funciones puras: entran cadenas y salen cadenas. Se
  separan asi a proposito, porque son la parte de la facturacion que SI se puede
  probar sin base, sin red y sin timbrar un CFDI de verdad — y es tambien la parte
  donde legacy tiene la logica mas fragil del sistema.

  1) EL CANDADO ANTIPRODUCCION
  ---------------------------
  `Constantes.FacturapiSecretKey` no guarda el secreto de Facturapi: guarda el
  secreto CON EL NOMBRE DEL EJECUTABLE EMBEBIDO. Antes de timbrar, legacy exige
  que el key contenga `AppMac` sin ".exe", y despues le QUITA ese trozo para
  obtener el secreto real:

      if (!key.Contains(appMac.Replace(".exe",""))) -> "KEY PARA FACTURAR NO COINCIDE."
      key = key.Replace(appMac.Replace(".exe",""), "");

  Es un candado casero y es el unico que impide que una instalacion de pruebas
  timbre contra el Facturapi de produccion: si alguien copia la base de produccion
  a una maquina de pruebas, el AppMac de esa maquina no coincide y no se timbra.
  Rustico, pero es lo que ha evitado el accidente, asi que se respeta tal cual —
  incluido el mensaje literal, que es el que la gente de soporte reconoce.

  2) LA SERIE
  -----------
  Sale de comparar el key contra `AppMac` con una cadena de `else if`. El codigo de
  legacy escribe `FacturapiSecretKey.Contains(AppMac.Replace("MacT.exe", ""))`, que
  solo quita algo cuando AppMac ES "MacT.exe"; en cualquier otro caso compara
  contra el AppMac completo. Es decir: la rama que gana es la del ejecutable que
  de verdad esta corriendo, y las demas comparan el key contra un nombre que no es
  el suyo. Se reproduce con la misma forma, porque de la serie depende el FOLIO
  FISCAL: una serie distinta son facturas con otra numeracion ante el SAT, y eso
  no se arregla despues.

  3) EL FOLIO
  -----------
  Viene del folio de la remision, que puede traer prefijo: "A-1234" se parte en
  serie "A" (que se PEGA a la serie base) y folio 1234. Sin guion, todo es folio.

  4) EL @Params DE sp_n_ActualizarParametro
  -----------------------------------------
  Veinte columnas de texto donde se guardan los datos del CFDI para que el reporte
  los vuelva a leer. Varias llevan DENTRO varios campos pegados con "@@@@" —cuatro
  arrobas, aunque el comentario de legacy diga cinco— y se cruzan por posicion al
  leerlos. Es un formato posicional y por eso esta aqui, con una funcion por
  columna y probado.
*/
public static class ReglasFacturacion
{
    /// <summary>
    /// El delimitador de los campos que van pegados dentro de un Param.
    /// Son CUATRO arrobas: el comentario de legacy dice cinco y el codigo escribe
    /// cuatro. Manda el codigo, porque es lo que hay escrito en la base.
    /// </summary>
    public const string DelimitadorParams = "@@@@";

    public const string MensajeKeyNoCoincide = "KEY PARA FACTURAR NO COINCIDE.";
    public const string MensajeKeyIncorrecto = "KEY PARA FACTURAR ES INCORRECTO.";

    /// <summary>
    /// Comprueba el candado y devuelve el secreto real de Facturapi.
    /// `null` cuando el candado no pasa: quien llama contesta con el mensaje
    /// literal que corresponda a su pantalla.
    /// </summary>
    public static string? SecretoReal(string? keyGuardado, string? appMac)
    {
        if (string.IsNullOrWhiteSpace(keyGuardado) || string.IsNullOrWhiteSpace(appMac))
            return null;

        var marca = SinExtension(appMac);
        if (marca.Length == 0) return null;
        if (!keyGuardado.Contains(marca, StringComparison.Ordinal)) return null;

        var secreto = keyGuardado.Replace(marca, string.Empty, StringComparison.Ordinal);
        return secreto.Length == 0 ? null : secreto;
    }

    /// <summary>"MacZCS3.exe" -> "MacZCS3". Sin extension no cambia nada.</summary>
    public static string SinExtension(string? appMac)
        => string.IsNullOrWhiteSpace(appMac)
            ? string.Empty
            : appMac.Replace(".exe", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();

    /// <summary>Que clase de documento se esta timbrando.</summary>
    public enum TipoDocumento
    {
        /// <summary>Factura normal, con remision.</summary>
        Factura = 0,

        /// <summary>Factura sin remision (se arma desde un pedido).</summary>
        FacturaSinRemision = 1,

        /// <summary>Nota de credito.</summary>
        NotaCredito = 2,

        /// <summary>Complemento de pago.</summary>
        ComplementoPago = 3
    }

    /// <summary>
    /// La serie, igual que la resuelve legacy.
    ///
    /// El orden de las ramas es el suyo y no se reordena: la primera que acierta
    /// gana, y de la serie depende la numeracion fiscal.
    /// </summary>
    public static string Serie(string? keyGuardado, string? appMac, TipoDocumento tipo)
    {
        var key = keyGuardado ?? string.Empty;

        /* Que sufijo lleva cada ejecutable conocido. */
        var sufijo = SufijoDeEjecutable(key, appMac);

        return tipo switch
        {
            TipoDocumento.Factura => sufijo ?? "Z",
            TipoDocumento.FacturaSinRemision => sufijo is null ? "ZSR" : sufijo + "SR",
            TipoDocumento.NotaCredito => sufijo is null ? "NCZ" : "NC" + sufijo,
            TipoDocumento.ComplementoPago => sufijo == "L" ? "LPAG" : "ZPAG",
            _ => "Z"
        };
    }

    /// <summary>
    /// El sufijo del ejecutable que esta corriendo, o null si no es ninguno de
    /// los conocidos (lo que en legacy cae al "else").
    /// </summary>
    private static string? SufijoDeEjecutable(string key, string? appMac)
    {
        /* El orden es el de legacy, de arriba abajo. */
        var conocidos = new (string Exe, string Sufijo)[]
        {
            ("MacT.exe", "T"),
            ("MacZCS1.exe", "CS1"),
            ("MacZCS2.exe", "CS2"),
            ("MacZCS3.exe", "CS3"),
            ("MacZCS4.exe", "CS4"),
            ("MacZCS5.exe", "CS5"),
            ("MacZCS6.exe", "CS6"),
            ("MacL.exe", "L")
        };

        foreach (var (exe, sufijo) in conocidos)
        {
            /*
              Esta es la comparacion de legacy, con su forma exacta:
              `key.Contains(appMac.Replace(exe, ""))`. Cuando appMac ES ese
              ejecutable, Replace lo deja en "" y Contains("") es SIEMPRE true, asi
              que la rama del ejecutable propio acierta. Cuando no lo es, compara
              el key contra el appMac completo.
            */
            var aguja = (appMac ?? string.Empty).Replace(exe, string.Empty, StringComparison.OrdinalIgnoreCase);
            if (aguja.Length == 0) return sufijo;
            if (key.Contains(aguja, StringComparison.Ordinal)) return sufijo;
        }

        return null;
    }

    /// <summary>
    /// El folio y el prefijo de serie que trae el folio de la remision.
    ///
    /// "A-1234" -> prefijo "A", folio 1234. "1234" -> sin prefijo, folio 1234.
    /// El prefijo se PEGA al final de la serie base, igual que en legacy.
    /// </summary>
    public static (string SerieCompleta, int Folio) SerieYFolio(string serieBase, string? folioRemision)
    {
        var texto = (folioRemision ?? string.Empty).Trim();
        if (texto.Length == 0) return (serieBase, 0);

        var guion = texto.IndexOf('-');
        if (guion < 0)
        {
            return (serieBase, int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var solo) ? solo : 0);
        }

        var prefijo = texto[..guion];
        var resto = texto[(guion + 1)..];
        var folio = int.TryParse(resto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
        return (serieBase + prefijo, folio);
    }

    /// <summary>
    /// La forma de pago con dos digitos, como la pide el SAT: "1" -> "01".
    /// Lo que no es numero se devuelve recortado, para que el error lo diga
    /// Facturapi con su propio mensaje y no se pierda aqui.
    /// </summary>
    public static string FormaPagoSat(string? formaPago)
    {
        var texto = (formaPago ?? string.Empty).Trim();
        if (texto.Length == 0) return string.Empty;
        return int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? n.ToString("00", CultureInfo.InvariantCulture)
            : texto;
    }

    /// <summary>
    /// Los catalogos del SAT que llegan como "G03 - Gastos en general" se mandan
    /// solo con la clave. Legacy parte por el guion y recorta.
    /// </summary>
    public static string ClaveSat(string? valor)
    {
        var texto = (valor ?? string.Empty).Trim();
        if (texto.Length == 0) return string.Empty;
        var guion = texto.IndexOf('-');
        return guion < 0 ? texto : texto[..guion].Trim();
    }

    /// <summary>
    /// Pega varios campos en una columna de Param con el delimitador de cuatro
    /// arrobas. Los nulos entran como cadena vacia: la posicion tiene que
    /// conservarse o el que lee desplaza todo lo demas.
    /// </summary>
    public static string Param(params string?[] campos)
        => string.Join(DelimitadorParams, campos.Select(x => x ?? string.Empty));

    /// <summary>
    /// La cadena del QR de verificacion del SAT, con el formato exacto de legacy.
    ///
    /// `fe` son los OCHO ULTIMOS caracteres del sello del CFDI, y el total va con
    /// seis decimales y sin separador de miles: asi lo pide el SAT y asi lo arma
    /// Mac31. Un formato distinto da un QR que no valida.
    /// </summary>
    public static string CadenaQr(
        string? urlVerificaSat, string? uuid, string? rfcEmisor, string? rfcReceptor,
        decimal total, string? selloCfdi)
    {
        var fe = string.Empty;
        var sello = selloCfdi ?? string.Empty;
        if (sello.Length >= 8) fe = sello[^8..];

        return (urlVerificaSat ?? string.Empty)
            + "id=" + (uuid ?? string.Empty)
            + "&re=" + (rfcEmisor ?? string.Empty)
            + "&rr=" + (rfcReceptor ?? string.Empty)
            + "&tt=" + total.ToString("############0.000000", CultureInfo.InvariantCulture)
            + "&fe=" + fe;
    }

    /// <summary>
    /// La fecha y hora del CFDI con el formato que guarda legacy:
    /// la fecha en ISO y la hora en HH:mm:ss.
    ///
    /// Legacy la arma con ConvertFtmDate sobre ToShortDateString() mas los ocho
    /// primeros caracteres de TimeOfDay.ToString(), lo que depende de la cultura
    /// del servidor y del `formatoFechaDB` de Constantes. Aqui es un solo
    /// ToString con cultura invariante: el mismo instante, sin depender de nada.
    /// </summary>
    public static string FechaHoraCfdi(DateTime? valor)
        => valor is null ? string.Empty : valor.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>
    /// La fecha de pago en el formato que exige el complemento del SAT:
    /// "2024-01-03T21:48:18".
    ///
    /// Legacy la arma con Substring de posiciones fijas sobre la cadena del
    /// cliente mas la hora en otro campo, asi que un dia escrito distinto daba una
    /// fecha de pago distinta en el CFDI. Esto no depende del formato de entrada.
    /// </summary>
    public static string FechaPagoSat(DateTime? valor)
        => valor is null ? string.Empty : valor.Value.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
}
