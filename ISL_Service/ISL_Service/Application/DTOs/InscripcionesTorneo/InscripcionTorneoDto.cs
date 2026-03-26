namespace ISL_Service.Application.DTOs.InscripcionesTorneo;

public class InscripcionTorneoDto
{
    public Guid Id { get; set; }

    public Guid TorneoId { get; set; }
    public string? TorneoNombre { get; set; }

    public Guid EquipoId { get; set; }
    public string? EquipoNombre { get; set; }

    public Guid CategoriaId { get; set; }
    public string? CategoriaNombre { get; set; }

    public byte DiaJuego { get; set; }

    public Guid? ProfesorTitularId { get; set; }
    public string? ProfesorTitularNombre { get; set; }

    public Guid? ProfesorAuxiliarId { get; set; }
    public string? ProfesorAuxiliarNombre { get; set; }

    public byte Estado { get; set; }
    public DateTime FechaCreacion { get; set; }
    public Guid UsuarioCreacionId { get; set; }
    public DateTime? FechaActualizacion { get; set; }
    public Guid? UsuarioActualizacionId { get; set; }
    public DateTime? FechaCancelacion { get; set; }
    public Guid? UsuarioCancelacionId { get; set; }
    public string? MotivoCancelacion { get; set; }
    public byte[]? RowVer { get; set; }
}
