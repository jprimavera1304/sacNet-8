using System.Data;
using ISL_Service.Application.DTOs.VentasUsados;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/*
  MODIFICACION DE USADOS: LOS MISMOS PROCEDIMIENTOS QUE MAC31, NI UNO NUEVO

  Los cinco que se llaman aqui son los que llama Mac31 por debajo
  (Legacy/MacServicios2/.../VentasUsadosCargoRepository.cs:29 y
  VentasUsadosCreditoRepository.cs:29-33):

    sp_n_ConsultaVentas               la cabecera y los totales de la venta
    sp_n_VentasUsadosCargo            los cascos que se le COBRAN
    sp_n_VentasUsadosCredito  @2      los ENTREGADOS + el catalogo completo
    sp_n_VentasUsadosCredito  @1      los que se dieron de baja antes
    sp_n_VentasUsadosCreditoAjuste    guarda        (Tauro)
    sp_n_VentasUsadosCreditoAjusteZara guarda       (Zaragoza)

  LAS DOS EMPRESAS
  Las diferencias entre Tauro y Zaragoza NO estan aqui: viven DENTRO de los
  procedimientos, que se ramifican solos con Constantes.Funcionalidad. Se
  compararon los cuatro entre Produccion_svr y MacZ ignorando espacios y las
  lecturas son identicas; la unica bifurcacion que si toca decidir a quien
  llama es cual de los dos procedimientos de AJUSTE corre, y eso lo dice
  Constantes.EsquemaPago ('TAU' / 'ZARA') — exactamente el mismo criterio que
  usa legacy (VentasUsadosCreditoRepository.cs:166 y VentasUsadosCreditoService.cs:108).

  OJO: sp_n_VentasUsadosCredito @Accion=2 no devuelve solo lo que la venta
  tiene. Devuelve tambien, con cantidad CERO, los tipos de usado del catalogo
  que la venta no trae (los INSERT "Agregar los usados que esten en la venta").
  Esos renglones en cero son justo los que permiten AGREGAR un tipo que no
  venia — por eso no se filtran.
*/
public class VentasUsadosRepository : IVentasUsadosRepository
{
    private const int CommandTimeoutSeconds = 500;

    /// El delimitador con el que legacy arma sus cadenas de parametros.
    private const string Delimitador = "~";

    /// @Accion de sp_n_VentasUsadosCredito, igual que enumUsadosCreditoStatus.
    private const int CreditoAnterior = 1;
    private const int CreditoActualConCatalogo = 2;

    private readonly IConfiguration _configuration;

    public VentasUsadosRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /* ------------------------------------------------------------------ */

    public async Task<ConstantesUsados> ConsultarConstantesAsync(CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(
            "SELECT TOP 1 ISNULL(MaximaDiferenciaUsados,0) AS MaximaDiferenciaUsados, " +
            "ISNULL(IVA,0) AS IVA, UPPER(ISNULL(EsquemaPago,'')) AS EsquemaPago FROM Constantes;",
            conn)
        { CommandTimeout = CommandTimeoutSeconds };

        await using var rd = await cmd.ExecuteReaderAsync(ct);
        if (!await rd.ReadAsync(ct)) return new ConstantesUsados();

        return new ConstantesUsados
        {
            MaximaDiferenciaUsados = LeerDecimal(rd, "MaximaDiferenciaUsados"),
            /* En la tabla viene 16.00; los procedimientos siempre usan IVA/100. */
            PorcentajeIva = LeerDecimal(rd, "IVA") / 100m,
            EsquemaPago = LeerTexto(rd, "EsquemaPago")
        };
    }

    public async Task<VentaDeUsados?> ConsultarVentaAsync(int idVenta, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        /*
          Mac31 llama a este mismo procedimiento pasandole SOLO el IDVenta
          (VentasUsados.cs:147-154, ConsultaVentasInputDTO con IDVenta). Los
          demas parametros tienen valor por omision y el filtro por IDVenta gana
          sobre el rango de fechas.
        */
        var tabla = await LlenarAsync(conn, "sp_n_ConsultaVentas",
            new Dictionary<string, object> { ["@IDVenta"] = idVenta }, ct);

        if (tabla.Rows.Count == 0) return null;
        var row = tabla.Rows[0];

        return new VentaDeUsados
        {
            IdVenta = idVenta,
            Folio = Texto(row, "Folio"),
            FolioFtm = Texto(row, "FolioFtm"),
            Empresa = Texto(row, "Empresa"),
            Agente = Texto(row, "NumeroNombreAgente"),
            /* "12 - JUAN PEREZ", igual que VentasUsados.cs:169. */
            Cliente = (Texto(row, "NumeroCliente") + " - " + Texto(row, "NombreCliente")).Trim(),
            FechaPagoFtm = Texto(row, "FechaPagoFtm"),
            Cancelada = Texto(row, "Cancelada"),

            Importe = Numero(row, "Importe"),
            Cargos = Numero(row, "Cargos"),
            Creditos = Numero(row, "Creditos"),
            IvaCreditos = Numero(row, "IvaCreditos"),
            TotalPagar = Numero(row, "TotalPagar"),
            Descuentos = Numero(row, "Descuentos"),
            Abonos = Numero(row, "Abonos"),
            Saldo = Numero(row, "Saldo")
        };
    }

    public async Task<VentasUsadosBloqueDto> ConsultarCargosAsync(int idVenta, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var tabla = await LlenarAsync(conn, "sp_n_VentasUsadosCargo",
            new Dictionary<string, object> { ["@IDVenta"] = idVenta }, ct);

        var bloque = new VentasUsadosBloqueDto();
        if (tabla.Rows.Count == 0) return bloque;

        foreach (DataRow row in tabla.Rows)
        {
            bloque.Renglones.Add(new VentasUsadosRenglonDto
            {
                IdTipoUsado = Entero(row, "IDTipoUsado"),
                Tipo = Texto(row, "TipoUsadoCargo"),
                Costo = Numero(row, "CostoUsadoCargo"),
                CostoConIva = Numero(row, "CostoVentaCargoConIva"),
                Cantidad = Entero(row, "CantidadUsadoCargo"),
                Total = Numero(row, "TotalUsadoCargo")
            });
        }

        /* Los totales vienen repetidos en cada renglon; se toma el primero. */
        var primero = tabla.Rows[0];
        bloque.TotalCantidad = Entero(primero, "TotalCantidadUsadoCargo");
        bloque.TotalImporte = Numero(primero, "TotalImporteUsadoCargo");
        bloque.TotalIva = Numero(primero, "TotalIVAUsadoCargo");
        bloque.TotalTotal = Numero(primero, "TotalTotalUsadoCargo");
        return bloque;
    }

    public async Task<VentasUsadosBloqueDto> ConsultarCreditosAsync(int idVenta, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var tabla = await LlenarAsync(conn, "sp_n_VentasUsadosCredito",
            new Dictionary<string, object>
            {
                ["@Accion"] = CreditoActualConCatalogo,
                ["@IDVenta"] = idVenta
            }, ct);

        var bloque = new VentasUsadosBloqueDto();
        if (tabla.Rows.Count == 0) return bloque;

        foreach (DataRow row in tabla.Rows)
        {
            bloque.Renglones.Add(new VentasUsadosRenglonDto
            {
                IdTipoUsado = Entero(row, "IDTipoUsado"),
                Tipo = Texto(row, "TipoUsadoCredito"),
                Costo = Numero(row, "CostoUsadoCredito"),
                CostoConIva = Numero(row, "CostoVentaCreditoConIva"),
                Cantidad = Entero(row, "CantidadUsadoCredito"),
                Total = Numero(row, "TotalUsadoCredito")
            });
        }

        var primero = tabla.Rows[0];
        bloque.TotalCantidad = Entero(primero, "TotalCantidadUsadoCredito");
        bloque.TotalImporte = Numero(primero, "TotalImporteUsadoCredito");
        bloque.TotalIva = Numero(primero, "TotalIVAUsadoCredito");
        bloque.TotalTotal = Numero(primero, "TotalTotalUsadoCredito");
        return bloque;
    }

    public async Task<List<VentasUsadosAnteriorDto>> ConsultarAnterioresAsync(int idVenta, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var tabla = await LlenarAsync(conn, "sp_n_VentasUsadosCredito",
            new Dictionary<string, object>
            {
                ["@Accion"] = CreditoAnterior,
                ["@IDVenta"] = idVenta
            }, ct);

        var lista = new List<VentasUsadosAnteriorDto>();
        foreach (DataRow row in tabla.Rows)
        {
            lista.Add(new VentasUsadosAnteriorDto
            {
                Fecha = Texto(row, "FechaUsadoCredito"),
                Tipo = Texto(row, "TipoUsadoCredito"),
                Cantidad = Entero(row, "CantidadUsadoCredito"),
                Usuario = Texto(row, "NombreUsuario")
            });
        }
        return lista;
    }

    /* ------------------------------------------------------------------ */

    public async Task<VentasUsadosGuardarResultadoDto> AjustarAsync(AjusteDeUsados ajuste, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        /*
          ESQUEMA DE PAGO: CUAL DE LOS DOS PROCEDIMIENTOS
          Tauro guarda con ...Ajuste y Zaragoza con ...AjusteZara. No es el
          mismo procedimiento con un parametro: son dos, y hacen cosas
          distintas. El de Tauro reescribe [Total a Pagar] y el renglon 0 de
          [Ventas Pagos]; el de Zaragoza no toca el total y en cambio mueve
          SaldoFavorCascos de la venta y del cliente. Por eso el que elige es
          Constantes.EsquemaPago y no una bandera nuestra.
        */
        var sp = ajuste.EsquemaPago.ToUpperInvariant() == "ZARA"
            ? "sp_n_VentasUsadosCreditoAjusteZara"
            : "sp_n_VentasUsadosCreditoAjuste";

        await using var cmd = new SqlCommand(sp, conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };

        /* IDCobro 0: se ajusta desde la consulta de ventas, no desde un cobro. */
        cmd.Parameters.AddWithValue("@IDCobro", 0);
        cmd.Parameters.AddWithValue("@IDVenta", ajuste.IdVenta);
        cmd.Parameters.AddWithValue("@IDUsuario", ajuste.IdUsuario);
        cmd.Parameters.AddWithValue("@IDsTipoUsadoCredito", Unir(ajuste.IdsTipoUsado));
        cmd.Parameters.AddWithValue("@CostosUsadoCredito", Unir(ajuste.Costos));
        cmd.Parameters.AddWithValue("@CantidadesUsadoCredito", Unir(ajuste.Cantidades));
        cmd.Parameters.AddWithValue("@ImportesUsadoCredito", Unir(ajuste.Importes));
        cmd.Parameters.AddWithValue("@Creditos", ajuste.Creditos);
        cmd.Parameters.AddWithValue("@IvaCreditos", ajuste.IvaCreditos);
        cmd.Parameters.AddWithValue("@TotalPagar", ajuste.TotalPagar);
        cmd.Parameters.AddWithValue("@CantidadesUsadoCreditoNuevo", Unir(ajuste.CantidadesNuevo));
        cmd.Parameters.AddWithValue("@Equipo", ajuste.Equipo);
        /* 0 = no se esta corrigiendo un movimiento del log, es uno nuevo. */
        cmd.Parameters.AddWithValue("@IDVentaUsadoCreditoLog", 0);

        await using var rd = await cmd.ExecuteReaderAsync(ct);

        if (!await rd.ReadAsync(ct))
        {
            /* El procedimiento SIEMPRE devuelve un renglon. Si no, algo se rompio. */
            return new VentasUsadosGuardarResultadoDto
            {
                Aplicado = false,
                IdVenta = ajuste.IdVenta,
                Mensaje = "El procedimiento de ajuste de usados no devolvió resultado."
            };
        }

        var result = LeerEntero(rd, "result");
        var mensaje = LeerTexto(rd, "mensaje");

        return new VentasUsadosGuardarResultadoDto
        {
            /* 1 exito; -1 sin existencias o error; -2 saldo negativo. */
            Aplicado = result == 1,
            IdVenta = ajuste.IdVenta,
            Folio = LeerTexto(rd, "Folio"),
            /* "@@@@" es el salto de linea de legacy dentro de un mensaje. */
            Mensaje = mensaje.Replace("@@@@", "\n")
        };
    }

    /* ------------------------------------------------------------------ */

    private static string Unir(IEnumerable<int> valores) => string.Join(Delimitador, valores);

    /*
      Los decimales se mandan con punto SIEMPRE. El procedimiento los parte con
      fn_DevuelveCadenaEnTabla y los convierte a decimal, asi que una coma de
      separador (que es lo que pondria una maquina en es-MX) los partiria en dos
      numeros y desalinearia TODAS las listas.
    */
    private static string Unir(IEnumerable<decimal> valores) =>
        string.Join(Delimitador, valores.Select(v => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)));

    private static async Task<DataTable> LlenarAsync(
        SqlConnection conn, string sp, Dictionary<string, object> parametros, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(sp, conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };
        foreach (var p in parametros) cmd.Parameters.AddWithValue(p.Key, p.Value);

        var tabla = new DataTable();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        tabla.Load(rd);
        return tabla;
    }

    /*
      Se lee POR NOMBRE y tolerando que la columna no exista. Los sp_n_ de
      legacy cambian de columnas sin avisar, y es preferible un cero a que la
      pantalla entera reviente por un campo que ya no esta.
    */
    private static string Texto(DataRow row, string columna)
    {
        if (!row.Table.Columns.Contains(columna)) return "";
        var v = row[columna];
        return v == DBNull.Value ? "" : (Convert.ToString(v) ?? "").Trim();
    }

    private static decimal Numero(DataRow row, string columna)
    {
        if (!row.Table.Columns.Contains(columna)) return 0m;
        var v = row[columna];
        return v == DBNull.Value ? 0m : Convert.ToDecimal(v);
    }

    private static int Entero(DataRow row, string columna)
    {
        if (!row.Table.Columns.Contains(columna)) return 0;
        var v = row[columna];
        return v == DBNull.Value ? 0 : Convert.ToInt32(v);
    }

    private static int LeerEntero(SqlDataReader rd, string columna)
    {
        var i = rd.GetOrdinal(columna);
        if (rd.IsDBNull(i)) return 0;
        return int.TryParse(Convert.ToString(rd.GetValue(i)), out var v) ? v : 0;
    }

    private static string LeerTexto(SqlDataReader rd, string columna)
    {
        var i = rd.GetOrdinal(columna);
        return rd.IsDBNull(i) ? "" : (Convert.ToString(rd.GetValue(i)) ?? "").Trim();
    }

    private static decimal LeerDecimal(SqlDataReader rd, string columna)
    {
        var i = rd.GetOrdinal(columna);
        return rd.IsDBNull(i) ? 0m : Convert.ToDecimal(rd.GetValue(i));
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
