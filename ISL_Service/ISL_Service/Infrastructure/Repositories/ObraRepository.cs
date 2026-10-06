using System.Data;
using ISL_Service.Application.DTOs.Obra;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// Los `sp_n_Consultar_MC*` de obra, tal cual los llama Mac31.
///
/// POR QUE SE MANDAN TODOS LOS PARAMETROS SIEMPRE, INCLUIDOS LOS CEROS
/// -------------------------------------------------------------------
/// Los SP declaran defaults que NO son cero: `sp_n_Consultar_MCGerentes` nace
/// con @IDStatus = 1 y `sp_n_Consultar_MCProveedores` tambien. Legacy nunca ve
/// esos defaults, porque su InputDTO es un `int` que nace en 0 y lo pasa
/// explicito (MCGerenteRepository.cs:57). O sea: en Mac31, "no pedi status"
/// significa 0 = TODOS, no 1 = activos. Omitir el parametro aqui cambiaria el
/// resultado de todas las pantallas. Se manda explicito, igual que legacy.
///
/// EL APOSTROFO SI IMPORTA Y EL PARAMETRO NO PROTEGE
/// -------------------------------------------------
/// Todos estos SP arman el WHERE concatenando y lo corren con EXEC(@Sql):
///   SET @Sql = @Sql + ' AND G.Gerente LIKE ''%' + @Gerente + '%'''
/// Pasar el texto como SqlParameter NO evita nada, porque la inyeccion ocurre
/// DENTRO del procedimiento. Por eso el apostrofo se quita en el service
/// (ObraService.Texto) antes de llegar aqui. Legacy lo quitaba solo en las
/// ramas de insert/update y se le olvidaba en las de consulta.
/// </summary>
public class ObraRepository : IObraRepository
{
    private const string SpGerentes = "dbo.sp_n_Consultar_MCGerentes";
    private const string SpPuestos = "dbo.sp_n_Consultar_MCPuestos";
    private const string SpResidentes = "dbo.sp_n_Consultar_MCResidentes";
    private const string SpTiposContratacion = "dbo.sp_n_Consultar_MCTiposContratacion";
    private const string SpTiposDocumento = "dbo.sp_n_Consultar_MCTiposDocumento";
    private const string SpTiposObra = "dbo.sp_n_Consultar_MCTiposObra";
    private const string SpTiposRecurso = "dbo.sp_n_Consultar_MCTiposRecurso";
    private const string SpUnidadesMedida = "dbo.sp_n_Consultar_MCUnidadesMedida";
    private const string SpProveedores = "dbo.sp_n_Consultar_MCProveedores";
    private const string SpContratoGrupos = "dbo.sp_n_Consultar_MCContratosGrupos";
    private const string SpContratoConceptos = "dbo.sp_n_Consultar_MCContratosConceptos";
    private const string SpContratoEstimaciones = "dbo.sp_n_Consultar_MCContratosEstimaciones";

    /// <summary>
    /// 0 = todos los campos. El 1 es "Excel": el MISMO SP devuelve otras
    /// columnas con los encabezados en espanol. No se expone (ver ObraDtos).
    /// </summary>
    private const int FormatoTodosLosCampos = 0;

    private readonly IConfiguration _configuration;

    public ObraRepository(IConfiguration configuration)
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

    /// <summary>
    /// El trio @Formato / @Identico y el texto, que todos estos SP comparten.
    /// </summary>
    private static void AgregarComunes(SqlCommand cmd, ObraCatalogoFiltro filtro)
    {
        cmd.Parameters.AddWithValue("@Formato", FormatoTodosLosCampos);
        cmd.Parameters.AddWithValue("@Identico", filtro.Exacto ? 1 : 0);
    }

    public async Task<List<ObraGerenteDto>> ConsultarGerentesAsync(ObraCatalogoFiltro filtro, int idTipoGerente, CancellationToken ct)
    {
        var dt = await EjecutarAsync(SpGerentes, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDGerente", filtro.Id);
            cmd.Parameters.AddWithValue("@IDTipoGerente", idTipoGerente);
            cmd.Parameters.AddWithValue("@IDStatus", filtro.IDStatus);
            cmd.Parameters.AddWithValue("@Gerente", filtro.Texto ?? string.Empty);
            AgregarComunes(cmd, filtro);
        }, ct);

        return Funciones.DataTableToList<ObraGerenteDto>(dt);
    }

    public async Task<List<ObraPuestoDto>> ConsultarPuestosAsync(
        ObraCatalogoFiltro filtro, int esRepresentanteLegal, int esRepresentanteTecnico, CancellationToken ct)
    {
        var dt = await EjecutarAsync(SpPuestos, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDPuesto", filtro.Id);
            cmd.Parameters.AddWithValue("@IDStatus", filtro.IDStatus);
            cmd.Parameters.AddWithValue("@Puesto", filtro.Texto ?? string.Empty);
            // El SP filtra con `<> 0`, asi que 0 es "no me importa" y NO "los que
            // valen 0". Pedir los que NO son representante legal no se puede, ni
            // en Mac31.
            cmd.Parameters.AddWithValue("@EsRepresentanteLegal", esRepresentanteLegal);
            cmd.Parameters.AddWithValue("@EsRepresentanteTecnico", esRepresentanteTecnico);
            AgregarComunes(cmd, filtro);
        }, ct);

        return Funciones.DataTableToList<ObraPuestoDto>(dt);
    }

    public async Task<List<ObraResidenteDto>> ConsultarResidentesAsync(
        ObraCatalogoFiltro filtro, int idPuesto, int esSupervision, int esObra, CancellationToken ct)
    {
        var dt = await EjecutarAsync(SpResidentes, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDResidente", filtro.Id);
            cmd.Parameters.AddWithValue("@IDPuesto", idPuesto);
            cmd.Parameters.AddWithValue("@IDStatus", filtro.IDStatus);
            cmd.Parameters.AddWithValue("@Residente", filtro.Texto ?? string.Empty);
            // Igual que en puestos: 0 = sin filtrar.
            cmd.Parameters.AddWithValue("@EsSupervision", esSupervision);
            cmd.Parameters.AddWithValue("@EsObra", esObra);
            AgregarComunes(cmd, filtro);
        }, ct);

        return Funciones.DataTableToList<ObraResidenteDto>(dt);
    }

    public async Task<List<ObraUnidadMedidaDto>> ConsultarUnidadesMedidaAsync(ObraCatalogoFiltro filtro, CancellationToken ct)
    {
        var dt = await EjecutarAsync(SpUnidadesMedida, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDUnidadMedida", filtro.Id);
            cmd.Parameters.AddWithValue("@IDStatus", filtro.IDStatus);
            cmd.Parameters.AddWithValue("@UnidadMedida", filtro.Texto ?? string.Empty);
            AgregarComunes(cmd, filtro);
        }, ct);

        return Funciones.DataTableToList<ObraUnidadMedidaDto>(dt);
    }

    public async Task<List<ObraTipoDto>> ConsultarTipoAsync(ObraTipoCatalogo tipo, ObraCatalogoFiltro filtro, CancellationToken ct)
    {
        var (sp, paramId, paramTexto, colId, colNombre) = tipo switch
        {
            ObraTipoCatalogo.TiposContratacion =>
                (SpTiposContratacion, "@IDTipoContratacion", "@TipoContratacion", "IDTipoContratacion", "TipoContratacion"),
            ObraTipoCatalogo.TiposDocumento =>
                (SpTiposDocumento, "@IDTipoDocumento", "@TipoDocumento", "IDTipoDocumento", "TipoDocumento"),
            ObraTipoCatalogo.TiposObra =>
                (SpTiposObra, "@IDTipoObra", "@TipoObra", "IDTipoObra", "TipoObra"),
            ObraTipoCatalogo.TiposRecurso =>
                (SpTiposRecurso, "@IDTipoRecurso", "@TipoRecurso", "IDTipoRecurso", "TipoRecurso"),
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };

        var dt = await EjecutarAsync(sp, cmd =>
        {
            cmd.Parameters.AddWithValue(paramId, filtro.Id);
            cmd.Parameters.AddWithValue("@IDStatus", filtro.IDStatus);
            cmd.Parameters.AddWithValue(paramTexto, filtro.Texto ?? string.Empty);
            AgregarComunes(cmd, filtro);
        }, ct);

        // Mapeo a mano porque la columna del id y la del nombre cambian con el
        // catalogo y el contrato publicado no: DataTableToList empareja por
        // nombre exacto y aqui los nombres no coinciden a proposito.
        var lista = new List<ObraTipoDto>(dt.Rows.Count);
        foreach (DataRow row in dt.Rows)
        {
            lista.Add(new ObraTipoDto
            {
                Id = LeerInt(row, colId),
                IDStatus = LeerInt(row, "IDStatus"),
                Nombre = LeerTexto(row, colNombre),
                Estatus = LeerTexto(row, "Estatus")
            });
        }
        return lista;
    }

    public async Task<List<ObraProveedorDto>> ConsultarProveedoresAsync(ObraProveedorFiltro filtro, CancellationToken ct)
    {
        var dt = await EjecutarAsync(SpProveedores, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDProveedor", filtro.IDProveedor);
            cmd.Parameters.AddWithValue("@IDStatus", filtro.IDStatus);
            cmd.Parameters.AddWithValue("@Numero", filtro.Numero ?? string.Empty);
            cmd.Parameters.AddWithValue("@Nombre", filtro.Nombre ?? string.Empty);
            cmd.Parameters.AddWithValue("@ApellidoPaterno", filtro.ApellidoPaterno ?? string.Empty);
            cmd.Parameters.AddWithValue("@ApellidoMaterno", filtro.ApellidoMaterno ?? string.Empty);
            cmd.Parameters.AddWithValue("@RFC", filtro.Rfc ?? string.Empty);
            cmd.Parameters.AddWithValue("@Formato", FormatoTodosLosCampos);
            cmd.Parameters.AddWithValue("@Identico", filtro.Exacto ? 1 : 0);
        }, ct);

        return Funciones.DataTableToList<ObraProveedorDto>(dt);
    }

    public async Task<List<ObraContratoGrupoDto>> ConsultarContratoGruposAsync(
        int idContratoGrupo, int idContrato, ObraCatalogoFiltro filtro, CancellationToken ct)
    {
        var dt = await EjecutarAsync(SpContratoGrupos, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDContratoGrupo", idContratoGrupo);
            cmd.Parameters.AddWithValue("@IDContrato", idContrato);
            cmd.Parameters.AddWithValue("@IDStatus", filtro.IDStatus);
            cmd.Parameters.AddWithValue("@Grupo", filtro.Texto ?? string.Empty);
            AgregarComunes(cmd, filtro);
        }, ct);

        return Funciones.DataTableToList<ObraContratoGrupoDto>(dt);
    }

    public async Task<List<ObraContratoConceptoDto>> ConsultarContratoConceptosAsync(
        int idContratoConcepto, int idContratoGrupo, int idContrato, ObraCatalogoFiltro filtro, CancellationToken ct)
    {
        var dt = await EjecutarAsync(SpContratoConceptos, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDContratoConcepto", idContratoConcepto);
            cmd.Parameters.AddWithValue("@IDContratoGrupo", idContratoGrupo);
            cmd.Parameters.AddWithValue("@IDContrato", idContrato);
            cmd.Parameters.AddWithValue("@IDStatus", filtro.IDStatus);
            cmd.Parameters.AddWithValue("@Concepto", filtro.Texto ?? string.Empty);
            AgregarComunes(cmd, filtro);
        }, ct);

        return Funciones.DataTableToList<ObraContratoConceptoDto>(dt);
    }

    public async Task<List<ObraContratoEstimacionDto>> ConsultarContratoEstimacionesAsync(
        int idContratoEstimacion, int idContrato, ObraCatalogoFiltro filtro, CancellationToken ct)
    {
        var dt = await EjecutarAsync(SpContratoEstimaciones, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDContratoEstimacion", idContratoEstimacion);
            cmd.Parameters.AddWithValue("@IDContrato", idContrato);
            cmd.Parameters.AddWithValue("@IDStatus", filtro.IDStatus);
            cmd.Parameters.AddWithValue("@Nombre", filtro.Texto ?? string.Empty);
            AgregarComunes(cmd, filtro);
        }, ct);

        return Funciones.DataTableToList<ObraContratoEstimacionDto>(dt);
    }

    private static int LeerInt(DataRow row, string columna)
    {
        if (!row.Table.Columns.Contains(columna) || row[columna] == DBNull.Value) return 0;
        return Convert.ToInt32(row[columna]);
    }

    private static string LeerTexto(DataRow row, string columna)
    {
        if (!row.Table.Columns.Contains(columna) || row[columna] == DBNull.Value) return string.Empty;
        // Varias columnas son char(n) y traen el relleno adentro. El relleno es
        // del tipo de la columna, no un dato.
        return Convert.ToString(row[columna])?.Trim() ?? string.Empty;
    }
}
