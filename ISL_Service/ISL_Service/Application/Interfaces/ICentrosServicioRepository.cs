using ISL_Service.Application.DTOs.Reportes;

namespace ISL_Service.Application.Interfaces;

/// <summary>El catalogo de centros de servicio, leido de la base PRINCIPAL.</summary>
public interface ICentrosServicioRepository
{
    /// <summary>Los centros activos. Vacia si la tabla no existe en esta base.</summary>
    Task<IReadOnlyList<CentroServicioItem>> ListarAsync(CancellationToken ct = default);

    /// <summary>
    /// El centro con ese Id, o null si no existe o esta inactivo.
    /// Es la UNICA forma de llegar al nombre de una base: por Id, nunca por texto.
    /// </summary>
    Task<CentroServicioItem?> BuscarAsync(int idCentro, CancellationToken ct = default);
}
