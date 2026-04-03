namespace ISL_Service.Application.DTOs.GeneracionRolTorneo;

public class GeneracionRolEquipoDto
{
    public Guid Id { get; set; }
    public Guid GeneracionRolTorneoId { get; set; }
    public Guid InscripcionTorneoId { get; set; }
    public Guid? TorneoId { get; set; }
    public Guid? EquipoId { get; set; }
    public string? Equipo { get; set; }
    public Guid? CategoriaId { get; set; }
    public string? Categoria { get; set; }
    public Guid? ProfesorTitularId { get; set; }
    public string? ProfesorTitular { get; set; }
    public Guid? ProfesorAuxiliarId { get; set; }
    public string? ProfesorAuxiliar { get; set; }
    public byte? DiaJuego { get; set; }
    public string? DiaJuegoNombre { get; set; }
    public bool Participa { get; set; }
    public bool? EsElegible { get; set; }
    public string? MotivoNoElegible { get; set; }
    public string? Observaciones { get; set; }
    public byte Estado { get; set; }
    public string? EstadoNombre { get; set; }
    public DateTime? FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
    public DateTime? FechaCancelacion { get; set; }
    public string? MotivoCancelacion { get; set; }
    public byte[]? RowVer { get; set; }
}
