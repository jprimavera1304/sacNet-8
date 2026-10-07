using System.Data;
using ISL_Service.Application.Empresa;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// Lee dbo.Constantes SIN TOCAR NADA.
///
/// Es la unica lectura de constantes del backend que no puede permitirse escribir,
/// porque la pide el front en cada inicio de sesion. Por eso NO llama a
/// sp_n_ConsultaConstantes: ese procedimiento, cuando la fecha de operacion esta
/// atrasada, borra y rehace AlmacenProductoHist y adelanta Constantes.FechaOperacion
/// -el cierre del dia- y lo haria el primer login de cada manana, desde el web, sin
/// que nadie lo pidiera (migracion/CUIDADO-consultas-que-escriben.md).
///
/// Lo que si se copia del procedimiento son sus derivados, para que el front vea lo
/// mismo que Mac31:
///   PorcentajeIVA   = IVA / 100                         (MapaDeCapacidades)
///   Servidor        = SERVERPROPERTY('MachineName')
///   BaseDatos       = db_name()
///   fecha de pagos  = [Usuarios Fechas] en ZARA, Constantes en TAU
/// </summary>
public class CapacidadesEmpresaRepository : ICapacidadesEmpresaRepository
{
    private const int CommandTimeoutSeconds = 30;

    /*
      Un solo SELECT, columna por columna y con ISNULL en todo lo que la tabla
      permite nulo. No se usa SELECT * a proposito: las dos bases tienen la misma
      lista de columnas hoy, pero una tercera instalacion (Luro, un centro de
      servicio) podria no tenerla, y con nombres explicitos el fallo dice cual
      falta en vez de salir como un indice fuera de rango al mapear.
    */
    private const string SqlConstantes = @"
SELECT TOP 1
    CONVERT(varchar(128), SERVERPROPERTY('MachineName')) AS Servidor,
    DB_NAME()                                            AS BaseDatos,
    UPPER(LTRIM(RTRIM(ISNULL(Funcionalidad, ''))))       AS Funcionalidad,
    UPPER(LTRIM(RTRIM(ISNULL(EsquemaPago, ''))))         AS EsquemaPago,
    ISNULL(EsCentroServicio, 0)                          AS EsCentroServicio,
    ISNULL(EsAlcaldiaMC, 0)                              AS EsAlcaldiaMC,
    ISNULL(EsLuro, 0)                                    AS EsLuro,
    ISNULL(EsAviacion, 0)                                AS EsAviacion,
    ISNULL(EsAviacionServer, 0)                          AS EsAviacionServer,
    ISNULL(AviacionIDCaja, 0)                            AS AviacionIDCaja,
    ISNULL(InventarioNegativo, 0)                        AS InventarioNegativo,
    ISNULL(IVA, 0)                                       AS IVA,
    ISNULL(NoDiasAtrasados, 0)                           AS NoDiasAtrasados,
    ISNULL(NoDiasAdelantados, 0)                         AS NoDiasAdelantados,
    ISNULL(NoDiasDevDeposito, 0)                         AS NoDiasDevDeposito,
    ISNULL(DiasFolioConcentrado, 0)                      AS DiasFolioConcentrado,
    ISNULL(DiasFolioCobranza, 0)                         AS DiasFolioCobranza,
    ISNULL(NumDiasPedidosRechazados, 0)                  AS NumDiasPedidosRechazados,
    ISNULL(DiferenciaPagoVsTransCheq, 0)                 AS DiferenciaPagoVsTransCheq,
    ISNULL(MaximaDiferenciaUsados, 0)                    AS MaximaDiferenciaUsados,
    ISNULL(FacturaPorcentajeDescMax, 0)                  AS FacturaPorcentajeDescMax,
    ISNULL(PorDescMax, 0)                                AS PorDescMax,
    ISNULL(MaxTiempoInactividadMinutos, 0)               AS MaxTiempoInactividadMinutos,
    ISNULL(ChecadorHoraTarde, 0)                         AS ChecadorHoraTarde,
    ISNULL(horasLaborales, 0)                            AS HorasLaborales,
    ISNULL(HoraEntrada, '')                              AS HoraEntrada,
    ISNULL(SalidaTarde, '')                              AS SalidaTarde,
    ISNULL(defaultNumCopias, 0)                          AS DefaultNumCopias,
    ISNULL(maxNumCopias, 0)                              AS MaxNumCopias,
    ISNULL(IDAgenteAutorizaGarantias, 0)                 AS IDAgenteAutorizaGarantias,
    ISNULL(IDGrupoCategoriaServicios, 0)                 AS IDGrupoCategoriaServicios,
    ISNULL(IDEmpresaCS, 0)                               AS IDEmpresaCS,
    ISNULL(IDEmpresaZara, 0)                             AS IDEmpresaZara,
    ISNULL(IDEmpresaLuro, 0)                             AS IDEmpresaLuro,
    ISNULL(IDClienteCS, 0)                               AS IDClienteCS,
    ISNULL(IDsClienteZaragoza, '')                       AS IDsClienteZaragoza,
    ISNULL(NombreCentroServicio, '')                     AS NombreCentroServicio,
    ISNULL(ZaraSaldoLiquidarDineroMonto, 0)              AS ZaraSaldoLiquidarDineroMonto,
    ISNULL(ZaraSaldoLiquidarCascosMonto, 0)              AS ZaraSaldoLiquidarCascosMonto,
    ISNULL(PrecioCascoKilo, 0)                           AS PrecioCascoKilo,
    ISNULL(IDClienteServicioDomicilioZara, 0)            AS ClienteDomicilio0,
    ISNULL(IDClienteServicioDomicilioZaraCS1, 0)         AS ClienteDomicilio1,
    ISNULL(IDClienteServicioDomicilioZaraCS2, 0)         AS ClienteDomicilio2,
    ISNULL(IDClienteServicioDomicilioZaraCS3, 0)         AS ClienteDomicilio3,
    ISNULL(IDClienteServicioDomicilioZaraCS4, 0)         AS ClienteDomicilio4,
    ISNULL(IDClienteServicioDomicilioZaraCS5, 0)         AS ClienteDomicilio5,
    FechaOperacion                                       AS FechaOperacion,
    FechaOperacionPagos                                  AS FechaOperacionPagos,
    ISNULL(UsuVen.Usuario, '')                           AS UsuarioFechaOperacion,
    FechaCambioFechaOperacion                            AS FechaCambioFechaOperacion,
    ISNULL(EquipoFechaOperacion, '')                     AS EquipoFechaOperacion,
    ISNULL(UsuPag.Usuario, '')                           AS UsuarioFechaOperacionPagos,
    FechaCambioFechaOperacionPagos                       AS FechaCambioFechaOperacionPagos,
    ISNULL(EquipoFechaOperacionPagos, '')                AS EquipoFechaOperacionPagos
FROM dbo.Constantes C
    LEFT JOIN dbo.Usuarios UsuVen ON UsuVen.IDUsuario = C.IDUsuarioFechaOperacion
    LEFT JOIN dbo.Usuarios UsuPag ON UsuPag.IDUsuario = C.IDUsuarioFechaOperacionPagos;";

    /*
      LA FECHA DE PAGOS DE ZARAGOZA ES DE CADA USUARIO.
      sp_n_ConsultaConstantes la saca de [Usuarios Fechas] solo cuando
      Funcionalidad = 'ZARA', y ahi mismo INSERTA la fila si no existe. Aqui NO se
      inserta: si el usuario no tiene fila todavia, se devuelve la de Constantes,
      que es exactamente el valor con el que legacy la crearia. Leer capacidades no
      escribe.
    */
    private const string SqlFechaPagosDelUsuario = @"
SELECT TOP 1 FechaOperacionPagos
FROM dbo.[Usuarios Fechas]
WHERE IDUsuario = @IDUsuario;";

    private readonly IConfiguration _configuration;

    public CapacidadesEmpresaRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<ConstantesDeEmpresa> ConsultarAsync(int idUsuario, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var constantes = await LeerConstantesAsync(conn, ct);

        // Solo en ZARA existe la fecha por usuario; en TAU la consulta sobraria.
        if (constantes.Funcionalidad == "ZARA" && idUsuario > 0)
            constantes.FechaOperacionPagosDelUsuario = await LeerFechaPagosDelUsuarioAsync(conn, idUsuario, ct);

        return constantes;
    }

    private static async Task<ConstantesDeEmpresa> LeerConstantesAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(SqlConstantes, conn)
        {
            CommandType = CommandType.Text,
            CommandTimeout = CommandTimeoutSeconds
        };

        await using var rd = await cmd.ExecuteReaderAsync(ct);
        if (!await rd.ReadAsync(ct))
            throw new Application.Exceptions.ConflictException(
                "La empresa no tiene constantes configuradas.",
                "dbo.Constantes no tiene ningun renglon.");

        var hoy = DateOnly.FromDateTime(DateTime.Today);

        return new ConstantesDeEmpresa
        {
            Servidor = Texto(rd, "Servidor"),
            BaseDatos = Texto(rd, "BaseDatos"),
            Funcionalidad = Texto(rd, "Funcionalidad"),
            EsquemaPago = Texto(rd, "EsquemaPago"),
            EsCentroServicio = Entero(rd, "EsCentroServicio"),
            EsAlcaldiaMC = Entero(rd, "EsAlcaldiaMC"),
            EsLuro = Entero(rd, "EsLuro"),
            EsAviacion = Entero(rd, "EsAviacion"),
            EsAviacionServer = Entero(rd, "EsAviacionServer"),
            AviacionIdCaja = Entero(rd, "AviacionIDCaja"),
            InventarioNegativo = Entero(rd, "InventarioNegativo"),
            Iva = Decimal(rd, "IVA"),
            NoDiasAtrasados = Entero(rd, "NoDiasAtrasados"),
            NoDiasAdelantados = Entero(rd, "NoDiasAdelantados"),
            NoDiasDevDeposito = Entero(rd, "NoDiasDevDeposito"),
            DiasFolioConcentrado = Entero(rd, "DiasFolioConcentrado"),
            DiasFolioCobranza = Entero(rd, "DiasFolioCobranza"),
            NumDiasPedidosRechazados = Entero(rd, "NumDiasPedidosRechazados"),
            DiferenciaPagoVsTransCheq = Decimal(rd, "DiferenciaPagoVsTransCheq"),
            MaximaDiferenciaUsados = Decimal(rd, "MaximaDiferenciaUsados"),
            FacturaPorcentajeDescMax = Decimal(rd, "FacturaPorcentajeDescMax"),
            PorDescMax = Decimal(rd, "PorDescMax"),
            MaxTiempoInactividadMinutos = Entero(rd, "MaxTiempoInactividadMinutos"),
            ChecadorHoraTarde = Entero(rd, "ChecadorHoraTarde"),
            HorasLaborales = Entero(rd, "HorasLaborales"),
            HoraEntrada = Texto(rd, "HoraEntrada"),
            SalidaTarde = Texto(rd, "SalidaTarde"),
            DefaultNumCopias = Entero(rd, "DefaultNumCopias"),
            MaxNumCopias = Entero(rd, "MaxNumCopias"),
            IdAgenteAutorizaGarantias = Entero(rd, "IDAgenteAutorizaGarantias"),
            IdGrupoCategoriaServicios = Entero(rd, "IDGrupoCategoriaServicios"),
            IdEmpresaCS = Entero(rd, "IDEmpresaCS"),
            IdEmpresaZara = Entero(rd, "IDEmpresaZara"),
            IdEmpresaLuro = Entero(rd, "IDEmpresaLuro"),
            IdClienteCS = Entero(rd, "IDClienteCS"),
            IdsClienteZaragoza = Texto(rd, "IDsClienteZaragoza"),
            NombreCentroServicio = Texto(rd, "NombreCentroServicio"),
            ZaraSaldoLiquidarDineroMonto = Decimal(rd, "ZaraSaldoLiquidarDineroMonto"),
            ZaraSaldoLiquidarCascosMonto = Decimal(rd, "ZaraSaldoLiquidarCascosMonto"),
            PrecioCascoKilo = Decimal(rd, "PrecioCascoKilo"),
            IdsClienteServicioDomicilio = new List<int>
            {
                Entero(rd, "ClienteDomicilio0"),
                Entero(rd, "ClienteDomicilio1"),
                Entero(rd, "ClienteDomicilio2"),
                Entero(rd, "ClienteDomicilio3"),
                Entero(rd, "ClienteDomicilio4"),
                Entero(rd, "ClienteDomicilio5")
            },
            FechaOperacion = Fecha(rd, "FechaOperacion") ?? hoy,
            FechaOperacionPagos = Fecha(rd, "FechaOperacionPagos") ?? hoy,
            UsuarioFechaOperacion = Texto(rd, "UsuarioFechaOperacion"),
            FechaCambioFechaOperacion = FechaHora(rd, "FechaCambioFechaOperacion"),
            EquipoFechaOperacion = Texto(rd, "EquipoFechaOperacion"),
            UsuarioFechaOperacionPagos = Texto(rd, "UsuarioFechaOperacionPagos"),
            FechaCambioFechaOperacionPagos = FechaHora(rd, "FechaCambioFechaOperacionPagos"),
            EquipoFechaOperacionPagos = Texto(rd, "EquipoFechaOperacionPagos")
        };
    }

    private static async Task<DateOnly?> LeerFechaPagosDelUsuarioAsync(
        SqlConnection conn, int idUsuario, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(SqlFechaPagosDelUsuario, conn)
        {
            CommandType = CommandType.Text,
            CommandTimeout = CommandTimeoutSeconds
        };
        cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);

        var valor = await cmd.ExecuteScalarAsync(ct);
        if (valor is null || valor is DBNull) return null;
        return DateOnly.FromDateTime(Convert.ToDateTime(valor));
    }

    private static string Texto(SqlDataReader rd, string columna)
    {
        var i = rd.GetOrdinal(columna);
        return rd.IsDBNull(i) ? string.Empty : (Convert.ToString(rd.GetValue(i)) ?? string.Empty).Trim();
    }

    private static int Entero(SqlDataReader rd, string columna)
    {
        var i = rd.GetOrdinal(columna);
        return rd.IsDBNull(i) ? 0 : Convert.ToInt32(rd.GetValue(i));
    }

    private static decimal Decimal(SqlDataReader rd, string columna)
    {
        var i = rd.GetOrdinal(columna);
        return rd.IsDBNull(i) ? 0m : Convert.ToDecimal(rd.GetValue(i));
    }

    private static DateOnly? Fecha(SqlDataReader rd, string columna)
    {
        var i = rd.GetOrdinal(columna);
        return rd.IsDBNull(i) ? null : DateOnly.FromDateTime(Convert.ToDateTime(rd.GetValue(i)));
    }

    private static DateTime? FechaHora(SqlDataReader rd, string columna)
    {
        var i = rd.GetOrdinal(columna);
        return rd.IsDBNull(i) ? null : Convert.ToDateTime(rd.GetValue(i));
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
}
