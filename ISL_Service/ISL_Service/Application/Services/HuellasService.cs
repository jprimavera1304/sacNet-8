using ISL_Service.Application.DTOs.Huellas;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Repositories;

namespace ISL_Service.Application.Services;

/// <summary>
/// Traduce "quiero todas" al valor magico que entiende el SP, y rechaza la
/// combinacion que en legacy devolvia un 500.
/// </summary>
public class HuellasService : IHuellasService
{
    private readonly IHuellasRepository _repository;

    public HuellasService(IHuellasRepository repository)
    {
        _repository = repository;
    }

    public Task<List<HuellaDto>> ConsultarAsync(HuellaOrigen origen, int id, bool todas, CancellationToken ct)
    {
        // El -1 no viaja por HTTP: el cliente dice `todas=true` y la traduccion
        // al valor magico se hace aqui. Si el -1 viajara, un `id=-1` por error de
        // un front se volveria "dame todas las huellas de la empresa" sin que
        // nadie lo hubiera pedido.
        if (todas)
            return _repository.ConsultarAsync(origen, HuellasRepository.TodasLasHuellas, ct);

        if (id <= 0)
            throw new ArgumentException("Falta el id, o pide todas=true.", nameof(id));

        return _repository.ConsultarAsync(origen, id, ct);
    }
}
