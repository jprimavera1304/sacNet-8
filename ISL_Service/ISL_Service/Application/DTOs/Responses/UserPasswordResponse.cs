namespace ISL_Service.Application.DTOs.Responses;

/// <summary>
/// Contrasena en claro de un usuario, leida de legacy (dbo.Usuarios.Contrasena).
/// El hash de UsuarioWeb no se puede revertir, asi que legacy es la unica fuente
/// que puede contestar "cual es la contrasena de fulano".
/// </summary>
public class UserPasswordResponse
{
    public Guid UserId { get; set; }
    public string Usuario { get; set; } = default!;

    /// <summary>Null cuando el usuario no existe en legacy: no hay nada que mostrar.</summary>
    public string? Password { get; set; }

    public bool Disponible { get; set; }
    public string? Mensaje { get; set; }
}
