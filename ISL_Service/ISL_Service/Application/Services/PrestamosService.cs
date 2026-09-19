using System.Globalization;
using ISL_Service.Application.DTOs.Empleados;
using ISL_Service.Application.DTOs.Prestamos;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

public class PrestamosService : IPrestamosService
{
    private readonly IPrestamosRepository _repository;

    public PrestamosService(IPrestamosRepository repository)
    {
        _repository = repository;
    }

    public async Task<PrestamosResponse> ConsultarAsync(int idEmpleado, string? estado, CancellationToken ct = default)
    {
        var pagados = PrestamoEstado.ALegacy(estado);
        if (pagados is null)
            throw new ArgumentException("estado debe ser: todos, pagados, pendientes o cancelados.");

        return await _repository.ConsultarAsync(0, Math.Max(idEmpleado, 0), pagados.Value, ct);
    }

    public async Task<Dictionary<string, object?>?> ObtenerAsync(int idEmpleadoPrestamo, CancellationToken ct = default)
    {
        if (idEmpleadoPrestamo <= 0)
            throw new ArgumentException("id es requerido.");

        // @Pagados = 0 (todos) a proposito: al pedir UNO por id se quiere ese,
        // este pagado, pendiente o cancelado.
        var res = await _repository.ConsultarAsync(idEmpleadoPrestamo, 0, 0, ct);
        return res.Rows.FirstOrDefault();
    }

    public async Task<PrestamoCreadoDto> CrearAsync(CrearPrestamoRequest request, int idUsuario, CancellationToken ct = default)
    {
        if (request.IdEmpleado <= 0)
            throw new ArgumentException("idEmpleado es requerido.");
        if (request.MontoPrestamo <= 0)
            throw new ArgumentException("El monto del prestamo debe ser mayor que cero.");
        if (request.MontoPagos < 0)
            throw new ArgumentException("El descuento semanal no puede ser negativo.");
        if (request.FechaPrestamo is null)
            throw new ArgumentException("fechaPrestamo es requerida.");
        if (!request.PagoInmediato && request.FechaInicioPagos is null)
            throw new ArgumentException("fechaInicioPagos es requerida.");
        if (!request.PagoInmediato && request.MontoPagos <= 0)
            throw new ArgumentException("El descuento semanal debe ser mayor que cero.");

        // TRES REGLAS DE MAC31 QUE FALTABAN AQUI.
        //
        // Estan en Prestamos.cs, metodo Validaciones(), y el procedimiento NO
        // las revisa: sp_n_InsertaEmpleadoPrestamos inserta lo que le manden.
        // O sea que sin estas lineas el web podia guardar prestamos que Mac31
        // nunca habria dejado capturar, y la diferencia solo se notaria semanas
        // despues, cuando el descuento semanal no cuadrara.
        //
        // Se repiten en el front (nucleo/prestamos.ts, revisarNuevoPrestamo)
        // para avisar antes de mandar; aqui viven porque el movil entra por la
        // misma puerta y una validacion que solo esta en el navegador no es una
        // validacion.

        // Prestamos.cs:389 — el descuento no puede ser mayor que la deuda: se
        // cobraria de mas en el primer pago.
        if (!request.PagoInmediato && request.MontoPagos > request.MontoPrestamo)
            throw new ArgumentException("El monto de los pagos no puede ser mayor al monto del prestamo.");

        // Prestamos.cs:412 — no se puede empezar a descontar antes de haber
        // prestado. Mac31 compara fechaPrestamo > fechaInicioPagos; el mensaje
        // de alla dice lo contrario de lo que revisa, asi que se conserva LA
        // REGLA y se redacta el aviso como lo que de verdad pasa.
        if (!request.PagoInmediato
            && request.FechaInicioPagos is not null
            && request.FechaPrestamo!.Value.Date > request.FechaInicioPagos.Value.Date)
            throw new ArgumentException("La fecha de inicio de los pagos no puede ser anterior a la fecha del prestamo.");

        // Prestamos.cs:421 — el motivo es obligatorio. Es la unica explicacion
        // de por que existe el prestamo, y es lo primero que se busca cuando
        // alguien reclama un descuento en su recibo.
        if (string.IsNullOrWhiteSpace(request.MotivoPrestamo))
            throw new ArgumentException("Ingrese el motivo del prestamo.");

        // Se pregunta ANTES de insertar: despues del alta ya hay un prestamo
        // abierto de todas formas y no se podria distinguir.
        var yaTenia = await _repository.TienePrestamoAbiertoAsync(request.IdEmpleado, ct);

        var (resultado, id) = await _repository.InsertarAsync(request, idUsuario, ct);
        if (!resultado.Ok)
            throw new ArgumentException(Mensaje(resultado, "No se pudo registrar el prestamo."));

        var creado = new PrestamoCreadoDto { IdEmpleadoPrestamo = id };

        // Cuando el empleado ya tenia un prestamo abierto, legacy NO actualiza
        // su descuento semanal (EmpleadoPrestamosMontoPagos se queda como
        // estaba), y si ademas se mando montoPagos = 0 reusa el del prestamo
        // que ya estaba. El capturista escribio un numero: tiene derecho a
        // saber que no se aplico al descuento del empleado.
        if (yaTenia && !request.PagoInmediato)
        {
            creado.Advertencia =
                "El empleado ya tenia un prestamo abierto. El sistema viejo conserva su descuento semanal anterior: " +
                "si hay que cambiarlo, hagalo desde 'Cambiar descuento semanal'.";
        }

        return creado;
    }

    public async Task CambiarMontoPagosAsync(int idEmpleadoPrestamo, decimal montoPagos, int idUsuario, CancellationToken ct = default)
    {
        if (idEmpleadoPrestamo <= 0)
            throw new ArgumentException("id es requerido.");
        if (montoPagos <= 0)
            throw new ArgumentException("El descuento semanal debe ser mayor que cero.");

        // sp_n_ActualizaEmpleadoPrestamos pide TAMBIEN el IDEmpleado y filtra
        // por los dos. Se saca del prestamo, no del body: si el cliente mandara
        // un idEmpleado que no corresponde, el UPDATE no encontraria renglon y
        // legacy contestaria "exito" sin haber cambiado nada.
        var prestamo = await ObtenerAsync(idEmpleadoPrestamo, ct);
        if (prestamo is null)
            throw new NotFoundException("El prestamo no existe.");

        if (TieneFecha(prestamo, "fechaCancelacion"))
            throw new ConflictException("El prestamo esta cancelado: ya no se puede cambiar su descuento semanal.");
        if (TieneFecha(prestamo, "fechaPago"))
            throw new ConflictException("El prestamo ya esta pagado: ya no se puede cambiar su descuento semanal.");

        // La misma regla de Prestamos.cs:389, tambien al modificar: en Mac31 el
        // boton GUARDAR corre Validaciones() completo, no solo en el alta.
        // Faltaba aqui, y el hueco era peor que en el alta: este endpoint
        // existe justamente para cambiar ese numero.
        var montoPrestamo = ADecimal(prestamo, "montoPrestamo");
        if (montoPrestamo > 0 && montoPagos > montoPrestamo)
            throw new ArgumentException("El monto de los pagos no puede ser mayor al monto del prestamo.");

        var idEmpleado = AEntero(prestamo, "idEmpleado");
        var resultado = await _repository.ActualizarMontoPagosAsync(idEmpleadoPrestamo, idEmpleado, montoPagos, idUsuario, ct);
        if (!resultado.Ok)
            throw new ArgumentException(Mensaje(resultado, "No se pudo cambiar el descuento semanal."));
    }

    public async Task CancelarAsync(int idEmpleadoPrestamo, int idUsuario, CancellationToken ct = default)
    {
        if (idEmpleadoPrestamo <= 0)
            throw new ArgumentException("id es requerido.");

        var prestamo = await ObtenerAsync(idEmpleadoPrestamo, ct);
        if (prestamo is null)
            throw new NotFoundException("El prestamo no existe.");

        // Legacy vuelve a poner [Fecha Cancelacion] sin revisar si ya estaba
        // cancelado, y con eso se pierde la fecha real de la cancelacion
        // original. Se ataja aqui.
        if (TieneFecha(prestamo, "fechaCancelacion"))
            throw new ConflictException("El prestamo ya estaba cancelado.");

        var resultado = await _repository.CancelarAsync(idEmpleadoPrestamo, idUsuario, ct);
        if (!resultado.Ok)
            throw new ArgumentException(Mensaje(resultado, "No se pudo cancelar el prestamo."));
    }

    /// <summary>
    /// Si el prestamo esta cancelado o pagado.
    ///
    /// Se mira la FECHA cruda ('fechaCancelacion' / 'fechaPago'), no la columna
    /// de texto. Las columnas 'cancelado' y 'pagado' de legacy NO son banderas:
    /// valen la cadena 'NO', o la fecha ya formateada
    /// ("28/08/2026 18:34 - JUAN PRI"). Comparar contra "SI" nunca da verdadero
    /// y el candado se queda abierto sin que nadie lo note — que es exactamente
    /// lo que pasaba antes de este cambio.
    /// </summary>
    private static bool TieneFecha(Dictionary<string, object?> row, string llave)
        => row.TryGetValue(llave, out var v) && v is not null;

    private static decimal ADecimal(Dictionary<string, object?> row, string llave)
        => row.TryGetValue(llave, out var v) && v is not null
            ? Convert.ToDecimal(v, CultureInfo.InvariantCulture)
            : 0m;

    private static int AEntero(Dictionary<string, object?> row, string llave)
        => row.TryGetValue(llave, out var v) && v is not null
            ? Convert.ToInt32(v, CultureInfo.InvariantCulture)
            : 0;

    private static string Mensaje(ResultadoLegacy r, string porDefecto)
        => string.IsNullOrWhiteSpace(r.Mensaje) || r.Mensaje.Trim().Equals("ERROR", StringComparison.OrdinalIgnoreCase)
            ? porDefecto
            : r.Mensaje.Trim();
}
