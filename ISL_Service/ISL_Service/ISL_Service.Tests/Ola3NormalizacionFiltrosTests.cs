using ISL_Service.Application.DTOs.CatalogosComplementos;
using ISL_Service.Application.DTOs.Huellas;
using ISL_Service.Application.DTOs.Obra;
using ISL_Service.Application.DTOs.Productos;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Services;

namespace ISL_Service.Tests;

/// <summary>
/// Lo que los services de la ola 3 (las lecturas que no son catalogos ni ventas)
/// le hacen a los filtros ANTES de que lleguen al stored procedure.
///
/// POR QUE ESTO MERECE PRUEBAS Y EL RESTO DEL REPOSITORIO NO
/// --------------------------------------------------------
/// Cada una de estas transformaciones existe porque sin ella el procedimiento de
/// legacy hace algo peor que fallar: devuelve datos equivocados sin avisar.
/// * El apostrofo: los procedimientos concatenan el filtro y lo corren con
///   EXEC(@Sql); el SqlParameter no protege adentro.
/// * El cero en la lista de marcas: `IDMarca IN (0)` devuelve CERO renglones, no
///   todos. Un filtro que vacia el catalogo en silencio.
/// * El punto decimal de los descuentos: con la cultura del servidor en espanol,
///   12.5 se escribiria "12,5" y el procedimiento leeria DOS descuentos.
/// * El -1 de las huellas: si viajara por HTTP, un id negativo por error de un
///   front se volveria "dame todas las huellas de la empresa".
/// Son reglas puras, se prueban sin base de datos, y son exactamente las que se
/// romperian en un refactor descuidado.
/// </summary>
public class Ola3NormalizacionFiltrosTests
{
    // ------------------------------------------------------------------ obra

    [Fact]
    public async Task Obra_Quita_El_Apostrofo_Del_Filtro_De_Texto()
    {
        var repo = new FakeObraRepository();
        var service = new ObraService(repo);

        await service.ConsultarGerentesAsync(
            new ObraCatalogoFiltro { Texto = "O'BRIEN" }, 0, CancellationToken.None);

        Assert.Equal("OBRIEN", repo.UltimoFiltro!.Texto);
    }

    [Fact]
    public async Task Obra_El_Texto_Ausente_Viaja_Como_Cadena_Vacia()
    {
        // Los procedimientos comparan `IF @X <> ''`, no `IS NULL`. Y legacy
        // mandaba null porque su InputDTO no inicializaba el campo, lo que hacia
        // fallar el comando entero con "has no default value".
        var repo = new FakeObraRepository();
        var service = new ObraService(repo);

        await service.ConsultarGerentesAsync(
            new ObraCatalogoFiltro { Texto = null }, 0, CancellationToken.None);

        Assert.Equal(string.Empty, repo.UltimoFiltro!.Texto);
    }

    [Fact]
    public async Task Obra_Recorta_Los_Espacios_Del_Filtro()
    {
        var repo = new FakeObraRepository();
        var service = new ObraService(repo);

        await service.ConsultarGerentesAsync(
            new ObraCatalogoFiltro { Texto = "  PEREZ  " }, 0, CancellationToken.None);

        Assert.Equal("PEREZ", repo.UltimoFiltro!.Texto);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public async Task Obra_La_Bandera_Sin_Valor_Y_El_False_No_Filtran(bool? valor, int esperado)
    {
        // El procedimiento compara `<> 0`: el 0 es "no filtres". Pedir "los que NO
        // son representante legal" no se puede, ni aqui ni en Mac31, y por eso un
        // false explicito tiene que viajar como 0 y no como algo distinto.
        var repo = new FakeObraRepository();
        var service = new ObraService(repo);

        await service.ConsultarPuestosAsync(new ObraCatalogoFiltro(), valor, null, CancellationToken.None);

        Assert.Equal(esperado, repo.UltimoEsRepresentanteLegal);
    }

    [Fact]
    public async Task Obra_Conserva_El_IdStatus_Tal_Como_Llega()
    {
        // No se traduce ni se corrige: 0 significa TODOS para
        // fn_RegresaTablaStatus, y es lo que manda Mac31 cuando no se filtra
        // (su InputDTO nace en 0 y pisa el default 1 del procedimiento).
        var repo = new FakeObraRepository();
        var service = new ObraService(repo);

        await service.ConsultarGerentesAsync(
            new ObraCatalogoFiltro { IDStatus = 0 }, 0, CancellationToken.None);

        Assert.Equal(0, repo.UltimoFiltro!.IDStatus);
    }

    // ----------------------------------------------------- catalogos (marcas)

    [Fact]
    public async Task Marcas_El_Id_Suelto_Gana_Sobre_La_Lista()
    {
        // sp_n_ConsultaMarcas no tiene @IDMarca: solo @IDsMarca. Mac31 resuelve el
        // id suelto convirtiendolo a cadena y descartando la lista.
        var repo = new FakeCatalogosComplementosRepository();
        var service = new CatalogosComplementosService(repo);

        await service.ConsultarMarcasAsync(7, new[] { 1, 2, 3 }, 0, null, false, CancellationToken.None);

        Assert.Equal("7", repo.UltimoIdsMarca);
    }

    [Fact]
    public async Task Marcas_Une_La_Lista_Con_Tilde()
    {
        var repo = new FakeCatalogosComplementosRepository();
        var service = new CatalogosComplementosService(repo);

        await service.ConsultarMarcasAsync(0, new[] { 4, 9 }, 0, null, false, CancellationToken.None);

        Assert.Equal("4~9", repo.UltimoIdsMarca);
    }

    [Fact]
    public async Task Marcas_Descarta_El_Cero_De_La_Lista()
    {
        // ESTO ES LO IMPORTANTE DE ESTA CLASE DE PRUEBAS.
        // El procedimiento NO tiene la guarda `<> '0'` que si tienen los filtros de
        // categoria: un IDsMarca = "0" se vuelve `IDMarca IN (0)` y devuelve CERO
        // renglones. "Sin filtro" tiene que viajar como cadena vacia, o el catalogo
        // sale vacio y nadie entiende por que.
        var repo = new FakeCatalogosComplementosRepository();
        var service = new CatalogosComplementosService(repo);

        await service.ConsultarMarcasAsync(0, new[] { 0 }, 0, null, false, CancellationToken.None);

        Assert.Equal(string.Empty, repo.UltimoIdsMarca);
    }

    [Fact]
    public async Task Marcas_Sin_Lista_Manda_Cadena_Vacia()
    {
        var repo = new FakeCatalogosComplementosRepository();
        var service = new CatalogosComplementosService(repo);

        await service.ConsultarMarcasAsync(0, null, 0, null, false, CancellationToken.None);

        Assert.Equal(string.Empty, repo.UltimoIdsMarca);
    }

    [Fact]
    public async Task Catalogos_Quita_El_Apostrofo_De_La_Clave_Del_Sat()
    {
        var repo = new FakeCatalogosComplementosRepository();
        var service = new CatalogosComplementosService(repo);

        await service.ConsultarCatalogoFeAsync(CatalogoFe.FormasPago, 0, "0'1", false, CancellationToken.None);

        Assert.Equal("01", repo.UltimaClaveFe);
    }

    // ------------------------------------------------------------- productos

    [Fact]
    public async Task Productos_Arma_Los_Porcentajes_Con_Punto_Decimal_Invariante()
    {
        // Con la cultura del servidor en espanol, 12.5m.ToString() da "12,5": el
        // procedimiento parte la cadena por `~` y una coma ahi no estorba... pero
        // FORMAT y CONVERT adentro leerian otro numero. Se fija la cultura.
        var repo = new FakeProductosRepository();
        var service = new ProductosService(repo);

        await service.ConsultarAsync(new ProductosFiltro
        {
            PorcentajesDescuento = new[] { 12.5m, 5m }
        }, CancellationToken.None);

        Assert.Equal("12.5~5", repo.UltimoFiltro!.PorcentajesDescuento);
    }

    [Fact]
    public async Task Productos_Sin_Porcentajes_Manda_Cadena_Vacia()
    {
        var repo = new FakeProductosRepository();
        var service = new ProductosService(repo);

        await service.ConsultarAsync(new ProductosFiltro(), CancellationToken.None);

        Assert.Equal(string.Empty, repo.UltimoFiltro!.PorcentajesDescuento);
    }

    [Fact]
    public async Task Productos_Descarta_El_Cero_De_Las_Tres_Listas()
    {
        var repo = new FakeProductosRepository();
        var service = new ProductosService(repo);

        await service.ConsultarAsync(new ProductosFiltro
        {
            IDsMarca = new[] { 0, 3 },
            IDsCategoria = new[] { 0 },
            IDsGrupoCategoria = new[] { 0, 1, 2 }
        }, CancellationToken.None);

        Assert.Equal("3", repo.UltimoFiltro!.IDsMarca);
        Assert.Equal(string.Empty, repo.UltimoFiltro.IDsCategoria);
        Assert.Equal("1~2", repo.UltimoFiltro.IDsGrupoCategoria);
    }

    [Fact]
    public async Task Productos_No_Revienta_Cuando_No_Vienen_Clave_Ni_Descripcion()
    {
        // En legacy esto era un NullReferenceException y un 400: su service hacia
        // Clave.Replace(...) sobre un campo que su propio DTO no inicializaba.
        var repo = new FakeProductosRepository();
        var service = new ProductosService(repo);

        await service.ConsultarAsync(new ProductosFiltro { Clave = null, Descripcion = null }, CancellationToken.None);

        Assert.Equal(string.Empty, repo.UltimoFiltro!.Clave);
        Assert.Equal(string.Empty, repo.UltimoFiltro.Descripcion);
    }

    [Fact]
    public async Task Productos_Quita_El_Apostrofo_De_Clave_Y_Descripcion()
    {
        var repo = new FakeProductosRepository();
        var service = new ProductosService(repo);

        await service.ConsultarAsync(new ProductosFiltro
        {
            Clave = "AM'22F",
            Descripcion = " O'RING "
        }, CancellationToken.None);

        Assert.Equal("AM22F", repo.UltimoFiltro!.Clave);
        Assert.Equal("ORING", repo.UltimoFiltro.Descripcion);
    }

    // --------------------------------------------------------------- huellas

    [Fact]
    public async Task Huellas_El_Todas_Se_Traduce_Al_Valor_Magico_Menos_Uno()
    {
        var repo = new FakeHuellasRepository();
        var service = new HuellasService(repo);

        await service.ConsultarAsync(HuellaOrigen.Empleado, 0, todas: true, CancellationToken.None);

        Assert.Equal(-1, repo.UltimoId);
        Assert.Equal(HuellaOrigen.Empleado, repo.UltimoOrigen);
    }

    [Fact]
    public async Task Huellas_Sin_Id_Y_Sin_Todas_Es_Un_Error_De_Argumento()
    {
        // No se deja pasar: con los tres parametros en 0 el procedimiento no emite
        // NINGUN result set y legacy contestaba 500 "[No se pudo procesar
        // sp_n_ConsultaHuellas]". Un error claro vale mas que un 500 de base.
        var repo = new FakeHuellasRepository();
        var service = new HuellasService(repo);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ConsultarAsync(HuellaOrigen.Usuario, 0, todas: false, CancellationToken.None));
    }

    [Fact]
    public async Task Huellas_Con_Id_Concreto_No_Pide_Todas()
    {
        var repo = new FakeHuellasRepository();
        var service = new HuellasService(repo);

        await service.ConsultarAsync(HuellaOrigen.Cliente, 42, todas: false, CancellationToken.None);

        Assert.Equal(42, repo.UltimoId);
        Assert.Equal(HuellaOrigen.Cliente, repo.UltimoOrigen);
    }

    // ------------------------------------------------------------------ fakes

    private sealed class FakeObraRepository : IObraRepository
    {
        public ObraCatalogoFiltro? UltimoFiltro { get; private set; }
        public int UltimoEsRepresentanteLegal { get; private set; } = -99;

        public Task<List<ObraGerenteDto>> ConsultarGerentesAsync(ObraCatalogoFiltro filtro, int idTipoGerente, CancellationToken ct)
        {
            UltimoFiltro = filtro;
            return Task.FromResult(new List<ObraGerenteDto>());
        }

        public Task<List<ObraPuestoDto>> ConsultarPuestosAsync(ObraCatalogoFiltro filtro, int esRepresentanteLegal, int esRepresentanteTecnico, CancellationToken ct)
        {
            UltimoFiltro = filtro;
            UltimoEsRepresentanteLegal = esRepresentanteLegal;
            return Task.FromResult(new List<ObraPuestoDto>());
        }

        public Task<List<ObraResidenteDto>> ConsultarResidentesAsync(ObraCatalogoFiltro filtro, int idPuesto, int esSupervision, int esObra, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<List<ObraUnidadMedidaDto>> ConsultarUnidadesMedidaAsync(ObraCatalogoFiltro filtro, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<List<ObraTipoDto>> ConsultarTipoAsync(ObraTipoCatalogo tipo, ObraCatalogoFiltro filtro, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<List<ObraProveedorDto>> ConsultarProveedoresAsync(ObraProveedorFiltro filtro, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<List<ObraContratoGrupoDto>> ConsultarContratoGruposAsync(int idContratoGrupo, int idContrato, ObraCatalogoFiltro filtro, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<List<ObraContratoConceptoDto>> ConsultarContratoConceptosAsync(int idContratoConcepto, int idContratoGrupo, int idContrato, ObraCatalogoFiltro filtro, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<List<ObraContratoEstimacionDto>> ConsultarContratoEstimacionesAsync(int idContratoEstimacion, int idContrato, ObraCatalogoFiltro filtro, CancellationToken ct)
            => throw new NotImplementedException();
    }

    private sealed class FakeCatalogosComplementosRepository : ICatalogosComplementosRepository
    {
        public string? UltimoIdsMarca { get; private set; }
        public string? UltimaClaveFe { get; private set; }

        public Task<List<MarcaCatalogoDto>> ConsultarMarcasAsync(string idsMarca, int idStatus, string texto, bool exacto, CancellationToken ct)
        {
            UltimoIdsMarca = idsMarca;
            return Task.FromResult(new List<MarcaCatalogoDto>());
        }

        public Task<List<CatalogoFeDto>> ConsultarCatalogoFeAsync(CatalogoFe cual, int id, string clave, bool exacto, CancellationToken ct)
        {
            UltimaClaveFe = clave;
            return Task.FromResult(new List<CatalogoFeDto>());
        }

        public Task<List<TipoDomicilioDto>> ConsultarTiposDomicilioAsync(int id, int idStatus, string texto, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<List<CondicionCreditoDto>> ConsultarCondicionesCreditoAsync(int id, int idStatus, string texto, bool exacto, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<List<EmpresaSerieDto>> ConsultarEmpresasSeriesAsync(int id, int idEmpresa, int idStatus, string texto, bool exacto, CancellationToken ct)
            => throw new NotImplementedException();
    }

    private sealed class FakeProductosRepository : IProductosRepository
    {
        public ProductosFiltroSp? UltimoFiltro { get; private set; }

        public Task<List<ProductoDto>> ConsultarAsync(ProductosFiltroSp filtro, CancellationToken ct)
        {
            UltimoFiltro = filtro;
            return Task.FromResult(new List<ProductoDto>());
        }
    }

    private sealed class FakeHuellasRepository : IHuellasRepository
    {
        public HuellaOrigen UltimoOrigen { get; private set; }
        public int UltimoId { get; private set; } = -99;

        public Task<List<HuellaDto>> ConsultarAsync(HuellaOrigen origen, int id, CancellationToken ct)
        {
            UltimoOrigen = origen;
            UltimoId = id;
            return Task.FromResult(new List<HuellaDto>());
        }
    }
}
