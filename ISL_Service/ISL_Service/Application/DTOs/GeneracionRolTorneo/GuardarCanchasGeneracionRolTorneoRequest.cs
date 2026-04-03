namespace ISL_Service.Application.DTOs.GeneracionRolTorneo;

public class GuardarCanchasGeneracionRolTorneoRequest
{
    public List<GeneracionRolCanchaItemRequest> Canchas { get; set; } = new();
}
