namespace ISL_Service.Application.DTOs.Requests;

public class PermisosWebUpdateUserOverridesRequest
{
    public Guid UserId { get; set; }
    public List<string> Allow { get; set; } = new();
    public List<string> Deny { get; set; } = new();
}
