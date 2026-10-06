using ISL_Service.Application.DTOs.Obra;

namespace ISL_Service.Application.Interfaces;

/// <summary>Capa de aplicacion de obra: normaliza los filtros antes del SP.</summary>
public interface IObraService
{
    Task<List<ObraGerenteDto>> ConsultarGerentesAsync(ObraCatalogoFiltro filtro, int idTipoGerente, CancellationToken ct);
    Task<List<ObraPuestoDto>> ConsultarPuestosAsync(ObraCatalogoFiltro filtro, bool? esRepresentanteLegal, bool? esRepresentanteTecnico, CancellationToken ct);
    Task<List<ObraResidenteDto>> ConsultarResidentesAsync(ObraCatalogoFiltro filtro, int idPuesto, bool? esSupervision, bool? esObra, CancellationToken ct);
    Task<List<ObraTipoDto>> ConsultarTipoAsync(ObraTipoCatalogo tipo, ObraCatalogoFiltro filtro, CancellationToken ct);
    Task<List<ObraUnidadMedidaDto>> ConsultarUnidadesMedidaAsync(ObraCatalogoFiltro filtro, CancellationToken ct);
    Task<List<ObraProveedorDto>> ConsultarProveedoresAsync(ObraProveedorFiltro filtro, CancellationToken ct);

    Task<List<ObraContratoGrupoDto>> ConsultarContratoGruposAsync(int idContratoGrupo, int idContrato, ObraCatalogoFiltro filtro, CancellationToken ct);
    Task<List<ObraContratoConceptoDto>> ConsultarContratoConceptosAsync(int idContratoConcepto, int idContratoGrupo, int idContrato, ObraCatalogoFiltro filtro, CancellationToken ct);
    Task<List<ObraContratoEstimacionDto>> ConsultarContratoEstimacionesAsync(int idContratoEstimacion, int idContrato, ObraCatalogoFiltro filtro, CancellationToken ct);
}
