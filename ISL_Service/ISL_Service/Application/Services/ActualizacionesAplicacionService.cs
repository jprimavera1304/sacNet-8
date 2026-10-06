using ISL_Service.Application.DTOs.ActualizacionesAplicacion;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/// <summary>
/// Normaliza el filtro de nombre de archivo.
///
/// El SP lo concatena dentro de su `EXEC(@Sql)` sin escapar, asi que el apostrofo
/// se quita aqui. Legacy no lo hacia: `/api/aplicacionesactualizacion/consultar`
/// era inyectable con un nombre de archivo con comilla.
/// </summary>
public class ActualizacionesAplicacionService : IActualizacionesAplicacionService
{
    private readonly IActualizacionesAplicacionRepository _repository;

    public ActualizacionesAplicacionService(IActualizacionesAplicacionRepository repository)
    {
        _repository = repository;
    }

    public Task<List<ActualizacionAplicacionDto>> ConsultarAsync(
        int id, int idAplicacion, int idStatus, string? archivoOrigen, CancellationToken ct)
    {
        var archivo = string.IsNullOrWhiteSpace(archivoOrigen)
            ? string.Empty
            : archivoOrigen.Replace("'", string.Empty).Trim();

        return _repository.ConsultarAsync(id, idAplicacion, idStatus, archivo, ct);
    }
}
