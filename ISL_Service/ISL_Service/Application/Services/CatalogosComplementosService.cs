using ISL_Service.Application.DTOs.CatalogosComplementos;
using ISL_Service.Application.Interfaces;
using ISL_Service.Utils;

namespace ISL_Service.Application.Services;

/// <summary>
/// Normaliza los filtros de los catalogos complementarios antes del SP.
///
/// EL APOSTROFO. Los siete SP concatenan el filtro de texto y corren el WHERE
/// con EXEC(@Sql); el SqlParameter no protege adentro. De los siete, legacy no
/// quitaba el apostrofo en NINGUNO en la rama de consulta (los Replace que
/// tienen sus services estan en las de alta y modificacion). Aqui se quita en
/// todos, como ya se hizo en los catalogos de la primera ola.
/// </summary>
public class CatalogosComplementosService : ICatalogosComplementosService
{
    private readonly ICatalogosComplementosRepository _repository;

    public CatalogosComplementosService(ICatalogosComplementosRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Sin apostrofo y recortado. El vacio se devuelve como "" porque es con lo
    /// que estos SP entienden "sin filtro" (comparan `IF @X &lt;&gt; ''`).
    ///
    /// Tambien convierte el null a "": los InputDTO de legacy no inicializaban
    /// estos strings, asi que un body sin el campo mandaba un SqlParameter con
    /// Value = null y el comando fallaba con "Parameter '@Marca' has no default
    /// value" — un 500 por omitir un filtro opcional.
    /// </summary>
    private static string Texto(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return string.Empty;
        return valor.Replace("'", string.Empty).Trim();
    }

    public Task<List<TipoDomicilioDto>> ConsultarTiposDomicilioAsync(int id, int idStatus, string? texto, CancellationToken ct)
        => _repository.ConsultarTiposDomicilioAsync(id, idStatus, Texto(texto), ct);

    public Task<List<CondicionCreditoDto>> ConsultarCondicionesCreditoAsync(int id, int idStatus, string? texto, bool exacto, CancellationToken ct)
        => _repository.ConsultarCondicionesCreditoAsync(id, idStatus, Texto(texto), exacto, ct);

    public Task<List<EmpresaSerieDto>> ConsultarEmpresasSeriesAsync(int id, int idEmpresa, int idStatus, string? texto, bool exacto, CancellationToken ct)
        => _repository.ConsultarEmpresasSeriesAsync(id, idEmpresa, idStatus, Texto(texto), exacto, ct);

    public Task<List<CatalogoFeDto>> ConsultarCatalogoFeAsync(CatalogoFe cual, int id, string? clave, bool exacto, CancellationToken ct)
        => _repository.ConsultarCatalogoFeAsync(cual, id, Texto(clave), exacto, ct);

    public Task<List<MarcaCatalogoDto>> ConsultarMarcasAsync(
        int id, IEnumerable<int>? ids, int idStatus, string? texto, bool exacto, CancellationToken ct)
    {
        // sp_n_ConsultaMarcas no tiene @IDMarca: solo @IDsMarca, una cadena que
        // el SP parte con fn_DevuelveCadenaEnTabla(@IDsMarca, '~'). Un id suelto
        // se convierte a esa cadena, y gana sobre la lista — igual que en Mac31
        // (MarcaService.cs:116-119).
        string idsMarca;
        if (id != 0)
        {
            idsMarca = id.ToString();
        }
        else
        {
            // El 0 se descarta en vez de mandarse: el SP NO tiene la guarda
            // `<> '0'` que si tienen los filtros de categoria, asi que un
            // IDsMarca = "0" se vuelve `IDMarca IN (0)` y devuelve CERO
            // renglones. "Sin filtro" tiene que viajar como cadena vacia.
            var limpios = (ids ?? Array.Empty<int>()).Where(x => x != 0).Distinct();
            idsMarca = string.Join(Variables.DELIMITADOR_PARAM_TILDE_SP, limpios);
        }

        return _repository.ConsultarMarcasAsync(idsMarca, idStatus, Texto(texto), exacto, ct);
    }
}
