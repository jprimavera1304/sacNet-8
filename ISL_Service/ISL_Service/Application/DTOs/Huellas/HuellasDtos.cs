namespace ISL_Service.Application.DTOs.Huellas;

/// <summary>
/// Una huella registrada. El MISMO renglon sirve para las tres tablas
/// (UsuariosHuellas, EmpleadosHuellas, ClientesHuellas) porque las tres tienen
/// la misma forma; `origen` dice de cual salio.
/// </summary>
public class HuellaDto
{
    /// <summary>"usuario", "empleado" o "cliente".</summary>
    public string Origen { get; set; } = string.Empty;
    /// <summary>Id del renglon de huella en su tabla.</summary>
    public int IdHuella { get; set; }
    /// <summary>Id del usuario, empleado o cliente, segun el origen.</summary>
    public int IdPersona { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int IDMano { get; set; }
    public int IDDedo { get; set; }
    public string Mano { get; set; } = string.Empty;
    public string Dedo { get; set; } = string.Empty;

    /// <summary>
    /// La plantilla de la huella, en base64.
    ///
    /// Es un `varbinary(max)` en la base y sale como base64 porque es lo que
    /// System.Text.Json hace con un byte[] — y es lo mismo que salia de legacy
    /// (Newtonsoft tambien serializa byte[] en base64), asi que el lector de
    /// huellas del cliente no cambia.
    ///
    /// NO ES UN DATO CUALQUIERA: es un identificador biometrico. Por eso el
    /// endpoint que la devuelve pide un permiso aparte y manda `no-store`.
    /// </summary>
    public byte[]? Huella { get; set; }
}
