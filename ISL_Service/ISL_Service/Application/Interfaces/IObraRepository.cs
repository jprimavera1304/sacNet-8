using ISL_Service.Application.DTOs.Obra;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Lectura de los catalogos y contratos de obra (las tablas MC_* de Mac31).
/// Solo lectura: los `sp_n_Insertar_MC*` / `sp_n_Actualizar_MC*` existen en la
/// base y NO se exponen aqui.
/// </summary>
public interface IObraRepository
{
    Task<List<ObraGerenteDto>> ConsultarGerentesAsync(ObraCatalogoFiltro filtro, int idTipoGerente, CancellationToken ct);
    Task<List<ObraPuestoDto>> ConsultarPuestosAsync(ObraCatalogoFiltro filtro, int esRepresentanteLegal, int esRepresentanteTecnico, CancellationToken ct);
    Task<List<ObraResidenteDto>> ConsultarResidentesAsync(ObraCatalogoFiltro filtro, int idPuesto, int esSupervision, int esObra, CancellationToken ct);
    Task<List<ObraUnidadMedidaDto>> ConsultarUnidadesMedidaAsync(ObraCatalogoFiltro filtro, CancellationToken ct);

    /// <summary>
    /// Los cuatro catalogos de la misma forma. <paramref name="tipo"/> decide
    /// cual, porque el SP, la columna de id y la del nombre cambian juntos.
    /// </summary>
    Task<List<ObraTipoDto>> ConsultarTipoAsync(ObraTipoCatalogo tipo, ObraCatalogoFiltro filtro, CancellationToken ct);

    Task<List<ObraProveedorDto>> ConsultarProveedoresAsync(ObraProveedorFiltro filtro, CancellationToken ct);

    Task<List<ObraContratoGrupoDto>> ConsultarContratoGruposAsync(int idContratoGrupo, int idContrato, ObraCatalogoFiltro filtro, CancellationToken ct);
    Task<List<ObraContratoConceptoDto>> ConsultarContratoConceptosAsync(int idContratoConcepto, int idContratoGrupo, int idContrato, ObraCatalogoFiltro filtro, CancellationToken ct);
    Task<List<ObraContratoEstimacionDto>> ConsultarContratoEstimacionesAsync(int idContratoEstimacion, int idContrato, ObraCatalogoFiltro filtro, CancellationToken ct);
}

/// <summary>Cual de los cuatro catalogos "id + nombre" de obra se pide.</summary>
public enum ObraTipoCatalogo
{
    TiposContratacion = 1,
    TiposDocumento = 2,
    TiposObra = 3,
    TiposRecurso = 4
}

/// <summary>Filtros propios del catalogo de proveedores de obra.</summary>
public class ObraProveedorFiltro
{
    public int IDProveedor { get; set; }
    public int IDStatus { get; set; }
    public string? Numero { get; set; }
    public string? Nombre { get; set; }
    public string? ApellidoPaterno { get; set; }
    public string? ApellidoMaterno { get; set; }
    public string? Rfc { get; set; }
    public bool Exacto { get; set; }
}
