using System.Data;
using System.Globalization;
using ISL_Service.Application.DTOs.VentasSaldos;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

public class VentasSaldosRepository : IVentasSaldosRepository
{
    private const int CommandTimeoutSeconds = 120;

    private readonly IConfiguration _configuration;

    public VentasSaldosRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<VentasSaldosResponse> ConsultarSaldosClienteAsync(int idCliente, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var fechaOperacion = await ConsultarFechaOperacionAsync(conn, ct);

        await using var cmd = new SqlCommand("sp_n_rptVentasSaldos", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };

        /*
          LOS MISMOS PARAMETROS QUE MANDA MAC31

          Ver ConsultarVentas.cs, metodo VentasSaldosCliente:

            @FechaFinal = la fecha de OPERACION, no la del reloj ni la del filtro
                          de arriba. Legacy cierra el dia cuando quiere y los
                          dias vencidos se cuentan contra ese corte; usar la
                          fecha de hoy daria un vencimiento distinto al que ve
                          Mac31, y esa cifra es la que decide si se le vende al
                          cliente o no.
            @Status = 4   PendientesVencidas. Lo pagado no interesa aqui.

          Lo demas en cero A PROPOSITO: este panel es del CLIENTE, no del periodo
          ni del agente que se esten consultando arriba. Filtrarlo por el periodo
          esconderia justo lo que se viene a ver, que es lo que debe de hace
          meses.

          La fecha va como MM-dd-yyyy: el procedimiento la recibe en varchar y la
          convierte con la configuracion del servidor. Con dd/MM/yyyy devuelve
          vacio sin avisar — probado contra esta base.
        */
        cmd.Parameters.AddWithValue("@IDsEmpresa", string.Empty);
        cmd.Parameters.AddWithValue("@IDCliente", idCliente);
        cmd.Parameters.AddWithValue("@IDAgente", 0);
        cmd.Parameters.AddWithValue("@FechaInicial", string.Empty);
        cmd.Parameters.AddWithValue("@FechaFinal", fechaOperacion.ToString("MM-dd-yyyy", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@DiasVencimiento", 0);
        cmd.Parameters.AddWithValue("@Status", 4);
        cmd.Parameters.AddWithValue("@Formato", 0);

        var respuesta = new VentasSaldosResponse();

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var renglon = new VentasSaldoRenglon
            {
                Numero = Texto(reader, "Numero"),
                Nombre = Texto(reader, "Nombre"),
                Total = Numero(reader, "Total"),
                Cargos = Numero(reader, "Cargos"),
                Abonos = Numero(reader, "Abonos"),
                Descuentos = Numero(reader, "Descuentos"),
                Saldo = Numero(reader, "Saldo"),
                EstatusMostrar = Texto(reader, "EstatusMostrar"),
                DiasVencimiento = Entero(reader, "DiasVencimientoMostrar")
            };

            respuesta.Renglones.Add(renglon);
            respuesta.TotalSaldo += renglon.Saldo;
            if (renglon.DiasVencimiento > respuesta.MaximoVencimiento)
                respuesta.MaximoVencimiento = renglon.DiasVencimiento;
        }

        return respuesta;
    }

    /*
      La fecha de operacion vive en Constantes y es la que todo legacy usa como
      "hoy". Se lee aparte en vez de mandar DateTime.Now para no inventar un
      corte distinto al de Mac31.
    */
    private static async Task<DateTime> ConsultarFechaOperacionAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("SELECT TOP 1 FechaOperacion FROM Constantes", conn)
        {
            CommandTimeout = CommandTimeoutSeconds
        };

        var valor = await cmd.ExecuteScalarAsync(ct);
        if (valor is DateTime fecha && fecha.Year > 1900) return fecha;

        /* Sin constante utilizable, hoy: es peor no consultar que consultar con
           el corte del reloj. */
        return DateTime.Today;
    }

    /*
      Los ayudantes leen POR NOMBRE y aguantan que la columna no venga. El
      procedimiento devuelve veinticinco columnas y aqui se usan nueve; si un dia
      legacy quita una de las que no miramos, esto sigue en pie.
    */
    private static string Texto(SqlDataReader reader, string columna)
    {
        var i = Ordinal(reader, columna);
        if (i < 0 || reader.IsDBNull(i)) return string.Empty;
        return Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
    }

    private static decimal Numero(SqlDataReader reader, string columna)
    {
        var i = Ordinal(reader, columna);
        if (i < 0 || reader.IsDBNull(i)) return 0m;
        return Convert.ToDecimal(reader.GetValue(i), CultureInfo.InvariantCulture);
    }

    private static int Entero(SqlDataReader reader, string columna)
    {
        var i = Ordinal(reader, columna);
        if (i < 0 || reader.IsDBNull(i)) return 0;
        return Convert.ToInt32(reader.GetValue(i), CultureInfo.InvariantCulture);
    }

    private static int Ordinal(SqlDataReader reader, string columna)
    {
        try
        {
            return reader.GetOrdinal(columna);
        }
        catch (IndexOutOfRangeException)
        {
            return -1;
        }
    }

    private SqlConnection GetConnection()
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
}
