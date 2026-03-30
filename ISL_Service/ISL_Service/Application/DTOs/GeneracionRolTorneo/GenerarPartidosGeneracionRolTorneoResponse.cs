namespace ISL_Service.Application.DTOs.GeneracionRolTorneo;

public class GenerarPartidosGeneracionRolTorneoResponse
{
    public List<PartidoGeneracionRolTorneoDto> Partidos { get; set; } = new();
    public List<GeneracionRolNotaDto> Notas { get; set; } = new();
}
