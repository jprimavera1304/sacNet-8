using ISL_Service.Application.DTOs.ConsultasLegacy;
using ISL_Service.Application.DTOs.VentasCobranza;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// Las siete lecturas de la cobranza de Mac31, con los mismos
/// `sp_n_Consultar*` y los mismos parametros que MacServicios2.
/// </summary>
public class VentasCobranzaConsultaRepository : ConsultaLegacyRepositoryBase, IVentasCobranzaConsultaRepository
{
    private const string SpCobranzaFolio = "dbo.sp_n_ConsultarCobranzaFolio";
    private const string SpCobranzaDetalle = "dbo.sp_n_ConsultarCobranzaDetalle";
    private const string SpConcentradoFolio = "dbo.sp_n_ConsultarConcentradoFolio";
    private const string SpConcentradoDetalle = "dbo.sp_n_ConsultarConcentradoDetalle";
    private const string SpCobrosCentroServicio = "dbo.sp_n_ConsultarCobrosCentroServicio";
    private const string SpFoliosUsados = "dbo.sp_n_ConcentradoFoliosUsados";
    private const string SpCobroPagos = "dbo.sp_n_ConsultaVentasCobroPagos";
    private const string SpFoliosSinAsignar = "dbo.sp_n_rptVentasFoliosSinAsignar";
    private const string SpPagosKilos = "dbo.sp_n_ConsultarVentasPagosKilos";

    public VentasCobranzaConsultaRepository(IConfiguration configuration) : base(configuration)
    {
    }

    /*
      EL FOLIO DE LA VENTA VA DONDE LEGACY LO PONE, NO DONDE SU NOMBRE SUGIERE

      El DTO de legacy se llama `FolioInicialVenta` y el parametro del
      procedimiento `@FolioVenta`: es el MISMO dato, no un rango
      (ConsultaCobranzasRepository.cs:178 de MacServicios2 lo pasa asi). Aqui el
      campo se llama `FolioVenta`, que es lo que de verdad es.

      Y `@IDCobranzaActual` recibe `IDCobranza` del DTO de legacy, no su campo
      `IDCobranzaActual`, que nunca se usa (ConsultaCobranzasRepository.cs:179).
      Aqui se expone un solo campo, `IDCobranzaActual`, y se manda a ese
      parametro: un campo que nadie lee solo sirve para confundir.
    */
    public Task<ConsultaFilasResponse> ValidarFolioCobranzaAsync(CobranzaFolioRequest request, int idUsuarioActual, CancellationToken ct)
    {
        return EjecutarFilasAsync(SpCobranzaFolio, p =>
        {
            p.AddWithValue("@IDEmpresaVenta", request.IDEmpresaVenta);
            p.AddWithValue("@FolioVenta", Folio(request.FolioVenta));
            p.AddWithValue("@IDCobranzaActual", request.IDCobranzaActual);
            /*
              EL USUARIO SALE DEL TOKEN, NO DEL CUERPO.

              No es cosmetico: con `@IDUsuarioActual` el procedimiento decide la
              columna `ValidoAlmacen` ("LA NOTA PERTENECE AL ALMACEN X / NO TIENE
              ACCESO A ESE ALMACEN"), comparando contra `UsuariosAlmacenes`. En
              legacy ese id viaja en el body, asi que cualquiera podia pedir la
              validacion con el id de otro y saltarse el candado de almacen.
            */
            p.AddWithValue("@IDUsuarioActual", idUsuarioActual);
            p.AddWithValue("@IDTipoDocumento", request.IDTipoDocumento);
        }, ct);
    }

    public Task<ConsultaFilasResponse> ConsultarCobranzaDetalleAsync(int idCobranza, int formato, CancellationToken ct)
    {
        return EjecutarFilasAsync(SpCobranzaDetalle, p =>
        {
            p.AddWithValue("@IDCobranza", idCobranza);
            /* `@Presentacion` es el enum de Mac31: 0 pantalla, 1 impresora, 2 pdf.
               Cambia las columnas que devuelve el procedimiento, por eso se expone. */
            p.AddWithValue("@Presentacion", formato);
        }, ct);
    }

    public Task<ConsultaFilasResponse> ValidarFolioConcentradoAsync(ConcentradoFolioRequest request, int idUsuarioActual, CancellationToken ct)
    {
        return EjecutarFilasAsync(SpConcentradoFolio, p =>
        {
            p.AddWithValue("@IDEmpresaVenta", request.IDEmpresaVenta);
            p.AddWithValue("@FolioVenta", Folio(request.FolioVenta));
            p.AddWithValue("@IDConcentradoActual", request.IDConcentradoActual);
            p.AddWithValue("@IDUsuarioActual", idUsuarioActual);
            p.AddWithValue("@IDTipoDocumento", request.IDTipoDocumento);
        }, ct);
    }

    public Task<ConsultaFilasResponse> ConsultarConcentradoDetalleAsync(int idConcentrado, CancellationToken ct)
    {
        return EjecutarFilasAsync(SpConcentradoDetalle, p =>
            p.AddWithValue("@IDConcentrado", idConcentrado), ct);
    }

    public Task<ConsultaFilasResponse> ConsultarCobrosCentroServicioAsync(int idCobro, int folioCobro, CancellationToken ct)
    {
        return EjecutarFilasAsync(SpCobrosCentroServicio, p =>
        {
            p.AddWithValue("@IDCobro", idCobro);
            p.AddWithValue("@FolioCobro", folioCobro);
        }, ct);
    }

    public Task<ConsultaFilasResponse> ConsultarFoliosUsadosAsync(CobroFoliosUsadosRequest request, CancellationToken ct)
    {
        return EjecutarFilasAsync(SpFoliosUsados, p =>
        {
            p.AddWithValue("@IDConcentrado", request.IDConcentrado);
            p.AddWithValue("@IDCobranza", request.IDCobranza);
            p.AddWithValue("@IDCobro", request.IDCobro);
            p.AddWithValue("@SoloConcentrado", request.SoloConcentrado ? 1 : 0);
            p.AddWithValue("@SoloCobranza", request.SoloCobranza ? 1 : 0);
        }, ct);
    }

    public Task<ConsultaFilasResponse> ConsultarCobroPagosAsync(CobroPagosRequest request, CancellationToken ct)
    {
        return EjecutarFilasAsync(SpCobroPagos, p =>
        {
            p.AddWithValue("@IDConcentrado", request.IDConcentrado);
            p.AddWithValue("@IDCobranza", request.IDCobranza);
            p.AddWithValue("@IDCobro", request.IDCobro);
            p.AddWithValue("@Formato", request.Formato);
        }, ct);
    }

    /*
      LOS FOLIOS QUE NO ESTAN EN NINGUN CONCENTRADO

      Pertenece a la cobranza y no a los reportes aunque el procedimiento se
      llame `rpt`: lo que contesta es "¿que notas se entregaron y todavia no
      estan en un concentrado?", y en el renglon la columna `Repartidor` dice
      literalmente "SIN ASIGNAR" (comprobado ejecutandolo contra Produccion_svr:
      285 folios).

      `@IDsEmpresa` va con virgulilla, como todas las listas de esta familia
      (`Variables.DELIMITADOR_PARAM_TILDE_SP`, VentasFoliosService.cs:43).
    */
    public Task<ConsultaFilasResponse> ConsultarFoliosSinAsignarAsync(IEnumerable<int> idsEmpresa, int devueltasA, CancellationToken ct)
    {
        return EjecutarFilasAsync(SpFoliosSinAsignar, p =>
        {
            p.AddWithValue("@IDsEmpresa", Ids(idsEmpresa, null));
            p.AddWithValue("@DevueltasA", devueltasA);
        }, ct);
    }

    /*
      PAGOS POR KILO

      Legacy pasa `FechaInicial` y `FechaFinal` **tal como vienen del cuerpo**,
      sin `ConvertFtmDate` y sin pegarles la hora
      (ConsultaVentasPagosService.findKilosAsync, :72-96 de MacServicios2: ni
      lee Constantes ni toca las fechas). O sea que un rango escrito en el
      formato de la pantalla —"01-02-2026"— llega a un procedimiento que espera
      el formato de la base, y en esta instalacion `formatoFechaDB` vale
      "MM-dd-yyyy": el 1 de febrero se lee como el 2 de enero. Silencioso.

      Aqui se normaliza a ISO como en todas las demas, asi que el rango se
      respeta. Es una diferencia deliberada y esta anotada en el caso del
      comparador; para que la comparacion fuera limpia se compara sin rango, que
      es como la pantalla lo pide por omision.
    */
    public Task<ConsultaFilasResponse> ConsultarPagosKilosAsync(PagosKilosRequest request, CancellationToken ct)
    {
        return EjecutarFilasAsync(SpPagosKilos, p =>
        {
            p.AddWithValue("@IDVentaPagoKilo", request.IDVentaPagoKilo);
            p.AddWithValue("@IDCobro", request.IDCobro);
            p.AddWithValue("@IDVenta", request.IDVenta);
            p.AddWithValue("@IDCliente", request.IDCliente);
            p.AddWithValue("@FechaInicial", Fecha(request.FechaInicial, false));
            p.AddWithValue("@FechaFinal", Fecha(request.FechaFinal, true));
        }, ct);
    }
}
