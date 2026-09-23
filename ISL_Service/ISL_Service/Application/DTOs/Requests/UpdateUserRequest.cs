using System.ComponentModel.DataAnnotations;

namespace ISL_Service.Application.DTOs.Requests;

public class UpdateUserRequest
{
    [Required, StringLength(60, MinimumLength = 4)]
    [RegularExpression("^[A-Z0-9_]+$", ErrorMessage = "Usuario inválido. Usa A-Z, 0-9 y _")]
    public string Usuario { get; set; } = default!;

    [Required, StringLength(30)]
    public string Rol { get; set; } = default!;

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


    /// <summary>
    /// Opcional. Si viene, se cambia la contrasena en los dos lados (web y legacy).
    /// Vacia o ausente significa "dejala como esta": editar el rol de alguien no
    /// tiene por que tumbarle la contrasena.
    /// </summary>
    [StringLength(100, MinimumLength = 8)]
    public string? Password { get; set; }
}
