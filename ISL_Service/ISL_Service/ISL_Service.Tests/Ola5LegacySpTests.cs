using System.Data;
using System.Globalization;
using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Tests;

/*
  LO QUE SE LE HACE A UN PARAMETRO ANTES DE DARSELO A UN sp_n_ DE OBRA O AVIACION.

  Estas cuatro cosas son la raiz de media docena de defectos del lote y se pueden
  probar sin base, asi que se prueban.
*/
public class Ola5LegacySpTests
{
    // ------------------------------------------------------------------
    // El apostrofo: fuera de los filtros, dentro de los datos
    // ------------------------------------------------------------------

    [Fact]
    public void Filtro_Quita_El_Apostrofo()
    {
        // Estos SP arman el WHERE concatenando y lo corren con EXEC(@Sql): pasar
        // el filtro como SqlParameter no protege porque la inyeccion ocurre
        // dentro del procedimiento.
        Assert.Equal("OBRIEN", LegacySp.Filtro("O'BRIEN"));
        Assert.Equal("a OR 1=1 --", LegacySp.Filtro(" a' OR 1=1 -- "));
    }

    [Fact]
    public void Filtro_Traduce_Nulo_Y_Vacio_A_Cadena_Vacia()
    {
        // "" es lo que estos SP entienden por "sin filtro": comparan IF @X <> ''.
        // Legacy dejaba el string sin inicializar y AddWithValue(null) daba
        // "Parameter has no default value": un 500 por omitir un filtro opcional.
        Assert.Equal(string.Empty, LegacySp.Filtro(null));
        Assert.Equal(string.Empty, LegacySp.Filtro("   "));
    }

    [Fact]
    public void Texto_Conserva_El_Apostrofo_Porque_Es_Parte_Del_Nombre()
    {
        // sp_n_Insertar_MCPuesto guarda con un INSERT parametrizado de verdad; el
        // apostrofo es inofensivo ahi. Legacy lo borraba y "ASESOR DE O'HIGGINS"
        // se guardaba sin el.
        Assert.Equal("ASESOR DE O'HIGGINS", LegacySp.Texto("  ASESOR DE O'HIGGINS  "));
    }

    [Fact]
    public void Texto_Traduce_Nulo_A_Cadena_Vacia()
    {
        Assert.Equal(string.Empty, LegacySp.Texto(null));
    }

    // ------------------------------------------------------------------
    // Las fechas: ISO, sin voltear dias y meses y sin leer Constantes
    // ------------------------------------------------------------------

    [Fact]
    public void Las_Fechas_Van_En_Iso_Con_La_T()
    {
        // Con la T, SQL Server las interpreta igual con cualquier DATEFORMAT. Por
        // eso ninguna consulta de este lote necesita Constantes.formatoFechaDB, y
        // por tanto ninguna llama a sp_n_ConsultaConstantes, que cierra el dia.
        var dia = new DateOnly(2026, 3, 7);
        Assert.Equal("2026-03-07T00:00:00", LegacySp.DesdeElDia(dia));
        Assert.Equal("2026-03-07T23:59:59", LegacySp.HastaElDia(dia));
        Assert.Equal("2026-03-07T00:00:00", LegacySp.ElDia(dia));
    }

    [Fact]
    public void Sin_Fecha_El_Filtro_Va_Vacio_Y_No_Revienta()
    {
        // El ConvertFtmDate de Legacy hacia Substring(3,2) sobre la cadena: con la
        // fecha vacia lanzaba y el servicio contestaba 400 con el stack trace.
        Assert.Equal(string.Empty, LegacySp.DesdeElDia(null));
        Assert.Equal(string.Empty, LegacySp.HastaElDia(null));
        Assert.Equal(string.Empty, LegacySp.ElDia(null));
    }

    [Fact]
    public void FechaODbNull_Manda_Null_Cuando_No_Hay_Fecha()
    {
        Assert.Equal(DBNull.Value, LegacySp.FechaODbNull(null));
        Assert.Equal(new DateTime(2026, 3, 7, 0, 0, 0), LegacySp.FechaODbNull(new DateOnly(2026, 3, 7)));
    }

    [Fact]
    public void Las_Fechas_No_Dependen_De_La_Cultura_Del_Servidor()
    {
        var antes = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("es-MX");
            Assert.Equal("2026-12-01T00:00:00", LegacySp.DesdeElDia(new DateOnly(2026, 12, 1)));
        }
        finally
        {
            CultureInfo.CurrentCulture = antes;
        }
    }

    // ------------------------------------------------------------------
    // Las listas pegadas con "~"
    // ------------------------------------------------------------------

    [Fact]
    public void Las_Listas_Se_Pegan_Con_Tilde()
    {
        Assert.Equal("1~2~3", LegacySp.Lista(new[] { 1, 2, 3 }));
    }

    [Fact]
    public void Lista_Vacia_O_Nula_Da_Cadena_Vacia()
    {
        Assert.Equal(string.Empty, LegacySp.Lista(Array.Empty<int>()));
        Assert.Equal(string.Empty, LegacySp.Lista((IEnumerable<int>?)null));
        Assert.Equal(string.Empty, LegacySp.Lista((IEnumerable<decimal>?)null));
    }

    [Fact]
    public void Los_Decimales_Se_Escriben_Con_Punto_Aunque_El_Servidor_Sea_Espanol()
    {
        // Con la cultura en espanol, 12.5 se habria escrito "12,5" y el SP, que
        // parte por "~", habria leido un numero distinto.
        var antes = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("es-MX");
            Assert.Equal("12.5~0.25", LegacySp.Lista(new[] { 12.5m, 0.25m }));
        }
        finally
        {
            CultureInfo.CurrentCulture = antes;
        }
    }

    // ------------------------------------------------------------------
    // El renglon (result, mensaje) con el que contestan estos SP
    // ------------------------------------------------------------------

    [Fact]
    public void LeerResultado_Lee_El_Id_Nuevo_Y_El_Mensaje()
    {
        var dt = new DataTable();
        dt.Columns.Add("result", typeof(int));
        dt.Columns.Add("mensaje", typeof(string));
        dt.Rows.Add(42, "  ");

        var r = LegacySp.LeerResultado(dt);
        Assert.Equal(42, r.Result);
        Assert.Equal(string.Empty, r.Mensaje);
    }

    [Fact]
    public void LeerResultado_Lee_El_Menos_Uno_Del_Error()
    {
        var dt = new DataTable();
        dt.Columns.Add("result", typeof(int));
        dt.Columns.Add("mensaje", typeof(string));
        dt.Rows.Add(-1, "ERROR");

        var r = LegacySp.LeerResultado(dt);
        Assert.Equal(-1, r.Result);
        Assert.Equal("ERROR", r.Mensaje);
    }

    [Fact]
    public void LeerResultado_Sin_Renglon_No_Revienta()
    {
        // Hay procedimientos de esta familia que no emiten el renglon cuando salen
        // por un RETURN temprano; un 500 ahi tapa la unica pista que queda.
        Assert.Equal(0, LegacySp.LeerResultado(new DataTable()).Result);
        Assert.Equal(0, LegacySp.LeerResultado(null).Result);
    }

    // ------------------------------------------------------------------
    // Las columnas que no salen al cliente
    // ------------------------------------------------------------------

    [Fact]
    public void Renglones_Quita_Las_Columnas_Ftm_Y_El_Hueco_De_Error()
    {
        var dt = new DataTable();
        dt.Columns.Add("IDContrato", typeof(int));
        dt.Columns.Add("FechaInicio", typeof(DateTime));
        dt.Columns.Add("FechaInicioFtm", typeof(string));
        dt.Columns.Add("result", typeof(int));
        dt.Columns.Add("mensaje", typeof(string));
        dt.Rows.Add(7, new DateTime(2026, 3, 7), "07-03-2026", 0, "");

        var filas = LegacySp.Renglones(dt);

        Assert.Single(filas);
        Assert.True(filas[0].ContainsKey("idContrato"));
        Assert.True(filas[0].ContainsKey("fechaInicio"));
        Assert.False(filas[0].ContainsKey("fechaInicioFtm"));
        Assert.False(filas[0].ContainsKey("result"));
        Assert.False(filas[0].ContainsKey("mensaje"));
    }

    [Fact]
    public void Una_Columna_Que_Empieza_Con_Dos_Mayusculas_No_Sale_Con_Un_Numero_Delante()
    {
        /*
          ESTA PRUEBA EXISTE POR UN DEFECTO REAL QUE SE ENCONTRO AQUI.

          Funciones.ACamelCase hacia
              char.ToLowerInvariant(n[0]) + char.ToLowerInvariant(n[1]) + n.Substring(2)
          y en C# `char + char` SUMA los codigos en vez de concatenar: 'i' + 'd'
          da 205, asi que "IDContrato" salia al cliente como "205Contrato". No se
          habia notado porque los SP que usaban DataTableToRows hasta hoy
          devuelven las columnas ya en camelCase; los de obra y aviacion las
          devuelven en Pascal.
        */
        var dt = new DataTable();
        dt.Columns.Add("IDContrato", typeof(int));
        dt.Columns.Add("IDPoliza", typeof(int));
        dt.Columns.Add("Contrato", typeof(string));
        dt.Columns.Add("idStatus", typeof(int));
        dt.Rows.Add(1, 2, "OP-1", 1);

        var fila = LegacySp.Renglones(dt)[0];

        Assert.True(fila.ContainsKey("idContrato"));
        Assert.True(fila.ContainsKey("idPoliza"));
        Assert.True(fila.ContainsKey("contrato"));
        // Lo que ya venia en camelCase se deja igual.
        Assert.True(fila.ContainsKey("idStatus"));
        Assert.DoesNotContain(fila.Keys, k => k.Length > 0 && char.IsDigit(k[0]));
    }

    [Fact]
    public void La_Columna_Que_El_Sp_Manda_Dos_Veces_No_Sale_Duplicada()
    {
        /*
          sp_n_ConsultarGasolinas selecciona G.Equipo DOS VECES (y lo mismo con
          FechaModificacion y FechaCancelacion). El DataTable renombra la segunda a
          "Equipo1" y saldria al cliente un `equipo1` que no significa nada.
        */
        var dt = new DataTable();
        dt.Columns.Add("Equipo", typeof(string));
        dt.Columns.Add("Equipo1", typeof(string));   // lo que hace el DataTable con el duplicado
        dt.Rows.Add("MAQ-PAMELA", "MAQ-PAMELA");

        var fila = LegacySp.Renglones(dt)[0];

        Assert.True(fila.ContainsKey("equipo"));
        Assert.False(fila.ContainsKey("equipo1"));
    }

    [Fact]
    public void Una_Columna_Que_Acaba_En_Numero_Y_No_Es_Copia_Se_Queda()
    {
        // La regla no puede tragarse una columna que legitimamente se llame asi.
        var dt = new DataTable();
        dt.Columns.Add("Telefono", typeof(string));
        dt.Columns.Add("Telefono2", typeof(string));
        dt.Rows.Add("555-1111", "555-2222");

        var fila = LegacySp.Renglones(dt)[0];

        Assert.True(fila.ContainsKey("telefono"));
        Assert.True(fila.ContainsKey("telefono2"));
    }
}
