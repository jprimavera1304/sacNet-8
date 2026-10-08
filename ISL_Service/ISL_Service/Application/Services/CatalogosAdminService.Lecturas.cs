using ISL_Service.Application.DTOs.CatalogosAdmin;

namespace ISL_Service.Application.Services;

/// <summary>
/// Las lecturas de los siete catalogos editables. Pasa derecho al repositorio,
/// igual que ConsultarDiasFestivosAsync: no hay nada que decidir entre el SP y
/// el controlador, y una capa que solo reenvia pero "por si acaso" transforma es
/// donde se cuelan las diferencias entre lo que ve Mac31 y lo que ve el web.
/// </summary>
public partial class CatalogosAdminService
{
    public Task<List<Dictionary<string, object?>>> ConsultarAlmacenesAsync(AlmacenesFiltro f, CancellationToken ct)
        => _repo.ConsultarAlmacenesAsync(f, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarAgentesAsync(AgentesFiltro f, CancellationToken ct)
        => _repo.ConsultarAgentesAsync(f, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarTiposGastoAsync(TiposGastoFiltro f, CancellationToken ct)
        => _repo.ConsultarTiposGastoAsync(f, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarAutosAsync(AutosFiltro f, CancellationToken ct)
        => _repo.ConsultarAutosAsync(f, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarGruposCategoriaAsync(GruposCategoriaFiltro f, CancellationToken ct)
        => _repo.ConsultarGruposCategoriaAsync(f, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarCategoriasMacAsync(CategoriasMacFiltro f, CancellationToken ct)
        => _repo.ConsultarCategoriasMacAsync(f, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarTiposUsadoAsync(TiposUsadoFiltro f, CancellationToken ct)
        => _repo.ConsultarTiposUsadoAsync(f, ct);

    public Task<Dictionary<string, object?>?> ConsultarConstantesOperacionAsync(CancellationToken ct)
        => _repo.ConsultarConstantesOperacionAsync(ct);

    public Task<List<Dictionary<string, object?>>> ConsultarEmpresasMacAsync(EmpresasMacFiltro f, CancellationToken ct)
        => _repo.ConsultarEmpresasMacAsync(f, ct);

    public async Task<CatalogoEscrituraResponse> RelacionarCategoriaMarcasAsync(
        int idCategoria, IReadOnlyList<MarcaDeLaCategoria> marcas, CancellationToken ct)
    {
        var r = await _repo.RelacionarCategoriaMarcasAsync(idCategoria, marcas, ct);
        return new CatalogoEscrituraResponse { Guardo = r.Ok, Mensaje = r.Mensaje, Id = idCategoria };
    }
}
