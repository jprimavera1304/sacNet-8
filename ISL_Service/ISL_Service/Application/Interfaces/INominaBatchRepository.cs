using ISL_Service.Application.DTOs.Nomina;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Los tres batches de nomina. Los tres ESCRIBEN, aunque en legacy dos se
/// llamaran "consultar" y "validar".
/// </summary>
public interface INominaBatchRepository
{
    /// <summary>Si esta empresa usa la variante Tauro del recibo (Constantes.AppMac).</summary>
    Task<bool> EsTauroAsync(CancellationToken ct);

    /// <summary>Calcula los recibos del periodo: borra los existentes y los vuelve a insertar.</summary>
    Task<List<Dictionary<string, object?>>> CalcularRecibosAsync(CalcularRecibosRequest r, bool esTauro, CancellationToken ct);

    /// <summary>Crea los periodos de nomina que falten.</summary>
    Task<List<Dictionary<string, object?>>> GenerarPeriodosAsync(int idUsuario, CancellationToken ct);

    /// <summary>Descuenta los prestamos de la nomina. Mueve dinero.</summary>
    Task<List<Dictionary<string, object?>>> DescontarPrestamosAsync(int idUsuario, CancellationToken ct);
}
