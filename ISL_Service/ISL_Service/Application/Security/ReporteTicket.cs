using System.Security.Cryptography;
using System.Text;

namespace ISL_Service.Application.Security;

/*
  EL PASE PARA VER UN REPORTE DEL MODULO REPORTES

  POR QUE HACE FALTA

  El GET que entrega el reporte (ReportesV3W) estaba [AllowAnonymous] y lo unico
  que pedia era `psp`, que es el IDParametro que acaba de guardar "generar": un
  entero CONSECUTIVO de una tabla compartida. Quien probara numeros veia los
  reportes de los demas -ventas, saldos, cobranza- sin haber iniciado sesion.

  Sus hermanos ya lo tenian resuelto: /remision y /usados van firmados con
  RemisionTicket. Este es el mismo mecanismo para el otro endpoint.

  POR QUE EL GET SIGUE SIENDO ANONIMO

  El reporte se abre en una pestaña nueva del navegador, y una pestaña nueva no
  manda el encabezado Authorization: solo lleva lo que vaya en la direccion. Por
  eso lo que autoriza es el pase y no el token de sesion. Meter el token en la
  direccion seria peor: queda en el historial, en los registros del servidor y
  en el "copiar enlace", y con el se puede hacer cualquier cosa, no solo ver un
  papel.

  QUE DICE EL PASE

  Que `psp` se puede ver, de que centro, para que usuario y hasta cuando. Va
  firmado con la misma llave del JWT: si alguien le cambia una coma, la firma no
  cuadra y no hay reporte.

  POR QUE NO SE REUSO RemisionTicket TAL CUAL

  Aquel pase habla de ventas: lleva ids de venta y las dos banderas de impresion
  que cambian el papel y marcan la remision como impresa. Aqui no hay nada de
  eso, y meterle campos que no significan nada en este caso haria mas dificil
  leer los dos. Lo que si se comparte es lo unico que convenia compartir: la
  firma, en PaseFirmado.
*/
public static class ReporteTicket
{
    /*
      DIEZ MINUTOS.

      Es para pasar de la respuesta del POST a la pestaña que ya se abrio, nada
      mas. No es un enlace para guardar ni para mandar por correo: si alguien lo
      reenvia, a los diez minutos no sirve.
    */
    private static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(10);

    public sealed class Contenido
    {
        public int Psp { get; init; }
        public int IdCentro { get; init; }
        public int IdUsuario { get; init; }
    }

    public static string Firmar(string llave, int psp, int idCentro, int idUsuario)
    {
        var expira = DateTimeOffset.UtcNow.Add(Vigencia).ToUnixTimeSeconds();
        var cuerpo = $"{psp}|{idCentro}|{idUsuario}|{expira}";
        return PaseFirmado.Firmar(llave, cuerpo);
    }

    /// Devuelve null si el pase esta mal formado, fue alterado o ya vencio.
    public static Contenido? Validar(string llave, string? pase)
    {
        var cuerpo = PaseFirmado.Validar(llave, pase);
        if (cuerpo is null)
            return null;

        var partes = cuerpo.Split('|');
        if (partes.Length != 4)
            return null;

        if (!long.TryParse(partes[3], out var expira)
            || DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expira)
            return null;

        if (!int.TryParse(partes[0], out var psp) || psp <= 0)
            return null;

        int.TryParse(partes[1], out var idCentro);
        int.TryParse(partes[2], out var idUsuario);

        return new Contenido { Psp = psp, IdCentro = idCentro, IdUsuario = idUsuario };
    }
}

/*
  FIRMAR Y COMPROBAR UN PASE QUE VIAJA EN LA DIRECCION.

  Lo unico que de verdad conviene compartir entre los pases: la firma. Lo que
  cada pase mete en su cuerpo es asunto suyo.

  La comparacion de la firma es en tiempo constante a proposito: comparar con
  != termina en la primera letra distinta, y la diferencia de tiempo entre un
  fallo temprano y uno tardio alcanza para ir adivinando la firma byte a byte.
*/
public static class PaseFirmado
{
    public static string Firmar(string llave, string cuerpo)
    {
        return Base64Url(Encoding.UTF8.GetBytes(cuerpo)) + "." + Base64Url(Firma(llave, cuerpo));
    }

    public static string? Validar(string llave, string? pase)
    {
        if (string.IsNullOrWhiteSpace(pase))
            return null;

        var punto = pase.LastIndexOf('.');
        if (punto <= 0 || punto == pase.Length - 1)
            return null;

        string cuerpo;
        byte[] firmaRecibida;
        try
        {
            cuerpo = Encoding.UTF8.GetString(DeBase64Url(pase[..punto]));
            firmaRecibida = DeBase64Url(pase[(punto + 1)..]);
        }
        catch (FormatException)
        {
            return null;
        }

        var esperada = Firma(llave, cuerpo);
        if (!CryptographicOperations.FixedTimeEquals(firmaRecibida, esperada))
            return null;

        return cuerpo;
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
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }
}
