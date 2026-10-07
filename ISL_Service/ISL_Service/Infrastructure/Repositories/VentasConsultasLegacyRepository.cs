using ISL_Service.Application.DTOs.VentasConsultasLegacy;
using ISL_Service.Application.DTOs.VentasEscrituras;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Infrastructure.Repositories;

/*
  LAS SIETE CABECERAS DE PANTALLA DE VENTAS

  Lo que se corrige de legacy, con el motivo de cada cosa:

  1) NINGUNA LLAMA A sp_n_ConsultaConstantes. Era el primer paso de todas, solo
     para leer `formatoFechaDB`, y ese procedimiento hace el cierre del dia.
     Mandando las fechas en formato ISO el DATEFORMAT deja de importar.

  2) LA FECHA INICIAL DE LOS PAGOS DE CENTRO DE SERVICIO LLEVA 00:00:00.
     Legacy le pega " 23:59:59" a LAS DOS (findCSAsync), asi que un filtro
     "del dia 5 al dia 5" empieza a las 23:59:59 y termina a las 23:59:59: deja
     fuera el dia entero menos el ultimo segundo. Esta escrito literal en su
     codigo y no es una convencion, es un dedazo. No se hereda.

  3) EL RANGO DE PAGO SE CONDICIONA POR SU INICIO, no por su fin. Legacy
     pregunta `if (FechaPagoFinal != "")` para decidir si formatea el PAR
     completo, asi que mandar solo la inicial la dejaba cruda y mandar solo la
     final formateaba una inicial vacia. Aqui cada fecha va sola si va sola.

  4) @Presentacion y @IDsUsuario del reporte de folios SI SE MANDAN. El
     procedimiento los declara y legacy se quedo en ocho de once parametros, asi
     que esos filtros existian en la base y no en la pantalla. Mandarlos en cero
     y vacio da exactamente el comportamiento de hoy.
*/
public class VentasConsultasLegacyRepository : SpLegacyRepositoryBase, IVentasConsultasLegacyRepository
{
    private const string SpPedidoDetalle = "sp_n_ConsultaVentasPedidoDetalle";
    private const string SpTransito = "sp_n_ConsultaVentasTransito";
    private const string SpPendientes = "sp_n_ConsultaVentasPendiente";
    private const string SpPagos = "sp_n_ConsultaVentasPagos";
    private const string SpNuevosPagos = "sp_n_ConsultaVentasNuevosPagos";
    private const string SpPagosCs = "sp_n_ConsultarVentasPagosCS";
    private const string SpPagosLiquidacion = "sp_n_ConsultaVentasPagosLiquidacion";
    private const string SpFolios = "sp_n_rptVentasFolios";

    private const int LargoLista = 4000;
    private const int LargoFecha20 = 20;
    private const int LargoFecha100 = 100;

    public VentasConsultasLegacyRepository(IConfiguration configuration) : base(configuration) { }

    public async Task<FilasLegacyDto> ConsultarPedidoDetalleAsync(
        VentasPedidoDetalleConsultaRequest request, int idUsuarioActual, CancellationToken ct)
    {
        var filas = await EjecutarFilasAsync(SpPedidoDetalle, cmd =>
        {
            cmd.Parameters.Add(Varchar("@IDsVenta", LargoLista, FiltrosLegacy.Ids(request.IdsVenta)));
            cmd.Parameters.Add(Entero("@IDVenta", request.IdVenta));
            cmd.Parameters.Add(Entero("@IDEmpresa", request.IdEmpresa));
            cmd.Parameters.Add(Entero("@IDCliente", request.IdCliente));
            cmd.Parameters.Add(Entero("@IDUsuario", request.IdUsuario));
            cmd.Parameters.Add(Entero("@IDAgente", request.IdAgente));
            cmd.Parameters.Add(Entero("@IDTipoDocumento", request.IdTipoDocumento));
            cmd.Parameters.Add(Entero("@FolioInicial", request.FolioInicial));
            cmd.Parameters.Add(Entero("@FolioFinal", request.FolioFinal));
            cmd.Parameters.Add(Varchar("@FechaEmisionInicial", LargoFecha20, FiltrosLegacy.FechaDesde(request.FechaEmisionInicial)));
            cmd.Parameters.Add(Varchar("@FechaEmisionFinal", LargoFecha20, FiltrosLegacy.FechaHasta(request.FechaEmisionFinal)));
            cmd.Parameters.Add(Varchar("@FechaCancelInicial", LargoFecha20, FiltrosLegacy.FechaDesde(request.FechaCancelInicial)));
            cmd.Parameters.Add(Varchar("@FechaCancelFinal", LargoFecha20, FiltrosLegacy.FechaHasta(request.FechaCancelFinal)));
            cmd.Parameters.Add(Varchar("@FechaPagoInicial", LargoFecha20, FiltrosLegacy.FechaDesde(request.FechaPagoInicial)));
            cmd.Parameters.Add(Varchar("@FechaPagoFinal", LargoFecha20, FiltrosLegacy.FechaHasta(request.FechaPagoFinal)));
            cmd.Parameters.Add(Entero("@Formato", FiltrosLegacy.Presentacion(request.Formato)));
            cmd.Parameters.Add(Entero("@IDUsuarioActual", idUsuarioActual));
            cmd.Parameters.Add(Varchar("@IDsClientes", LargoLista, FiltrosLegacy.Ids(request.IdsCliente)));
            cmd.Parameters.Add(Varchar("@IDsProducto", LargoLista, FiltrosLegacy.Ids(request.IdsProducto)));
            cmd.Parameters.Add(Entero("@IDProducto", request.IdProducto));
        }, ct);

        return new FilasLegacyDto { Rows = filas };
    }

    public async Task<FilasLegacyDto> ConsultarTransitoAsync(
        VentasTransitoConsultaRequest request, CancellationToken ct)
    {
        var filas = await EjecutarFilasAsync(SpTransito, cmd =>
        {
            cmd.Parameters.Add(Entero("@StatusInventario", request.StatusInventario));
            cmd.Parameters.Add(Entero("@IDCliente", request.IdCliente));
            cmd.Parameters.Add(Varchar("@FechaEmisionInicial", LargoFecha20, FiltrosLegacy.FechaDesde(request.FechaEmisionInicial)));
            cmd.Parameters.Add(Varchar("@FechaEmisionFinal", LargoFecha20, FiltrosLegacy.FechaHasta(request.FechaEmisionFinal)));
            cmd.Parameters.Add(Entero("@Agrupado", request.Agrupado));
        }, ct);

        return new FilasLegacyDto { Rows = filas };
    }

    public async Task<FilasLegacyDto> ConsultarPendientesAsync(
        VentasPendienteConsultaRequest request, CancellationToken ct)
    {
        var filas = await EjecutarFilasAsync(SpPendientes, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDVentaPendiente", request.IdVentaPendiente));
            cmd.Parameters.Add(Entero("@IDVenta", request.IdVenta));
            cmd.Parameters.Add(Entero("@IDEmpresa", request.IdEmpresa));
            cmd.Parameters.Add(Entero("@IDCliente", request.IdCliente));
            cmd.Parameters.Add(Entero("@IDUsuario", request.IdUsuario));
            cmd.Parameters.Add(Entero("@IDAgente", request.IdAgente));
            cmd.Parameters.Add(Entero("@IDTipoDocumento", request.IdTipoDocumento));
            cmd.Parameters.Add(Entero("@IDAlmacen", request.IdAlmacen));
            cmd.Parameters.Add(Entero("@IDUsuarioAutorizacion", request.IdUsuarioAutorizacion));
            cmd.Parameters.Add(Entero("@IDUsuarioRechazo", request.IdUsuarioRechazo));
            cmd.Parameters.Add(Varchar("@FechaEmisionInicial", LargoFecha20, FiltrosLegacy.FechaDesde(request.FechaEmisionInicial)));
            cmd.Parameters.Add(Varchar("@FechaEmisionFinal", LargoFecha20, FiltrosLegacy.FechaHasta(request.FechaEmisionFinal)));
            cmd.Parameters.Add(Varchar("@FechaAutorizacionInicial", LargoFecha20, FiltrosLegacy.FechaDesde(request.FechaAutorizacionInicial)));
            cmd.Parameters.Add(Varchar("@FechaAutorizacionFinal", LargoFecha20, FiltrosLegacy.FechaHasta(request.FechaAutorizacionFinal)));
            cmd.Parameters.Add(Varchar("@FechaRechazoInicial", LargoFecha20, FiltrosLegacy.FechaDesde(request.FechaRechazoInicial)));
            cmd.Parameters.Add(Varchar("@FechaRechazoFinal", LargoFecha20, FiltrosLegacy.FechaHasta(request.FechaRechazoFinal)));
            cmd.Parameters.Add(Entero("@Formato", FiltrosLegacy.Presentacion(request.Formato)));
            cmd.Parameters.Add(Entero("@Filtrar", request.Filtrar));
        }, ct);

        return new FilasLegacyDto { Rows = filas };
    }

    public async Task<FilasLegacyDto> ConsultarPagosAsync(
        VentasPagosConsultaRequest request, CancellationToken ct)
    {
        var filas = await EjecutarFilasAsync(SpPagos, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDVenta", request.IdVenta));
            cmd.Parameters.Add(Entero("@IDPagoVenta", request.IdPagoVenta));
            cmd.Parameters.Add(Entero("@DiferenciaUsados", request.DiferenciaUsados));
            cmd.Parameters.Add(Entero("@IDCobro", request.IdCobro));
            cmd.Parameters.Add(Entero("@DineroExcedente", request.DineroExcedente));
        }, ct);

        return new FilasLegacyDto { Rows = filas };
    }

    public async Task<FilasLegacyDto> ConsultarNuevosPagosAsync(
        VentasNuevosPagosConsultaRequest request, CancellationToken ct)
    {
        /* Cuatro parametros: el procedimiento NO declara @IDVenta, aunque la
           cadena de traza de legacy lo nombre. */
        var filas = await EjecutarFilasAsync(SpNuevosPagos, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDPagoVenta", request.IdPagoVenta));
            cmd.Parameters.Add(Entero("@IDCliente", request.IdCliente));
            cmd.Parameters.Add(Varchar("@FechaInicial", LargoFecha100, FiltrosLegacy.FechaDesde(request.FechaInicial)));
            cmd.Parameters.Add(Varchar("@FechaFinal", LargoFecha100, FiltrosLegacy.FechaHasta(request.FechaFinal)));
        }, ct);

        return new FilasLegacyDto { Rows = filas };
    }

    public async Task<FilasLegacyDto> ConsultarPagosCentroServicioAsync(
        VentasPagosCentroServicioConsultaRequest request, CancellationToken ct)
    {
        var filas = await EjecutarFilasAsync(SpPagosCs, cmd =>
        {
            cmd.Parameters.Add(Varchar("@IDTipoPago", LargoLista, FiltrosLegacy.Ids(request.IdsTipoPago)));
            cmd.Parameters.Add(Entero("@IDCliente", request.IdCliente));
            /* 00:00:00 en la inicial. Legacy pone 23:59:59 en las dos, y con eso
               un rango de un dia deja fuera el dia. */
            cmd.Parameters.Add(Varchar("@FechaInicial", LargoFecha100, FiltrosLegacy.FechaDesde(request.FechaInicial)));
            cmd.Parameters.Add(Varchar("@FechaFinal", LargoFecha100, FiltrosLegacy.FechaHasta(request.FechaFinal)));
            cmd.Parameters.Add(Entero("@IDCorte", request.IdCorte));
        }, ct);

        return new FilasLegacyDto { Rows = filas };
    }

    public async Task<FilasLegacyDto> ConsultarPagosLiquidacionAsync(
        VentasPagosLiquidacionConsultaRequest request, CancellationToken ct)
    {
        var filas = await EjecutarFilasAsync(SpPagosLiquidacion, cmd =>
        {
            cmd.Parameters.Add(Varchar("@FechaLiquidacion", LargoFecha100, FiltrosLegacy.Fecha(request.FechaLiquidacion)));
        }, ct);

        return new FilasLegacyDto { Rows = filas };
    }

    public async Task<FilasLegacyDto> ConsultarFoliosAsync(
        VentasFoliosConsultaRequest request, CancellationToken ct)
    {
        var filas = await EjecutarFilasAsync(SpFolios, cmd =>
        {
            cmd.Parameters.Add(Varchar("@IDsEmpresa", LargoLista, FiltrosLegacy.Ids(request.IdsEmpresa)));
            cmd.Parameters.Add(Varchar("@IDsAlmacen", LargoLista, FiltrosLegacy.Ids(request.IdsAlmacen)));
            cmd.Parameters.Add(Varchar("@IDsTipoDocumento", LargoLista, FiltrosLegacy.Ids(request.IdsTipoDocumento)));
            cmd.Parameters.Add(Entero("@IDCliente", request.IdCliente));
            cmd.Parameters.Add(Entero("@IDAgente", request.IdAgente));
            cmd.Parameters.Add(Varchar("@FechaInicial", LargoFecha100, FiltrosLegacy.FechaDesde(request.FechaInicial)));
            cmd.Parameters.Add(Varchar("@FechaFinal", LargoFecha100, FiltrosLegacy.FechaHasta(request.FechaFinal)));
            cmd.Parameters.Add(Entero("@StatusFolio", request.StatusFolio));
            cmd.Parameters.Add(Entero("@Presentacion", FiltrosLegacy.Presentacion(request.Presentacion)));
            cmd.Parameters.Add(Varchar("@IDsUsuario", LargoLista, FiltrosLegacy.Ids(request.IdsUsuario)));
            cmd.Parameters.Add(Entero("@Formato", FiltrosLegacy.Presentacion(request.Formato)));
        }, ct);

        return new FilasLegacyDto { Rows = filas };
    }
}
