using ISL_Service.Application.DTOs.ObraCatalogos;
using ISL_Service.Application.DTOs.ObraContratos;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Services;
using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Tests;

/*
  LAS REGLAS DE OBRA QUE SE PUEDEN PROBAR SIN BASE.

  Son las que Legacy NO tenia y por las que se colaban datos malos a la base, mas
  las dos correcciones de defecto que cambian el comportamiento: que el status -1
  del SP sea un 409 y que "eliminar" un concepto no le borre los datos.
*/
public class Ola5ObraReglasTests
{
    // ==================================================================
    // Catalogos
    // ==================================================================

    [Fact]
    public async Task Un_Residente_Tiene_Que_Ser_De_Obra_O_De_Supervision()
    {
        // Con las dos banderas en cero no sale en ninguno de los dos desplegables
        // de contrato: queda en el catalogo y no se puede elegir nunca.
        var service = new ObraCatalogosEscrituraService(new FakeObraCatalogosRepo());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CrearResidenteAsync(
                new CrearObraResidenteRequest { IdPuesto = 1, Residente = "JUAN", EsSupervision = false, EsObra = false },
                3, "PC"));
    }

    [Fact]
    public async Task Un_Residente_De_Obra_Pasa()
    {
        var repo = new FakeObraCatalogosRepo();
        var service = new ObraCatalogosEscrituraService(repo);

        var r = await service.CrearResidenteAsync(
            new CrearObraResidenteRequest { IdPuesto = 1, Residente = "JUAN", EsObra = true }, 3, "PC");

        Assert.Equal(11, r.Id);
    }

    [Fact]
    public async Task El_Status_Solo_Puede_Ser_1_O_2()
    {
        // Legacy mandaba al SP el IDStatus que viniera, y el 0 por omision dejaba
        // el renglon en un status que no existe: invisible y sin forma de
        // reactivarlo.
        var service = new ObraCatalogosEscrituraService(new FakeObraCatalogosRepo());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ActualizarTipoObraAsync(1, new ActualizarObraCatalogoSimpleRequest { Nombre = "X", IdStatus = 0 }, 3, "PC"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ActualizarTipoObraAsync(1, new ActualizarObraCatalogoSimpleRequest { Nombre = "X", IdStatus = 3 }, 3, "PC"));
    }

    [Fact]
    public async Task El_Nombre_Es_Requerido_Y_Tiene_Tope()
    {
        var service = new ObraCatalogosEscrituraService(new FakeObraCatalogosRepo());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CrearTipoObraAsync(new CrearObraCatalogoSimpleRequest { Nombre = "  " }, 3, "PC"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CrearTipoObraAsync(new CrearObraCatalogoSimpleRequest { Nombre = new string('X', 401) }, 3, "PC"));
    }

    [Fact]
    public async Task El_Menos_Uno_Del_Procedimiento_Es_Un_409_Y_No_Un_200()
    {
        // Estos SP no lanzan RAISERROR: contestan -1 con 200. Legacy lo pasaba tal
        // cual al cliente y la pantalla tenia que acordarse de mirarlo.
        var repo = new FakeObraCatalogosRepo { Resultado = new ResultadoSp(-1, "ERROR") };
        var service = new ObraCatalogosEscrituraService(repo);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CrearTipoObraAsync(new CrearObraCatalogoSimpleRequest { Nombre = "CARRETERA" }, 3, "PC"));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task El_Rfc_Del_Proveedor_Se_Guarda_En_Mayusculas()
    {
        // Es con lo que se timbra y con lo que se compara contra el padron. Legacy
        // lo guardaba como lo escribieran y el mismo contratista salia dos veces.
        var repo = new FakeObraCatalogosRepo();
        var service = new ObraCatalogosEscrituraService(repo);

        await service.CrearProveedorAsync(
            new CrearObraProveedorRequest { Nombre = "CONSTRUCTORA", Rfc = " abc950101xx1 " }, 3, "PC");

        Assert.Equal("ABC950101XX1", repo.UltimoProveedor!.Rfc);
    }

    [Fact]
    public async Task El_Monto_De_Una_Partida_No_Puede_Ser_Negativo()
    {
        var service = new ObraCatalogosEscrituraService(new FakeObraCatalogosRepo());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CrearPartidaAsync(new CrearObraPartidaRequest { Partida = "1", Monto = -1m }, 3, "PC"));
    }

    // ==================================================================
    // Contratos
    // ==================================================================

    [Fact]
    public async Task Un_Contrato_No_Puede_Terminar_Antes_De_Empezar()
    {
        // Legacy solo comprobaba que las tres cadenas de fecha no estuvieran
        // vacias. Un contrato al reves hacia que el programa de obra saliera con
        // avances negativos.
        var service = new ObraContratosService(new FakeObraContratosRepo());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CrearContratoAsync(ContratoValido(c =>
            {
                c.FechaInicio = new DateOnly(2026, 5, 1);
                c.FechaTermino = new DateOnly(2026, 4, 1);
            }), 3, "PC"));
    }

    [Fact]
    public async Task El_Total_Del_Contrato_Tiene_Que_Cuadrar_Con_Subtotal_Mas_Iva()
    {
        // El SP guarda los tres numeros tal como llegan y no recalcula ninguno.
        var service = new ObraContratosService(new FakeObraContratosRepo());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CrearContratoAsync(ContratoValido(c =>
            {
                c.SubTotal = 100m;
                c.Iva = 16m;
                c.Total = 200m;
            }), 3, "PC"));
    }

    [Fact]
    public async Task El_Total_Del_Contrato_Admite_Un_Centavo_De_Redondeo()
    {
        var service = new ObraContratosService(new FakeObraContratosRepo());

        var r = await service.CrearContratoAsync(ContratoValido(c =>
        {
            c.SubTotal = 100m;
            c.Iva = 16m;
            c.Total = 116.01m;
        }), 3, "PC");

        Assert.Equal(11, r.Id);
    }

    [Fact]
    public async Task El_Ejercicio_Tiene_Que_Ser_Un_Ano_De_Cuatro_Cifras()
    {
        // En Legacy el ejercicio era un string contra un parametro int: vacio
        // reventaba el alta completa con un error de conversion.
        var service = new ObraContratosService(new FakeObraContratosRepo());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CrearContratoAsync(ContratoValido(c => c.Ejercicio = 26), 3, "PC"));
    }

    [Fact]
    public async Task Los_Filtros_De_Contrato_No_Se_Tiran_Cuando_Viene_El_Numero()
    {
        /*
          EL DEFECTO QUE NO SE HEREDA.
          Legacy: `if (Contrato != "") { ...reconstruye el DTO... }`, asi que
          filtrar por numero de contrato Y por status devolvia los de cualquier
          status.
        */
        var repo = new FakeObraContratosRepo();
        var service = new ObraContratosService(repo);

        await service.ConsultarContratosAsync(new ObraContratosFiltro
        {
            Contrato = "OP-2026-001",
            IdStatus = 1,
            IdTipoObra = 4
        });

        Assert.Equal("OP-2026-001", repo.UltimoFiltro!.Contrato);
        Assert.Equal(1, repo.UltimoFiltro.IdStatus);
        Assert.Equal(4, repo.UltimoFiltro.IdTipoObra);
    }

    // ==================================================================
    // Reordenar
    // ==================================================================

    [Fact]
    public async Task Subir_Es_Menos_Uno_Y_Bajar_Es_Mas_Uno()
    {
        // @Orden es un DELTA, y es la bandera que decide el modo entero del SP.
        var repo = new FakeObraContratosRepo();
        var service = new ObraContratosService(repo);

        await service.ReordenarGrupoAsync(5, new ReordenarRequest { IdContrato = 1, Direccion = "subir" }, 3, "PC");
        Assert.Equal(-1, repo.UltimoDelta);

        await service.ReordenarGrupoAsync(5, new ReordenarRequest { IdContrato = 1, Direccion = "BAJAR" }, 3, "PC");
        Assert.Equal(1, repo.UltimoDelta);
    }

    [Fact]
    public async Task Reordenar_Exige_El_Contrato()
    {
        /*
          En la rama de reordenar, el SP intercambia con el vecino usando
          `WHERE IDContrato = @IDContrato`. Legacy no lo exigia en /subir ni en
          /bajar: sin el, el intercambio no ocurria y quedaban DOS renglones con el
          mismo Orden, sin error y sin rastro.
        */
        var service = new ObraContratosService(new FakeObraContratosRepo());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReordenarGrupoAsync(5, new ReordenarRequest { Direccion = "subir" }, 3, "PC"));
    }

    [Fact]
    public async Task Reordenar_Rechaza_Una_Direccion_Que_No_Existe()
    {
        var service = new ObraContratosService(new FakeObraContratosRepo());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReordenarGrupoAsync(5, new ReordenarRequest { IdContrato = 1, Direccion = "arriba" }, 3, "PC"));
    }

    // ==================================================================
    // Dar de baja un concepto sin borrarle los datos
    // ==================================================================

    [Fact]
    public async Task Dar_De_Baja_Un_Concepto_Conserva_Su_Clave_Precio_Y_Cantidad()
    {
        /*
          EL DEFECTO MAS GRANDE DEL LOTE.
          En Legacy, PUT /conceptos/eliminar validaba solo el id y llamaba al mismo
          update, asi que el SP recibia Clave y Concepto vacios y PrecioUnitario y
          Cantidad en cero, y su UPDATE los escribia. Borrar un concepto le borraba
          los datos y lo dejaba en IDStatus = 0, que no es ni activo ni cancelado.
        */
        var repo = new FakeObraContratosRepo
        {
            ConceptosResult = new List<Dictionary<string, object?>>
            {
                new()
                {
                    ["idContratoConcepto"] = 10,
                    ["idContratoGrupo"] = 5,
                    ["idUnidadMedida"] = 6,
                    ["idContratoPartida"] = 2,
                    ["clave"] = "1.4",
                    ["concepto"] = "REVISION INTEGRAL DE LA PROPUESTA",
                    ["precioUnitario"] = 106885.47m,
                    ["cantidad"] = 0.50m,
                    ["ajuste"] = 1.25m,
                    ["conceptoGlobal"] = 1
                }
            }
        };
        var service = new ObraContratosService(repo);

        await service.EliminarConceptoAsync(10, 1, 3, "PC");

        var enviado = repo.UltimoConceptoActualizado!;
        Assert.Equal(2, enviado.IdStatus);                      // baja logica
        Assert.Equal("1.4", enviado.Clave);                     // y nada se perdio
        Assert.Equal("REVISION INTEGRAL DE LA PROPUESTA", enviado.Concepto);
        Assert.Equal(106885.47m, enviado.PrecioUnitario);
        Assert.Equal(0.50m, enviado.Cantidad);
        Assert.Equal(1.25m, enviado.Ajuste);
        Assert.True(enviado.ConceptoGlobal);
        Assert.Equal(5, enviado.IdContratoGrupo);
    }

    [Fact]
    public async Task Dar_De_Baja_Un_Concepto_Que_No_Existe_Es_404()
    {
        var repo = new FakeObraContratosRepo { ConceptosResult = new List<Dictionary<string, object?>>() };
        var service = new ObraContratosService(repo);

        await Assert.ThrowsAsync<NotFoundException>(() => service.EliminarConceptoAsync(10, 1, 3, "PC"));
    }

    // ==================================================================
    // Las cinco listas paralelas de la estimacion
    // ==================================================================

    [Fact]
    public async Task Las_Cantidades_Estimadas_Tienen_Que_Medir_Lo_Mismo_Que_Los_Conceptos()
    {
        /*
          El SP empareja por posicion. Legacy solo comprobaba que las dos primeras
          listas no estuvieran vacias: con una cantidad de menos, el ultimo concepto
          se estimaba con la cantidad de otro. En una estimacion de obra eso es
          dinero que se paga mal.
        */
        var service = new ObraContratosService(new FakeObraContratosRepo());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ActualizarConceptoEstimacionAsync(new ActualizarObraConceptoEstimacionRequest
            {
                IdContrato = 1,
                IdContratoEstimacion = 2,
                IdsContratoConcepto = new List<int> { 1, 2, 3 },
                Cantidades = new List<decimal> { 1m, 2m }
            }, 3, "PC"));
    }

    [Fact]
    public async Task Las_Listas_Opcionales_De_La_Estimacion_Pueden_Venir_Vacias()
    {
        var service = new ObraContratosService(new FakeObraContratosRepo());

        var r = await service.ActualizarConceptoEstimacionAsync(new ActualizarObraConceptoEstimacionRequest
        {
            IdContrato = 1,
            IdContratoEstimacion = 2,
            IdsContratoConcepto = new List<int> { 1, 2 },
            Cantidades = new List<decimal> { 1m, 2m }
        }, 3, "PC");

        Assert.Equal(11, r.Id);
    }

    [Fact]
    public async Task La_Estimacion_Rechaza_El_Mismo_Concepto_Dos_Veces()
    {
        var service = new ObraContratosService(new FakeObraContratosRepo());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ActualizarConceptoEstimacionAsync(new ActualizarObraConceptoEstimacionRequest
            {
                IdContrato = 1,
                IdContratoEstimacion = 2,
                IdsContratoConcepto = new List<int> { 7, 7 },
                Cantidades = new List<decimal> { 1m, 2m }
            }, 3, "PC"));
    }

    [Fact]
    public async Task La_Fecha_Real_De_Termino_Solo_Existe_Si_El_Contrato_Se_Termina()
    {
        var service = new ObraContratosService(new FakeObraContratosRepo());

        // Termina el contrato y no dice cuando.
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CrearEstimacionAsync(EstimacionValida(e =>
            {
                e.ContratoTerminado = true;
                e.FechaRealTermino = null;
            }), 3, "PC"));

        // No lo termina y manda la fecha igual.
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CrearEstimacionAsync(EstimacionValida(e =>
            {
                e.ContratoTerminado = false;
                e.FechaRealTermino = new DateOnly(2026, 6, 1);
            }), 3, "PC"));
    }

    [Fact]
    public async Task El_Avance_Fisico_Va_De_0_A_100()
    {
        var service = new ObraContratosService(new FakeObraContratosRepo());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CrearEstimacionAsync(EstimacionValida(e => e.PorcentajeAvanceFisicoReal = 150m), 3, "PC"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CrearPeriodoAsync(new CrearObraContratoPeriodoRequest
            {
                IdContrato = 1,
                Periodo = "MAYO",
                FechaInicio = new DateOnly(2026, 5, 1),
                FechaTermino = new DateOnly(2026, 5, 31),
                PorcentajeAvanceFisicoProgramado = -1m
            }, 3, "PC"));
    }

    [Fact]
    public async Task Un_Convenio_Si_Puede_Ser_Negativo_Porque_Puede_Reducir_El_Monto()
    {
        var service = new ObraContratosService(new FakeObraContratosRepo());

        var r = await service.CrearConvenioAsync(new CrearObraConvenioRequest
        {
            IdContrato = 1,
            IdConvenioMotivo = 2,
            Convenio = "CV-1",
            FechaInicio = new DateOnly(2026, 5, 1),
            FechaTermino = new DateOnly(2026, 6, 1),
            SubTotal = -100m,
            Iva = -16m,
            Total = -116m
        }, 3, "PC");

        Assert.Equal(11, r.Id);
    }

    // ------------------------------------------------------------------
    // Ayudas
    // ------------------------------------------------------------------

    private static CrearObraContratoRequest ContratoValido(Action<CrearObraContratoRequest>? ajuste = null)
    {
        var c = new CrearObraContratoRequest
        {
            IdProveedor = 1,
            IdTipoObra = 1,
            IdTipoContratacion = 1,
            IdTipoRecurso = 1,
            IdResidenteObra = 1,
            Contrato = "OP-2026-001",
            Descripcion = "PAVIMENTACION",
            FechaContratacion = new DateOnly(2026, 4, 1),
            FechaInicio = new DateOnly(2026, 4, 15),
            FechaTermino = new DateOnly(2026, 8, 15),
            Ejercicio = 2026
        };
        ajuste?.Invoke(c);
        return c;
    }

    private static CrearObraContratoEstimacionRequest EstimacionValida(Action<CrearObraContratoEstimacionRequest>? ajuste = null)
    {
        var e = new CrearObraContratoEstimacionRequest
        {
            IdContrato = 1,
            Nombre = "ESTIMACION 1",
            FechaElaboracion = new DateOnly(2026, 5, 1),
            FechaInicioEstimacion = new DateOnly(2026, 4, 15),
            FechaTerminoEstimacion = new DateOnly(2026, 4, 30)
        };
        ajuste?.Invoke(e);
        return e;
    }
}

// ======================================================================
// Dobles escritos a mano: el proyecto no usa libreria de mocks a proposito.
// ======================================================================

internal class FakeObraCatalogosRepo : IObraCatalogosEscrituraRepository
{
    public ResultadoSp Resultado { get; set; } = new(11, string.Empty);
    public CrearObraProveedorRequest? UltimoProveedor { get; private set; }

    private Task<ResultadoSp> Ok() => Task.FromResult(Resultado);

    public Task<ResultadoSp> InsertarTipoContratacionAsync(string nombre, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ActualizarTipoContratacionAsync(int id, string nombre, int idStatus, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> InsertarTipoDocumentoAsync(string nombre, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ActualizarTipoDocumentoAsync(int id, string nombre, int idStatus, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> InsertarTipoObraAsync(string nombre, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ActualizarTipoObraAsync(int id, string nombre, int idStatus, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> InsertarTipoRecursoAsync(string nombre, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ActualizarTipoRecursoAsync(int id, string nombre, int idStatus, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> InsertarPuestoAsync(CrearObraPuestoRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ActualizarPuestoAsync(int id, ActualizarObraPuestoRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> InsertarResidenteAsync(CrearObraResidenteRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ActualizarResidenteAsync(int id, ActualizarObraResidenteRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> InsertarGerenteAsync(CrearObraGerenteRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ActualizarGerenteAsync(int id, ActualizarObraGerenteRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> InsertarUnidadMedidaAsync(CrearObraUnidadMedidaRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ActualizarUnidadMedidaAsync(int id, ActualizarObraUnidadMedidaRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();

    public Task<List<Dictionary<string, object?>>> ConsultarPartidasAsync(ObraPartidasFiltro filtro, CancellationToken ct = default)
        => Task.FromResult(new List<Dictionary<string, object?>>());

    public Task<ResultadoSp> InsertarPartidaAsync(CrearObraPartidaRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ActualizarPartidaAsync(int id, ActualizarObraPartidaRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();

    public Task<ResultadoSp> InsertarProveedorAsync(CrearObraProveedorRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        UltimoProveedor = request;
        return Ok();
    }

    public Task<ResultadoSp> ActualizarProveedorAsync(int id, ActualizarObraProveedorRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        UltimoProveedor = request;
        return Ok();
    }
}

internal class FakeObraContratosRepo : IObraContratosRepository
{
    public ResultadoSp Resultado { get; set; } = new(11, string.Empty);
    public List<Dictionary<string, object?>> ConceptosResult { get; set; } = new();

    public ObraContratosFiltro? UltimoFiltro { get; private set; }
    public int UltimoDelta { get; private set; }
    public ActualizarObraContratoConceptoRequest? UltimoConceptoActualizado { get; private set; }

    private Task<ResultadoSp> Ok() => Task.FromResult(Resultado);
    private static Task<List<Dictionary<string, object?>>> Vacio() => Task.FromResult(new List<Dictionary<string, object?>>());

    public Task<List<Dictionary<string, object?>>> ConsultarContratosAsync(ObraContratosFiltro filtro, CancellationToken ct = default)
    {
        UltimoFiltro = filtro;
        return Vacio();
    }

    public Task<ResultadoSp> InsertarContratoAsync(CrearObraContratoRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ActualizarContratoAsync(int id, ActualizarObraContratoRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ActualizarContratoPartidasIndividualesAsync(int id, bool partidasIndividuales, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ActualizarContratoStatusAsync(int id, bool contratoCerrado, int idUsuario, string equipo, CancellationToken ct = default) => Ok();

    public Task<List<Dictionary<string, object?>>> ConsultarConveniosAsync(ObraConveniosFiltro filtro, CancellationToken ct = default) => Vacio();
    public Task<ResultadoSp> InsertarConvenioAsync(CrearObraConvenioRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ActualizarConvenioAsync(int id, ActualizarObraConvenioRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ActualizarConvenioStatusAsync(int id, bool convenioCerrado, int idUsuario, string equipo, CancellationToken ct = default) => Ok();

    public Task<ResultadoSp> InsertarGrupoAsync(CrearObraContratoGrupoRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ActualizarGrupoAsync(int id, ActualizarObraContratoGrupoRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();

    public Task<ResultadoSp> ReordenarGrupoAsync(int id, int idContrato, int delta, int idUsuario, string equipo, CancellationToken ct = default)
    {
        UltimoDelta = delta;
        return Ok();
    }

    public Task<List<Dictionary<string, object?>>> ConsultarConceptosAsync(ObraContratoConceptosFiltro filtro, CancellationToken ct = default)
        => Task.FromResult(ConceptosResult);

    public Task<List<Dictionary<string, object?>>> ConsultarConceptosEstimacionesAsync(ObraConceptosEstimacionesFiltro filtro, CancellationToken ct = default) => Vacio();
    public Task<ResultadoSp> InsertarConceptoAsync(CrearObraContratoConceptoRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();

    public Task<ResultadoSp> ActualizarConceptoAsync(int id, ActualizarObraContratoConceptoRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        UltimoConceptoActualizado = request;
        return Ok();
    }

    public Task<ResultadoSp> ReordenarConceptoAsync(int id, int idContrato, int delta, int idUsuario, string equipo, CancellationToken ct = default)
    {
        UltimoDelta = delta;
        return Ok();
    }

    public Task<ResultadoSp> CargarConceptosDeArchivoAsync(CargarObraContratoConceptosRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ActualizarConceptoEstimacionAsync(ActualizarObraConceptoEstimacionRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();

    public Task<List<Dictionary<string, object?>>> ConsultarContratoPartidasAsync(ObraContratoPartidasFiltro filtro, CancellationToken ct = default) => Vacio();
    public Task<ResultadoSp> InsertarContratoPartidaAsync(CrearObraContratoPartidaRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ActualizarContratoPartidaAsync(int id, ActualizarObraContratoPartidaRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();

    public Task<ResultadoSp> ReordenarContratoPartidaAsync(int id, int idContrato, int delta, int idUsuario, string equipo, CancellationToken ct = default)
    {
        UltimoDelta = delta;
        return Ok();
    }

    public Task<List<Dictionary<string, object?>>> ConsultarPeriodosAsync(ObraContratoPeriodosFiltro filtro, CancellationToken ct = default) => Vacio();
    public Task<ResultadoSp> InsertarPeriodoAsync(CrearObraContratoPeriodoRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ActualizarPeriodoAsync(int id, ActualizarObraContratoPeriodoRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();

    public Task<ResultadoSp> InsertarEstimacionAsync(CrearObraContratoEstimacionRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ActualizarEstimacionAsync(int id, ActualizarObraContratoEstimacionRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
}
