namespace ISL_Service.Application.DTOs.ActualizacionesAplicacion;

/// <summary>
/// Un archivo del manifiesto de actualizacion de las aplicaciones de escritorio
/// (lo que el arrancador de Mac31 baja y copia antes de abrir el programa).
///
/// NO ES LA VERSION DE LA APP MOVIL. Eso es `GET api/app/version`, que es anonimo
/// porque el telefono pregunta antes del login. Esto es otra cosa: la lista de
/// archivos a reemplazar, con las rutas del servidor.
/// </summary>
public class ActualizacionAplicacionDto
{
    public int IDAplicacionActualizacion { get; set; }
    public int IDAplicacion { get; set; }
    /// <summary>1 = Activo, 2 = Inactivo.</summary>
    public int IDStatus { get; set; }
    public string? PathOrigen { get; set; }
    public string ArchivoOrigen { get; set; } = string.Empty;
    public string? PathDestino { get; set; }
    public string? ArchivoDestino { get; set; }
    /// <summary>1 = si el archivo ya existe en destino, se pisa.</summary>
    public int Sobreescribir { get; set; }
    /// <summary>1 = despues de copiarlo, se ejecuta.</summary>
    public int Ejecutar { get; set; }
    public string? NumVersion { get; set; }
    public DateTime? Fecha { get; set; }
    /// <summary>Id del archivo en Google Drive, cuando la copia viene de ahi.</summary>
    public string? DriveId { get; set; }
}
