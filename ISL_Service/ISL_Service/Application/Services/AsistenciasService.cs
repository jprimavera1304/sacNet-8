using System.Text.RegularExpressions;
using ISL_Service.Application.DTOs.Asistencias;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Application.Services;

public class AsistenciasService : IAsistenciasService
{
    // Solo HH:mm o HH:mm:ss. Legacy mete la cadena directo en una columna
    // 'time', asi que cualquier otra cosa o revienta o se convierte en algo que
    // nadie escribio. Se filtra antes de que llegue a la base.
    private static readonly Regex FormatoHora = new(@"^([01]\d|2[0-3]):[0-5]\d(:[0-5]\d)?$", RegexOptions.Compiled);

    // El unico tipo de sueldo que tiene dias generados, y por tanto el unico
    // que puede producir una rejilla. Ver ConsultarPeriodosAsync.
    private const int TipoSueldoNomina = 1;

    private readonly IAsistenciasRepository _repository;

    public AsistenciasService(IAsistenciasRepository repository)
    {
        _repository = repository;
    }

    public Task<List<PeriodoNominaDto>> ConsultarPeriodosAsync(int idTipoSueldo, int top, CancellationToken ct = default)
        // 52 semanas por default: un anio hacia atras es lo que se corrige en la
        // practica. Se topa en 520 para que un top absurdo no arrastre los 540
        // periodos completos a un combo.
        //
        // SIN TIPO DE SUELDO SE ENTIENDE "NOMINA" (1), NO "TODOS".
        //
        // Antes 0 significaba "todos" y el combo salia con periodos de COMISION
        // adentro. Elegir uno de esos no puede funcionar NUNCA:
        // sp_n_ConsultaPeriodosTipoSueldoDias hace INNER JOIN contra
        // PeriodosTipoSueldoDias y un periodo de comision no tiene dias, asi que
        // /periodos/{id}/dias contesta 404 y la rejilla sale vacia sin explicar
        // por que.
        //
        // No es una limitacion nuestra: Mac31 tampoco los ve, porque su consulta
        // (Asistencias.cs:259, ConsultaDias) pasa por ese mismo INNER JOIN.
        // Verificado contra las dos bases: los 1897 dias de Tauro y los 1656 de
        // Zaragoza son TODOS de IDTipoSueldo = 1, sin una sola excepcion.
        //
        // Quien de verdad quiera comision lo pide explicito (idTipoSueldo=2) y
        // recibe lo mismo que le daria legacy: nada.
        => _repository.ConsultarPeriodosAsync(idTipoSueldo > 0 ? idTipoSueldo : TipoSueldoNomina, top <= 0 ? 52 : Math.Min(top, 520), ct);

    public async Task<List<DiaPeriodoDto>> ConsultarDiasAsync(int idPeriodoTipoSueldo, CancellationToken ct = default)
    {
        if (idPeriodoTipoSueldo <= 0)
            throw new ArgumentException("idPeriodoTipoSueldo es requerido.");

        var dias = await _repository.ConsultarDiasAsync(idPeriodoTipoSueldo, ct);
        if (dias.Count == 0)
            throw new NotFoundException("Ese periodo no existe o no tiene dias generados.");

        return dias;
    }

    public async Task<RejillaAsistenciaResponse> ConsultarRejillaAsync(
        int idPeriodoTipoSueldo, int idEmpleado, int idStatus, CancellationToken ct = default)
    {
        if (idPeriodoTipoSueldo <= 0)
            throw new ArgumentException("idPeriodoTipoSueldo es requerido.");

        return await _repository.ConsultarRejillaAsync(
            idPeriodoTipoSueldo,
            Math.Max(idEmpleado, 0),
            AsistenciaStatus.Normalizar(idStatus),
            ct);
    }

    public async Task GuardarDiaAsync(GuardarAsistenciaDiaRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        if (request.IdEmpleado <= 0)
            throw new ArgumentException("idEmpleado es requerido.");
        if (request.Fecha is null)
            throw new ArgumentException("fecha es requerida.");
        if (request.Inasistencia && request.Vacacion)
            throw new ArgumentException("Un dia no puede ser falta y vacacion al mismo tiempo.");

        request.HoraEntrada = NormalizarHora(request.HoraEntrada, "entrada");
        request.HoraSalida = NormalizarHora(request.HoraSalida, "salida");

        // SALIDA SIN ENTRADA, NO.
        //
        // Ni el sp_w_ ni legacy lo revisan, pero la base lo paga: es EXACTAMENTE
        // lo que rompe los totales del periodo en Zaragoza. A su copia de
        // sp_n_ConsultaEmpleadosPeriodoAsistencia le falta el
        // ISNULL(@diaHorasLaboradas, 0) que si tiene la de Tauro, asi que un
        // DATEDIFF contra una entrada NULL deja en NULL la suma del periodo
        // COMPLETO de esa persona. Medido en el periodo 12551 de MacZ: 3 de 85
        // empleados sin totales, y uno de ellos (EDDER ROBLES SANCHEZ) por tener
        // el dia 1 con salida 18:54 y sin entrada.
        //
        // Mac31 tampoco lo permite, aunque lo consigue de otra forma: la hora de
        // salida solo se puede escribir con la casilla SALIDA marcada, y esa
        // casilla solo se enciende cuando el dia tiene entrada
        // (Asistencias.cs:206 y :239-245). Aqui se dice con una regla en vez de
        // con un control apagado.
        //
        // No aplica a la huella: el checador entra por otro camino, no por este
        // endpoint (aqui @IDEmpleadoHuella siempre va en 0 = captura manual).
        if (!request.Inasistencia && !request.Vacacion
            && string.IsNullOrEmpty(request.HoraEntrada) && !string.IsNullOrEmpty(request.HoraSalida))
        {
            throw new ArgumentException("No se puede capturar una hora de salida sin la hora de entrada.");
        }

        // Ni falta, ni vacacion, ni horas: no hay nada que guardar. Legacy
        // aceptaria y dejaria un renglon vacio que en la rejilla se ve igual
        // que no haber capturado nada, pero que ya no se puede distinguir.
        if (!request.Inasistencia && !request.Vacacion
            && string.IsNullOrEmpty(request.HoraEntrada) && string.IsNullOrEmpty(request.HoraSalida))
        {
            throw new ArgumentException("Capture al menos una hora, o marque falta o vacacion. Para dejar el dia en blanco, use borrar.");
        }

        await _repository.GuardarDiaAsync(request, idUsuario, equipo, ct);
    }

    public async Task EliminarDiaAsync(int idEmpleado, DateTime? fecha, int idUsuario, string equipo, CancellationToken ct = default)
    {
        if (idEmpleado <= 0)
            throw new ArgumentException("idEmpleado es requerido.");
        if (fecha is null)
            throw new ArgumentException("fecha es requerida.");

        try
        {
            await _repository.EliminarDiaAsync(idEmpleado, fecha.Value, idUsuario, equipo, ct);
        }
        catch (SqlException ex) when (ex.Message.Contains("no tiene asistencia capturada", StringComparison.OrdinalIgnoreCase))
        {
            // El sp_w_ avisa con RAISERROR, que por default se convierte en 400.
            // Aqui es un 404 de verdad: se pidio borrar algo que no existe.
            throw new NotFoundException("Ese empleado no tiene asistencia capturada en esa fecha.");
        }
    }

    /// <summary>
    /// Vacio -> cadena vacia (significa "no hay hora"). Formato malo -> 400.
    /// Nunca se deja pasar una hora dudosa: legacy la escribe tal cual en una
    /// columna 'time' y despues no hay como saber que se escribio.
    /// </summary>
    private static string NormalizarHora(string? hora, string cual)
    {
        if (string.IsNullOrWhiteSpace(hora)) return string.Empty;

        var limpia = hora.Trim();
        if (!FormatoHora.IsMatch(limpia))
            throw new ArgumentException($"La hora de {cual} debe venir como HH:mm (24 horas).");

        // Se recorta a HH:mm, que es como la guarda y la muestra Mac31.
        return limpia.Length >= 5 ? limpia.Substring(0, 5) : limpia;
    }
}
