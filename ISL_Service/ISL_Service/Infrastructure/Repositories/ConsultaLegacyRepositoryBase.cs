using System.Data;
using ISL_Service.Application.DTOs.ConsultasLegacy;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// La plomeria que comparten las consultas migradas de MacServicios2.
///
/// POR QUE UNA CLASE BASE Y NO UNA COPIA POR MODULO
/// ------------------------------------------------
/// Los tres repositorios de consultas de esta ola (cobranza, usados y
/// descuentos) hacen literalmente lo mismo: abrir la conexion con el mismo orden
/// de cadenas, ejecutar un `sp_n_Consultar*`, devolver la primera tabla como
/// renglones y normalizar los filtros al dialecto de legacy. Copiarlo tres veces
/// significa que el dia que haya que arreglar el formato de fecha hay que
/// acordarse de tres lugares; es el mismo argumento que ya justifico
/// <see cref="Controllers.PermisoControllerBase"/>. Y quedan por migrar las
/// cabeceras de esas mismas pantallas, que seran mas herederos de esta base.
///
/// Los repositorios que ya existian (VentasConsultaRepository y compania) NO se
/// migran a esta base: funcionan, estan probados contra legacy y cambiarlos no
/// es parte de esta entrega.
/// </summary>
public abstract class ConsultaLegacyRepositoryBase
{
    /*
      MEDIO MINUTO LARGO DE PACIENCIA.

      Es el mismo 500 que usa VentasConsultaRepository. No es generosidad: estos
      procedimientos barren rangos de fechas sobre tablas de millones de
      renglones y algunos (`sp_n_rptVentasFolios`, `sp_n_ConsultarCobros` sin
      filtros) tardan minutos en la base de produccion. Con el default de 30 s la
      pantalla devolvia un error de red y el usuario volvia a pulsar Buscar, que
      es justo lo peor que puede pasarle a una consulta lenta.
    */
    protected const int CommandTimeoutSeconds = 500;

    private readonly IConfiguration _configuration;

    protected ConsultaLegacyRepositoryBase(IConfiguration configuration)
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
    /// Corre un stored procedure y devuelve su PRIMERA tabla como renglones.
    /// </summary>
    /// <remarks>
    /// Solo la primera, igual que Legacy: todos sus repositorios de consulta
    /// hacen `ds.Tables[0]` y descartan el resto. Si algun procedimiento
    /// devuelve mas de una tabla y hace falta, se usa
    /// <see cref="EjecutarTablasAsync"/>.
    /// </remarks>
    protected async Task<ConsultaFilasResponse> EjecutarFilasAsync(
        string storedProcedure,
        Action<SqlParameterCollection> parametros,
        CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        return await EjecutarFilasAsync(conn, storedProcedure, parametros, ct);
    }

    protected static async Task<ConsultaFilasResponse> EjecutarFilasAsync(
        SqlConnection conn,
        string storedProcedure,
        Action<SqlParameterCollection> parametros,
        CancellationToken ct)
    {
        var tabla = await EjecutarPrimeraTablaAsync(conn, storedProcedure, parametros, ct);
        var filas = TablaAFilas(tabla);
        return new ConsultaFilasResponse { Rows = filas, Total = filas.Count };
    }

    /// <summary>Todas las tablas del procedimiento, en orden.</summary>
    protected async Task<List<ConsultaFilasResponse>> EjecutarTablasAsync(
        string storedProcedure,
        Action<SqlParameterCollection> parametros,
        CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(storedProcedure, conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };
        parametros(cmd.Parameters);

        // Asincrono de verdad: `adapter.Fill` bloquea un hilo del ThreadPool
        // durante toda la consulta. Ver LlenadoAsincrono.
        var conjunto = await LlenadoAsincrono.LlenarAsync(cmd, ct);

        var salida = new List<ConsultaFilasResponse>();
        foreach (DataTable tabla in conjunto.Tables)
        {
            var filas = TablaAFilas(tabla);
            salida.Add(new ConsultaFilasResponse { Rows = filas, Total = filas.Count });
        }
        return salida;
    }

    private static async Task<DataTable> EjecutarPrimeraTablaAsync(
        SqlConnection conn,
        string storedProcedure,
        Action<SqlParameterCollection> parametros,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand(storedProcedure, conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };
        parametros(cmd.Parameters);

        var conjunto = await LlenadoAsincrono.LlenarAsync(cmd, ct);
        return conjunto.Tables.Count > 0 ? conjunto.Tables[0] : new DataTable();
    }

    private static List<Dictionary<string, object?>> TablaAFilas(DataTable tabla)
    {
        var filas = new List<Dictionary<string, object?>>();
        foreach (DataRow fila in tabla.Rows)
        {
            var item = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (DataColumn col in tabla.Columns)
            {
                var valor = fila[col];
                /*
                  EL RELLENO DE LOS char(n) NO ES UN DATO.

                  Varias columnas de legacy son char fijo y SQL Server devuelve
                  el relleno dentro ("AGUASCALIENTES      "). En Mac31 no se veia
                  porque el control de WinForms lo recortaba al pintar. Se quita
                  aqui, igual que se hizo en la ola de catalogos.
                */
                item[col.ColumnName] = valor == DBNull.Value
                    ? null
                    : valor is string texto ? texto.TrimEnd() : valor;
            }
            filas.Add(item);
        }
        return filas;
    }

    /*
      LOS FILTROS VIVEN EN FiltrosLegacy, NO AQUI

      Se sacaron a `Infrastructure/Data/FiltrosLegacy.cs` para poder PROBARLOS:
      son funciones puras y son el sitio exacto donde una migracion descuidada
      pierde datos sin que salga un error (una fecha mal volteada no truena,
      devuelve otros renglones). Estos atajos quedan para que los repositorios se
      lean cortos.
    */
    protected static string Fecha(string? valor, bool finDelDia) => FiltrosLegacy.Fecha(valor, finDelDia);

    protected static string Folio(string? valor) => FiltrosLegacy.Folio(valor);

    protected static string Ids(IEnumerable<int>? valores, string? crudo, string separador = "~")
        => FiltrosLegacy.Ids(valores, crudo, separador);

    protected static string Texto(string? valor) => FiltrosLegacy.Texto(valor);

    protected static string TextoSinApostrofo(string? valor) => FiltrosLegacy.TextoSinApostrofo(valor);
}
