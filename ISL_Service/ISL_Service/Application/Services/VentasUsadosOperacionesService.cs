using ISL_Service.Application.DTOs.VentasEscrituras;
using ISL_Service.Application.DTOs.VentasUsadosOperaciones;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/*
  LAS DOS REGLAS DE LOS CASCOS USADOS QUE NO ESTAN EN NINGUN PROCEDIMIENTO

  1) LAS LISTAS PARALELAS TIENEN QUE MEDIR LO MISMO.

     Los sp_n_ de usados reciben tres, cinco o doce cadenas separadas por
     virgulilla y las vuelven a cruzar por POSICION: el tercer tipo de casco va
     con el tercer precio y la tercera cantidad. Si una lista trae un elemento
     menos, el procedimiento no falla: cruza mal y guarda un renglon con el
     precio de otro. Legacy no lo comprueba en ningun lado — ni en el
     controlador ni en el servicio — y el unico sintoma es un importe raro
     semanas despues.

     Se comprueba aqui, antes de tocar la base, y con el nombre de las dos
     listas en el mensaje para que se sepa cual falta.

  2) LA PRIORIDAD DE FILTROS SE DECIDE CON NOMBRE.

     Legacy, en cada consulta, cuando viene un id construye un DTO NUEVO y con
     eso tira todos los demas filtros sin decirlo. El efecto buscado es "si
     pides un renglon concreto, los demas filtros no estorban"; el efecto real
     incluye tirar IDUsuarioActual, que en otras pantallas de la familia es el
     candado de almacen. Aqui la misma prioridad se aplica de forma explicita, se
     ve en el codigo, y nunca toca la identidad.
*/
public class VentasUsadosOperacionesService : IVentasUsadosOperacionesService
{
    private readonly IVentasUsadosOperacionesRepository _repository;

    public VentasUsadosOperacionesService(IVentasUsadosOperacionesRepository repository)
    {
        _repository = repository;
    }

    /* ============================================================ */
    /* Cobros de dinero por cascos                                   */
    /* ============================================================ */

    public async Task<ResultadoLegacyDto> CrearCobroDineroAsync(
        CobroDineroUsadoCrearRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (request.IdCobro <= 0)
            throw new ArgumentException("El cobro es obligatorio.");
        if (request.IdsTipoUsado.Count == 0)
            throw new ArgumentException("Hay que indicar al menos un tipo de casco.");

        MismoLargo("IdsTipoUsado", request.IdsTipoUsado.Count, "PreciosUnitario", request.PreciosUnitario.Count);
        MismoLargo("IdsTipoUsado", request.IdsTipoUsado.Count, "Cantidades", request.Cantidades.Count);

        return await _repository.CrearCobroDineroAsync(request, idUsuario, equipo, ct);
    }

    public async Task<ResultadoLegacyDto> GuardarRepartoCobroDineroAsync(
        CobroDineroUsadoRepartoRequest request, int idUsuario, string equipo, CancellationToken ct)
        => await RepartoAsync(request, aplicar: false, idUsuario, equipo, ct);

    public async Task<ResultadoLegacyDto> AplicarCobroDineroAsync(
        CobroDineroUsadoRepartoRequest request, int idUsuario, string equipo, CancellationToken ct)
        => await RepartoAsync(request, aplicar: true, idUsuario, equipo, ct);

    private async Task<ResultadoLegacyDto> RepartoAsync(
        CobroDineroUsadoRepartoRequest request, bool aplicar, int idUsuario, string equipo, CancellationToken ct)
    {
        if (request.IdCobroDineroUsado <= 0)
            throw new ArgumentException("El cobro de dinero es obligatorio.");

        /*
          LAS OCHO COLUMNAS DE GRUPO SE MIDEN CONTRA LOS CLIENTES, NO ENTRE SI.

          Cada renglon del reparto es un cliente, y de ese cliente se dice
          cuantos cascos de cada grupo entrego. O sea que las ocho listas de
          cantidades y las dos de pagos tienen un elemento por CLIENTE, igual
          que IdsClientes. Lo que no cuadre aqui cruza dinero con el cliente
          equivocado.
        */
        var clientes = request.IdsClientes.Count;
        if (clientes > 0)
        {
            MismoLargo("IdsClientes", clientes, "CantidadesG1", request.CantidadesG1.Count);
            MismoLargo("IdsClientes", clientes, "CantidadesG2", request.CantidadesG2.Count);
            MismoLargo("IdsClientes", clientes, "CantidadesG3", request.CantidadesG3.Count);
            MismoLargo("IdsClientes", clientes, "CantidadesG4", request.CantidadesG4.Count);
            MismoLargo("IdsClientes", clientes, "CantidadesG5", request.CantidadesG5.Count);
            MismoLargo("IdsClientes", clientes, "CantidadesG6", request.CantidadesG6.Count);
            MismoLargo("IdsClientes", clientes, "CantidadesG7", request.CantidadesG7.Count);
            MismoLargo("IdsClientes", clientes, "CantidadesMoto", request.CantidadesMoto.Count);
            MismoLargo("IdsClientes", clientes, "PagosEfectivo", request.PagosEfectivo.Count);
            MismoLargo("IdsClientes", clientes, "PagosTarjeta", request.PagosTarjeta.Count);
        }

        return await _repository.RepartirCobroDineroAsync(request, aplicar, idUsuario, equipo, ct);
    }

    public async Task<ResultadoLegacyDto> CancelarCobroDineroAsync(
        CobroDineroUsadoCancelarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (request.IdCobroDineroUsado <= 0)
            throw new ArgumentException("El cobro de dinero es obligatorio.");

        return await _repository.CancelarCobroDineroAsync(request, idUsuario, equipo, ct);
    }

    public async Task<FilasLegacyDto> ConsultarCobrosDineroAsync(
        CobroDineroUsadoConsultaRequest request, CancellationToken ct)
    {
        /*
          LA PRIORIDAD, IGUAL QUE EN LEGACY PERO A LA VISTA:
          un id concreto gana sobre un rango de folios, y el rango de folios
          gana sobre el rango de fechas. Lo que pierde se pone en cero AQUI, no
          construyendo un objeto nuevo, para que se vea que se esta tirando.
        */
        var filtro = Acotar(request);
        ExigirAlgunFiltro(filtro);
        return await _repository.ConsultarCobrosDineroAsync(filtro, ct);
    }

    private static CobroDineroUsadoConsultaRequest Acotar(CobroDineroUsadoConsultaRequest request)
    {
        if (request.IdCobroDineroUsado > 0)
        {
            return new CobroDineroUsadoConsultaRequest { IdCobroDineroUsado = request.IdCobroDineroUsado };
        }

        if (request.FolioInicial > 0)
        {
            return new CobroDineroUsadoConsultaRequest
            {
                FolioInicial = request.FolioInicial,
                FolioFinal = request.FolioFinal > 0 ? request.FolioFinal : request.FolioInicial
            };
        }

        return request;
    }

    /// <summary>
    /// Sin ningun filtro, el procedimiento barre la tabla entera. En una base de
    /// produccion eso son cientos de miles de renglones por una pantalla que
    /// alguien abrio sin querer. Se exige al menos uno.
    /// </summary>
    private static void ExigirAlgunFiltro(CobroDineroUsadoConsultaRequest filtro)
    {
        var hayAlgo = filtro.IdCobroDineroUsado > 0
            || filtro.IdCobro > 0
            || filtro.IdRepartidor > 0
            || filtro.FolioInicial > 0
            || filtro.FechaInicial is not null
            || filtro.FechaModificacionInicial is not null;

        if (!hayAlgo)
            throw new ArgumentException("Hay que indicar al menos un filtro: cobro, repartidor, folio o rango de fechas.");
    }

    public async Task<ResultadoLegacyDto> CrearChequeAsync(
        CobroDineroUsadoChequeCrearRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (request.IdCobroDineroUsado <= 0)
            throw new ArgumentException("El cobro de dinero es obligatorio.");
        if (request.IdBancoCheque <= 0)
            throw new ArgumentException("El banco del cheque es obligatorio.");
        if (string.IsNullOrWhiteSpace(request.NumeroCheque))
            throw new ArgumentException("El numero de cheque es obligatorio.");

        /*
          LA FECHA DEL CHEQUE ES OBLIGATORIA, Y ESO ARREGLA UN DEFECTO.

          En legacy, al insertar, la conversion de formato se hace SIN comprobar
          que venga la fecha, asi que una fecha vacia lanza un error de
          Substring y el endpoint contesta 400 con el stack. El de actualizar si
          la comprueba. Aqui se pide siempre: un cheque sin fecha no es un
          cheque, y la diferencia entre los dos endpoints de legacy era un
          descuido, no una regla.
        */
        if (request.FechaCheque is null)
            throw new ArgumentException("La fecha del cheque es obligatoria.");
        if (request.ImporteCheque <= 0)
            throw new ArgumentException("El importe del cheque tiene que ser mayor que cero.");

        return await _repository.CrearChequeAsync(request, idUsuario, equipo, ct);
    }

    public async Task<ResultadoLegacyDto> ActualizarChequeAsync(
        CobroDineroUsadoChequeActualizarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        /*
          AQUI SI SE EXIGE EL ID DEL CHEQUE, Y LEGACY NO LO EXIGIA.

          El controlador de legacy valida IDCobroDineroUsado y se olvida de
          IDCobroDineroUsadoCheque, que es el renglon que se va a modificar.
          Con 0, el procedimiento no encuentra nada y la pantalla se queda
          pensando que guardo.
        */
        if (request.IdCobroDineroUsadoCheque <= 0)
            throw new ArgumentException("El cheque es obligatorio.");
        if (request.IdCobroDineroUsado <= 0)
            throw new ArgumentException("El cobro de dinero es obligatorio.");

        return await _repository.ActualizarChequeAsync(request, idUsuario, equipo, ct);
    }

    public async Task<FilasLegacyDto> ConsultarChequesAsync(
        CobroDineroUsadoChequeConsultaRequest request, CancellationToken ct)
    {
        var filtro = request;
        if (request.IdCobroDineroUsadoCheque > 0)
            filtro = new CobroDineroUsadoChequeConsultaRequest { IdCobroDineroUsadoCheque = request.IdCobroDineroUsadoCheque };
        else if (request.IdCobroDineroUsado > 0)
            filtro = new CobroDineroUsadoChequeConsultaRequest { IdCobroDineroUsado = request.IdCobroDineroUsado };

        if (filtro.IdCobroDineroUsadoCheque <= 0 && filtro.IdCobroDineroUsado <= 0
            && filtro.IdCobro <= 0 && filtro.FechaInicial is null)
            throw new ArgumentException("Hay que indicar al menos un filtro: cheque, cobro de dinero, cobro o rango de fechas.");

        return await _repository.ConsultarChequesAsync(filtro, ct);
    }

    public async Task<ResultadoLegacyDto> CrearCorteCsAsync(
        CobroDineroUsadoCorteCsRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        ValidarCorteCs(request);
        return await _repository.CrearCorteCsAsync(request, idUsuario, equipo, ct);
    }

    public async Task<ResultadoLegacyDto> EliminarCorteCsAsync(
        CobroDineroUsadoCorteCsRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        ValidarCorteCs(request);
        return await _repository.EliminarCorteCsAsync(request, idUsuario, equipo, ct);
    }

    /*
      SE EXIGEN CENTRO Y CORTE, Y LEGACY NO EXIGIA NINGUNO.

      El controlador de legacy solo mira IDUsuario y Equipo, que son justo los
      dos que aqui no vienen del cuerpo. Sin centro ni corte, el procedimiento de
      ELIMINAR borra por la combinacion (cobro, centro=0, corte=0): o no borra
      nada, o borra el renglon equivocado. Es una operacion destructiva sin
      ningun campo obligatorio.
    */
    private static void ValidarCorteCs(CobroDineroUsadoCorteCsRequest request)
    {
        if (request.IdCobroDineroUsado <= 0)
            throw new ArgumentException("El cobro de dinero es obligatorio.");
        if (request.IdCentro <= 0)
            throw new ArgumentException("El centro de servicio es obligatorio.");
        if (request.IdCorte <= 0)
            throw new ArgumentException("El corte es obligatorio.");
    }

    public async Task<FilasLegacyDto> ConsultarCortesCsAsync(
        CobroDineroUsadoCorteCsConsultaRequest request, CancellationToken ct)
    {
        var filtro = request;
        if (request.IdCobroDineroUsadoCorteCs > 0)
            filtro = new CobroDineroUsadoCorteCsConsultaRequest { IdCobroDineroUsadoCorteCs = request.IdCobroDineroUsadoCorteCs };
        else if (request.IdCobroDineroUsado > 0)
            filtro = new CobroDineroUsadoCorteCsConsultaRequest { IdCobroDineroUsado = request.IdCobroDineroUsado };
        else
            filtro.IdsCobro = FiltrosIds(request.IdsCobro);

        if (filtro.IdCobroDineroUsadoCorteCs <= 0 && filtro.IdCobroDineroUsado <= 0
            && filtro.IdCobro <= 0 && filtro.IdsCobro.Count == 0 && filtro.FechaInicial is null)
            throw new ArgumentException("Hay que indicar al menos un filtro: corte, cobro de dinero, cobro o rango de fechas.");

        return await _repository.ConsultarCortesCsAsync(filtro, ct);
    }

    /* ============================================================ */
    /* Ingresos de cascos usados                                     */
    /* ============================================================ */

    public async Task<ResultadoLegacyDto> CrearCobroUsadoAsync(
        VentasCobroUsadoCrearRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (request.IdCobro <= 0)
            throw new ArgumentException("El cobro es obligatorio.");
        if (request.IdEmpresa <= 0)
            throw new ArgumentException("La empresa es obligatoria.");
        if (request.IdsTipoUsado.Count == 0)
            throw new ArgumentException("Hay que indicar al menos un tipo de casco.");

        MismoLargo("IdsTipoUsado", request.IdsTipoUsado.Count, "CantidadesUsado", request.CantidadesUsado.Count);
        MismoLargo("IdsTipoUsado", request.IdsTipoUsado.Count, "CostosConIvaUsado", request.CostosConIvaUsado.Count);

        return await _repository.CrearCobroUsadoAsync(request, idUsuario, equipo, ct);
    }

    public async Task<ResultadoLegacyDto> CancelarCobroUsadoAsync(
        VentasCobroUsadoCancelarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (request.IdVentaCobroUsado <= 0)
            throw new ArgumentException("El ingreso de cascos es obligatorio.");

        return await _repository.CancelarCobroUsadoAsync(request, idUsuario, equipo, ct);
    }

    public async Task<FilasLegacyDto> ConsultarCobrosUsadoAsync(
        VentasCobroUsadoConsultaRequest request, CancellationToken ct)
    {
        var filtro = request;
        if (request.IdVentaCobroUsado > 0)
            filtro = new VentasCobroUsadoConsultaRequest { IdVentaCobroUsado = request.IdVentaCobroUsado };

        if (filtro.IdVentaCobroUsado <= 0 && filtro.IdCobro <= 0 && filtro.FechaInicial is null)
            throw new ArgumentException("Hay que indicar al menos un filtro: ingreso, cobro o rango de fechas.");

        return await _repository.ConsultarCobrosUsadoAsync(filtro, ct);
    }

    /* ============================================================ */
    /* Excedentes de cascos                                          */
    /* ============================================================ */

    public async Task<ResultadoLegacyDto> CrearExcedenteAsync(
        VentasUsadoExcedenteCrearRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (request.IdCobro <= 0)
            throw new ArgumentException("El cobro es obligatorio.");
        if (request.IdEmpresa <= 0)
            throw new ArgumentException("La empresa es obligatoria.");
        if (request.IdRepartidor <= 0)
            throw new ArgumentException("El repartidor es obligatorio.");
        if (string.IsNullOrWhiteSpace(request.Vale))
            throw new ArgumentException("El vale es obligatorio.");
        if (request.IdsTipoUsado.Count == 0)
            throw new ArgumentException("Hay que indicar al menos un tipo de casco.");

        MismoLargo("IdsTipoUsado", request.IdsTipoUsado.Count, "CantidadesUsado", request.CantidadesUsado.Count);
        MismoLargo("IdsTipoUsado", request.IdsTipoUsado.Count, "CostosConIvaUsado", request.CostosConIvaUsado.Count);

        return await _repository.CrearExcedenteAsync(request, idUsuario, equipo, ct);
    }

    public async Task<ResultadoLegacyDto> CancelarExcedenteAsync(
        VentasUsadoExcedenteCancelarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (request.IdVentaUsadoExcedente <= 0)
            throw new ArgumentException("El excedente es obligatorio.");

        return await _repository.CancelarExcedenteAsync(request, idUsuario, equipo, ct);
    }

    public async Task<FilasLegacyDto> ConsultarExcedentesAsync(
        VentasUsadoExcedenteConsultaRequest request, CancellationToken ct)
    {
        var filtro = request;
        if (request.IdVentaUsadoExcedente > 0)
            filtro = new VentasUsadoExcedenteConsultaRequest { IdVentaUsadoExcedente = request.IdVentaUsadoExcedente };

        if (filtro.IdVentaUsadoExcedente <= 0 && filtro.IdCobro <= 0 && filtro.IdCliente <= 0
            && filtro.IdRepartidor <= 0 && string.IsNullOrWhiteSpace(filtro.Vale) && filtro.FechaInicial is null)
            throw new ArgumentException("Hay que indicar al menos un filtro: excedente, cobro, cliente, repartidor, vale o rango de fechas.");

        return await _repository.ConsultarExcedentesAsync(filtro, ct);
    }

    /* ============================================================ */
    /* Movimientos del almacen de usados                             */
    /* ============================================================ */

    public async Task<ResultadoLegacyDto> CrearMovimientoAsync(
        UsadoMovimientoCrearRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (request.IdAlmacenUsado <= 0)
            throw new ArgumentException("El almacen de usados es obligatorio.");
        if (request.IdsTipoUsado.Count == 0)
            throw new ArgumentException("Hay que indicar al menos un tipo de casco.");

        MismoLargo("IdsTipoUsado", request.IdsTipoUsado.Count, "PreciosCambio", request.PreciosCambio.Count);
        MismoLargo("IdsTipoUsado", request.IdsTipoUsado.Count, "PreciosUnitario", request.PreciosUnitario.Count);
        MismoLargo("IdsTipoUsado", request.IdsTipoUsado.Count, "CantidadesSalida", request.CantidadesSalida.Count);
        MismoLargo("IdsTipoUsado", request.IdsTipoUsado.Count, "CantidadesEntrada", request.CantidadesEntrada.Count);

        return await _repository.CrearMovimientoAsync(request, idUsuario, equipo, ct);
    }

    public async Task<ResultadoLegacyDto> CancelarMovimientoAsync(
        UsadoMovimientoCancelarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (request.IdUsadoMovimiento <= 0)
            throw new ArgumentException("El movimiento es obligatorio.");

        return await _repository.CancelarMovimientoAsync(request, idUsuario, equipo, ct);
    }

    public async Task<FilasLegacyDto> ConsultarMovimientosAsync(
        UsadoMovimientoConsultaRequest request, CancellationToken ct)
    {
        var filtro = request;
        if (request.IdUsadoMovimiento > 0)
            filtro = new UsadoMovimientoConsultaRequest { IdUsadoMovimiento = request.IdUsadoMovimiento };
        else if (request.FolioInicial > 0)
            filtro = new UsadoMovimientoConsultaRequest
            {
                FolioInicial = request.FolioInicial,
                FolioFinal = request.FolioFinal > 0 ? request.FolioFinal : request.FolioInicial,
                TipoConsulta = request.TipoConsulta
            };

        if (filtro.IdUsadoMovimiento <= 0 && filtro.IdGallero <= 0 && filtro.IdRepartidor <= 0
            && filtro.FolioInicial <= 0 && filtro.FechaInicial is null)
            throw new ArgumentException("Hay que indicar al menos un filtro: movimiento, gallero, repartidor, folio o rango de fechas.");

        return await _repository.ConsultarMovimientosAsync(filtro, ct);
    }

    public async Task<FilasLegacyDto> ConsultarMovimientosPagadosAsync(
        UsadoMovimientoPagadoConsultaRequest request, CancellationToken ct)
    {
        if (request.IdGallero <= 0 && request.IdRepartidor <= 0 && request.IdCorte <= 0 && request.FechaInicial is null)
            throw new ArgumentException("Hay que indicar al menos un filtro: gallero, repartidor, corte o rango de fechas.");

        return await _repository.ConsultarMovimientosPagadosAsync(request, ct);
    }

    /* ---------------------------------------------------------------- */
    /* Reglas compartidas                                               */
    /* ---------------------------------------------------------------- */

    /// <summary>
    /// Dos listas que el procedimiento va a cruzar por posicion tienen que
    /// medir lo mismo. El mensaje nombra las dos: "faltan elementos" no le dice
    /// a nadie que columna de la rejilla se quedo corta.
    /// </summary>
    private static void MismoLargo(string nombreA, int largoA, string nombreB, int largoB)
    {
        if (largoA != largoB)
            throw new ArgumentException(
                $"Las listas {nombreA} ({largoA}) y {nombreB} ({largoB}) tienen que traer el mismo numero de elementos.");
    }

    private static List<int> FiltrosIds(IEnumerable<int>? valores)
        => Infrastructure.Data.FiltrosLegacy.LimpiarIds(valores);
}
