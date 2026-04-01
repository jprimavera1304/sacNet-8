namespace ISL_Service.Application.DTOs.GeneracionRolTorneo;

public class ActualizarOrdenPartidosRequest
{
    public List<OrdenPartidoItemRequest> Partidos { get; set; } = new();
}
