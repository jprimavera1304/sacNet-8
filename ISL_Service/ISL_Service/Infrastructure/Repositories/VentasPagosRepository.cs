using System.Data;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/*
  PAGOS DE UNA REMISION: LOS MISMOS PROCEDIMIENTOS QUE MAC31

  Ni uno nuevo, y ninguno tocado. Son exactamente los dos que llama Mac31 por
  debajo de ConsultarVentasPagos.cs:

    sp_n_ConsultaVentasPagos   lee la pantalla completa (cabecera, renglones y
                               totales) — Variables.cs, linea 489,
                               "api/consultaventaspagos/consultar"
    sp_n_CancelarVentasPagos   cancela un pago — Variables.cs, linea 520,
                               "api/ventaspagos/cancelar". Ojo con el nombre:
                               en VentasPagosInputDTO el campo se llama
                               UrlActualizar y apunta al de CANCELAR; el propio
                               legacy lo comenta ("No se por que le puse el
                               cancelar en actualizar, pero asi esta
                               funcionando"). Aqui se llama por su nombre real.

  No hay ni una consulta escrita a mano contra las tablas de pagos. El saldo de
  una remision es de los numeros mas delicados del sistema: si lo calculara
  este archivo, el dia que legacy cambie una regla el web seguiria diciendo el
  numero viejo, sin error y sin aviso.
*/
public class VentasPagosRepository : IVentasPagosRepository
{
    private const int CommandTimeoutSeconds = 500;

    private readonly IConfiguration _configuration;

    public VentasPagosRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<string> EsquemaPagoAsync(CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(
            "SELECT TOP 1 UPPER(ISNULL(EsquemaPago,'')) FROM Constantes;", conn)
        { CommandTimeout = CommandTimeoutSeconds };

        var valor = await cmd.ExecuteScalarAsync(ct);
        return valor as string ?? string.Empty;
    }

    public async Task<List<VentasPagoCrudo>> ConsultarAsync(int idVenta, CancellationToken ct)
    {
        var lista = new List<VentasPagoCrudo>();
        if (idVenta <= 0) return lista;

        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand("sp_n_ConsultaVentasPagos", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };

        /*
          LOS CINCO PARAMETROS, IGUAL QUE MAC31.

          Mac31 manda el DTO completo pero solo llena IDVenta
          (ConsultarVentasPagos.cs, lineas 60-61); los otros cuatro se quedan en
          cero por el constructor de ConsultaVentasInputDTO. Se mandan explicitos
          porque los valores por omision del procedimiento NO son cero —trae un
          IDVenta de pruebas escrito a mano— y confiar en ellos seria leer la
          remision de otro.

          @DiferenciaUsados = 0 es lo que hace que salgan los pagos de [Ventas
          Pagos] y NO los de [Ventas Pagos Usados]: el procedimiento filtra por
          EsquemaPago = '' cuando vale 0 y por 'ZARA' cuando vale 1
          (sp_n_ConsultaVentasPagos, linea 503). Esa otra mitad es la pantalla
          ConsultarVentasPagosUsados, que es otra cosa y tiene sus propios
          botones dados de alta en n_Procesos (IDForma 4029).
        */
        cmd.Parameters.Add(new SqlParameter("@IDVenta", SqlDbType.Int) { Value = idVenta });
        cmd.Parameters.Add(new SqlParameter("@IDPagoVenta", SqlDbType.Int) { Value = 0 });
        cmd.Parameters.Add(new SqlParameter("@DiferenciaUsados", SqlDbType.Int) { Value = 0 });
        cmd.Parameters.Add(new SqlParameter("@IDCobro", SqlDbType.Int) { Value = 0 });
        cmd.Parameters.Add(new SqlParameter("@DineroExcedente", SqlDbType.Int) { Value = 0 });

        await using var rd = await cmd.ExecuteReaderAsync(ct);

        /*
          Las columnas se buscan por NOMBRE y tolerando que falten.

          No es pereza: sp_n_ConsultaVentasPagos devuelve mas de cien columnas y
          las cuatro PagaCon* que Mac31 lee (lineas 93-96) NO estan entre ellas
          —se comprobo en Produccion_svr y en MacZ—. En Mac31 eso no truena
          porque el DataTable se arma desde la clase de C#, no desde el
          resultado. Aqui, pedirlas por ordinal seria una excepcion en cada
          consulta.
        */
        var col = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < rd.FieldCount; i++)
            col.TryAdd(rd.GetName(i), i);

        while (await rd.ReadAsync(ct))
        {
            lista.Add(new VentasPagoCrudo
            {
                IdPagoVenta = Entero(rd, col, "IDPagoVenta"),
                IdVenta = Entero(rd, col, "IDVenta"),
                IdTipoPago = Entero(rd, col, "IDTipoPago"),

                FolioFtm = Texto(rd, col, "FolioFtm"),
                NumeroCliente = Entero(rd, col, "NumeroCliente"),
                NombreCliente = Texto(rd, col, "NombreCliente"),
                Empresa = Texto(rd, col, "Empresa"),
                NumeroNombreAgente = Texto(rd, col, "NumeroNombreAgente"),
                FechaVencimientoFtm = Texto(rd, col, "FechaVencimientoFtm"),
                FechaCancelacionFtm = Texto(rd, col, "FechaCancelacionFtm"),

                FechaPagoFtm = Texto(rd, col, "FechaPagoFtm"),
                Usuario = Texto(rd, col, "Usuario"),
                Equipo = Texto(rd, col, "Equipo"),
                NumeroPago = Texto(rd, col, "NumeroPago"),
                Movimiento = Texto(rd, col, "Movimiento"),
                TipoPagoCorto = Texto(rd, col, "TipoPagoCorto"),

                Cargo = Dinero(rd, col, "Cargo"),
                Abono = Dinero(rd, col, "Abono"),
                SaldoAcumulado = Dinero(rd, col, "SaldoAcumulado"),

                DiffUsadosTexto = Texto(rd, col, "DiffUsadosTexto"),
                AgenteLiquidacion = Texto(rd, col, "AgenteLiquidacion"),
                RepartidorLiquidacion = Texto(rd, col, "RepartidorLiquidacion"),
                DetallePago = Texto(rd, col, "DetallePago"),

                Cancelado = Texto(rd, col, "Cancelado"),
                FechaPagoCancelacionFtm = Texto(rd, col, "FechaPagoCancelacionFtm"),

                TotalPagar = Dinero(rd, col, "TotalPagar"),
                Cargos = Dinero(rd, col, "Cargos"),
                Abonos = Dinero(rd, col, "Abonos"),
                Descuentos = Dinero(rd, col, "Descuentos"),
                Saldo = Dinero(rd, col, "Saldo"),

                PagaConEfectivo = Entero(rd, col, "PagaConEfectivo"),
                PagaConCheque = Entero(rd, col, "PagaConCheque"),
                PagaConTransferencia = Entero(rd, col, "PagaConTransferencia"),
                PagaConDepositoEfectivo = Entero(rd, col, "PagaConDepositoEfectivo")
            });
        }

        return lista;
    }

    public async Task<VentasPagoCancelacionCrudo> CancelarAsync(
        int idPagoVenta, int idVenta, int idUsuario, string equipo, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand("sp_n_CancelarVentasPagos", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };

        /*
          @IDPagoVentaUsado = 0 SIEMPRE desde esta pantalla.

          El mismo procedimiento sirve a dos pantallas: con @IDPagoVenta cancela
          un pago de [Ventas Pagos] (esta) y con @IDPagoVentaUsado uno de
          [Ventas Pagos Usados] (la de cascos). Mandar los dos, o el que no es,
          cancelaria un movimiento de la otra pantalla.
        */
        cmd.Parameters.Add(new SqlParameter("@IDPagoVenta", SqlDbType.Int) { Value = idPagoVenta });
        cmd.Parameters.Add(new SqlParameter("@IDPagoVentaUsado", SqlDbType.Int) { Value = 0 });
        cmd.Parameters.Add(new SqlParameter("@IDVenta", SqlDbType.Int) { Value = idVenta });
        cmd.Parameters.Add(new SqlParameter("@IDUsuario", SqlDbType.Int) { Value = idUsuario });
        cmd.Parameters.Add(new SqlParameter("@Equipo", SqlDbType.VarChar, 1000) { Value = equipo ?? string.Empty });

        await using var rd = await cmd.ExecuteReaderAsync(ct);

        /*
          El procedimiento devuelve un solo renglon con result y mensaje, pero
          las tres salidas buenas traen ademas SaldoFavor y SaldoFavorCascos y
          una de las malas no trae mensaje formateado igual. Se leen por nombre
          y con tolerancia, que es lo unico estable entre las cuatro salidas.
        */
        if (!await rd.ReadAsync(ct))
            return new VentasPagoCancelacionCrudo { Result = -1, Mensaje = "NO SE PUDO REALIZAR LA OPERACIÓN." };

        var col = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < rd.FieldCount; i++)
            col.TryAdd(rd.GetName(i), i);

        return new VentasPagoCancelacionCrudo
        {
            Result = Entero(rd, col, "result"),
            Mensaje = Texto(rd, col, "mensaje")
        };
    }

    /* ---------------------------------------------------------------- */
    /* Lectura tolerante                                                 */
    /* ---------------------------------------------------------------- */

    private static string Texto(SqlDataReader rd, IReadOnlyDictionary<string, int> col, string nombre)
    {
        if (!col.TryGetValue(nombre, out var i) || rd.IsDBNull(i)) return string.Empty;
        return Convert.ToString(rd.GetValue(i))?.Trim() ?? string.Empty;
    }

    private static int Entero(SqlDataReader rd, IReadOnlyDictionary<string, int> col, string nombre)
    {
        if (!col.TryGetValue(nombre, out var i) || rd.IsDBNull(i)) return 0;
        try { return Convert.ToInt32(rd.GetValue(i)); } catch { return 0; }
    }

    private static decimal Dinero(SqlDataReader rd, IReadOnlyDictionary<string, int> col, string nombre)
    {
        if (!col.TryGetValue(nombre, out var i) || rd.IsDBNull(i)) return 0m;
        try { return Convert.ToDecimal(rd.GetValue(i)); } catch { return 0m; }
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
