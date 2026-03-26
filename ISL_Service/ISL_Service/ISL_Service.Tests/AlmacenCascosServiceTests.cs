using ISL_Service.Application.DTOs.AlmacenCascos;
using ISL_Service.Application.DTOs.Catalogos;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Services;

namespace ISL_Service.Tests;

public class AlmacenCascosServiceTests
{
    [Fact]
    public async Task GuardarKilosTarimaAsync_Inserta_Y_Recalcula()
    {
        var repo = new FakeAlmacenCascosRepository
        {
            Movimiento = new MovimientoCascoDto { IdMovimiento = 10, TipoMovimiento = 2, Estatus = 1 },
            ExisteTarima = true,
            UpsertResult = new MovimientoCascoTarimaKilosResultDto
            {
                IdMovimiento = 10,
                NumeroTarima = 1,
                Kilos = 12.3456m,
                TotalKilos = 55.1234m,
                TotalTarimasConKilos = 3
            }
        };
        var service = new AlmacenCascosService(repo, new FakeCatalogosRepository());

        var result = await service.GuardarKilosTarimaAsync(10, 1, 12.3456m, "tester");

        Assert.Equal(10, result.IdMovimiento);
        Assert.Equal(1, result.NumeroTarima);
        Assert.Equal(55.1234m, result.TotalKilos);
        Assert.Equal(3, result.TotalTarimasConKilos);
        Assert.Equal(1, repo.UpsertCallCount);
    }

    [Fact]
    public async Task GuardarKilosTarimaAsync_Actualiza_Kilos()
    {
        var repo = new FakeAlmacenCascosRepository
        {
            Movimiento = new MovimientoCascoDto { IdMovimiento = 10, TipoMovimiento = 2, Estatus = 1 },
            ExisteTarima = true,
            UpsertResult = new MovimientoCascoTarimaKilosResultDto
            {
                IdMovimiento = 10,
                NumeroTarima = 1,
                Kilos = 20m,
                TotalKilos = 60m,
                TotalTarimasConKilos = 3
            }
        };
        var service = new AlmacenCascosService(repo, new FakeCatalogosRepository());

        var result = await service.GuardarKilosTarimaAsync(10, 1, 20m, "tester");

        Assert.Equal(20m, result.Kilos);
        Assert.Equal(1, repo.UpsertCallCount);
        Assert.Equal(20m, repo.LastUpsertKilos);
    }

    [Fact]
    public async Task GuardarKilosTarimaAsync_Rechaza_Salidas()
    {
        var repo = new FakeAlmacenCascosRepository
        {
            Movimiento = new MovimientoCascoDto { IdMovimiento = 10, TipoMovimiento = 1, Estatus = 1 },
            ExisteTarima = true
        };
        var service = new AlmacenCascosService(repo, new FakeCatalogosRepository());

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.GuardarKilosTarimaAsync(10, 1, 1m, "tester"));
        Assert.Contains("solo aplican a movimientos de entrada", ex.Message);
    }

    [Fact]
    public async Task GuardarKilosTarimaAsync_Rechaza_Cancelados()
    {
        var repo = new FakeAlmacenCascosRepository
        {
            Movimiento = new MovimientoCascoDto { IdMovimiento = 10, TipoMovimiento = 2, Estatus = 3 },
            ExisteTarima = true
        };
        var service = new AlmacenCascosService(repo, new FakeCatalogosRepository());

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.GuardarKilosTarimaAsync(10, 1, 1m, "tester"));
        Assert.Contains("movimiento cancelado", ex.Message);
    }

    [Fact]
    public async Task GuardarKilosTarimaAsync_Rechaza_Tarima_Inexistente()
    {
        var repo = new FakeAlmacenCascosRepository
        {
            Movimiento = new MovimientoCascoDto { IdMovimiento = 10, TipoMovimiento = 2, Estatus = 1 },
            ExisteTarima = false
        };
        var service = new AlmacenCascosService(repo, new FakeCatalogosRepository());

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.GuardarKilosTarimaAsync(10, 99, 1m, "tester"));
        Assert.Contains("no existe en el detalle", ex.Message);
    }

    private sealed class FakeAlmacenCascosRepository : IAlmacenCascosRepository
    {
        public MovimientoCascoDto? Movimiento { get; set; }
        public bool ExisteTarima { get; set; }
        public MovimientoCascoTarimaKilosResultDto UpsertResult { get; set; } = new();
        public int UpsertCallCount { get; private set; }
        public decimal LastUpsertKilos { get; private set; }

        public Task<List<MovimientoCascoDto>> ConsultarMovimientosAsync(int? estatus, int? tipoMovimiento, DateTime? fechaInicio, DateTime? fechaFin, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<List<MovimientoCascoDetalleDto>> GetDetalleMovimientoAsync(int idMovimiento, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<MovimientoCascoDto?> GetMovimientoAsync(int idMovimiento, CancellationToken ct = default)
            => Task.FromResult(Movimiento);

        public Task<bool> ExisteNumeroTarimaDetalleAsync(int idMovimiento, int numeroTarima, CancellationToken ct = default)
            => Task.FromResult(ExisteTarima);

        public Task<int> InsertarSalidaAsync(CreateSalidaRequest request, string usuario, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task AceptarEntradaAsync(CreateEntradaRequest request, string usuario, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task ActualizarSalidaAsync(int idMovimiento, UpdateSalidaRequest request, string usuario, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task ActualizarEntradaAsync(int idMovimiento, UpdateEntradaRequest request, string usuario, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<MovimientoCascoTarimaKilosResultDto> UpsertMovimientoTarimaKilosAsync(int idMovimiento, int numeroTarima, decimal kilos, string usuario, CancellationToken ct = default)
        {
            UpsertCallCount++;
            LastUpsertKilos = kilos;
            return Task.FromResult(UpsertResult);
        }

        public Task<List<MovimientoCascoTarimaKilosDto>> GetMovimientoTarimasKilosAsync(int idMovimiento, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task CancelarMovimientoAsync(int idMovimiento, string motivoCancelacion, string usuario, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    private sealed class FakeCatalogosRepository : ICatalogosRepository
    {
        public Task<List<TipoCascoItemDto>> ListTiposCascoAsync(int? idStatus, CancellationToken ct = default)
            => Task.FromResult(new List<TipoCascoItemDto>());

        public Task<List<RepartidorItemDto>> ListRepartidoresAsync(int? idStatus, CancellationToken ct = default)
            => Task.FromResult(new List<RepartidorItemDto>());

        public Task<List<TarimaCatalogItemDto>> ListTarimasAsync(int? idStatus, CancellationToken ct = default)
            => Task.FromResult(new List<TarimaCatalogItemDto>());

        public Task<List<ClienteCatalogItemDto>> ListClientesAsync(int? idStatus, CancellationToken ct = default)
            => Task.FromResult(new List<ClienteCatalogItemDto>());

        public Task<List<BancoCatalogItemDto>> ListBancosAsync(int? idStatus, CancellationToken ct = default)
            => Task.FromResult(new List<BancoCatalogItemDto>());
    }
}
