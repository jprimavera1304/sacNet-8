using ISL_Service.Application.DTOs.ActualizacionesAplicacion;

namespace ISL_Service.Application.Interfaces;

/// <summary>Lectura del manifiesto de actualizacion de las aplicaciones de escritorio.</summary>
public interface IActualizacionesAplicacionRepository
{
    Task<List<ActualizacionAplicacionDto>> ConsultarAsync(
        int id, int idAplicacion, int idStatus, string archivoOrigen, CancellationToken ct);
}

/// <inheritdoc cref="IActualizacionesAplicacionRepository"/>
public interface IActualizacionesAplicacionService
{
    Task<List<ActualizacionAplicacionDto>> ConsultarAsync(
        int id, int idAplicacion, int idStatus, string? archivoOrigen, CancellationToken ct);
}
