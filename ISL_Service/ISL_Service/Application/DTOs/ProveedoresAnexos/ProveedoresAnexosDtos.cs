namespace ISL_Service.Application.DTOs.ProveedoresAnexos;

/*
  LAS PESTANAS DE LA FICHA DEL PROVEEDOR: apoderados, domicilios y documentos.

  El proveedor en si ya lo sirve el backend (catalogo de remisiones y pagos a
  proveedores); esto es lo que cuelga de el y no estaba migrado.
*/

/// <summary>
/// Un apoderado del proveedor y el puesto con el que firma. El puesto viene de
/// MC_Puestos, y de ahi sale si es representante legal o tecnico.
/// </summary>
public class ProveedorApoderadoDto
{
    public int IDProveedorApoderado { get; set; }
    public int IDProveedor { get; set; }
    public int IDPuesto { get; set; }
    public int IDStatus { get; set; }
    public string Apoderado { get; set; } = string.Empty;
    public string Puesto { get; set; } = string.Empty;
    /// <summary>
    /// "Apoderado  (Puesto)", como lo arma el SP. Se conserva porque es la
    /// cadena con la que se firma un contrato de obra y el documento impreso
    /// tiene que decir exactamente eso, con su doble espacio incluido.
    /// </summary>
    public string ApoderadoPuesto { get; set; } = string.Empty;
    public string Estatus { get; set; } = string.Empty;
}

/// <summary>Un domicilio del proveedor, con la colonia ya resuelta.</summary>
public class ProveedorDomicilioDto
{
    public int IDProveedorDomicilio { get; set; }
    public int IDProveedor { get; set; }
    public int IDTipoDomicilio { get; set; }
    public int IDColonia { get; set; }
    public int IDStatus { get; set; }
    public string TipoDomicilio { get; set; } = string.Empty;
    public string Domicilio { get; set; } = string.Empty;
    public string? CalleNumero { get; set; }
    public string Estatus { get; set; } = string.Empty;
    public string? Colonia { get; set; }
    public string? CodigoPostal { get; set; }
    public string? DelegacionMunicipio { get; set; }
    public string? Ciudad { get; set; }
    public string? Estado { get; set; }
}

/// <summary>Un documento del proveedor (acta, poder, comprobante...).</summary>
public class ProveedorDocumentoDto
{
    public int IDProveedorDocumento { get; set; }
    public int IDProveedor { get; set; }
    public int IDTipoDocumento { get; set; }
    public int IDStatus { get; set; }
    public string Documento { get; set; } = string.Empty;
    public string? Archivo { get; set; }
    public string? ArchivoFtp { get; set; }
    public string TipoDocumento { get; set; } = string.Empty;
    public string Estatus { get; set; } = string.Empty;

    /*
      `ArchivoFtm` NO SE PUBLICA, Y ESTA VEZ NO ES POR SER TEXTO PRECONCATENADO.
      El SP lo calcula como CASE WHEN Archivo = '' THEN 'NO' ELSE 'SI' END, y
      ese CASE solo compara con cadena vacia: con Archivo en NULL contesta 'SI'
      y la pantalla ensena que hay documento adjunto cuando no hay ninguno. El
      front decide mirando si `archivo` trae algo, que es la verdad.
    */
}
