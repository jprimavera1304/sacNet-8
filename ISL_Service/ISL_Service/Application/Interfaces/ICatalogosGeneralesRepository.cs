using ISL_Service.Application.DTOs.CatalogosGenerales;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Los 28 catalogos de SOLO LECTURA que venian de MacServicios2. Ninguno escribe.
/// Cada metodo llama al mismo sp_n_Consulta* que usaba Legacy, con los mismos parametros:
/// es la unica forma de garantizar que el dato sea identico mientras los dos backends
/// convivan. Los sp_n_ no se tocan (se leen).
/// </summary>
public interface ICatalogosGeneralesRepository
{
    Task<List<AgenteDisponibleDto>> ConsultarAgentesDisponiblesAsync(int numeroAgente, CancellationToken ct = default);

    Task<List<CajaDto>> ConsultarCajasAsync(int idCaja, int idStatus, int numCaja, string nombreCaja, string equipoCaja, CancellationToken ct = default);

    Task<List<CentroServicioDto>> ConsultarCentrosServicioAsync(int idCentro, int idStatus, string nombreCentro, int identico, CancellationToken ct = default);

    Task<List<CiudadDto>> ConsultarCiudadesAsync(string idEstado, string idCiudad, string ciudad, int identico, CancellationToken ct = default);

    Task<List<ColoniaDto>> ConsultarColoniasAsync(ConsultaColoniasFiltro filtro, CancellationToken ct = default);

    Task<List<ConceptoDto>> ConsultarConceptosAsync(int idConcepto, int idStatus, string numConcepto, string nombreConcepto, CancellationToken ct = default);

    Task<List<ConceptoDto>> ConsultarSiguienteNumeroCuentaAsync(int idConcepto, CancellationToken ct = default);

    Task<List<DocumentoDto>> ConsultarDocumentosAsync(int idDocumento, int idStatus, string documento, int identico, CancellationToken ct = default);

    Task<List<EconomicoAuxiliarDto>> ConsultarEconomicosAuxiliaresAsync(ConsultaEconomicosAuxiliaresFiltro filtro, CancellationToken ct = default);

    Task<List<EstadoDto>> ConsultarEstadosAsync(int idEstado, string estado, int identico, CancellationToken ct = default);

    Task<List<FeRegimenFiscalDto>> ConsultarFeRegimenesFiscalesAsync(int idRegimen, string feClave, string feRegimenFiscal, string rfc, int feFisica, int feMoral, int identico, CancellationToken ct = default);

    Task<List<FeRelacionFacturaDto>> ConsultarFeRelacionesFacturaAsync(int idRelacion, string feClave, int identico, CancellationToken ct = default);

    Task<List<FeUsoCfdiDto>> ConsultarFeUsosCfdiAsync(int idUsoCfdi, string feClave, string feUsoCfdi, string rfc, int feFisica, int feMoral, int identico, CancellationToken ct = default);

    Task<List<GalleroDto>> ConsultarGallerosAsync(int idGallero, int idStatus, string gallero, CancellationToken ct = default);

    Task<List<GrupoClienteDto>> ConsultarGruposClienteAsync(int idGrupoCliente, int idStatus, CancellationToken ct = default);

    Task<List<MunicipioDto>> ConsultarMunicipiosAsync(string idEstado, string idMunicipio, string municipio, int identico, CancellationToken ct = default);

    Task<List<PuestoDto>> ConsultarPuestosAsync(int idStatus, CancellationToken ct = default);

    Task<List<TipoNotaCreditoDto>> ConsultarTiposNotaCreditoAsync(int idStatus, CancellationToken ct = default);

    Task<List<TipoPagoDto>> ConsultarTiposPagoAsync(int idTipoPago, int idStatus, string tipoPago, int identico, int esPago, CancellationToken ct = default);

    Task<List<TipoSueldoDto>> ConsultarTiposSueldoAsync(int idStatus, CancellationToken ct = default);

    Task<List<TipoDocumentoDto>> ConsultarTiposDocumentoAsync(int idTipoDocumento, int mostrarPantalla, int sinDevCliente, int sinCambioVigencia, CancellationToken ct = default);

    Task<List<TipoRecordatorioDto>> ConsultarTiposRecordatorioAsync(int idTipoRecordatorio, int idStatus, string tipoRecordatorio, int esUrgente, int esImportante, int identico, CancellationToken ct = default);

    Task<List<UnidadMedidaDto>> ConsultarUnidadesMedidaAsync(int idUnidadMedida, int idStatus, string unidadMedida, int esPieza, int esCaja, int esServicio, int identico, CancellationToken ct = default);

    Task<List<UnidadTerritorialDto>> ConsultarUnidadesTerritorialesAsync(int idUnidadTerritorial, int idStatus, string clave, CancellationToken ct = default);

    Task<List<CategoriaMarcaDto>> ConsultarCategoriasMarcasAsync(int idCategoria, int idMarca, int marcado, CancellationToken ct = default);

    Task<List<GrupoCategoriaMarcaDto>> ConsultarGrupoCategoriasMarcasAsync(int idCategoria, int idMarca, CancellationToken ct = default);

    Task<List<FormaDto>> ConsultarFormasAsync(int idForma, int aplicaContrasena, CancellationToken ct = default);
}

/// <summary>
/// Filtro de colonias. Son once parametros porque el SP tiene once y la pantalla de
/// domicilios los usa todos (busca por codigo postal, o por estado + municipio, o por texto).
/// </summary>
public class ConsultaColoniasFiltro
{
    public int IdColonia { get; set; }
    public string IdEstado { get; set; } = string.Empty;
    public string IdCiudad { get; set; } = string.Empty;
    public string IdMunicipio { get; set; } = string.Empty;
    public string IdAsentamiento { get; set; } = string.Empty;
    public string Colonia { get; set; } = string.Empty;
    public string CodigoPostal { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string Ciudad { get; set; } = string.Empty;
    public string Municipio { get; set; } = string.Empty;
    /// <summary>0 = contiene / empieza con, 1 = texto exacto.</summary>
    public int Identico { get; set; }
}

/// <summary>Filtro de auxiliares por economico. Ocho parametros, los mismos del SP.</summary>
public class ConsultaEconomicosAuxiliaresFiltro
{
    public int IdRecaudacion { get; set; }
    public int IdEconomico { get; set; }
    public int NumEconomico { get; set; }
    public int IdPermisionario { get; set; }
    public int IdCuenta { get; set; }
    public string NumCuenta { get; set; } = string.Empty;
    public int IdCaja { get; set; }
    public int StatusPago { get; set; }
}
