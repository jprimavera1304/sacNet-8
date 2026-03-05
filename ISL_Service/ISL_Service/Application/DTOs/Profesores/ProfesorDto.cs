namespace ISL_Service.Application.DTOs.Profesores;

public class ProfesorDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string? Correo { get; set; }
    public byte Estado { get; set; }
    public DateTime FechaCreacion { get; set; }
    public Guid UsuarioCreacionId { get; set; }
    public DateTime? FechaActualizacion { get; set; }
    public Guid? UsuarioActualizacionId { get; set; }
    public DateTime? FechaCancelacion { get; set; }
    public Guid? UsuarioCancelacionId { get; set; }
    public string? MotivoCancelacion { get; set; }
    public string? UsuarioCreacion { get; set; }
    public string? UsuarioActualizacion { get; set; }
    public string? UsuarioCancelacion { get; set; }
    public byte[]? RowVer { get; set; }
}
