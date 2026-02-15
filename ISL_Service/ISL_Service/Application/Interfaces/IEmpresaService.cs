using System.Security.Claims;
using ISL_Service.Application.DTOs.Responses;

namespace ISL_Service.Application.Interfaces;

public interface IEmpresaService
{
    Task<List<EmpresaResponse>> ListAsync(ClaimsPrincipal actor, CancellationToken ct);
}
