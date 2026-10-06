using ISL_Service.Application.DTOs.UsuariosAnexos;

namespace ISL_Service.Application.Interfaces;

/// <summary>Las tres lecturas sueltas de la pantalla de usuarios de Mac31.</summary>
public interface IUsuariosAnexosRepository
{
    Task<List<UsuarioFormaProcesoDto>> ConsultarFormasProcesosAsync(int idUsuario, CancellationToken ct);
    Task<List<UsuarioFechaDto>> ConsultarFechasAsync(int idUsuario, CancellationToken ct);

    /// <summary>El siguiente NumUsuario libre: MAX(NumUsuario) + 1 entre los activos.</summary>
    Task<int> ConsultarSiguienteNumeroAsync(CancellationToken ct);
}

/// <inheritdoc cref="IUsuariosAnexosRepository"/>
public interface IUsuariosAnexosService
{
    Task<List<UsuarioFormaProcesoDto>> ConsultarFormasProcesosAsync(int idUsuario, CancellationToken ct);
    Task<List<UsuarioFechaDto>> ConsultarFechasAsync(int idUsuario, CancellationToken ct);
    Task<int> ConsultarSiguienteNumeroAsync(CancellationToken ct);
}
