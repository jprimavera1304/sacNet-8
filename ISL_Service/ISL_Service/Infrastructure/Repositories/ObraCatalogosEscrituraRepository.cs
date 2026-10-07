using System.Data;
using ISL_Service.Application.DTOs.ObraCatalogos;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/*
  ALTAS Y MODIFICACIONES DE LOS CATALOGOS DE OBRA (MC).

  Cada metodo llama al MISMO sp_n_ que llamaba MacServicios2, con los mismos
  parametros y en el mismo orden. Los sp_n_ son de Mac31 y no se tocan: lo que
  se corrige vive aqui.

  TRES COSAS QUE LEGACY HACIA Y AQUI NO:

  1. La firma (@IDUsuario, @Equipo) venia en el cuerpo de la peticion. Ahora
     llega como argumento desde el token, nunca del body.
  2. El apostrofo se borraba de los nombres antes de guardarlos
     (MCPuestoService.cs:63). Estos SP guardan con un INSERT parametrizado, asi
     que el apostrofo no puede inyectar nada: se guarda el nombre como lo
     escribieron. Ver LegacySp.
  3. `Equipo.Replace("'", "")` sobre un Equipo nulo tiraba NullReferenceException
     y el servicio contestaba 400 con el stack trace. Aqui el equipo siempre
     viene del token.

  LO QUE ESTA BASE NO PUEDE PROBAR:
  sp_n_Consultar_MCPartidas, sp_n_Insertar_MCPartida y sp_n_Actualizar_MCPartida
  NO estan instalados en Produccion_svr (comprobado contra sys.procedures), y
  sp_n_Insertar_MCProveedor / sp_n_Actualizar_MCProveedor SI estan pero en una
  version mas vieja que la que espera Legacy: la de esta base declara 10
  parametros (Nombre..Observaciones) y el repositorio de Legacy le manda 28. O
  sea que en esta base esas llamadas fallan igual en los dos lados, con
  "Procedure or function has too many arguments specified". Se implementan contra
  el contrato de Legacy, que es la especificacion, y se dejan anotadas como no
  verificables aqui.
*/
public class ObraCatalogosEscrituraRepository : IObraCatalogosEscrituraRepository
{
    private readonly IConfiguration _configuration;

    private const string SpInsertarTipoContratacion = "dbo.sp_n_Insertar_MCTipoContratacion";
    private const string SpActualizarTipoContratacion = "dbo.sp_n_Actualizar_MCTipoContratacion";
    private const string SpInsertarTipoDocumento = "dbo.sp_n_Insertar_MCTipoDocumento";
    private const string SpActualizarTipoDocumento = "dbo.sp_n_Actualizar_MCTipoDocumento";
    private const string SpInsertarTipoObra = "dbo.sp_n_Insertar_MCTipoObra";
    private const string SpActualizarTipoObra = "dbo.sp_n_Actualizar_MCTipoObra";
    private const string SpInsertarTipoRecurso = "dbo.sp_n_Insertar_MCTipoRecurso";
    private const string SpActualizarTipoRecurso = "dbo.sp_n_Actualizar_MCTipoRecurso";
    private const string SpInsertarPuesto = "dbo.sp_n_Insertar_MCPuesto";
    private const string SpActualizarPuesto = "dbo.sp_n_Actualizar_MCPuesto";
    private const string SpInsertarResidente = "dbo.sp_n_Insertar_MCResidente";
    private const string SpActualizarResidente = "dbo.sp_n_Actualizar_MCResidente";
    private const string SpInsertarGerente = "dbo.sp_n_InsertarMCGerente";
    private const string SpActualizarGerente = "dbo.sp_n_ActualizarMCGerente";
    private const string SpInsertarUnidadMedida = "dbo.sp_n_Insertar_MCUnidadMedida";
    private const string SpActualizarUnidadMedida = "dbo.sp_n_Actualizar_MCUnidadMedida";
    private const string SpConsultarPartidas = "dbo.sp_n_Consultar_MCPartidas";
    private const string SpInsertarPartida = "dbo.sp_n_Insertar_MCPartida";
    private const string SpActualizarPartida = "dbo.sp_n_Actualizar_MCPartida";
    private const string SpInsertarProveedor = "dbo.sp_n_Insertar_MCProveedor";
    private const string SpActualizarProveedor = "dbo.sp_n_Actualizar_MCProveedor";

    public ObraCatalogosEscrituraRepository(IConfiguration configuration)
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

    /// <summary>
    /// Corre un SP de escritura y devuelve su renglon (result, mensaje).
    /// Se usa SqlDataAdapter y no ExecuteNonQuery porque estos SP CONTESTAN con
    /// un SELECT, no con parametros de salida.
    /// </summary>
    private async Task<ResultadoSp> EjecutarAsync(string sp, Action<SqlCommand> parametros, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(sp, conn) { CommandType = CommandType.StoredProcedure };
        parametros(cmd);

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd)) adapter.Fill(dt);
        return LegacySp.LeerResultado(dt);
    }

    private async Task<List<Dictionary<string, object?>>> ConsultarAsync(string sp, Action<SqlCommand> parametros, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(sp, conn) { CommandType = CommandType.StoredProcedure };
        parametros(cmd);

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd)) adapter.Fill(dt);
        return LegacySp.Renglones(dt);
    }

    // ------------------------------------------------------------------
    // Los cuatro catalogos de un solo nombre
    // ------------------------------------------------------------------

    private Task<ResultadoSp> InsertarSimpleAsync(string sp, string parametro, string nombre, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync(sp, cmd =>
        {
            cmd.Parameters.AddWithValue(parametro, LegacySp.Texto(nombre));
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", LegacySp.Texto(equipo));
        }, ct);

    private Task<ResultadoSp> ActualizarSimpleAsync(string sp, string parametroId, int id, string parametro, string nombre, int idStatus, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync(sp, cmd =>
        {
            cmd.Parameters.AddWithValue(parametroId, id);
            cmd.Parameters.AddWithValue("@IDStatus", idStatus);
            cmd.Parameters.AddWithValue(parametro, LegacySp.Texto(nombre));
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", LegacySp.Texto(equipo));
        }, ct);

    public Task<ResultadoSp> InsertarTipoContratacionAsync(string nombre, int idUsuario, string equipo, CancellationToken ct = default)
        => InsertarSimpleAsync(SpInsertarTipoContratacion, "@TipoContratacion", nombre, idUsuario, equipo, ct);

    public Task<ResultadoSp> ActualizarTipoContratacionAsync(int id, string nombre, int idStatus, int idUsuario, string equipo, CancellationToken ct = default)
        => ActualizarSimpleAsync(SpActualizarTipoContratacion, "@IDTipoContratacion", id, "@TipoContratacion", nombre, idStatus, idUsuario, equipo, ct);

    public Task<ResultadoSp> InsertarTipoDocumentoAsync(string nombre, int idUsuario, string equipo, CancellationToken ct = default)
        => InsertarSimpleAsync(SpInsertarTipoDocumento, "@TipoDocumento", nombre, idUsuario, equipo, ct);

    public Task<ResultadoSp> ActualizarTipoDocumentoAsync(int id, string nombre, int idStatus, int idUsuario, string equipo, CancellationToken ct = default)
        => ActualizarSimpleAsync(SpActualizarTipoDocumento, "@IDTipoDocumento", id, "@TipoDocumento", nombre, idStatus, idUsuario, equipo, ct);

    public Task<ResultadoSp> InsertarTipoObraAsync(string nombre, int idUsuario, string equipo, CancellationToken ct = default)
        => InsertarSimpleAsync(SpInsertarTipoObra, "@TipoObra", nombre, idUsuario, equipo, ct);

    public Task<ResultadoSp> ActualizarTipoObraAsync(int id, string nombre, int idStatus, int idUsuario, string equipo, CancellationToken ct = default)
        => ActualizarSimpleAsync(SpActualizarTipoObra, "@IDTipoObra", id, "@TipoObra", nombre, idStatus, idUsuario, equipo, ct);

    public Task<ResultadoSp> InsertarTipoRecursoAsync(string nombre, int idUsuario, string equipo, CancellationToken ct = default)
        => InsertarSimpleAsync(SpInsertarTipoRecurso, "@TipoRecurso", nombre, idUsuario, equipo, ct);

    public Task<ResultadoSp> ActualizarTipoRecursoAsync(int id, string nombre, int idStatus, int idUsuario, string equipo, CancellationToken ct = default)
        => ActualizarSimpleAsync(SpActualizarTipoRecurso, "@IDTipoRecurso", id, "@TipoRecurso", nombre, idStatus, idUsuario, equipo, ct);

    // ------------------------------------------------------------------
    // Puestos
    // ------------------------------------------------------------------

    public Task<ResultadoSp> InsertarPuestoAsync(CrearObraPuestoRequest request, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpInsertarPuesto, cmd =>
        {
            cmd.Parameters.AddWithValue("@Puesto", LegacySp.Texto(request.Puesto));
            cmd.Parameters.AddWithValue("@EsRepresentanteLegal", request.EsRepresentanteLegal ? 1 : 0);
            cmd.Parameters.AddWithValue("@EsRepresentanteTecnico", request.EsRepresentanteTecnico ? 1 : 0);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", LegacySp.Texto(equipo));
        }, ct);

    public Task<ResultadoSp> ActualizarPuestoAsync(int id, ActualizarObraPuestoRequest request, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpActualizarPuesto, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDPuesto", id);
            cmd.Parameters.AddWithValue("@IDStatus", request.IdStatus);
            cmd.Parameters.AddWithValue("@Puesto", LegacySp.Texto(request.Puesto));
            cmd.Parameters.AddWithValue("@EsRepresentanteLegal", request.EsRepresentanteLegal ? 1 : 0);
            cmd.Parameters.AddWithValue("@EsRepresentanteTecnico", request.EsRepresentanteTecnico ? 1 : 0);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", LegacySp.Texto(equipo));
        }, ct);

    // ------------------------------------------------------------------
    // Residentes
    // ------------------------------------------------------------------

    public Task<ResultadoSp> InsertarResidenteAsync(CrearObraResidenteRequest request, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpInsertarResidente, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDPuesto", request.IdPuesto);
            cmd.Parameters.AddWithValue("@Residente", LegacySp.Texto(request.Residente));
            cmd.Parameters.AddWithValue("@EsSupervision", request.EsSupervision ? 1 : 0);
            cmd.Parameters.AddWithValue("@EsObra", request.EsObra ? 1 : 0);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", LegacySp.Texto(equipo));
        }, ct);

    public Task<ResultadoSp> ActualizarResidenteAsync(int id, ActualizarObraResidenteRequest request, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpActualizarResidente, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDResidente", id);
            cmd.Parameters.AddWithValue("@IDPuesto", request.IdPuesto);
            cmd.Parameters.AddWithValue("@IDStatus", request.IdStatus);
            cmd.Parameters.AddWithValue("@Residente", LegacySp.Texto(request.Residente));
            cmd.Parameters.AddWithValue("@EsSupervision", request.EsSupervision ? 1 : 0);
            cmd.Parameters.AddWithValue("@EsObra", request.EsObra ? 1 : 0);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", LegacySp.Texto(equipo));
        }, ct);

    // ------------------------------------------------------------------
    // Gerentes
    // ------------------------------------------------------------------

    public Task<ResultadoSp> InsertarGerenteAsync(CrearObraGerenteRequest request, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpInsertarGerente, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDTipoGerente", request.IdTipoGerente);
            cmd.Parameters.AddWithValue("@Gerente", LegacySp.Texto(request.Gerente));
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", LegacySp.Texto(equipo));
        }, ct);

    public Task<ResultadoSp> ActualizarGerenteAsync(int id, ActualizarObraGerenteRequest request, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpActualizarGerente, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDGerente", id);
            cmd.Parameters.AddWithValue("@IDTipoGerente", request.IdTipoGerente);
            cmd.Parameters.AddWithValue("@IDStatus", request.IdStatus);
            cmd.Parameters.AddWithValue("@Gerente", LegacySp.Texto(request.Gerente));
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", LegacySp.Texto(equipo));
        }, ct);

    // ------------------------------------------------------------------
    // Unidades de medida
    // ------------------------------------------------------------------

    public Task<ResultadoSp> InsertarUnidadMedidaAsync(CrearObraUnidadMedidaRequest request, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpInsertarUnidadMedida, cmd =>
        {
            cmd.Parameters.AddWithValue("@UnidadMedida", LegacySp.Texto(request.UnidadMedida));
            cmd.Parameters.AddWithValue("@SoloUnidades", request.SoloUnidades ? 1 : 0);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", LegacySp.Texto(equipo));
        }, ct);

    public Task<ResultadoSp> ActualizarUnidadMedidaAsync(int id, ActualizarObraUnidadMedidaRequest request, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpActualizarUnidadMedida, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDUnidadMedida", id);
            cmd.Parameters.AddWithValue("@IDStatus", request.IdStatus);
            cmd.Parameters.AddWithValue("@UnidadMedida", LegacySp.Texto(request.UnidadMedida));
            cmd.Parameters.AddWithValue("@SoloUnidades", request.SoloUnidades ? 1 : 0);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", LegacySp.Texto(equipo));
        }, ct);

    // ------------------------------------------------------------------
    // Partidas presupuestales
    // ------------------------------------------------------------------

    public Task<List<Dictionary<string, object?>>> ConsultarPartidasAsync(ObraPartidasFiltro filtro, CancellationToken ct = default)
        => ConsultarAsync(SpConsultarPartidas, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDPartida", filtro.IdPartida);
            cmd.Parameters.AddWithValue("@IDStatus", filtro.IdStatus);
            cmd.Parameters.AddWithValue("@Partida", LegacySp.Filtro(filtro.Partida));
            // @Formato = 0 siempre: el 1 es la version "para Excel" del MISMO SP,
            // con otras columnas y encabezados en espanol con espacios. El Excel
            // lo arma el front con estos mismos datos.
            cmd.Parameters.AddWithValue("@Formato", 0);
            cmd.Parameters.AddWithValue("@Identico", filtro.Exacto ? 1 : 0);
        }, ct);

    private static void ParametrosDePartida(SqlCommand cmd, CrearObraPartidaRequest r)
    {
        cmd.Parameters.AddWithValue("@Partida", LegacySp.Texto(r.Partida));
        cmd.Parameters.AddWithValue("@Descripcion", LegacySp.Texto(r.Descripcion));
        cmd.Parameters.AddWithValue("@Monto", r.Monto);
        cmd.Parameters.AddWithValue("@PartidaFI", LegacySp.Texto(r.PartidaFi));
        cmd.Parameters.AddWithValue("@PartidaF", LegacySp.Texto(r.PartidaF));
        cmd.Parameters.AddWithValue("@PartidaSF", LegacySp.Texto(r.PartidaSf));
        cmd.Parameters.AddWithValue("@PartidaAI", LegacySp.Texto(r.PartidaAi));
        cmd.Parameters.AddWithValue("@PartidaPP", LegacySp.Texto(r.PartidaPp));
        cmd.Parameters.AddWithValue("@PartidaFF", LegacySp.Texto(r.PartidaFf));
        cmd.Parameters.AddWithValue("@PartidaFG", LegacySp.Texto(r.PartidaFg));
        cmd.Parameters.AddWithValue("@PartidaFE", LegacySp.Texto(r.PartidaFe));
        cmd.Parameters.AddWithValue("@PartidaAD", LegacySp.Texto(r.PartidaAd));
        cmd.Parameters.AddWithValue("@PartidaOR", LegacySp.Texto(r.PartidaOr));
        cmd.Parameters.AddWithValue("@PartidaPTDA", LegacySp.Texto(r.PartidaPtda));
        cmd.Parameters.AddWithValue("@PartidaTG", LegacySp.Texto(r.PartidaTg));
        cmd.Parameters.AddWithValue("@PartidaDI", LegacySp.Texto(r.PartidaDi));
        cmd.Parameters.AddWithValue("@PartidaDG", LegacySp.Texto(r.PartidaDg));
        cmd.Parameters.AddWithValue("@PartidaPY", LegacySp.Texto(r.PartidaPy));
    }

    public Task<ResultadoSp> InsertarPartidaAsync(CrearObraPartidaRequest request, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpInsertarPartida, cmd =>
        {
            ParametrosDePartida(cmd, request);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", LegacySp.Texto(equipo));
        }, ct);

    public Task<ResultadoSp> ActualizarPartidaAsync(int id, ActualizarObraPartidaRequest request, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpActualizarPartida, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDPartida", id);
            cmd.Parameters.AddWithValue("@IDStatus", request.IdStatus);
            ParametrosDePartida(cmd, request);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", LegacySp.Texto(equipo));
        }, ct);

    // ------------------------------------------------------------------
    // Padron de proveedores de obra
    // ------------------------------------------------------------------

    private static void ParametrosDeProveedor(SqlCommand cmd, CrearObraProveedorRequest r)
    {
        cmd.Parameters.AddWithValue("@Nombre", LegacySp.Texto(r.Nombre));
        cmd.Parameters.AddWithValue("@ApellidoPaterno", LegacySp.Texto(r.ApellidoPaterno));
        cmd.Parameters.AddWithValue("@ApellidoMaterno", LegacySp.Texto(r.ApellidoMaterno));
        cmd.Parameters.AddWithValue("@RFC", LegacySp.Texto(r.Rfc));
        cmd.Parameters.AddWithValue("@CURP", LegacySp.Texto(r.Curp));
        cmd.Parameters.AddWithValue("@Telefono", LegacySp.Texto(r.Telefono));
        cmd.Parameters.AddWithValue("@Celular", LegacySp.Texto(r.Celular));
        cmd.Parameters.AddWithValue("@Email", LegacySp.Texto(r.Email));
        cmd.Parameters.AddWithValue("@PaginaWeb", LegacySp.Texto(r.PaginaWeb));
        cmd.Parameters.AddWithValue("@Observaciones", LegacySp.Texto(r.Observaciones));
        cmd.Parameters.AddWithValue("@NumeroRegistro", LegacySp.Texto(r.NumeroRegistro));
        cmd.Parameters.AddWithValue("@NumeroRegistro2", LegacySp.Texto(r.NumeroRegistro2));
        cmd.Parameters.AddWithValue("@ActaConstitutivaEscritura", LegacySp.Texto(r.ActaConstitutivaEscritura));
        cmd.Parameters.AddWithValue("@ActaConstitutivaFecha", LegacySp.FechaODbNull(r.ActaConstitutivaFecha));
        cmd.Parameters.AddWithValue("@ActaConstitutivaNotaria", LegacySp.Texto(r.ActaConstitutivaNotaria));
        cmd.Parameters.AddWithValue("@ActaConstitutivaModificaciones", LegacySp.Texto(r.ActaConstitutivaModificaciones));
        cmd.Parameters.AddWithValue("@IMSS", LegacySp.Texto(r.Imss));
        cmd.Parameters.AddWithValue("@Infonavit", LegacySp.Texto(r.Infonavit));
        cmd.Parameters.AddWithValue("@CapitalContable", r.CapitalContable);
        cmd.Parameters.AddWithValue("@Especialidades", LegacySp.Texto(r.Especialidades));
        cmd.Parameters.AddWithValue("@EspecialidadesOtras", LegacySp.Texto(r.EspecialidadesOtras));
        cmd.Parameters.AddWithValue("@FechaInscripcion", LegacySp.FechaODbNull(r.FechaInscripcion));
        cmd.Parameters.AddWithValue("@ApoderadoNombre", LegacySp.Texto(r.ApoderadoNombre));
        cmd.Parameters.AddWithValue("@ApoderadoEscritura", LegacySp.Texto(r.ApoderadoEscritura));
        cmd.Parameters.AddWithValue("@ApoderadoFecha", LegacySp.FechaODbNull(r.ApoderadoFecha));
        cmd.Parameters.AddWithValue("@ApoderadoNotaria", LegacySp.Texto(r.ApoderadoNotaria));
        cmd.Parameters.AddWithValue("@CadenaOriginal", LegacySp.Texto(r.CadenaOriginal));
        cmd.Parameters.AddWithValue("@SelloDigital", LegacySp.Texto(r.SelloDigital));
    }

    public Task<ResultadoSp> InsertarProveedorAsync(CrearObraProveedorRequest request, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpInsertarProveedor, cmd =>
        {
            ParametrosDeProveedor(cmd, request);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", LegacySp.Texto(equipo));
        }, ct);

    public Task<ResultadoSp> ActualizarProveedorAsync(int id, ActualizarObraProveedorRequest request, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpActualizarProveedor, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDProveedor", id);
            cmd.Parameters.AddWithValue("@IDStatus", request.IdStatus);
            ParametrosDeProveedor(cmd, request);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", LegacySp.Texto(equipo));
        }, ct);
}
