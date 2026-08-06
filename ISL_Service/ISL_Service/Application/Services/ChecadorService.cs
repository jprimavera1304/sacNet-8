using ISL_Service.Application.DTOs.Checador;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Application.Services;

/// <summary>
/// Checadas de comida: valida lo que se puede validar sin ir a la base, normaliza
/// tipo/origen y traduce los errores del SP a 400/409.
/// </summary>
public class ChecadorService : IChecadorService
{
    // Tamaño de las columnas en WEmpleadoChecada. Se recorta en vez de rechazar:
    // que una descripcion larga tumbe una checada de comida seria peor que perder
    // la cola del texto.
    private const int MotivoMaximo = 400;
    private const int EquipoMaximo = 200;

    // Rango maximo de consulta. El SP devuelve un renglon por empleado y dia, asi
    // que sin tope un "desde 2010" traeria cientos de miles de filas a un front que
    // solo pinta una quincena.
    private const int DiasMaximosConsulta = 366;

    private readonly IChecadorRepository _repository;

    public ChecadorService(IChecadorRepository repository)
    {
        _repository = repository;
    }

    public async Task<ChecadaComidaRegistradaDto> RegistrarComidaAsync(
        RegistrarChecadaComidaRequest request,
        int idUsuario,
        string equipo,
        CancellationToken ct = default)
    {
        if (request == null)
            throw new ArgumentException("Body requerido.");
        if (request.IDEmpleado <= 0)
            throw new ArgumentException("idEmpleado es requerido y debe ser mayor a 0.");

        var tipo = ChecadaComidaTipo.Normalizar(request.Tipo);
        if (tipo == null)
            throw new ArgumentException("tipo invalido. Use COMIDA_INICIO o COMIDA_FIN, o dejelo vacio para que se deduzca.");

        var origen = ChecadaComidaOrigen.Normalizar(request.Origen);
        if (origen == null)
            throw new ArgumentException("origen invalido. Use CHECADOR o WEB.");

        // 0 o negativo no es una huella: se manda NULL para que el SP lo trate como
        // "checada sin lector" en vez de guardar una FK invalida.
        var idEmpleadoHuella = request.IDEmpleadoHuella is > 0 ? request.IDEmpleadoHuella : null;

        try
        {
            return await _repository.RegistrarComidaAsync(
                request.IDEmpleado, tipo, idEmpleadoHuella, origen, idUsuario, Recortar(equipo, EquipoMaximo), ct);
        }
        catch (SqlException ex)
        {
            MapSqlException(ex);
            throw;
        }
    }

    public async Task<ChecadaComidaRowsResponse> ConsultarComidasAsync(
        int idEmpleado,
        DateTime? fechaInicial,
        DateTime? fechaFinal,
        bool incluirCanceladas,
        CancellationToken ct = default)
    {
        if (fechaInicial == null || fechaFinal == null)
            throw new ArgumentException("fechaInicial y fechaFinal son requeridas.");

        var desde = fechaInicial.Value.Date;
        var hasta = fechaFinal.Value.Date;

        if (desde > hasta)
            throw new ArgumentException("fechaInicial no puede ser mayor que fechaFinal.");
        if ((hasta - desde).TotalDays > DiasMaximosConsulta)
            throw new ArgumentException($"El rango no puede ser mayor a {DiasMaximosConsulta} dias.");

        // Negativo no significa nada para el SP; 0 si: "todos los empleados".
        var empleado = idEmpleado > 0 ? idEmpleado : 0;

        try
        {
            return await _repository.ConsultarComidasAsync(empleado, desde, hasta, incluirCanceladas, ct);
        }
        catch (SqlException ex)
        {
            MapSqlException(ex);
            throw;
        }
    }

    public async Task CancelarComidaAsync(
        int idEmpleadoChecada,
        string motivo,
        int idUsuario,
        string equipo,
        CancellationToken ct = default)
    {
        if (idEmpleadoChecada <= 0)
            throw new ArgumentException("idEmpleadoChecada es requerido y debe ser mayor a 0.");
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("motivo es requerido.");

        try
        {
            await _repository.CancelarComidaAsync(
                idEmpleadoChecada, Recortar(motivo, MotivoMaximo), idUsuario, Recortar(equipo, EquipoMaximo), ct);
        }
        catch (SqlException ex)
        {
            MapSqlException(ex);
            throw;
        }
    }

    private static string Recortar(string? texto, int maximo)
    {
        var limpio = (texto ?? string.Empty).Trim();
        return limpio.Length > maximo ? limpio[..maximo] : limpio;
    }

    /// <summary>
    /// Los sp_w_ reportan sus reglas con RAISERROR (numero 50000+), que sin esto
    /// llegaria al cliente como 500. El criterio: si el registro que se pide no
    /// existe es 404; si existe pero su ESTADO no permite la operacion (secuencia
    /// de comida rota, checada ya cancelada) es 409; cualquier otro dato mal
    /// mandado es 400.
    ///
    /// El match es por texto porque RAISERROR no lleva codigo propio: todos salen
    /// como error 50000. Si el SP cambia sus mensajes, esto degrada a 400, que
    /// sigue siendo una respuesta correcta; por eso no se valida al arrancar.
    /// </summary>
    private static void MapSqlException(SqlException ex)
    {
        var msg = ex.Message ?? string.Empty;

        // Huella mal mandada: es un dato del request, no un estado. 400 antes de
        // que el "no existe" de mas abajo lo convierta en 404.
        if (msg.Contains("huella", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(msg);

        if (msg.Contains("ya esta cancelada", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("La checada ya esta cancelada.", msg);

        // Secuencia: cerrar una comida que nunca abrio, o abrir una segunda sin
        // haber cerrado la anterior.
        if (msg.Contains("no tiene una salida a comer abierta", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("ya tiene una salida a comer abierta", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException(msg, msg);

        if (msg.Contains("no existe", StringComparison.OrdinalIgnoreCase))
            throw new NotFoundException(msg, msg);

        if (ex.Number >= 50000 && ex.Number <= 99999)
            throw new ArgumentException(msg);
    }
}
