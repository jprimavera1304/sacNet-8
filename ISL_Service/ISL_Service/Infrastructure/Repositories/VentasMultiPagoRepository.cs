using System.Data;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/* ------------------------------------------------------------------ */
/* Lo que sale de la base, sin interpretar                             */
/* ------------------------------------------------------------------ */

public class MultiPagoConstantes
{
    /// <summary>"TAU" o "ZARA". Es la unica bifurcacion por empresa que se usa.</summary>
    public string Funcionalidad { get; set; } = string.Empty;
    public string EsquemaPago { get; set; } = string.Empty;
    public int EsCentroServicio { get; set; }
    public decimal PrecioCascoKilo { get; set; }

    /// <summary>
    /// Cuanto se tolera que el abono pase del monto del documento. NO es una
    /// constante del programa: en Tauro vale 1.00 y en Zaragoza 0.00. Si se
    /// escribiera a mano, una de las dos empresas cobraria de mas o de menos.
    /// </summary>
    public decimal DiferenciaPagoVsTransCheq { get; set; }

    /// <summary>El banco de la empresa: el que Mac31 preselecciona en el deposito.</summary>
    public int IdBancoEmpresa { get; set; }

    /// <summary>"FECHA PAGOS" tal como la pinta Mac31, ya formateada por legacy.</summary>
    public string FechaOperacionPagosFtm { get; set; } = string.Empty;

    /// <summary>La fecha de operacion, de donde sale la clave del dia.</summary>
    public DateTime? FechaOperacion { get; set; }
}

public class MultiPagoRemisionCruda
{
    public int IdVenta { get; set; }
    public int IdCliente { get; set; }
    public string Empresa { get; set; } = string.Empty;
    public string FolioFtm { get; set; } = string.Empty;
    public string Fecha { get; set; } = string.Empty;
    public string NombreCliente { get; set; } = string.Empty;

    /// <summary>Tauro pinta esta...</summary>
    public decimal TotalPagar { get; set; }
    /// <summary>...y Zaragoza esta otra. No son el mismo numero.</summary>
    public decimal ImporteZ { get; set; }

    public decimal Cargos { get; set; }
    public decimal Descuentos { get; set; }
    public decimal Abonos { get; set; }

    /// <summary>El saldo de Tauro.</summary>
    public decimal Saldo { get; set; }
    /// <summary>El saldo de Zaragoza.</summary>
    public decimal SaldoImporte { get; set; }

    public decimal SaldoFavor { get; set; }
    public decimal SaldoCascos { get; set; }

    public decimal SaldoFavorCliente { get; set; }
    public decimal SaldoFavorCascosCliente { get; set; }

    public string Cancelada { get; set; } = string.Empty;
    public string Pagada { get; set; } = string.Empty;

    public int FolioCobro { get; set; }
    public int NuevoEsquemaPagos { get; set; }
}

/// <summary>Los parametros de UNA llamada a sp_n_InsertarVentasPagos.</summary>
public class MultiPagoParametrosSp
{
    public int IdCobro { get; set; }
    public int IdVenta { get; set; }
    public int IdCliente { get; set; }
    public int IdTipoPago { get; set; }
    public int IdUsuario { get; set; }
    public decimal Abono { get; set; }
    public decimal MontoTotal { get; set; }
    public string Observaciones { get; set; } = string.Empty;
    public int IdBancoCheque { get; set; }
    public int IdBancoTransfer { get; set; }
    public int IdBancoTarjeta { get; set; }
    public string Transferencia { get; set; } = string.Empty;
    public string Cheque { get; set; } = string.Empty;
    public string Tarjeta { get; set; } = string.Empty;
    public int DiffUsados { get; set; }
    public string DepositoEfectivoNumero { get; set; } = string.Empty;
    public int IdBancoDepositoEfe { get; set; }
    public string ExcedenteUsadosMotivo { get; set; } = string.Empty;
    public decimal Cargo { get; set; }
    public string CargoMotivo { get; set; } = string.Empty;
    public int IdAgenteLiquidacion { get; set; }
    public int IdRepartidorLiquidacion { get; set; }
    public int TipoTarjeta { get; set; }
    public decimal CascoKiloCantidad { get; set; }
    public decimal CascoKiloTotal { get; set; }
    public decimal CascoKiloPrecio { get; set; }
    public string CascoKiloMotivo { get; set; } = string.Empty;
    public string Equipo { get; set; } = string.Empty;
}

public class MultiPagoResultadoSp
{
    public int Result { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public decimal SaldoFavorDineroCliente { get; set; }
    public decimal SaldoFavorCascosCliente { get; set; }
}

/* ------------------------------------------------------------------ */

/*
  MULTI PAGO: LOS MISMOS PROCEDIMIENTOS QUE MAC31

  Ni uno nuevo, ni uno tocado:

    sp_n_ConsultaConstantes      la empresa y sus constantes (Funcionalidad,
                                 PrecioCascoKilo, DiferenciaPagoVsTransCheq...)
    sp_n_ConsultaVentas          las remisiones seleccionadas, con sus saldos
                                 — VentasPago.cs 1152, URL_CONSULTAR_VENTAS
    sp_n_ConsultaBancos          el catalogo de bancos (Consultas.cs 540)
    sp_n_ConsultaRepartidores    agente y camioneta de la liquidacion de Tauro
    sp_n_InsertarContrasenaCodigo la bitacora del intento de clave del dia
    sp_n_InsertarVentasPagos     el abono, UNA llamada por remision
                                 — Variables.cs 516, "api/ventaspagos"

  NO se calcula ni un saldo aqui. El saldo de una remision es de los numeros
  mas delicados del sistema y lo arma sp_n_ConsultaVentas con reglas distintas
  por empresa (cascos, creditos, notas de credito, documentos de ajuste). Si lo
  calculara este archivo, el dia que legacy cambiara una regla el web seguiria
  diciendo el numero viejo, sin error y sin aviso.
*/
public class VentasMultiPagoRepository : IVentasMultiPagoRepository
{
    private const int CommandTimeoutSeconds = 500;

    /*
      EL SEPARADOR ES LA VIRGULILLA, NO LA COMA.

      sp_n_ConsultaVentas parte @IDsVenta con fn_DevuelveCadenaEnTabla y
      @Delimitador = '~' (esta escrito a mano dentro del procedimiento). Mac31
      arma la cadena con Globales.DELIMITADOR_PARAM_TILDE_SP = "~"
      (ConsultarVentas.cs 4032).

      Mandarlo separado por comas NO es un detalle de estilo: con dos o mas
      remisiones el procedimiento truena con
        "Conversion failed when converting the varchar value '1,2,3' to int"
      Se comprobo en Produccion_svr. Con una sola remision no truena, y por eso
      es el tipo de error que se descubre tarde y en produccion.
    */
    private const string Delimitador = "~";

    private readonly IConfiguration _configuration;

    public VentasMultiPagoRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private SqlConnection GetConnection()
    {
        var cs = _configuration.GetConnectionString("Main")
            ?? _configuration.GetConnectionString("Mac3")
            ?? _configuration.GetConnectionString("Local")
            ?? _configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("ConnectionString (Main/Mac3/Local/Default) no encontrada.");

        return new Mac3SqlServerConnector(cs).GetConnection;
    }

    /* -------------------------------------------------------------- */

    public async Task<MultiPagoConstantes> ConstantesAsync(CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand("sp_n_ConsultaConstantes", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };
        cmd.Parameters.AddWithValue("@ValidaActividad", 0);
        cmd.Parameters.AddWithValue("@IDUsuario", 0);
        cmd.Parameters.AddWithValue("@Equipo", string.Empty);

        var t = await PrimeraTablaAsync(cmd, ct);
        if (t.Rows.Count == 0) return new MultiPagoConstantes();

        var r = t.Rows[0];
        return new MultiPagoConstantes
        {
            Funcionalidad = Texto(r, "Funcionalidad").ToUpperInvariant(),
            EsquemaPago = Texto(r, "EsquemaPago").ToUpperInvariant(),
            EsCentroServicio = Entero(r, "EsCentroServicio"),
            PrecioCascoKilo = Decimal(r, "PrecioCascoKilo"),
            DiferenciaPagoVsTransCheq = Decimal(r, "DiferenciaPagoVsTransCheq"),
            IdBancoEmpresa = Entero(r, "IDBancoEmpresa"),
            /*
              Mac31 rotula "FECHA PAGOS:" con Variables.FechaOperacionPagosFtm
              (VentasPago.cs 195). Esa variable se llena de esta misma columna
              al arrancar el programa, asi que se toma tal cual: no se
              reformatea una fecha que legacy ya formateo.
            */
            FechaOperacionPagosFtm = Texto(r, "FechaOperacionPagosFtm"),
            FechaOperacion = Fecha(r, "FechaOperacion")
        };
    }

    public async Task<List<MultiPagoRemisionCruda>> RemisionesAsync(IEnumerable<int> idsVenta, CancellationToken ct)
    {
        var ids = idsVenta.Where(x => x > 0).Distinct().ToList();
        var lista = new List<MultiPagoRemisionCruda>();
        if (ids.Count == 0) return lista;

        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand("sp_n_ConsultaVentas", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };

        /*
          LOS 21 PARAMETROS, EN CERO SALVO @IDsVenta.

          Es lo que hace Mac31: ConsultarVentas.btnMultiPago_Click llena
          UNICAMENTE IDsVentaLst del ConsultaVentasInputDTO (linea 4031) y el
          constructor del DTO deja el resto en cero. Cuando @IDsVenta viene
          lleno, el procedimiento ignora todos los demas filtros y busca SOLO
          por esa lista (sp_n_ConsultaVentas, linea 623: "Busqueda SOLO por
          @IDsVenta"), asi que no hay riesgo de arrastrar un rango de fechas.

          Se mandan explicitos y no por omision porque los valores por omision
          del procedimiento traen datos de prueba escritos a mano.
        */
        cmd.Parameters.AddWithValue("@IDsVenta", string.Join(Delimitador, ids));
        cmd.Parameters.AddWithValue("@IDVenta", 0);
        cmd.Parameters.AddWithValue("@IDEmpresa", 0);
        cmd.Parameters.AddWithValue("@IDCliente", 0);
        cmd.Parameters.AddWithValue("@IDUsuario", 0);
        cmd.Parameters.AddWithValue("@IDAgente", 0);
        cmd.Parameters.AddWithValue("@IDTipoDocumento", 0);
        cmd.Parameters.AddWithValue("@TipoDocumento", string.Empty);
        cmd.Parameters.AddWithValue("@FolioInicial", 0);
        cmd.Parameters.AddWithValue("@FolioFinal", 0);
        cmd.Parameters.AddWithValue("@FechaEmisionInicial", string.Empty);
        cmd.Parameters.AddWithValue("@FechaEmisionFinal", string.Empty);
        cmd.Parameters.AddWithValue("@FechaCancelInicial", string.Empty);
        cmd.Parameters.AddWithValue("@FechaCancelFinal", string.Empty);
        cmd.Parameters.AddWithValue("@FechaPagoInicial", string.Empty);
        cmd.Parameters.AddWithValue("@FechaPagoFinal", string.Empty);
        cmd.Parameters.AddWithValue("@Formato", 0);
        cmd.Parameters.AddWithValue("@IDUsuarioActual", 0);
        cmd.Parameters.AddWithValue("@IDsClientes", string.Empty);
        cmd.Parameters.AddWithValue("@IDsProducto", string.Empty);
        cmd.Parameters.AddWithValue("@IDProducto", 0);

        var t = await PrimeraTablaAsync(cmd, ct);

        foreach (DataRow r in t.Rows)
        {
            lista.Add(new MultiPagoRemisionCruda
            {
                IdVenta = Entero(r, "IDVenta"),
                IdCliente = Entero(r, "IDCliente"),
                Empresa = Texto(r, "Empresa"),
                FolioFtm = Texto(r, "FolioFtm"),
                Fecha = Texto(r, "FechaEmisionCortoFtm"),
                NombreCliente = Texto(r, "NombreCliente"),
                TotalPagar = Decimal(r, "TotalPagar"),
                ImporteZ = Decimal(r, "ImporteZ"),
                Cargos = Decimal(r, "Cargos"),
                Descuentos = Decimal(r, "Descuentos"),
                Abonos = Decimal(r, "Abonos"),
                Saldo = Decimal(r, "Saldo"),
                SaldoImporte = Decimal(r, "SaldoImporte"),
                SaldoFavor = Decimal(r, "SaldoFavor"),
                SaldoCascos = Decimal(r, "SaldoCascos"),
                SaldoFavorCliente = Decimal(r, "SaldoFavorCliente"),
                SaldoFavorCascosCliente = Decimal(r, "SaldoFavorCascosCliente"),
                Cancelada = Texto(r, "Cancelada"),
                Pagada = Texto(r, "Pagada"),
                FolioCobro = Entero(r, "FolioCobro"),
                NuevoEsquemaPagos = Entero(r, "NuevoEsquemaPagos")
            });
        }

        /*
          El orden que devuelve el procedimiento es el suyo. Se respeta el orden
          en que la persona marco las remisiones, que es el que tenia delante en
          la pantalla de Ventas: si el grid se reordenara solo, el renglon donde
          escribio el pago cambiaria de sitio.
        */
        var posicion = ids.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        return lista
            .OrderBy(x => posicion.TryGetValue(x.IdVenta, out var p) ? p : int.MaxValue)
            .ToList();
    }

    public async Task<List<(int Id, string Nombre)>> BancosAsync(CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand("sp_n_ConsultaBancos", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };

        /*
          Los mismos valores que Mac31: new CatBancoInputDTO() deja IDBanco = 0,
          IDStatus = Activo (1), Banco = "" y Formato = Todo (0)
          — CatBancoInputDTO.cs, constructor. @Identico = 0 porque el DTO de
          Mac31 ni siquiera tiene ese campo, asi que viaja sin valor.

          El catalogo NO es el mismo en las dos empresas (Tauro tiene ALBO,
          Zaragoza tiene AMEX), asi que se lee de la base y no se escribe aqui.
        */
        cmd.Parameters.AddWithValue("@IDBanco", 0);
        cmd.Parameters.AddWithValue("@IDStatus", 1);
        cmd.Parameters.AddWithValue("@Banco", string.Empty);
        cmd.Parameters.AddWithValue("@Formato", 0);
        cmd.Parameters.AddWithValue("@Identico", 0);

        var t = await PrimeraTablaAsync(cmd, ct);
        return t.Rows.Cast<DataRow>()
            .Select(r => (Entero(r, "IDBanco"), Texto(r, "Banco")))
            .Where(x => x.Item1 > 0)
            .OrderBy(x => x.Item2, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<List<(int Id, string Nombre)>> RepartidoresAsync(CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand("sp_n_ConsultaRepartidores", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };

        /*
          Mac31 llena los DOS desplegables de la liquidacion —"Agente" y
          "Camioneta"— con ESTE MISMO catalogo (VentasPago.cs 288 y 295, las dos
          llamadas a Consultas.ConsultarRepartidores). Es una rareza de legacy,
          pero es la que hay: dos listas distintas darian ids que el
          procedimiento no reconoce.
        */
        cmd.Parameters.AddWithValue("@IDRepartidor", 0);
        cmd.Parameters.AddWithValue("@IDZona", 0);
        cmd.Parameters.AddWithValue("@IDStatus", 1);
        cmd.Parameters.AddWithValue("@Repartidor", string.Empty);
        cmd.Parameters.AddWithValue("@Identico", 0);

        var t = await PrimeraTablaAsync(cmd, ct);
        return t.Rows.Cast<DataRow>()
            .Select(r => (Entero(r, "IDRepartidor"), Texto(r, "Repartidor")))
            .Where(x => x.Item1 > 0)
            .OrderBy(x => x.Item2, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<(int Aplica, string Contrasena)> ContrasenaDeUsuarioAsync(int idUsuario, CancellationToken ct)
    {
        if (idUsuario <= 0) return (0, string.Empty);

        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        /*
          Lectura directa y no un procedimiento porque no existe uno que
          devuelva solo esto, y Mac31 la trae dentro del usuario que ya cargo al
          entrar (Globales.usuarioActual.ContrasenaAutorizacion). Es una lectura
          de dos columnas, sin ninguna regla de negocio dentro.
        */
        await using var cmd = new SqlCommand(
            "SELECT TOP 1 ISNULL(AplicaContrasenaAutorizacion,0) AS Aplica, " +
            "       ISNULL(ContrasenaAutorizacion,'') AS Clave " +
            "FROM Usuarios WHERE IDUsuario = @IDUsuario;", conn)
        { CommandTimeout = CommandTimeoutSeconds };
        cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);

        await using var rd = await cmd.ExecuteReaderAsync(ct);
        if (!await rd.ReadAsync(ct)) return (0, string.Empty);

        var aplica = rd.IsDBNull(0) ? 0 : Convert.ToInt32(rd.GetValue(0));
        var clave = rd.IsDBNull(1) ? string.Empty : Convert.ToString(rd.GetValue(1)) ?? string.Empty;
        return (aplica, clave);
    }

    public async Task RegistrarIntentoDeClaveAsync(
        string forma, bool correcto, string clave, int idUsuario, string equipo, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand("sp_n_InsertarContrasenaCodigo", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };

        /*
          Mac31 deja constancia del intento SIEMPRE, acertado o no
          (ConfirmarContrasena.cs 126: InsertarContrasena se llama antes de
          mirar si Correcto vale 0). Esa bitacora es justo la que sirve para
          saber quien estuvo probando claves, asi que tambien se escribe aqui
          cuando falla.
        */
        cmd.Parameters.AddWithValue("@IDForma", 0);
        cmd.Parameters.AddWithValue("@IDProceso", 0);
        cmd.Parameters.AddWithValue("@Forma", forma ?? string.Empty);
        cmd.Parameters.AddWithValue("@Correcto", correcto ? 1 : 0);
        cmd.Parameters.AddWithValue("@Contrasena", clave ?? string.Empty);
        cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
        cmd.Parameters.AddWithValue("@Equipo", equipo ?? string.Empty);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<MultiPagoResultadoSp> InsertarPagoAsync(MultiPagoParametrosSp p, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand("sp_n_InsertarVentasPagos", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };

        /*
          MISMO ORDEN Y MISMOS NOMBRES QUE EL API DE MAC31

          Copiado de Mac3Servicios, VentasPagosRepository.spInsertar (lineas
          126-163). Se mandan TODOS aunque muchos vayan en cero: el
          procedimiento tiene valores por omision con datos de prueba.

          El procedimiento abre y cierra su PROPIA transaccion, una por
          llamada. Eso no se toca: envolver las llamadas en una transaccion de
          aqui cambiaria el comportamiento de un procedimiento de legacy que
          hace COMMIT dentro.
        */
        cmd.Parameters.AddWithValue("@IDCobro", p.IdCobro);
        cmd.Parameters.AddWithValue("@IDVentasDescuentosAjustes", 0);
        cmd.Parameters.AddWithValue("@IDVenta", p.IdVenta);
        cmd.Parameters.AddWithValue("@IDCliente", p.IdCliente);
        cmd.Parameters.AddWithValue("@IDTipoPago", p.IdTipoPago);
        cmd.Parameters.AddWithValue("@IDUsuario", p.IdUsuario);
        cmd.Parameters.AddWithValue("@Abono", p.Abono);
        cmd.Parameters.AddWithValue("@MontoTotal", p.MontoTotal);
        cmd.Parameters.AddWithValue("@Observaciones", p.Observaciones ?? string.Empty);
        cmd.Parameters.AddWithValue("@IDBancoCheque", p.IdBancoCheque);
        cmd.Parameters.AddWithValue("@IDBancoTransfer", p.IdBancoTransfer);
        cmd.Parameters.AddWithValue("@IDBancoTarjeta", p.IdBancoTarjeta);
        cmd.Parameters.AddWithValue("@Transferencia", p.Transferencia ?? string.Empty);
        cmd.Parameters.AddWithValue("@Cheque", p.Cheque ?? string.Empty);
        cmd.Parameters.AddWithValue("@Tarjeta", p.Tarjeta ?? string.Empty);
        cmd.Parameters.AddWithValue("@DiffUsados", p.DiffUsados);
        cmd.Parameters.AddWithValue("@DepositoEfectivoNumero", p.DepositoEfectivoNumero ?? string.Empty);
        cmd.Parameters.AddWithValue("@IDBancoDepositoEfe", p.IdBancoDepositoEfe);
        cmd.Parameters.AddWithValue("@ExcedenteUsadosMotivo", p.ExcedenteUsadosMotivo ?? string.Empty);
        cmd.Parameters.AddWithValue("@Cargo", p.Cargo);
        cmd.Parameters.AddWithValue("@CargoMotivo", p.CargoMotivo ?? string.Empty);
        cmd.Parameters.AddWithValue("@IDsTipoUsado", string.Empty);
        cmd.Parameters.AddWithValue("@CantidadesUsado", string.Empty);
        cmd.Parameters.AddWithValue("@CostosConIvaUsado", string.Empty);
        cmd.Parameters.AddWithValue("@IDAgenteLiquidacion", p.IdAgenteLiquidacion);
        cmd.Parameters.AddWithValue("@IDRepartidorLiquidacion", p.IdRepartidorLiquidacion);
        cmd.Parameters.AddWithValue("@TipoTarjeta", p.TipoTarjeta);
        cmd.Parameters.AddWithValue("@IDPedido", 0);
        cmd.Parameters.AddWithValue("@IDGarantia", 0);
        cmd.Parameters.AddWithValue("@PedidoNumeroPago", 0);
        cmd.Parameters.AddWithValue("@IDBancoTransferenciaMovimiento", 0);
        cmd.Parameters.AddWithValue("@CascoKiloCantidad", p.CascoKiloCantidad);
        cmd.Parameters.AddWithValue("@CascoKiloTotal", p.CascoKiloTotal);
        cmd.Parameters.AddWithValue("@CascoKiloPrecio", p.CascoKiloPrecio);
        cmd.Parameters.AddWithValue("@CascoKiloMotivo", p.CascoKiloMotivo ?? string.Empty);
        cmd.Parameters.AddWithValue("@Equipo", p.Equipo ?? string.Empty);

        var t = await PrimeraTablaAsync(cmd, ct);
        if (t.Rows.Count == 0)
            return new MultiPagoResultadoSp { Result = -1, Mensaje = "El procedimiento no contestó." };

        var r = t.Rows[0];
        return new MultiPagoResultadoSp
        {
            Result = Entero(r, "result"),
            Mensaje = Texto(r, "mensaje"),
            SaldoFavorDineroCliente = Decimal(r, "SaldoFavorDineroCliente"),
            SaldoFavorCascosCliente = Decimal(r, "SaldoFavorCascosCliente")
        };
    }

    /* -------------------------------------------------------------- */
    /* Lectura tolerante                                               */
    /* -------------------------------------------------------------- */

    /*
      Se lee POR NOMBRE y tolerando que la columna no venga. sp_n_ConsultaVentas
      devuelve mas de cien columnas armadas con SQL dinamico, y no todas salen
      en las dos empresas. Pedirlas por posicion seria una excepcion en cada
      consulta el dia que alguien agregue una columna en medio.
    */
    private static async Task<DataTable> PrimeraTablaAsync(SqlCommand cmd, CancellationToken ct)
    {
        var tabla = new DataTable();
        using var adapter = new SqlDataAdapter(cmd);
        await Task.Run(() => adapter.Fill(tabla), ct);
        return tabla;
    }

    private static bool Hay(DataRow r, string columna) =>
        r.Table.Columns.Contains(columna) && r[columna] != DBNull.Value;

    private static string Texto(DataRow r, string columna) =>
        Hay(r, columna) ? (Convert.ToString(r[columna]) ?? string.Empty).Trim() : string.Empty;

    private static int Entero(DataRow r, string columna)
    {
        if (!Hay(r, columna)) return 0;
        return int.TryParse(Convert.ToString(r[columna]), out var v) ? v : 0;
    }

    private static decimal Decimal(DataRow r, string columna)
    {
        if (!Hay(r, columna)) return 0m;
        try { return Convert.ToDecimal(r[columna]); } catch { return 0m; }
    }

    private static DateTime? Fecha(DataRow r, string columna)
    {
        if (!Hay(r, columna)) return null;
        try { return Convert.ToDateTime(r[columna]); } catch { return null; }
    }
}
