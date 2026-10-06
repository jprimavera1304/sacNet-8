using System.Data;
using ISL_Service.Application.DTOs.ProveedoresAnexos;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// Apoderados, domicilios y documentos del proveedor.
///
/// LOS TRES RECIBEN @Formato Y NINGUNO LO USA. Se manda 0 porque es lo que
/// mandaba legacy y porque un dia alguien puede implementarlo; no se expone,
/// porque hoy no cambia nada.
///
/// LOS TRES BUSCAN POR PREFIJO, no por contenido: su filtro de texto es
/// LIKE 'texto%' sin el comodin inicial. Es distinto de la mayoria de los
/// catalogos y esta anotado en el controlador para que nadie lo lea como un bug
/// del backend nuevo.
/// </summary>
public class ProveedoresAnexosRepository : IProveedoresAnexosRepository
{
    private const string SpApoderados = "dbo.sp_n_ConsultaProveedorApoderados";
    private const string SpDomicilios = "dbo.sp_n_ConsultaProveedorDomicilios";
    private const string SpDocumentos = "dbo.sp_n_ConsultaProveedorDocumentos";

    private const int FormatoTodosLosCampos = 0;

    private readonly IConfiguration _configuration;

    public ProveedoresAnexosRepository(IConfiguration configuration)
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

    public async Task<List<ProveedorApoderadoDto>> ConsultarApoderadosAsync(
        int id, int idProveedor, int idStatus, string texto,
        int esRepresentanteLegal, int esRepresentanteTecnico, bool exacto, CancellationToken ct)
    {
        var dt = await EjecutarAsync(SpApoderados, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDProveedorApoderado", id);
            cmd.Parameters.AddWithValue("@IDProveedor", idProveedor);
            cmd.Parameters.AddWithValue("@IDStatus", idStatus);
            cmd.Parameters.AddWithValue("@Apoderado", texto);
            // El SP compara `<> 0`: 0 es "no filtres", no "los que no lo son".
            cmd.Parameters.AddWithValue("@EsRepresentanteLegal", esRepresentanteLegal);
            cmd.Parameters.AddWithValue("@EsRepresentanteTecnico", esRepresentanteTecnico);
            cmd.Parameters.AddWithValue("@Formato", FormatoTodosLosCampos);
            cmd.Parameters.AddWithValue("@Identico", exacto ? 1 : 0);
        }, ct);

        return Funciones.DataTableToList<ProveedorApoderadoDto>(dt);
    }

    public async Task<List<ProveedorDomicilioDto>> ConsultarDomiciliosAsync(
        int id, int idProveedor, int idTipoDomicilio, int idStatus, string texto, bool exacto, CancellationToken ct)
    {
        var dt = await EjecutarAsync(SpDomicilios, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDProveedorDomicilio", id);
            cmd.Parameters.AddWithValue("@IDProveedor", idProveedor);
            cmd.Parameters.AddWithValue("@IDTipoDomicilio", idTipoDomicilio);
            cmd.Parameters.AddWithValue("@IDStatus", idStatus);
            cmd.Parameters.AddWithValue("@Domicilio", texto);
            cmd.Parameters.AddWithValue("@Formato", FormatoTodosLosCampos);
            cmd.Parameters.AddWithValue("@Identico", exacto ? 1 : 0);
        }, ct);

        return Funciones.DataTableToList<ProveedorDomicilioDto>(dt);
    }

    public async Task<List<ProveedorDocumentoDto>> ConsultarDocumentosAsync(
        int id, int idProveedor, int idStatus, string texto, bool exacto, CancellationToken ct)
    {
        var dt = await EjecutarAsync(SpDocumentos, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDProveedorDocumento", id);
            cmd.Parameters.AddWithValue("@IDProveedor", idProveedor);
            cmd.Parameters.AddWithValue("@IDStatus", idStatus);
            cmd.Parameters.AddWithValue("@Documento", texto);
            cmd.Parameters.AddWithValue("@Formato", FormatoTodosLosCampos);
            cmd.Parameters.AddWithValue("@Identico", exacto ? 1 : 0);
        }, ct);

        return Funciones.DataTableToList<ProveedorDocumentoDto>(dt);
    }
}
