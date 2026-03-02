using ISL_Service.Application.DTOs.Tarima;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Services;

namespace ISL_Service.Tests;

public class TarimaServiceTests
{
    [Fact]
    public async Task ConsultarTarimasAsync_Devuelve_Lista_Del_Repository()
    {
        var repo = new FakeTarimaRepository
        {
            ConsultarResult = new List<TarimaDto>
            {
                new() { IdTarima = 1, NombreTarima = "T1", IdTipoCasco = 1, NumeroCascosBase = 10, IdStatus = 1 }
            }
        };
        var service = new TarimaService(repo);
        var list = await service.ConsultarTarimasAsync(null, null);
        Assert.Single(list);
        Assert.Equal(1, list[0].IdTarima);
        Assert.Equal("T1", list[0].NombreTarima);
    }

    [Fact]
    public async Task GetByIdAsync_Cuando_No_Existe_Devuelve_Null()
    {
        var repo = new FakeTarimaRepository { ConsultarResult = new List<TarimaDto>() };
        var service = new TarimaService(repo);
        var result = await service.GetByIdAsync(999);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_Cuando_Existe_Devuelve_Tarima()
    {
        var repo = new FakeTarimaRepository
        {
            ConsultarResult = new List<TarimaDto> { new() { IdTarima = 5, NombreTarima = "T5", IdStatus = 1 } }
        };
        var service = new TarimaService(repo);
        var result = await service.GetByIdAsync(5);
        Assert.NotNull(result);
        Assert.Equal(5, result.IdTarima);
    }

    [Fact]
    public async Task CambiarStatusAsync_Si_Ya_Esta_En_Ese_Status_No_Lanza()
    {
        var repo = new FakeTarimaRepository
        {
            ConsultarResult = new List<TarimaDto> { new() { IdTarima = 1, IdStatus = 2 } }
        };
        var service = new TarimaService(repo);
        await service.CambiarStatusAsync(1, 2, null, CancellationToken.None);
        Assert.Equal(0, repo.CambiarStatusCallCount);
    }

    [Fact]
    public async Task CambiarStatusAsync_Si_IdStatus_Invalido_Lanza_ArgumentException()
    {
        var repo = new FakeTarimaRepository { ConsultarResult = new List<TarimaDto>() };
        var service = new TarimaService(repo);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CambiarStatusAsync(1, 99, null, CancellationToken.None));
    }

    [Fact]
    public async Task CambiarStatusAsync_Si_No_Existe_Lanza_NotFoundException()
    {
        var repo = new FakeTarimaRepository { ConsultarResult = new List<TarimaDto>() };
        var service = new TarimaService(repo);
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => service.CambiarStatusAsync(999, 2, null, CancellationToken.None));
        Assert.Contains("no existe", ex.Message);
    }

    private sealed class FakeTarimaRepository : ITarimaRepository
    {
        public List<TarimaDto> ConsultarResult { get; set; } = new();
        public int InsertarReturns { get; set; } = 1;
        public int CambiarStatusCallCount { get; private set; }

        public Task<List<TarimaDto>> ConsultarTarimasAsync(int? idStatus, string? busqueda, CancellationToken ct = default)
            => Task.FromResult(ConsultarResult);

        public Task<int> InsertarTarimaAsync(CreateTarimaRequest request, string usuarioCreacion, CancellationToken ct = default)
            => Task.FromResult(InsertarReturns);

        public Task ActualizarTarimaAsync(int idTarima, UpdateTarimaRequest request, string usuarioModificacion, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task CambiarStatusTarimaAsync(int idTarima, int idStatus, string? usuarioModificacion, CancellationToken ct = default)
        {
            CambiarStatusCallCount++;
            return Task.CompletedTask;
        }
    }
}
