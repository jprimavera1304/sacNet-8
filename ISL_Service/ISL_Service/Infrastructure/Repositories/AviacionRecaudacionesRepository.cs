using System.Data;
using ISL_Service.Application.DTOs.Aviacion;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/*
  RECAUDACIONES DE AVIACION.

  QUE SE PUEDE COMPROBAR EN Produccion_svr:

    sp_n_ConsultaRecaudaciones    SI esta, pero es una version VIEJA: declara 9
                                  parametros y el repositorio de Legacy le manda
                                  31. La llamada falla con "Procedure or function
                                  has too many arguments specified" en los dos
                                  lados. (Ya lo dejo anotado la ola 3.)
    sp_n_InsertarRecaudacion      SI esta.
    sp_n_CancelarRecaudacion      NO esta.
    sp_n_InsertarRecaudacionCorte NO esta.
    sp_n_ConsultaRecaudacionesCortes       NO esta.
    sp_n_ConsultaRecaudacionesRecurrentes  NO esta.
    sp_n_ConsultaRecaudacionNextID         NO esta.

  Se implementa contra el contrato de Legacy, que es la especificacion, y queda
  anotado como no verificable aqui. No se recorta el juego de parametros para que
  "quepa" en la version vieja de esta base: eso seria migrar hacia atras.
*/
public class AviacionRecaudacionesRepository : IAviacionRecaudacionesRepository
{
    private readonly IConexionDeAviacion _conexion;

    private const string SpConsultar = "dbo.sp_n_ConsultaRecaudaciones";
    private const string SpConsultarRecurrentes = "dbo.sp_n_ConsultaRecaudacionesRecurrentes";
    private const string SpConsultarCortes = "dbo.sp_n_ConsultaRecaudacionesCortes";
    private const string SpSiguienteFolio = "dbo.sp_n_ConsultaRecaudacionNextID";
    private const string SpInsertar = "dbo.sp_n_InsertarRecaudacion";
    private const string SpCancelar = "dbo.sp_n_CancelarRecaudacion";
    private const string SpInsertarCorte = "dbo.sp_n_InsertarRecaudacionCorte";

    public AviacionRecaudacionesRepository(IConexionDeAviacion conexion)
    {
        _conexion = conexion;
    }

    private async Task<DataTable> TablaAsync(string sp, Action<SqlCommand> parametros, CancellationToken ct)
    {
        await using var conn = await _conexion.AbrirLocalAsync(ct);

        await using var cmd = new SqlCommand(sp, conn) { CommandType = CommandType.StoredProcedure };
        parametros(cmd);

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd)) adapter.Fill(dt);
        return dt;
    }

    private async Task<List<Dictionary<string, object?>>> ConsultarAsync(string sp, Action<SqlCommand> parametros, CancellationToken ct)
        => LegacySp.Renglones(await TablaAsync(sp, parametros, ct));

    private async Task<ResultadoSp> EjecutarAsync(string sp, Action<SqlCommand> parametros, CancellationToken ct)
        => LegacySp.LeerResultado(await TablaAsync(sp, parametros, ct));

    private static void Firma(SqlCommand cmd, int idUsuario, string equipo)
    {
        cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
        cmd.Parameters.AddWithValue("@Equipo", LegacySp.Texto(equipo));
    }

    public Task<List<Dictionary<string, object?>>> ConsultarAsync(AviacionRecaudacionesFiltro f, CancellationToken ct = default)
        => ConsultarAsync(SpConsultar, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDRecaudacion", f.IdRecaudacion);
            cmd.Parameters.AddWithValue("@IDsRecaudacion", LegacySp.Lista(f.IdsRecaudacion));
            cmd.Parameters.AddWithValue("@IDStatus", f.IdStatus);

            cmd.Parameters.AddWithValue("@IDCaja", f.IdCaja);
            cmd.Parameters.AddWithValue("@IDsCaja", LegacySp.Lista(f.IdsCaja));
            cmd.Parameters.AddWithValue("@NumCajaInicial", f.NumCajaInicial);
            cmd.Parameters.AddWithValue("@NumCajaFinal", f.NumCajaFinal);

            cmd.Parameters.AddWithValue("@IDConcepto", f.IdConcepto);
            cmd.Parameters.AddWithValue("@IDsConcepto", LegacySp.Lista(f.IdsConcepto));
            cmd.Parameters.AddWithValue("@NumConceptoInicial", f.NumConceptoInicial);
            cmd.Parameters.AddWithValue("@NumConceptoFinal", f.NumConceptoFinal);

            cmd.Parameters.AddWithValue("@IDCuenta", f.IdCuenta);
            cmd.Parameters.AddWithValue("@IDsCuenta", LegacySp.Lista(f.IdsCuenta));
            cmd.Parameters.AddWithValue("@NumCuentaInicial", f.NumCuentaInicial);
            cmd.Parameters.AddWithValue("@NumCuentaFinal", f.NumCuentaFinal);

            cmd.Parameters.AddWithValue("@IDEconomico", f.IdEconomico);
            cmd.Parameters.AddWithValue("@IDsEconomico", LegacySp.Lista(f.IdsEconomico));
            cmd.Parameters.AddWithValue("@NumEconomicoInicial", f.NumEconomicoInicial);
            cmd.Parameters.AddWithValue("@NumEconomicoFinal", f.NumEconomicoFinal);

            cmd.Parameters.AddWithValue("@IDPermisionario", f.IdPermisionario);
            cmd.Parameters.AddWithValue("@IDsPermisionario", LegacySp.Lista(f.IdsPermisionario));
            cmd.Parameters.AddWithValue("@NumPermisionarioInicial", f.NumPermisionarioInicial);
            cmd.Parameters.AddWithValue("@NumPermisionarioFinal", f.NumPermisionarioFinal);

            cmd.Parameters.AddWithValue("@IDOperador", f.IdOperador);
            cmd.Parameters.AddWithValue("@IDsOperador", LegacySp.Lista(f.IdsOperador));
            cmd.Parameters.AddWithValue("@NumNominaInicial", f.NumNominaInicial);
            cmd.Parameters.AddWithValue("@NumNominaFinal", f.NumNominaFinal);

            cmd.Parameters.AddWithValue("@FolioInicial", f.FolioInicial);
            cmd.Parameters.AddWithValue("@FolioFinal", f.FolioFinal);

            cmd.Parameters.AddWithValue("@FechaInicial", LegacySp.DesdeElDia(f.FechaInicial));
            cmd.Parameters.AddWithValue("@FechaFinal", LegacySp.HastaElDia(f.FechaFinal ?? f.FechaInicial));
        }, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarRecurrentesAsync(AviacionRecaudacionesRecurrentesFiltro f, CancellationToken ct = default)
        => ConsultarAsync(SpConsultarRecurrentes, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDRecaudacion", f.IdRecaudacion);

            cmd.Parameters.AddWithValue("@IDCuenta", f.IdCuenta);
            cmd.Parameters.AddWithValue("@IDsCuenta", LegacySp.Lista(f.IdsCuenta));
            cmd.Parameters.AddWithValue("@NumCuentaInicial", f.NumCuentaInicial);
            cmd.Parameters.AddWithValue("@NumCuentaFinal", f.NumCuentaFinal);

            cmd.Parameters.AddWithValue("@IDEconomico", f.IdEconomico);
            cmd.Parameters.AddWithValue("@IDsEconomico", LegacySp.Lista(f.IdsEconomico));
            cmd.Parameters.AddWithValue("@NumEconomicoInicial", f.NumEconomicoInicial);
            cmd.Parameters.AddWithValue("@NumEconomicoFinal", f.NumEconomicoFinal);

            cmd.Parameters.AddWithValue("@IDPermisionario", f.IdPermisionario);
            cmd.Parameters.AddWithValue("@IDsPermisionario", LegacySp.Lista(f.IdsPermisionario));
            cmd.Parameters.AddWithValue("@NumPermisionarioInicial", f.NumPermisionarioInicial);
            cmd.Parameters.AddWithValue("@NumPermisionarioFinal", f.NumPermisionarioFinal);

            cmd.Parameters.AddWithValue("@IDOperador", f.IdOperador);
            cmd.Parameters.AddWithValue("@IDsOperador", LegacySp.Lista(f.IdsOperador));
            cmd.Parameters.AddWithValue("@NumNominaInicial", f.NumNominaInicial);
            cmd.Parameters.AddWithValue("@NumNominaFinal", f.NumNominaFinal);

            cmd.Parameters.AddWithValue("@FechaInicial", LegacySp.DesdeElDia(f.FechaInicial));
            cmd.Parameters.AddWithValue("@FechaFinal", LegacySp.HastaElDia(f.FechaFinal ?? f.FechaInicial));
        }, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarCortesAsync(AviacionRecaudacionesCortesFiltro f, CancellationToken ct = default)
        => ConsultarAsync(SpConsultarCortes, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDRecaudacionCorte", f.IdRecaudacionCorte);
            cmd.Parameters.AddWithValue("@IDStatus", f.IdStatus);
            cmd.Parameters.AddWithValue("@FechaCorte", LegacySp.ElDia(f.FechaCorte));
            cmd.Parameters.AddWithValue("@ExisteCorteDiaAnterior", f.ExisteCorteDiaAnterior);
            cmd.Parameters.AddWithValue("@UltimoDiaCorte", f.UltimoDiaCorte);
        }, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarSiguienteFolioAsync(int idCaja, CancellationToken ct = default)
        => ConsultarAsync(SpSiguienteFolio, cmd =>
            cmd.Parameters.AddWithValue("@IDCaja", idCaja), ct);

    public Task<ResultadoSp> InsertarAsync(CrearAviacionRecaudacionRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpInsertar, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDRecaudacionOriginal", r.IdRecaudacionOriginal);
            cmd.Parameters.AddWithValue("@IDRecaudacionReverso", r.IdRecaudacionReverso);
            cmd.Parameters.AddWithValue("@IDEconomico", r.IdEconomico);
            cmd.Parameters.AddWithValue("@IDCuenta", r.IdCuenta);
            cmd.Parameters.AddWithValue("@IDOperador", r.IdOperador);
            cmd.Parameters.AddWithValue("@IDMotivoFianza", r.IdMotivoFianza);
            cmd.Parameters.AddWithValue("@IDCaja", r.IdCaja);
            cmd.Parameters.AddWithValue("@NumCuentas", r.NumCuentas);
            cmd.Parameters.AddWithValue("@ImporteCuentas", r.ImporteCuentas);
            cmd.Parameters.AddWithValue("@ImporteTotal", r.ImporteTotal);
            cmd.Parameters.AddWithValue("@IDsCuenta", LegacySp.Lista(r.IdsCuenta));
            cmd.Parameters.AddWithValue("@ImportesCuenta", LegacySp.Lista(r.ImportesCuenta));
            cmd.Parameters.AddWithValue("@FechaTarjAdmFueraHorario", LegacySp.ElDia(r.FechaTarjetaAdminFueraHorario));
            cmd.Parameters.AddWithValue("@Observaciones", LegacySp.Texto(r.Observaciones));
            cmd.Parameters.AddWithValue("@FolioLlanta", LegacySp.Texto(r.FolioLlanta));
            cmd.Parameters.AddWithValue("@TipoPago", LegacySp.Texto(r.TipoPago));
            cmd.Parameters.AddWithValue("@MontoTransferencia", r.MontoTransferencia);
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<ResultadoSp> CancelarAsync(int idRecaudacion, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpCancelar, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDRecaudacion", idRecaudacion);
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<ResultadoSp> InsertarCorteAsync(CrearAviacionRecaudacionCorteRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpInsertarCorte, cmd =>
        {
            cmd.Parameters.AddWithValue("@FechaCorte", LegacySp.ElDia(r.FechaCorte));
            Firma(cmd, idUsuario, equipo);
        }, ct);
}
