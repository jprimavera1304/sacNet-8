using ISL_Service.Application.DTOs.Cobranzas;
using ISL_Service.Application.DTOs.Gasolinas;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Services;
using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Tests;

/*
  COBRANZAS Y GASOLINAS: LAS REGLAS QUE LEGACY NO TENIA.

  Estos dos modulos SI son comparables en Produccion_svr (los siete sp_n_ de
  cobranzas y los cuatro de gasolinas estan instalados con la firma que espera
  Legacy), asi que lo de aqui son las reglas nuevas.
*/
public class Ola5CobranzasTests
{
    [Fact]
    public async Task Los_Filtros_De_Cobranza_No_Se_Tiran_Cuando_Viene_El_Folio()
    {
        /*
          Legacy tenia dos ramas:
              if  (FolioInicialCobranza != "0") { reconstruye con el folio de cobranza }
              else if (FolioInicialCobranza != "0") { reconstruye con el folio de venta }
          La segunda condicion es la primera repetida —copiar y pegar sin cambiar el
          campo—, asi que NUNCA se ejecuta. Y la primera perdia @IDUsuarioActual, que
          es lo que el SP usa para saber que cobranzas puede ver quien pregunta.
        */
        var repo = new FakeCobranzasRepo();
        var service = new CobranzasService(repo);

        await service.ConsultarAsync(new CobranzasFiltro
        {
            FolioInicialCobranza = 10,
            FolioFinalCobranza = 20,
            IdRepartidor = 7
        }, idUsuarioActual: 3);

        Assert.Equal(10, repo.UltimoFiltro!.FolioInicialCobranza);
        Assert.Equal(7, repo.UltimoFiltro.IdRepartidor);
        Assert.Equal(3, repo.UltimoUsuarioActual);
    }

    [Fact]
    public async Task Buscar_Por_Folio_De_Venta_Necesita_La_Empresa()
    {
        // El folio de venta se repite entre empresas: el SP compara IDEmpresa+Folio.
        var service = new CobranzasService(new FakeCobranzasRepo());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ConsultarAsync(new CobranzasFiltro { FolioInicialVenta = 100 }, 3));
    }

    [Fact]
    public async Task Armar_Una_Cobranza_Necesita_Repartidor_Y_Ventas()
    {
        var service = new CobranzasService(new FakeCobranzasRepo());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CrearAsync(new CrearCobranzaRequest { IdRepartidor = 0, IdsVenta = new List<int> { 1 } }, 3, "PC"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CrearAsync(new CrearCobranzaRequest { IdRepartidor = 1, IdsVenta = new List<int>() }, 3, "PC"));
    }

    [Fact]
    public async Task Si_La_Cobranza_Es_Parte_De_Un_Cobro_El_Cobro_Tiene_Que_Venir()
    {
        // Con esCobro = 1 e IDCobro = 0 quedan dos renglones que se buscan y no se
        // encuentran.
        var service = new CobranzasService(new FakeCobranzasRepo());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CrearAsync(new CrearCobranzaRequest
            {
                IdRepartidor = 1, IdsVenta = new List<int> { 5 }, EsCobro = true, IdCobro = 0
            }, 3, "PC"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CrearAsync(new CrearCobranzaRequest
            {
                IdRepartidor = 1, IdsVenta = new List<int> { 5 }, EsCobro = false, IdCobro = 9
            }, 3, "PC"));
    }

    [Fact]
    public async Task Cambiar_Una_Cobranza_Sin_Pedir_Nada_No_Se_Manda()
    {
        // Con las tres listas vacias el SP corre su transaccion entera, reescribe el
        // repartidor y deja bitacora, para nada. Era un camino facil de pisar el
        // repartidor de una cobranza sin querer.
        var service = new CobranzasService(new FakeCobranzasRepo());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ActualizarDetalleAsync(1, new ActualizarCobranzaDetalleRequest { IdRepartidor = 7 }, 3, "PC"));
    }

    [Fact]
    public async Task Un_Renglon_No_Se_Quita_Y_Se_Devuelve_A_La_Vez()
    {
        // Son dos destinos distintos; el SP procesa las dos listas una tras otra y el
        // resultado depende de cual es la segunda.
        var service = new CobranzasService(new FakeCobranzasRepo());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ActualizarDetalleAsync(1, new ActualizarCobranzaDetalleRequest
            {
                IdRepartidor = 7,
                IdsDetalleEliminar = new List<int> { 10, 11 },
                IdsDetalleDevolver = new List<int> { 11 }
            }, 3, "PC"));
    }

    [Fact]
    public async Task Cambiar_Una_Cobranza_Con_Una_Sola_Peticion_Pasa()
    {
        var service = new CobranzasService(new FakeCobranzasRepo());

        var r = await service.ActualizarDetalleAsync(1, new ActualizarCobranzaDetalleRequest
        {
            IdRepartidor = 7,
            IdsVentaAgregar = new List<int> { 99 }
        }, 3, "PC");

        Assert.Equal(1, r.Id);
    }

    [Fact]
    public async Task La_Fecha_De_Operacion_Del_Reporte_Sale_De_La_Base()
    {
        /*
          En Legacy la fecha de operacion viajaba en el cuerpo
          (CobranzaPendientesVencidosInputDTO.FechaOperacionFtm): quien pidiera el
          reporte podia mandar la fecha que quisiera y hacer que una venta vencida
          saliera como pendiente.
        */
        var repo = new FakeCobranzasRepo { FechaOperacion = new DateOnly(2026, 10, 6) };
        var service = new CobranzasService(repo);

        await service.ConsultarPendientesVencidosAsync(new CobranzaPendientesVencidosFiltro
        {
            FechaInicial = new DateOnly(2026, 1, 1),
            FechaFinal = new DateOnly(2026, 10, 6)
        });

        Assert.Equal(new DateOnly(2026, 10, 6), repo.FechaOperacionUsada);
    }

    [Fact]
    public async Task El_Reporte_Pendientes_Vencidos_Exige_Las_Dos_Fechas()
    {
        var service = new CobranzasService(new FakeCobranzasRepo());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ConsultarPendientesVencidosAsync(new CobranzaPendientesVencidosFiltro
            {
                FechaInicial = new DateOnly(2026, 1, 1)
            }));
    }

    [Fact]
    public async Task Cancelar_Cobranzas_Exige_Al_Menos_Una()
    {
        var service = new CobranzasService(new FakeCobranzasRepo());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CancelarAsync(new CancelarCobranzasRequest(), 3, "PC"));
    }
}

public class Ola5GasolinasTests
{
    [Fact]
    public async Task Los_Filtros_De_Gasolina_No_Se_Tiran_Cuando_Viene_El_Recibo()
    {
        // Legacy reconstruia el DTO con solo el numero de recibo: buscar "el recibo
        // 12345 de este repartidor" devolvia el recibo 12345 de cualquiera.
        var repo = new FakeGasolinasRepo();
        var service = new GasolinasService(repo);

        await service.ConsultarAsync(new GasolinasFiltro
        {
            NumeroRecibo = "12345",
            IdRepartidor = 8,
            IdAuto = 4
        });

        Assert.Equal("12345", repo.UltimoFiltro!.NumeroRecibo);
        Assert.Equal(8, repo.UltimoFiltro.IdRepartidor);
        Assert.Equal(4, repo.UltimoFiltro.IdAuto);
    }

    [Fact]
    public async Task Un_Recibo_De_Gasolina_No_Puede_Ser_Del_Futuro()
    {
        var service = new GasolinasService(new FakeGasolinasRepo());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CrearAsync(new CrearGasolinaRequest
            {
                IdAuto = 1,
                IdRepartidor = 1,
                NumeroRecibo = "1",
                FechaRecibo = DateOnly.FromDateTime(DateTime.Today.AddDays(1))
            }, 3, "PC"));
    }

    [Fact]
    public async Task Las_Dos_Listas_De_La_Cancelacion_Tienen_Que_Medir_Lo_Mismo()
    {
        /*
          El SP recorre @IDsGasolina y @NumerosRecibo a la vez para dejar el numero de
          recibo en la bitacora de cancelacion. Con una lista mas corta, los ultimos
          recibos se cancelan con el numero de otro, y la bitacora —que es lo que se
          revisa cuando falta dinero— queda mintiendo.
        */
        var service = new GasolinasService(new FakeGasolinasRepo());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CancelarAsync(new CancelarGasolinasRequest
            {
                IdsGasolina = new List<int> { 1, 2, 3 },
                NumerosRecibo = new List<string> { "A", "B" }
            }, 3, "PC"));
    }

    [Fact]
    public async Task Cancelar_Recibos_Con_Las_Dos_Listas_Parejas_Pasa()
    {
        var service = new GasolinasService(new FakeGasolinasRepo());

        var r = await service.CancelarAsync(new CancelarGasolinasRequest
        {
            IdsGasolina = new List<int> { 1, 2 },
            NumerosRecibo = new List<string> { "99990001", "99990002" }
        }, 3, "PC");

        // La respuesta es un renglon por recibo, no un si/no: el SP contesta asi.
        Assert.Equal(2, r.Total);
        Assert.Equal("EL RECIBO 99990001 HA SIDO CANCELADO.", r.Recibos[0]["resultado"]);
    }

    [Fact]
    public async Task Un_Numero_De_Recibo_Con_Letras_Se_Rechaza_Con_400_Y_No_Con_Un_Error_De_Sql()
    {
        /*
          La columna es varchar(100) y el parametro del SP tambien, pero
          sp_n_CancelarGasolina mete los numeros en una tabla temporal declarada
          `#TableNumeroRecibo ( NumeroRecibo int )`. Con una letra, SQL Server
          contesta "Conversion failed when converting the varchar value ... to data
          type int" y el usuario ve un error de base de datos en vez de saber que
          el problema es su numero de recibo. Comprobado contra Produccion_svr: el
          500 sale de verdad.
        */
        var service = new GasolinasService(new FakeGasolinasRepo());

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CancelarAsync(new CancelarGasolinasRequest
            {
                IdsGasolina = new List<int> { 1 },
                NumerosRecibo = new List<string> { "OLA5-0003" }
            }, 3, "PC"));

        Assert.Contains("digitos", ex.Message);
    }

    [Fact]
    public async Task El_Rango_De_Fechas_Tiene_Que_Ser_Coherente()
    {
        var service = new GasolinasService(new FakeGasolinasRepo());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ConsultarAsync(new GasolinasFiltro
            {
                FechaInicial = new DateOnly(2026, 5, 10),
                FechaFinal = new DateOnly(2026, 5, 1)
            }));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ConsultarAsync(new GasolinasFiltro { FechaFinal = new DateOnly(2026, 5, 1) }));
    }
}

// ======================================================================
// Dobles
// ======================================================================

internal class FakeCobranzasRepo : ICobranzasRepository
{
    public ResultadoSp Resultado { get; set; } = new(1, string.Empty);
    public DateOnly FechaOperacion { get; set; } = new(2026, 10, 6);

    public CobranzasFiltro? UltimoFiltro { get; private set; }
    public int UltimoUsuarioActual { get; private set; }
    public DateOnly? FechaOperacionUsada { get; private set; }

    private Task<ResultadoSp> Ok() => Task.FromResult(Resultado);
    private static Task<List<Dictionary<string, object?>>> Vacio() => Task.FromResult(new List<Dictionary<string, object?>>());

    public Task<List<Dictionary<string, object?>>> ConsultarAsync(CobranzasFiltro filtro, int idUsuarioActual, CancellationToken ct = default)
    {
        UltimoFiltro = filtro;
        UltimoUsuarioActual = idUsuarioActual;
        return Vacio();
    }

    public Task<List<Dictionary<string, object?>>> ConsultarDetalleAsync(CobranzaDetalleFiltro filtro, CancellationToken ct = default) => Vacio();
    public Task<List<Dictionary<string, object?>>> ValidarFolioAsync(CobranzaValidarFolioFiltro filtro, int idUsuarioActual, CancellationToken ct = default) => Vacio();

    public Task<ResultadoSp> InsertarAsync(CrearCobranzaRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ActualizarDetalleAsync(int idCobranza, ActualizarCobranzaDetalleRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> CancelarAsync(CancelarCobranzasRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();

    public Task<DateOnly> LeerFechaDeOperacionAsync(CancellationToken ct = default) => Task.FromResult(FechaOperacion);

    public Task<List<Dictionary<string, object?>>> ConsultarPendientesVencidosAsync(CobranzaPendientesVencidosFiltro filtro, DateOnly fechaOperacion, CancellationToken ct = default)
    {
        FechaOperacionUsada = fechaOperacion;
        return Vacio();
    }
}

internal class FakeGasolinasRepo : IGasolinasRepository
{
    public ResultadoSp Resultado { get; set; } = new(1, string.Empty);
    public GasolinasFiltro? UltimoFiltro { get; private set; }

    private Task<ResultadoSp> Ok() => Task.FromResult(Resultado);

    public Task<List<Dictionary<string, object?>>> ConsultarAsync(GasolinasFiltro filtro, CancellationToken ct = default)
    {
        UltimoFiltro = filtro;
        return Task.FromResult(new List<Dictionary<string, object?>>());
    }

    public Task<ResultadoSp> InsertarAsync(CrearGasolinaRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<ResultadoSp> ActualizarAsync(int idGasolina, ActualizarGasolinaRequest request, int idUsuario, string equipo, CancellationToken ct = default) => Ok();
    public Task<List<Dictionary<string, object?>>> CancelarAsync(CancelarGasolinasRequest request, int idUsuario, string equipo, CancellationToken ct = default)
        => Task.FromResult(request.NumerosRecibo.Select((n, i) => new Dictionary<string, object?>
        {
            ["idGasolina"] = request.IdsGasolina[i],
            ["numeroRecibo"] = int.Parse(n),
            ["resultado"] = $"EL RECIBO {n} HA SIDO CANCELADO."
        }).ToList());
}
