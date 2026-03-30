namespace ISL_Service.Application.DTOs.GeneracionRolTorneo;

public class CreateGeneracionRolTorneoRequest
{
    public Guid TorneoId { get; set; }
    public Guid JornadaId { get; set; }
    public byte DiaJuego { get; set; }
    public TimeSpan? HoraInicio { get; set; }
    public short? DuracionPartidoMin { get; set; }
    public short? MinutosEntrePartidos { get; set; }
    public byte? NumeroCanchas { get; set; }
    public string? Observaciones { get; set; }
}
