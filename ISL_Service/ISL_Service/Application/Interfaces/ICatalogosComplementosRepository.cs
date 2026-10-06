using ISL_Service.Application.DTOs.CatalogosComplementos;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Los siete catalogos de lectura que la primera ola de catalogos no cubrio.
/// </summary>
public interface ICatalogosComplementosRepository
{
    Task<List<TipoDomicilioDto>> ConsultarTiposDomicilioAsync(int id, int idStatus, string texto, CancellationToken ct);
    Task<List<CondicionCreditoDto>> ConsultarCondicionesCreditoAsync(int id, int idStatus, string texto, bool exacto, CancellationToken ct);
    Task<List<EmpresaSerieDto>> ConsultarEmpresasSeriesAsync(int id, int idEmpresa, int idStatus, string texto, bool exacto, CancellationToken ct);

    /// <summary>Formas de pago, metodos de pago o motivos de cancelacion del SAT.</summary>
    Task<List<CatalogoFeDto>> ConsultarCatalogoFeAsync(CatalogoFe cual, int id, string clave, bool exacto, CancellationToken ct);

    Task<List<MarcaCatalogoDto>> ConsultarMarcasAsync(string idsMarca, int idStatus, string texto, bool exacto, CancellationToken ct);
}

/// <summary>Cual de los tres catalogos del SAT con la misma forma se pide.</summary>
public enum CatalogoFe
{
    FormasPago = 1,
    MetodosPago = 2,
    MotivosCancelacion = 3
}
