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

    /*
      SI LA PERSONA EXISTE TAMBIEN EN MAC31.

      Los dos mundos se ligan por el NOMBRE DE USUARIO, no por una llave. Si no
      casan, la persona entra al web y se queda sin permisos —porque los
      permisos se leen del lado viejo por IDUsuario— y no hay nada en pantalla
      que lo explique.

      Viaja aqui para que la lista pueda enseñarlo de un vistazo. Antes no
      existia el dato y la unica forma de averiguarlo era preguntar por la
      contraseña de cada usuario, uno por uno: N llamadas para una columna, y
      encima exigia el permiso de ver contraseñas.
    */
    public bool ExisteEnMac31 { get; set; }

    /// Su IDUsuario de Mac31, cuando existe. Sirve para el rastro cuando algo
    /// no cuadra: con el numero se puede ir a mirar alla directamente.
    public int? IdUsuarioMac31 { get; set; }
}
