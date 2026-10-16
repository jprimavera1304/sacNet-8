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
          ══════════════════════════════════════════════════════════════════════
          QUE HACE DE VERDAD `sp_n_VentasReasignacion`. LEIDO DE MacZ EL
          2026-10-15, PORQUE LO QUE DECIA AQUI ESTABA MAL.
          ══════════════════════════════════════════════════════════════════════
          El procedimiento NO cambia el cliente de una remision. Nunca. En todo
          su cuerpo no hay un solo `UPDATE Ventas SET IDCliente`. Lo que hace es:

            1. Para cada id de @IDsVenta:  UPDATE Ventas SET IDAgente = @IDAgente
               (SIEMPRE, sin mirar @CambiarCliente) + una fila de auditoria en
               `[Ventas Reasignacion]`.
            2. Si @CambiarCliente = 1:     UPDATE Clientes SET IDAgente = @IDAgente
               para @IDCliente + su fila de auditoria con IDVenta = 0.

          O sea: @CambiarCliente = 1 significa "cambiale tambien el agente al
          CLIENTE", y @IDCliente solo se usa para saber A QUE CLIENTE. El nombre
          del campo es heredado y enganoso; se conserva porque es el parametro de
          legacy, pero las validaciones de aqui ya no se guian por el nombre.

          Son los tres botones de Mac31 (`VentasReasignacion.cs`):
              REASIGNAR NOTAS Y AGENTE  CambiarCliente=1  IDsVenta = los marcados
              REASIGNAR SOLO NOTAS      CambiarCliente=0  IDsVenta = los marcados
              REASIGNAR AGENTE          CambiarCliente=1  IDsVenta = (vacio)

          ══════════════════════════════════════════════════════════════════════
          LO QUE SE CORRIGE, Y ES UN MODO ENTERO QUE ESTABA MUERTO
          ══════════════════════════════════════════════════════════════════════
          Antes se exigia `IdsVenta.Count > 0` SIEMPRE, asi que el tercer boton
          —"REASIGNAR AGENTE", que manda la lista vacia a proposito— contestaba
          400 y no se podia usar por API. No es un modo exotico: en MacZ es EL
          MAS USADO. De las 793 filas de `[Ventas Reasignacion]`, 557 (70%)
          tienen IDVenta = 0, que es exactamente la firma de ese modo. El front
          del web lo ofrece (`ventasReasignacion.ts`, modo "solo-agente") y
          siempre fallaba.

          La preocupacion original sigue cubierta y era buena: con las DOS cosas
          vacias (lista vacia Y CambiarCliente = 0) el procedimiento no escribe
          nada y contesta exito. Eso es lo que se rechaza, y solo eso.

          ══════════════════════════════════════════════════════════════════════
          Y SE EXIGE EL AGENTE SIEMPRE, QUE ES LO QUE DE VERDAD PROTEGE ALGO
          ══════════════════════════════════════════════════════════════════════
          Antes, con CambiarCliente = 1 solo se exigia el cliente. Pero el
          procedimiento escribe `IDAgente = @IDAgente` en las dos ramas, y
          @IDAgente por omision es 0. O sea que un cuerpo con
          `{cambiarCliente:1, idCliente:N, idsVenta:[...]}` y sin agente PASABA
          la validacion y dejaba las notas y el cliente apuntando al agente 0.
          `Ventas.IDAgente` NO tiene llave ajena (comprobado en
          `sys.foreign_keys` de MacZ), asi que la base lo acepta, el
          procedimiento contesta exito, y esa cartera desaparece de cualquier
          consulta que cruce con `Agentes`: queda sin dueno y sin error.
        */
        if (request.IdAgente <= 0)
            throw new ArgumentException("Hay que indicar el agente que recibe la cartera.");

        if (request.CambiarCliente == 1 && request.IdCliente <= 0)
            throw new ArgumentException("Para cambiar el agente del cliente hace falta el cliente.");

        if (request.CambiarCliente != 1 && request.IdsVenta.Count == 0)
        {
            throw new ArgumentException(
                "Hay que indicar al menos una remision, o cambiar tambien el agente del cliente.");
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
        /* Salvo que quien llama EXIJA el multiple. Ver
           VentasDetalleConsultaRequest.ExigirDetalleMultiple: las dos
           procedimientos usan el mismo nombre de columna para cosas distintas,
           y facturar necesita las del multiple. */
        if (request.IdsVenta.Count == 1 && request.IdVenta <= 0 && !request.ExigirDetalleMultiple)
        {
            request.IdVenta = request.IdsVenta[0];
            request.IdsVenta = new List<int>();
        }

        return await _repository.ConsultarDetalleAsync(request, ct);
    }

    /// <inheritdoc />
    public Task<FilasLegacyDto> ConsultarDetalleParaFacturarAsync(
        IReadOnlyCollection<int> idsVenta, CancellationToken ct)
        => ConsultarDetalleAsync(new VentasDetalleConsultaRequest
        {
            IdsVenta = idsVenta.ToList(),
            // La bandera que impide que una sola remision se vaya al
            // procedimiento simple. Ver el remark de la interfaz.
            ExigirDetalleMultiple = true
        }, ct);
}
