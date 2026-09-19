using System.Security.Cryptography;
using System.Text;

namespace ISL_Service.Application.Security;

/*
  EL PASE PARA VER EL PDF DE LA REMISION

  El PDF se abre en una pestana nueva del navegador, y una pestana nueva no
  lleva encabezado Authorization: solo lleva lo que vaya en la direccion. Por
  eso el GET que entrega el PDF es anonimo y lo que lo autoriza es este pase.

  Lo que NO se hace y por que:
    - meter el token de sesion en la direccion: queda en el historial, en los
      logs del servidor y en el "copiar enlace" del usuario, y con el se puede
      hacer cualquier cosa, no solo ver un papel.
    - guardar los parametros en una tabla (que es lo que hacia el flujo viejo
      con sp_n_ActualizarParametro): obliga a escribir en la base para leer un
      reporte y deja basura que nadie limpia.

  Asi que el pase es autocontenido y firmado: dice QUE ventas, PARA QUE usuario
  y HASTA CUANDO, y va firmado con la misma llave del JWT. Si alguien le cambia
  una coma, la firma no cuadra y no hay PDF. Dura pocos minutos: es para pasar
  de la respuesta del POST a la pestana que ya se abrio, nada mas.
*/
public static class RemisionTicket
{
    private static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(10);

    public sealed class Contenido
    {
        public List<int> IdsVenta { get; init; } = new();
        public int IdUsuario { get; init; }
        public int Descargar { get; init; }
    }

    public static string Firmar(string llave, IEnumerable<int> idsVenta, int idUsuario, int descargar)
    {
        var expira = DateTimeOffset.UtcNow.Add(Vigencia).ToUnixTimeSeconds();
        var cuerpo = $"{string.Join(",", idsVenta)}|{idUsuario}|{(descargar == 1 ? 1 : 0)}|{expira}";
        return Base64Url(Encoding.UTF8.GetBytes(cuerpo)) + "." + Base64Url(Firma(llave, cuerpo));
    }

    /// Devuelve null si el pase esta mal formado, fue alterado o ya vencio.
    public static Contenido? Validar(string llave, string? ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket))
            return null;

        var partes = ticket.Split('.');
        if (partes.Length != 2)
            return null;

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

        // Comparacion en tiempo constante: no se filtra cuanto acerto quien lo intenta.
        if (!CryptographicOperations.FixedTimeEquals(firmaRecibida, Firma(llave, cuerpo)))
            return null;

        var campos = cuerpo.Split('|');
        if (campos.Length != 4)
            return null;

        if (!long.TryParse(campos[3], out var expira) || DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expira)
            return null;

        var ids = campos[0]
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => int.TryParse(x, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToList();

        if (ids.Count == 0)
            return null;

        return new Contenido
        {
            IdsVenta = ids,
            IdUsuario = int.TryParse(campos[1], out var idu) ? idu : 0,
            Descargar = campos[2] == "1" ? 1 : 0
        };
    }

    private static byte[] Firma(string llave, string cuerpo)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(llave));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(cuerpo));
    }

    private static string Base64Url(byte[] datos)
    {
        return Convert.ToBase64String(datos).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static byte[] DeBase64Url(string texto)
    {
        var s = texto.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }
}
