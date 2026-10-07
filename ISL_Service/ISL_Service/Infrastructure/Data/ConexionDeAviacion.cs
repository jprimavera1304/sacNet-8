using System.Data;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Data;

/*
  A QUE BASE LE HABLA EL MODULO DE AVIACION, Y POR QUE NO ES LA DE SIEMPRE.

  Aviacion (recaudaciones, permisionarios, economicos, polizas) NO vive en la
  base de la empresa. Vive en un servidor aparte, y la CADENA DE CONEXION a ese
  servidor esta guardada EN UNA COLUMNA de la base de la empresa:
  dbo.Constantes.AviacionDBServer. Legacy hace literalmente
  `new SqlConnection(AviacionDBServer)` con el contenido de esa columna
  (Models/Repository/Aviacion/CatCuentaRepo/CatCuentaRepository.cs:120).

  Eso explica las dos rutas gemelas que tiene cada catalogo de aviacion en
  Legacy, y que aqui son el parametro `origen`:

    /consultar          -> la base LOCAL de la caja (la replica del cobrador)
    /consultar/server   -> el servidor CENTRAL de aviacion

  Legacy lo decide con un bool `server` que, cuando es false, deja
  AviacionDBServer en "" y entonces el repositorio no reemplaza la conexion
  (CatCuentaRepository.cs:49-50 `if (AviacionDBServer != "")`). O sea: sin
  bandera se pregunta a la replica local; con bandera, al central.

  LAS DOS CONSTANTES SE LEEN CON UN SELECT, NO CON sp_n_ConsultaConstantes.
  Legacy las saca de sp_n_ConsultaConstantes, que ademas de leer CIERRA EL DIA
  del negocio cuando la fecha de operacion esta atrasada: borra y rehace
  AlmacenProductoHist y adelanta Constantes.FechaOperacion
  (migracion/CUIDADO-consultas-que-escriben.md). Abrir el catalogo de cuentas no
  puede rodar la fecha de operacion de la empresa, asi que aqui se leen las dos
  columnas con un SELECT TOP 1 que no escribe nada.

  CUANDO LA EMPRESA NO ES DE AVIACION, SE DICE.
  En Produccion_svr (Tauro) AviacionDBServer esta VACIA y AviacionIDCaja es 0:
  Tauro no tiene el modulo. Legacy, en ese caso, intenta
  `new SqlConnection("")` y revienta con un ArgumentException que el servicio
  convierte en un 400 con el stack trace completo. Aqui se contesta un 409 que
  dice que la empresa no tiene el modulo configurado, que es la verdad y es
  accionable.
*/
public interface IConexionDeAviacion
{
    /// <summary>
    /// La cadena del servidor central de aviacion y el numero de caja de esta
    /// instalacion, leidos de dbo.Constantes.
    /// </summary>
    Task<ConfiguracionDeAviacion> LeerAsync(CancellationToken ct = default);

    /// <summary>
    /// Una conexion abierta al origen pedido: la base de la empresa
    /// (<paramref name="central"/> = false, la replica de la caja) o el
    /// servidor central de aviacion (true).
    /// </summary>
    Task<SqlConnection> AbrirAsync(bool central, CancellationToken ct = default);

    /// <summary>La conexion de siempre (la base de la empresa), ya abierta.</summary>
    Task<SqlConnection> AbrirLocalAsync(CancellationToken ct = default);
}

/// <summary>
/// AviacionDBServer: cadena de conexion al servidor central; vacia = la empresa
/// no tiene el modulo. AviacionIDCaja: 0 = no es una caja, asi que no hay
/// replica local que sincronizar.
/// </summary>
public sealed record ConfiguracionDeAviacion(string CadenaCentral, int IdCaja)
{
    public bool TieneServidorCentral => !string.IsNullOrWhiteSpace(CadenaCentral);
    public bool EsCaja => IdCaja != 0;
}

public class ConexionDeAviacion : IConexionDeAviacion
{
    private readonly IConfiguration _configuration;

    private const string SqlConstantes =
        "SELECT TOP 1 ISNULL(AviacionDBServer, '') AS AviacionDBServer, ISNULL(AviacionIDCaja, 0) AS AviacionIDCaja FROM dbo.Constantes;";

    public ConexionDeAviacion(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private string CadenaDeLaEmpresa()
    {
        var cs = _configuration.GetConnectionString("Main")
            ?? _configuration.GetConnectionString("Mac3")
            ?? _configuration.GetConnectionString("Local")
            ?? _configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("ConnectionString (Main/Mac3/Local/Default) no encontrada.");
        return cs;
    }

    public async Task<ConfiguracionDeAviacion> LeerAsync(CancellationToken ct = default)
    {
        await using var conn = new Mac3SqlServerConnector(CadenaDeLaEmpresa()).GetConnection;
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SqlConstantes, conn) { CommandType = CommandType.Text };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return new ConfiguracionDeAviacion(string.Empty, 0);

        var cadena = reader.IsDBNull(0) ? string.Empty : reader.GetString(0).Trim();
        var idCaja = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1));
        return new ConfiguracionDeAviacion(cadena, idCaja);
    }

    public async Task<SqlConnection> AbrirLocalAsync(CancellationToken ct = default)
    {
        var conn = new Mac3SqlServerConnector(CadenaDeLaEmpresa()).GetConnection;
        await conn.OpenAsync(ct);
        return conn;
    }

    public async Task<SqlConnection> AbrirAsync(bool central, CancellationToken ct = default)
    {
        if (!central) return await AbrirLocalAsync(ct);

        var config = await LeerAsync(ct);
        if (!config.TieneServidorCentral)
            throw new Application.Exceptions.ConflictException(
                "Esta empresa no tiene configurado el modulo de Aviacion.",
                "dbo.Constantes.AviacionDBServer esta vacia: no hay servidor central de aviacion al que preguntar.");

        var conn = new Mac3SqlServerConnector(config.CadenaCentral).GetConnection;
        await conn.OpenAsync(ct);
        return conn;
    }
}
