using System.Data;
using ISL_Service.Application.DTOs.CatalogosGenerales;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/*
  POR QUE SE LLAMA AL MISMO sp_n_Consulta* DE LEGACY Y NO SE ESCRIBE EL SELECT A MANO.

  Porque mientras Mac31 y el backend nuevo convivan, los dos tienen que contestar el MISMO
  dato, y varios de estos SP esconden reglas que no se ven en el nombre de la tabla:
  fn_RegresaTablaStatus( 0 ) significa "todos los status", el WHERE de regimenes fiscales
  esta clavado a IDStatus = 1 aunque reciba @IDStatus, sp_n_ConsultaGrupoCategoriasMarcasRelacion
  se va a la base de Zaragoza cuando Constantes.Funcionalidad = 'ZARA'. Reescribir esos
  SELECT seria reinventar las reglas y descubrirlas rotas en produccion.

  Los sp_n_ se LEEN, nunca se editan ni se reemplazan por un sp_w_ equivalente: son los que
  usa Mac31 y cualquier cambio ahi se lo lleva el front viejo.

  POR QUE SE LEE POR NOMBRE DE COLUMNA Y NO POR POSICION.

  Cuatro de estos SP terminan en `SELECT *` sobre la tabla del catalogo (puestos, tipos de
  nota de credito, tipos de documento, parte de cajas). El numero y el orden de las columnas
  cambia entre bases: en Produccion_svr `[Catalogo Puestos]` trae EsAgente y en bases mas
  viejas no (el propio SP lo documenta con el ALTER TABLE comentado arriba). Leer por
  posicion daria el campo equivocado o una excepcion; leer por nombre, con default cuando la
  columna falta, degrada sin romperse.
*/

public class CatalogosGeneralesRepository : ICatalogosGeneralesRepository
{
    private readonly IConfiguration _configuration;

    private const string SpAgentesDisponibles = "dbo.sp_n_ConsultaAgentesDisponibles";
    private const string SpCajas = "dbo.sp_n_ConsultaCajas";
    private const string SpCentrosServicio = "dbo.sp_n_ConsultaCentrosServicio";
    private const string SpCiudades = "dbo.sp_n_ConsultaCiudades";
    private const string SpColonias = "dbo.sp_n_ConsultaColonias";
    private const string SpConceptos = "dbo.sp_n_ConsultaConceptos";
    private const string SpConceptoSiguienteNumCuenta = "dbo.sp_n_ConsultaConceptoNextNumCuenta";
    private const string SpDocumentos = "dbo.sp_n_ConsultaDocumentos";
    private const string SpEconomicosAuxiliares = "dbo.sp_n_ConsultaEconomicosAuxiliares";
    private const string SpEstados = "dbo.sp_n_ConsultaEstados";
    private const string SpFeRegimenesFiscales = "dbo.sp_n_ConsultaCatFERegimenesFiscales";
    private const string SpFeRelacionFacturas = "dbo.sp_n_ConsultaCatFERelacionFacturas";
    private const string SpFeUsosCfdi = "dbo.sp_n_ConsultaCatFEUsosCFDI";
    private const string SpGalleros = "dbo.sp_n_ConsultaGalleros";
    private const string SpGruposCliente = "dbo.sp_n_ConsultaCatGruposCliente";
    private const string SpMunicipios = "dbo.sp_n_ConsultaMunicipios";
    private const string SpPuestos = "dbo.sp_n_ConsultaPuestos";
    private const string SpTiposNotaCredito = "dbo.sp_n_ConsultaTipoNotasCredito";
    private const string SpTiposPago = "dbo.sp_n_ConsultaTiposPagos";
    private const string SpTiposSueldo = "dbo.sp_n_ConsultaTipoSueldos";
    private const string SpTiposDocumento = "dbo.sp_n_ConsultaCatTiposDocumentos";
    private const string SpTiposRecordatorio = "dbo.sp_n_ConsultaTiposRecordatorios";
    private const string SpUnidadesMedida = "dbo.sp_n_ConsultaUnidadesServicio";
    private const string SpUnidadesTerritoriales = "dbo.sp_n_ConsultaCatUnidadesTerritoriales";
    private const string SpCategoriasMarcas = "dbo.sp_n_ConsultaCategoriasMarcas";
    private const string SpGrupoCategoriasMarcas = "dbo.sp_n_ConsultaGrupoCategoriasMarcasRelacion";
    private const string SpFormas = "dbo.sp_n_ConsultaFormas";

    public CatalogosGeneralesRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private SqlConnection GetConnection()
    {
        var cs = _configuration.GetConnectionString("Main")
            ?? _configuration.GetConnectionString("Mac3")
            ?? _configuration.GetConnectionString("Local")
            ?? _configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("ConnectionString (Main/Mac3/Local/Default) no encontrada.");
        var connector = new Mac3SqlServerConnector(cs);
        return connector.GetConnection;
    }

    /// <summary>
    /// Corre un sp_n_Consulta* y arma la lista. El lector recibe un <see cref="Renglon"/>,
    /// que resuelve las columnas por nombre y tolera que falten.
    /// </summary>
    private async Task<List<T>> ConsultarAsync<T>(
        string storedProcedure,
        Action<SqlParameterCollection> parametros,
        Func<Renglon, T> leer,
        CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(storedProcedure, conn) { CommandType = CommandType.StoredProcedure };
        parametros(cmd.Parameters);

        var lista = new List<T>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        // Varios de estos SP llenan tablas temporales con EXEC(@Sql) antes del SELECT final
        // (categorias-marcas, grupo-categorias-marcas). Esos pasos pueden entregar resultados
        // sin columnas; el catalogo es el primer resultado que SI trae columnas.
        while (reader.FieldCount == 0)
        {
            if (!await reader.NextResultAsync(ct))
                return lista;
        }

        var indices = Renglon.Indices(reader);
        while (await reader.ReadAsync(ct))
            lista.Add(leer(new Renglon(reader, indices)));

        return lista;
    }

    /// <summary>
    /// Un renglon del lector con acceso por nombre de columna. Devuelve el valor neutro
    /// cuando la columna no viene o es NULL, para que un `SELECT *` con una columna de menos
    /// no tire el endpoint.
    /// </summary>
    private readonly struct Renglon
    {
        private readonly SqlDataReader _reader;
        private readonly Dictionary<string, int> _indices;

        public Renglon(SqlDataReader reader, Dictionary<string, int> indices)
        {
            _reader = reader;
            _indices = indices;
        }

        public static Dictionary<string, int> Indices(SqlDataReader reader)
        {
            var mapa = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
                mapa[reader.GetName(i)] = i;
            return mapa;
        }

        private bool Tiene(string columna, out int indice)
            => _indices.TryGetValue(columna, out indice) && !_reader.IsDBNull(indice);

        /// <summary>Texto recortado: las columnas char(n) traen el relleno de la base.</summary>
        public string Texto(string columna)
            => Tiene(columna, out var i) ? Convert.ToString(_reader.GetValue(i))?.Trim() ?? string.Empty : string.Empty;

        public int Entero(string columna)
            => Tiene(columna, out var i) ? Convert.ToInt32(_reader.GetValue(i)) : 0;

        public decimal Monto(string columna)
            => Tiene(columna, out var i) ? Convert.ToDecimal(_reader.GetValue(i)) : 0m;
    }

    public Task<List<AgenteDisponibleDto>> ConsultarAgentesDisponiblesAsync(int numeroAgente, CancellationToken ct = default)
        => ConsultarAsync(SpAgentesDisponibles,
            p => p.AddWithValue("@NumeroAgente", numeroAgente),
            r => new AgenteDisponibleDto { NumeroAgente = r.Entero("NumeroAgente") },
            ct);

    public Task<List<CajaDto>> ConsultarCajasAsync(int idCaja, int idStatus, int numCaja, string nombreCaja, string equipoCaja, CancellationToken ct = default)
        => ConsultarAsync(SpCajas,
            p =>
            {
                p.AddWithValue("@IDCaja", idCaja);
                p.AddWithValue("@IDStatus", idStatus);
                p.AddWithValue("@NumCaja", numCaja);
                p.AddWithValue("@NombreCaja", nombreCaja);
                p.AddWithValue("@EquipoCaja", equipoCaja);
            },
            r => new CajaDto
            {
                IdCaja = r.Entero("IDCaja"),
                IdStatus = r.Entero("IDStatus"),
                NumCaja = r.Entero("NumCaja"),
                NombreCaja = r.Texto("NombreCaja"),
                EquipoCaja = r.Texto("EquipoCaja"),
                MaestraCaja = r.Entero("MaestraCaja"),
                IdColonia = r.Entero("IDColonia"),
                Direccion = r.Texto("Direccion")
            },
            ct);

    public Task<List<CentroServicioDto>> ConsultarCentrosServicioAsync(int idCentro, int idStatus, string nombreCentro, int identico, CancellationToken ct = default)
        => ConsultarAsync(SpCentrosServicio,
            p =>
            {
                p.AddWithValue("@IDCentro", idCentro);
                p.AddWithValue("@IDStatus", idStatus);
                p.AddWithValue("@NombreCentro", nombreCentro);
                p.AddWithValue("@Identico", identico);
            },
            r => new CentroServicioDto
            {
                IdCentro = r.Entero("IDCentro"),
                IdStatus = r.Entero("IDStatus"),
                BaseCentro = r.Texto("BaseCentro"),
                NombreCentro = r.Texto("NombreCentro"),
                Status = r.Texto("Status")
            },
            ct);

    public Task<List<CiudadDto>> ConsultarCiudadesAsync(string idEstado, string idCiudad, string ciudad, int identico, CancellationToken ct = default)
        => ConsultarAsync(SpCiudades,
            p =>
            {
                p.AddWithValue("@IDEstado", idEstado);
                p.AddWithValue("@IDCiudad", idCiudad);
                p.AddWithValue("@Ciudad", ciudad);
                p.AddWithValue("@Identico", identico);
            },
            r => new CiudadDto
            {
                IdEstado = r.Texto("IDEstado"),
                IdCiudad = r.Texto("IDCiudad"),
                Ciudad = r.Texto("Ciudad")
            },
            ct);

    public Task<List<ColoniaDto>> ConsultarColoniasAsync(ConsultaColoniasFiltro filtro, CancellationToken ct = default)
        => ConsultarAsync(SpColonias,
            p =>
            {
                p.AddWithValue("@IDColonia", filtro.IdColonia);
                p.AddWithValue("@IDEstado", filtro.IdEstado);
                p.AddWithValue("@IDCiudad", filtro.IdCiudad);
                p.AddWithValue("@IDMunicipio", filtro.IdMunicipio);
                p.AddWithValue("@IDAsentamiento", filtro.IdAsentamiento);
                p.AddWithValue("@Colonia", filtro.Colonia);
                p.AddWithValue("@CodigoPostal", filtro.CodigoPostal);
                p.AddWithValue("@Estado", filtro.Estado);
                p.AddWithValue("@Ciudad", filtro.Ciudad);
                p.AddWithValue("@Municipio", filtro.Municipio);
                p.AddWithValue("@Identico", filtro.Identico);
            },
            r => new ColoniaDto
            {
                IdColonia = r.Entero("IDColonia"),
                IdEstado = r.Texto("IDEstado"),
                IdCiudad = r.Texto("IDCiudad"),
                IdMunicipio = r.Texto("IDMunicipio"),
                IdAsentamiento = r.Texto("IDAsentamiento"),
                Colonia = r.Texto("Colonia"),
                CodigoPostal = r.Texto("CodigoPostal"),
                DelegacionMunicipio = r.Texto("DelegacionMunicipio"),
                Ciudad = r.Texto("Ciudad"),
                Estado = r.Texto("Estado"),
                Asentamiento = r.Texto("Asentamiento")
            },
            ct);

    public Task<List<ConceptoDto>> ConsultarConceptosAsync(int idConcepto, int idStatus, string numConcepto, string nombreConcepto, CancellationToken ct = default)
        => ConsultarAsync(SpConceptos,
            p =>
            {
                p.AddWithValue("@IDConcepto", idConcepto);
                p.AddWithValue("@IDStatus", idStatus);
                p.AddWithValue("@NumConcepto", numConcepto);
                p.AddWithValue("@NombreConcepto", nombreConcepto);
            },
            LeerConcepto,
            ct);

    public Task<List<ConceptoDto>> ConsultarSiguienteNumeroCuentaAsync(int idConcepto, CancellationToken ct = default)
        => ConsultarAsync(SpConceptoSiguienteNumCuenta,
            p => p.AddWithValue("@IDConcepto", idConcepto),
            LeerConcepto,
            ct);

    private static ConceptoDto LeerConcepto(Renglon r) => new()
    {
        IdConcepto = r.Entero("IDConcepto"),
        IdStatus = r.Entero("IDStatus"),
        NumConcepto = r.Texto("NumConcepto"),
        NombreConcepto = r.Texto("NombreConcepto"),
        Estatus = r.Texto("Estatus")
    };

    public Task<List<DocumentoDto>> ConsultarDocumentosAsync(int idDocumento, int idStatus, string documento, int identico, CancellationToken ct = default)
        => ConsultarAsync(SpDocumentos,
            p =>
            {
                p.AddWithValue("@IDDocumento", idDocumento);
                p.AddWithValue("@IDStatus", idStatus);
                p.AddWithValue("@Documento", documento);
                p.AddWithValue("@Identico", identico);
            },
            r => new DocumentoDto
            {
                IdDocumento = r.Entero("IDDocumento"),
                IdStatus = r.Entero("IDStatus"),
                Documento = r.Texto("Documento"),
                Descripcion = r.Texto("Descripcion"),
                Obligatorio = r.Entero("Obligatorio"),
                Status = r.Texto("Status")
            },
            ct);

    public Task<List<EconomicoAuxiliarDto>> ConsultarEconomicosAuxiliaresAsync(ConsultaEconomicosAuxiliaresFiltro filtro, CancellationToken ct = default)
        => ConsultarAsync(SpEconomicosAuxiliares,
            p =>
            {
                p.AddWithValue("@IDRecaudacion", filtro.IdRecaudacion);
                p.AddWithValue("@IDEconomico", filtro.IdEconomico);
                p.AddWithValue("@NumEconomico", filtro.NumEconomico);
                p.AddWithValue("@IDPermisionario", filtro.IdPermisionario);
                p.AddWithValue("@IDCuenta", filtro.IdCuenta);
                p.AddWithValue("@NumCuenta", filtro.NumCuenta);
                p.AddWithValue("@IDCaja", filtro.IdCaja);
                p.AddWithValue("@StatusPago", filtro.StatusPago);
            },
            r => new EconomicoAuxiliarDto
            {
                IdEconomicoAuxiliar = r.Entero("IDEconomicoAuxiliar"),
                IdRecaudacion = r.Entero("IDRecaudacion"),
                IdStatus = r.Entero("IDStatus"),
                Estatus = r.Texto("Estatus"),
                Pagado = r.Entero("Pagado"),
                Folio = r.Entero("Folio"),
                IdPermisionario = r.Entero("IDPermisionario"),
                NumPermisionario = r.Texto("NumPermisionario"),
                NombrePermisionario = r.Texto("NombrePermisionario"),
                IdEconomico = r.Entero("IDEconomico"),
                NumEconomico = r.Entero("NumEconomico"),
                NombreEconomico = r.Texto("NombreEconomico"),
                IdCuenta = r.Entero("IDCuenta"),
                NumCuenta = r.Texto("NumCuenta"),
                NombreCuenta = r.Texto("NombreCuenta"),
                IdConcepto = r.Entero("IDConcepto"),
                NumConcepto = r.Texto("NumConcepto"),
                NombreConcepto = r.Texto("NombreConcepto"),
                ImporteCuenta = r.Monto("ImporteCuenta"),
                DebeHaber = r.Texto("DebeHaber"),
                IdCaja = r.Entero("IDCaja"),
                NumCaja = r.Entero("NumCaja")
            },
            ct);

    public Task<List<EstadoDto>> ConsultarEstadosAsync(int idEstado, string estado, int identico, CancellationToken ct = default)
        => ConsultarAsync(SpEstados,
            p =>
            {
                p.AddWithValue("@IDEstado", idEstado);
                p.AddWithValue("@Estado", estado);
                p.AddWithValue("@Identico", identico);
            },
            r => new EstadoDto
            {
                IdEstado = r.Texto("IDEstado"),
                Estado = r.Texto("Estado")
            },
            ct);

    public Task<List<FeRegimenFiscalDto>> ConsultarFeRegimenesFiscalesAsync(int idRegimen, string feClave, string feRegimenFiscal, string rfc, int feFisica, int feMoral, int identico, CancellationToken ct = default)
        => ConsultarAsync(SpFeRegimenesFiscales,
            p =>
            {
                p.AddWithValue("@IDFERegimenFiscal", idRegimen);
                p.AddWithValue("@IDStatus", 0);
                p.AddWithValue("@FEClave", feClave);
                p.AddWithValue("@FERegimenFiscal", feRegimenFiscal);
                p.AddWithValue("@RFC", rfc);
                p.AddWithValue("@FEFisica", feFisica);
                p.AddWithValue("@FEMoral", feMoral);
                p.AddWithValue("@Identico", identico);
            },
            r => new FeRegimenFiscalDto
            {
                IdFeRegimenFiscal = r.Entero("IDFERegimenFiscal"),
                IdStatus = r.Entero("IDStatus"),
                FeClave = r.Texto("FEClave"),
                FeRegimenFiscal = r.Texto("FERegimenFiscal"),
                FeFisica = r.Entero("FEFisica"),
                FeMoral = r.Entero("FEMoral"),
                Status = r.Texto("Status")
            },
            ct);

    public Task<List<FeRelacionFacturaDto>> ConsultarFeRelacionesFacturaAsync(int idRelacion, string feClave, int identico, CancellationToken ct = default)
        => ConsultarAsync(SpFeRelacionFacturas,
            p =>
            {
                p.AddWithValue("@IDFERelacionFactura", idRelacion);
                p.AddWithValue("@IDStatus", 1);
                p.AddWithValue("@FEClave", feClave);
                p.AddWithValue("@Identico", identico);
            },
            r => new FeRelacionFacturaDto
            {
                IdFeRelacionFactura = r.Entero("IDFERelacionFactura"),
                IdStatus = r.Entero("IDStatus"),
                FeClave = r.Texto("FEClave"),
                FeRelacionFactura = r.Texto("FERelacionFactura"),
                Status = r.Texto("Status")
            },
            ct);

    public Task<List<FeUsoCfdiDto>> ConsultarFeUsosCfdiAsync(int idUsoCfdi, string feClave, string feUsoCfdi, string rfc, int feFisica, int feMoral, int identico, CancellationToken ct = default)
        => ConsultarAsync(SpFeUsosCfdi,
            p =>
            {
                p.AddWithValue("@IDFEUsoCFDI", idUsoCfdi);
                p.AddWithValue("@IDStatus", 0);
                p.AddWithValue("@FEClave", feClave);
                p.AddWithValue("@FEUsoCFDI", feUsoCfdi);
                p.AddWithValue("@RFC", rfc);
                p.AddWithValue("@FEFisica", feFisica);
                p.AddWithValue("@FEMoral", feMoral);
                p.AddWithValue("@Identico", identico);
            },
            r => new FeUsoCfdiDto
            {
                IdFeUsoCfdi = r.Entero("IDFEUsoCFDI"),
                IdStatus = r.Entero("IDStatus"),
                FeClave = r.Texto("FEClave"),
                FeUsoCfdi = r.Texto("FEUsoCFDI"),
                FeFisica = r.Entero("FEFisica"),
                FeMoral = r.Entero("FEMoral"),
                Status = r.Texto("Status")
            },
            ct);

    public Task<List<GalleroDto>> ConsultarGallerosAsync(int idGallero, int idStatus, string gallero, CancellationToken ct = default)
        => ConsultarAsync(SpGalleros,
            p =>
            {
                p.AddWithValue("@IDGallero", idGallero);
                p.AddWithValue("@IDStatus", idStatus);
                p.AddWithValue("@Gallero", gallero);
            },
            r => new GalleroDto
            {
                IdGallero = r.Entero("IDGallero"),
                IdStatus = r.Entero("IDStatus"),
                Numero = r.Entero("Numero"),
                Gallero = r.Texto("Gallero"),
                Saldo = r.Monto("Saldo"),
                Status = r.Texto("Status")
            },
            ct);

    public Task<List<GrupoClienteDto>> ConsultarGruposClienteAsync(int idGrupoCliente, int idStatus, CancellationToken ct = default)
        => ConsultarAsync(SpGruposCliente,
            p =>
            {
                p.AddWithValue("@IDGrupoCliente", idGrupoCliente);
                p.AddWithValue("@IDStatus", idStatus);
                p.AddWithValue("@GrupoCliente", string.Empty);
                p.AddWithValue("@Identico", 0);
            },
            r => new GrupoClienteDto
            {
                IdGrupoCliente = r.Entero("IDGrupoCliente"),
                IdStatus = r.Entero("IDStatus"),
                GrupoCliente = r.Texto("GrupoCliente"),
                Status = r.Texto("Status")
            },
            ct);

    public Task<List<MunicipioDto>> ConsultarMunicipiosAsync(string idEstado, string idMunicipio, string municipio, int identico, CancellationToken ct = default)
        => ConsultarAsync(SpMunicipios,
            p =>
            {
                p.AddWithValue("@IDEstado", idEstado);
                p.AddWithValue("@IDMunicipio", idMunicipio);
                p.AddWithValue("@Municipio", municipio);
                p.AddWithValue("@Identico", identico);
            },
            r => new MunicipioDto
            {
                IdEstado = r.Texto("IDEstado"),
                IdMunicipio = r.Texto("IDMunicipio"),
                Municipio = r.Texto("Municipio")
            },
            ct);

    public Task<List<PuestoDto>> ConsultarPuestosAsync(int idStatus, CancellationToken ct = default)
        => ConsultarAsync(SpPuestos,
            p => p.AddWithValue("@IDStatus", idStatus),
            r => new PuestoDto
            {
                IdPuesto = r.Entero("IDPuesto"),
                IdStatus = r.Entero("IDStatus"),
                Puesto = r.Texto("Puesto"),
                EsAgente = r.Entero("EsAgente")
            },
            ct);

    public Task<List<TipoNotaCreditoDto>> ConsultarTiposNotaCreditoAsync(int idStatus, CancellationToken ct = default)
        => ConsultarAsync(SpTiposNotaCredito,
            p => p.AddWithValue("@IDStatus", idStatus),
            r => new TipoNotaCreditoDto
            {
                IdTipoNotaCredito = r.Entero("IDTipoNotaCredito"),
                IdStatus = r.Entero("IDStatus"),
                TipoNotaCredito = r.Texto("TipoNotaCredito")
            },
            ct);

    public Task<List<TipoPagoDto>> ConsultarTiposPagoAsync(int idTipoPago, int idStatus, string tipoPago, int identico, int esPago, CancellationToken ct = default)
        => ConsultarAsync(SpTiposPago,
            p =>
            {
                p.AddWithValue("@IDTipoPago", idTipoPago);
                p.AddWithValue("@IDStatus", idStatus);
                p.AddWithValue("@TipoPago", tipoPago);
                p.AddWithValue("@Identico", identico);
                p.AddWithValue("@EsPago", esPago);
            },
            r => new TipoPagoDto
            {
                IdTipoPago = r.Entero("IDTipoPago"),
                IdStatus = r.Entero("IDStatus"),
                TipoPago = r.Texto("TipoPago"),
                TipoPagoCorto = r.Texto("TipoPagoCorto"),
                EsDescuento = r.Entero("EsDescuento"),
                Status = r.Texto("Status")
            },
            ct);

    public Task<List<TipoSueldoDto>> ConsultarTiposSueldoAsync(int idStatus, CancellationToken ct = default)
        => ConsultarAsync(SpTiposSueldo,
            p => p.AddWithValue("@IDStatus", idStatus),
            r => new TipoSueldoDto
            {
                IdTipoSueldo = r.Entero("IDTipoSueldo"),
                IdStatus = r.Entero("IDStatus"),
                TipoSueldo = r.Texto("TipoSueldo"),
                EsComision = r.Entero("EsComision")
            },
            ct);

    public Task<List<TipoDocumentoDto>> ConsultarTiposDocumentoAsync(int idTipoDocumento, int mostrarPantalla, int sinDevCliente, int sinCambioVigencia, CancellationToken ct = default)
        => ConsultarAsync(SpTiposDocumento,
            p =>
            {
                p.AddWithValue("@IDTipoDocumento", idTipoDocumento);
                p.AddWithValue("@MostrarPantalla", mostrarPantalla);
                p.AddWithValue("@SinDevCliente", sinDevCliente);
                p.AddWithValue("@SinCambioVigencia", sinCambioVigencia);
            },
            r => new TipoDocumentoDto
            {
                IdTipoDocumento = r.Entero("IDTipoDocumento"),
                Documento = r.Texto("Documento"),
                NombreDocumento = r.Texto("NombreDocumento"),
                NombreDocumentoCorto = r.Texto("NombreDocumentoCorto"),
                MostrarPantalla = r.Entero("MostrarPantalla")
            },
            ct);

    public Task<List<TipoRecordatorioDto>> ConsultarTiposRecordatorioAsync(int idTipoRecordatorio, int idStatus, string tipoRecordatorio, int esUrgente, int esImportante, int identico, CancellationToken ct = default)
        => ConsultarAsync(SpTiposRecordatorio,
            p =>
            {
                p.AddWithValue("@IDTipoRecordatorio", idTipoRecordatorio);
                p.AddWithValue("@IDStatus", idStatus);
                p.AddWithValue("@TipoRecordatorio", tipoRecordatorio);
                p.AddWithValue("@EsUrgente", esUrgente);
                p.AddWithValue("@EsImportante", esImportante);
                p.AddWithValue("@Identico", identico);
            },
            r => new TipoRecordatorioDto
            {
                IdTipoRecordatorio = r.Entero("IDTipoRecordatorio"),
                IdStatus = r.Entero("IDStatus"),
                TipoRecordatorio = r.Texto("TipoRecordatorio"),
                EsUrgente = r.Entero("EsUrgente"),
                EsImportante = r.Entero("EsImportante"),
                Status = r.Texto("Status")
            },
            ct);

    public Task<List<UnidadMedidaDto>> ConsultarUnidadesMedidaAsync(int idUnidadMedida, int idStatus, string unidadMedida, int esPieza, int esCaja, int esServicio, int identico, CancellationToken ct = default)
        => ConsultarAsync(SpUnidadesMedida,
            p =>
            {
                p.AddWithValue("@IDUnidadMedida", idUnidadMedida);
                p.AddWithValue("@IDStatus", idStatus);
                p.AddWithValue("@UnidadMedida", unidadMedida);
                p.AddWithValue("@EsPieza", esPieza);
                p.AddWithValue("@EsCaja", esCaja);
                p.AddWithValue("@EsServicio", esServicio);
                p.AddWithValue("@Identico", identico);
            },
            r => new UnidadMedidaDto
            {
                IdUnidadMedida = r.Entero("IDUnidadMedida"),
                IdStatus = r.Entero("IDStatus"),
                UnidadMedida = r.Texto("UnidadMedida"),
                EsPieza = r.Entero("EsPieza"),
                EsCaja = r.Entero("EsCaja"),
                EsServicio = r.Entero("EsServicio"),
                Status = r.Texto("Status")
            },
            ct);

    public Task<List<UnidadTerritorialDto>> ConsultarUnidadesTerritorialesAsync(int idUnidadTerritorial, int idStatus, string clave, CancellationToken ct = default)
        => ConsultarAsync(SpUnidadesTerritoriales,
            p =>
            {
                p.AddWithValue("@IDUnidadTerritorial", idUnidadTerritorial);
                p.AddWithValue("@IDStatus", idStatus);
                p.AddWithValue("@Clave", clave);
            },
            r => new UnidadTerritorialDto
            {
                IdUnidadTerritorial = r.Entero("IDUnidadTerritorial"),
                IdStatus = r.Entero("IDStatus"),
                Clave = r.Texto("Clave"),
                Denominacion = r.Texto("Denominacion"),
                Status = r.Texto("Status")
            },
            ct);

    public Task<List<CategoriaMarcaDto>> ConsultarCategoriasMarcasAsync(int idCategoria, int idMarca, int marcado, CancellationToken ct = default)
        => ConsultarAsync(SpCategoriasMarcas,
            p =>
            {
                p.AddWithValue("@IDCategoria", idCategoria);
                p.AddWithValue("@IDMarca", idMarca);
                p.AddWithValue("@Marcado", marcado);
            },
            r => new CategoriaMarcaDto
            {
                IdCategoria = r.Entero("IDCategoria"),
                IdMarca = r.Entero("IDMarca"),
                Categoria = r.Texto("Categoria"),
                Marca = r.Texto("Marca"),
                Marcado = r.Entero("Marcado")
            },
            ct);

    public Task<List<GrupoCategoriaMarcaDto>> ConsultarGrupoCategoriasMarcasAsync(int idCategoria, int idMarca, CancellationToken ct = default)
        => ConsultarAsync(SpGrupoCategoriasMarcas,
            p =>
            {
                p.AddWithValue("@IDCategoria", idCategoria);
                p.AddWithValue("@IDMarca", idMarca);
            },
            r => new GrupoCategoriaMarcaDto
            {
                IdGrupoCategoria = r.Entero("IDGrupoCategoria"),
                IdCategoria = r.Entero("IDCategoria"),
                IdMarca = r.Entero("IDMarca"),
                GrupoCategoria = r.Texto("GrupoCategoria"),
                Categoria = r.Texto("Categoria"),
                Marca = r.Texto("Marca"),
                Marcado = r.Entero("Marcado")
            },
            ct);

    public Task<List<FormaDto>> ConsultarFormasAsync(int idForma, int aplicaContrasena, CancellationToken ct = default)
        => ConsultarAsync(SpFormas,
            p =>
            {
                p.AddWithValue("@IDForma", idForma);
                p.AddWithValue("@AplicaContrasena", aplicaContrasena);
            },
            r => new FormaDto
            {
                IdForma = r.Entero("IDForma"),
                FormaNombre = r.Texto("FormaNombre"),
                FormaDescripcion = r.Texto("FormaDescripcion"),
                FormaOrden = r.Entero("FormaOrden")
            },
            ct);
}
