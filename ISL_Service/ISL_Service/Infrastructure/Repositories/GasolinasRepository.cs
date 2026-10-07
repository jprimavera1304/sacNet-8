using System.Data;
using ISL_Service.Application.DTOs.Gasolinas;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/*
  RECIBOS DE GASOLINA.

  Los cuatro sp_n_ estan en Produccion_svr con la misma firma que espera Legacy,
  asi que esto SI se compara.

  LAS FECHAS, OTRA VEZ
  --------------------
  sp_n_ConsultarGasolinas recibe las cuatro fechas como varchar(20) y las
  concatena. Legacy les volteaba el dia y el mes leyendo Constantes.formatoFechaDB
  con sp_n_ConsultaConstantes, el procedimiento que cierra el dia del negocio.
  Aqui van tipadas y en ISO: la consulta de gasolinas ya no escribe nada.

  Y EL FILTRO POR NUMERO DE RECIBO
  --------------------------------
  Legacy, al recibir el numero de recibo, reconstruia el DTO y tiraba el auto, el
  repartidor y las cuatro fechas (GasolinaService.cs:46-53). Buscar "el recibo
  12345 de este repartidor" devolvia el recibo 12345 de cualquiera. Aqui todos los
  filtros se combinan.
*/
public class GasolinasRepository : IGasolinasRepository
{
    private readonly IConfiguration _configuration;

    private const string SpConsultar = "dbo.sp_n_ConsultarGasolinas";
    private const string SpInsertar = "dbo.sp_n_InsertarGasolina";
    private const string SpActualizar = "dbo.sp_n_ActualizarGasolina";
    private const string SpCancelar = "dbo.sp_n_CancelarGasolina";

    public GasolinasRepository(IConfiguration configuration)
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

    private static void Firma(SqlCommand cmd, int idUsuario, string equipo)
    {
        cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
        cmd.Parameters.AddWithValue("@Equipo", LegacySp.Texto(equipo));
    }

    public async Task<List<Dictionary<string, object?>>> ConsultarAsync(GasolinasFiltro f, CancellationToken ct = default)
        => LegacySp.Renglones(await TablaAsync(SpConsultar, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDGasolina", f.IdGasolina);
            cmd.Parameters.AddWithValue("@IDAuto", f.IdAuto);
            cmd.Parameters.AddWithValue("@IDRepartidor", f.IdRepartidor);
            cmd.Parameters.AddWithValue("@Existe", f.Existe);
            cmd.Parameters.AddWithValue("@NumeroRecibo", LegacySp.Filtro(f.NumeroRecibo));
            cmd.Parameters.AddWithValue("@FechaInicial", LegacySp.DesdeElDia(f.FechaInicial));
            cmd.Parameters.AddWithValue("@FechaFinal", LegacySp.HastaElDia(f.FechaFinal ?? f.FechaInicial));
            cmd.Parameters.AddWithValue("@FechaInicialCaptura", LegacySp.DesdeElDia(f.FechaInicialCaptura));
            cmd.Parameters.AddWithValue("@FechaFinalCaptura", LegacySp.HastaElDia(f.FechaFinalCaptura ?? f.FechaInicialCaptura));
        }, ct));

    public async Task<ResultadoSp> InsertarAsync(CrearGasolinaRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => LegacySp.LeerResultado(await TablaAsync(SpInsertar, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDAuto", r.IdAuto);
            cmd.Parameters.AddWithValue("@IDRepartidor", r.IdRepartidor);
            cmd.Parameters.AddWithValue("@FechaRecibo", LegacySp.ElDia(r.FechaRecibo));
            cmd.Parameters.AddWithValue("@NumeroRecibo", LegacySp.Texto(r.NumeroRecibo));
            cmd.Parameters.AddWithValue("@KmInicial", r.KmInicial);
            cmd.Parameters.AddWithValue("@KmFinal", r.KmFinal);
            cmd.Parameters.AddWithValue("@PrecioLitro", r.PrecioLitro);
            cmd.Parameters.AddWithValue("@TotalRecibo", r.TotalRecibo);
            Firma(cmd, idUsuario, equipo);
        }, ct));

    public async Task<ResultadoSp> ActualizarAsync(int idGasolina, ActualizarGasolinaRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => LegacySp.LeerResultado(await TablaAsync(SpActualizar, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDGasolina", idGasolina);
            cmd.Parameters.AddWithValue("@IDAuto", r.IdAuto);
            cmd.Parameters.AddWithValue("@IDRepartidor", r.IdRepartidor);
            cmd.Parameters.AddWithValue("@FechaRecibo", LegacySp.ElDia(r.FechaRecibo));
            cmd.Parameters.AddWithValue("@NumeroRecibo", LegacySp.Texto(r.NumeroRecibo));
            cmd.Parameters.AddWithValue("@KmInicial", r.KmInicial);
            cmd.Parameters.AddWithValue("@KmFinal", r.KmFinal);
            cmd.Parameters.AddWithValue("@PrecioLitro", r.PrecioLitro);
            cmd.Parameters.AddWithValue("@TotalRecibo", r.TotalRecibo);
            Firma(cmd, idUsuario, equipo);
        }, ct));

    public async Task<List<Dictionary<string, object?>>> CancelarAsync(CancelarGasolinasRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => LegacySp.Renglones(await TablaAsync(SpCancelar, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDsGasolina", LegacySp.Lista(r.IdsGasolina));
            // Los numeros de recibo van como texto y pueden traer apostrofo:
            // el SP los concatena para la bitacora, asi que se sanean.
            cmd.Parameters.AddWithValue("@NumerosRecibo",
                string.Join(LegacySp.Delimitador, r.NumerosRecibo.Select(LegacySp.Filtro)));
            Firma(cmd, idUsuario, equipo);
        }, ct));
}
