using ISL_Service.Application.DTOs.VentasEscrituras;
using ISL_Service.Application.DTOs.VentasGarantias;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Application.Services;

/*
  LAS REGLAS DE GARANTIAS QUE NO ESTAN EN EL PROCEDIMIENTO

  La mas importante es la PRIORIDAD DE FILTROS de las consultas, porque las tres
  pantallas de garantias buscan por campos de texto —ticket, vigencia, numero de
  garantia, diagnostico, folio— y legacy las ordena en una cascada de siete
  ramas donde cada una TIRA todo lo demas. Esa cascada no es un capricho: buscar
  "el vale 9981" y a la vez "del repartidor 3 entre el 1 y el 5 de marzo" casi
  siempre devuelve vacio, porque quien teclea un vale no se acuerda de limpiar
  los otros filtros. Se conserva el orden, escrito con nombre:

      estatus de aceptacion  ->  ticket  ->  vigencia  ->  garantia
      ->  diagnostico  ->  folio  ->  rangos de fecha

  Lo que NO se conserva es que la cascada tirara tambien `IDUsuarioActual`: ese
  ya no viene del cuerpo, viene del token, y es el que resuelve el acceso por
  almacen. En legacy, al reconstruir el DTO, se perdia — y con el, el candado.
*/
public class VentasGarantiasService : IVentasGarantiasService
{
    private readonly IVentasGarantiasRepository _repository;

    public VentasGarantiasService(IVentasGarantiasRepository repository)
    {
        _repository = repository;
    }

    public async Task<ResultadoLegacyDto> CrearAsync(
        GarantiaCrearRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (request.IdCliente <= 0) throw new ArgumentException("El cliente es obligatorio.");
        if (request.IdRepartidor <= 0) throw new ArgumentException("El repartidor es obligatorio.");
        if (request.IdAlmacen <= 0) throw new ArgumentException("El almacen es obligatorio.");
        if (request.IdProducto <= 0) throw new ArgumentException("El producto es obligatorio.");
        if (request.FechaIngreso is null) throw new ArgumentException("La fecha de ingreso es obligatoria.");

        /*
          SI ES DE MAYORISTA, HACE FALTA SABER DE QUIEN.

          Legacy acepta GarantiaMayorista = 1 con IDEmpresaMayorista = 0: queda
          una garantia marcada como de mayorista sin mayorista, y despues nadie
          sabe a quien cobrarsela.
        */
        if (request.GarantiaMayorista == 1 && request.IdEmpresaMayorista <= 0)
            throw new ArgumentException("Una garantia de mayorista necesita la empresa mayorista.");

        return await _repository.CrearAsync(request, idUsuario, equipo, ct);
    }

    public async Task<ResultadoLegacyDto> ActualizarAsync(
        GarantiaActualizarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        request.IdsGarantia = FiltrosLegacy.LimpiarIds(request.IdsGarantia);
        request.IdsVenta = FiltrosLegacy.LimpiarIds(request.IdsVenta);

        /*
          LA LISTA DE GARANTIAS ES OBLIGATORIA, Y LEGACY NO LA PEDIA.

          Su controlador valida IDUsuario y Equipo —los dos que aqui vienen del
          token— y deja pasar IDsGarantia vacio. Con la lista vacia el
          procedimiento no encuentra nada que cambiar, no da error, y la pantalla
          dice que guardo. Este es el endpoint por el que Mac31 CANCELA garantias
          de verdad (mandando el estatus de cancelada), asi que "creyo que
          guardo" significa "creyo que cancelo".
        */
        if (request.IdsGarantia.Count == 0)
            throw new ArgumentException("Hay que indicar al menos una garantia.");

        if (request.IdStatusGarantia <= 0)
            throw new ArgumentException("El estatus de la garantia es obligatorio.");

        /*
          PORCENTAJE Y MONTO SON EXCLUYENTES.

          El procedimiento recibe los dos y calcula la bonificacion con el que no
          sea cero; con los dos distintos de cero el resultado depende de su
          orden interno, que es justo lo que no se debe dejar al azar cuando el
          numero es dinero que se le reconoce al cliente.
        */
        if (request.Porcentaje != 0 && request.Monto != 0)
            throw new ArgumentException("Indica porcentaje o monto, no los dos.");

        return await _repository.ActualizarAsync(request, idUsuario, equipo, ct);
    }

    public async Task<ResultadoLegacyDto> CancelarAsync(
        GarantiaCancelarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        request.IdsGarantia = FiltrosLegacy.LimpiarIds(request.IdsGarantia);
        if (request.IdsGarantia.Count == 0)
            throw new ArgumentException("Hay que indicar al menos una garantia.");

        return await _repository.CancelarAsync(request, idUsuario, equipo, ct);
    }

    public async Task<FilasLegacyDto> ConsultarAsync(
        GarantiaConsultaRequest request, int idUsuarioActual, CancellationToken ct)
    {
        var filtro = Acotar(request, conRamaDeDiagnostico: true);
        return await _repository.ConsultarAsync(filtro, idUsuarioActual, ct);
    }

    public async Task<ResultadoLegacyDto> AceptarAsync(
        GarantiaAceptarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (request.IdGarantia <= 0)
            throw new ArgumentException("La garantia es obligatoria.");

        /*
          EL CENTRO DE SERVICIO ES OBLIGATORIO, Y LEGACY NO LO PEDIA.

          El procedimiento arma con SQL dinamico un UPDATE contra la tabla
          Garantias de la base de ESE centro. Con 0 no hay base a la que ir: en
          el mejor caso no hace nada, y en el peor arma un nombre de base
          incompleto. El controlador de legacy no lo valida.
        */
        if (request.IdCentroServicio <= 0)
            throw new ArgumentException("El centro de servicio es obligatorio.");

        return await _repository.AceptarAsync(request, idUsuario, equipo, ct);
    }

    public async Task<FilasLegacyDto> ConsultarAceptarAsync(
        GarantiaConsultaRequest request, int idUsuarioActual, CancellationToken ct)
    {
        /*
          AQUI EL ESTATUS DE ACEPTACION MANDA SOBRE TODO.

          Con 1 (pendientes de aceptar) legacy tira el resto de los filtros, y
          tiene sentido: la pantalla es "que me falta aceptar", no "busquemos".
          Con 2 (ya aceptadas) conserva el rango de fechas. Con 0 no hace nada
          —ni formatea las fechas—, lo que en legacy mandaba las cadenas crudas
          al procedimiento; aqui las fechas siempre van en forma, asi que 0
          simplemente significa "sin ese filtro".
        */
        var filtro = request.IdStatusGarantiaAceptada == 1
            ? new GarantiaConsultaRequest { IdStatusGarantiaAceptada = 1 }
            : Acotar(request, conRamaDeDiagnostico: true);

        filtro.IdStatusGarantiaAceptada = request.IdStatusGarantiaAceptada;
        return await _repository.ConsultarAceptarAsync(filtro, idUsuarioActual, ct);
    }

    public async Task<ResultadoLegacyDto> CrearRegistroAsync(
        GarantiaRegistroCrearRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (request.IdProducto <= 0)
            throw new ArgumentException("El producto es obligatorio.");

        /*
          EL TICKET ES OBLIGATORIO EN UN REGISTRO PREVIO.

          Un registro previo existe para apuntar un VALE antes de que haya una
          remision a la que colgarlo. Sin vale no apunta nada: es un renglon que
          nadie podra volver a encontrar. Legacy no lo pide.
        */
        if (string.IsNullOrWhiteSpace(request.Ticket))
            throw new ArgumentException("El ticket es obligatorio.");

        return await _repository.CrearRegistroAsync(request, idUsuario, equipo, ct);
    }

    public async Task<ResultadoLegacyDto> CancelarRegistroAsync(
        GarantiaRegistroCancelarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        request.IdsRegistro = FiltrosLegacy.LimpiarIds(request.IdsRegistro);
        if (request.IdsRegistro.Count == 0)
            throw new ArgumentException("Hay que indicar al menos un registro.");

        return await _repository.CancelarRegistroAsync(request, idUsuario, equipo, ct);
    }

    public async Task<FilasLegacyDto> ConsultarRegistroAsync(
        GarantiaRegistroConsultaRequest request, CancellationToken ct)
    {
        /*
          LA MISMA CASCADA, SIN LA RAMA DE DIAGNOSTICO: este procedimiento solo
          declara nueve parametros y el diagnostico no esta entre ellos. Legacy
          tambien formatea aqui las fechas de ingreso aunque no las mande; eso no
          se copia, porque formatear un valor que no se usa solo sirve para
          reventar cuando llega vacio.
        */
        var filtro = request;

        if (!string.IsNullOrWhiteSpace(request.Ticket))
            filtro = new GarantiaRegistroConsultaRequest { Ticket = FiltrosLegacy.Texto(request.Ticket) };
        else if (!string.IsNullOrWhiteSpace(request.Vigencia))
            filtro = new GarantiaRegistroConsultaRequest { Vigencia = FiltrosLegacy.Texto(request.Vigencia) };
        else if (!string.IsNullOrWhiteSpace(request.Garantia))
            filtro = new GarantiaRegistroConsultaRequest { Garantia = FiltrosLegacy.Texto(request.Garantia) };
        else if (!string.IsNullOrWhiteSpace(request.Folio))
            filtro = new GarantiaRegistroConsultaRequest { Folio = FiltrosLegacy.Texto(request.Folio) };

        return await _repository.ConsultarRegistroAsync(filtro, ct);
    }

    /// <summary>
    /// La cascada de filtros de las dos consultas de garantias: el primer campo
    /// de texto que venga gana y se queda solo. Es la prioridad de legacy, con
    /// nombre y en un solo lugar.
    /// </summary>
    private static GarantiaConsultaRequest Acotar(GarantiaConsultaRequest request, bool conRamaDeDiagnostico)
    {
        if (request.IdGarantia > 0)
            return new GarantiaConsultaRequest { IdGarantia = request.IdGarantia };

        if (!string.IsNullOrWhiteSpace(request.Ticket))
            return new GarantiaConsultaRequest { Ticket = FiltrosLegacy.Texto(request.Ticket) };

        if (!string.IsNullOrWhiteSpace(request.Vigencia))
            return new GarantiaConsultaRequest { Vigencia = FiltrosLegacy.Texto(request.Vigencia) };

        if (!string.IsNullOrWhiteSpace(request.Garantia))
            return new GarantiaConsultaRequest { Garantia = FiltrosLegacy.Texto(request.Garantia) };

        if (conRamaDeDiagnostico && !string.IsNullOrWhiteSpace(request.Diagnostico))
            return new GarantiaConsultaRequest { Diagnostico = FiltrosLegacy.Texto(request.Diagnostico) };

        if (!string.IsNullOrWhiteSpace(request.Folio))
            return new GarantiaConsultaRequest { Folio = FiltrosLegacy.Texto(request.Folio) };

        return request;
    }
}
