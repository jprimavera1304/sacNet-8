using System.Data;
using ISL_Service.Application.DTOs.Cobranzas;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/*
  COBRANZAS.

  Los siete sp_n_ de este archivo estan en Produccion_svr con la firma que espera
  Legacy: es material comparable.

  DOS COSAS QUE NO SE HEREDAN
  ---------------------------
  1. LA RAMA MUERTA DEL FILTRO POR FOLIO DE VENTA.
     ConsultaCobranzasService.cs tiene esto:

         if (FolioInicialCobranza != "0") { ...reconstruye el DTO con el folio de COBRANZA... }
         else if (FolioInicialCobranza != "0") { ...reconstruye con el folio de VENTA... }

     La segunda condicion es la PRIMERA repetida —un copiar y pegar sin cambiar el
     campo—, asi que esa rama NO SE EJECUTA NUNCA. Buscar por folio de venta nunca
     hizo lo que dice su comentario; caia al else y el folio de venta se mandaba
     junto con todo lo demas. Aqui no hay ramas: todos los filtros se combinan,
     que es lo que la rama muerta pretendia hacer mal.

     De paso, la reconstruccion del DTO tambien perdia @IDUsuarioActual, que es lo
     que el SP usa para saber que cobranzas puede ver quien pregunta.

  2. EL "0" PEGADO DELANTE DEL FOLIO.
     Legacy hacia `FolioFinalVenta = "0" + FolioFinalVenta` (linea 48) para que la
     cadena vacia se volviera "0". Aqui el folio es un int y el cero es cero.

  Y LAS FECHAS: tipadas y en ISO, sin leer Constantes.formatoFechaDB y por tanto
  sin llamar al procedimiento que cierra el dia.
*/
public class CobranzasRepository : ICobranzasRepository
{
    private readonly IConfiguration _configuration;

    private const string SpConsultar = "dbo.sp_n_ConsultarCobranzas";
    private const string SpConsultarDetalle = "dbo.sp_n_ConsultarCobranzaDetalle";
    private const string SpValidarFolio = "dbo.sp_n_ConsultarCobranzaFolio";
    private const string SpInsertar = "dbo.sp_n_InsertarCobranzaDetalle";
    private const string SpActualizarDetalle = "dbo.sp_n_ActualizarCobranzaDetalle";
    private const string SpCancelar = "dbo.sp_n_CancelarCobranza";
    private const string SpPendientesVencidos = "dbo.sp_n_rptVentasSaldosPrincipal";

    private const string SqlFechaOperacion = "SELECT TOP 1 FechaOperacion FROM dbo.Constantes;";

    public CobranzasRepository(IConfiguration configuration)
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

    public async Task<List<Dictionary<string, object?>>> ConsultarAsync(CobranzasFiltro f, int idUsuarioActual, CancellationToken ct = default)
        => LegacySp.Renglones(await TablaAsync(SpConsultar, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCobranza", f.IdCobranza);
            cmd.Parameters.AddWithValue("@IDRepartidor", f.IdRepartidor);
            cmd.Parameters.AddWithValue("@IDUsuarioCobranza", f.IdUsuarioCobranza);
            cmd.Parameters.AddWithValue("@FolioInicialCobranza", f.FolioInicialCobranza);
            cmd.Parameters.AddWithValue("@FolioFinalCobranza", f.FolioFinalCobranza);
            cmd.Parameters.AddWithValue("@FechaInicialCobranza", LegacySp.DesdeElDia(f.FechaInicialCobranza));
            cmd.Parameters.AddWithValue("@FechaFinalCobranza", LegacySp.HastaElDia(f.FechaFinalCobranza ?? f.FechaInicialCobranza));
            cmd.Parameters.AddWithValue("@IDEmpresaVenta", f.IdEmpresaVenta);
            cmd.Parameters.AddWithValue("@FolioInicialVenta", f.FolioInicialVenta);
            cmd.Parameters.AddWithValue("@FolioFinalVenta", f.FolioFinalVenta);
            // Quien pregunta. Del token, nunca del cuerpo.
            cmd.Parameters.AddWithValue("@IDUsuarioActual", idUsuarioActual);
        }, ct));

    public async Task<List<Dictionary<string, object?>>> ConsultarDetalleAsync(CobranzaDetalleFiltro f, CancellationToken ct = default)
        => LegacySp.Renglones(await TablaAsync(SpConsultarDetalle, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCobranza", f.IdCobranza);
            cmd.Parameters.AddWithValue("@Presentacion", f.Presentacion);
        }, ct));

    public async Task<List<Dictionary<string, object?>>> ValidarFolioAsync(CobranzaValidarFolioFiltro f, int idUsuarioActual, CancellationToken ct = default)
        => LegacySp.Renglones(await TablaAsync(SpValidarFolio, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDEmpresaVenta", f.IdEmpresaVenta);
            cmd.Parameters.AddWithValue("@FolioVenta", f.FolioVenta);
            cmd.Parameters.AddWithValue("@IDCobranzaActual", f.IdCobranzaActual);
            cmd.Parameters.AddWithValue("@IDUsuarioActual", idUsuarioActual);
            cmd.Parameters.AddWithValue("@IDTipoDocumento", f.IdTipoDocumento);
        }, ct));

    public async Task<ResultadoSp> InsertarAsync(CrearCobranzaRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => LegacySp.LeerResultado(await TablaAsync(SpInsertar, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDRepartidor", r.IdRepartidor);
            cmd.Parameters.AddWithValue("@IDsVentaAgregar", LegacySp.Lista(r.IdsVenta));
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", LegacySp.Texto(equipo));
            cmd.Parameters.AddWithValue("@Observaciones", LegacySp.Texto(r.Observaciones));
            cmd.Parameters.AddWithValue("@IDCobro", r.IdCobro);
            cmd.Parameters.AddWithValue("@esCobro", r.EsCobro ? 1 : 0);
            cmd.Parameters.AddWithValue("@IDAlmacen", r.IdAlmacen);
        }, ct));

    public async Task<ResultadoSp> ActualizarDetalleAsync(int idCobranza, ActualizarCobranzaDetalleRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => LegacySp.LeerResultado(await TablaAsync(SpActualizarDetalle, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCobranza", idCobranza);
            cmd.Parameters.AddWithValue("@IDsCobranzaDetalleEliminar", LegacySp.Lista(r.IdsDetalleEliminar));
            cmd.Parameters.AddWithValue("@IDsCobranzaDetalleDevolver", LegacySp.Lista(r.IdsDetalleDevolver));
            cmd.Parameters.AddWithValue("@IDsVentaAgregar", LegacySp.Lista(r.IdsVentaAgregar));
            cmd.Parameters.AddWithValue("@IDRepartidor", r.IdRepartidor);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", LegacySp.Texto(equipo));
            cmd.Parameters.AddWithValue("@Observaciones", LegacySp.Texto(r.Observaciones));
        }, ct));

    public async Task<ResultadoSp> CancelarAsync(CancelarCobranzasRequest r, int idUsuario, string equipo, CancellationToken ct = default)
        => LegacySp.LeerResultado(await TablaAsync(SpCancelar, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDsCobranza", LegacySp.Lista(r.IdsCobranza));
            // El SP llama a la firma de otra forma aqui: UsuarioCancelo /
            // EquipoCancelo. Sigue saliendo del token.
            cmd.Parameters.AddWithValue("@IDUsuarioCancelo", idUsuario);
            cmd.Parameters.AddWithValue("@EquipoCancelo", LegacySp.Texto(equipo));
            cmd.Parameters.AddWithValue("@IDsCobro", LegacySp.Lista(r.IdsCobro));
        }, ct));

    /*
      LA FECHA DE OPERACION SALE DE LA BASE, NO DEL CUERPO DE LA PETICION.

      El reporte de cobranza pendiente necesita saber respecto a que dia esta algo
      vencido, y eso es Constantes.FechaOperacion. Legacy lo pedia en el cuerpo
      (CobranzaPendientesVencidosInputDTO.FechaOperacionFtm): quien pidiera el
      reporte podia mandar la fecha que quisiera y hacer que una venta vencida
      saliera como pendiente, o al contrario.

      Se lee con un SELECT TOP 1. No se usa sp_n_ConsultaConstantes, que es la
      rutina de cierre del dia.
    */
    public async Task<DateOnly> LeerFechaDeOperacionAsync(CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SqlFechaOperacion, conn) { CommandType = CommandType.Text };
        var valor = await cmd.ExecuteScalarAsync(ct);

        if (valor is null || valor is DBNull)
            throw new Application.Exceptions.ConflictException(
                "La empresa no tiene fecha de operacion configurada.",
                "dbo.Constantes.FechaOperacion esta vacia.");

        return DateOnly.FromDateTime(Convert.ToDateTime(valor));
    }

    public async Task<List<Dictionary<string, object?>>> ConsultarPendientesVencidosAsync(CobranzaPendientesVencidosFiltro f, DateOnly fechaOperacion, CancellationToken ct = default)
        => LegacySp.Renglones(await TablaAsync(SpPendientesVencidos, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDEmpresa", f.IdEmpresa);
            cmd.Parameters.AddWithValue("@FechaOperacion", LegacySp.HastaElDia(fechaOperacion));
            cmd.Parameters.AddWithValue("@FechaInicial", LegacySp.DesdeElDia(f.FechaInicial));
            cmd.Parameters.AddWithValue("@FechaFinal", LegacySp.HastaElDia(f.FechaFinal));

            /*
              LOS SIETE PARAMETROS QUE DEFINEN ESTA PANTALLA.

              Copiados de CobranzaPendientesVencidosService.cs:45-62, donde Legacy
              los fijaba en C# con un comentario por cada uno. No son del usuario:
              son lo que hace que este reporte sea "cobranza pendiente y vencida" y
              no el reporte general de saldos, que ya existe aparte.
            */
            cmd.Parameters.AddWithValue("@DiasVencimiento", 0);   // 0 = todos
            cmd.Parameters.AddWithValue("@Status", 4);            // 4 = pendientes Y vencidas
            cmd.Parameters.AddWithValue("@Mostrar", 0);           // 0 = contado y credito
            cmd.Parameters.AddWithValue("@Formato", 1);           // 1 = detallado
            cmd.Parameters.AddWithValue("@IDTipoDocumento", 0);   // 0 = todos
            cmd.Parameters.AddWithValue("@IDAlmacen", 0);         // 0 = todos
            cmd.Parameters.AddWithValue("@Criterio", 0);          // 0 = todos

            cmd.Parameters.AddWithValue("@FormatoMostrar", f.FormatoMostrar);
            cmd.Parameters.AddWithValue("@IDAgente", f.IdAgente);
            cmd.Parameters.AddWithValue("@IDCliente", f.IdCliente);
        }, ct));
}
