namespace ISL_Service.Application.DTOs.ConfiguracionRolTorneo;

public class ConfiguracionRolTorneoDto
{
    public Guid Id { get; set; }
    public Guid TorneoId { get; set; }
    public string? Torneo { get; set; }
    public string? TorneoClave { get; set; }
    public Guid? TemporadaId { get; set; }
    public string? Temporada { get; set; }
    public TimeSpan HoraInicioPredeterminada { get; set; }
    public short DuracionPartidoMin { get; set; }
    public short MinutosEntrePartidos { get; set; }
    public byte NumeroCanchas { get; set; }
    public string? ObservacionesPredeterminadas { get; set; }
    public byte Estado { get; set; }
    public string? EstadoNombre { get; set; }
    public DateTime? FechaCreacion { get; set; }
    public Guid? UsuarioCreacionId { get; set; }
    public DateTime? FechaActualizacion { get; set; }
    public Guid? UsuarioActualizacionId { get; set; }
    public DateTime? FechaCancelacion { get; set; }
    public Guid? UsuarioCancelacionId { get; set; }
    public string? MotivoCancelacion { get; set; }
    public byte[]? RowVer { get; set; }
}
