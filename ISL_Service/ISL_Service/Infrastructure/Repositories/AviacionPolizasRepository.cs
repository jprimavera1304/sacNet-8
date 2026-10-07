using System.Data;
using ISL_Service.Application.DTOs.Aviacion;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/*
  POLIZAS DE CHEQUES DE AVIACION.

  Ninguno de los once procedimientos de este archivo esta instalado en
  Produccion_svr (comprobado contra sys.procedures), y la cadena del servidor
  central —Constantes.AviacionDBServer— esta vacia en esta base. Legacy falla
  igual. No verificable aqui; el contrato es el de Legacy.

  UNA DIFERENCIA QUE SI SE CORRIGE
  --------------------------------
  Legacy, al consultar polizas, hacia el mismo truco de siempre: si venia
  IDPoliza, RECONSTRUIA el DTO y tiraba los demas filtros
  (PolizaService.cs:50-56). Aqui todos los filtros se combinan.

  Y LAS FECHAS
  ------------
  Legacy mandaba las fechas como cadena volteada con ConvertFtmDate, leyendo
  Constantes.formatoFechaDB a traves de sp_n_ConsultaConstantes, que cierra el
  dia del negocio. Aqui las fechas llegan tipadas y se escriben en ISO.
  Detalle curioso del original que NO se copia: en el alta, Legacy le pegaba
  " 00:00:00" tambien a FechaFinal (PolizaService.cs:300), de modo que el ultimo
  dia del rango entraba a medianoche y los cobros de ese dia se quedaban fuera de
  la poliza. Aqui el final del rango es 23:59:59, como en la consulta.
*/
public class AviacionPolizasRepository : IAviacionPolizasRepository
{
    private readonly IConexionDeAviacion _conexion;

    private const string SpConsultar = "dbo.sp_n_ConsultaPolizas";
    private const string SpConsultarDetalle = "dbo.sp_n_ConsultaPolizaDetalle";
    private const string SpConsultarCheques = "dbo.sp_n_ConsultaPolizaCheques";
    private const string SpConsultarEncabezados = "dbo.sp_n_ConsultaPolizaEncabezados";
    private const string SpReporteIngresos = "dbo.sp_n_rptPolizaIngresos";
    private const string SpReporteFianzas = "dbo.sp_n_rptPolizaFianzas";
    private const string SpReporteDocumentos = "dbo.sp_n_rptPolizaDocumentos";
    private const string SpInsertar = "dbo.sp_n_InsertarPoliza";
    private const string SpCancelar = "dbo.sp_n_CancelarPoliza";
    private const string SpActualizarEncabezado = "dbo.sp_n_ActualizarPolizaEncabezado";
    private const string SpActualizarCheque = "dbo.sp_n_ActualizarPolizaCheque";

    public AviacionPolizasRepository(IConexionDeAviacion conexion)
    {
        _conexion = conexion;
    }

    private async Task<DataTable> TablaAsync(string sp, Action<SqlCommand> parametros, CancellationToken ct)
    {
        await using var conn = await _conexion.AbrirAsync(central: true, ct);

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

    public Task<List<Dictionary<string, object?>>> ConsultarAsync(AviacionPolizasFiltro f, CancellationToken ct = default)
        => ConsultarAsync(SpConsultar, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDPoliza", f.IdPoliza);
            cmd.Parameters.AddWithValue("@IDPolizaEncabezado", f.IdPolizaEncabezado);
            cmd.Parameters.AddWithValue("@IDStatus", f.IdStatus);
            cmd.Parameters.AddWithValue("@FechaInicial", LegacySp.DesdeElDia(f.FechaInicial));
            cmd.Parameters.AddWithValue("@FechaFinal", LegacySp.HastaElDia(f.FechaFinal ?? f.FechaInicial));
            cmd.Parameters.AddWithValue("@NumChequeInicial", f.NumChequeInicial);
            cmd.Parameters.AddWithValue("@NumChequeFinal", f.NumChequeFinal);
            cmd.Parameters.AddWithValue("@NumPolizaInicial", f.NumPolizaInicial);
            cmd.Parameters.AddWithValue("@NumPolizaFinal", f.NumPolizaFinal);
        }, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarDetalleAsync(int idPoliza, CancellationToken ct = default)
        => ConsultarAsync(SpConsultarDetalle, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDPoliza", idPoliza);
            // @Formato = 0 siempre: el 1 es la version "para Excel" del mismo SP,
            // con otras columnas. El Excel lo arma el front con estos datos.
            cmd.Parameters.AddWithValue("@Formato", 0);
        }, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarChequesAsync(int idPoliza, CancellationToken ct = default)
        => ConsultarAsync(SpConsultarCheques, cmd =>
            cmd.Parameters.AddWithValue("@IDPoliza", idPoliza), ct);

    public Task<List<Dictionary<string, object?>>> ConsultarEncabezadosAsync(int idPolizaEncabezado, int idStatus, CancellationToken ct = default)
        => ConsultarAsync(SpConsultarEncabezados, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDPolizaEncabezado", idPolizaEncabezado);
            cmd.Parameters.AddWithValue("@IDStatus", idStatus);
        }, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarIngresosAsync(string parametros, CancellationToken ct = default)
        => ConsultarAsync(SpReporteIngresos, cmd => cmd.Parameters.AddWithValue("@Params", parametros), ct);

    public Task<List<Dictionary<string, object?>>> ConsultarFianzasAsync(string parametros, CancellationToken ct = default)
        => ConsultarAsync(SpReporteFianzas, cmd => cmd.Parameters.AddWithValue("@Params", parametros), ct);

    public Task<List<Dictionary<string, object?>>> ConsultarDocumentosAsync(string parametros, CancellationToken ct = default)
        => ConsultarAsync(SpReporteDocumentos, cmd => cmd.Parameters.AddWithValue("@Params", parametros), ct);

    public Task<ResultadoSp> InsertarAsync(CrearAviacionPolizaRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpInsertar, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDPolizaEncabezado", r.IdPolizaEncabezado);
            cmd.Parameters.AddWithValue("@FechaInicial", LegacySp.DesdeElDia(r.FechaInicial));
            cmd.Parameters.AddWithValue("@FechaFinal", LegacySp.HastaElDia(r.FechaFinal));
            cmd.Parameters.AddWithValue("@NumChequeInicial", r.NumChequeInicial);
            cmd.Parameters.AddWithValue("@NumPolizaInicial", r.NumPolizaInicial);
            cmd.Parameters.AddWithValue("@cambioPolizaEncabezado", r.CambiarEncabezado ? 1 : 0);
            cmd.Parameters.AddWithValue("@ConceptoPoliza", LegacySp.Texto(r.ConceptoPoliza));
            cmd.Parameters.AddWithValue("@CuentaDebe", LegacySp.Texto(r.CuentaDebe));
            cmd.Parameters.AddWithValue("@CuentaHaber", LegacySp.Texto(r.CuentaHaber));
            cmd.Parameters.AddWithValue("@NombreCuentaBancaria", LegacySp.Texto(r.NombreCuentaBancaria));
            cmd.Parameters.AddWithValue("@BancoCuenta", LegacySp.Texto(r.BancoCuenta));
            cmd.Parameters.AddWithValue("@BancoSubCuenta", LegacySp.Texto(r.BancoSubCuenta));
            cmd.Parameters.AddWithValue("@ChequePorcentaje", r.ChequePorcentaje);
            cmd.Parameters.AddWithValue("@ChequeIdentificador", LegacySp.Texto(r.ChequeIdentificador));
            cmd.Parameters.AddWithValue("@RevisadoPor", LegacySp.Texto(r.RevisadoPor));
            cmd.Parameters.AddWithValue("@AutorizadoPor", LegacySp.Texto(r.AutorizadoPor));
            cmd.Parameters.AddWithValue("@FechaCheques", LegacySp.ElDia(r.FechaCheques));
            cmd.Parameters.AddWithValue("@DiaCheques", r.DiaCheques);
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<ResultadoSp> CancelarAsync(int idPoliza, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpCancelar, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDPoliza", idPoliza);
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<ResultadoSp> ActualizarEncabezadoAsync(ActualizarAviacionPolizaEncabezadoRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpActualizarEncabezado, cmd =>
        {
            /*
              EL SP NO RECIBE EL ID DEL ENCABEZADO.

              sp_n_ActualizarPolizaEncabezado recibe solo los campos y la firma:
              no hay @IDPolizaEncabezado. O sea que la instalacion tiene UN
              encabezado y el procedimiento lo actualiza sin preguntar cual. Se
              respeta tal cual, pero queda escrito porque la ruta tiene pinta de
              recibir un id y no lo recibe.
            */
            cmd.Parameters.AddWithValue("@ConceptoPoliza", LegacySp.Texto(r.ConceptoPoliza));
            cmd.Parameters.AddWithValue("@CuentaDebe", LegacySp.Texto(r.CuentaDebe));
            cmd.Parameters.AddWithValue("@CuentaHaber", LegacySp.Texto(r.CuentaHaber));
            cmd.Parameters.AddWithValue("@NombreCuentaBancaria", LegacySp.Texto(r.NombreCuentaBancaria));
            cmd.Parameters.AddWithValue("@BancoCuenta", LegacySp.Texto(r.BancoCuenta));
            cmd.Parameters.AddWithValue("@BancoSubCuenta", LegacySp.Texto(r.BancoSubCuenta));
            cmd.Parameters.AddWithValue("@ChequePorcentaje", r.ChequePorcentaje);
            cmd.Parameters.AddWithValue("@ChequeIdentificador", LegacySp.Texto(r.ChequeIdentificador));
            cmd.Parameters.AddWithValue("@RevisadoPor", LegacySp.Texto(r.RevisadoPor));
            cmd.Parameters.AddWithValue("@AutorizadoPor", LegacySp.Texto(r.AutorizadoPor));
            Firma(cmd, idUsuario, equipo);
        }, ct);

    public Task<ResultadoSp> ActualizarChequeAsync(int idPolizaCheque, ActualizarAviacionPolizaChequeRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => EjecutarAsync(SpActualizarCheque, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDPoliza", r.IdPoliza);
            cmd.Parameters.AddWithValue("@IDPolizaCheque", idPolizaCheque);
            cmd.Parameters.AddWithValue("@NumCheque", r.NumCheque);
            cmd.Parameters.AddWithValue("@NumPoliza", r.NumPoliza);
            Firma(cmd, idUsuario, equipo);
        }, ct);
}
