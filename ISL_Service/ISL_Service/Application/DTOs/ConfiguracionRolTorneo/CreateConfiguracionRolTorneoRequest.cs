namespace ISL_Service.Application.DTOs.ConfiguracionRolTorneo;

public class CreateConfiguracionRolTorneoRequest
{
    public Guid TorneoId { get; set; }
    public TimeSpan? HoraInicioPredeterminada { get; set; }
    public short? DuracionPartidoMin { get; set; }
    public short? MinutosEntrePartidos { get; set; }
    public byte? NumeroCanchas { get; set; }
    public string? ObservacionesPredeterminadas { get; set; }
}
