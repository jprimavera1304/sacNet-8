namespace ISL_Service.Application.DTOs.AppVersion;

/// <summary>
/// Que version de la app movil acepta el servidor para una plataforma.
/// La app la pide ANTES del login y decide si se deja usar, si solo avisa, o si
/// bloquea con una pantalla que manda a la tienda.
/// </summary>
public class AppVersionResponse
{
    public string Plataforma { get; set; } = string.Empty;

    /// <summary>Por debajo de esta version la app se BLOQUEA.</summary>
    public string VersionMinima { get; set; } = "0.0.0";

    /// <summary>Por debajo de esta version solo se AVISA (se puede posponer).</summary>
    public string VersionRecomendada { get; set; } = "0.0.0";

    /// <summary>A donde manda el boton "Actualizar" (Play o App Store).</summary>
    public string UrlTienda { get; set; } = string.Empty;

    /// <summary>Texto opcional que se le muestra al usuario.</summary>
    public string? Mensaje { get; set; }
}
