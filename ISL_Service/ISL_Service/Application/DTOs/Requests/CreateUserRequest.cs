using System.ComponentModel.DataAnnotations;

namespace ISL_Service.Application.DTOs.Requests;

public class CreateUserRequest
{
    [Required, StringLength(60, MinimumLength = 4)]
    [RegularExpression("^[A-Z0-9_]+$", ErrorMessage = "Usuario inválido. Usa A-Z, 0-9 y _")]
    public string Usuario { get; set; } = default!;

    /// <summary>
    /// La contrasena definitiva del usuario. Ya no hay temporal ni cambio forzado:
    /// quien da de alta decide la contrasena y esa es la que queda.
    /// </summary>
    [StringLength(100, MinimumLength = 8)]
    public string? Password { get; set; }

    /// <summary>
    /// Nombre viejo del campo. Se sigue aceptando para no romper a los clientes
    /// que ya lo mandan, pero significa lo mismo que Password.
    /// </summary>
    [StringLength(100, MinimumLength = 8)]
    public string? PasswordTemporal { get; set; }

    [Required, StringLength(30)]
    public string Rol { get; set; } = default!; // User | Admin | SuperAdmin

    /*
      EL NOMBRE DE LA PERSONA, para Mac31.

      Alla la columna Nombre es obligatoria y es la que sale en las pantallas.
      Como no venia en la peticion, el alta la rellenaba con el propio usuario:
      en Mac31 la gente quedaba llamandose "JAZMIN" o "MARYORIS" en vez de con
      su nombre. Opcional para no romper a quien ya manda altas sin el; cuando
      falta se sigue usando el usuario, como antes.
    */
    [StringLength(255)]
    public string? Nombre { get; set; }


    public string ResolverPassword()
        => !string.IsNullOrWhiteSpace(Password) ? Password! : (PasswordTemporal ?? string.Empty);
}
