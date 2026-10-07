using ISL_Service.Application.DTOs.Aviacion;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Services;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Tests;

/*
  AVIACION: LO QUE SE PUEDE PROBAR SIN LA BASE MacAviacion.

  Ningun sp_n_ de aviacion esta instalado en Produccion_svr y
  Constantes.AviacionDBServer esta vacia, asi que estas capacidades no son
  comparables en esta maquina. Lo que si se prueba aqui es la logica que vive en
  C# y que es donde estaban los defectos: las validaciones, el armado de las
  listas con "~", y la TABLA DE SINCRONIZACION por operacion, que es un dato
  copiado de Legacy y que si se copia mal, la caja cobra con el catalogo viejo.
*/
public class Ola5AviacionReglasTests
{
    // ==================================================================
    // Las tablas que se sincronizan despues de cada cambio
    // ==================================================================

    [Fact]
    public async Task Dar_De_Alta_Una_Cuenta_Sincroniza_Cuentas_Amortizaciones_Y_Auxiliares()
    {
        var repo = new FakeAviacionCatalogosRepo();
        var service = new AviacionCatalogosService(repo, new FakeConexionDeAviacion { EsCaja = true });

        await service.CrearCuentaAsync(CuentaValida(), 3, "CAJA1");

        Assert.Equal(
            new[] { TablasDeAviacion.CatalogoCuentas, TablasDeAviacion.EconomicosAmortizaciones, TablasDeAviacion.EconomicosAuxiliares },
            repo.TablasSincronizadas);
    }

    [Fact]
    public async Task Cambiar_Una_Cuenta_Sincroniza_Dos_Tablas_Y_No_Tres()
    {
        var repo = new FakeAviacionCatalogosRepo();
        var service = new AviacionCatalogosService(repo, new FakeConexionDeAviacion { EsCaja = true });

        await service.ActualizarCuentaAsync(1, CuentaValidaActualizar(), 3, "CAJA1");

        Assert.Equal(
            new[] { TablasDeAviacion.CatalogoCuentas, TablasDeAviacion.EconomicosAmortizaciones },
            repo.TablasSincronizadas);
    }

    [Fact]
    public async Task Reasignar_Un_Economico_Sincroniza_Las_Cuatro_Tablas()
    {
        // Incluye el historico ([Catalogo Economicos His]): la reasignacion deja
        // rastro de que la unidad cambio de dueno.
        var repo = new FakeAviacionCatalogosRepo();
        var service = new AviacionCatalogosService(repo, new FakeConexionDeAviacion { EsCaja = true });

        await service.ReasignarEconomicoAsync(5, new ReasignarAviacionEconomicoRequest
        {
            IdPermisionarioAnterior = 1,
            IdPermisionarioNuevo = 2
        }, 3, "CAJA1");

        Assert.Equal(
            new[]
            {
                TablasDeAviacion.CatalogoEconomicos,
                TablasDeAviacion.CatalogoEconomicosHis,
                TablasDeAviacion.CatalogoPermisionarios,
                TablasDeAviacion.EconomicosAmortizaciones
            },
            repo.TablasSincronizadas);
    }

    [Fact]
    public async Task Si_La_Instalacion_No_Es_Una_Caja_No_Se_Sincroniza_Nada()
    {
        // En el central no hay replica a la que bajar nada. Legacy lo decide con
        // Constantes.AviacionIDCaja != 0; aqui igual.
        var repo = new FakeAviacionCatalogosRepo();
        var service = new AviacionCatalogosService(repo, new FakeConexionDeAviacion { EsCaja = false });

        await service.CrearCuentaAsync(CuentaValida(), 3, "SERVIDOR");

        Assert.Empty(repo.TablasSincronizadas);
    }

    [Fact]
    public async Task Si_El_Procedimiento_Dijo_Menos_Uno_No_Se_Sincroniza()
    {
        // Bajar tablas por un cambio que no ocurrio solo gasta tiempo de la caja.
        var repo = new FakeAviacionCatalogosRepo { Resultado = new ResultadoSp(-1, "ERROR") };
        var service = new AviacionCatalogosService(repo, new FakeConexionDeAviacion { EsCaja = true });

        await Assert.ThrowsAsync<ConflictException>(() => service.CrearCuentaAsync(CuentaValida(), 3, "CAJA1"));
        Assert.Empty(repo.TablasSincronizadas);
    }

    [Fact]
    public async Task Los_Conceptos_Y_Las_Cajas_No_Se_Sincronizan()
    {
        // Sus servicios de Legacy no llaman a sp_n_SincronizarDownload ni una vez.
        var repo = new FakeAviacionCatalogosRepo();
        var service = new AviacionCatalogosService(repo, new FakeConexionDeAviacion { EsCaja = true });

        await service.CrearConceptoAsync(new CrearAviacionConceptoRequest { NumConcepto = "001", NombreConcepto = "PEAJE" }, 3, "CAJA1");
        Assert.Empty(repo.TablasSincronizadas);
    }

    [Fact]
    public async Task Si_Falla_La_Sincronizacion_Se_Dice_Que_El_Cambio_Si_Se_Guardo()
    {
        // "Se guardo y no se replico" y "no se guardo" exigen cosas distintas de
        // quien esta en la caja.
        var repo = new FakeAviacionCatalogosRepo { ResultadoSincronizacion = new ResultadoSp(-1, "sin red") };
        var service = new AviacionCatalogosService(repo, new FakeConexionDeAviacion { EsCaja = true });

        var ex = await Assert.ThrowsAsync<ConflictException>(() => service.CrearCuentaAsync(CuentaValida(), 3, "CAJA1"));
        Assert.Contains("se guardo", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ==================================================================
    // Los porcentajes de tarjeta
    // ==================================================================

    [Fact]
    public void Los_Porcentajes_De_Tarjeta_Tienen_Que_Sumar_100()
    {
        // Es una tabla de distribucion del cobro con tarjeta. Si suman 90, el 10 por
        // ciento de cada cobro no se asigna a ninguna cuenta y la contabilidad
        // cuadra mal sin que nada avise.
        var ex = Assert.Throws<ArgumentException>(() =>
            AviacionCatalogosService.ValidarTarjetaPorcentajes(new ActualizarAviacionTarjetaPorcentajesRequest
            {
                IdsCuenta = new List<int> { 1, 2 },
                Porcentajes = new List<decimal> { 40m, 50m }
            }));

        Assert.Contains("100", ex.Message);
    }

    [Fact]
    public void Los_Porcentajes_De_Tarjeta_Que_Suman_100_Pasan()
    {
        AviacionCatalogosService.ValidarTarjetaPorcentajes(new ActualizarAviacionTarjetaPorcentajesRequest
        {
            IdsCuenta = new List<int> { 1, 2, 3 },
            Porcentajes = new List<decimal> { 33.33m, 33.33m, 33.34m }
        });
    }

    [Fact]
    public void Los_Porcentajes_De_Tarjeta_Tienen_Que_Medir_Lo_Mismo_Que_Las_Cuentas()
    {
        Assert.Throws<ArgumentException>(() =>
            AviacionCatalogosService.ValidarTarjetaPorcentajes(new ActualizarAviacionTarjetaPorcentajesRequest
            {
                IdsCuenta = new List<int> { 1, 2 },
                Porcentajes = new List<decimal> { 100m }
            }));
    }

    // ==================================================================
    // Las reglas de los catalogos
    // ==================================================================

    [Fact]
    public async Task Una_Cuenta_No_Puede_Ser_Globalizada_E_Individual_A_La_Vez()
    {
        var service = new AviacionCatalogosService(new FakeAviacionCatalogosRepo(), new FakeConexionDeAviacion());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CrearCuentaAsync(CuentaValida(c =>
            {
                c.CuentaGlobalizada = true;
                c.CuentaIndividual = true;
            }), 3, "CAJA1"));
    }

    [Fact]
    public async Task DebeHaber_Solo_Puede_Ser_D_O_H()
    {
        var service = new AviacionCatalogosService(new FakeAviacionCatalogosRepo(), new FakeConexionDeAviacion());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CrearCuentaAsync(CuentaValida(c => c.DebeHaber = "X"), 3, "CAJA1"));
    }

    [Fact]
    public async Task Un_Economico_Tiene_Que_Pagar_Al_Menos_Una_Cosa()
    {
        // Con las tres banderas en cero el economico existe, sale en el catalogo y
        // no se le puede cobrar nunca.
        var service = new AviacionCatalogosService(new FakeAviacionCatalogosRepo(), new FakeConexionDeAviacion());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CrearEconomicoAsync(new CrearAviacionEconomicoRequest
            {
                IdPermisionario = 1,
                NumEconomico = 10,
                NombreEconomico = "UNIDAD 10"
            }, 3, "CAJA1"));
    }

    [Fact]
    public async Task Reasignar_Al_Mismo_Permisionario_No_Es_Reasignar()
    {
        var service = new AviacionCatalogosService(new FakeAviacionCatalogosRepo(), new FakeConexionDeAviacion());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReasignarEconomicoAsync(5, new ReasignarAviacionEconomicoRequest
            {
                IdPermisionarioAnterior = 7,
                IdPermisionarioNuevo = 7
            }, 3, "CAJA1"));
    }

    [Fact]
    public async Task Un_Motivo_De_Fianza_Es_De_Ingreso_O_De_Egreso_Y_Solo_Uno()
    {
        // Y es irreversible: sp_n_ActualizarMotivoFianza no recibe estas banderas.
        var service = new AviacionCatalogosService(new FakeAviacionCatalogosRepo(), new FakeConexionDeAviacion());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CrearMotivoFianzaAsync(new CrearAviacionMotivoFianzaRequest
            {
                NumMotivoFianza = 1, MotivoFianza = "DEPOSITO", EsIngreso = false, EsEgreso = false
            }, 3, "CAJA1"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CrearMotivoFianzaAsync(new CrearAviacionMotivoFianzaRequest
            {
                NumMotivoFianza = 1, MotivoFianza = "DEPOSITO", EsIngreso = true, EsEgreso = true
            }, 3, "CAJA1"));
    }

    [Fact]
    public async Task Un_Operador_Necesita_Fecha_De_Alta()
    {
        // Legacy la mandaba como la cadena que trajera el cuerpo; vacia guardaba
        // 1900-01-01 y todos los calculos de antiguedad salian mal.
        var service = new AviacionCatalogosService(new FakeAviacionCatalogosRepo(), new FakeConexionDeAviacion());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CrearOperadorAsync(new CrearAviacionOperadorRequest
            {
                NumNomina = 100,
                OperadorNombre = "JUAN",
                OperadorApellidoPaterno = "PEREZ"
            }, 3, "CAJA1"));
    }

    [Fact]
    public async Task Actualizar_Una_Caja_Exige_Su_Id()
    {
        // Legacy no lo validaba: con IDCaja = 0 el UPDATE no tocaba nada y el
        // usuario creia que habia guardado.
        var service = new AviacionCatalogosService(new FakeAviacionCatalogosRepo(), new FakeConexionDeAviacion());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ActualizarCajaAsync(0, new ActualizarAviacionCajaRequest
            {
                NumCaja = 1, NombreCaja = "CAJA 1", EquipoCaja = "PC-1"
            }, 3, "PC"));
    }

    // ==================================================================
    // Polizas
    // ==================================================================

    [Fact]
    public async Task La_Poliza_No_Admite_Fecha_De_Cheques_Y_Dia_A_La_Vez()
    {
        // El SP usa uno o el otro y cual gana depende del orden de sus IF. Un cheque
        // con fecha equivocada no lo paga el banco.
        var service = new AviacionPolizasService(new FakeAviacionPolizasRepo());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CrearAsync(PolizaValida(p =>
            {
                p.FechaCheques = new DateOnly(2026, 5, 1);
                p.DiaCheques = 15;
            }), 3, "PC"));
    }

    [Fact]
    public async Task La_Poliza_Exige_Numero_De_Cheque_Y_De_Poliza()
    {
        var service = new AviacionPolizasService(new FakeAviacionPolizasRepo());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CrearAsync(PolizaValida(p => p.NumPolizaInicial = 0), 3, "PC"));
    }

    [Fact]
    public async Task Corregir_Un_Cheque_Exige_El_Numero_De_Poliza()
    {
        // Legacy validaba NumCheque DOS VECES (el mismo campo repetido) y nunca
        // NumPoliza: con 0, el cheque se quedaba sin poliza y no se podia amarrar
        // con la contabilidad.
        var service = new AviacionPolizasService(new FakeAviacionPolizasRepo());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ActualizarChequeAsync(1, new ActualizarAviacionPolizaChequeRequest
            {
                IdPoliza = 1, NumCheque = 500, NumPoliza = 0
            }, 3, "PC"));
    }

    [Fact]
    public async Task Los_Parametros_Del_Reporte_Se_Pegan_Con_Tilde_En_El_Servidor()
    {
        var repo = new FakeAviacionPolizasRepo();
        var service = new AviacionPolizasService(repo);

        await service.ConsultarIngresosAsync(new AviacionPolizaReporteFiltro
        {
            Parametros = new List<string> { "1", " 2026-05-01 ", "3" }
        });

        Assert.Equal("1~2026-05-01~3", repo.UltimosParametros);
    }

    [Fact]
    public async Task Un_Parametro_No_Puede_Traer_El_Delimitador()
    {
        // En Legacy la cadena venia preconcatenada del cliente: un "~" dentro de un
        // valor partia el reporte en parametros distintos de los que se pidieron.
        var service = new AviacionPolizasService(new FakeAviacionPolizasRepo());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ConsultarIngresosAsync(new AviacionPolizaReporteFiltro
            {
                Parametros = new List<string> { "1~2" }
            }));
    }

    // ==================================================================
    // Recaudaciones
    // ==================================================================

    [Fact]
    public async Task Pedir_Una_Recaudacion_Devuelve_Esa_Y_No_Todas()
    {
        /*
          EL PEOR DEFECTO DEL LOTE.
          Legacy: al venir IDRecaudacion con la lista vacia, reconstruia el DTO y
          dejaba IDsRecaudacion en cadena vacia. Se perdia el id pedido Y todos los
          demas filtros, y el SP devolvia TODAS las recaudaciones de la base.
        */
        var repo = new FakeAviacionRecaudacionesRepo();
        var service = new AviacionRecaudacionesService(repo);

        await service.ConsultarAsync(new AviacionRecaudacionesFiltro
        {
            IdRecaudacion = 777,
            IdCaja = 2,
            IdStatus = 1
        });

        Assert.Equal(777, repo.UltimoFiltro!.IdRecaudacion);
        Assert.Equal(2, repo.UltimoFiltro.IdCaja);
        Assert.Equal(1, repo.UltimoFiltro.IdStatus);
    }

    [Fact]
    public async Task El_Importe_Del_Cobro_Tiene_Que_Cuadrar_Con_Sus_Renglones()
    {
        // El SP guarda los tres numeros tal como llegan. Un total que no es el de
        // sus renglones descuadra el corte del dia, y la diferencia se le carga al
        // cajero.
        var service = new AviacionRecaudacionesService(new FakeAviacionRecaudacionesRepo());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CrearAsync(new CrearAviacionRecaudacionRequest
            {
                IdEconomico = 1,
                IdCaja = 1,
                IdsCuenta = new List<int> { 10, 11 },
                ImportesCuenta = new List<decimal> { 50m, 50m },
                ImporteCuentas = 120m,
                ImporteTotal = 120m
            }, 3, "CAJA1"));
    }

    [Fact]
    public async Task El_Cobro_Completa_NumCuentas_Si_No_Vino()
    {
        var repo = new FakeAviacionRecaudacionesRepo();
        var service = new AviacionRecaudacionesService(repo);

        await service.CrearAsync(new CrearAviacionRecaudacionRequest
        {
            IdEconomico = 1,
            IdCaja = 1,
            IdsCuenta = new List<int> { 10, 11, 12 },
            ImportesCuenta = new List<decimal> { 10m, 20m, 30m },
            ImporteCuentas = 60m,
            ImporteTotal = 60m
        }, 3, "CAJA1");

        Assert.Equal(3, repo.UltimoCobro!.NumCuentas);
    }

    [Fact]
    public async Task Un_Cobro_No_Es_El_Reverso_Y_El_Reversado_A_La_Vez()
    {
        var service = new AviacionRecaudacionesService(new FakeAviacionRecaudacionesRepo());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CrearAsync(new CrearAviacionRecaudacionRequest
            {
                IdEconomico = 1,
                IdCaja = 1,
                IdRecaudacionOriginal = 1,
                IdRecaudacionReverso = 2,
                IdsCuenta = new List<int> { 10 },
                ImportesCuenta = new List<decimal> { 10m },
                ImporteCuentas = 10m,
                ImporteTotal = 10m
            }, 3, "CAJA1"));
    }

    [Fact]
    public async Task Las_Listas_De_Ids_No_Admiten_El_Cero()
    {
        // Un 0 pegado en la lista queda como `IN (0)` dentro del SP y no cambia nada
        // visible. Es el mismo defecto que la ola 3 encontro en la lista de marcas.
        var service = new AviacionRecaudacionesService(new FakeAviacionRecaudacionesRepo());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ConsultarAsync(new AviacionRecaudacionesFiltro { IdsCaja = new List<int> { 1, 0, 3 } }));
    }

    [Fact]
    public async Task No_Se_Cierra_Un_Dia_Que_No_Ha_Pasado()
    {
        var service = new AviacionRecaudacionesService(new FakeAviacionRecaudacionesRepo());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CrearCorteAsync(new CrearAviacionRecaudacionCorteRequest
            {
                FechaCorte = DateOnly.FromDateTime(DateTime.Today.AddDays(1))
            }, 3, "CAJA1"));
    }

    // ------------------------------------------------------------------
    // Ayudas
    // ------------------------------------------------------------------

    private static CrearAviacionCuentaRequest CuentaValida(Action<CrearAviacionCuentaRequest>? ajuste = null)
    {
        var c = new CrearAviacionCuentaRequest
        {
            IdConcepto = 1,
            NumCuenta = "001",
            NombreCuenta = "PEAJE NORTE",
            DebeHaber = "D"
        };
        ajuste?.Invoke(c);
        return c;
    }

    private static ActualizarAviacionCuentaRequest CuentaValidaActualizar()
        => new()
        {
            IdConcepto = 1,
            NumCuenta = "001",
            NombreCuenta = "PEAJE NORTE",
            DebeHaber = "D",
            IdStatus = 1
        };

    private static CrearAviacionPolizaRequest PolizaValida(Action<CrearAviacionPolizaRequest>? ajuste = null)
    {
        var p = new CrearAviacionPolizaRequest
        {
            IdPolizaEncabezado = 1,
            FechaInicial = new DateOnly(2026, 5, 1),
            FechaFinal = new DateOnly(2026, 5, 15),
            NumChequeInicial = 500,
            NumPolizaInicial = 100
        };
        ajuste?.Invoke(p);
        return p;
    }
}

// ======================================================================
// Dobles
// ======================================================================

internal class FakeConexionDeAviacion : IConexionDeAviacion
{
    public bool EsCaja { get; set; }
    public string CadenaCentral { get; set; } = "Server=.;Database=MacAviacion;";

    public Task<ConfiguracionDeAviacion> LeerAsync(CancellationToken ct = default)
        => Task.FromResult(new ConfiguracionDeAviacion(CadenaCentral, EsCaja ? 7 : 0));

    public Task<SqlConnection> AbrirAsync(bool central, CancellationToken ct = default)
        => throw new NotImplementedException("Las pruebas de reglas no abren conexiones.");

    public Task<SqlConnection> AbrirLocalAsync(CancellationToken ct = default)
        => throw new NotImplementedException("Las pruebas de reglas no abren conexiones.");
}

internal class FakeAviacionCatalogosRepo : IAviacionCatalogosRepository
{
    public ResultadoSp Resultado { get; set; } = new(11, string.Empty);
    public ResultadoSp ResultadoSincronizacion { get; set; } = new(1, string.Empty);
    public List<int> TablasSincronizadas { get; } = new();

    private Task<ResultadoSp> Ok() => Task.FromResult(Resultado);
    private static Task<List<Dictionary<string, object?>>> Vacio() => Task.FromResult(new List<Dictionary<string, object?>>());

    public Task<ResultadoSp> SincronizarDescargaAsync(int idTabla, int idUsuario, string equipo, CancellationToken ct = default)
    {
        TablasSincronizadas.Add(idTabla);
        return Task.FromResult(ResultadoSincronizacion);
    }

    public Task<ResultadoSp> ActualizarCajaAsync(int idCaja, ActualizarAviacionCajaRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();

    public Task<List<Dictionary<string, object?>>> ConsultarConceptosAsync(AviacionConceptosFiltro filtro, CancellationToken ct = default) => Vacio();
    public Task<List<Dictionary<string, object?>>> ConsultarConceptoSiguienteCuentaAsync(int idConcepto, CancellationToken ct = default) => Vacio();
    public Task<ResultadoSp> InsertarConceptoAsync(CrearAviacionConceptoRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ActualizarConceptoAsync(int idConcepto, ActualizarAviacionConceptoRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();

    public Task<List<Dictionary<string, object?>>> ConsultarCuentasAsync(AviacionCuentasFiltro filtro, bool central, CancellationToken ct = default) => Vacio();
    public Task<List<Dictionary<string, object?>>> ConsultarCuentaSiguienteNumeroAsync(int idCuenta, CancellationToken ct = default) => Vacio();
    public Task<ResultadoSp> InsertarCuentaAsync(CrearAviacionCuentaRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ActualizarCuentaAsync(int idCuenta, ActualizarAviacionCuentaRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> AceptarCuentaAsync(int idCuenta, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<List<Dictionary<string, object?>>> ConsultarTarjetaPorcentajesAsync(int idCuenta, int idStatus, CancellationToken ct = default) => Vacio();
    public Task<ResultadoSp> ActualizarTarjetaPorcentajesAsync(ActualizarAviacionTarjetaPorcentajesRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();

    public Task<List<Dictionary<string, object?>>> ConsultarEconomicosAsync(AviacionEconomicosFiltro filtro, bool central, CancellationToken ct = default) => Vacio();
    public Task<List<Dictionary<string, object?>>> ConsultarEconomicoSiguienteIdAsync(int idPermisionario, CancellationToken ct = default) => Vacio();
    public Task<List<Dictionary<string, object?>>> ConsultarEconomicoSiguienteNumeroCuentaAsync(int idPermisionario, CancellationToken ct = default) => Vacio();
    public Task<List<Dictionary<string, object?>>> ConsultarEconomicosAuxiliaresAsync(AviacionEconomicosAuxiliaresFiltro filtro, CancellationToken ct = default) => Vacio();
    public Task<ResultadoSp> InsertarEconomicoAsync(CrearAviacionEconomicoRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ActualizarEconomicoAsync(int idEconomico, ActualizarAviacionEconomicoRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ActualizarEconomicoAmortizacionAsync(int idEconomico, ActualizarAviacionEconomicoAmortizacionRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ReasignarEconomicoAsync(int idEconomico, ReasignarAviacionEconomicoRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> AceptarEconomicoAsync(int idEconomico, int idUsuario, string equipo, CancellationToken ct = default) => Ok();

    public Task<List<Dictionary<string, object?>>> ConsultarMotivosFianzaAsync(AviacionMotivosFianzaFiltro filtro, bool central, CancellationToken ct = default) => Vacio();
    public Task<List<Dictionary<string, object?>>> ConsultarMotivoFianzaSiguienteIdAsync(int idMotivoFianza, CancellationToken ct = default) => Vacio();
    public Task<ResultadoSp> InsertarMotivoFianzaAsync(CrearAviacionMotivoFianzaRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ActualizarMotivoFianzaAsync(int idMotivoFianza, ActualizarAviacionMotivoFianzaRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();

    public Task<List<Dictionary<string, object?>>> ConsultarOperadoresAsync(AviacionOperadoresFiltro filtro, bool central, CancellationToken ct = default) => Vacio();
    public Task<List<Dictionary<string, object?>>> ConsultarOperadorSiguienteNumeroCuentaAsync(int idOperador, CancellationToken ct = default) => Vacio();
    public Task<ResultadoSp> InsertarOperadorAsync(CrearAviacionOperadorRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ActualizarOperadorAsync(int idOperador, ActualizarAviacionOperadorRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> AceptarOperadorAsync(int idOperador, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ActualizarOperadorFianzaAsync(int idOperador, decimal importeFianza, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ActualizarOperadorCuentaAsync(int idOperador, ActualizarAviacionOperadorCuentaRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();

    public Task<List<Dictionary<string, object?>>> ConsultarPermisionariosAsync(AviacionPermisionariosFiltro filtro, bool central, CancellationToken ct = default) => Vacio();
    public Task<List<Dictionary<string, object?>>> ConsultarPermisionarioSiguienteNumeroCuentaAsync(CancellationToken ct = default) => Vacio();
    public Task<ResultadoSp> InsertarPermisionarioAsync(CrearAviacionPermisionarioRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ActualizarPermisionarioAsync(int idPermisionario, ActualizarAviacionPermisionarioRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> AceptarPermisionarioAsync(int idPermisionario, int idUsuario, string equipo, CancellationToken ct = default) => Ok();

    public Task<List<Dictionary<string, object?>>> ConsultarImpresorasAsync(AviacionImpresorasFiltro filtro, CancellationToken ct = default) => Vacio();
    public Task<ResultadoSp> ActualizarImpresoraAsync(ActualizarAviacionImpresoraRequest request, int idUsuario, CancellationToken ct = default) => Ok();
}

internal class FakeAviacionPolizasRepo : IAviacionPolizasRepository
{
    public ResultadoSp Resultado { get; set; } = new(11, string.Empty);
    public string? UltimosParametros { get; private set; }

    private Task<ResultadoSp> Ok() => Task.FromResult(Resultado);
    private static Task<List<Dictionary<string, object?>>> Vacio() => Task.FromResult(new List<Dictionary<string, object?>>());

    public Task<List<Dictionary<string, object?>>> ConsultarAsync(AviacionPolizasFiltro filtro, CancellationToken ct = default) => Vacio();
    public Task<List<Dictionary<string, object?>>> ConsultarDetalleAsync(int idPoliza, CancellationToken ct = default) => Vacio();
    public Task<List<Dictionary<string, object?>>> ConsultarChequesAsync(int idPoliza, CancellationToken ct = default) => Vacio();
    public Task<List<Dictionary<string, object?>>> ConsultarEncabezadosAsync(int idPolizaEncabezado, int idStatus, CancellationToken ct = default) => Vacio();

    public Task<List<Dictionary<string, object?>>> ConsultarIngresosAsync(string parametros, CancellationToken ct = default)
    {
        UltimosParametros = parametros;
        return Vacio();
    }

    public Task<List<Dictionary<string, object?>>> ConsultarFianzasAsync(string parametros, CancellationToken ct = default)
    {
        UltimosParametros = parametros;
        return Vacio();
    }

    public Task<List<Dictionary<string, object?>>> ConsultarDocumentosAsync(string parametros, CancellationToken ct = default)
    {
        UltimosParametros = parametros;
        return Vacio();
    }

    public Task<ResultadoSp> InsertarAsync(CrearAviacionPolizaRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> CancelarAsync(int idPoliza, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ActualizarEncabezadoAsync(ActualizarAviacionPolizaEncabezadoRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ActualizarChequeAsync(int idPolizaCheque, ActualizarAviacionPolizaChequeRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
}

internal class FakeAviacionRecaudacionesRepo : IAviacionRecaudacionesRepository
{
    public ResultadoSp Resultado { get; set; } = new(11, string.Empty);
    public AviacionRecaudacionesFiltro? UltimoFiltro { get; private set; }
    public CrearAviacionRecaudacionRequest? UltimoCobro { get; private set; }

    private Task<ResultadoSp> Ok() => Task.FromResult(Resultado);
    private static Task<List<Dictionary<string, object?>>> Vacio() => Task.FromResult(new List<Dictionary<string, object?>>());

    public Task<List<Dictionary<string, object?>>> ConsultarAsync(AviacionRecaudacionesFiltro filtro, CancellationToken ct = default)
    {
        UltimoFiltro = filtro;
        return Vacio();
    }

    public Task<List<Dictionary<string, object?>>> ConsultarRecurrentesAsync(AviacionRecaudacionesRecurrentesFiltro filtro, CancellationToken ct = default) => Vacio();
    public Task<List<Dictionary<string, object?>>> ConsultarCortesAsync(AviacionRecaudacionesCortesFiltro filtro, CancellationToken ct = default) => Vacio();
    public Task<List<Dictionary<string, object?>>> ConsultarSiguienteFolioAsync(int idCaja, CancellationToken ct = default) => Vacio();

    public Task<ResultadoSp> InsertarAsync(CrearAviacionRecaudacionRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        UltimoCobro = request;
        return Ok();
    }

    public Task<ResultadoSp> CancelarAsync(int idRecaudacion, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> InsertarCorteAsync(CrearAviacionRecaudacionCorteRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
}
