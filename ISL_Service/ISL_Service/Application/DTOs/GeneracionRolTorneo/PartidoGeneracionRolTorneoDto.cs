namespace ISL_Service.Application.DTOs.GeneracionRolTorneo;

public class PartidoGeneracionRolTorneoDto
{
    public Guid Id { get; set; }
    public Guid GeneracionRolTorneoId { get; set; }
    public Guid TorneoId { get; set; }
    public string? Torneo { get; set; }
    public Guid JornadaId { get; set; }
    public string? Jornada { get; set; }
    public Guid CategoriaId { get; set; }
    public string? Categoria { get; set; }
    public Guid InscripcionLocalId { get; set; }
    public string? EquipoLocal { get; set; }
    public Guid InscripcionVisitanteId { get; set; }
    public string? EquipoVisitante { get; set; }
    public byte DiaJuego { get; set; }
    public string? DiaJuegoNombre { get; set; }
    public TimeSpan HoraInicio { get; set; }
    public TimeSpan HoraFin { get; set; }
    public string? Cancha { get; set; }
    public int OrdenProgramacion { get; set; }
    public string? Observaciones { get; set; }
    public byte Estado { get; set; }
    public string? EstadoNombre { get; set; }
    public DateTime? FechaCreacion { get; set; }
}
