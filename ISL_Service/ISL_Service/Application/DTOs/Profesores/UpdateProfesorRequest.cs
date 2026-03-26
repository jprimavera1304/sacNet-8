namespace ISL_Service.Application.DTOs.Profesores;

public class UpdateProfesorRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string? Correo { get; set; }
}
