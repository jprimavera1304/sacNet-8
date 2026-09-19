using ISL_Service.Application.DTOs.VentasSaldos;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

public class VentasSaldosService : IVentasSaldosService
{
    private readonly IVentasSaldosRepository _repository;

    public VentasSaldosService(IVentasSaldosRepository repository)
    {
        _repository = repository;
    }

    public Task<VentasSaldosResponse> ConsultarSaldosClienteAsync(VentasSaldosRequest request, CancellationToken ct)
    {
        var idCliente = request?.IdCliente ?? 0;

        /*
          Sin cliente no hay consulta que hacer. Se devuelve vacio en vez de
          lanzar: la pantalla pide esto cada vez que se mueve de renglon, y un
          error por un renglon sin cliente seria un aviso rojo por nada.
        */
        if (idCliente <= 0)
            return Task.FromResult(new VentasSaldosResponse());

        return _repository.ConsultarSaldosClienteAsync(idCliente, ct);
    }
}
