using System.Data;
using ISL_Service.Application.DTOs.Aviacion;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/*
  LOS CATALOGOS DE AVIACION.

  NINGUNO DE ESTOS PROCEDIMIENTOS ESTA EN Produccion_svr. Comprobado contra
  sys.procedures: de los 109 procedimientos de este lote faltan 66, y practicamente
  todos los de aviacion estan entre ellos (sp_n_InsertarCuenta,
  sp_n_ActualizarCuenta, sp_n_AceptarCuenta, sp_n_InsertarEconomico,
  sp_n_ConsultaPermisionarios, sp_n_ActualizarImpresora, sp_n_SincronizarDownload,
  ...). Los que si estan son solo tres lecturas (sp_n_ConsultaCuentas,
  sp_n_ConsultaEconomicos, sp_n_ConsultaConceptos, sp_n_ConsultaCajas).

  Y aunque estuvieran: en esta base dbo.Constantes.AviacionDBServer esta VACIA y
  AviacionIDCaja es 0, porque Tauro no es una instalacion de aviacion. Legacy, con
  esa configuracion, hace `new SqlConnection("")` y revienta. O sea que estas
  capacidades NO SON VERIFICABLES en esta maquina por dos motivos independientes:
  falta la base MacAviacion* y falta la configuracion de la empresa.

  Se implementan contra el contrato de Legacy —que es la especificacion— con los
  mismos parametros, en el mismo orden y con los mismos valores por omision, y se
  dejan anotadas como no verificables aqui. La logica que SI se puede probar sin
  base (validaciones, armado de las listas con "~", la tabla de sincronizacion por
  operacion) esta cubierta con pruebas unitarias.
*/
public class AviacionCatalogosRepository : IAviacionCatalogosRepository
{
    private readonly IConexionDeAviacion _conexion;

    private const string SpActualizarCaja = "dbo.sp_n_ActualizarCaja";

    private const string SpConsultarConceptos = "dbo.sp_n_ConsultaConceptos";
    private const string SpConceptoSiguienteCuenta = "dbo.sp_n_ConsultaConceptoNextNumCuenta";
    private const string SpInsertarConcepto = "dbo.sp_n_InsertarConcepto";
    private const string SpActualizarConcepto = "dbo.sp_n_ActualizarConcepto";

    private const string SpConsultarCuentas = "dbo.sp_n_ConsultaCuentas";
    private const string SpCuentaSiguienteNumero = "dbo.sp_n_ConsultaCuentaNextNumCuenta";
    private const string SpInsertarCuenta = "dbo.sp_n_InsertarCuenta";
    private const string SpActualizarCuenta = "dbo.sp_n_ActualizarCuenta";
    private const string SpAceptarCuenta = "dbo.sp_n_AceptarCuenta";
    private const string SpConsultarTarjetaPorcentajes = "dbo.sp_n_ConsultaAviacionTarjetaPorcentajes";
    private const string SpActualizarTarjetaPorcentaje = "dbo.sp_n_ActualizarAviacionTarjetaPorcentaje";

    private const string SpConsultarEconomicos = "dbo.sp_n_ConsultaEconomicos";
    private const string SpEconomicoSiguienteId = "dbo.sp_n_ConsultaEconomicoNextID";
    private const string SpEconomicoSiguienteNumeroCuenta = "dbo.sp_n_ConsultaEconomicoNextNumCuenta";
    private const string SpConsultarEconomicosAuxiliares = "dbo.sp_n_ConsultaEconomicosAuxiliares";
    private const string SpInsertarEconomico = "dbo.sp_n_InsertarEconomico";
    private const string SpActualizarEconomico = "dbo.sp_n_ActualizarEconomico";
    private const string SpActualizarEconomicoAmortizacion = "dbo.sp_n_ActualizarEconomicoAmortizacion";
    private const string SpReasignarEconomico = "dbo.sp_n_EconomicoReasignacion";
    private const string SpAceptarEconomico = "dbo.sp_n_AceptarEconomico";

    private const string SpConsultarMotivosFianza = "dbo.sp_n_ConsultaMotivosFianza";
    private const string SpMotivoFianzaSiguienteId = "dbo.sp_n_ConsultaMotivoFianzaNextID";
    private const string SpInsertarMotivoFianza = "dbo.sp_n_InsertarMotivoFianza";
    private const string SpActualizarMotivoFianza = "dbo.sp_n_ActualizarMotivoFianza";

    private const string SpConsultarOperadores = "dbo.sp_n_ConsultaOperadores";
    private const string SpOperadorSiguienteNumeroCuenta = "dbo.sp_n_ConsultaOperadorNextNumCuenta";
    private const string SpInsertarOperador = "dbo.sp_n_InsertarOperador";
    private const string SpActualizarOperador = "dbo.sp_n_ActualizarOperador";
    private const string SpAceptarOperador = "dbo.sp_n_AceptarOperador";
    private const string SpActualizarOperadorFianza = "dbo.sp_n_ActualizarOperadorFianza";
    private const string SpActualizarOperadorCuenta = "dbo.sp_n_ActualizarOperadorCuenta";

    private const string SpConsultarPermisionarios = "dbo.sp_n_ConsultaPermisionarios";
    private const string SpPermisionarioSiguienteNumeroCuenta = "dbo.sp_n_ConsultaPermisionarioNextNumCuenta";
    private const string SpInsertarPermisionario = "dbo.sp_n_InsertarPermisionario";
    private const string SpActualizarPermisionario = "dbo.sp_n_ActualizarPermisionario";
    private const string SpAceptarPermisionario = "dbo.sp_n_AceptarPermisionario";

    private const string SpConsultarImpresoras = "dbo.sp_n_ConsultaImpresoras";
    private const string SpActualizarImpresora = "dbo.sp_n_ActualizarImpresora";

    private const string SpSincronizarDescarga = "dbo.sp_n_SincronizarDownload";

    public AviacionCatalogosRepository(IConexionDeAviacion conexion)
    {
        _conexion = conexion;
    }

    private async Task<DataTable> TablaAsync(string sp, bool central, Action<SqlCommand> parametros, CancellationToken ct)
    {
        await using var conn = await _conexion.AbrirAsync(central, ct);

        await using var cmd = new SqlCommand(sp, conn) { CommandType = CommandType.StoredProcedure };
        parametros(cmd);

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd)) adapter.Fill(dt);
        return dt;
    }

    private async Task<List<Dictionary<string, object?>>> ConsultarAsync(string sp, bool central, Action<SqlCommand> parametros, CancellationToken ct)
        => LegacySp.Renglones(await TablaAsync(sp, central, parametros, ct));

    /*
      LAS ESCRITURAS VAN SIEMPRE AL SERVIDOR CENTRAL.

      Lo que se lee puede venir de la replica local (es mas rapido y la caja
      trabaja sin red), pero lo que se escribe va al central, igual que en
      Legacy: en los repositorios de aviacion, insert/update/aceptar hacen
      `new SqlConnection(AviacionDBServer)` SIN la guarda `if (AviacionDBServer
      != "")` que si tiene la consulta (CatCuentaRepository.cs:49-50 contra
      :177). Es decir: la lectura degrada a local a proposito y la escritura no.
      Tiene sentido: dos cajas escribiendo cada una en su replica crearian dos
      cuentas distintas con el mismo numero.
    */
    private async Task<ResultadoSp> EscribirAsync(string sp, Action<SqlCommand> parametros, CancellationToken ct)
        => LegacySp.LeerResultado(await TablaAsync(sp, central: true, parametros, ct));

    /// <summary>Para los SP que son siempre de la base de la empresa.</summary>
    private async Task<ResultadoSp> EscribirLocalAsync(string sp, Action<SqlCommand> parametros, CancellationToken ct)
        => LegacySp.LeerResultado(await TablaAsync(sp, central: false, parametros, ct));

    private static void Firma(SqlCommand cmd, int idUsuario, string equipo)
    {
        cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
        cmd.Parameters.AddWithValue("@Equipo", LegacySp.Texto(equipo));
    }

    // ==================================================================
    // Cajas
    // ==================================================================

    public Task<ResultadoSp> ActualizarCajaAsync(int idCaja, ActualizarAviacionCajaRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => EscribirLocalAsync(SpActualizarCaja, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCaja", idCaja);
            cmd.Parameters.AddWithValue("@NumCaja", r.NumCaja);
            cmd.Parameters.AddWithValue("@NombreCaja", LegacySp.Texto(r.NombreCaja));
            cmd.Parameters.AddWithValue("@EquipoCaja", LegacySp.Texto(r.EquipoCaja));
            cmd.Parameters.AddWithValue("@MaestraCaja", r.MaestraCaja ? 1 : 0);
            Firma(cmd, idUsuario, equipo);
        }, ct);

    // ==================================================================
    // Conceptos
    // ==================================================================

    public Task<List<Dictionary<string, object?>>> ConsultarConceptosAsync(AviacionConceptosFiltro f, CancellationToken ct = default)
        => ConsultarAsync(SpConsultarConceptos, central: false, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDConcepto", f.IdConcepto);
            cmd.Parameters.AddWithValue("@IDStatus", f.IdStatus);
            cmd.Parameters.AddWithValue("@NumConcepto", LegacySp.Filtro(f.NumConcepto));
            cmd.Parameters.AddWithValue("@NombreConcepto", LegacySp.Filtro(f.NombreConcepto));
        }, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarConceptoSiguienteCuentaAsync(int idConcepto, CancellationToken ct = default)
        => ConsultarAsync(SpConceptoSiguienteCuenta, central: false, cmd =>
            cmd.Parameters.AddWithValue("@IDConcepto", idConcepto), ct);

    public Task<ResultadoSp> InsertarConceptoAsync(CrearAviacionConceptoRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => EscribirLocalAsync(SpInsertarConcepto, cmd =>
        {
            cmd.Parameters.AddWithValue("@NumConcepto", LegacySp.Texto(r.NumConcepto));
            cmd.Parameters.AddWithValue("@NombreConcepto", LegacySp.Texto(r.NombreConcepto));
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<ResultadoSp> ActualizarConceptoAsync(int idConcepto, ActualizarAviacionConceptoRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => EscribirLocalAsync(SpActualizarConcepto, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDConcepto", idConcepto);
            cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);
            cmd.Parameters.AddWithValue("@NumConcepto", LegacySp.Texto(r.NumConcepto));
            cmd.Parameters.AddWithValue("@NombreConcepto", LegacySp.Texto(r.NombreConcepto));
            Firma(cmd, idUsuario, equipo);
        }, ct);

    // ==================================================================
    // Cuentas
    // ==================================================================

    public Task<List<Dictionary<string, object?>>> ConsultarCuentasAsync(AviacionCuentasFiltro f, bool central, CancellationToken ct = default)
        => ConsultarAsync(SpConsultarCuentas, central, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCuenta", f.IdCuenta);
            cmd.Parameters.AddWithValue("@IDConcepto", f.IdConcepto);
            cmd.Parameters.AddWithValue("@IDStatus", f.IdStatus);
            cmd.Parameters.AddWithValue("@NumCuenta", LegacySp.Filtro(f.NumCuenta));
            cmd.Parameters.AddWithValue("@CuentaContable", LegacySp.Filtro(f.CuentaContable));
            cmd.Parameters.AddWithValue("@NombreCuenta", LegacySp.Filtro(f.NombreCuenta));
            cmd.Parameters.AddWithValue("@DebeHaber", LegacySp.Filtro(f.DebeHaber));
            cmd.Parameters.AddWithValue("@Auxiliar", f.Auxiliar);
            cmd.Parameters.AddWithValue("@TarjetaDistribucion", f.TarjetaDistribucion);
            cmd.Parameters.AddWithValue("@PendienteAceptacion", f.PendienteAceptacion);
        }, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarCuentaSiguienteNumeroAsync(int idCuenta, CancellationToken ct = default)
        => ConsultarAsync(SpCuentaSiguienteNumero, central: true, cmd =>
            cmd.Parameters.AddWithValue("@IDCuenta", idCuenta), ct);

    private static void ParametrosDeCuenta(SqlCommand cmd, CrearAviacionCuentaRequest r)
    {
        cmd.Parameters.AddWithValue("@IDConcepto", r.IdConcepto);
        cmd.Parameters.AddWithValue("@NumCuenta", LegacySp.Texto(r.NumCuenta));
        cmd.Parameters.AddWithValue("@CuentaContable", LegacySp.Texto(r.CuentaContable));
        cmd.Parameters.AddWithValue("@NombreCuenta", LegacySp.Texto(r.NombreCuenta));
        cmd.Parameters.AddWithValue("@Importe", r.Importe);
        cmd.Parameters.AddWithValue("@ImporteFijo", r.ImporteFijo ? 1 : 0);
        cmd.Parameters.AddWithValue("@EsRecurrente", r.EsRecurrente ? 1 : 0);
        cmd.Parameters.AddWithValue("@DebeHaber", LegacySp.Texto(r.DebeHaber));
        cmd.Parameters.AddWithValue("@Enlace", LegacySp.Texto(r.Enlace));
        cmd.Parameters.AddWithValue("@SRojo", LegacySp.Texto(r.SRojo));
        cmd.Parameters.AddWithValue("@Auxiliar", r.Auxiliar ? 1 : 0);
        cmd.Parameters.AddWithValue("@CuentaGlobalizada", r.CuentaGlobalizada ? 1 : 0);
        cmd.Parameters.AddWithValue("@CuentaIndividual", r.CuentaIndividual ? 1 : 0);
        cmd.Parameters.AddWithValue("@PagoVirtual", r.PagoVirtual ? 1 : 0);
    }

    public Task<ResultadoSp> InsertarCuentaAsync(CrearAviacionCuentaRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => EscribirAsync(SpInsertarCuenta, cmd =>
        {
            ParametrosDeCuenta(cmd, r);
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<ResultadoSp> ActualizarCuentaAsync(int idCuenta, ActualizarAviacionCuentaRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => EscribirAsync(SpActualizarCuenta, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCuenta", idCuenta);
            cmd.Parameters.AddWithValue("@IDConcepto", r.IdConcepto);
            cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);
            cmd.Parameters.AddWithValue("@NumCuenta", LegacySp.Texto(r.NumCuenta));
            cmd.Parameters.AddWithValue("@CuentaContable", LegacySp.Texto(r.CuentaContable));
            cmd.Parameters.AddWithValue("@NombreCuenta", LegacySp.Texto(r.NombreCuenta));
            cmd.Parameters.AddWithValue("@Importe", r.Importe);
            cmd.Parameters.AddWithValue("@ImporteFijo", r.ImporteFijo ? 1 : 0);
            cmd.Parameters.AddWithValue("@EsRecurrente", r.EsRecurrente ? 1 : 0);
            cmd.Parameters.AddWithValue("@DebeHaber", LegacySp.Texto(r.DebeHaber));
            cmd.Parameters.AddWithValue("@Enlace", LegacySp.Texto(r.Enlace));
            cmd.Parameters.AddWithValue("@SRojo", LegacySp.Texto(r.SRojo));
            cmd.Parameters.AddWithValue("@Auxiliar", r.Auxiliar ? 1 : 0);
            cmd.Parameters.AddWithValue("@CuentaGlobalizada", r.CuentaGlobalizada ? 1 : 0);
            cmd.Parameters.AddWithValue("@CuentaIndividual", r.CuentaIndividual ? 1 : 0);
            cmd.Parameters.AddWithValue("@PagoVirtual", r.PagoVirtual ? 1 : 0);
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<ResultadoSp> AceptarCuentaAsync(int idCuenta, int idUsuario, string equipo, CancellationToken ct = default)
        => EscribirAsync(SpAceptarCuenta, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCuenta", idCuenta);
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarTarjetaPorcentajesAsync(int idCuenta, int idStatus, CancellationToken ct = default)
        => ConsultarAsync(SpConsultarTarjetaPorcentajes, central: true, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCuenta", idCuenta);
            cmd.Parameters.AddWithValue("@IDStatus", idStatus);
        }, ct);

    public Task<ResultadoSp> ActualizarTarjetaPorcentajesAsync(ActualizarAviacionTarjetaPorcentajesRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => EscribirAsync(SpActualizarTarjetaPorcentaje, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDsCuenta", LegacySp.Lista(r.IdsCuenta));
            cmd.Parameters.AddWithValue("@Porcentajes", LegacySp.Lista(r.Porcentajes));
            Firma(cmd, idUsuario, equipo);
        }, ct);

    // ==================================================================
    // Economicos
    // ==================================================================

    public Task<List<Dictionary<string, object?>>> ConsultarEconomicosAsync(AviacionEconomicosFiltro f, bool central, CancellationToken ct = default)
        => ConsultarAsync(SpConsultarEconomicos, central, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDEconomico", f.IdEconomico);
            cmd.Parameters.AddWithValue("@IDPermisionario", f.IdPermisionario);
            cmd.Parameters.AddWithValue("@IDStatus", f.IdStatus);
            cmd.Parameters.AddWithValue("@NumEconomico", f.NumEconomico);
            cmd.Parameters.AddWithValue("@NumCuentaEconomico", LegacySp.Filtro(f.NumCuentaEconomico));
            cmd.Parameters.AddWithValue("@NombreEconomico", LegacySp.Filtro(f.NombreEconomico));
            cmd.Parameters.AddWithValue("@EsMicro", f.EsMicro);
            cmd.Parameters.AddWithValue("@PendienteAceptacion", f.PendienteAceptacion);
        }, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarEconomicoSiguienteIdAsync(int idPermisionario, CancellationToken ct = default)
        => ConsultarAsync(SpEconomicoSiguienteId, central: true, cmd =>
            cmd.Parameters.AddWithValue("@IDPermisionario", idPermisionario), ct);

    public Task<List<Dictionary<string, object?>>> ConsultarEconomicoSiguienteNumeroCuentaAsync(int idPermisionario, CancellationToken ct = default)
        => ConsultarAsync(SpEconomicoSiguienteNumeroCuenta, central: true, cmd =>
            cmd.Parameters.AddWithValue("@IDPermisionario", idPermisionario), ct);

    public Task<List<Dictionary<string, object?>>> ConsultarEconomicosAuxiliaresAsync(AviacionEconomicosAuxiliaresFiltro f, CancellationToken ct = default)
        => ConsultarAsync(SpConsultarEconomicosAuxiliares, central: false, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDRecaudacion", f.IdRecaudacion);
            cmd.Parameters.AddWithValue("@IDEconomico", f.IdEconomico);
            cmd.Parameters.AddWithValue("@NumEconomico", f.NumEconomico);
            cmd.Parameters.AddWithValue("@IDPermisionario", f.IdPermisionario);
            cmd.Parameters.AddWithValue("@IDCuenta", f.IdCuenta);
            cmd.Parameters.AddWithValue("@NumCuenta", f.NumCuenta);
            cmd.Parameters.AddWithValue("@IDCaja", f.IdCaja);
            cmd.Parameters.AddWithValue("@StatusPago", f.StatusPago);
        }, ct);

    public Task<ResultadoSp> InsertarEconomicoAsync(CrearAviacionEconomicoRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => EscribirAsync(SpInsertarEconomico, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDPermisionario", r.IdPermisionario);
            cmd.Parameters.AddWithValue("@NumEconomico", r.NumEconomico);
            cmd.Parameters.AddWithValue("@NumCuentaEconomico", LegacySp.Texto(r.NumCuentaEconomico));
            cmd.Parameters.AddWithValue("@NombreEconomico", LegacySp.Texto(r.NombreEconomico));
            cmd.Parameters.AddWithValue("@EsMicro", r.EsMicro ? 1 : 0);
            cmd.Parameters.AddWithValue("@EsRecaudacion", r.EsRecaudacion ? 1 : 0);
            cmd.Parameters.AddWithValue("@EsAmortizacion", r.EsAmortizacion ? 1 : 0);
            cmd.Parameters.AddWithValue("@EsInscripcion", r.EsInscripcion ? 1 : 0);
            cmd.Parameters.AddWithValue("@Observaciones", LegacySp.Texto(r.Observaciones));
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<ResultadoSp> ActualizarEconomicoAsync(int idEconomico, ActualizarAviacionEconomicoRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => EscribirAsync(SpActualizarEconomico, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDEconomico", idEconomico);
            cmd.Parameters.AddWithValue("@IDPermisionario", r.IdPermisionario);
            cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);
            cmd.Parameters.AddWithValue("@NumEconomico", r.NumEconomico);
            cmd.Parameters.AddWithValue("@NumCuentaEconomico", LegacySp.Texto(r.NumCuentaEconomico));
            cmd.Parameters.AddWithValue("@NombreEconomico", LegacySp.Texto(r.NombreEconomico));
            cmd.Parameters.AddWithValue("@EsMicro", r.EsMicro ? 1 : 0);
            cmd.Parameters.AddWithValue("@EsRecaudacion", r.EsRecaudacion ? 1 : 0);
            cmd.Parameters.AddWithValue("@EsAmortizacion", r.EsAmortizacion ? 1 : 0);
            cmd.Parameters.AddWithValue("@EsInscripcion", r.EsInscripcion ? 1 : 0);
            cmd.Parameters.AddWithValue("@Observaciones", LegacySp.Texto(r.Observaciones));
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<ResultadoSp> ActualizarEconomicoAmortizacionAsync(int idEconomico, ActualizarAviacionEconomicoAmortizacionRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => EscribirAsync(SpActualizarEconomicoAmortizacion, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDEconomico", idEconomico);
            cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);
            cmd.Parameters.AddWithValue("@EsRecaudacion", r.EsRecaudacion ? 1 : 0);
            cmd.Parameters.AddWithValue("@EsAmortizacion", r.EsAmortizacion ? 1 : 0);
            cmd.Parameters.AddWithValue("@EsInscripcion", r.EsInscripcion ? 1 : 0);
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<ResultadoSp> ReasignarEconomicoAsync(int idEconomico, ReasignarAviacionEconomicoRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => EscribirAsync(SpReasignarEconomico, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDEconomicoAnterior", idEconomico);
            cmd.Parameters.AddWithValue("@IDPermisionarioAnterior", r.IdPermisionarioAnterior);
            cmd.Parameters.AddWithValue("@IDPermisionarioNuevo", r.IdPermisionarioNuevo);
            cmd.Parameters.AddWithValue("@EsRecaudacion", r.EsRecaudacion ? 1 : 0);
            cmd.Parameters.AddWithValue("@EsAmortizacion", r.EsAmortizacion ? 1 : 0);
            cmd.Parameters.AddWithValue("@EsInscripcion", r.EsInscripcion ? 1 : 0);
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<ResultadoSp> AceptarEconomicoAsync(int idEconomico, int idUsuario, string equipo, CancellationToken ct = default)
        => EscribirAsync(SpAceptarEconomico, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDEconomico", idEconomico);
            Firma(cmd, idUsuario, equipo);
        }, ct);

    // ==================================================================
    // Motivos de fianza
    // ==================================================================

    public Task<List<Dictionary<string, object?>>> ConsultarMotivosFianzaAsync(AviacionMotivosFianzaFiltro f, bool central, CancellationToken ct = default)
        => ConsultarAsync(SpConsultarMotivosFianza, central, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDMotivoFianza", f.IdMotivoFianza);
            cmd.Parameters.AddWithValue("@IDStatus", f.IdStatus);
            cmd.Parameters.AddWithValue("@NumMotivoFianza", f.NumMotivoFianza);
            cmd.Parameters.AddWithValue("@MotivoFianza", LegacySp.Filtro(f.MotivoFianza));
            cmd.Parameters.AddWithValue("@EsIngreso", f.EsIngreso);
            cmd.Parameters.AddWithValue("@EsEgreso", f.EsEgreso);
        }, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarMotivoFianzaSiguienteIdAsync(int idMotivoFianza, CancellationToken ct = default)
        => ConsultarAsync(SpMotivoFianzaSiguienteId, central: true, cmd =>
            cmd.Parameters.AddWithValue("@IDMotivoFianza", idMotivoFianza), ct);

    public Task<ResultadoSp> InsertarMotivoFianzaAsync(CrearAviacionMotivoFianzaRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => EscribirAsync(SpInsertarMotivoFianza, cmd =>
        {
            cmd.Parameters.AddWithValue("@NumMotivoFianza", r.NumMotivoFianza);
            cmd.Parameters.AddWithValue("@CuentaContable", LegacySp.Texto(r.CuentaContable));
            cmd.Parameters.AddWithValue("@MotivoFianza", LegacySp.Texto(r.MotivoFianza));
            cmd.Parameters.AddWithValue("@EsIngreso", r.EsIngreso ? 1 : 0);
            cmd.Parameters.AddWithValue("@EsEgreso", r.EsEgreso ? 1 : 0);
            cmd.Parameters.AddWithValue("@EsDocumento", r.EsDocumento ? 1 : 0);
            cmd.Parameters.AddWithValue("@EsContabilizable", r.EsContabilizable ? 1 : 0);
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<ResultadoSp> ActualizarMotivoFianzaAsync(int idMotivoFianza, ActualizarAviacionMotivoFianzaRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => EscribirAsync(SpActualizarMotivoFianza, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDMotivoFianza", idMotivoFianza);
            cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);
            cmd.Parameters.AddWithValue("@NumMotivoFianza", r.NumMotivoFianza);
            cmd.Parameters.AddWithValue("@CuentaContable", LegacySp.Texto(r.CuentaContable));
            cmd.Parameters.AddWithValue("@MotivoFianza", LegacySp.Texto(r.MotivoFianza));
            Firma(cmd, idUsuario, equipo);
        }, ct);

    // ==================================================================
    // Operadores
    // ==================================================================

    public Task<List<Dictionary<string, object?>>> ConsultarOperadoresAsync(AviacionOperadoresFiltro f, bool central, CancellationToken ct = default)
        => ConsultarAsync(SpConsultarOperadores, central, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDOperador", f.IdOperador);
            cmd.Parameters.AddWithValue("@IDCuenta", f.IdCuenta);
            cmd.Parameters.AddWithValue("@IDStatus", f.IdStatus);
            cmd.Parameters.AddWithValue("@NumNomina", f.NumNomina);
            cmd.Parameters.AddWithValue("@NumCuentaOperador", LegacySp.Filtro(f.NumCuentaOperador));
            cmd.Parameters.AddWithValue("@OperadorNombre", LegacySp.Filtro(f.OperadorNombre));
            cmd.Parameters.AddWithValue("@OperadorApellidoPaterno", LegacySp.Filtro(f.OperadorApellidoPaterno));
            cmd.Parameters.AddWithValue("@OperadorApellidoMaterno", LegacySp.Filtro(f.OperadorApellidoMaterno));
            cmd.Parameters.AddWithValue("@PendienteAceptacion", f.PendienteAceptacion);
        }, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarOperadorSiguienteNumeroCuentaAsync(int idOperador, CancellationToken ct = default)
        => ConsultarAsync(SpOperadorSiguienteNumeroCuenta, central: true, cmd =>
            cmd.Parameters.AddWithValue("@IDOperador", idOperador), ct);

    public Task<ResultadoSp> InsertarOperadorAsync(CrearAviacionOperadorRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => EscribirAsync(SpInsertarOperador, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDOperadorAnterior", r.IdOperadorAnterior);
            cmd.Parameters.AddWithValue("@NumNomina", r.NumNomina);
            cmd.Parameters.AddWithValue("@NumCuentaOperador", LegacySp.Texto(r.NumCuentaOperador));
            cmd.Parameters.AddWithValue("@OperadorNombre", LegacySp.Texto(r.OperadorNombre));
            cmd.Parameters.AddWithValue("@OperadorApellidoPaterno", LegacySp.Texto(r.OperadorApellidoPaterno));
            cmd.Parameters.AddWithValue("@OperadorApellidoMaterno", LegacySp.Texto(r.OperadorApellidoMaterno));
            cmd.Parameters.AddWithValue("@FechaAlta", LegacySp.FechaODbNull(r.FechaAlta));
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<ResultadoSp> ActualizarOperadorAsync(int idOperador, ActualizarAviacionOperadorRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => EscribirAsync(SpActualizarOperador, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDOperador", idOperador);
            cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);
            cmd.Parameters.AddWithValue("@NumNomina", r.NumNomina);
            cmd.Parameters.AddWithValue("@NumCuentaOperador", LegacySp.Texto(r.NumCuentaOperador));
            cmd.Parameters.AddWithValue("@OperadorNombre", LegacySp.Texto(r.OperadorNombre));
            cmd.Parameters.AddWithValue("@OperadorApellidoPaterno", LegacySp.Texto(r.OperadorApellidoPaterno));
            cmd.Parameters.AddWithValue("@OperadorApellidoMaterno", LegacySp.Texto(r.OperadorApellidoMaterno));
            cmd.Parameters.AddWithValue("@FechaAlta", LegacySp.FechaODbNull(r.FechaAlta));
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<ResultadoSp> AceptarOperadorAsync(int idOperador, int idUsuario, string equipo, CancellationToken ct = default)
        => EscribirAsync(SpAceptarOperador, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDOperador", idOperador);
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<ResultadoSp> ActualizarOperadorFianzaAsync(int idOperador, decimal importeFianza, int idUsuario, string equipo, CancellationToken ct = default)
        => EscribirAsync(SpActualizarOperadorFianza, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDOperador", idOperador);
            cmd.Parameters.AddWithValue("@ImporteFianza", importeFianza);
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<ResultadoSp> ActualizarOperadorCuentaAsync(int idOperador, ActualizarAviacionOperadorCuentaRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => EscribirAsync(SpActualizarOperadorCuenta, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDOperador", idOperador);
            cmd.Parameters.AddWithValue("@IDCuenta", r.IdCuenta);
            cmd.Parameters.AddWithValue("@Importe", r.Importe);
            Firma(cmd, idUsuario, equipo);
        }, ct);

    // ==================================================================
    // Permisionarios
    // ==================================================================

    public Task<List<Dictionary<string, object?>>> ConsultarPermisionariosAsync(AviacionPermisionariosFiltro f, bool central, CancellationToken ct = default)
        => ConsultarAsync(SpConsultarPermisionarios, central, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDPermisionario", f.IdPermisionario);
            cmd.Parameters.AddWithValue("@IDStatus", f.IdStatus);
            cmd.Parameters.AddWithValue("@NumPermisionario", f.NumPermisionario);
            cmd.Parameters.AddWithValue("@CuentaContablePermisionario", LegacySp.Filtro(f.CuentaContablePermisionario));
            cmd.Parameters.AddWithValue("@NombrePermisionario", LegacySp.Filtro(f.NombrePermisionario));
            cmd.Parameters.AddWithValue("@PendienteAceptacion", f.PendienteAceptacion);
        }, ct);

    /// <summary>
    /// El siguiente numero de cuenta de permisionario.
    ///
    /// Legacy le manda al SP la CADENA "0" literal
    /// (CatPermisionarioRepository: `AddWithValue("@IDPermisionario", "0")`), no
    /// el id que trae el DTO: el numero no depende de ningun permisionario, es el
    /// siguiente del catalogo. Se conserva el 0, como entero, y el endpoint no
    /// pide ningun parametro — ofrecerlo seria ofrecer un filtro que el SP
    /// ignora.
    /// </summary>
    public Task<List<Dictionary<string, object?>>> ConsultarPermisionarioSiguienteNumeroCuentaAsync(CancellationToken ct = default)
        => ConsultarAsync(SpPermisionarioSiguienteNumeroCuenta, central: true, cmd =>
            cmd.Parameters.AddWithValue("@IDPermisionario", 0), ct);

    public Task<ResultadoSp> InsertarPermisionarioAsync(CrearAviacionPermisionarioRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => EscribirAsync(SpInsertarPermisionario, cmd =>
        {
            cmd.Parameters.AddWithValue("@NumPermisionario", r.NumPermisionario);
            cmd.Parameters.AddWithValue("@NombrePermisionario", LegacySp.Texto(r.NombrePermisionario));
            cmd.Parameters.AddWithValue("@CuentaContablePermisionario", LegacySp.Texto(r.CuentaContablePermisionario));
            cmd.Parameters.AddWithValue("@Observaciones", LegacySp.Texto(r.Observaciones));
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<ResultadoSp> ActualizarPermisionarioAsync(int idPermisionario, ActualizarAviacionPermisionarioRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => EscribirAsync(SpActualizarPermisionario, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDPermisionario", idPermisionario);
            cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);
            cmd.Parameters.AddWithValue("@NumPermisionario", r.NumPermisionario);
            cmd.Parameters.AddWithValue("@CuentaContablePermisionario", LegacySp.Texto(r.CuentaContablePermisionario));
            cmd.Parameters.AddWithValue("@NombrePermisionario", LegacySp.Texto(r.NombrePermisionario));
            cmd.Parameters.AddWithValue("@Observaciones", LegacySp.Texto(r.Observaciones));
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<ResultadoSp> AceptarPermisionarioAsync(int idPermisionario, int idUsuario, string equipo, CancellationToken ct = default)
        => EscribirAsync(SpAceptarPermisionario, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDPermisionario", idPermisionario);
            Firma(cmd, idUsuario, equipo);
        }, ct);

    // ==================================================================
    // Impresoras
    // ==================================================================

    public Task<List<Dictionary<string, object?>>> ConsultarImpresorasAsync(AviacionImpresorasFiltro f, CancellationToken ct = default)
        => ConsultarAsync(SpConsultarImpresoras, central: false, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDImpresora", f.IdImpresora);
            cmd.Parameters.AddWithValue("@Equipo", LegacySp.Filtro(f.Equipo));
        }, ct);

    /// <summary>
    /// Aqui @Equipo es la LLAVE (de que computadora son las impresoras) y no la
    /// firma: el SP no recibe ningun otro identificador de renglon. Por eso este
    /// metodo no toma `equipo` del token.
    /// </summary>
    public Task<ResultadoSp> ActualizarImpresoraAsync(ActualizarAviacionImpresoraRequest r, int idUsuario, CancellationToken ct = default)
        => EscribirLocalAsync(SpActualizarImpresora, cmd =>
        {
            cmd.Parameters.AddWithValue("@ImpresoraTickets", LegacySp.Texto(r.ImpresoraTickets));
            cmd.Parameters.AddWithValue("@ImpresoraReportes", LegacySp.Texto(r.ImpresoraReportes));
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", LegacySp.Texto(r.Equipo));
        }, ct);

    // ==================================================================
    // Replicacion
    // ==================================================================

    public Task<ResultadoSp> SincronizarDescargaAsync(int idTabla, int idUsuario, string equipo, CancellationToken ct = default)
        => EscribirLocalAsync(SpSincronizarDescarga, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDTabla", idTabla);
            // Los dos valores que Legacy fija en el constructor de cada servicio
            // de aviacion: TablaPrincipal = 1, TablaBidireccional = 0.
            cmd.Parameters.AddWithValue("@TablaPrincipal", 1);
            cmd.Parameters.AddWithValue("@TablaBidireccional", 0);
            cmd.Parameters.AddWithValue("@IDCajaOrigen", 0);
            Firma(cmd, idUsuario, equipo);
        }, ct);
}
