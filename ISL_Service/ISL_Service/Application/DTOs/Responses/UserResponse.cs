using System.Text.Json.Serialization;

namespace ISL_Service.Application.DTOs.Responses;

public class UserResponse
{
    public Guid Id { get; set; }

    [JsonPropertyName("idUsuario")]
    public Guid IdUsuario => Id;

    public string Usuario { get; set; } = default!;
    public string Rol { get; set; } = default!;
    public int EmpresaId { get; set; }
    public int Estado { get; set; } // 1/2/3
    public bool DebeCambiarContrasena { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
}
