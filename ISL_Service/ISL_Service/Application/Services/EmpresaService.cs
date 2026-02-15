using System.Security.Claims;
using ISL_Service.Application.DTOs.Responses;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;

namespace ISL_Service.Application.Services;

public class EmpresaService : IEmpresaService
{
    private const int EMPRESA_SISTEMA = 5;

    private readonly IEmpresaRepository _repo;

    public EmpresaService(IEmpresaRepository repo)
    {
        _repo = repo;
    }

    public async Task<List<EmpresaResponse>> ListAsync(ClaimsPrincipal actor, CancellationToken ct)
    {
        var actorEmpresaId = CurrentUser.GetEmpresaId(actor);
        var isSuper = CurrentUser.IsSuperAdmin(actor);

        if (!isSuper)
        {
            // Admin: solo su empresa
            return new List<EmpresaResponse>
            {
                new EmpresaResponse
                {
                    EmpresaId = actorEmpresaId,
                    Nombre = actorEmpresaId == EMPRESA_SISTEMA ? "Sistema" : $"Empresa {actorEmpresaId}"
                }
            };
        }

        // SuperAdmin: todas (segun exista en Usuarios)
        var ids = await _repo.ListEmpresaIdsAsync(ct);

        // Si quieres asegurar que siempre aparezca "Sistema"
        if (!ids.Contains(EMPRESA_SISTEMA))
            ids.Insert(0, EMPRESA_SISTEMA);

        return ids
            .Distinct()
            .OrderBy(x => x)
            .Select(id => new EmpresaResponse
            {
                EmpresaId = id,
                Nombre = id == EMPRESA_SISTEMA ? "Sistema" : $"Empresa {id}"
            })
            .ToList();
    }
}
