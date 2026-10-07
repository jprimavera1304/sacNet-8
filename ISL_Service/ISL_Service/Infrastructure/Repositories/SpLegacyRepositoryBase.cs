using System.Data;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/*
  EJECUTAR UN sp_n_ Y LEER LO QUE DEVUELVA, SIN INVENTAR NADA.

  Esta ola migra un centenar de capacidades de ventas que ESCRIBEN, y todas
  tienen la misma forma: armar los parametros, llamar al procedimiento de
  legacy tal cual, y devolver lo que contesto. Lo unico que cambia entre ellas
  es la lista de parametros.

  POR QUE UNA BASE Y NO COPIAR LA CONEXION EN CADA REPOSITORIO
  ------------------------------------------------------------
  El patron de GetConnection() (Main -> Mac3 -> Local -> Default) esta copiado
  a mano en cuarenta y cinco repositorios. Copiarlo doce veces mas para esta
  ola significa que el dia que cambie el orden hay que acordarse de cincuenta y
  siete lugares. Los repositorios que ya existen se quedan como estan: tocarlos
  no es parte de esta entrega.

  POR QUE SE DEVUELVEN RENGLONES CRUDOS Y NO UN DTO TIPADO
  --------------------------------------------------------
  Legacy mete cada tabla en un DTO con DataTableToList, que empareja por nombre
  EXACTO. Eso tiene dos efectos que no se quieren heredar: lo que el
  procedimiento devuelve y el DTO no declara se TIRA (asi se perdieron
  SaldoFavor y SaldoFavorCascos, escritos con f minuscula en el DTO), y lo que
  el DTO declara y el procedimiento no devuelve sale como 0 o 0001-01-01 de
  relleno (un 0 en TotalPagar se lee como "no se pago nada", que no es lo mismo
  que "este renglon no tiene pago"). Aqui baja el renglon del procedimiento tal
  como salio, y donde el procedimiento dice NULL el JSON dice null.

  LOS RAISERROR NO SE ATRAPAN AQUI
  --------------------------------
  Los sp_n_ dicen que algo no se puede con RAISERROR de severidad 16, y el
  middleware de errores ya traduce esos SqlException (numeros 50000-99999) a un
  400 con su texto literal. De eso dependen pantallas enteras: el mensaje que
  ve el usuario es el que escribio quien escribio el procedimiento, no uno
  nuestro. Atraparlo aqui para "mejorarlo" seria perderlo.
*/
public abstract class SpLegacyRepositoryBase
{
    /// <summary>
    /// 500 segundos, el mismo que usan los repositorios de ventas que ya
    /// existen. Varios de estos procedimientos recorren el historico completo
    /// de una remision y el timeout de 30 segundos por defecto los mata.
    /// </summary>
    protected const int CommandTimeoutSeconds = 500;

    private readonly IConfiguration _configuration;

    protected SpLegacyRepositoryBase(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    protected SqlConnection GetConnection()
    {
        var cs = _configuration.GetConnectionString("Main")
            ?? _configuration.GetConnectionString("Mac3")
            ?? _configuration.GetConnectionString("Local")
            ?? _configuration.GetConnectionString("Default");

        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("ConnectionString (Main/Mac3/Local/Default) no encontrada.");

        var connector = new Mac3SqlServerConnector(cs);
        return connector.GetConnection;
    }

    /// <summary>
    /// Corre el procedimiento y devuelve TODOS sus conjuntos de resultados como
    /// listas de diccionarios columna -> valor.
    ///
    /// Varios de estos procedimientos devuelven mas de una tabla (la cabecera y
    /// el detalle, o el resultado y el renglon que se acaba de crear) y legacy
    /// publicaba solo la primera o solo algunas. Aqui bajan todas y cada
    /// endpoint decide: tirar una tabla en el repositorio es una decision que
    /// no se puede deshacer desde arriba.
    /// </summary>
    protected async Task<List<List<Dictionary<string, object?>>>> EjecutarTablasAsync(
        string procedimiento,
        Action<SqlCommand> parametros,
        CancellationToken ct)
    {
        var tablas = new List<List<Dictionary<string, object?>>>();

        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(procedimiento, conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };
        parametros(cmd);

        await using var rd = await cmd.ExecuteReaderAsync(ct);
        do
        {
            // Un procedimiento que solo ejecuta DML devuelve un conjunto sin
            // campos. No es una tabla vacia: es "no hubo tabla", y meterla
            // haria creer a quien lee que el procedimiento devolvio algo.
            if (rd.FieldCount == 0) continue;

            var filas = new List<Dictionary<string, object?>>();
            var nombres = new string[rd.FieldCount];
            for (var i = 0; i < rd.FieldCount; i++) nombres[i] = rd.GetName(i);

            while (await rd.ReadAsync(ct))
            {
                var fila = new Dictionary<string, object?>(rd.FieldCount, StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < rd.FieldCount; i++)
                {
                    var nombre = string.IsNullOrEmpty(nombres[i]) ? $"columna{i + 1}" : nombres[i];
                    fila[nombre] = Valor(rd, i);
                }
                filas.Add(fila);
            }
            tablas.Add(filas);
        }
        while (await rd.NextResultAsync(ct));

        return tablas;
    }

    /// <summary>
    /// La primera tabla del procedimiento, o una lista vacia si no devolvio
    /// ninguna. Es el caso de la inmensa mayoria.
    /// </summary>
    protected async Task<List<Dictionary<string, object?>>> EjecutarFilasAsync(
        string procedimiento,
        Action<SqlCommand> parametros,
        CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(procedimiento, parametros, ct);
        return tablas.Count > 0 ? tablas[0] : new List<Dictionary<string, object?>>();
    }

    /// <summary>
    /// El primer renglon de la primera tabla, o null. Es la forma de los
    /// procedimientos de escritura, que contestan un renglon con `result` y
    /// `mensaje`.
    /// </summary>
    protected async Task<Dictionary<string, object?>?> EjecutarFilaAsync(
        string procedimiento,
        Action<SqlCommand> parametros,
        CancellationToken ct)
    {
        var filas = await EjecutarFilasAsync(procedimiento, parametros, ct);
        return filas.Count > 0 ? filas[0] : null;
    }

    /// <summary>
    /// Corre un procedimiento que no devuelve tabla y entrega los renglones
    /// afectados que reporte ADO.NET.
    /// </summary>
    protected async Task<int> EjecutarAsync(
        string procedimiento,
        Action<SqlCommand> parametros,
        CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(procedimiento, conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };
        parametros(cmd);

        return await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// El valor de una celda: DBNull se vuelve null de verdad, y los char(n) de
    /// ancho fijo se recortan.
    ///
    /// Lo del recorte no es cosmetico. Varias columnas de legacy son char(50) y
    /// SQL Server devuelve el relleno de espacios dentro del valor. En Mac31 no
    /// se veia porque la rejilla de WinForms lo recortaba al pintar; en un JSON
    /// si se ve, y un "EFECTIVO                " no es igual a "EFECTIVO" para
    /// quien compare dos respuestas.
    /// </summary>
    private static object? Valor(SqlDataReader rd, int indice)
    {
        if (rd.IsDBNull(indice)) return null;
        var valor = rd.GetValue(indice);
        return valor is string texto ? texto.TrimEnd() : valor;
    }

    /// <summary>Lee un entero de un renglon crudo, tolerando el tipo.</summary>
    protected static int Entero(Dictionary<string, object?>? fila, string columna, int siFalta = 0)
    {
        if (fila is null || !fila.TryGetValue(columna, out var valor) || valor is null) return siFalta;
        return valor switch
        {
            int i => i,
            long l => (int)l,
            short s => s,
            byte b => b,
            decimal d => (int)d,
            double db => (int)db,
            string t => int.TryParse(t, out var n) ? n : siFalta,
            _ => siFalta
        };
    }

    /// <summary>Lee un texto de un renglon crudo.</summary>
    protected static string Texto(Dictionary<string, object?>? fila, string columna)
    {
        if (fila is null || !fila.TryGetValue(columna, out var valor) || valor is null) return string.Empty;
        return valor as string ?? Convert.ToString(valor, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

    /// <summary>
    /// El parametro varchar de un sp_n_, con su largo real.
    ///
    /// El largo importa: sin el, SqlClient lo deduce del valor y el plan de
    /// ejecucion cambia de tamano con cada llamada, lo que llena la cache de
    /// planes de una variante por largo de cadena.
    /// </summary>
    protected static SqlParameter Varchar(string nombre, int largo, string? valor)
        => new(nombre, SqlDbType.VarChar, largo) { Value = valor ?? string.Empty };

    protected static SqlParameter Entero(string nombre, int valor)
        => new(nombre, SqlDbType.Int) { Value = valor };

    protected static SqlParameter Decimal18(string nombre, decimal valor, byte escala = 2)
        => new(nombre, SqlDbType.Decimal) { Precision = 18, Scale = escala, Value = valor };

    /// <summary>
    /// Un datetime de verdad (los pocos sp_n_ que no reciben la fecha como
    /// cadena). Null baja como DBNull y no como 0001-01-01: un
    /// 0001-01-01 guardado en una columna de fecha es un dato falso que luego
    /// nadie sabe de donde salio.
    /// </summary>
    protected static SqlParameter Fecha(string nombre, DateTime? valor)
        => new(nombre, SqlDbType.DateTime) { Value = valor.HasValue ? valor.Value : DBNull.Value };
}
