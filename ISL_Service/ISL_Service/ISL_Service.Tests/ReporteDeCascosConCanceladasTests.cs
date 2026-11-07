using System;
using System.Collections.Generic;
using ISL_Service.Application.DTOs.RecepcionCascos;
using ISL_Service.Application.Security;
using ISL_Service.Infrastructure.Reports;
using Xunit;

namespace ISL_Service.Tests;

/*
  EL PAPEL DE CASCOS CUANDO SE PIDEN LAS CANCELADAS.

  POR QUE EXISTE ESTE ARCHIVO. La pantalla del telefono ya tenia el interruptor
  de "imprimir con canceladas" y mandaba `incluirCanceladas=true`, pero el
  backend NO recibia el parametro: lo tiraba en silencio. El papel salia
  siempre sin canceladas y nadie veia un error — ni un 400, ni un renglon en la
  bitacora. Un filtro que se ignora sin ruido es la peor clase de rota, asi que
  aqui se fija la cadena entera.

  LO QUE SE PROTEGE, EN UNA LINEA: el total del papel NUNCA suma canceladas, se
  pidan o no. Una recepcion cancelada es justamente la que NO se recibio; si
  entrara en el total, la hoja sumaria cascos que nadie tiene Y CUADRARIA
  CONSIGO MISMA, que es peor que contradecirse a la vista: un numero equivocado
  que no se contradice no lo descubre nadie.

  Lo que la base hace con el parametro (que renglones devuelve) se prueba
  contra la base en `RecepcionCascosContraLaBaseTests`. Aqui se prueba lo que
  no necesita base: el pase firmado y lo que acaba dibujado en la hoja.
*/
public class ReporteDeCascosConCanceladasTests
{
    private const string Llave = "llave-de-prueba-para-firmar-el-pase-00000000";
    private const int Activa = 1;
    private const int Cancelada = 2;

    /* ---------------------------------------------------------------- */
    /* El pase firmado                                                   */
    /* ---------------------------------------------------------------- */

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ElPaseLlevaYDevuelveSiVanLasCanceladas(bool conCanceladas)
    {
        var ticket = ReporteRecepcionCascosTicket.Firmar(
            Llave, new DateTime(2026, 11, 1), new DateTime(2026, 11, 7),
            idRepartidor: 0, idUsuario: 7, usuario: "JUAN",
            incluirCanceladas: conCanceladas);

        var pase = ReporteRecepcionCascosTicket.Validar(Llave, ticket);

        Assert.NotNull(pase);
        Assert.Equal(conCanceladas, pase!.IncluirCanceladas);
    }

    /*
      QUE NO SE PUEDA ENCENDER DESDE LA BARRA DEL NAVEGADOR.

      Es el motivo de que la bandera viaje DENTRO del pase y no como un
      parametro suelto de la direccion. Si se pudiera cambiar a mano, quien
      tiene permiso de imprimir el reporte operativo se sacaria tambien el de
      las canceladas sin pasar por la pantalla.
    */
    [Fact]
    public void CambiarLaBanderaAManoInvalidaElPase()
    {
        var sinCanceladas = ReporteRecepcionCascosTicket.Firmar(
            Llave, new DateTime(2026, 11, 1), new DateTime(2026, 11, 7),
            0, 7, "JUAN", incluirCanceladas: false);

        /* El cuerpo va en claro: se puede leer, cambiar y volver a pegar. Lo
           que no se puede es volver a firmar sin la llave. */
        var partes = sinCanceladas.Split('.');
        var cuerpo = System.Text.Encoding.UTF8.GetString(DeBase64Url(partes[0]));
        var alterado = cuerpo[..^1] + "1";
        var falso = Base64Url(System.Text.Encoding.UTF8.GetBytes(alterado)) + "." + partes[1];

        Assert.Null(ReporteRecepcionCascosTicket.Validar(Llave, falso));
    }

    /*
      EL PASE DE SEIS CAMPOS —el que firmaba el servidor anterior— SE SIGUE
      LEYENDO.

      Dura diez minutos, asi que durante un despliegue hay pases vivos en el
      historial de alguien. Rechazarlos le daria "el enlace no es valido" a
      quien pidio su reporte un minuto antes de subir, y ese error no se
      entiende desde el otro lado.
    */
    [Fact]
    public void ElPaseViejoDeSeisCamposSigueValiendoYSaleSinCanceladas()
    {
        var expira = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();
        var cuerpo = $"2026-11-01|2026-11-07|0|7|{expira}|JUAN";
        var viejo = Base64Url(System.Text.Encoding.UTF8.GetBytes(cuerpo)) + "." +
                    Base64Url(Hmac(Llave, cuerpo));

        var pase = ReporteRecepcionCascosTicket.Validar(Llave, viejo);

        Assert.NotNull(pase);
        Assert.False(pase!.IncluirCanceladas);
        Assert.Equal("JUAN", pase.Usuario);
    }

    /* ---------------------------------------------------------------- */
    /* La hoja                                                           */
    /* ---------------------------------------------------------------- */

    /*
      LA PRUEBA QUE DE VERDAD IMPORTA. Dos recepciones del mismo tamano: una
      activa de 10 piezas y una cancelada de 4. El total tiene que decir 10.
    */
    [Fact]
    public void ElTotalNoSumaLasCanceladas()
    {
        var html = RecepcionCascosHtmlBuilder.Construir(ConUnaActivaYUnaCancelada());

        var total = RenglonQueEmpiezaEn(html, "TOTAL DE CASCOS RECIBIDOS");
        Assert.Contains(">10<", total);
        Assert.DoesNotContain(">14<", total);
    }

    [Fact]
    public void LaCanceladaSaleMarcadaYConSuPalabra()
    {
        var html = RecepcionCascosHtmlBuilder.Construir(ConUnaActivaYUnaCancelada());

        Assert.Contains("class=\"cancelada\"", html);
        Assert.Contains("CANCELADA", html);
        /* El motivo, cuando lo hay, se lee al lado y no se pierde. */
        Assert.Contains("se capturo dos veces", html);
    }

    [Fact]
    public void LoCanceladoBajaASuPropioRenglonYDiceQueNoSuma()
    {
        var html = RecepcionCascosHtmlBuilder.Construir(ConUnaActivaYUnaCancelada());

        var renglon = RenglonQueEmpiezaEn(html, "DE LO CANCELADO");
        Assert.Contains("no suma", renglon);
        Assert.Contains(">4<", renglon);
        Assert.Contains("1 recepción cancelada", renglon);
    }

    /*
      Y SIN CANCELADAS, LA HOJA ES LA DE SIEMPRE.

      Esto es lo que protege a quien no pidio nada: el reporte operativo no
      cambia de forma porque exista la opcion. Un renglon de ceros en cada hoja
      ensena a no mirarlo, y el dia que dijera algo tampoco lo miraria nadie.
    */
    [Fact]
    public void SinNingunaCanceladaNoApareceElRenglonDeAviso()
    {
        var datos = ConUnaActivaYUnaCancelada();
        datos.Renglones.RemoveAll(r => r.estatus == Cancelada);
        datos.Totales[0].piezasCanceladas = 0;

        var html = RecepcionCascosHtmlBuilder.Construir(datos);

        Assert.DoesNotContain("DE LO CANCELADO", html);
        Assert.DoesNotContain("class=\"cancelada\"", html);
    }

    /* ---------------------------------------------------------------- */

    private static DatosReporteRecepcionCascos ConUnaActivaYUnaCancelada() => new()
    {
        Renglones = new List<RenglonReporteRecepcionCascoDto>
        {
            new()
            {
                idRecepcion = 1, fecha = new DateTime(2026, 11, 3),
                quienEntrego = "PEDRO", idRepartidor = 10,
                idTipoUsado = 6, piezas = 10, totalPiezas = 10,
                estatus = Activa
            },
            new()
            {
                idRecepcion = 2, fecha = new DateTime(2026, 11, 4),
                quienEntrego = "PEDRO", idRepartidor = 10,
                idTipoUsado = 6, piezas = 4, totalPiezas = 4,
                observaciones = "se capturo dos veces",
                estatus = Cancelada
            }
        },
        Totales = new List<TotalPorTipoRecepcionCascoDto>
        {
            new()
            {
                idTipoUsado = 6, clave = "MINI CHICO (1)",
                nombre = "11141608 MINI CHICO (1)", orden = 1,
                piezas = 10, piezasCanceladas = 4
            }
        }
    };

    /* El <tr> completo que contiene ese texto: asi una asercion sobre un
       numero no se cumple por casualidad con el numero de otro renglon. */
    private static string RenglonQueEmpiezaEn(string html, string texto)
    {
        var i = html.IndexOf(texto, StringComparison.Ordinal);
        Assert.True(i >= 0, $"No esta el renglon «{texto}» en la hoja.");
        var abre = html.LastIndexOf("<tr", i, StringComparison.Ordinal);
        var cierra = html.IndexOf("</tr>", i, StringComparison.Ordinal);
        return html[abre..(cierra + 5)];
    }

    private static byte[] Hmac(string llave, string cuerpo)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(
            System.Text.Encoding.UTF8.GetBytes(llave));
        return hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(cuerpo));
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
