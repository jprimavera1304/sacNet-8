using ISL_Service.Application.DTOs.UsuariosAnexos;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/// <summary>
/// Pasa derecho al repositorio: estos tres SP no reciben texto ni fechas, asi
/// que no hay nada que normalizar. Existe igual para no saltarse la capa — el
/// dia que alguno gane un filtro, el lugar donde ponerlo ya esta.
/// </summary>
public class UsuariosAnexosService : IUsuariosAnexosService
{
    private readonly IUsuariosAnexosRepository _repository;

    public UsuariosAnexosService(IUsuariosAnexosRepository repository)
    {
        _repository = repository;
    }

    public Task<List<UsuarioFormaProcesoDto>> ConsultarFormasProcesosAsync(int idUsuario, CancellationToken ct)
        => _repository.ConsultarFormasProcesosAsync(idUsuario, ct);

    public Task<List<UsuarioFechaDto>> ConsultarFechasAsync(int idUsuario, CancellationToken ct)
        => _repository.ConsultarFechasAsync(idUsuario, ct);

    public Task<int> ConsultarSiguienteNumeroAsync(CancellationToken ct)
        => _repository.ConsultarSiguienteNumeroAsync(ct);
}
