using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Tests;

/*
  LO QUE SE PRUEBA AQUI ES LO QUE SE PUEDE EQUIVOCAR EN SILENCIO.

  Los sp_n_ de ventas reciben las listas como cadenas y las vuelven a partir
  dentro. Cuando el separador, el formato del numero o el relleno estan mal, el
  procedimiento NO falla: lee menos renglones, o lee un numero partido en dos, y
  guarda otra cosa. No hay excepcion, no hay mensaje y el sintoma aparece semanas
  despues en un importe raro.

  Por eso estas pruebas miran el caracter exacto, no "que funcione".
*/
public class FiltrosLegacyOla4Tests
{
    [Fact]
    public void El_separador_de_los_sp_n_ES_la_virgulilla()
    {
        /*
          La tentacion natural es la coma, y una lista con comas no da error: le
          llega al procedimiento como UN valor que no es numero, no encuentra nada
          que procesar y contesta como si hubiera trabajado. Comprobado en el
          cuerpo de los procedimientos: todos hacen SELECT @Delimitador = '~'.
        */
        Assert.Equal("~", FiltrosLegacy.SeparadorIds);
        Assert.Equal("~", FiltrosLegacy.SeparadorUsados);
    }

    [Fact]
    public void Las_listas_de_ids_se_unen_con_virgulilla()
    {
        Assert.Equal("10~20~30", FiltrosLegacy.Ids(new[] { 10, 20, 30 }));
        Assert.Equal("7", FiltrosLegacy.Ids(new[] { 7 }));
    }

    [Fact]
    public void Una_lista_nula_o_vacia_da_cadena_vacia_y_no_revienta()
    {
        /*
          En legacy, string.Join sobre una lista null lanza ArgumentNullException y
          el endpoint contesta 400 con el stack: es lo que le pasa al detalle de
          descuentos cuando el cliente no manda las tres listas.
        */
        Assert.Equal(string.Empty, FiltrosLegacy.Ids(null));
        Assert.Equal(string.Empty, FiltrosLegacy.Ids(Array.Empty<int>()));
        Assert.Equal(string.Empty, FiltrosLegacy.ImportesUsados(null));
        Assert.Equal(string.Empty, FiltrosLegacy.TextosUsados(null));
    }

    [Fact]
    public void Los_importes_van_con_punto_decimal_y_cultura_invariante()
    {
        /*
          ESTE ES EL IMPORTANTE.

          Con la cultura del servidor en espanol, 12.5m se escribe "12,5" y la coma
          no rompe nada... hasta que el separador de la lista fuera una coma. Pero
          el riesgo de verdad es otro: un procedimiento que espera "12.5" recibiendo
          "12,5" convierte mal o falla con un mensaje de SQL Server que no dice de
          que campo habla. Legacy hace string.Join sobre decimales SIN cultura.
        */
        var texto = FiltrosLegacy.ImportesUsados(new[] { 12.5m, 100m, 0.25m });
        Assert.Equal("12.5~100~0.25", texto);
        Assert.DoesNotContain(",", texto);
    }

    [Fact]
    public void El_rango_de_fechas_abre_a_las_cero_y_cierra_al_ultimo_segundo()
    {
        var dia = new DateTime(2026, 3, 5, 14, 37, 12);

        Assert.Equal("2026-03-05 00:00:00", FiltrosLegacy.FechaDesde(dia));
        Assert.Equal("2026-03-05 23:59:59", FiltrosLegacy.FechaHasta(dia));
    }

    [Fact]
    public void Hay_parametros_que_llevan_la_fecha_SIN_hora_porque_el_sp_n_la_completa()
    {
        /*
          ESTO SALIO DE UN CASO ROJO Y POR ESO ESTA PROBADO.

          sp_n_ActualizaStatusVentasDescuentosAjustes y
          sp_n_InsertarVentasDescuentosAjustes hacen, literalmente:

              -- Agregar la hora
              select @Fecha = @Fecha + ' ' + SUBSTRING(CAST(CONVERT(TIME, GETDATE()) as varchar(20)), 1, 8)

          Con "2026-10-06 00:00:00" el procedimiento acaba con
          "2026-10-06 00:00:00 23:23:32", que no es una fecha. Y como corre con
          SET ANSI_WARNINGS OFF no revienta: guarda NULL en la columna y contesta
          result = 1. El ajuste quedaba autorizado SIN fecha de autorizacion.
        */
        var dia = new DateTime(2026, 10, 6, 14, 37, 12);

        Assert.Equal("2026-10-06", FiltrosLegacy.SoloFecha(dia));
        Assert.DoesNotContain(":", FiltrosLegacy.SoloFecha(dia));
        Assert.Equal(string.Empty, FiltrosLegacy.SoloFecha(null));
    }

    [Fact]
    public void Sin_fecha_el_filtro_va_vacio_que_es_como_los_sp_n_leen_sin_filtro()
    {
        Assert.Equal(string.Empty, FiltrosLegacy.Fecha(null));
        Assert.Equal(string.Empty, FiltrosLegacy.FechaDesde(null));
        Assert.Equal(string.Empty, FiltrosLegacy.FechaHasta(null));
    }

    [Fact]
    public void La_fecha_va_en_ISO_y_asi_no_hace_falta_leer_Constantes()
    {
        /*
          Es la razon de que esta ola no dispare el cierre del dia: legacy leia
          `formatoFechaDB` con sp_n_ConsultaConstantes solo para voltear la cadena
          con Substring. Una fecha en "yyyy-MM-dd HH:mm:ss" la entiende SQL Server
          con cualquier DATEFORMAT, asi que no hay nada que preguntar.
        */
        var momento = new DateTime(2026, 12, 31, 23, 5, 9);
        Assert.Equal("2026-12-31 23:05:09", FiltrosLegacy.Fecha(momento));
    }

    [Fact]
    public void El_texto_pierde_el_apostrofo_porque_asi_lo_guardaba_Mac31()
    {
        /*
          Aqui todo va parametrizado, asi que esto no es defensa contra inyeccion:
          es fidelidad de datos. Si el web guardara el apostrofo, el mismo renglon
          escrito desde el web y desde Mac31 no seria igual.
        */
        Assert.Equal("Casa de Juan", FiltrosLegacy.Texto("  Casa de 'Juan'  "));
        Assert.Equal(string.Empty, FiltrosLegacy.Texto(null));
    }

    [Fact]
    public void El_texto_se_recorta_a_lo_que_cabe_en_el_parametro()
    {
        var largo = new string('A', 500);
        Assert.Equal(400, FiltrosLegacy.Texto(largo, 400).Length);
    }

    [Fact]
    public void LimpiarIds_quita_ceros_negativos_y_repetidos()
    {
        /*
          Es lo que la pantalla queria decir cuando manda [0, 123, 123, -1]: un
          cero en una lista de ids es "no seleccione nada", y mandarlo al
          procedimiento le hace buscar el id 0.
        */
        var limpio = FiltrosLegacy.LimpiarIds(new[] { 0, 123, 123, -1, 7 });
        Assert.Equal(new[] { 123, 7 }, limpio);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 0)]
    [InlineData(-1, 0)]
    [InlineData(null, 0)]
    public void La_presentacion_se_acota_al_enum_de_Mac31(int? pedida, int esperada)
    {
        /* enumPresentacion de Mac31 solo tiene 0, 1 y 2. Cualquier otro numero
           baja al procedimiento como una rama que nadie escribio. */
        Assert.Equal(esperada, FiltrosLegacy.Presentacion(pedida));
    }
}
