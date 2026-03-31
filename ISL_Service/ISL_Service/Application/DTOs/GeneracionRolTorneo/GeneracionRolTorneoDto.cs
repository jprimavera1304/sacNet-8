namespace ISL_Service.Application.DTOs.GeneracionRolTorneo;

public class GeneracionRolTorneoDto
{
    public Guid Id { get; set; }
    public Guid TorneoId { get; set; }
    public string? Torneo { get; set; }
    public string? TorneoClave { get; set; }
    public Guid JornadaId { get; set; }
    public string? Jornada { get; set; }
    public DateTime FechaJuego { get; set; }
    public byte DiaJuego { get; set; }
    public string? DiaJuegoNombre { get; set; }
    public TimeSpan HoraInicio { get; set; }
    public short DuracionPartidoMin { get; set; }
    public short MinutosEntrePartidos { get; set; }
    public byte NumeroCanchas { get; set; }
    public string? Observaciones { get; set; }
    public byte Estado { get; set; }
    public string? EstadoNombre { get; set; }
    public DateTime? FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
    public DateTime? FechaCancelacion { get; set; }
    public string? MotivoCancelacion { get; set; }
    public byte[]? RowVer { get; set; }
}
