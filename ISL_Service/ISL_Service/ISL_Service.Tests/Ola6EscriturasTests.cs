using System.Data;
using System.Globalization;
using ISL_Service.Application.DTOs.OperacionMac;
using ISL_Service.Application.DTOs.SeguridadMac;
using ISL_Service.Infrastructure.Repositories;
using ISL_Service.Utils;

namespace ISL_Service.Tests;

/// <summary>
/// Pruebas de la logica PURA de la ola 6: lo que se puede comprobar sin base de
/// datos. Son tres cosas, y las tres son de las que fallan en silencio:
///
///  1. El formato del CorteInfo. Veinte importes en una sola cadena que
///     sp_n_InsertarCorte vuelve a partir con CHARINDEX. Si sobra o falta un
///     delimitador, el procedimiento no falla: lee corrido y mete el efectivo
///     donde iba el total de gastos. El corte cuadra en la pantalla y no cuadra
///     en la contabilidad.
///
///  2. La eleccion del procedimiento al aplicar una transferencia. En legacy eran
///     dos banderas sueltas con dos `if`: con las dos en true ganaba cascos y con
///     las dos en false se ejecutaba un procedimiento con nombre vacio.
///
///  3. El nombre de las columnas que salen al JSON. 'IDDiaFestivo' tiene que
///     llegar como 'idDiaFestivo'; estaba llegando como '205DiaFestivo' porque
///     sumar dos char en C# suma sus codigos.
/// </summary>
public class Ola6EscriturasTests
{
    // -----------------------------------------------------------------------
    // 1. CorteInfo
    // -----------------------------------------------------------------------

    private static DesgloseCorte DesgloseNumerado() => new()
    {
        SaeEsquina = 1, SaeEsquinaDomingo = 2, SaeLocalTauro = 3, TotalSae = 4,
        MacCamioneta = 5, MacAgentes = 6, TotalMac = 7, Transferencias = 8,
        TransferenciasTiendaEsquina = 9, TransferenciasLocalTauro = 10,
        DepositoCheque = 11, DepositoEfectivo = 12,
        Tarjetas = 13, TarjetasTiendas = 130,
        Cheque = 14, Efectivo = 15, Morraya = 16, TotalGastos = 17,
        TotalDesglose = 18, GranTotal = 19, NoAplicadoDesglose = 20
    };

    [Fact]
    public void CorteInfo_tiene_veinte_campos_en_el_orden_del_procedimiento()
    {
        var cadena = CorteInfoLegacy.Armar(DesgloseNumerado());

        var campos = cadena.Split(CorteInfoLegacy.Delimitador);

        // Veinte lugares: el treceavo lleva las dos tarjetas juntas.
        Assert.Equal(20, campos.Length);
        Assert.Equal("1.00", campos[0]);
        Assert.Equal("12.00", campos[11]);
        Assert.Equal("13.00||||130.00", campos[12]);
        Assert.Equal("14.00", campos[13]);
        Assert.Equal("19.00", campos[18]);
        Assert.Equal("20.00", campos[19]);
    }

    [Fact]
    public void CorteInfo_no_termina_con_delimitador()
    {
        // El procedimiento se lleva el RESTO de la cadena como
        // NoAplicadoDesglose: un delimitador de sobra al final le mete la cadena
        // vacia y el importe se pierde.
        var cadena = CorteInfoLegacy.Armar(DesgloseNumerado());

        Assert.False(cadena.EndsWith(CorteInfoLegacy.Delimitador, StringComparison.Ordinal));
        Assert.EndsWith("20.00", cadena, StringComparison.Ordinal);
    }

    [Fact]
    public void CorteInfo_separa_las_dos_tarjetas_con_su_propio_delimitador()
    {
        var cadena = CorteInfoLegacy.Armar(DesgloseNumerado());

        // Una sola vez en toda la cadena, y en el campo 13.
        var veces = cadena.Split(CorteInfoLegacy.DelimitadorTarjetas).Length - 1;
        Assert.Equal(1, veces);
        Assert.Contains("13.00||||130.00", cadena, StringComparison.Ordinal);
    }

    [Fact]
    public void CorteInfo_escribe_los_decimales_con_punto_aunque_la_maquina_use_coma()
    {
        // Este es el defecto que se esta evitando: legacy usaba .ToString() con la
        // cultura del servidor. En una maquina con coma decimal, 12.5 se vuelve
        // "12,5" y el procedimiento, que parte cadenas, puede leer dos valores
        // donde habia uno y correr todo el desglose un lugar.
        var anterior = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("es-ES"); // coma decimal
            var cadena = CorteInfoLegacy.Armar(new DesgloseCorte { SaeEsquina = 12.5m });

            Assert.StartsWith("12.50@@@@", cadena, StringComparison.Ordinal);
            Assert.DoesNotContain(",", cadena);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = anterior;
        }
    }

    [Fact]
    public void CorteInfo_de_un_corte_en_ceros_sigue_teniendo_veinte_lugares()
    {
        // Un corte sin movimientos es legitimo (un domingo cerrado). La cadena
        // tiene que conservar su forma: si se fueran los lugares vacios, el
        // procedimiento leeria todo corrido.
        var cadena = CorteInfoLegacy.Armar(new DesgloseCorte());

        Assert.Equal(20, cadena.Split(CorteInfoLegacy.Delimitador).Length);
    }

    // -----------------------------------------------------------------------
    // 2. A que se aplica una transferencia
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("dinero", "dbo.sp_n_AplicarBancoTransferencia")]
    [InlineData("DINERO", "dbo.sp_n_AplicarBancoTransferencia")]
    [InlineData("  dinero  ", "dbo.sp_n_AplicarBancoTransferencia")]
    [InlineData("cascos", "dbo.sp_n_AplicarBancoTransferenciaUsados")]
    [InlineData("Cascos", "dbo.sp_n_AplicarBancoTransferenciaUsados")]
    public void Aplicar_transferencia_elige_el_procedimiento_por_el_valor(string valor, string esperado)
    {
        Assert.Equal(esperado, AplicacionTransferencia.SpDe(valor));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("ambos")]
    [InlineData("dinero y cascos")]
    public void Aplicar_transferencia_con_un_valor_que_no_existe_no_elige_nada(string? valor)
    {
        // null significa "no hay procedimiento" y el controlador contesta 400.
        // En legacy este caso armaba un SqlCommand con nombre VACIO y el usuario
        // leia "[No se pudo procesar ] - ..." sin saber que le faltaba elegir.
        Assert.Null(AplicacionTransferencia.SpDe(valor));
    }

    // -----------------------------------------------------------------------
    // 3. Los veinte parametros de reporte
    // -----------------------------------------------------------------------

    [Fact]
    public void Parametros_de_reporte_convierte_los_nulos_en_cadena_vacia()
    {
        // En legacy cualquiera de los veinte en nulo tiraba la llamada con una
        // excepcion de argumento nulo, asi que el cliente tenia que mandar los
        // veinte aunque usara tres.
        var r = new GuardarParametrosRequest { IdReporte = 7, Param1 = "uno", Param3 = "tres" };

        var valores = r.EnOrden();

        Assert.Equal(20, valores.Length);
        Assert.Equal("uno", valores[0]);
        Assert.Equal(string.Empty, valores[1]);
        Assert.Equal("tres", valores[2]);
        Assert.All(valores, x => Assert.NotNull(x));
    }

    // -----------------------------------------------------------------------
    // 4. Los nombres de las columnas que salen al JSON
    // -----------------------------------------------------------------------

    [Fact]
    public void Las_columnas_que_empiezan_con_ID_salen_como_id()
    {
        // REGRESION. 'IDDiaFestivo' estaba saliendo al JSON como '205DiaFestivo':
        // en C#, 'i' + 'd' no es "id", es 205, porque sumar dos char suma sus
        // codigos. No se habia visto porque los SP de los primeros modulos que
        // usan este helper ya devuelven camelCase y la rama no se tocaba.
        // Comprobado contra Produccion_svr con GET api/catalogos/dias-festivos.
        var dt = new DataTable();
        dt.Columns.Add("IDDiaFestivo", typeof(int));
        dt.Columns.Add("IDStatus", typeof(int));
        dt.Columns.Add("DiaFestivo", typeof(DateTime));
        dt.Columns.Add("diaEntrada1", typeof(string));
        dt.Rows.Add(16, 1, new DateTime(2025, 5, 1), "08:00");

        var renglones = Funciones.DataTableToRows(dt);

        var fila = Assert.Single(renglones);
        Assert.True(fila.ContainsKey("idDiaFestivo"), "IDDiaFestivo debe salir como idDiaFestivo");
        Assert.True(fila.ContainsKey("idStatus"), "IDStatus debe salir como idStatus");
        Assert.True(fila.ContainsKey("diaFestivo"), "DiaFestivo debe salir como diaFestivo");
        // Lo que ya venia en camelCase no se toca.
        Assert.True(fila.ContainsKey("diaEntrada1"));
        Assert.DoesNotContain("205DiaFestivo", fila.Keys);
    }

    [Fact]
    public void Los_nulos_de_la_base_llegan_como_null_y_no_como_cero()
    {
        // Ya lo decia el comentario del helper y conviene tenerlo clavado: cero
        // asistencias y "no se pudo calcular" no son lo mismo.
        var dt = new DataTable();
        dt.Columns.Add("IDEmpleado", typeof(int));
        dt.Columns.Add("asistencias", typeof(int));
        dt.Rows.Add(5, DBNull.Value);

        var fila = Assert.Single(Funciones.DataTableToRows(dt));

        Assert.Equal(5, fila["idEmpleado"]);
        Assert.Null(fila["asistencias"]);
    }
}
