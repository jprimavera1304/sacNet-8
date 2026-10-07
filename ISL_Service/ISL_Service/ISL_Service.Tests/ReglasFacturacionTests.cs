using ISL_Service.Infrastructure.Facturacion;

namespace ISL_Service.Tests;

/*
  LA PARTE DE LA FACTURACION QUE SI SE PUEDE PROBAR.

  Timbrar no se puede probar: emitiria un CFDI real ante el SAT y volver la base
  no lo deshace. Pero todo lo que decide COMO se timbra —el candado del key, la
  serie, el folio, las claves del SAT, el formato del QR— son funciones puras, y
  son justo donde legacy tiene la logica mas fragil.

  La serie importa especialmente: de ella depende la NUMERACION FISCAL. Una serie
  distinta son facturas con otra numeracion ante el SAT, y eso no se arregla
  despues.
*/
public class ReglasFacturacionTests
{
    [Fact]
    public void El_candado_pide_que_el_key_lleve_el_nombre_del_ejecutable_dentro()
    {
        /*
          Es el unico candado que impide que una instalacion de pruebas con una
          copia de la base de produccion timbre comprobantes reales: en esa maquina
          el AppMac es otro y no coincide.
        */
        var secreto = ReglasFacturacion.SecretoReal("sk_testMacZCS3abc", "MacZCS3.exe");
        Assert.Equal("sk_testabc", secreto);
    }

    [Fact]
    public void Si_el_key_no_lleva_el_nombre_no_hay_secreto()
    {
        Assert.Null(ReglasFacturacion.SecretoReal("sk_test_sin_marca", "MacZCS3.exe"));
        Assert.Null(ReglasFacturacion.SecretoReal(null, "MacZCS3.exe"));
        Assert.Null(ReglasFacturacion.SecretoReal("sk_testMacZCS3", null));
    }

    [Fact]
    public void Un_key_que_es_solo_la_marca_no_vale_como_secreto()
    {
        /* Quitarle la marca lo deja vacio: no hay secreto que mandar. */
        Assert.Null(ReglasFacturacion.SecretoReal("MacZCS3", "MacZCS3.exe"));
    }

    [Fact]
    public void El_mensaje_del_candado_es_el_que_reconoce_soporte()
    {
        Assert.Equal("KEY PARA FACTURAR NO COINCIDE.", ReglasFacturacion.MensajeKeyNoCoincide);
        Assert.Equal("KEY PARA FACTURAR ES INCORRECTO.", ReglasFacturacion.MensajeKeyIncorrecto);
    }

    [Theory]
    [InlineData("MacT.exe", ReglasFacturacion.TipoDocumento.Factura, "T")]
    [InlineData("MacZCS3.exe", ReglasFacturacion.TipoDocumento.Factura, "CS3")]
    [InlineData("MacL.exe", ReglasFacturacion.TipoDocumento.Factura, "L")]
    [InlineData("MacZCS1.exe", ReglasFacturacion.TipoDocumento.FacturaSinRemision, "CS1SR")]
    [InlineData("MacZCS6.exe", ReglasFacturacion.TipoDocumento.NotaCredito, "NCCS6")]
    [InlineData("MacL.exe", ReglasFacturacion.TipoDocumento.ComplementoPago, "LPAG")]
    public void La_serie_sale_del_ejecutable_que_esta_corriendo(
        string appMac, ReglasFacturacion.TipoDocumento tipo, string esperada)
    {
        Assert.Equal(esperada, ReglasFacturacion.Serie("sk_cualquiera", appMac, tipo));
    }

    [Theory]
    [InlineData(ReglasFacturacion.TipoDocumento.Factura, "Z")]
    [InlineData(ReglasFacturacion.TipoDocumento.FacturaSinRemision, "ZSR")]
    [InlineData(ReglasFacturacion.TipoDocumento.NotaCredito, "NCZ")]
    [InlineData(ReglasFacturacion.TipoDocumento.ComplementoPago, "ZPAG")]
    public void Un_ejecutable_desconocido_cae_a_la_serie_de_Zaragoza(
        ReglasFacturacion.TipoDocumento tipo, string esperada)
    {
        /* Es el "else" de legacy: lo que no es ninguno de los ocho conocidos
           factura como Zaragoza. */
        Assert.Equal(esperada, ReglasFacturacion.Serie("sk_cualquiera", "OtraCosa.exe", tipo));
    }

    [Fact]
    public void El_folio_con_guion_parte_la_serie_y_el_numero()
    {
        /* "A-1234": la A se PEGA a la serie base y 1234 es el folio. */
        var (serie, folio) = ReglasFacturacion.SerieYFolio("Z", "A-1234");
        Assert.Equal("ZA", serie);
        Assert.Equal(1234, folio);
    }

    [Fact]
    public void El_folio_sin_guion_es_todo_folio()
    {
        var (serie, folio) = ReglasFacturacion.SerieYFolio("CS3", "9876");
        Assert.Equal("CS3", serie);
        Assert.Equal(9876, folio);
    }

    [Fact]
    public void Un_folio_vacio_deja_la_serie_intacta_y_el_folio_en_cero()
    {
        var (serie, folio) = ReglasFacturacion.SerieYFolio("T", null);
        Assert.Equal("T", serie);
        Assert.Equal(0, folio);
    }

    [Theory]
    [InlineData("1", "01")]
    [InlineData("3", "03")]
    [InlineData("28", "28")]
    [InlineData("", "")]
    public void La_forma_de_pago_va_con_dos_digitos_como_la_pide_el_SAT(string entrada, string esperada)
    {
        Assert.Equal(esperada, ReglasFacturacion.FormaPagoSat(entrada));
    }

    [Theory]
    [InlineData("G03 - Gastos en general", "G03")]
    [InlineData("601", "601")]
    [InlineData("  PUE  ", "PUE")]
    [InlineData(null, "")]
    public void De_los_catalogos_del_SAT_solo_viaja_la_clave(string? entrada, string esperada)
    {
        Assert.Equal(esperada, ReglasFacturacion.ClaveSat(entrada));
    }

    [Fact]
    public void El_delimitador_de_los_Params_son_CUATRO_arrobas()
    {
        /*
          El comentario de legacy dice cinco y el codigo escribe cuatro. Manda el
          codigo, porque es el formato que ya esta guardado en la base y el que el
          reporte vuelve a leer.
        */
        Assert.Equal("@@@@", ReglasFacturacion.DelimitadorParams);
        Assert.Equal(4, ReglasFacturacion.DelimitadorParams.Length);
    }

    [Fact]
    public void Los_campos_de_un_Param_conservan_su_posicion_aunque_vengan_vacios()
    {
        /*
          El que lee cruza por posicion. Un campo que se salta desplaza todos los de
          despues: el sello donde deberia ir la ciudad.
        */
        var param = ReglasFacturacion.Param("Calle", null, "", "Centro");
        Assert.Equal("Calle@@@@@@@@@@@@Centro", param);
        Assert.Equal(4, param.Split("@@@@").Length);
    }

    [Fact]
    public void La_cadena_del_QR_lleva_el_total_con_seis_decimales_y_los_ocho_ultimos_del_sello()
    {
        var cadena = ReglasFacturacion.CadenaQr(
            "https://verificacfdi.facturaelectronica.sat.gob.mx/default.aspx?",
            "A1B2C3",
            "DAZ100826S29",
            "XAXX010101000",
            1234.5m,
            selloCfdi: "0123456789ABCDEFGH12345678");

        Assert.Contains("id=A1B2C3", cadena);
        Assert.Contains("re=DAZ100826S29", cadena);
        Assert.Contains("rr=XAXX010101000", cadena);
        Assert.Contains("tt=1234.500000", cadena);
        Assert.EndsWith("&fe=12345678", cadena);
    }

    [Fact]
    public void Un_sello_mas_corto_de_ocho_deja_el_fe_vacio_y_no_revienta()
    {
        var cadena = ReglasFacturacion.CadenaQr("u?", "id", "re", "rr", 1m, "ABC");
        Assert.EndsWith("&fe=", cadena);
    }

    [Fact]
    public void La_fecha_de_pago_del_complemento_va_en_el_formato_del_SAT()
    {
        /*
          Legacy la arma con Substring de posiciones fijas sobre la cadena del
          cliente mas la hora de otro campo, asi que un dia escrito distinto daba
          una fecha de pago distinta DENTRO del CFDI.
        */
        var pago = new DateTime(2024, 1, 3, 21, 48, 18);
        Assert.Equal("2024-01-03T21:48:18", ReglasFacturacion.FechaPagoSat(pago));
        Assert.Equal(string.Empty, ReglasFacturacion.FechaPagoSat(null));
    }

    [Fact]
    public void La_fecha_del_comprobante_se_guarda_en_ISO_con_su_hora()
    {
        var momento = new DateTime(2026, 2, 28, 10, 45, 35);
        Assert.Equal("2026-02-28 10:45:35", ReglasFacturacion.FechaHoraCfdi(momento));
    }
}
