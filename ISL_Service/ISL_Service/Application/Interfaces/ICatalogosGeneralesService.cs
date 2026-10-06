using ISL_Service.Application.DTOs.CatalogosGenerales;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Reglas de los catalogos generales: normalizar los filtros de texto antes de que lleguen
/// al SP. No hay nada mas que decidir: son consultas.
/// </summary>
public interface ICatalogosGeneralesService
{
    Task<List<AgenteDisponibleDto>> ConsultarAgentesDisponiblesAsync(int? numeroAgente, CancellationToken ct = default);

    Task<List<CajaDto>> ConsultarCajasAsync(int? idCaja, int? idStatus, int? numCaja, string? nombreCaja, string? equipoCaja, CancellationToken ct = default);

    Task<List<CentroServicioDto>> ConsultarCentrosServicioAsync(int? idCentro, int? idStatus, string? nombreCentro, bool identico, CancellationToken ct = default);

    Task<List<CiudadDto>> ConsultarCiudadesAsync(string? idEstado, string? idCiudad, string? ciudad, bool identico, CancellationToken ct = default);

    Task<List<ColoniaDto>> ConsultarColoniasAsync(ConsultaColoniasFiltro filtro, CancellationToken ct = default);

    Task<List<ConceptoDto>> ConsultarConceptosAsync(int? idConcepto, int? idStatus, string? numConcepto, string? nombreConcepto, CancellationToken ct = default);

    Task<List<ConceptoDto>> ConsultarSiguienteNumeroCuentaAsync(int idConcepto, CancellationToken ct = default);

    Task<List<DocumentoDto>> ConsultarDocumentosAsync(int? idDocumento, int? idStatus, string? documento, bool identico, CancellationToken ct = default);

    Task<List<EconomicoAuxiliarDto>> ConsultarEconomicosAuxiliaresAsync(ConsultaEconomicosAuxiliaresFiltro filtro, CancellationToken ct = default);

    Task<List<EstadoDto>> ConsultarEstadosAsync(int? idEstado, string? estado, bool identico, CancellationToken ct = default);

    Task<List<FeRegimenFiscalDto>> ConsultarFeRegimenesFiscalesAsync(int? idRegimen, string? feClave, string? feRegimenFiscal, string? rfc, bool? feFisica, bool? feMoral, bool identico, CancellationToken ct = default);

    Task<List<FeRelacionFacturaDto>> ConsultarFeRelacionesFacturaAsync(int? idRelacion, string? feClave, bool identico, CancellationToken ct = default);

    Task<List<FeUsoCfdiDto>> ConsultarFeUsosCfdiAsync(int? idUsoCfdi, string? feClave, string? feUsoCfdi, string? rfc, bool? feFisica, bool? feMoral, bool identico, CancellationToken ct = default);

    Task<List<GalleroDto>> ConsultarGallerosAsync(int? idGallero, int? idStatus, string? gallero, CancellationToken ct = default);

    Task<List<GrupoClienteDto>> ConsultarGruposClienteAsync(int? idGrupoCliente, int? idStatus, CancellationToken ct = default);

    Task<List<MunicipioDto>> ConsultarMunicipiosAsync(string? idEstado, string? idMunicipio, string? municipio, bool identico, CancellationToken ct = default);

    Task<List<PuestoDto>> ConsultarPuestosAsync(int? idStatus, CancellationToken ct = default);

    Task<List<TipoNotaCreditoDto>> ConsultarTiposNotaCreditoAsync(int? idStatus, CancellationToken ct = default);

    Task<List<TipoPagoDto>> ConsultarTiposPagoAsync(int? idTipoPago, int? idStatus, string? tipoPago, bool identico, bool? esPago, CancellationToken ct = default);

    Task<List<TipoSueldoDto>> ConsultarTiposSueldoAsync(int? idStatus, CancellationToken ct = default);

    Task<List<TipoDocumentoDto>> ConsultarTiposDocumentoAsync(int? idTipoDocumento, bool? mostrarPantalla, bool sinDevCliente, bool sinCambioVigencia, CancellationToken ct = default);

    Task<List<TipoRecordatorioDto>> ConsultarTiposRecordatorioAsync(int? idTipoRecordatorio, int? idStatus, string? tipoRecordatorio, bool? esUrgente, bool? esImportante, bool identico, CancellationToken ct = default);

    Task<List<UnidadMedidaDto>> ConsultarUnidadesMedidaAsync(int? idUnidadMedida, int? idStatus, string? unidadMedida, bool? esPieza, bool? esCaja, bool? esServicio, bool identico, CancellationToken ct = default);

    Task<List<UnidadTerritorialDto>> ConsultarUnidadesTerritorialesAsync(int? idUnidadTerritorial, int? idStatus, string? clave, CancellationToken ct = default);

    Task<List<CategoriaMarcaDto>> ConsultarCategoriasMarcasAsync(int idCategoria, int? idMarca, bool soloMarcadas, CancellationToken ct = default);

    Task<List<GrupoCategoriaMarcaDto>> ConsultarGrupoCategoriasMarcasAsync(int idCategoria, int? idMarca, CancellationToken ct = default);

    Task<List<FormaDto>> ConsultarFormasAsync(int? idForma, bool? aplicaContrasena, CancellationToken ct = default);
}
