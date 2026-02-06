using System;

namespace ISL_Service.Application.DTOs.Responses;

public class MeResponse
{
    public Guid Id { get; set; }
    public string Usuario { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public int EmpresaId { get; set; }
    public bool DebeCambiarContrasena { get; set; }
}
