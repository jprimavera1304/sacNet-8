using System;

namespace ISL_Service.Domain.Entities;

public class Usuario
{
    public Guid Id { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty; // columna dbo.Usuarios.Usuario
    public string ContrasenaHash { get; set; } = string.Empty;
    public string Rol { get; set; } = "User";
    public int EmpresaId { get; set; }
    public bool DebeCambiarContrasena { get; set; }
    public int Estado { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime FechaActualizacion { get; set; }
}
