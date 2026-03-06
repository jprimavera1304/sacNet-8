namespace ISL_Service.Application.DTOs.Equipos;

public class EquipoDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    public Guid CategoriaPredeterminadaId { get; set; }
    public string? CategoriaPredeterminadaNombre { get; set; }

    public byte DiaJuegoPredeterminado { get; set; }

    public Guid ProfesorTitularPredeterminadoId { get; set; }
    public string? ProfesorTitular { get; set; }

    public Guid? ProfesorAuxiliarPredeterminadoId { get; set; }
    public string? ProfesorAuxiliar { get; set; }

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

