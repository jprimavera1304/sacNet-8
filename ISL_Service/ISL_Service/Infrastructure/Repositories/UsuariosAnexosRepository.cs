using System.Data;
using ISL_Service.Application.DTOs.UsuariosAnexos;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// Las tres lecturas sueltas de la pantalla de usuarios de Mac31.
///
/// `sp_n_ConsultaUsuarioFormaProcesos` es el inventario completo de botones de
/// Mac31 con el acceso de un usuario, y es la MISMA informacion que lee
/// PermissionService para derivar los permisos de ventas, empleados, prestamos y
/// reportes. Se publica, pero NO se reimplementa aqui el calculo de permisos:
/// quien manda sobre eso sigue siendo PermissionService, y tener dos lugares que
/// deciden lo mismo es como no tener ninguno.
///
/// El SP ajusta su filtro con tres constantes de la empresa (`EsAlcaldiaMC`,
/// `AviacionIDCaja`, `EsAviacionServer`), asi que la misma llamada devuelve
/// distinto numero de formas segun la base. Eso se deja tal cual: es la
/// configuracion de la empresa, no un parametro.
/// </summary>
public class UsuariosAnexosRepository : IUsuariosAnexosRepository
{
    private const string SpFormasProcesos = "dbo.sp_n_ConsultaUsuarioFormaProcesos";
    private const string SpFechas = "dbo.sp_n_ConsultaUsuariosFechas";
    private const string SpSiguienteNumero = "dbo.sp_n_ConsultaUsuarioNextID";

    private readonly IConfiguration _configuration;

    public UsuariosAnexosRepository(IConfiguration configuration)
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

    private async Task<DataTable> EjecutarAsync(string sp, Action<SqlCommand> parametros, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(sp, conn) { CommandType = CommandType.StoredProcedure };
        parametros(cmd);

        var dt = new DataTable();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        dt.Load(reader);
        return dt;
    }

    public async Task<List<UsuarioFormaProcesoDto>> ConsultarFormasProcesosAsync(int idUsuario, CancellationToken ct)
    {
        var dt = await EjecutarAsync(SpFormasProcesos, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
        }, ct);

        return Funciones.DataTableToList<UsuarioFormaProcesoDto>(dt);
    }

    public async Task<List<UsuarioFechaDto>> ConsultarFechasAsync(int idUsuario, CancellationToken ct)
    {
        var dt = await EjecutarAsync(SpFechas, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
        }, ct);

        return Funciones.DataTableToList<UsuarioFechaDto>(dt);
    }

    public async Task<int> ConsultarSiguienteNumeroAsync(CancellationToken ct)
    {
        var dt = await EjecutarAsync(SpSiguienteNumero, cmd =>
        {
            // @IDUsuario SE DECLARA Y NUNCA SE USA en el SP: el siguiente numero
            // es MAX(NumUsuario)+1 de toda la tabla. Se manda 0 para completar la
            // firma; el endpoint no lo pide, porque pedirlo haria creer que el
            // resultado depende de el.
            cmd.Parameters.AddWithValue("@IDUsuario", 0);
        }, ct);

        if (dt.Rows.Count == 0) return 0;
        var valor = dt.Rows[0]["NumUsuario"];
        return valor == DBNull.Value ? 0 : Convert.ToInt32(valor);
    }
}
