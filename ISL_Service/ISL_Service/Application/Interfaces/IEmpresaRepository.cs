namespace ISL_Service.Application.Interfaces;

public interface IEmpresaRepository
{
    Task<List<int>> ListEmpresaIdsAsync(CancellationToken ct);
    Task<string> GetCompanyKeyAsync(CancellationToken ct);
}
