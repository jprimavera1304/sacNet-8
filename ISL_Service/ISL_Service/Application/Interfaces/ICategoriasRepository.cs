using ISL_Service.Application.DTOs.Categorias;

namespace ISL_Service.Application.Interfaces;

public interface ICategoriasRepository
{
    Task<List<CategoriaDto>> ConsultarAsync(byte? estado, string? texto, CancellationToken ct = default);
    Task<CategoriaDto?> InsertarAsync(CreateCategoriaRequest request, Guid usuarioId, CancellationToken ct = default);
    Task<CategoriaDto?> ActualizarAsync(Guid id, UpdateCategoriaRequest request, Guid usuarioId, CancellationToken ct = default);
    Task<CategoriaDto?> InhabilitarAsync(Guid id, string? motivo, Guid usuarioId, CancellationToken ct = default);
}

