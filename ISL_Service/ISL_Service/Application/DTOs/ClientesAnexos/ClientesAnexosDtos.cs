namespace ISL_Service.Application.DTOs.ClientesAnexos;

/*
  LAS PESTANAS DE LA FICHA DEL CLIENTE.

  Cuatro lecturas que en Mac31 son las pestanas de la pantalla de clientes:
  bonificaciones por tipo de usado, centros de servicio permitidos, datos de
  facturacion y la bitacora de cambios. El cliente en si ya lo sirve el backend
  (captura de pedidos y reportes); esto es lo que cuelga de el.

  FUERA LOS `*Ftm`, COMO EN TODO LO MIGRADO. El SP devuelve cada fecha dos
  veces, el valor y su texto `dd-MM-yyyy`. Se manda el valor.
*/

/// <summary>
/// Bonificacion que se le reconoce a un cliente por tipo de casco usado.
///
/// OJO: el SP devuelve UN RENGLON POR TIPO DE USADO ACTIVO, exista o no la
/// bonificacion del cliente (es un LEFT JOIN desde el catalogo). Un renglon con
/// idClienteBonificacion = 0 significa "este tipo de usado no tiene
/// bonificacion pactada", no que falte un dato.
/// </summary>
public class ClienteBonificacionDto
{
    public int IDClienteBonificacion { get; set; }
    public int IDCliente { get; set; }
    public int NumeroCliente { get; set; }
    public string? NombreCliente { get; set; }
    public int IDTipoUsado { get; set; }
    public string TipoUsado { get; set; } = string.Empty;
    public decimal Bonificacion { get; set; }
    public int IDUsuarioAlta { get; set; }
    public DateTime? FechaAlta { get; set; }
    public string? EquipoAlta { get; set; }
    public DateTime? FechaModificacion { get; set; }
    public string? EquipoModifico { get; set; }
    public int IDUsuarioModifico { get; set; }
    /// <summary>
    /// Quien modifico. Legacy la perdia: su DTO no declaraba esta columna, asi
    /// que el nombre del que cambio una bonificacion nunca llegaba a la
    /// pantalla aunque el SP si lo mandaba.
    /// </summary>
    public string? UsuarioModifico { get; set; }
}

/// <summary>
/// Un centro de servicio y si este cliente puede comprar ahi.
///
/// Igual que las bonificaciones: hay un renglon por centro activo, con
/// `permitido` en 0 cuando no se le ha dado acceso.
/// </summary>
public class ClienteCentroServicioDto
{
    public int IDEmpresa { get; set; }
    public int IDClienteCentroServicio { get; set; }
    public int IDCliente { get; set; }
    public int NumeroCliente { get; set; }
    public string? NombreCliente { get; set; }
    public int IDCentro { get; set; }
    public string NombreCentro { get; set; } = string.Empty;
    /// <summary>
    /// Nombre de la base de datos de ese centro. Se conserva porque es el id
    /// con el que Mac31 y los reportes del web (BaseDelCentro) saben a donde
    /// apuntar, y sin el el selector de centros no puede armarse.
    /// </summary>
    public string? BaseCentro { get; set; }
    /// <summary>1 = el cliente puede comprar en ese centro.</summary>
    public int Permitido { get; set; }
    public int IDUsuario { get; set; }
    public DateTime? Fecha { get; set; }
    public string? Equipo { get; set; }
    public DateTime? FechaModificacion { get; set; }
    public string? EquipoModificacion { get; set; }
    public int IDUsuarioModificacion { get; set; }
    public string? UsuarioModifico { get; set; }
}

/// <summary>
/// Un movimiento de la bitacora del cliente. Mezcla dos origenes a proposito,
/// como en Mac31: los cambios de [Clientes Log] y las reasignaciones de agente
/// de [Ventas Reasignacion].
/// </summary>
public class ClienteLogDto
{
    public int IDClientesLog { get; set; }
    /// <summary>0 = alta. Cualquier otro valor = modificacion.</summary>
    public int IDAccion { get; set; }
    /// <summary>Texto que arma el SP: "ALTA", "MODIFICACION" o la reasignacion completa.</summary>
    public string Accion { get; set; } = string.Empty;
    public int IDUsuarioModificacion { get; set; }
    public DateTime? FechaModificacion { get; set; }
    public string? UsuarioModificacion { get; set; }
    public string? EquipoModificacion { get; set; }
}

/// <summary>Un juego de datos de facturacion de un cliente (puede tener varios).</summary>
public class ClienteFacturacionDto
{
    public int IDClienteFacturacion { get; set; }
    public int IDCliente { get; set; }
    public int IDColonia { get; set; }
    public int IDStatus { get; set; }
    public string NombreFacturacion { get; set; } = string.Empty;
    public string? RFC { get; set; }
    public int IDUsuario { get; set; }
    public string? Usuario { get; set; }
    public DateTime? Fecha { get; set; }
    public string? Equipo { get; set; }
    public string Estatus { get; set; } = string.Empty;

    public string? Calle { get; set; }
    public string? NumeroExterior { get; set; }
    public string? NumeroInterior { get; set; }
    public string? Colonia { get; set; }
    public string? CodigoPostal { get; set; }
    public string? DelegacionMunicipio { get; set; }
    public string? Ciudad { get; set; }
    public string? Estado { get; set; }
    /// <summary>
    /// Direccion armada por el SP. Esta SI se conserva aunque sea texto
    /// preconcatenado: es la direccion fiscal tal como la imprime la factura, y
    /// rearmarla en el front con otro orden o otra puntuacion seria imprimir un
    /// domicilio distinto del que se timbro.
    /// </summary>
    public string? Direccion { get; set; }

    /// <summary>La clave del SAT, no un id numerico: el SP la devuelve como texto.</summary>
    public string? IDFERegimenFiscal { get; set; }
    public string? FERegimenFiscal { get; set; }
    public string? IDFEFormaPago { get; set; }
    public string? FEFormaPago { get; set; }
    public string? IDFEUsoCFDI { get; set; }
    public string? FEUsoCFDI { get; set; }
    public string? IDFEMetodoPago { get; set; }
    public string? FEMetodoPago { get; set; }

    /// <summary>
    /// A quien se le manda la factura. Son cinco columnas separadas en la base,
    /// no una lista: se publican tal cual para no inventar un orden.
    /// </summary>
    public string? Email { get; set; }
    public string? Email2 { get; set; }
    public string? Email3 { get; set; }
    public string? Email4 { get; set; }
    public string? Email5 { get; set; }

    /*
      ESTOS TRES LEGACY LOS PERDIA.
      Su DTO los llamaba IDUsuarioModifico / UsuarioModifico / EquipoModifico y
      el SP los manda como *Modificacion. Como DataTableToList empareja por
      nombre exacto, los tres salian siempre vacios: desde el web no se podia
      saber quien habia cambiado un RFC. Aqui se llaman como la columna.
    */
    public int IDUsuarioModificacion { get; set; }
    public string? UsuarioModificacion { get; set; }
    public DateTime? FechaModificacion { get; set; }
    public string? EquipoModificacion { get; set; }
}
