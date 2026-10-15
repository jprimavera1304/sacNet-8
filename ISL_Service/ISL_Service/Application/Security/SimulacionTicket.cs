using System.Security.Cryptography;
using System.Text;

namespace ISL_Service.Application.Security;

/*
  EL PASE PARA VER EL PDF DE LA SIMULACION (reporte 1175)

  Mismo mecanismo y misma razon que RemisionTicket: el PDF se abre en una
  pestana nueva, una pestana nueva no lleva Authorization, asi que el GET que
  entrega el papel es anonimo y lo que autoriza es este pase firmado con la
  llave del JWT.

  POR QUE ES UNA CLASE APARTE Y NO UN CAMPO MAS EN EL DE LA REMISION

  Dos razones medidas, no de estilo:

    1. EL PASE DE LA REMISION TIRA LOS IDS NEGATIVOS. Su validacion filtra con
       `Where(id => id > 0)` y, si no queda ninguno, devuelve null. Y el id de
       una simulacion de pedido es justamente negativo (-IDPedido), porque asi
       lo lee `sp_n_VentasInformacion`. Meterlo ahi obligaba a aflojar ese
       filtro, que es el que impide pedir "la venta 0" o un pase vacio.

    2. LO QUE VIAJA ES OTRA COSA. Aqui no hay banderas de primera impresion ni
       de reimpresion -simular no sella nada- y si hay descuento simulado y
       cascos a cambio. Un pase con seis campos que solo valen en la mitad de
       los casos se lee peor que dos pases cortos.

  EL DESCUENTO Y LOS CASCOS VAN FIRMADOS, NO EN LA DIRECCION. No es que
  descubran nada: es que son los dos numeros que cambian las cuentas del papel.
  Si se pudieran teclear en la barra del navegador, cualquiera se armaria la
  simulacion que quisiera sobre la venta de otro sin pasar por la pantalla que
  comprueba el permiso.
*/
public static class SimulacionTicket
{
    /// Lo justo para ir de la respuesta del POST a la pestana que ya se abrio,
    /// igual que el de la remision.
    private static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(10);

    public sealed class Contenido
    {
        /// Positivo = una remision emitida. Negativo = -IDPedido, un pedido sin
        /// emitir. Nunca 0.
        public int IdVenta { get; init; }

        public int IdUsuario { get; init; }

        /// El descuento que se simula; 0 = ninguno.
        public int IdDescuento { get; init; }

        /// 0 = como venga, 1 = CON cascos a cambio, 2 = SIN cascos a cambio.
        public int ConCascos { get; init; }
    }

    public static string Firmar(
        string llave,
        int idVenta,
        int idUsuario,
        int idDescuento,
        int conCascos)
    {
        var expira = DateTimeOffset.UtcNow.Add(Vigencia).ToUnixTimeSeconds();
        var cuerpo = $"{idVenta}|{idUsuario}|{idDescuento}|{conCascos}|{expira}";
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
        if (campos.Length != 5)
            return null;

        if (!long.TryParse(campos[4], out var expira) || DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expira)
            return null;

        if (!int.TryParse(campos[0], out var idVenta) || idVenta == 0)
            return null;

        return new Contenido
        {
            IdVenta = idVenta,
            IdUsuario = int.TryParse(campos[1], out var idu) ? idu : 0,
            IdDescuento = int.TryParse(campos[2], out var idd) && idd > 0 ? idd : 0,
            /*
              Solo 1 y 2 son valores de `enumSimulaCascos`. Cualquier otra cosa
              se lee como 0, que es "no toques las cuentas": un numero raro no
              puede acabar aplicando un ajuste a medias.
            */
            ConCascos = int.TryParse(campos[3], out var cc) && (cc == 1 || cc == 2) ? cc : 0
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
