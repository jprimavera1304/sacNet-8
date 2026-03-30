namespace ISL_Service.Application.DTOs.Jornadas;

public class JornadaDto
{
    public Guid Id { get; set; }
    public short NumeroJornada { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public byte Estado { get; set; }
    public string? EstadoNombre { get; set; }
    public DateTime FechaCreacion { get; set; }
    public Guid UsuarioCreacionId { get; set; }
    public DateTime? FechaActualizacion { get; set; }
    public Guid? UsuarioActualizacionId { get; set; }
    public DateTime? FechaCancelacion { get; set; }
    public Guid? UsuarioCancelacionId { get; set; }
    public string? MotivoCancelacion { get; set; }
    public byte[]? RowVer { get; set; }
}
