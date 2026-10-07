using System.Data;
using ISL_Service.Application.DTOs.ObraContratos;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/*
  CONTRATOS DE OBRA (MC) Y SUS CINCO ARBOLES, CONTRA LOS sp_n_ DE MAC31.

  QUE DE ESTO SE PUEDE COMPROBAR EN Produccion_svr, LEIDO DE sys.procedures Y
  sys.parameters (no supuesto):

  ESTAN Y CON LA MISMA FIRMA QUE ESPERA LEGACY
    sp_n_Consultar_MCContratos            (14 parametros)
    sp_n_Consultar_MCContratosConceptos   (7)
    sp_n_Insertar_MCContratoGrupo         (5)
    sp_n_Actualizar_MCContratoGrupo       (8)
    sp_n_Actualizar_MCContratoStatus      (4)

  ESTAN, PERO LA BASE TIENE UNA VERSION MAS VIEJA QUE LA QUE LEGACY LLAMA
    sp_n_Insertar_MCContrato              le faltan @IDProveedorApoderado y @PartidasIndividuales
    sp_n_Actualizar_MCContrato            igual
    sp_n_Insertar_MCContratoConcepto      le faltan @IDContratoPartida, @Ajuste, @ConceptoGlobal
    sp_n_Actualizar_MCContratoConcepto    igual
    sp_n_Insertar_MCContratoConceptoFile  le faltan @Limpiar, @CrearGrupo, @Ajuste, @ConceptoGlobal, @PartidaPresupuestal
    sp_n_Actualizar_MCContratoConceptoEstimacion  le faltan @IDContratoPartida, @CantidadesExcedentes, @PorcentajesExcedentes, @Ajustes
    sp_n_Insertar_MCContratoEstimacion    le falta @PorcentajeAvanceFisicoReal
    sp_n_Actualizar_MCContratoEstimacion  igual

  NO ESTAN EN ESTA BASE
    sp_n_Consultar_MCConvenios, sp_n_Insertar_MCConvenio,
    sp_n_Actualizar_MCConvenio, sp_n_Actualizar_MCConvenioStatus,
    sp_n_Consultar_MCContratosPartidas, sp_n_Insertar_MCContratoPartida,
    sp_n_Actualizar_MCContratoPartida,
    sp_n_Consultar_MCContratosPeriodos, sp_n_Insertar_MCContratoPeriodo,
    sp_n_Actualizar_MCContratoPeriodo,
    sp_n_Consultar_MCContratosConceptosEstimacionesAcumulado
    (de este ultimo la base tiene sp_n_Consultar_MCContratosConceptosEstimaciones,
     sin "Acumulado" y con 3 parametros en vez de 4: no es el mismo).

  En los dos ultimos grupos Legacy falla exactamente igual en esta base
  ("Procedure or function has too many arguments specified" o "Could not find
  stored procedure"), asi que no son comparables aqui. Se implementan contra el
  contrato de Legacy, que es la especificacion, y quedan anotados como no
  verificables.

  NO SE INVENTA UN FALLBACK. El backend nuevo tiene un patron de "SP opcional"
  (VentasPedidoCapturaRepository.IsMissingStoredProcedure) para cuando una
  instalacion no tiene un procedimiento; aqui NO se usa, porque estos no son
  opcionales: si el procedimiento de obra no esta, la operacion no se puede
  hacer y hay que decirlo, no seguir como si nada.
*/
public class ObraContratosRepository : IObraContratosRepository
{
    private readonly IConfiguration _configuration;

    private const string SpConsultarContratos = "dbo.sp_n_Consultar_MCContratos";
    private const string SpInsertarContrato = "dbo.sp_n_Insertar_MCContrato";
    private const string SpActualizarContrato = "dbo.sp_n_Actualizar_MCContrato";
    private const string SpActualizarContratoStatus = "dbo.sp_n_Actualizar_MCContratoStatus";

    private const string SpConsultarConvenios = "dbo.sp_n_Consultar_MCConvenios";
    private const string SpInsertarConvenio = "dbo.sp_n_Insertar_MCConvenio";
    private const string SpActualizarConvenio = "dbo.sp_n_Actualizar_MCConvenio";
    private const string SpActualizarConvenioStatus = "dbo.sp_n_Actualizar_MCConvenioStatus";

    private const string SpInsertarGrupo = "dbo.sp_n_Insertar_MCContratoGrupo";
    private const string SpActualizarGrupo = "dbo.sp_n_Actualizar_MCContratoGrupo";

    private const string SpConsultarConceptos = "dbo.sp_n_Consultar_MCContratosConceptos";
    private const string SpConsultarConceptosEstimaciones = "dbo.sp_n_Consultar_MCContratosConceptosEstimacionesAcumulado";
    private const string SpInsertarConcepto = "dbo.sp_n_Insertar_MCContratoConcepto";
    private const string SpActualizarConcepto = "dbo.sp_n_Actualizar_MCContratoConcepto";
    private const string SpInsertarConceptoArchivo = "dbo.sp_n_Insertar_MCContratoConceptoFile";
    private const string SpActualizarConceptoEstimacion = "dbo.sp_n_Actualizar_MCContratoConceptoEstimacion";

    private const string SpConsultarContratoPartidas = "dbo.sp_n_Consultar_MCContratosPartidas";
    private const string SpInsertarContratoPartida = "dbo.sp_n_Insertar_MCContratoPartida";
    private const string SpActualizarContratoPartida = "dbo.sp_n_Actualizar_MCContratoPartida";

    private const string SpConsultarPeriodos = "dbo.sp_n_Consultar_MCContratosPeriodos";
    private const string SpInsertarPeriodo = "dbo.sp_n_Insertar_MCContratoPeriodo";
    private const string SpActualizarPeriodo = "dbo.sp_n_Actualizar_MCContratoPeriodo";

    private const string SpInsertarEstimacion = "dbo.sp_n_Insertar_MCContratoEstimacion";
    private const string SpActualizarEstimacion = "dbo.sp_n_Actualizar_MCContratoEstimacion";

    public ObraContratosRepository(IConfiguration configuration)
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

    private async Task<DataTable> TablaAsync(string sp, Action<SqlCommand> parametros, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(sp, conn) { CommandType = CommandType.StoredProcedure };
        parametros(cmd);

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd)) adapter.Fill(dt);
        return dt;
    }

    private async Task<ResultadoSp> EjecutarAsync(string sp, Action<SqlCommand> parametros, CancellationToken ct)
        => LegacySp.LeerResultado(await TablaAsync(sp, parametros, ct));

    private async Task<List<Dictionary<string, object?>>> ConsultarAsync(string sp, Action<SqlCommand> parametros, CancellationToken ct)
        => LegacySp.Renglones(await TablaAsync(sp, parametros, ct));

    private static void Firma(SqlCommand cmd, int idUsuario, string equipo)
    {
        cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
        cmd.Parameters.AddWithValue("@Equipo", LegacySp.Texto(equipo));
    }

    // ==================================================================
    // El contrato
    // ==================================================================

    public Task<List<Dictionary<string, object?>>> ConsultarContratosAsync(ObraContratosFiltro f, CancellationToken ct = default)
        => ConsultarAsync(SpConsultarContratos, cmd =>
        {
            /*
              AQUI SE DEJA DE HEREDAR UN DEFECTO.

              Legacy, al recibir el numero de contrato, RECONSTRUIA el DTO:

                  if (mcContratoInputDTO.Contrato != "") {
                      string Contrato = mcContratoInputDTO.Contrato;
                      mcContratoInputDTO = new MCContratoInputDTO();   // <-- todo lo demas al suelo
                      mcContratoInputDTO.Contrato = Contrato;
                  }

              (MCContratoService.cs:44-51). O sea que filtrar por numero de
              contrato Y por status devolvia los contratos de cualquier status,
              sin avisar: el usuario veia una lista que no era la que pidio y no
              tenia forma de saberlo. Aqui todos los filtros se aplican juntos.
            */
            cmd.Parameters.AddWithValue("@IDContrato", f.IdContrato);
            cmd.Parameters.AddWithValue("@IDProveedor", f.IdProveedor);
            cmd.Parameters.AddWithValue("@IDContratoSupervisionExterna", f.IdContratoSupervisionExterna);
            cmd.Parameters.AddWithValue("@IDTipoObra", f.IdTipoObra);
            cmd.Parameters.AddWithValue("@IDTipoContratacion", f.IdTipoContratacion);
            cmd.Parameters.AddWithValue("@IDTipoRecurso", f.IdTipoRecurso);
            cmd.Parameters.AddWithValue("@IDResidenteSupervision", f.IdResidenteSupervision);
            cmd.Parameters.AddWithValue("@IDResidenteObra", f.IdResidenteObra);
            cmd.Parameters.AddWithValue("@IDStatus", f.IdStatus);
            cmd.Parameters.AddWithValue("@Contrato", LegacySp.Filtro(f.Contrato));
            cmd.Parameters.AddWithValue("@FechaInicial", LegacySp.DesdeElDia(f.FechaInicial));
            cmd.Parameters.AddWithValue("@FechaFinal", LegacySp.HastaElDia(f.FechaFinal ?? f.FechaInicial));
            cmd.Parameters.AddWithValue("@FechaContratacionInicial", LegacySp.DesdeElDia(f.FechaContratacionInicial));
            cmd.Parameters.AddWithValue("@FechaContratacionFinal", LegacySp.HastaElDia(f.FechaContratacionFinal ?? f.FechaContratacionInicial));
        }, ct);

    private static void ParametrosDeContrato(SqlCommand cmd, CrearObraContratoRequest r)
    {
        cmd.Parameters.AddWithValue("@IDProveedor", r.IdProveedor);
        cmd.Parameters.AddWithValue("@IDProveedorApoderado", r.IdProveedorApoderado);
        cmd.Parameters.AddWithValue("@IDContratoSupervisionExterna", r.IdContratoSupervisionExterna);
        cmd.Parameters.AddWithValue("@IDTipoObra", r.IdTipoObra);
        cmd.Parameters.AddWithValue("@IDTipoContratacion", r.IdTipoContratacion);
        cmd.Parameters.AddWithValue("@IDTipoRecurso", r.IdTipoRecurso);
        cmd.Parameters.AddWithValue("@IDResidenteSupervision", r.IdResidenteSupervision);
        cmd.Parameters.AddWithValue("@IDResidenteObra", r.IdResidenteObra);
        cmd.Parameters.AddWithValue("@Contrato", LegacySp.Texto(r.Contrato));
        cmd.Parameters.AddWithValue("@Descripcion", LegacySp.Texto(r.Descripcion));
        cmd.Parameters.AddWithValue("@Ubicacion", LegacySp.Texto(r.Ubicacion));
        cmd.Parameters.AddWithValue("@FechaContratacion", LegacySp.FechaODbNull(r.FechaContratacion));
        cmd.Parameters.AddWithValue("@FechaInicio", LegacySp.FechaODbNull(r.FechaInicio));
        cmd.Parameters.AddWithValue("@FechaTermino", LegacySp.FechaODbNull(r.FechaTermino));
        cmd.Parameters.AddWithValue("@SubTotal", r.SubTotal);
        cmd.Parameters.AddWithValue("@IVA", r.Iva);
        cmd.Parameters.AddWithValue("@Total", r.Total);
        cmd.Parameters.AddWithValue("@FechaAsignacionRecurso", LegacySp.FechaODbNull(r.FechaAsignacionRecurso));
        cmd.Parameters.AddWithValue("@NumeroOficio", LegacySp.Texto(r.NumeroOficio));
        cmd.Parameters.AddWithValue("@Folio", LegacySp.Texto(r.Folio));
        cmd.Parameters.AddWithValue("@Caso", LegacySp.Texto(r.Caso));
        cmd.Parameters.AddWithValue("@NumeroProcedimiento", LegacySp.Texto(r.NumeroProcedimiento));
        // @Ejercicio es int en el SP. Sin ejercicio va 0, no "" : en Legacy el
        // campo era string y el vacio tiraba la conversion de todo el alta.
        cmd.Parameters.AddWithValue("@Ejercicio", r.Ejercicio ?? 0);
        cmd.Parameters.AddWithValue("@SICV", r.Sicv);
        cmd.Parameters.AddWithValue("@SRCOP", r.Srcop);
        cmd.Parameters.AddWithValue("@LFD", r.Lfd);
        cmd.Parameters.AddWithValue("@PartidasIndividuales", r.PartidasIndividuales ? 1 : 0);
        cmd.Parameters.AddWithValue("@Observaciones", LegacySp.Texto(r.Observaciones));
    }

    public Task<ResultadoSp> InsertarContratoAsync(CrearObraContratoRequest request, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpInsertarContrato, cmd =>
        {
            ParametrosDeContrato(cmd, request);
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<ResultadoSp> ActualizarContratoAsync(int id, ActualizarObraContratoRequest request, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpActualizarContrato, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDContrato", id);
            ParametrosDeContrato(cmd, request);
            cmd.Parameters.AddWithValue("@IDStatus", request.IdStatus);
            Firma(cmd, idUsuario, equipo);
        }, ct);

    /*
      SOLO LA BANDERA DE PARTIDAS INDIVIDUALES.

      Legacy llegaba aqui por la misma ruta de actualizar, con IDStatus = 3 como
      interruptor, y en esa rama le mandaba al SP todos los demas campos con lo
      que trajera el cuerpo: en la practica, si el cliente solo queria cambiar
      la bandera y no reenviaba el contrato entero, el UPDATE le borraba la
      descripcion, la ubicacion, los importes y las fechas.

      Aqui se lee el contrato primero y se reenvia tal como esta, cambiando solo
      la bandera. Son dos viajes a la base y no estan en una transaccion, igual
      que en Legacy; lo que cambia es que no se pierde nada.
    */
    public async Task<ResultadoSp> ActualizarContratoPartidasIndividualesAsync(int id, bool partidasIndividuales, int idUsuario, string equipo, CancellationToken ct = default)
    {
        var actuales = await ConsultarContratosAsync(new ObraContratosFiltro { IdContrato = id }, ct);
        if (actuales.Count == 0)
            throw new Application.Exceptions.NotFoundException($"El contrato {id} no existe.");

        var fila = actuales[0];
        var request = new ActualizarObraContratoRequest
        {
            IdProveedor = Entero(fila, "idProveedor"),
            IdProveedorApoderado = Entero(fila, "idProveedorApoderado"),
            IdContratoSupervisionExterna = Entero(fila, "idContratoSupervisionExterna"),
            IdTipoObra = Entero(fila, "idTipoObra"),
            IdTipoContratacion = Entero(fila, "idTipoContratacion"),
            IdTipoRecurso = Entero(fila, "idTipoRecurso"),
            IdResidenteSupervision = Entero(fila, "idResidenteSupervision"),
            IdResidenteObra = Entero(fila, "idResidenteObra"),
            IdStatus = Entero(fila, "idStatus") is var st && st > 0 ? st : 1,
            Contrato = Cadena(fila, "contrato"),
            Descripcion = Cadena(fila, "descripcion"),
            Ubicacion = Cadena(fila, "ubicacion"),
            FechaContratacion = Fecha(fila, "fechaContratacion"),
            FechaInicio = Fecha(fila, "fechaInicio"),
            FechaTermino = Fecha(fila, "fechaTermino"),
            SubTotal = Numero(fila, "subTotal"),
            Iva = Numero(fila, "iva"),
            Total = Numero(fila, "total"),
            FechaAsignacionRecurso = Fecha(fila, "fechaAsignacionRecurso"),
            NumeroOficio = Cadena(fila, "numeroOficio"),
            Folio = Cadena(fila, "folio"),
            Caso = Cadena(fila, "caso"),
            NumeroProcedimiento = Cadena(fila, "numeroProcedimiento"),
            Ejercicio = Entero(fila, "ejercicio"),
            Sicv = Numero(fila, "sicv"),
            Srcop = Numero(fila, "srcop"),
            Lfd = Numero(fila, "lfd"),
            Observaciones = Cadena(fila, "observaciones"),
            PartidasIndividuales = partidasIndividuales
        };

        return await ActualizarContratoAsync(id, request, idUsuario, equipo, ct);
    }

    public Task<ResultadoSp> ActualizarContratoStatusAsync(int id, bool contratoCerrado, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpActualizarContratoStatus, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDContrato", id);
            cmd.Parameters.AddWithValue("@ContratoCerrado", contratoCerrado ? 1 : 0);
            Firma(cmd, idUsuario, equipo);
        }, ct);

    // ==================================================================
    // Convenios
    // ==================================================================

    public Task<List<Dictionary<string, object?>>> ConsultarConveniosAsync(ObraConveniosFiltro f, CancellationToken ct = default)
        => ConsultarAsync(SpConsultarConvenios, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDConvenio", f.IdConvenio);
            cmd.Parameters.AddWithValue("@IDContrato", f.IdContrato);
            cmd.Parameters.AddWithValue("@IDConvenioMotivo", f.IdConvenioMotivo);
            cmd.Parameters.AddWithValue("@IDProveedor", f.IdProveedor);
            cmd.Parameters.AddWithValue("@IDStatus", f.IdStatus);
            cmd.Parameters.AddWithValue("@Convenio", LegacySp.Filtro(f.Convenio));
            cmd.Parameters.AddWithValue("@Contrato", LegacySp.Filtro(f.Contrato));
            cmd.Parameters.AddWithValue("@FechaInicial", LegacySp.DesdeElDia(f.FechaInicial));
            cmd.Parameters.AddWithValue("@FechaFinal", LegacySp.HastaElDia(f.FechaFinal ?? f.FechaInicial));
        }, ct);

    private static void ParametrosDeConvenio(SqlCommand cmd, CrearObraConvenioRequest r)
    {
        cmd.Parameters.AddWithValue("@IDConvenioMotivo", r.IdConvenioMotivo);
        cmd.Parameters.AddWithValue("@Convenio", LegacySp.Texto(r.Convenio));
        cmd.Parameters.AddWithValue("@Descripcion", LegacySp.Texto(r.Descripcion));
        cmd.Parameters.AddWithValue("@FechaInicio", LegacySp.FechaODbNull(r.FechaInicio));
        cmd.Parameters.AddWithValue("@FechaTermino", LegacySp.FechaODbNull(r.FechaTermino));
        cmd.Parameters.AddWithValue("@SubTotal", r.SubTotal);
        cmd.Parameters.AddWithValue("@IVA", r.Iva);
        cmd.Parameters.AddWithValue("@Total", r.Total);
        cmd.Parameters.AddWithValue("@Observaciones", LegacySp.Texto(r.Observaciones));
    }

    public Task<ResultadoSp> InsertarConvenioAsync(CrearObraConvenioRequest request, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpInsertarConvenio, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDContrato", request.IdContrato);
            ParametrosDeConvenio(cmd, request);
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<ResultadoSp> ActualizarConvenioAsync(int id, ActualizarObraConvenioRequest request, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpActualizarConvenio, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDConvenio", id);
            cmd.Parameters.AddWithValue("@IDConvenioMotivo", request.IdConvenioMotivo);
            cmd.Parameters.AddWithValue("@IDContrato", request.IdContrato);
            cmd.Parameters.AddWithValue("@IDStatus", request.IdStatus);
            cmd.Parameters.AddWithValue("@Convenio", LegacySp.Texto(request.Convenio));
            cmd.Parameters.AddWithValue("@Descripcion", LegacySp.Texto(request.Descripcion));
            cmd.Parameters.AddWithValue("@FechaInicio", LegacySp.FechaODbNull(request.FechaInicio));
            cmd.Parameters.AddWithValue("@FechaTermino", LegacySp.FechaODbNull(request.FechaTermino));
            cmd.Parameters.AddWithValue("@SubTotal", request.SubTotal);
            cmd.Parameters.AddWithValue("@IVA", request.Iva);
            cmd.Parameters.AddWithValue("@Total", request.Total);
            cmd.Parameters.AddWithValue("@Observaciones", LegacySp.Texto(request.Observaciones));
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<ResultadoSp> ActualizarConvenioStatusAsync(int id, bool convenioCerrado, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpActualizarConvenioStatus, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDConvenio", id);
            cmd.Parameters.AddWithValue("@ConvenioCerrado", convenioCerrado ? 1 : 0);
            Firma(cmd, idUsuario, equipo);
        }, ct);

    // ==================================================================
    // Grupos
    // ==================================================================

    public Task<ResultadoSp> InsertarGrupoAsync(CrearObraContratoGrupoRequest request, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpInsertarGrupo, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDContrato", request.IdContrato);
            cmd.Parameters.AddWithValue("@Clave", LegacySp.Texto(request.Clave));
            cmd.Parameters.AddWithValue("@Grupo", LegacySp.Texto(request.Grupo));
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<ResultadoSp> ActualizarGrupoAsync(int id, ActualizarObraContratoGrupoRequest request, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpActualizarGrupo, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDContratoGrupo", id);
            cmd.Parameters.AddWithValue("@IDContrato", request.IdContrato);
            cmd.Parameters.AddWithValue("@IDStatus", request.IdStatus);
            cmd.Parameters.AddWithValue("@Clave", LegacySp.Texto(request.Clave));
            cmd.Parameters.AddWithValue("@Grupo", LegacySp.Texto(request.Grupo));
            // @Orden = 0 es lo que le dice al SP "modifica los campos, no
            // reordenes". Reordenar es la otra ruta.
            cmd.Parameters.AddWithValue("@Orden", 0);
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<ResultadoSp> ReordenarGrupoAsync(int id, int idContrato, int delta, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpActualizarGrupo, cmd =>
        {
            /*
              LA RAMA DE REORDENAR DEL SP NO TOCA NINGUN OTRO CAMPO.

              `IF @Orden <> 0` solo hace dos UPDATE de Orden y sale
              (sp_n_Actualizar_MCContratoGrupo). Por eso Clave, Grupo e IDStatus
              van en blanco y en 0: el SP no los usa en este camino. Lo que SI
              usa es @IDContrato, para encontrar al vecino con el que
              intercambia la posicion.
            */
            cmd.Parameters.AddWithValue("@IDContratoGrupo", id);
            cmd.Parameters.AddWithValue("@IDContrato", idContrato);
            cmd.Parameters.AddWithValue("@IDStatus", 0);
            cmd.Parameters.AddWithValue("@Clave", string.Empty);
            cmd.Parameters.AddWithValue("@Grupo", string.Empty);
            cmd.Parameters.AddWithValue("@Orden", delta);
            Firma(cmd, idUsuario, equipo);
        }, ct);

    // ==================================================================
    // Conceptos
    // ==================================================================

    public Task<List<Dictionary<string, object?>>> ConsultarConceptosAsync(ObraContratoConceptosFiltro f, CancellationToken ct = default)
        => ConsultarAsync(SpConsultarConceptos, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDContratoConcepto", f.IdContratoConcepto);
            cmd.Parameters.AddWithValue("@IDContratoGrupo", f.IdContratoGrupo);
            cmd.Parameters.AddWithValue("@IDContrato", f.IdContrato);
            cmd.Parameters.AddWithValue("@IDStatus", f.IdStatus);
            cmd.Parameters.AddWithValue("@Concepto", LegacySp.Filtro(f.Concepto));
            cmd.Parameters.AddWithValue("@Formato", 0);
            cmd.Parameters.AddWithValue("@Identico", f.Exacto ? 1 : 0);
        }, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarConceptosEstimacionesAsync(ObraConceptosEstimacionesFiltro f, CancellationToken ct = default)
        => ConsultarAsync(SpConsultarConceptosEstimaciones, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDContratoEstimacionDetalle", f.IdContratoEstimacionDetalle);
            cmd.Parameters.AddWithValue("@IDContratoEstimacion", f.IdContratoEstimacion);
            cmd.Parameters.AddWithValue("@IDContrato", f.IdContrato);
            cmd.Parameters.AddWithValue("@IDContratoPartida", f.IdContratoPartida);
        }, ct);

    private static void ParametrosDeConcepto(SqlCommand cmd, CrearObraContratoConceptoRequest r)
    {
        cmd.Parameters.AddWithValue("@IDContratoGrupo", r.IdContratoGrupo);
        cmd.Parameters.AddWithValue("@IDContrato", r.IdContrato);
        cmd.Parameters.AddWithValue("@IDUnidadMedida", r.IdUnidadMedida);
        cmd.Parameters.AddWithValue("@IDContratoPartida", r.IdContratoPartida);
        cmd.Parameters.AddWithValue("@Clave", LegacySp.Texto(r.Clave));
        cmd.Parameters.AddWithValue("@Concepto", LegacySp.Texto(r.Concepto));
        cmd.Parameters.AddWithValue("@PrecioUnitario", r.PrecioUnitario);
        cmd.Parameters.AddWithValue("@Cantidad", r.Cantidad);
        cmd.Parameters.AddWithValue("@Ajuste", r.Ajuste);
        cmd.Parameters.AddWithValue("@ConceptoGlobal", r.ConceptoGlobal ? 1 : 0);
    }

    public Task<ResultadoSp> InsertarConceptoAsync(CrearObraContratoConceptoRequest request, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpInsertarConcepto, cmd =>
        {
            ParametrosDeConcepto(cmd, request);
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<ResultadoSp> ActualizarConceptoAsync(int id, ActualizarObraContratoConceptoRequest request, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpActualizarConcepto, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDContratoConcepto", id);
            ParametrosDeConcepto(cmd, request);
            cmd.Parameters.AddWithValue("@IDStatus", request.IdStatus);
            cmd.Parameters.AddWithValue("@Orden", 0);
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<ResultadoSp> ReordenarConceptoAsync(int id, int idContrato, int delta, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpActualizarConcepto, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDContratoConcepto", id);
            // En la rama de reordenar, el SP se lee el IDContratoGrupo del
            // propio renglon; el que se mande aqui lo pisa.
            cmd.Parameters.AddWithValue("@IDContratoGrupo", 0);
            cmd.Parameters.AddWithValue("@IDContrato", idContrato);
            cmd.Parameters.AddWithValue("@IDUnidadMedida", 0);
            cmd.Parameters.AddWithValue("@IDContratoPartida", 0);
            cmd.Parameters.AddWithValue("@Clave", string.Empty);
            cmd.Parameters.AddWithValue("@Concepto", string.Empty);
            cmd.Parameters.AddWithValue("@PrecioUnitario", 0m);
            cmd.Parameters.AddWithValue("@Cantidad", 0m);
            cmd.Parameters.AddWithValue("@Ajuste", 0m);
            cmd.Parameters.AddWithValue("@ConceptoGlobal", 0);
            cmd.Parameters.AddWithValue("@IDStatus", 0);
            cmd.Parameters.AddWithValue("@Orden", delta);
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<ResultadoSp> CargarConceptosDeArchivoAsync(CargarObraContratoConceptosRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpInsertarConceptoArchivo, cmd =>
        {
            cmd.Parameters.AddWithValue("@Limpiar", r.Limpiar ? 1 : 0);
            cmd.Parameters.AddWithValue("@Archivo", LegacySp.Texto(r.Archivo));
            cmd.Parameters.AddWithValue("@Contrato", LegacySp.Texto(r.Contrato));
            cmd.Parameters.AddWithValue("@CrearGrupo", r.CrearGrupo ? 1 : 0);
            cmd.Parameters.AddWithValue("@ClaveGrupo", LegacySp.Texto(r.ClaveGrupo));
            cmd.Parameters.AddWithValue("@Grupo", LegacySp.Texto(r.Grupo));
            cmd.Parameters.AddWithValue("@ClaveConcepto", LegacySp.Texto(r.ClaveConcepto));
            cmd.Parameters.AddWithValue("@Concepto", LegacySp.Texto(r.Concepto));
            cmd.Parameters.AddWithValue("@UnidadMedida", LegacySp.Texto(r.UnidadMedida));
            cmd.Parameters.AddWithValue("@PrecioUnitario", r.PrecioUnitario);
            cmd.Parameters.AddWithValue("@Cantidad", r.Cantidad);
            cmd.Parameters.AddWithValue("@Ajuste", r.Ajuste);
            cmd.Parameters.AddWithValue("@ConceptoGlobal", r.ConceptoGlobal ? 1 : 0);
            cmd.Parameters.AddWithValue("@PartidaPresupuestal", LegacySp.Texto(r.PartidaPresupuestal));
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<ResultadoSp> ActualizarConceptoEstimacionAsync(ActualizarObraConceptoEstimacionRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpActualizarConceptoEstimacion, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDContrato", r.IdContrato);
            cmd.Parameters.AddWithValue("@IDContratoEstimacion", r.IdContratoEstimacion);
            cmd.Parameters.AddWithValue("@IDContratoPartida", r.IdContratoPartida);
            cmd.Parameters.AddWithValue("@IDsContratoConcepto", LegacySp.Lista(r.IdsContratoConcepto));
            cmd.Parameters.AddWithValue("@Cantidades", LegacySp.Lista(r.Cantidades));
            cmd.Parameters.AddWithValue("@CantidadesExcedentes", LegacySp.Lista(r.CantidadesExcedentes));
            cmd.Parameters.AddWithValue("@PorcentajesExcedentes", LegacySp.Lista(r.PorcentajesExcedentes));
            cmd.Parameters.AddWithValue("@Ajustes", LegacySp.Lista(r.Ajustes));
            Firma(cmd, idUsuario, equipo);
        }, ct);

    // ==================================================================
    // Partidas del contrato
    // ==================================================================

    public Task<List<Dictionary<string, object?>>> ConsultarContratoPartidasAsync(ObraContratoPartidasFiltro f, CancellationToken ct = default)
        => ConsultarAsync(SpConsultarContratoPartidas, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDContratoPartida", f.IdContratoPartida);
            cmd.Parameters.AddWithValue("@IDContrato", f.IdContrato);
            cmd.Parameters.AddWithValue("@IDPartida", f.IdPartida);
            cmd.Parameters.AddWithValue("@IDStatus", f.IdStatus);
            cmd.Parameters.AddWithValue("@Partida", LegacySp.Filtro(f.Partida));
            cmd.Parameters.AddWithValue("@Comentario", LegacySp.Filtro(f.Comentario));
            cmd.Parameters.AddWithValue("@Formato", 0);
            cmd.Parameters.AddWithValue("@Identico", f.Exacto ? 1 : 0);
        }, ct);

    public Task<ResultadoSp> InsertarContratoPartidaAsync(CrearObraContratoPartidaRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpInsertarContratoPartida, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDContrato", r.IdContrato);
            cmd.Parameters.AddWithValue("@IDConvenio", r.IdConvenio);
            cmd.Parameters.AddWithValue("@IDPartida", r.IdPartida);
            cmd.Parameters.AddWithValue("@Monto", r.Monto);
            cmd.Parameters.AddWithValue("@Comentario", LegacySp.Texto(r.Comentario));
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<ResultadoSp> ActualizarContratoPartidaAsync(int id, ActualizarObraContratoPartidaRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpActualizarContratoPartida, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDContratoPartida", id);
            cmd.Parameters.AddWithValue("@IDContrato", r.IdContrato);
            cmd.Parameters.AddWithValue("@IDConvenio", r.IdConvenio);
            cmd.Parameters.AddWithValue("@IDPartida", r.IdPartida);
            cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);
            cmd.Parameters.AddWithValue("@Monto", r.Monto);
            cmd.Parameters.AddWithValue("@Orden", 0);
            cmd.Parameters.AddWithValue("@Comentario", LegacySp.Texto(r.Comentario));
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<ResultadoSp> ReordenarContratoPartidaAsync(int id, int idContrato, int delta, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpActualizarContratoPartida, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDContratoPartida", id);
            cmd.Parameters.AddWithValue("@IDContrato", idContrato);
            cmd.Parameters.AddWithValue("@IDConvenio", 0);
            cmd.Parameters.AddWithValue("@IDPartida", 0);
            cmd.Parameters.AddWithValue("@IDStatus", 0);
            cmd.Parameters.AddWithValue("@Monto", 0m);
            cmd.Parameters.AddWithValue("@Orden", delta);
            cmd.Parameters.AddWithValue("@Comentario", string.Empty);
            Firma(cmd, idUsuario, equipo);
        }, ct);

    // ==================================================================
    // Periodos
    // ==================================================================

    public Task<List<Dictionary<string, object?>>> ConsultarPeriodosAsync(ObraContratoPeriodosFiltro f, CancellationToken ct = default)
        => ConsultarAsync(SpConsultarPeriodos, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDContratoPeriodo", f.IdContratoPeriodo);
            cmd.Parameters.AddWithValue("@IDContrato", f.IdContrato);
            cmd.Parameters.AddWithValue("@IDStatus", f.IdStatus);
            cmd.Parameters.AddWithValue("@Periodo", LegacySp.Filtro(f.Periodo));
            cmd.Parameters.AddWithValue("@Formato", 0);
            cmd.Parameters.AddWithValue("@Identico", f.Exacto ? 1 : 0);
        }, ct);

    public Task<ResultadoSp> InsertarPeriodoAsync(CrearObraContratoPeriodoRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpInsertarPeriodo, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDContrato", r.IdContrato);
            cmd.Parameters.AddWithValue("@Periodo", LegacySp.Texto(r.Periodo));
            cmd.Parameters.AddWithValue("@FechaInicio", LegacySp.FechaODbNull(r.FechaInicio));
            cmd.Parameters.AddWithValue("@FechaTermino", LegacySp.FechaODbNull(r.FechaTermino));
            cmd.Parameters.AddWithValue("@PorcentajeAvanceFisicoProgramado", r.PorcentajeAvanceFisicoProgramado);
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<ResultadoSp> ActualizarPeriodoAsync(int id, ActualizarObraContratoPeriodoRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpActualizarPeriodo, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDContratoPeriodo", id);
            cmd.Parameters.AddWithValue("@IDContrato", r.IdContrato);
            cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);
            cmd.Parameters.AddWithValue("@Periodo", LegacySp.Texto(r.Periodo));
            cmd.Parameters.AddWithValue("@FechaInicio", LegacySp.FechaODbNull(r.FechaInicio));
            cmd.Parameters.AddWithValue("@FechaTermino", LegacySp.FechaODbNull(r.FechaTermino));
            cmd.Parameters.AddWithValue("@PorcentajeAvanceFisicoProgramado", r.PorcentajeAvanceFisicoProgramado);
            Firma(cmd, idUsuario, equipo);
        }, ct);

    // ==================================================================
    // Estimaciones
    // ==================================================================

    private static void ParametrosDeEstimacion(SqlCommand cmd, CrearObraContratoEstimacionRequest r)
    {
        cmd.Parameters.AddWithValue("@IDProveedorRepresentanteTecnico", r.IdProveedorRepresentanteTecnico);
        cmd.Parameters.AddWithValue("@IDProveedorRepresentanteTecnicoSupExt", r.IdProveedorRepresentanteTecnicoSupExt);
        cmd.Parameters.AddWithValue("@FechaElaboracion", LegacySp.FechaODbNull(r.FechaElaboracion));
        cmd.Parameters.AddWithValue("@FechaInicioEstimacion", LegacySp.FechaODbNull(r.FechaInicioEstimacion));
        cmd.Parameters.AddWithValue("@FechaTerminoEstimacion", LegacySp.FechaODbNull(r.FechaTerminoEstimacion));
        cmd.Parameters.AddWithValue("@Nombre", LegacySp.Texto(r.Nombre));
        cmd.Parameters.AddWithValue("@TotalEstimacionProgramada", r.TotalEstimacionProgramada);
        cmd.Parameters.AddWithValue("@ContratoTerminado", r.ContratoTerminado ? 1 : 0);
        cmd.Parameters.AddWithValue("@FechaRealTermino", LegacySp.FechaODbNull(r.FechaRealTermino));
        cmd.Parameters.AddWithValue("@PorcentajeAvanceFisicoReal", r.PorcentajeAvanceFisicoReal);
    }

    public Task<ResultadoSp> InsertarEstimacionAsync(CrearObraContratoEstimacionRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpInsertarEstimacion, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDContrato", r.IdContrato);
            ParametrosDeEstimacion(cmd, r);
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<ResultadoSp> ActualizarEstimacionAsync(int id, ActualizarObraContratoEstimacionRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpActualizarEstimacion, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDContratoEstimacion", id);
            cmd.Parameters.AddWithValue("@IDProveedorRepresentanteTecnico", r.IdProveedorRepresentanteTecnico);
            cmd.Parameters.AddWithValue("@IDProveedorRepresentanteTecnicoSupExt", r.IdProveedorRepresentanteTecnicoSupExt);
            cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);
            cmd.Parameters.AddWithValue("@FechaElaboracion", LegacySp.FechaODbNull(r.FechaElaboracion));
            cmd.Parameters.AddWithValue("@FechaInicioEstimacion", LegacySp.FechaODbNull(r.FechaInicioEstimacion));
            cmd.Parameters.AddWithValue("@FechaTerminoEstimacion", LegacySp.FechaODbNull(r.FechaTerminoEstimacion));
            cmd.Parameters.AddWithValue("@Nombre", LegacySp.Texto(r.Nombre));
            cmd.Parameters.AddWithValue("@TotalEstimacionProgramada", r.TotalEstimacionProgramada);
            cmd.Parameters.AddWithValue("@ContratoTerminado", r.ContratoTerminado ? 1 : 0);
            cmd.Parameters.AddWithValue("@FechaRealTermino", LegacySp.FechaODbNull(r.FechaRealTermino));
            cmd.Parameters.AddWithValue("@PorcentajeAvanceFisicoReal", r.PorcentajeAvanceFisicoReal);
            Firma(cmd, idUsuario, equipo);
        }, ct);

    // ==================================================================
    // Lectores del renglon que devuelve la consulta de contratos
    // ==================================================================

    private static int Entero(Dictionary<string, object?> fila, string clave)
        => fila.TryGetValue(clave, out var v) && v is not null && v is not DBNull
            ? Convert.ToInt32(v)
            : 0;

    private static decimal Numero(Dictionary<string, object?> fila, string clave)
        => fila.TryGetValue(clave, out var v) && v is not null && v is not DBNull
            ? Convert.ToDecimal(v)
            : 0m;

    private static string Cadena(Dictionary<string, object?> fila, string clave)
        => fila.TryGetValue(clave, out var v) && v is not null && v is not DBNull
            ? Convert.ToString(v) ?? string.Empty
            : string.Empty;

    private static DateOnly? Fecha(Dictionary<string, object?> fila, string clave)
    {
        if (!fila.TryGetValue(clave, out var v) || v is null || v is DBNull) return null;
        if (v is DateTime dt) return DateOnly.FromDateTime(dt);
        return DateTime.TryParse(Convert.ToString(v), out var parsed) ? DateOnly.FromDateTime(parsed) : null;
    }
}
