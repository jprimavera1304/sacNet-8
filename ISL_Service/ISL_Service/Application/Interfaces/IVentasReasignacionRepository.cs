using ISL_Service.Application.DTOs.VentasReasignacion;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Las lecturas de la pantalla de reasignar cartera. NINGUNA escribe.
/// </summary>
/// <remarks>
/// La escritura vive aparte, en <see cref="IVentasPagosOperacionesService"/>
/// (`POST /api/ventas/reasignar`), y se queda ahi: separar la lectura de la
/// escritura es lo que permite que esta interfaz se pueda dar por buena de un
/// vistazo.
/// </remarks>
public interface IVentasReasignacionRepository
{
    /// <summary>El catalogo completo de agentes, con su estatus.</summary>
    Task<List<AgenteDeReasignacionDto>> ConsultarAgentesAsync(CancellationToken ct);

    /// <summary>Un cliente por su IDCliente, con su agente actual. null si no existe.</summary>
    Task<ClienteDeReasignacionDto?> ConsultarClienteAsync(int idCliente, CancellationToken ct);

    /// <summary>
    /// Candidatos para el buscador. Por numero es exacto; por nombre, contiene.
    /// </summary>
    Task<ClienteCandidatoResponse> BuscarClientesAsync(int numero, string? nombre, CancellationToken ct);

    /// <summary>
    /// Las notas VIGENTES del cliente, SIN recortar por usuario. Ver el bloque
    /// de los DTOs para por que el recorte no se aplica aqui.
    /// </summary>
    Task<(List<Dictionary<string, object?>> Filas, int Total)> ConsultarRemisionesVigentesAsync(
        int idCliente, string desde, string hasta, CancellationToken ct);
}

/// <summary>Las reglas de la lectura de reasignacion.</summary>
public interface IVentasReasignacionService
{
    /// <summary>
    /// Todo lo que la pantalla necesita para abrirse. Con
    /// <paramref name="idCliente"/> en 0 devuelve solo el catalogo de agentes.
    /// </summary>
    Task<ReasignacionArranqueResponse> ArranqueAsync(
        int idCliente, string? desde, string? hasta, CancellationToken ct);

    /// <summary>El buscador de cliente de esta pantalla.</summary>
    Task<ClienteCandidatoResponse> BuscarClientesAsync(string? texto, CancellationToken ct);
}
