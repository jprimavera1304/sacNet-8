using ISL_Service.Application.DTOs.VentasCobranzaOperaciones;
using ISL_Service.Application.DTOs.VentasEscrituras;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Application.Services;

/*
  LAS REGLAS DE COBRANZA QUE NO ESTAN EN NINGUN PROCEDIMIENTO

  Casi todas son "exigir lo que legacy dejaba pasar en cero". No es celo: estos
  procedimientos, con un id en cero, no fallan — hacen algo distinto y callado.
  Tres ejemplos de este modulo:

    * Crear un CONCENTRADO sin remisiones: legacy valida IDsVentaAgregar al crear
      una cobranza y se olvida de validarlo al crear un concentrado. Resultado:
      un concentrado vacio con su folio consumido, que alguien tiene que ir a
      cancelar a mano.

    * Cerrar un cobro con la lista vacia: el procedimiento parte "" en cero
      elementos y cierra cero cobros, pero contesta como si hubiera trabajado.

    * Modificar un cobro con IDConcentrado = 0 y IDCobranza = 0: lo DESLIGA de
      los dos. Puede ser lo que se queria, asi que no se prohibe — pero se pide
      que venga el cobro, que legacy tampoco exigia del todo.
*/
public class VentasCobranzaOperacionesService : IVentasCobranzaOperacionesService
{
    private readonly IVentasCobranzaOperacionesRepository _repository;

    public VentasCobranzaOperacionesService(IVentasCobranzaOperacionesRepository repository)
    {
        _repository = repository;
    }

    /* ---------------------------- Cobranza ---------------------------- */

    public async Task<ResultadoLegacyDto> CrearCobranzaAsync(
        CobranzaCrearRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (request.IdRepartidor <= 0)
            throw new ArgumentException("El repartidor es obligatorio.");

        request.IdsVentaAgregar = FiltrosLegacy.LimpiarIds(request.IdsVentaAgregar);
        if (request.IdsVentaAgregar.Count == 0)
            throw new ArgumentException("Hay que indicar al menos una remision.");

        return await _repository.CrearCobranzaAsync(request, idUsuario, equipo, ct);
    }

    public async Task<ResultadoLegacyDto> ActualizarCobranzaAsync(
        CobranzaActualizarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (request.IdCobranza <= 0)
            throw new ArgumentException("La cobranza es obligatoria.");

        request.IdsDetalleEliminar = FiltrosLegacy.LimpiarIds(request.IdsDetalleEliminar);
        request.IdsDetalleDevolver = FiltrosLegacy.LimpiarIds(request.IdsDetalleDevolver);
        request.IdsVentaAgregar = FiltrosLegacy.LimpiarIds(request.IdsVentaAgregar);

        /*
          ALGO TIENE QUE CAMBIAR.

          Con las tres listas vacias y el repartidor en cero, el procedimiento
          recorre la cobranza, no cambia nada y de todas formas escribe la firma
          de modificacion: queda una modificacion en la bitacora que no modifico
          nada. Es el tipo de renglon que despues nadie puede explicar.
        */
        var hayCambio = request.IdsDetalleEliminar.Count > 0
            || request.IdsDetalleDevolver.Count > 0
            || request.IdsVentaAgregar.Count > 0
            || request.IdRepartidor > 0
            || !string.IsNullOrWhiteSpace(request.Observaciones);

        if (!hayCambio)
            throw new ArgumentException("No hay nada que cambiar: indica remisiones, repartidor u observaciones.");

        return await _repository.ActualizarCobranzaAsync(request, idUsuario, equipo, ct);
    }

    public async Task<ResultadoLegacyDto> CancelarCobranzaAsync(
        CobranzaCancelarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        request.IdsCobranza = FiltrosLegacy.LimpiarIds(request.IdsCobranza);
        request.IdsCobro = FiltrosLegacy.LimpiarIds(request.IdsCobro);

        if (request.IdsCobranza.Count == 0)
            throw new ArgumentException("Hay que indicar al menos una cobranza.");

        return await _repository.CancelarCobranzaAsync(request, idUsuario, equipo, ct);
    }

    public async Task<FilasLegacyDto> ConsultarCobranzasAsync(
        CobranzaConsultaRequest request, int idUsuarioActual, CancellationToken ct)
    {
        var filtro = request;

        /*
          LA PRIORIDAD: un id gana sobre el folio de cobranza, y el folio de
          cobranza gana sobre todo lo demas. Es la que queria legacy; su segunda
          rama nunca corria porque repetia la condicion de la primera, asi que
          "buscar solo por folio de venta" no existia. Aqui si existe: si no hay
          id ni folio de cobranza, los filtros de venta se respetan.
        */
        if (request.IdCobranza > 0)
        {
            filtro = new CobranzaConsultaRequest { IdCobranza = request.IdCobranza };
        }
        else if (request.FolioInicialCobranza > 0)
        {
            filtro = new CobranzaConsultaRequest
            {
                FolioInicialCobranza = request.FolioInicialCobranza,
                FolioFinalCobranza = request.FolioFinalCobranza > 0
                    ? request.FolioFinalCobranza
                    : request.FolioInicialCobranza
            };
        }
        else if (request.FolioInicialVenta > 0 && request.FolioFinalVenta <= 0)
        {
            filtro.FolioFinalVenta = request.FolioInicialVenta;
        }

        var hayFiltro = filtro.IdCobranza > 0 || filtro.IdRepartidor > 0 || filtro.IdUsuarioCobranza > 0
            || filtro.FolioInicialCobranza > 0 || filtro.FechaInicialCobranza is not null
            || filtro.FolioInicialVenta > 0;

        if (!hayFiltro)
            throw new ArgumentException("Hay que indicar al menos un filtro: cobranza, repartidor, folio o rango de fechas.");

        return await _repository.ConsultarCobranzasAsync(filtro, idUsuarioActual, ct);
    }

    /* --------------------------- Concentrado --------------------------- */

    public async Task<ResultadoLegacyDto> CrearConcentradoAsync(
        ConcentradoCrearRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (request.IdRepartidor <= 0)
            throw new ArgumentException("El repartidor es obligatorio.");

        request.IdsVentaAgregar = FiltrosLegacy.LimpiarIds(request.IdsVentaAgregar);

        /*
          CON `SoloDevueltas = 1` LA LISTA PUEDE VENIR VACIA, Y ES CORRECTO:
          en ese modo el procedimiento arma el concentrado el solo con todas las
          devueltas del repartidor. Fuera de ese modo, un concentrado sin
          remisiones es un folio gastado para nada, y eso es lo que legacy
          permitia por olvidarse de validarlo (en cobranzas si lo valida).
        */
        if (request.SoloDevueltas != 1 && request.IdsVentaAgregar.Count == 0)
            throw new ArgumentException("Hay que indicar al menos una remision, o pedir SoloDevueltas = 1.");

        return await _repository.CrearConcentradoAsync(request, idUsuario, equipo, ct);
    }

    public async Task<ResultadoLegacyDto> ActualizarConcentradoAsync(
        ConcentradoActualizarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (request.IdConcentrado <= 0)
            throw new ArgumentException("El concentrado es obligatorio.");

        request.IdsDetalleEliminar = FiltrosLegacy.LimpiarIds(request.IdsDetalleEliminar);
        request.IdsVentaAgregar = FiltrosLegacy.LimpiarIds(request.IdsVentaAgregar);

        var hayCambio = request.IdsDetalleEliminar.Count > 0
            || request.IdsVentaAgregar.Count > 0
            || request.IdRepartidor > 0
            || !string.IsNullOrWhiteSpace(request.Observaciones);

        if (!hayCambio)
            throw new ArgumentException("No hay nada que cambiar: indica remisiones, repartidor u observaciones.");

        return await _repository.ActualizarConcentradoAsync(request, idUsuario, equipo, ct);
    }

    public async Task<ResultadoLegacyDto> CancelarConcentradoAsync(
        ConcentradoCancelarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        request.IdsConcentrado = FiltrosLegacy.LimpiarIds(request.IdsConcentrado);
        request.IdsCobro = FiltrosLegacy.LimpiarIds(request.IdsCobro);

        if (request.IdsConcentrado.Count == 0)
            throw new ArgumentException("Hay que indicar al menos un concentrado.");

        return await _repository.CancelarConcentradoAsync(request, idUsuario, equipo, ct);
    }

    /* ------------------------------ Cobro ------------------------------ */

    public async Task<ResultadoLegacyDto> AbrirCobroAsync(
        CobroLoteRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        Limpiar(request);
        return await _repository.AbrirCobroAsync(request, idUsuario, equipo, ct);
    }

    public async Task<ResultadoLegacyDto> CerrarCobroAsync(
        CobroLoteRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        Limpiar(request);
        return await _repository.CerrarCobroAsync(request, idUsuario, equipo, ct);
    }

    public async Task<ResultadoLegacyDto> CancelarCobroAsync(
        CobroLoteRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        Limpiar(request);
        return await _repository.CancelarCobroAsync(request, idUsuario, equipo, ct);
    }

    /// <summary>
    /// Las tres operaciones de lote aceptan ids O folios, y necesitan al menos
    /// una de las dos listas con algo. Con las dos vacias el procedimiento no
    /// toca ningun cobro y contesta igual que si hubiera trabajado.
    /// </summary>
    private static void Limpiar(CobroLoteRequest request)
    {
        request.IdsCobro = FiltrosLegacy.LimpiarIds(request.IdsCobro);
        request.FoliosCobro = FiltrosLegacy.LimpiarIds(request.FoliosCobro);

        if (request.IdsCobro.Count == 0 && request.FoliosCobro.Count == 0)
            throw new ArgumentException("Hay que indicar al menos un cobro, por id o por folio.");
    }

    public async Task<ResultadoLegacyDto> ActualizarCobroAsync(
        CobroActualizarRequest request, CancellationToken ct)
    {
        if (request.IdCobro <= 0)
            throw new ArgumentException("El cobro es obligatorio.");

        return await _repository.ActualizarCobroAsync(request, ct);
    }

    public async Task<FilasLegacyDto> ConsultarCobrosAsync(
        CobroConsultaRequest request, int idUsuarioActual, CancellationToken ct)
    {
        var filtro = request;

        if (request.IdCobro > 0)
        {
            filtro = new CobroConsultaRequest { IdCobro = request.IdCobro };
        }
        else if (request.FolioInicialCobro > 0)
        {
            filtro = new CobroConsultaRequest
            {
                FolioInicialCobro = request.FolioInicialCobro,
                FolioFinalCobro = request.FolioFinalCobro > 0 ? request.FolioFinalCobro : request.FolioInicialCobro
            };
        }
        else if (request.IdCliente > 0)
        {
            /*
              EL CLIENTE MANDA SOBRE EL REPARTIDOR Y LOS FOLIOS.

              Es lo que hace legacy al final de su cascada: si viene cliente,
              borra repartidor y los cuatro folios. Tiene sentido — los cobros de
              un cliente no se buscan por el folio del repartidor que se los
              llevo— y se conserva, pero escrito donde se ve.
            */
            filtro = new CobroConsultaRequest
            {
                IdCliente = request.IdCliente,
                IdUsuarioCobro = request.IdUsuarioCobro,
                FechaInicialCobro = request.FechaInicialCobro,
                FechaFinalCobro = request.FechaFinalCobro,
                IdEmpresaVenta = request.IdEmpresaVenta
            };
        }

        var hayFiltro = filtro.IdCobro > 0 || filtro.IdCliente > 0 || filtro.IdRepartidor > 0
            || filtro.IdUsuarioCobro > 0 || filtro.FolioInicialCobro > 0
            || filtro.FechaInicialCobro is not null || filtro.FolioInicialVenta > 0;

        if (!hayFiltro)
            throw new ArgumentException("Hay que indicar al menos un filtro: cobro, cliente, repartidor, folio o rango de fechas.");

        return await _repository.ConsultarCobrosAsync(filtro, idUsuarioActual, ct);
    }

    public async Task<FilasLegacyDto> ConsultarCobroClientesAsync(int idCobro, CancellationToken ct)
    {
        if (idCobro <= 0)
            throw new ArgumentException("El cobro es obligatorio.");

        return await _repository.ConsultarCobroClientesAsync(idCobro, ct);
    }

    /* --------------------- Pendientes y vencidos --------------------- */

    public async Task<FilasLegacyDto> ConsultarPendientesVencidosAsync(
        CobranzaPendientesVencidosRequest request, CancellationToken ct)
    {
        /*
          LA FECHA DE OPERACION ES OBLIGATORIA Y NO SE ADIVINA.

          Es el corte del saldo: cambiarla cambia todos los numeros del reporte.
          Legacy la declara [Required] y revienta con un error de Substring si
          llega vacia; aqui se pide con un mensaje que se entiende.

          Ojo con la tentacion de rellenarla con Constantes.FechaOperacion: leer
          Constantes es lo que DISPARA el cierre del dia, y una consulta no tiene
          por que adelantar la fecha de operacion del negocio. Si el front la
          necesita, la pide por su cuenta.
        */
        if (request.FechaOperacion is null)
            throw new ArgumentException("La fecha de operacion es obligatoria.");
        if (request.IdEmpresa <= 0)
            throw new ArgumentException("La empresa es obligatoria.");
        if (request.FechaInicial is null || request.FechaFinal is null)
            throw new ArgumentException("El rango de fechas es obligatorio.");
        if (request.FechaInicial > request.FechaFinal)
            throw new ArgumentException("La fecha inicial no puede ser posterior a la final.");

        return await _repository.ConsultarPendientesVencidosAsync(request, ct);
    }
}
