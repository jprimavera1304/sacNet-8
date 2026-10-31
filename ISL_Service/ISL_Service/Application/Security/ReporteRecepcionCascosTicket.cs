using System.Security.Cryptography;
using System.Text;

namespace ISL_Service.Application.Security;

/*
  EL PASE FIRMADO DEL REPORTE DE RECEPCION DE CASCOS

  El PDF lo abre una pestana nueva del navegador, y una pestana nueva no manda
  el encabezado Authorization: no hay forma de que el GET que entrega el papel
  vaya autenticado como el resto de la aplicacion. Lo que autoriza es esto: un
  pase corto, firmado y con caducidad, que viaja en la direccion.

  POR QUE NO SE REUSA ReporteUsadosTicket. Aquel pase lleva estatus, variante y
  una bandera de "por fecha de registro" que aqui no existen, y le falta lo
  unico que este necesita ademas del periodo: el ORIGEN y el AGENTE, que es
  como se pide esta hoja. Meter las dos cosas en el mismo formato obligaria a
  que cada uno entienda los campos del otro, y el dia que uno cambie se rompe
  el otro sin que nada lo avise. La firma se hace igual —HMAC con la llave del
  JWT—, que es lo que de verdad importa que no se separe.

  QUE PROTEGE. Que alguien escriba otro periodo o otro agente en la barra del
  navegador y se saque un papel de algo que no puede ver. El cuerpo va en claro
  —no es secreto, son dos fechas y dos numeros— pero firmado: cambiar un digito
  invalida el pase.

  LOS DIEZ MINUTOS son para que el enlace no sirva de aqui a manana si queda en
  el historial del navegador. Da de sobra para abrir el PDF y no para guardarlo
  como una puerta.
*/
public static class ReporteRecepcionCascosTicket
{
    private static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(10);

    public sealed class Contenido
    {
        public DateTime Desde { get; init; }
        public DateTime Hasta { get; init; }
        /* 0 = los tres origenes; 1 AGENTE, 2 TIENDA, 3 GALLERO. */
        public int IdOrigen { get; init; }
        /* 0 = todos los agentes. */
        public int IdAgente { get; init; }
        public int IdUsuario { get; init; }
        /*
          QUIEN PIDIO EL PAPEL. Se imprime arriba: cuando una copia aparece
          sobre un escritorio, lo primero que hace falta saber es quien la saco.
          Viaja en el pase porque el GET que entrega el PDF es anonimo y ahi ya
          no hay token del que sacarlo.
        */
        public string Usuario { get; init; } = "";
    }

    public static string Firmar(
        string llave, DateTime desde, DateTime hasta,
        int idOrigen, int idAgente, int idUsuario, string usuario = "")
    {
        var expira = DateTimeOffset.UtcNow.Add(Vigencia).ToUnixTimeSeconds();
        var cuerpo = $"{Fecha(desde)}|{Fecha(hasta)}|{Origen(idOrigen)}|{Mayor(idAgente)}" +
                     $"|{idUsuario}|{expira}|{SinSeparador(usuario)}";
        return Base64Url(Encoding.UTF8.GetBytes(cuerpo)) + "." + Base64Url(Firma(llave, cuerpo));
    }

    /// Devuelve null si el pase esta mal formado, fue alterado o ya vencio.
    public static Contenido? Validar(string llave, string? ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket)) return null;

        var partes = ticket.Split('.');
        if (partes.Length != 2) return null;

        string cuerpo;
        byte[] firmaRecibida;
        try
        {
            cuerpo = Encoding.UTF8.GetString(DeBase64Url(partes[0]));
            firmaRecibida = DeBase64Url(partes[1]);
        }
        catch
        {
            return null;
        }

        // Tiempo constante: no se filtra cuanto acerto quien lo intenta.
        if (!CryptographicOperations.FixedTimeEquals(firmaRecibida, Firma(llave, cuerpo)))
            return null;

        var campos = cuerpo.Split('|');
        if (campos.Length != 7) return null;

        if (!long.TryParse(campos[5], out var expira) ||
            DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expira)
            return null;

        var desde = LeerFecha(campos[0]);
        var hasta = LeerFecha(campos[1]);
        if (desde == null || hasta == null) return null;

        return new Contenido
        {
            Desde = desde.Value,
            Hasta = hasta.Value,
            IdOrigen = Origen(Numero(campos[2])),
            IdAgente = Mayor(Numero(campos[3])),
            IdUsuario = Numero(campos[4]),
            Usuario = campos[6]
        };
    }

    /*
      Solo los origenes conocidos; cualquier otra cosa cae en 0 = todos. Este
      numero llega hasta un WHERE: dejarlo pasar tal cual seria confiar en que
      nadie escriba nada raro en un pase que ademas se puede intentar
      falsificar.
    */
    private static int Origen(int valor) => valor is 1 or 2 or 3 ? valor : 0;

    private static int Mayor(int valor) => valor > 0 ? valor : 0;

    private static int Numero(string texto) => int.TryParse(texto, out var n) ? n : 0;

    /* Solo el dia: la hora no filtra nada aqui y alargaria el pase. */
    private static string Fecha(DateTime valor) => valor.ToString("yyyy-MM-dd");

    private static DateTime? LeerFecha(string texto)
        => DateTime.TryParseExact(texto, "yyyy-MM-dd", null,
               System.Globalization.DateTimeStyles.None, out var f)
           ? f
           : null;

    /*
      EL NOMBRE NO PUEDE LLEVAR EL SEPARADOR. Un usuario que se llamara "a|b"
      partiria el cuerpo en ocho campos y el pase —firmado y todo— se leeria
      mal. Se cambia por un espacio. Y se corta a 60: el nombre se imprime en
      un renglon del encabezado, y uno de trescientas letras no lo hace
      ilegible a el, hace ilegible el encabezado.
    */
    private static string SinSeparador(string? texto)
    {
        var limpio = (texto ?? "").Replace('|', ' ').Trim();
        return limpio.Length > 60 ? limpio[..60] : limpio;
    }

    private static byte[] Firma(string llave, string cuerpo)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(llave));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(cuerpo));
    }

    private static string Base64Url(byte[] datos)
        => Convert.ToBase64String(datos).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] DeBase64Url(string texto)
    {
        var s = texto.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }
}
