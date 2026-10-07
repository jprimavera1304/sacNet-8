using ISL_Service.Application.DTOs.VentasConsultasLegacy;
using ISL_Service.Application.DTOs.VentasEscrituras;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Application.Services;

/*
  LOS FILTROS DE LAS CABECERAS DE VENTAS

  La regla de fondo es la misma de toda la ola: SE EXIGE AL MENOS UN FILTRO donde
  el procedimiento, con todo en cero, barre la tabla entera. Estas consultas van
  contra Ventas y [Ventas Pagos], que son las tablas mas grandes del sistema;
  abrir una pantalla sin querer no puede costar un recorrido completo.

  Y se conserva la PRIORIDAD de legacy donde existia, escrita con nombre: una
  lista de remisiones gana sobre un rango de folios, y el rango de folios gana
  sobre los rangos de fecha. El motivo original es bueno — quien pide tres
  remisiones concretas no quiere que el rango de fechas de la pantalla se las
  quite— y lo unico que se cambia es que ya no se tira la identidad por el
  camino.
*/
public class VentasConsultasLegacyService : IVentasConsultasLegacyService
{
    private readonly IVentasConsultasLegacyRepository _repository;

    public VentasConsultasLegacyService(IVentasConsultasLegacyRepository repository)
    {
        _repository = repository;
    }

    public async Task<FilasLegacyDto> ConsultarPedidoDetalleAsync(
        VentasPedidoDetalleConsultaRequest request, int idUsuarioActual, CancellationToken ct)
    {
        request.IdsVenta = FiltrosLegacy.LimpiarIds(request.IdsVenta);
        request.IdsCliente = FiltrosLegacy.LimpiarIds(request.IdsCliente);
        request.IdsProducto = FiltrosLegacy.LimpiarIds(request.IdsProducto);

        var filtro = request;

        if (request.IdsVenta.Count > 0)
        {
            /*
              UNA LISTA DE REMISIONES GANA, PERO NO SE LLEVA LOS CLIENTES.

              Legacy, en esta rama, construye un DTO nuevo con IDsVenta y Formato
              y con eso tira el `IDsClientes` que acababa de calcular tres lineas
              antes. Es decir: pedir remisiones concretas de ciertos clientes
              perdia los clientes, en silencio. Aqui se conservan los filtros que
              acotan la MISMA lista, y se tiran los rangos, que son los que
              estorban.
            */
            filtro = new VentasPedidoDetalleConsultaRequest
            {
                IdsVenta = request.IdsVenta,
                Formato = request.Formato,
                IdsCliente = request.IdsCliente,
                IdsProducto = request.IdsProducto,
                IdProducto = request.IdProducto
            };
        }
        else if (request.FolioInicial > 0)
        {
            filtro = new VentasPedidoDetalleConsultaRequest
            {
                FolioInicial = request.FolioInicial,
                FolioFinal = request.FolioFinal > 0 ? request.FolioFinal : request.FolioInicial,
                IdEmpresa = request.IdEmpresa,
                IdTipoDocumento = request.IdTipoDocumento,
                Formato = request.Formato
            };
        }
        else if (request.TodasLasRemisiones)
        {
            /*
              "TODAS LAS REMISIONES" SE RESUELVE SIN RANGO, NO CON UN RANGO ENORME.

              Legacy pone FechaEmisionInicial = "01-01-2000 00:00:00" literal y la
              final en la fecha de operacion leida de Constantes — que es la
              llamada que dispara el cierre del dia. Quitar el rango da el mismo
              conjunto (el procedimiento lee "" como sin filtro) sin inventar un
              ano 2000 ni tocar Constantes.
            */
            filtro.FechaEmisionInicial = null;
            filtro.FechaEmisionFinal = null;
        }

        var hayFiltro = filtro.IdsVenta.Count > 0 || filtro.IdVenta > 0 || filtro.FolioInicial > 0
            || filtro.IdCliente > 0 || filtro.IdsCliente.Count > 0 || filtro.IdAgente > 0
            || filtro.FechaEmisionInicial is not null || filtro.FechaCancelInicial is not null
            || filtro.FechaPagoInicial is not null || request.TodasLasRemisiones;

        if (!hayFiltro)
            throw new ArgumentException("Hay que indicar al menos un filtro: remision, folio, cliente, agente o rango de fechas.");

        return await _repository.ConsultarPedidoDetalleAsync(filtro, idUsuarioActual, ct);
    }

    public async Task<FilasLegacyDto> ConsultarTransitoAsync(
        VentasTransitoConsultaRequest request, CancellationToken ct)
    {
        if (request.FechaEmisionInicial is null && request.IdCliente <= 0)
            throw new ArgumentException("Hay que indicar el cliente o un rango de fechas de emision.");

        return await _repository.ConsultarTransitoAsync(request, ct);
    }

    public async Task<FilasLegacyDto> ConsultarPendientesAsync(
        VentasPendienteConsultaRequest request, CancellationToken ct)
    {
        var hayFiltro = request.IdVentaPendiente > 0 || request.IdVenta > 0 || request.IdCliente > 0
            || request.IdAgente > 0 || request.IdAlmacen > 0
            || request.FechaEmisionInicial is not null
            || request.FechaAutorizacionInicial is not null
            || request.FechaRechazoInicial is not null
            || request.Filtrar > 0;

        if (!hayFiltro)
            throw new ArgumentException("Hay que indicar al menos un filtro: pendiente, remision, cliente, agente, almacen o rango de fechas.");

        return await _repository.ConsultarPendientesAsync(request, ct);
    }

    public async Task<FilasLegacyDto> ConsultarPagosAsync(
        VentasPagosConsultaRequest request, CancellationToken ct)
    {
        if (request.IdVenta <= 0 && request.IdPagoVenta <= 0 && request.IdCobro <= 0)
            throw new ArgumentException("Hay que indicar la remision, el pago o el cobro.");

        return await _repository.ConsultarPagosAsync(request, ct);
    }

    public async Task<FilasLegacyDto> ConsultarNuevosPagosAsync(
        VentasNuevosPagosConsultaRequest request, CancellationToken ct)
    {
        if (request.IdPagoVenta <= 0 && request.IdCliente <= 0 && request.FechaInicial is null)
            throw new ArgumentException("Hay que indicar el pago, el cliente o un rango de fechas.");

        return await _repository.ConsultarNuevosPagosAsync(request, ct);
    }

    public async Task<FilasLegacyDto> ConsultarPagosCentroServicioAsync(
        VentasPagosCentroServicioConsultaRequest request, CancellationToken ct)
    {
        request.IdsTipoPago = FiltrosLegacy.LimpiarIds(request.IdsTipoPago);

        if (request.IdCliente <= 0 && request.IdCorte <= 0 && request.FechaInicial is null)
            throw new ArgumentException("Hay que indicar el cliente, el corte o un rango de fechas.");

        return await _repository.ConsultarPagosCentroServicioAsync(request, ct);
    }

    public async Task<FilasLegacyDto> ConsultarPagosLiquidacionAsync(
        VentasPagosLiquidacionConsultaRequest request, CancellationToken ct)
    {
        /*
          LA FECHA ES EL UNICO PARAMETRO: sin ella el procedimiento no tiene por
          donde empezar. Legacy la formatea sin comprobarla, asi que con la fecha
          vacia contesta 400 con un error de Substring; aqui lo dice con palabras.
        */
        if (request.FechaLiquidacion is null)
            throw new ArgumentException("La fecha de liquidacion es obligatoria.");

        return await _repository.ConsultarPagosLiquidacionAsync(request, ct);
    }

    public async Task<FilasLegacyDto> ConsultarFoliosAsync(
        VentasFoliosConsultaRequest request, CancellationToken ct)
    {
        request.IdsEmpresa = FiltrosLegacy.LimpiarIds(request.IdsEmpresa);
        request.IdsAlmacen = FiltrosLegacy.LimpiarIds(request.IdsAlmacen);
        request.IdsTipoDocumento = FiltrosLegacy.LimpiarIds(request.IdsTipoDocumento);
        request.IdsUsuario = FiltrosLegacy.LimpiarIds(request.IdsUsuario);

        /*
          EL RANGO DE FECHAS ES OBLIGATORIO.

          Es un reporte de folios: sin fechas recorre todos los folios de todas
          las empresas desde el principio. Legacy no lo pedia y encima reventaba
          si faltaban (formatea las dos sin comprobar), asi que la consulta sin
          rango nunca funciono de todos modos.
        */
        if (request.FechaInicial is null || request.FechaFinal is null)
            throw new ArgumentException("El rango de fechas es obligatorio.");
        if (request.FechaInicial > request.FechaFinal)
            throw new ArgumentException("La fecha inicial no puede ser posterior a la final.");

        return await _repository.ConsultarFoliosAsync(request, ct);
    }
}
