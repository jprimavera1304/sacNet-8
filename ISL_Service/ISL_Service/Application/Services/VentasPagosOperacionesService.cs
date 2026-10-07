using ISL_Service.Application.DTOs.VentasEscrituras;
using ISL_Service.Application.DTOs.VentasPagosOperaciones;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Application.Services;

/*
  LAS REGLAS DE PAGOS QUE NO ESTAN EN EL PROCEDIMIENTO

  Las dos que de verdad protegen algo:

  1) UN ABONO ES MAYOR QUE CERO. sp_n_InsertarVentasPagosKilos acepta
     AbonoKilos = 0 y escribe el renglon: queda un pago de cero kilos en el
     historico de la remision, con su firma y su fecha, que no abona nada y que
     hay que explicar cada vez que alguien lee ese historico. El controlador de
     legacy solo valida IDCliente e IDUsuario.

  2) CARGO Y ABONO NO PUEDEN SER LOS DOS CERO en un pago del esquema nuevo. El
     procedimiento inserta igual, y entonces hay un movimiento de cliente que no
     mueve el saldo. Legacy no valida ninguno de los tres importes.

  Y una que no es una regla sino una cortesia: el motivo de una reasignacion es
  obligatorio porque es lo UNICO que explica por que una remision cambio de
  cliente. Legacy tambien lo pide, y es de las pocas cosas que valida bien.
*/
public class VentasPagosOperacionesService : IVentasPagosOperacionesService
{
    private readonly IVentasPagosOperacionesRepository _repository;

    public VentasPagosOperacionesService(IVentasPagosOperacionesRepository repository)
    {
        _repository = repository;
    }

    public async Task<ResultadoLegacyDto> DeclararIncobrableAsync(
        VentaIncobrableRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (request.IdVenta <= 0)
            throw new ArgumentException("La remision es obligatoria.");

        return await _repository.DeclararIncobrableAsync(request, idUsuario, equipo, ct);
    }

    public async Task<ResultadoLegacyDto> AbonarKilosAsync(
        VentaPagoKilosRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (request.IdCliente <= 0)
            throw new ArgumentException("El cliente es obligatorio.");
        if (request.AbonoKilos <= 0)
            throw new ArgumentException("Los kilos del abono tienen que ser mayores que cero.");

        return await _repository.AbonarKilosAsync(request, idUsuario, equipo, ct);
    }

    public async Task<ResultadoLegacyDto> CancelarAbonoKilosAsync(
        VentaPagoKilosCancelarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        /*
          HACE FALTA UNO DE LOS DOS PAGOS.

          El procedimiento cancela por @IDPagoVenta (el pago en dinero) o por
          @IDPagoVentaUsado (el de cascos). Con los dos en cero no cancela nada.
          Legacy valida IDVentaPagoKilo, que ni siquiera le manda.
        */
        if (request.IdPagoVenta <= 0 && request.IdPagoVentaUsado <= 0)
            throw new ArgumentException("Hay que indicar el pago, en dinero o en cascos.");

        return await _repository.CancelarAbonoKilosAsync(request, idUsuario, equipo, ct);
    }

    public async Task<ResultadoLegacyDto> LimpiarPagosCentroServicioAsync(CancellationToken ct)
    {
        /*
          SE LLAMA CON CEROS, COMO LEGACY.

          El repositorio de MacServicios2 manda @IDPagoVenta = 0 y @IDVenta = 0
          literales, aunque su DTO traiga valores. Es decir: la operacion siempre
          fue global, y los campos del cuerpo nunca llegaron a la base. Pasarlos
          ahora cambiaria lo que hace la operacion, no lo que dice el codigo.
        */
        return await _repository.LimpiarPagosCentroServicioAsync(0, 0, ct);
    }

    public async Task<ResultadoLegacyDto> CrearNuevoPagoAsync(
        NuevoPagoCrearRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (request.IdCliente <= 0)
            throw new ArgumentException("El cliente es obligatorio.");
        if (request.IdTipoPago <= 0)
            throw new ArgumentException("El tipo de pago es obligatorio.");
        if (request.Cargo < 0 || request.Abono < 0)
            throw new ArgumentException("El cargo y el abono no pueden ser negativos.");
        if (request.Cargo == 0 && request.Abono == 0)
            throw new ArgumentException("Un pago tiene que traer cargo o abono.");

        return await _repository.CrearNuevoPagoAsync(request, idUsuario, equipo, ct);
    }

    public async Task<ResultadoLegacyDto> AplicarNuevoPagoAsync(
        NuevoPagoRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (request.IdNuevoPagoVenta <= 0)
            throw new ArgumentException("El pago es obligatorio.");

        return await _repository.AplicarNuevoPagoAsync(request, idUsuario, equipo, ct);
    }

    public async Task<ResultadoLegacyDto> CancelarNuevoPagoAsync(
        NuevoPagoRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (request.IdNuevoPagoVenta <= 0)
            throw new ArgumentException("El pago es obligatorio.");

        return await _repository.CancelarNuevoPagoAsync(request, idUsuario, equipo, ct);
    }

    public async Task<ResultadoLegacyDto> CancelarTransferenciaAsync(
        TransferenciaCancelarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (request.IdBancoTransferenciaMovimiento <= 0)
            throw new ArgumentException("El movimiento de transferencia es obligatorio.");

        return await _repository.CancelarTransferenciaAsync(request, idUsuario, equipo, ct);
    }

    public async Task<ResultadoLegacyDto> CancelarTransferenciaPagoAsync(
        TransferenciaPagoCancelarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        /*
          LA VALIDACION DE LEGACY ERA UNA SUMA, Y ESO ES UN DEFECTO.

          Su controlador comprueba `(IDPagoVenta + IDPagoVentaUsado) == 0`. Con
          IDPagoVenta = 5 y IDPagoVentaUsado = -5 la suma es cero y rechaza algo
          valido; y con cualquiera de los dos negativo la suma puede dar distinto
          de cero y deja pasar un id imposible. Aqui se comprueba que haya al
          menos uno, y positivo.
        */
        if (request.IdPagoVenta <= 0 && request.IdPagoVentaUsado <= 0)
            throw new ArgumentException("Hay que indicar el pago, en dinero o en cascos.");

        return await _repository.CancelarTransferenciaPagoAsync(request, idUsuario, equipo, ct);
    }

    public async Task<ResultadoLegacyDto> ReasignarAsync(
        VentasReasignarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Motivo))
            throw new ArgumentException("El motivo es obligatorio.");

        request.IdsVenta = FiltrosLegacy.LimpiarIds(request.IdsVenta);

        /*
          LA LISTA DE REMISIONES ES OBLIGATORIA, Y LEGACY NO LA PEDIA.

          Su controlador valida cliente, agente, usuario, equipo y motivo, y deja
          pasar IDsVenta vacio. Con la lista vacia el procedimiento no reasigna
          nada y contesta igual: la pantalla dice que reasigno y nada cambio de
          dueno.
        */
        if (request.IdsVenta.Count == 0)
            throw new ArgumentException("Hay que indicar al menos una remision.");

        /*
          SEGUN LO QUE SE CAMBIA, SE PIDE LO QUE HACE FALTA.

          Legacy exige cliente Y agente siempre, aunque solo vaya a usar uno: con
          CambiarCliente = 1 el procedimiento mueve el cliente y con 0 mueve el
          agente. Exigir el que no se va a usar obliga a mandar un numero de
          relleno, y un numero de relleno en un campo que decide de quien es una
          remision es una mala idea.
        */
        if (request.CambiarCliente == 1)
        {
            if (request.IdCliente <= 0)
                throw new ArgumentException("Para cambiar el cliente hace falta el cliente.");
        }
        else if (request.IdAgente <= 0)
        {
            throw new ArgumentException("Para cambiar el agente hace falta el agente.");
        }

        return await _repository.ReasignarAsync(request, idUsuario, equipo, ct);
    }

    public async Task<ResultadoLegacyDto> ActualizarVentaAsync(
        VentaActualizarRequest request, CancellationToken ct)
    {
        if (request.IdVenta <= 0)
            throw new ArgumentException("La remision es obligatoria.");

        return await _repository.ActualizarVentaAsync(request, ct);
    }

    public async Task<ResultadoLegacyDto> MarcarPagoPorKiloAsync(
        VentasPagoPorKiloRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        request.IdsVenta = FiltrosLegacy.LimpiarIds(request.IdsVenta);
        if (request.IdsVenta.Count == 0)
            throw new ArgumentException("Hay que indicar al menos una remision.");
        if (request.PagoPorKilo is not (0 or 1))
            throw new ArgumentException("PagoPorKilo tiene que ser 0 o 1.");

        return await _repository.MarcarPagoPorKiloAsync(request, idUsuario, equipo, ct);
    }

    public async Task<FilasLegacyDto> ConsultarDetalleAsync(
        VentasDetalleConsultaRequest request, CancellationToken ct)
    {
        request.IdsVenta = FiltrosLegacy.LimpiarIds(request.IdsVenta);

        if (request.IdsVenta.Count == 0 && request.IdVenta <= 0)
            throw new ArgumentException("Hay que indicar la remision, o una lista de remisiones.");

        /*
          CON UNA SOLA REMISION EN LA LISTA SE USA EL PROCEDIMIENTO SIMPLE.

          No es lo mismo: el simple acepta @IDDescuento (para ver el detalle con
          un descuento simulado) y el multiple no. Legacy, con una lista de un
          elemento, se iba al multiple y perdia el descuento sin avisar.
        */
        if (request.IdsVenta.Count == 1 && request.IdVenta <= 0)
        {
            request.IdVenta = request.IdsVenta[0];
            request.IdsVenta = new List<int>();
        }

        return await _repository.ConsultarDetalleAsync(request, ct);
    }
}
