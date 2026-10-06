using ISL_Service.Application.DTOs.CatalogosGenerales;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/*
  POR QUE ESTE SERVICIO EXISTE SI SOLO REENVIA.

  Porque hay una cosa que SI decide, y es de seguridad: los `sp_n_Consulta*` de catalogos
  arman su WHERE concatenando cadenas y lo corren con EXEC(@Sql). Un apostrofo en un filtro
  de texto sale del literal y lo que siga se ejecuta. Pasar el filtro como parametro de
  SqlCommand NO protege: el parametro entra limpio al SP y el SP lo concatena adentro.

  Legacy ya se habia topado con esto y hacia `.Replace("'", "")` suelto en tres o cuatro
  servicios (CatFERegimenFiscalService, CatFEUsoCFDIService, CatGrupoClienteService,
  CatConceptoService), olvidandolo en los demas: ciudades, municipios, colonias, documentos,
  centros de servicio y tipos de recordatorio entraban con el apostrofo intacto.
  Aqui se quita en TODOS, en un solo lugar (`Texto`), y de paso se recorta.

  Los SP no se arreglan porque son `sp_n_` y no se tocan; se cierra la puerta del lado del
  llamador, que es el lado que si es nuestro.
*/

public class CatalogosGeneralesService : ICatalogosGeneralesService
{
    private readonly ICatalogosGeneralesRepository _repository;

    public CatalogosGeneralesService(ICatalogosGeneralesRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Deja un filtro de texto en condiciones de viajar a un SP que concatena SQL:
    /// sin espacios de sobra y sin apostrofos.
    /// </summary>
    private static string Texto(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? string.Empty : valor.Trim().Replace("'", string.Empty);

    /// <summary>0 es el "todos" de estos SP: los filtros numericos ausentes van en 0.</summary>
    private static int Numero(int? valor) => valor ?? 0;

    /// <summary>Los SP usan 0/1 en lugar de bit; un filtro booleano ausente es 0 = no filtrar.</summary>
    private static int Bandera(bool? valor) => valor == true ? 1 : 0;

    private static int Bandera(bool valor) => valor ? 1 : 0;

    public Task<List<AgenteDisponibleDto>> ConsultarAgentesDisponiblesAsync(int? numeroAgente, CancellationToken ct = default)
        => _repository.ConsultarAgentesDisponiblesAsync(Numero(numeroAgente), ct);

    public Task<List<CajaDto>> ConsultarCajasAsync(int? idCaja, int? idStatus, int? numCaja, string? nombreCaja, string? equipoCaja, CancellationToken ct = default)
        => _repository.ConsultarCajasAsync(Numero(idCaja), Numero(idStatus), Numero(numCaja), Texto(nombreCaja), Texto(equipoCaja), ct);

    public Task<List<CentroServicioDto>> ConsultarCentrosServicioAsync(int? idCentro, int? idStatus, string? nombreCentro, bool identico, CancellationToken ct = default)
        => _repository.ConsultarCentrosServicioAsync(Numero(idCentro), Numero(idStatus), Texto(nombreCentro), Bandera(identico), ct);

    /*
      CIUDADES Y MUNICIPIOS: EL "TODOS" ES "0", NO LA CADENA VACIA.

      Los dos SP meten @IDEstado dentro de fn_RegresaTablaEstados(...) por concatenacion. Si
      llega vacio, la llamada queda `fn_RegresaTablaEstados( )` y SQL Server contesta
      "An insufficient number of arguments were supplied". Legacy manda lo que traiga el
      cuerpo, asi que su /api/catciudades/consultar revienta cada vez que el front no pone
      estado: comprobado en Produccion_svr. Aqui el ausente se traduce a "0", que es el
      valor que la funcion entiende como "todos los estados".
    */
    public Task<List<CiudadDto>> ConsultarCiudadesAsync(string? idEstado, string? idCiudad, string? ciudad, bool identico, CancellationToken ct = default)
        => _repository.ConsultarCiudadesAsync(ClaveTerritorial(idEstado), Texto(idCiudad), Texto(ciudad), Bandera(identico), ct);

    public Task<List<MunicipioDto>> ConsultarMunicipiosAsync(string? idEstado, string? idMunicipio, string? municipio, bool identico, CancellationToken ct = default)
        => _repository.ConsultarMunicipiosAsync(ClaveTerritorial(idEstado), ClaveTerritorial(idMunicipio), Texto(municipio), Bandera(identico), ct);

    private static string ClaveTerritorial(string? clave)
    {
        var limpio = Texto(clave);
        return limpio.Length == 0 ? "0" : limpio;
    }

    public Task<List<ColoniaDto>> ConsultarColoniasAsync(ConsultaColoniasFiltro filtro, CancellationToken ct = default)
        => _repository.ConsultarColoniasAsync(new ConsultaColoniasFiltro
        {
            IdColonia = filtro.IdColonia,
            IdEstado = Texto(filtro.IdEstado),
            IdCiudad = Texto(filtro.IdCiudad),
            IdMunicipio = Texto(filtro.IdMunicipio),
            IdAsentamiento = Texto(filtro.IdAsentamiento),
            Colonia = Texto(filtro.Colonia),
            CodigoPostal = Texto(filtro.CodigoPostal),
            Estado = Texto(filtro.Estado),
            Ciudad = Texto(filtro.Ciudad),
            Municipio = Texto(filtro.Municipio),
            Identico = filtro.Identico == 1 ? 1 : 0
        }, ct);

    public Task<List<ConceptoDto>> ConsultarConceptosAsync(int? idConcepto, int? idStatus, string? numConcepto, string? nombreConcepto, CancellationToken ct = default)
        => _repository.ConsultarConceptosAsync(Numero(idConcepto), Numero(idStatus), Texto(numConcepto), Texto(nombreConcepto), ct);

    public Task<List<ConceptoDto>> ConsultarSiguienteNumeroCuentaAsync(int idConcepto, CancellationToken ct = default)
        => _repository.ConsultarSiguienteNumeroCuentaAsync(idConcepto, ct);

    public Task<List<DocumentoDto>> ConsultarDocumentosAsync(int? idDocumento, int? idStatus, string? documento, bool identico, CancellationToken ct = default)
        => _repository.ConsultarDocumentosAsync(Numero(idDocumento), Numero(idStatus), Texto(documento), Bandera(identico), ct);

    public Task<List<EconomicoAuxiliarDto>> ConsultarEconomicosAuxiliaresAsync(ConsultaEconomicosAuxiliaresFiltro filtro, CancellationToken ct = default)
        => _repository.ConsultarEconomicosAuxiliaresAsync(new ConsultaEconomicosAuxiliaresFiltro
        {
            IdRecaudacion = filtro.IdRecaudacion,
            IdEconomico = filtro.IdEconomico,
            NumEconomico = filtro.NumEconomico,
            IdPermisionario = filtro.IdPermisionario,
            IdCuenta = filtro.IdCuenta,
            NumCuenta = Texto(filtro.NumCuenta),
            IdCaja = filtro.IdCaja,
            StatusPago = filtro.StatusPago
        }, ct);

    public Task<List<EstadoDto>> ConsultarEstadosAsync(int? idEstado, string? estado, bool identico, CancellationToken ct = default)
        => _repository.ConsultarEstadosAsync(Numero(idEstado), Texto(estado), Bandera(identico), ct);

    public Task<List<FeRegimenFiscalDto>> ConsultarFeRegimenesFiscalesAsync(int? idRegimen, string? feClave, string? feRegimenFiscal, string? rfc, bool? feFisica, bool? feMoral, bool identico, CancellationToken ct = default)
        => _repository.ConsultarFeRegimenesFiscalesAsync(Numero(idRegimen), Texto(feClave), Texto(feRegimenFiscal), Texto(rfc), Bandera(feFisica), Bandera(feMoral), Bandera(identico), ct);

    public Task<List<FeRelacionFacturaDto>> ConsultarFeRelacionesFacturaAsync(int? idRelacion, string? feClave, bool identico, CancellationToken ct = default)
        => _repository.ConsultarFeRelacionesFacturaAsync(Numero(idRelacion), Texto(feClave), Bandera(identico), ct);

    public Task<List<FeUsoCfdiDto>> ConsultarFeUsosCfdiAsync(int? idUsoCfdi, string? feClave, string? feUsoCfdi, string? rfc, bool? feFisica, bool? feMoral, bool identico, CancellationToken ct = default)
        => _repository.ConsultarFeUsosCfdiAsync(Numero(idUsoCfdi), Texto(feClave), Texto(feUsoCfdi), Texto(rfc), Bandera(feFisica), Bandera(feMoral), Bandera(identico), ct);

    public Task<List<GalleroDto>> ConsultarGallerosAsync(int? idGallero, int? idStatus, string? gallero, CancellationToken ct = default)
        => _repository.ConsultarGallerosAsync(Numero(idGallero), Numero(idStatus), Texto(gallero), ct);

    public Task<List<GrupoClienteDto>> ConsultarGruposClienteAsync(int? idGrupoCliente, int? idStatus, CancellationToken ct = default)
        => _repository.ConsultarGruposClienteAsync(Numero(idGrupoCliente), Numero(idStatus), ct);

    public Task<List<PuestoDto>> ConsultarPuestosAsync(int? idStatus, CancellationToken ct = default)
        => _repository.ConsultarPuestosAsync(Numero(idStatus), ct);

    public Task<List<TipoNotaCreditoDto>> ConsultarTiposNotaCreditoAsync(int? idStatus, CancellationToken ct = default)
        => _repository.ConsultarTiposNotaCreditoAsync(Numero(idStatus), ct);

    /*
      TIPOS DE PAGO: @IDStatus Y @EsPago TIENEN DEFAULT 1 EN EL SP, NO 0.

      sp_n_ConsultaTiposPagos se declara con @IDStatus = 1 y @EsPago = 1, y por eso la lista
      que ve la gente en la cobranza son solo los activos que son pago de verdad. Si aqui se
      tradujera "no filtrar" a 0 se colarian los inactivos y los tipos que no son pago, y
      el desplegable de la pantalla de pagos dejaria de parecerse al de Mac31. Asi que el
      ausente conserva el default del SP: 1.
    */
    public Task<List<TipoPagoDto>> ConsultarTiposPagoAsync(int? idTipoPago, int? idStatus, string? tipoPago, bool identico, bool? esPago, CancellationToken ct = default)
        => _repository.ConsultarTiposPagoAsync(Numero(idTipoPago), idStatus ?? 1, Texto(tipoPago), Bandera(identico), esPago == false ? 0 : 1, ct);

    public Task<List<TipoSueldoDto>> ConsultarTiposSueldoAsync(int? idStatus, CancellationToken ct = default)
        => _repository.ConsultarTiposSueldoAsync(Numero(idStatus), ct);

    /// <summary>
    /// @MostrarPantalla tambien tiene default 1 en el SP: la lista normal es la de los tipos
    /// que se ofrecen en pantalla. Pedir explicitamente mostrarPantalla=false trae todos.
    /// </summary>
    public Task<List<TipoDocumentoDto>> ConsultarTiposDocumentoAsync(int? idTipoDocumento, bool? mostrarPantalla, bool sinDevCliente, bool sinCambioVigencia, CancellationToken ct = default)
        => _repository.ConsultarTiposDocumentoAsync(Numero(idTipoDocumento), mostrarPantalla == false ? 0 : 1, Bandera(sinDevCliente), Bandera(sinCambioVigencia), ct);

    public Task<List<TipoRecordatorioDto>> ConsultarTiposRecordatorioAsync(int? idTipoRecordatorio, int? idStatus, string? tipoRecordatorio, bool? esUrgente, bool? esImportante, bool identico, CancellationToken ct = default)
        => _repository.ConsultarTiposRecordatorioAsync(Numero(idTipoRecordatorio), Numero(idStatus), Texto(tipoRecordatorio), Bandera(esUrgente), Bandera(esImportante), Bandera(identico), ct);

    public Task<List<UnidadMedidaDto>> ConsultarUnidadesMedidaAsync(int? idUnidadMedida, int? idStatus, string? unidadMedida, bool? esPieza, bool? esCaja, bool? esServicio, bool identico, CancellationToken ct = default)
        => _repository.ConsultarUnidadesMedidaAsync(Numero(idUnidadMedida), Numero(idStatus), Texto(unidadMedida), Bandera(esPieza), Bandera(esCaja), Bandera(esServicio), Bandera(identico), ct);

    public Task<List<UnidadTerritorialDto>> ConsultarUnidadesTerritorialesAsync(int? idUnidadTerritorial, int? idStatus, string? clave, CancellationToken ct = default)
        => _repository.ConsultarUnidadesTerritorialesAsync(Numero(idUnidadTerritorial), Numero(idStatus), Texto(clave), ct);

    public Task<List<CategoriaMarcaDto>> ConsultarCategoriasMarcasAsync(int idCategoria, int? idMarca, bool soloMarcadas, CancellationToken ct = default)
        => _repository.ConsultarCategoriasMarcasAsync(idCategoria, Numero(idMarca), Bandera(soloMarcadas), ct);

    public Task<List<GrupoCategoriaMarcaDto>> ConsultarGrupoCategoriasMarcasAsync(int idCategoria, int? idMarca, CancellationToken ct = default)
        => _repository.ConsultarGrupoCategoriasMarcasAsync(idCategoria, Numero(idMarca), ct);

    /// <summary>
    /// @AplicaContrasena tiene default 1 en el SP: la lista normal son las formas que piden
    /// contraseña. Pedir aplicaContrasena=false trae todas las formas activas.
    /// </summary>
    public Task<List<FormaDto>> ConsultarFormasAsync(int? idForma, bool? aplicaContrasena, CancellationToken ct = default)
        => _repository.ConsultarFormasAsync(Numero(idForma), aplicaContrasena == false ? 0 : 1, ct);
}
