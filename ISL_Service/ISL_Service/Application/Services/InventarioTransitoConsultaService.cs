using ISL_Service.Application.DTOs.InventarioTransito;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/// <summary>
/// Normaliza los filtros del inventario en transito.
///
/// No hay texto que sanear aqui (el SP solo recibe numeros y dos fechas), pero
/// si hay un default que importa: la fecha final cubre el DIA COMPLETO. Si se
/// manda la fecha pelada, el BETWEEN termina a las 00:00 y el ultimo dia del
/// rango se pierde entero.
/// </summary>
public class InventarioTransitoConsultaService : IInventarioTransitoConsultaService
{
    private readonly IInventarioTransitoConsultaRepository _repository;

    public InventarioTransitoConsultaService(IInventarioTransitoConsultaRepository repository)
    {
        _repository = repository;
    }

    public Task<List<InventarioTransitoDto>> ConsultarAsync(InventarioTransitoFiltro filtro, CancellationToken ct)
        => _repository.ConsultarAsync(new InventarioTransitoFiltro
        {
            StatusInventario = filtro.StatusInventario,
            IDCliente = Math.Max(filtro.IDCliente, 0),
            FechaEmisionInicial = filtro.FechaEmisionInicial.Date,
            // 23:59:59 del dia final, no las 00:00: con la hora en cero el BETWEEN
            // deja fuera todo lo emitido ese dia.
            FechaEmisionFinal = filtro.FechaEmisionFinal.Date.AddDays(1).AddSeconds(-1),
            Agrupado = filtro.Agrupado
        }, ct);
}
