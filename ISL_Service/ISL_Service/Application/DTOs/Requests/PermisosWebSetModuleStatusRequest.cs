namespace ISL_Service.Application.DTOs.Requests;

public class PermisosWebSetModuleStatusRequest
{
    public string ModuloClave { get; set; } = string.Empty;
    public int IdStatus { get; set; }
}
