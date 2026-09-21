using ISL_Service.Application.DTOs.CascosCambio;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/// <summary>
/// "Cascos a cambio". Aqui vive lo que la hoja de Excel hace a mano: el saldo
/// que corre y el corte del periodo.
///
/// POR QUE EL SALDO SE CALCULA AQUI Y NO SE GUARDA
/// Guardar el saldo en cada renglon es lo que hace el Excel, y por eso el Excel
/// se descuadra: basta corregir un movimiento de hace tres semanas para que
/// todos los saldos de abajo queden mintiendo. Calculado al vuelo no hay nada
/// que se pueda quedar viejo. Son decenas de renglones por mes: no es un
/// problema de rendimiento, es un problema de verdad.
///
/// POR QUE DOS SALDOS
/// El mismo casco vale distinto de cada lado (verificado en las dos bases:
/// 400 contra 380, 520 contra 495, ...). Llevar un solo saldo obligaria a
/// elegir de quien es la razon, y el punto del modulo es justo enseñar las dos
/// cuentas y su diferencia.
/// </summary>
public class CascosCambioService : ICascosCambioService
{
    /* Los cuatro tipos, escritos una sola vez. */
    private const int TipoEntrega = 1;
    private const int TipoPedido = 2;
    private const int TipoPago = 3;
    private const int TipoSaldoInicial = 4;

    private const int EstatusActivo = 1;

    private readonly ICascosCambioRepository _repository;
    private readonly IConfiguration _configuration;

    public CascosCambioService(ICascosCambioRepository repository, IConfiguration configuration)
    {
        _repository = repository;
        _configuration = configuration;
    }

    public Task<List<TipoCascoCambioDto>> ConsultarTiposAsync(CancellationToken ct = default)
        => _repository.ConsultarTiposAsync(ct);

    public async Task<MovimientosCascosCambioResponse> ConsultarMovimientosAsync(
        DateTime? fechaInicio, DateTime? fechaFin, int? tipoMovimiento, bool incluirCancelados,
        bool filtrarPorRegistro, CancellationToken ct = default)
    {
        if (fechaInicio.HasValue && fechaFin.HasValue && fechaFin.Value.Date < fechaInicio.Value.Date)
            throw new ArgumentException("La fecha final no puede ser anterior a la inicial.");

        /*
          EL SALDO SE ACUMULA DESDE EL PRINCIPIO, NO DESDE EL FILTRO.

          Se piden los movimientos SIN fecha inicial (todos hasta fechaFin) y la
          fecha inicial se usa nada mas para decidir que renglones se devuelven.

          Si el saldo empezara en cero en la fecha del filtro, consultar "los
          ultimos 30 dias" daria un saldo que no es el saldo de nadie: se
          quedaria fuera el arrastre y hasta el saldo inicial de la cuenta. Un
          numero asi, con cara de total, es peor que no enseñar ninguno.
        */
        var todos = await _repository.ConsultarMovimientosAsync(
            null, fechaFin, tipoMovimiento, incluirCancelados, filtrarPorRegistro, ct);

        var corte = new CorteCascosCambioDto();
        var movimientos = new List<MovimientoCascoCambioDto>();
        decimal saldo = 0, saldoContraparte = 0;
        var desde = fechaInicio?.Date;

        foreach (var m in todos)
        {
            /*
              Se compara con la MISMA fecha por la que se filtro arriba. Si se
              pidio por fecha de registro y aqui se mirara la del movimiento, el
              corte diria una cosa y la lista otra.
            */
            /*
              Si no hubiera fecha de registro se usa la del movimiento: un
              renglon sin ella se quedaria fuera de TODOS los periodos y
              desapareceria de la pantalla sin que nadie sepa por que. Mejor que
              salga por su otra fecha que no salga.
            */
            var fechaQueManda = filtrarPorRegistro
                ? (m.fechaCreacion?.Date ?? m.fecha.Date)
                : m.fecha.Date;
            var enElPeriodo = desde is null || fechaQueManda >= desde.Value;

            // Lo anterior al periodo mueve el saldo pero no se ve ni se suma al
            // corte: es historia, no es lo que se esta revisando.
            if (!enElPeriodo)
            {
                if (m.estatus == EstatusActivo)
                {
                    saldo += m.importeConSigno;
                    saldoContraparte += m.importeContraparteConSigno;
                }
                continue;
            }

            if (movimientos.Count == 0)
            {
                corte.saldoAnterior = saldo;
                corte.saldoAnteriorContraparte = saldoContraparte;
            }
            movimientos.Add(m);

            // Un movimiento cancelado se sigue viendo — la contraparte tiene esa
            // remision en su hoja y hay que poder explicarle que paso — pero no
            // mueve ni un peso del saldo.
            if (m.estatus == EstatusActivo)
            {
                saldo += m.importeConSigno;
                saldoContraparte += m.importeContraparteConSigno;

                switch (m.tipoMovimiento)
                {
                    case TipoEntrega:
                        corte.entregas += m.importe;
                        corte.entregasContraparte += m.importeContraparte;
                        corte.piezasEntrega += m.totalPiezas;
                        break;
                    case TipoPedido:
                        corte.pedidos += m.importe;
                        corte.pedidosContraparte += m.importeContraparte;
                        corte.piezasPedido += m.totalPiezas;
                        break;
                    case TipoPago:
                        corte.pagos += m.importe;
                        break;
                    case TipoSaldoInicial:
                        corte.saldoInicial += m.importe;
                        break;
                }

                corte.movimientos += 1;
            }

            m.saldo = saldo;
            m.saldoContraparte = saldoContraparte;
        }

        // Sin un solo movimiento en el periodo, el saldo anterior es el saldo:
        // no hubo nada que lo moviera. Sin esto se quedaba en cero y el panel
        // decia "antes 0, ahora un millon" sin que hubiera pasado nada.
        if (movimientos.Count == 0)
        {
            corte.saldoAnterior = saldo;
            corte.saldoAnteriorContraparte = saldoContraparte;
        }

        corte.saldo = saldo;
        corte.saldoContraparte = saldoContraparte;
        // Positiva: esta empresa cuenta mas de lo que le reconoce la otra, o
        // sea, le FALTA que se lo reconozcan. Negativa: le sobra.
        corte.diferencia = saldo - saldoContraparte;

        return new MovimientosCascosCambioResponse { movimientos = movimientos, corte = corte };
    }

    public async Task<List<DetalleCascoCambioDto>> ConsultarDetalleAsync(int idMovimiento, CancellationToken ct = default)
    {
        if (idMovimiento <= 0) throw new ArgumentException("idMovimiento es requerido.");
        return await _repository.ConsultarDetalleAsync(idMovimiento, ct);
    }

    public async Task<List<ResumenTipoCascoCambioDto>> ConsultarResumenAsync(
        DateTime? fechaInicio, DateTime? fechaFin, CancellationToken ct = default)
    {
        if (fechaInicio.HasValue && fechaFin.HasValue && fechaFin.Value.Date < fechaInicio.Value.Date)
            throw new ArgumentException("La fecha final no puede ser anterior a la inicial.");

        return await _repository.ConsultarResumenAsync(fechaInicio, fechaFin, ct);
    }

    public async Task<MovimientoCascoCambioCreadoDto> CrearAsync(
        CrearMovimientoCascoCambioRequest request, string usuario, CancellationToken ct = default)
    {
        if (request.Fecha is null)
            throw new ArgumentException("La fecha es requerida.");
        if (request.TipoMovimiento is < TipoEntrega or > TipoSaldoInicial)
            throw new ArgumentException("tipoMovimiento debe ser 1 (entrega), 2 (pedido), 3 (pago) o 4 (saldo inicial).");

        var conPiezas = (request.Piezas ?? new List<PiezasPorTipoRequest>())
            .Count(p => p != null && p.IdTipoUsado > 0 && p.Piezas > 0);

        // Las mismas reglas las valida el procedimiento. Se repiten aqui para
        // contestar un 400 con un mensaje util en vez de un ok:false generico,
        // pero la que manda es la de la base: es la unica que nadie puede
        // saltarse llamando al API de otra forma.
        if (request.TipoMovimiento is TipoEntrega or TipoPedido && conPiezas == 0)
            throw new ArgumentException("Capture al menos un tipo de casco con piezas.");

        if (request.TipoMovimiento is TipoPago or TipoSaldoInicial)
        {
            if (conPiezas > 0)
                throw new ArgumentException("Un movimiento de dinero no lleva piezas.");
            if (request.Importe <= 0)
                throw new ArgumentException("El importe debe ser mayor que cero.");
        }

        if ((request.Piezas ?? new List<PiezasPorTipoRequest>()).Any(p => p != null && p.Piezas < 0))
            throw new ArgumentException("Las piezas no pueden ser negativas.");

        var (ok, mensaje, id, advertencia) = await _repository.InsertarAsync(request, usuario, ct);
        if (!ok)
            throw new ArgumentException(string.IsNullOrWhiteSpace(mensaje) ? "No se pudo registrar el movimiento." : mensaje);

        return new MovimientoCascoCambioCreadoDto { IdMovimiento = id, Advertencia = advertencia ?? string.Empty };
    }

    public async Task CancelarAsync(int idMovimiento, string? motivo, string usuario, CancellationToken ct = default)
    {
        if (idMovimiento <= 0) throw new ArgumentException("idMovimiento es requerido.");
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("El motivo de la cancelación es requerido.");

        var (ok, mensaje) = await _repository.CancelarAsync(idMovimiento, motivo.Trim(), usuario, ct);
        if (!ok)
            throw new ArgumentException(string.IsNullOrWhiteSpace(mensaje) ? "No se pudo cancelar el movimiento." : mensaje);
    }

    /*
      Cual es la base de la contraparte NO se adivina ni se escribe aqui: viene
      de configuracion, porque en local es MacZ y en produccion puede ser otra
      cosa (o no alcanzarse). Si no se alcanza, el procedimiento lo dice y los
      precios se quedan como estaban; nunca se quedan en cero, que seria peor:
      la conciliacion diria que la otra empresa no reconoce nada.
    */
    public Task<SincronizacionPreciosDto> SincronizarPreciosContraparteAsync(CancellationToken ct = default)
    {
        var baseContraparte = _configuration["CascosCambio:BaseContraparte"];
        var empresaContraparte = _configuration["CascosCambio:EmpresaContraparte"];

        if (string.IsNullOrWhiteSpace(baseContraparte)) baseContraparte = "MacZ";
        if (string.IsNullOrWhiteSpace(empresaContraparte)) empresaContraparte = "zaragoza";

        return _repository.SincronizarPreciosContraparteAsync(baseContraparte, empresaContraparte, ct);
    }
}
