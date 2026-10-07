using ISL_Service.Application.DTOs.Empresa;
using ISL_Service.Application.Empresa;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/// <summary>
/// Pega la lectura de la base con la traduccion a capacidades.
///
/// No cachea nada a proposito. Las banderas no cambian casi nunca, pero la fecha de
/// operacion si -puede moverla el cierre del dia o el endpoint de la fecha de
/// ventas- y un cache de capacidades dejaria al front mostrando la fecha de ayer
/// sin forma de saberlo. La lectura es un SELECT TOP 1 de una tabla de un renglon.
/// </summary>
public class CapacidadesEmpresaService : ICapacidadesEmpresaService
{
    private readonly ICapacidadesEmpresaRepository _repo;

    public CapacidadesEmpresaService(ICapacidadesEmpresaRepository repo)
    {
        _repo = repo;
    }

    public async Task<CapacidadesEmpresaResponse> ConsultarAsync(int idUsuarioLegacy, CancellationToken ct)
    {
        var crudas = await _repo.ConsultarAsync(idUsuarioLegacy, ct);
        return MapaDeCapacidades.Armar(crudas, DateOnly.FromDateTime(DateTime.Today));
    }
}
