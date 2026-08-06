using ISL_Service.Application.DTOs.AppVersion;

namespace ISL_Service.Application.Interfaces;

public interface IAppVersionRepository
{
    Task<AppVersionResponse> ConsultarAsync(string plataforma, CancellationToken ct = default);
}
