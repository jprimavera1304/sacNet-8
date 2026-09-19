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

        /*
          LAS DOS BANDERAS DE IMPRESION VIAJAN EN EL PASE

          Son las mismas que Mac31 le manda al procedimiento
          (ConsultarVentas.cs:4213 y :4221 -> Imprimir(primerImpresion,
          reimpresion)), y tienen que llegar hasta sp_n_VentasInformacion porque
          ahi es donde cambian el resultado:

            PrimerImpresion = 1  ->  si la remision YA la imprimio alguien, el
                                     procedimiento no la entrega: devuelve
                                     result = 0 con "EL FOLIO ... YA FUE
                                     IMPRESO POR ...".
            Reimpresion     = 1  ->  autoriza volver a hacer la primer
                                     impresion: reescribe quien imprimio y
                                     desde que equipo, y marca el pedido como
                                     reimpreso.

          Viajan en el pase FIRMADO y no como parametro suelto de la direccion
          justamente por eso: la que marca papel es la bandera, y si fuera
          editable en la barra del navegador cualquiera podria autorizarse una
          reimpresion escribiendo un 1.
        */
        public int PrimerImpresion { get; init; }
        public int Reimpresion { get; init; }

        /*
          DESDE DONDE SE IMPRIMIO, Y POR QUE VIAJA AQUI

          El procedimiento GUARDA este dato en Ventas y en Pedidos: es el rastro
          de quien saco el papel y desde que maquina. En Mac31 es el nombre del
          equipo; en el web no hay equipo, asi que se manda el usuario, que es
          lo mas parecido a "quien fue".

          Tiene que venir en el pase porque el GET que entrega el PDF es ANONIMO
          —una pestaña nueva no manda el token—, asi que ahi ya no se sabe quien
          pidio el reporte. Sin esto se guardaba el nombre de la maquina del
          SERVIDOR, o sea "todas las impresiones del web salieron del servidor":
          un rastro que no sirve para nada.
        */
        public string Equipo { get; init; } = "";
    }

    public static string Firmar(
        string llave,
        IEnumerable<int> idsVenta,
        int idUsuario,
        int descargar,
        int primerImpresion = 0,
        int reimpresion = 0,
        string equipo = "")
    {
        var expira = DateTimeOffset.UtcNow.Add(Vigencia).ToUnixTimeSeconds();
        var cuerpo = $"{string.Join(",", idsVenta)}|{idUsuario}|{(descargar == 1 ? 1 : 0)}" +
                     $"|{(primerImpresion == 1 ? 1 : 0)}|{(reimpresion == 1 ? 1 : 0)}" +
                     $"|{Limpio(equipo)}|{expira}";
        return Base64Url(Encoding.UTF8.GetBytes(cuerpo)) + "." + Base64Url(Firma(llave, cuerpo));
    }

    /// El separador del pase es "|": si el dato lo trae, se quita, o el pase se
    /// leeria partido en un campo de mas. Se corta a lo que cabe en la columna.
    private static string Limpio(string? texto)
        => (texto ?? "").Replace("|", "").Trim() is { Length: > 0 } t
            ? (t.Length > 60 ? t[..60] : t)
            : "";

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

        /*
          SE ACEPTAN LOS DOS FORMATOS, EL VIEJO Y EL NUEVO

          El viejo son cuatro campos (sin banderas de impresion) y el nuevo son
          seis. No es por nostalgia: el pase dura diez minutos, asi que cuando
          se publica una version hay pestañas ya abiertas con un pase del
          formato anterior. Rechazarlas le daria "el enlace no es valido" a
          gente que no hizo nada mal, a media jornada.

          El pase viejo se lee como lo que era: sin banderas, o sea Pantalla.
        */
        var campos = cuerpo.Split('|');
        if (campos.Length != 4 && campos.Length != 7)
            return null;

        var posicionExpira = campos.Length - 1;
        if (!long.TryParse(campos[posicionExpira], out var expira) || DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expira)
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
            Descargar = campos[2] == "1" ? 1 : 0,
            PrimerImpresion = campos.Length == 7 && campos[3] == "1" ? 1 : 0,
            Reimpresion = campos.Length == 7 && campos[4] == "1" ? 1 : 0,
            Equipo = campos.Length == 7 ? campos[5] : ""
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
