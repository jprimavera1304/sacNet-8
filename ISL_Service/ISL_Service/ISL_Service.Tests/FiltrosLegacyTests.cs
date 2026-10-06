using ISL_Service.Application.Services;
using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Tests;

/// <summary>
/// Las traducciones de filtro de las consultas migradas de MacServicios2.
///
/// Se prueban estas y no los repositorios porque es donde una migracion
/// descuidada pierde datos SIN que salga un error: una fecha mal volteada no
/// truena, devuelve otros renglones; una lista unida con el separador que no es
/// devuelve cero renglones. Las dos cosas se ven en pantalla como "no hay
/// datos", que es lo que nadie reporta como falla.
/// </summary>
public class FiltrosLegacyTests
{
    // ------------------------------------------------------------------ fechas

    [Theory]
    // ISO, que es lo que manda el front nuevo.
    [InlineData("2026-02-01", "2026-02-01 00:00:00")]
    // El formato de la pantalla de Mac31.
    [InlineData("01-02-2026", "2026-02-01 00:00:00")]
    [InlineData("01/02/2026", "2026-02-01 00:00:00")]
    // Un digito: esto es lo que a legacy le corrompe la fecha con su Substring.
    [InlineData("1-2-2026", "2026-02-01 00:00:00")]
    // Ya traia hora: se ignora, el inicio del rango es el dia completo.
    [InlineData("2026-02-01 13:45:00", "2026-02-01 00:00:00")]
    public void Fecha_inicio_de_rango_siempre_a_medianoche(string entrada, string esperado)
    {
        Assert.Equal(esperado, FiltrosLegacy.Fecha(entrada, finDelDia: false));
    }

    [Theory]
    [InlineData("2026-02-01", "2026-02-01 23:59:59")]
    [InlineData("01-02-2026", "2026-02-01 23:59:59")]
    [InlineData("1-2-2026", "2026-02-01 23:59:59")]
    public void Fecha_fin_de_rango_llega_al_ultimo_segundo_del_dia(string entrada, string esperado)
    {
        // Sin esto, un rango "hoy a hoy" pierde todo lo capturado despues de
        // medianoche. Es el " 23:59:59" que legacy pega a mano.
        Assert.Equal(esperado, FiltrosLegacy.Fecha(entrada, finDelDia: true));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Fecha_vacia_se_queda_vacia(string? entrada)
    {
        // "" es lo que el procedimiento entiende por "sin filtro de fecha". Si
        // aqui saliera una fecha inventada, una consulta sin rango se volveria
        // una consulta acotada y faltarian renglones.
        Assert.Equal(string.Empty, FiltrosLegacy.Fecha(entrada, finDelDia: false));
    }

    [Fact]
    public void Fecha_que_no_se_entiende_se_pasa_tal_cual()
    {
        // A proposito NO se traga el error: mandar "" convertiria un filtro
        // escrito mal en una consulta sin filtro, y el usuario creeria estar
        // viendo febrero cuando esta viendo todo.
        Assert.Equal("ayer", FiltrosLegacy.Fecha("ayer", finDelDia: false));
    }

    [Fact]
    public void Fecha_el_dia_31_no_se_desborda_al_mes_siguiente()
    {
        Assert.Equal("2026-01-31 23:59:59", FiltrosLegacy.Fecha("31-01-2026", finDelDia: true));
    }

    // ------------------------------------------------------------------ folios

    [Theory]
    [InlineData(null, "0")]
    [InlineData("", "0")]
    [InlineData("  ", "0")]
    [InlineData("135346", "135346")]
    [InlineData("  135346  ", "135346")]
    public void Folio_vacio_vale_cero(string? entrada, string esperado)
    {
        // Es lo que legacy consigue pegandole un "0" por delante
        // (ConsultaCobrosService.cs:45). Aqui es explicito.
        Assert.Equal(esperado, FiltrosLegacy.Folio(entrada));
    }

    // --------------------------------------------------------------------- ids

    [Fact]
    public void Ids_se_unen_con_virgulilla_no_con_coma()
    {
        // LA PRUEBA QUE IMPORTA. Los `sp_n_` traen el separador escrito a mano
        // dentro (SET @Delimitador = '~'). Con coma y DOS ids el filtro no
        // encuentra nada; con UNO funciona igual, asi que el error pasa
        // desapercibido hasta que alguien marca dos casillas.
        Assert.Equal("94~87", FiltrosLegacy.Ids(new[] { 94, 87 }));
    }

    [Fact]
    public void Ids_quita_ceros_y_repetidos()
    {
        // Un 0 en la lista la convierte en un filtro que no empareja con ningun
        // id y la consulta sale vacia sin decir por que.
        Assert.Equal("94~87", FiltrosLegacy.Ids(new[] { 94, 0, 87, 94, -3 }));
    }

    [Fact]
    public void Ids_sin_nada_util_queda_vacio()
    {
        // Vacio es lo que el procedimiento entiende por "todos".
        Assert.Equal(string.Empty, FiltrosLegacy.Ids(new[] { 0, -1 }));
        Assert.Equal(string.Empty, FiltrosLegacy.Ids(null));
    }

    [Fact]
    public void Ids_respeta_la_cadena_cruda_si_viene()
    {
        // La pantalla puede mandar la lista ya armada; entonces manda ella.
        Assert.Equal("1~2~3", FiltrosLegacy.Ids(new[] { 9 }, "1~2~3"));
    }

    [Fact]
    public void Ids_acepta_otro_separador_cuando_el_procedimiento_lo_pide()
    {
        Assert.Equal("1,2", FiltrosLegacy.Ids(new[] { 1, 2 }, null, ","));
    }

    // ------------------------------------------------------------------ textos

    [Fact]
    public void Texto_quita_el_apostrofo()
    {
        // No es cosmetico: los `sp_n_Consultar*` arman el WHERE concatenando y lo
        // corren con EXEC(@Sql), asi que pasarlo como SqlParameter NO protege.
        Assert.Equal("OREILLYS", FiltrosLegacy.TextoSinApostrofo("O'REILLY'S"));
    }

    [Fact]
    public void Texto_nunca_devuelve_null()
    {
        Assert.Equal(string.Empty, FiltrosLegacy.Texto(null));
        Assert.Equal(string.Empty, FiltrosLegacy.TextoSinApostrofo(null));
    }

    // --------------------------------------------- reglas de los dos servicios

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    // Cualquier otra cosa cae a 0, que es el default de legacy: un numero suelto
    // de la pantalla no debe llegar al procedimiento a ver que pasa.
    [InlineData(3, 0)]
    [InlineData(-1, 0)]
    [InlineData(99, 0)]
    public void Formato_solo_acepta_los_tres_del_enum_de_Mac31(int entrada, int esperado)
    {
        Assert.Equal(esperado, VentasCobranzaConsultaService.NormalizarFormato(entrada));
    }

    [Fact]
    public void Limpiar_ids_de_descuentos_quita_ceros_y_repetidos()
    {
        Assert.Equal(new List<int> { 94, 87 },
            VentasDescuentosConsultaService.Limpiar(new[] { 94, 0, 87, 94 }));
        Assert.Empty(VentasDescuentosConsultaService.Limpiar(null));
    }
}
