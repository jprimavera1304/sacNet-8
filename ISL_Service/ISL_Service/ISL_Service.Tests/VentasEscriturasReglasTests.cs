using ISL_Service.Application.DTOs.VentasGarantias;
using ISL_Service.Application.Services;
using ISL_Service.Infrastructure.Repositories;

namespace ISL_Service.Tests;

/*
  LAS REGLAS DE LA OLA 4 QUE SE PUEDEN PROBAR SIN BASE.

  Son tres y las tres deciden algo que no se nota cuando esta mal:

    * El ORDEN del lote de pedidos decide que folio de remision le toca a cada
      pedido.
    * El TEXTO DE BITACORA de garantias es lo que alguien lee con los ojos meses
      despues, y ya hay diez anos escritos con un formato.
    * El PORCENTAJE de un descuento es dinero que se le reconoce al cliente.
*/
public class VentasEscriturasReglasTests
{
    /* ---------------- El orden del lote de pedidos ---------------- */

    [Fact]
    public void El_lote_de_pedidos_se_ordena_como_TEXTO_igual_que_Mac31()
    {
        /*
          POR QUE SE CONSERVA UN ORDEN QUE PARECE EQUIVOCADO.

          Legacy hace List<string>.Sort() sobre una lista de cadenas, asi que ordena
          ordinalmente: ["2","10","9"] se vuelve ["10","2","9"]. Ese orden decide la
          SECUENCIA EN QUE SE CONSUMEN LOS FOLIOS de remision.

          Ordenar numericamente —que es "lo correcto"— haria que el mismo lote
          procesado desde el web y desde Mac31 asignara folios distintos a los
          mismos pedidos. Dos mitades del sistema que numeran distinto es peor que
          un orden raro.
        */
        var orden = PedidosOperacionesService.OrdenDeLegacy(new[] { 2, 10, 9 });
        Assert.Equal(new[] { 10, 2, 9 }, orden);
    }

    [Fact]
    public void El_orden_del_lote_es_estable_con_un_solo_pedido()
    {
        Assert.Equal(new[] { 7 }, PedidosOperacionesService.OrdenDeLegacy(new[] { 7 }));
        Assert.Empty(PedidosOperacionesService.OrdenDeLegacy(Array.Empty<int>()));
    }

    [Fact]
    public void El_orden_del_lote_pone_el_100_antes_del_99()
    {
        /* Otra vez: ordinal, no numerico. "100" < "99" porque '1' < '9'. */
        Assert.Equal(new[] { 100, 99 }, PedidosOperacionesService.OrdenDeLegacy(new[] { 99, 100 }));
    }

    /* ---------------- El texto de bitacora de garantias ---------------- */

    [Fact]
    public void El_texto_de_bitacora_de_garantias_respeta_el_formato_de_Mac31()
    {
        /*
          Dos espacios antes y despues del guion, "VALE: " con uno, elementos
          pegados con virgulilla y SIN virgulilla final. Lo armaba el cliente de
          WinForms recorriendo la rejilla; ahora lo arma el servidor, byte por byte
          igual, porque la bitacora vieja ya esta escrita asi y alguien la lee.
        */
        var texto = VentasGarantiasRepository.TextoDeBitacora(new[]
        {
            new GarantiaPartida { Codigo = "ABC123", Ticket = "9981" },
            new GarantiaPartida { Codigo = "XYZ777", Ticket = "9982" }
        });

        Assert.Equal("CLAVE: ABC123  -  VALE: 9981~CLAVE: XYZ777  -  VALE: 9982", texto);
        Assert.False(texto.EndsWith('~'));
    }

    [Fact]
    public void Una_sola_partida_no_lleva_virgulilla()
    {
        var texto = VentasGarantiasRepository.TextoDeBitacora(new[]
        {
            new GarantiaPartida { Codigo = "A1", Ticket = "5" }
        });

        Assert.Equal("CLAVE: A1  -  VALE: 5", texto);
        Assert.DoesNotContain("~", texto);
    }

    [Fact]
    public void Sin_partidas_el_texto_va_vacio()
    {
        Assert.Equal(string.Empty, VentasGarantiasRepository.TextoDeBitacora(null));
        Assert.Equal(string.Empty, VentasGarantiasRepository.TextoDeBitacora(Array.Empty<GarantiaPartida>()));
    }

    [Fact]
    public void Una_partida_sin_codigo_ni_vale_conserva_su_sitio_en_el_texto()
    {
        /* El texto es humano, no estructurado, pero una partida que desaparece
           hace que el numero de elementos no coincida con la lista de ids y eso
           confunde a quien compara las dos cosas. */
        var texto = VentasGarantiasRepository.TextoDeBitacora(new[]
        {
            new GarantiaPartida { Codigo = "A1", Ticket = "5" },
            new GarantiaPartida()
        });

        Assert.Equal("CLAVE: A1  -  VALE: 5~CLAVE:   -  VALE: ", texto);
    }

    [Fact]
    public void El_apostrofo_sale_del_texto_de_bitacora()
    {
        var texto = VentasGarantiasRepository.TextoDeBitacora(new[]
        {
            new GarantiaPartida { Codigo = "A'1", Ticket = "5'5" }
        });

        Assert.Equal("CLAVE: A1  -  VALE: 55", texto);
    }

    /* ---------------- El porcentaje de un descuento ---------------- */

    [Theory]
    [InlineData("10", 10)]
    [InlineData("10.5", 10.5)]
    [InlineData("10,5", 10.5)]
    [InlineData(" 7 ", 7)]
    [InlineData("12%", 12)]
    [InlineData("0", 0)]
    [InlineData("100", 100)]
    public void Un_porcentaje_valido_se_entiende_escriba_quien_lo_escriba(string texto, double esperado)
    {
        /*
          La coma decimal se acepta porque la gente la escribe, pero se interpreta
          con cultura invariante: el resultado no depende de como este configurado
          el servidor.
        */
        Assert.True(VentasDescuentosService.EsPorcentajeValido(texto, out var valor));
        Assert.Equal((decimal)esperado, valor);
    }

    [Theory]
    [InlineData("101")]
    [InlineData("-5")]
    [InlineData("mucho")]
    [InlineData("")]
    [InlineData(null)]
    public void Un_porcentaje_fuera_de_rango_o_que_no_es_numero_no_pasa(string? texto)
    {
        /*
          El procedimiento recibe varchar(50) y convierte el. Con un texto que no es
          numero, el error viene de SQL Server y no dice de que campo habla; con un
          110 no hay error y el importe del renglon queda NEGATIVO.
        */
        Assert.False(VentasDescuentosService.EsPorcentajeValido(texto, out _));
    }

    /* ---------------- El mensaje de una cancelacion de CFDI ---------------- */

    [Theory]
    [InlineData(2, 0, "LA FACTURA HA SIDO CANCELADA.")]
    [InlineData(2, 1, "LA NOTA DE CREDITO HA SIDO CANCELADA.")]
    [InlineData(2, 4, "EL COMPLEMENTO DE PAGO HA SIDO CANCELADO.")]
    public void Cuando_el_SAT_acepta_el_mensaje_dice_cancelado(int status, int tipo, string esperado)
    {
        Assert.Equal(esperado, VentasFacturacionService.MensajeDeCancelacion(status, tipo));
    }

    [Fact]
    public void Cuando_el_cliente_RECHAZA_el_mensaje_NO_dice_cancelada()
    {
        /*
          ESTE ES EL DEFECTO MAS CARO DE LEGACY EN FACTURACION.

          Su codigo mapea bien los cuatro estados del SAT y acto seguido los
          sobreescribe con "aceptada" —con el comentario "el servicio no informa
          bien"—, asi que una cancelacion RECHAZADA por el cliente queda en Mac31
          como cancelada: la factura sigue VALIDA ante el SAT y el sistema la da por
          muerta. Aqui el mensaje y el estatus son los reales.
        */
        var rechazada = VentasFacturacionService.MensajeDeCancelacion(4, 0);
        Assert.Contains("RECHAZO", rechazada);
        Assert.Contains("NO FUE CANCELADO", rechazada);

        var pendiente = VentasFacturacionService.MensajeDeCancelacion(3, 0);
        Assert.Contains("EN ESPERA", pendiente);

        var expirada = VentasFacturacionService.MensajeDeCancelacion(5, 0);
        Assert.Contains("EXPIRO", expirada);
    }
}
