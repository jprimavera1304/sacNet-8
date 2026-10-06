using System.Data;
using ISL_Service.Application.DTOs.ActualizacionesAplicacion;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// `sp_n_ConsultaAplicacionActualizacion`.
///
/// EL SP TERMINA EN `SELECT * FROM AplicacionesActualizacion`, asi que las
/// columnas son las de la tabla y pueden crecer con un ALTER TABLE (`DriveId` se
/// agrego asi). Por eso el DTO se llena por NOMBRE de columna: una columna nueva
/// simplemente no aparece hasta que alguien la declare, en vez de romper el
/// mapeo.
///
/// LEGACY NO DEVOLVIA ESTO DE FORMA USABLE. Su repositorio hacia
/// `ds.Tables[0].AsEnumerable().ToList()` y lo envolvia en
/// `GenericResponse(List&lt;DataRow&gt;)`: Newtonsoft no tiene converter para
/// `DataRow`, asi que lo que salia por el cable eran las propiedades publicas del
/// objeto DataRow (`itemArray`, `table`, `rowState`, `hasErrors`...) y el cliente
/// tenia que leer los valores por POSICION dentro de `itemArray`. El DTO existia
/// en el codigo y estaba declarado en el `[ResponseType]` del controlador, pero
/// nunca se usaba. Aqui se publican las columnas con su nombre.
/// </summary>
public class ActualizacionesAplicacionRepository : IActualizacionesAplicacionRepository
{
    private const string SpConsultar = "dbo.sp_n_ConsultaAplicacionActualizacion";

    private readonly IConfiguration _configuration;

    public ActualizacionesAplicacionRepository(IConfiguration configuration)
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
        var connector = new Mac3SqlServerConnector(cs);
        return connector.GetConnection;
    }

    public async Task<List<ActualizacionAplicacionDto>> ConsultarAsync(
        int id, int idAplicacion, int idStatus, string archivoOrigen, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpConsultar, conn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@IDAplicacionActualizacion", id);
        cmd.Parameters.AddWithValue("@IDAplicacion", idAplicacion);
        cmd.Parameters.AddWithValue("@IDStatus", idStatus);
        // Nunca DBNull: el SP compara `IF @ArchivoOrigen <> ''`, y con NULL esa
        // comparacion es NULL y el filtro no aplica — que es lo mismo que "",
        // pero por un camino que no esta escrito en ninguna parte. Legacy mandaba
        // null porque su DTO no inicializaba el campo.
        cmd.Parameters.AddWithValue("@ArchivoOrigen", archivoOrigen);

        var dt = new DataTable();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            dt.Load(reader);
        }

        return Funciones.DataTableToList<ActualizacionAplicacionDto>(dt);
    }
}
