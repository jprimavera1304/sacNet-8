using ISL_Service.Application.DTOs.CatalogosGenerales;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Services;
using ISL_Service.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Tests;

/*
  QUE SE PRUEBA AQUI Y QUE NO.

  Lo que se prueba es lo UNICO que el servicio decide, y son las tres cosas que ya mordieron:

  1. El apostrofo. Los sp_n_ de catalogo arman su WHERE concatenando texto y lo corren con
     EXEC(@Sql). Pasar el filtro como parametro de SqlCommand no protege de nada, porque el
     SP lo concatena adentro. Legacy quitaba el apostrofo en cuatro de sus servicios y se le
     olvidaba en el resto.

  2. El "todos" de ciudades y municipios es "0", no "". Con cadena vacia, el SP termina
     llamando a fn_RegresaTablaEstados( ) y SQL Server tira
     "An insufficient number of arguments were supplied". Es el motivo por el que
     /api/catciudades/consultar de Legacy revienta cuando el front no manda estado.

  3. Los defaults del SP que NO son 0. @IDStatus y @EsPago de tipos de pago, @MostrarPantalla
     de tipos de documento y @AplicaContrasena de formas se declaran en 1, y de eso depende
     que la lista se parezca a la de Mac31. Traducir "ausente" a 0 colaria inactivos.

  Lo que NO se prueba es el SQL: eso lo demuestra el comparador contra Legacy, contra la misma
  base, en migracion/comparador/casos/catalogo-*.json.
*/

public class CatalogosGeneralesServiceTests
{
    [Fact]
    public async Task Filtros_De_Texto_Pierden_El_Apostrofo_Y_Los_Espacios()
    {
        var repo = new FakeCatalogosGeneralesRepository();
        var service = new CatalogosGeneralesService(repo);

        await service.ConsultarCentrosServicioAsync(null, null, "  O'BRIEN  ", false);

        Assert.Equal("OBRIEN", repo.UltimoTexto);
    }

    [Theory]
    [InlineData("ventas'; DROP TABLE Clientes --", "ventas; DROP TABLE Clientes --")]
    [InlineData("'", "")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public async Task El_Apostrofo_No_Llega_Al_Procedimiento(string? entrada, string esperado)
    {
        var repo = new FakeCatalogosGeneralesRepository();
        var service = new CatalogosGeneralesService(repo);

        await service.ConsultarGallerosAsync(null, null, entrada);

        Assert.Equal(esperado, repo.UltimoTexto);
    }

    [Fact]
    public async Task Ciudades_Sin_Estado_Preguntan_Por_Cero_Y_No_Por_Vacio()
    {
        var repo = new FakeCatalogosGeneralesRepository();
        var service = new CatalogosGeneralesService(repo);

        await service.ConsultarCiudadesAsync(null, null, null, false);

        Assert.Equal("0", repo.UltimaClaveEstado);
    }

    [Fact]
    public async Task Municipios_Sin_Estado_Preguntan_Por_Cero_Y_No_Por_Vacio()
    {
        var repo = new FakeCatalogosGeneralesRepository();
        var service = new CatalogosGeneralesService(repo);

        await service.ConsultarMunicipiosAsync("  ", "  ", null, false);

        Assert.Equal("0", repo.UltimaClaveEstado);
        Assert.Equal("0", repo.UltimaClaveMunicipio);
    }

    [Fact]
    public async Task Ciudades_Con_Estado_Respetan_El_Estado_Pedido()
    {
        var repo = new FakeCatalogosGeneralesRepository();
        var service = new CatalogosGeneralesService(repo);

        await service.ConsultarCiudadesAsync(" 19 ", null, null, false);

        Assert.Equal("19", repo.UltimaClaveEstado);
    }

    [Fact]
    public async Task Tipos_De_Pago_Sin_Filtros_Conservan_Los_Defaults_Del_Procedimiento()
    {
        var repo = new FakeCatalogosGeneralesRepository();
        var service = new CatalogosGeneralesService(repo);

        await service.ConsultarTiposPagoAsync(null, null, null, false, null);

        Assert.Equal(1, repo.UltimoIdStatus);
        Assert.Equal(1, repo.UltimoEsPago);
    }

    [Fact]
    public async Task Tipos_De_Pago_Con_EsPago_Falso_Apagan_El_Filtro()
    {
        var repo = new FakeCatalogosGeneralesRepository();
        var service = new CatalogosGeneralesService(repo);

        await service.ConsultarTiposPagoAsync(null, 2, null, false, false);

        Assert.Equal(2, repo.UltimoIdStatus);
        Assert.Equal(0, repo.UltimoEsPago);
    }

    [Fact]
    public async Task Tipos_De_Documento_Sin_Filtro_Piden_Solo_Los_De_Pantalla()
    {
        var repo = new FakeCatalogosGeneralesRepository();
        var service = new CatalogosGeneralesService(repo);

        await service.ConsultarTiposDocumentoAsync(null, null, false, false);

        Assert.Equal(1, repo.UltimoMostrarPantalla);
    }

    [Fact]
    public async Task Formas_Sin_Filtro_Piden_Solo_Las_Que_Aplican_Contrasena()
    {
        var repo = new FakeCatalogosGeneralesRepository();
        var service = new CatalogosGeneralesService(repo);

        await service.ConsultarFormasAsync(null, null);

        Assert.Equal(1, repo.UltimoAplicaContrasena);
    }

    [Fact]
    public async Task Formas_Con_AplicaContrasena_Falso_Traen_Todas()
    {
        var repo = new FakeCatalogosGeneralesRepository();
        var service = new CatalogosGeneralesService(repo);

        await service.ConsultarFormasAsync(null, false);

        Assert.Equal(0, repo.UltimoAplicaContrasena);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public async Task Las_Banderas_Booleanas_Viajan_Como_Cero_O_Uno(bool? pedido, int esperado)
    {
        var repo = new FakeCatalogosGeneralesRepository();
        var service = new CatalogosGeneralesService(repo);

        await service.ConsultarUnidadesMedidaAsync(null, null, null, pedido, null, null, false);

        Assert.Equal(esperado, repo.UltimoEsPieza);
    }

    /*
      COLONIAS SIN FILTRO NO SE SIRVE.

      El catalogo de colonias de Produccion_svr tiene 68,101 renglones y el SP les resuelve
      estado, ciudad, municipio y asentamiento con cuatro INNER JOIN. Legacy dejaba pedirlo
      entero y la respuesta pasa de 14 MB. Esta prueba fija que el nuevo conteste 400 y que
      NO llegue a la base.
    */
    [Fact]
    public async Task Colonias_Sin_Ningun_Filtro_Se_Rechazan_Antes_De_La_Base()
    {
        var repo = new FakeCatalogosGeneralesRepository();
        var controller = new CatalogosGeneralesController(new CatalogosGeneralesService(repo));

        var resultado = await controller.ConsultarColoniasAsync(new ConsultaColoniasFiltro(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Equal(0, repo.LlamadasColonias);
    }

    [Fact]
    public async Task Colonias_Con_Codigo_Postal_Si_Llegan_A_La_Base()
    {
        var repo = new FakeCatalogosGeneralesRepository();
        var controller = new CatalogosGeneralesController(new CatalogosGeneralesService(repo));

        var resultado = await controller.ConsultarColoniasAsync(
            new ConsultaColoniasFiltro { CodigoPostal = "64000" }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(resultado);
        Assert.Equal(1, repo.LlamadasColonias);
    }

    [Fact]
    public async Task Formas_Procesos_Ignora_El_IdProceso_Que_Legacy_Mandaba_De_Mas()
    {
        var repo = new FakeCatalogosGeneralesRepository();
        var controller = new CatalogosGeneralesController(new CatalogosGeneralesService(repo));

        var resultado = await controller.ConsultarFormasProcesosAsync(idForma: 4060, idProceso: 777, aplicaContrasena: true, CancellationToken.None);

        Assert.IsType<OkObjectResult>(resultado);
        Assert.Equal(4060, repo.UltimoIdForma);
    }
}

/// <summary>
/// Doble a mano del repositorio: guarda lo que el servicio le manda. Lo que no se usa en
/// estas pruebas lanza NotImplementedException, como el resto de los dobles del proyecto.
/// </summary>
internal class FakeCatalogosGeneralesRepository : ICatalogosGeneralesRepository
{
    public string? UltimoTexto { get; private set; }
    public string? UltimaClaveEstado { get; private set; }
    public string? UltimaClaveMunicipio { get; private set; }
    public int UltimoIdStatus { get; private set; }
    public int UltimoEsPago { get; private set; }
    public int UltimoMostrarPantalla { get; private set; }
    public int UltimoAplicaContrasena { get; private set; }
    public int UltimoEsPieza { get; private set; }
    public int UltimoIdForma { get; private set; }
    public int LlamadasColonias { get; private set; }

    public Task<List<CentroServicioDto>> ConsultarCentrosServicioAsync(int idCentro, int idStatus, string nombreCentro, int identico, CancellationToken ct = default)
    {
        UltimoTexto = nombreCentro;
        return Task.FromResult(new List<CentroServicioDto>());
    }

    public Task<List<GalleroDto>> ConsultarGallerosAsync(int idGallero, int idStatus, string gallero, CancellationToken ct = default)
    {
        UltimoTexto = gallero;
        return Task.FromResult(new List<GalleroDto>());
    }

    public Task<List<CiudadDto>> ConsultarCiudadesAsync(string idEstado, string idCiudad, string ciudad, int identico, CancellationToken ct = default)
    {
        UltimaClaveEstado = idEstado;
        return Task.FromResult(new List<CiudadDto>());
    }

    public Task<List<MunicipioDto>> ConsultarMunicipiosAsync(string idEstado, string idMunicipio, string municipio, int identico, CancellationToken ct = default)
    {
        UltimaClaveEstado = idEstado;
        UltimaClaveMunicipio = idMunicipio;
        return Task.FromResult(new List<MunicipioDto>());
    }

    public Task<List<TipoPagoDto>> ConsultarTiposPagoAsync(int idTipoPago, int idStatus, string tipoPago, int identico, int esPago, CancellationToken ct = default)
    {
        UltimoIdStatus = idStatus;
        UltimoEsPago = esPago;
        return Task.FromResult(new List<TipoPagoDto>());
    }

    public Task<List<TipoDocumentoDto>> ConsultarTiposDocumentoAsync(int idTipoDocumento, int mostrarPantalla, int sinDevCliente, int sinCambioVigencia, CancellationToken ct = default)
    {
        UltimoMostrarPantalla = mostrarPantalla;
        return Task.FromResult(new List<TipoDocumentoDto>());
    }

    public Task<List<FormaDto>> ConsultarFormasAsync(int idForma, int aplicaContrasena, CancellationToken ct = default)
    {
        UltimoIdForma = idForma;
        UltimoAplicaContrasena = aplicaContrasena;
        return Task.FromResult(new List<FormaDto>());
    }

    public Task<List<UnidadMedidaDto>> ConsultarUnidadesMedidaAsync(int idUnidadMedida, int idStatus, string unidadMedida, int esPieza, int esCaja, int esServicio, int identico, CancellationToken ct = default)
    {
        UltimoEsPieza = esPieza;
        return Task.FromResult(new List<UnidadMedidaDto>());
    }

    public Task<List<ColoniaDto>> ConsultarColoniasAsync(ConsultaColoniasFiltro filtro, CancellationToken ct = default)
    {
        LlamadasColonias++;
        return Task.FromResult(new List<ColoniaDto>());
    }

    public Task<List<AgenteDisponibleDto>> ConsultarAgentesDisponiblesAsync(int numeroAgente, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<List<CajaDto>> ConsultarCajasAsync(int idCaja, int idStatus, int numCaja, string nombreCaja, string equipoCaja, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<List<ConceptoDto>> ConsultarConceptosAsync(int idConcepto, int idStatus, string numConcepto, string nombreConcepto, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<List<ConceptoDto>> ConsultarSiguienteNumeroCuentaAsync(int idConcepto, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<List<DocumentoDto>> ConsultarDocumentosAsync(int idDocumento, int idStatus, string documento, int identico, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<List<EconomicoAuxiliarDto>> ConsultarEconomicosAuxiliaresAsync(ConsultaEconomicosAuxiliaresFiltro filtro, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<List<EstadoDto>> ConsultarEstadosAsync(int idEstado, string estado, int identico, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<List<FeRegimenFiscalDto>> ConsultarFeRegimenesFiscalesAsync(int idRegimen, string feClave, string feRegimenFiscal, string rfc, int feFisica, int feMoral, int identico, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<List<FeRelacionFacturaDto>> ConsultarFeRelacionesFacturaAsync(int idRelacion, string feClave, int identico, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<List<FeUsoCfdiDto>> ConsultarFeUsosCfdiAsync(int idUsoCfdi, string feClave, string feUsoCfdi, string rfc, int feFisica, int feMoral, int identico, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<List<GrupoClienteDto>> ConsultarGruposClienteAsync(int idGrupoCliente, int idStatus, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<List<PuestoDto>> ConsultarPuestosAsync(int idStatus, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<List<TipoNotaCreditoDto>> ConsultarTiposNotaCreditoAsync(int idStatus, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<List<TipoSueldoDto>> ConsultarTiposSueldoAsync(int idStatus, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<List<TipoRecordatorioDto>> ConsultarTiposRecordatorioAsync(int idTipoRecordatorio, int idStatus, string tipoRecordatorio, int esUrgente, int esImportante, int identico, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<List<UnidadTerritorialDto>> ConsultarUnidadesTerritorialesAsync(int idUnidadTerritorial, int idStatus, string clave, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<List<CategoriaMarcaDto>> ConsultarCategoriasMarcasAsync(int idCategoria, int idMarca, int marcado, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<List<GrupoCategoriaMarcaDto>> ConsultarGrupoCategoriasMarcasAsync(int idCategoria, int idMarca, CancellationToken ct = default)
        => throw new NotImplementedException();
}
