using System.Security.Cryptography;
using System.Text;

namespace ISL_Service.Application.Security;

/*
  EL PASE FIRMADO DEL REPORTE DE USADOS

  El PDF lo abre una pestaña nueva del navegador, y una pestaña nueva no manda
  el encabezado Authorization: no hay forma de que el GET que entrega el papel
  vaya autenticado como el resto de la aplicacion. Lo que autoriza es esto — un
  pase corto, firmado y con caducidad, que viaja en la direccion.

  POR QUE NO SE REUSA RemisionTicket. Aquel pase lleva una lista de ventas y dos
  banderas de impresion que cambian el resultado del procedimiento de legacy.
  Este lleva un PERIODO. Meter las dos cosas en el mismo formato obligaria a que
  cada uno entienda los campos del otro, y el dia que uno cambie se rompe el
  otro sin que nada lo avise. La firma se hace igual —HMAC con la llave del
  JWT—, que es lo que de verdad importa que no se separe.

  QUE PROTEGE. Que alguien escriba otro periodo en la barra del navegador y se
  saque un reporte de un rango que no puede ver. El cuerpo va en claro —no es
  secreto, son dos fechas— pero firmado: cambiar un digito invalida el pase.

  LOS DIEZ MINUTOS son para que el enlace no sirva de aqui a mañana si queda en
  el historial del navegador. Da de sobra para abrir el PDF y no para guardarlo
  como una puerta.
*/
public static class ReporteUsadosTicket
{
    private static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(10);

    public sealed class Contenido
    {
        public DateTime? Desde { get; init; }
        public DateTime? Hasta { get; init; }
        public bool IncluirCancelados { get; init; }
        public bool PorRegistro { get; init; }
        public int IdUsuario { get; init; }
    }

    public static string Firmar(
        string llave,
        DateTime? desde,
        DateTime? hasta,
        bool incluirCancelados,
        bool porRegistro,
        int idUsuario)
    {
        var expira = DateTimeOffset.UtcNow.Add(Vigencia).ToUnixTimeSeconds();
        var cuerpo = $"{Fecha(desde)}|{Fecha(hasta)}|{(incluirCancelados ? 1 : 0)}" +
                     $"|{(porRegistro ? 1 : 0)}|{idUsuario}|{expira}";
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
        if (campos.Length != 6) return null;

        if (!long.TryParse(campos[5], out var expira) ||
            DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expira)
            return null;

        return new Contenido
        {
            Desde = LeerFecha(campos[0]),
            Hasta = LeerFecha(campos[1]),
            IncluirCancelados = campos[2] == "1",
            PorRegistro = campos[3] == "1",
            IdUsuario = int.TryParse(campos[4], out var idu) ? idu : 0
        };
    }

    /* Solo el dia: la hora no filtra nada aqui y alargaria el pase. */
    private static string Fecha(DateTime? valor) => valor?.ToString("yyyy-MM-dd") ?? "";

    private static DateTime? LeerFecha(string texto)
        => DateTime.TryParseExact(texto, "yyyy-MM-dd", null,
               System.Globalization.DateTimeStyles.None, out var f)
           ? f
           : null;

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
