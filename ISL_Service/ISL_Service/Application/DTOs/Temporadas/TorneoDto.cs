namespace ISL_Service.Application.DTOs.Temporadas;

public class TorneoDto
{
    public Guid Id { get; set; }
    public Guid TemporadaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Clave { get; set; }
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public byte Estado { get; set; }
    public DateTime FechaCreacion { get; set; }
    public Guid UsuarioCreacionId { get; set; }
    public DateTime? FechaActualizacion { get; set; }
    public Guid? UsuarioActualizacionId { get; set; }
    public DateTime? FechaCancelacion { get; set; }
    public Guid? UsuarioCancelacionId { get; set; }
    public string? MotivoCancelacion { get; set; }
    public byte[]? RowVer { get; set; }

    // Campos agregados por sp_w_ConsultarTorneos
    public string? TemporadaNombre { get; set; }
    public string? UsuarioCreacion { get; set; }
    public string? UsuarioActualizacion { get; set; }
    public string? UsuarioCancelacion { get; set; }
}
