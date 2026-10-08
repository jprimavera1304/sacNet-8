namespace ISL_Service.Application.DTOs.ProveedoresConsulta;

/*
  LA CONSULTA DE PROVEEDORES DE MAC31.

  Un renglon por proveedor, con los nombres de la tabla temporal #Proveedores de
  sp_n_ConsultaProveedores: el mapeo empareja por nombre exacto y distingue
  mayusculas, asi que `Rfc` es Rfc y no RFC.

  FUERA EL `FechaAltaFtm`: es la misma FechaAlta como texto 'dd-MM-yyyy' para la
  rejilla de WinForms. Se manda el valor.

  NO HAY FILTRO POR AGENTE y no hace falta: un proveedor no pertenece a un
  agente. El agente acota clientes, no proveedores.
*/

/// <summary>
/// Un proveedor con su condicion de credito, su domicilio resuelto y su limite.
/// </summary>
public class ProveedorConsultaDto
{
    public int IDProveedor { get; set; }
    public int IDColonia { get; set; }
    public int IDCondicionesCredito { get; set; }
    public int IDDescuento { get; set; }
    /// <summary>1 = Activo, 2 = Inactivo (Catalogo Status).</summary>
    public int IDStatus { get; set; }

    /// <summary>El numero de proveedor que se teclea en la pantalla. Es int en la tabla.</summary>
    public int Numero { get; set; }

    public string? ApellidoPaterno { get; set; }
    public string? ApellidoMaterno { get; set; }
    public string? Nombre { get; set; }

    /// <summary>Apellidos + nombre, ya armado por el SP. Es el orden del listado.</summary>
    public string NombreProveedor { get; set; } = string.Empty;
    public string? Rfc { get; set; }

    /// <summary>Texto de la condicion; "CONTADO" cuando el proveedor no tiene ninguna.</summary>
    public string? CondicionCredito { get; set; }
    public int DiasCredito { get; set; }

    public string? Telefono1 { get; set; }
    public string? Telefono2 { get; set; }
    public string? Telefonos { get; set; }

    public DateTime? FechaAlta { get; set; }
    public string? Email { get; set; }
    public string Estatus { get; set; } = string.Empty;

    /// <summary>
    /// Calle y numero en UNA columna. En proveedores la base no los tiene
    /// separados como en clientes: es la columna Proveedores.[Calle y Numero].
    /// </summary>
    public string? CalleNumero { get; set; }
    public string? Colonia { get; set; }
    public string? CodigoPostal { get; set; }
    public string? DelegacionMunicipio { get; set; }
    public string? Ciudad { get; set; }
    public string? Estado { get; set; }

    public string? Observaciones { get; set; }
    public decimal LimiteCredito { get; set; }
    public string? PaginaWeb { get; set; }
    public string? IDFacturama { get; set; }
}
