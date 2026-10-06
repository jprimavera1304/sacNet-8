using ISL_Service.Application.Security;
using Xunit;

namespace ISL_Service.Tests;

/*
  El pase es lo unico que separa los reportes de quien pase por ahi: la
  direccion que los entrega es anonima a proposito, porque una pestaña nueva del
  navegador no manda Authorization.

  Asi que lo que se prueba aqui no es que "funcione", sino que NO funcione
  cuando debe fallar: alterado, caducado, de otro psp, de otro centro, o con
  otra llave.
*/
public class ReporteTicketTests
{
    private const string Llave = "llave-de-prueba-que-no-es-la-de-produccion";

    [Fact]
    public void Un_pase_recien_firmado_se_valida_y_conserva_lo_que_dice()
    {
        var pase = ReporteTicket.Firmar(Llave, psp: 266392, idCentro: 3, idUsuario: 77);

        var contenido = ReporteTicket.Validar(Llave, pase);

        Assert.NotNull(contenido);
        Assert.Equal(266392, contenido!.Psp);
        Assert.Equal(3, contenido.IdCentro);
        Assert.Equal(77, contenido.IdUsuario);
    }

    [Fact]
    public void Un_pase_con_otra_llave_no_vale()
    {
        var pase = ReporteTicket.Firmar(Llave, 100, 0, 1);

        Assert.Null(ReporteTicket.Validar("otra-llave-distinta", pase));
    }

    [Fact]
    public void Un_pase_alterado_no_vale()
    {
        // Se le cambia un caracter al cuerpo, que es lo que haria quien quisiera
        // leer el reporte de al lado: cambiar el numero y dejar la firma.
        var pase = ReporteTicket.Firmar(Llave, 100, 0, 1);
        var punto = pase.LastIndexOf('.');
        var cuerpo = pase[..punto];
        var alterado = (cuerpo[0] == 'M' ? "N" : "M") + cuerpo[1..] + pase[punto..];

        Assert.Null(ReporteTicket.Validar(Llave, alterado));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sinpunto")]
    [InlineData(".")]
    [InlineData("cuerpo.")]
    [InlineData(".firma")]
    [InlineData("no-es-base64!!.tampoco!!")]
    public void Un_pase_mal_formado_no_vale_y_no_revienta(string? pase)
    {
        Assert.Null(ReporteTicket.Validar(Llave, pase));
    }

    [Fact]
    public void El_pase_de_un_psp_no_sirve_para_otro()
    {
        // No lo decide el pase sino el controlador, pero la prueba deja escrito
        // POR QUE el controlador tiene que comparar: el pase es valido, y aun
        // asi no autoriza este reporte.
        var pase = ReporteTicket.Firmar(Llave, psp: 100, idCentro: 0, idUsuario: 1);

        var contenido = ReporteTicket.Validar(Llave, pase);

        Assert.NotNull(contenido);
        Assert.NotEqual(200, contenido!.Psp);
    }

    [Fact]
    public void La_firma_cubre_el_centro_tambien()
    {
        var deLaMatriz = ReporteTicket.Firmar(Llave, 100, idCentro: 0, idUsuario: 1);
        var delCentro = ReporteTicket.Firmar(Llave, 100, idCentro: 5, idUsuario: 1);

        Assert.NotEqual(deLaMatriz, delCentro);
        Assert.Equal(0, ReporteTicket.Validar(Llave, deLaMatriz)!.IdCentro);
        Assert.Equal(5, ReporteTicket.Validar(Llave, delCentro)!.IdCentro);
    }

    [Fact]
    public void Un_pase_vencido_no_vale()
    {
        // La vigencia son 10 minutos y no se puede adelantar el reloj desde
        // aqui, asi que se arma a mano un pase con la misma forma pero caducado.
        // Si manana alguien cambia el formato del cuerpo, esta prueba se cae y
        // es justo lo que tiene que pasar.
        var vencido = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds();
        var pase = PaseFirmado.Firmar(Llave, $"100|0|1|{vencido}");

        Assert.Null(ReporteTicket.Validar(Llave, pase));
    }
}
