namespace ISL_Service.Application.DTOs.ClientesConsulta;

/*
  LA PANTALLA ConsultarClientes DE MAC31.

  Un renglon por cliente, tal como lo arma sp_n_ConsultaClientes en su tabla
  temporal #Clientes. Los nombres de las propiedades son EXACTAMENTE los de esa
  tabla temporal porque el mapeo (Funciones.DataTableToList) empareja por nombre
  y distingue mayusculas: `Rfc` es Rfc y no RFC, y cambiarle la caja a una
  propiedad la deja vacia en silencio.

  FUERA LOS `*Ftm`, COMO EN TODO LO MIGRADO. El SP devuelve `FechaAlta` y
  `FechaAltaFtm` (la misma fecha como texto 'dd-MM-yyyy' para la rejilla de
  WinForms). Se manda el valor; el formato lo decide quien pinta.

  LOS NUMEROS QUE PARECEN BOOLEANOS NO LO SON. Redondear, CascosCambio,
  Efectivo, Cheque, Transferencia, DepositoEfectivo, FacturaParaPago y
  EsMostrador estan declarados `int` en #Clientes, no `bit`. Se publican como
  int: convertirlos a bool aqui seria decidir por el front que 2 es true.
*/

/// <summary>
/// Un cliente del padron, con su agente, sus condiciones de credito, su
/// domicilio resuelto y sus datos de facturacion electronica.
/// </summary>
public class ClienteConsultaDto
{
    public int IDCliente { get; set; }
    public int IDColonia { get; set; }
    public int IDCondicionesCredito { get; set; }
    public int IDCondicionesCreditoAceites { get; set; }
    public int IDCondicionesCreditoCascos { get; set; }
    public int IDDescuento { get; set; }

    /// <summary>
    /// El agente al que pertenece el cliente. Es la columna por la que filtra
    /// este endpoint, y por la que no se deja filtrar desde fuera.
    /// </summary>
    public int IDAgente { get; set; }

    /// <summary>1 = Activo, 2 = Inactivo (Catalogo Status).</summary>
    public int IDStatus { get; set; }

    /// <summary>El numero de cliente que se teclea en la pantalla. Es int en la tabla.</summary>
    public int Numero { get; set; }

    public string? ApellidoPaterno { get; set; }
    public string? ApellidoMaterno { get; set; }
    public string? Nombre { get; set; }

    /// <summary>Apellidos + nombre, ya armado por el SP. Es el orden con el que ordena el listado.</summary>
    public string NombreCliente { get; set; } = string.Empty;
    public string? NombreNegocio { get; set; }
    public string? Rfc { get; set; }

    /// <summary>Numero del agente (no su id): es lo que ve el usuario.</summary>
    public int Agente { get; set; }
    public string? NombreAgente { get; set; }
    /// <summary>"numero - nombre", recortado a 20 caracteres por el propio SP.</summary>
    public string? NumeroNombreAgente { get; set; }

    public string? CondicionCredito { get; set; }
    public string? CondicionCreditoAceites { get; set; }
    public string? CondicionCreditoCascos { get; set; }
    public int DiasCredito { get; set; }

    public string? Telefono1 { get; set; }
    public string? Telefono2 { get; set; }
    public string? Telefonos { get; set; }

    public DateTime? FechaAlta { get; set; }

    /// <summary>
    /// A quien se le manda la factura. Son cinco columnas separadas en la base,
    /// no una lista: se publican tal cual para no inventar un orden.
    /// </summary>
    public string? Email { get; set; }
    public string? Email2 { get; set; }
    public string? Email3 { get; set; }
    public string? Email4 { get; set; }
    public string? Email5 { get; set; }

    public string Estatus { get; set; } = string.Empty;

    public string? Calle { get; set; }
    public string? NumeroExterior { get; set; }
    public string? NumeroInterior { get; set; }
    /// <summary>Calle + exterior + interior, ya concatenado por el SP.</summary>
    public string? CalleConNumero { get; set; }
    public string? Colonia { get; set; }
    public string? CodigoPostal { get; set; }
    public string? DelegacionMunicipio { get; set; }
    public string? Ciudad { get; set; }
    public string? Estado { get; set; }
    public string? Observaciones { get; set; }

    /// <summary>Limite de credito estatico del cliente (Clientes.[Limite Credito]).</summary>
    public decimal LimiteCredito { get; set; }

    public string? IDFacturama { get; set; }

    /// <summary>1 = si factura, 2 = no factura. Es el combo TIPO CLIENTE de Mac31.</summary>
    public int IDTipoCliente { get; set; }
    public string TipoCliente { get; set; } = string.Empty;

    /// <summary>Las claves del SAT son texto, no ids numericos.</summary>
    public string? IDFEMetodoPago { get; set; }
    public string? FEMetodoPago { get; set; }
    public string? IDFEFormaPago { get; set; }
    public string? FEFormaPago { get; set; }
    public string? IDFERegimenFiscal { get; set; }
    public string? FERegimenFiscal { get; set; }
    public string? IDFEUsoCFDI { get; set; }
    public string? FEUsoCFDI { get; set; }

    public int Redondear { get; set; }

    public int IDGrupoCliente { get; set; }
    public string GrupoCliente { get; set; } = string.Empty;

    /// <summary>"SI" / "NO". Texto, no bandera: asi lo arma el SP.</summary>
    public string? Huellas { get; set; }

    public int CascosCambio { get; set; }
    public decimal SaldoFavor { get; set; }
    public decimal SaldoFavorCascos { get; set; }

    /// <summary>Formas de pago que tiene permitidas. Son int en la tabla, no bit.</summary>
    public int Efectivo { get; set; }
    public int Cheque { get; set; }
    public int Transferencia { get; set; }
    public int DepositoEfectivo { get; set; }
    public int FacturaParaPago { get; set; }

    public string? NombreFacturacion { get; set; }

    public int EsMostrador { get; set; }
    public string? Referencia { get; set; }
}
