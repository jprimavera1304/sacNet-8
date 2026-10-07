namespace ISL_Service.Application.DTOs.Maestros;

/*
  LAS TRES FICHAS MAESTRAS DE MAC31: CLIENTE, PRODUCTO Y PROVEEDOR.

  Van juntas porque comparten exactamente la misma forma y el mismo problema:
  un sp_n_Insertar*, un sp_n_Actualizar* que pisa TODAS las columnas, y un
  DELETE de legacy que llamaba a ese mismo UPDATE con lo que trajera el cuerpo.
  La ola 3 migro sus LECTURAS; esto es la otra mitad.

  CUATRO DECISIONES QUE VALEN PARA LAS TRES
  -----------------------------------------
  1. LA MODIFICACION ES COMPLETA, NO PARCIAL, y eso se dice aqui en vez de
     fingir que es un PATCH. El UPDATE de legacy escribe todas las columnas: un
     campo que no venga en el cuerpo se guarda en blanco o en cero. Por eso el
     request de modificacion pide la ficha entera y el front tiene que mandar
     lo que leyo. Ofrecer un PATCH seria mentir: el backend no puede saber que
     valor dejar si el procedimiento ya los piso todos.

  2. LA BAJA NO PIDE CUERPO. POST {id}/inhabilitar. El backend lee los valores
     actuales y solo cambia IDStatus. En legacy ese DELETE dejaba al cliente sin
     nombre y sin telefonos, con las ventas viejas apuntando a un renglon
     vaciado.

  3. EL APOSTROFO SE GUARDA. Legacy hacia Replace("'","") campo por campo y
     O'BRIEN quedaba OBRIEN para siempre. Y en el PUT de facturacion hacia algo
     peor: Replace("'","''"), que guarda la comilla DUPLICADA en la base. En una
     escritura el valor va como SqlParameter: no se concatena en ningun
     EXEC(@Sql), asi que no hay nada de lo que protegerse y si hay un apellido
     que mutilar.

  4. EL USUARIO Y EL EQUIPO SALEN DEL TOKEN. En legacy viajaban en el cuerpo, lo
     que permite dar de alta un cliente a nombre de otra persona.
*/

/// <summary>El par result/mensaje de legacy, traducido.</summary>
public class MaestroEscrituraResponse
{
    public bool Guardo { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public int Id { get; set; }
}

// ===========================================================================
// CLIENTE
// ===========================================================================

/// <summary>
/// Alta de cliente.
///
/// `IdTipoCliente` manda sobre lo que es obligatorio: 1 = con factura exige
/// agente, colonia, calle, numero, RFC y correo; 2 = sin factura solo exige
/// nombre. Legacy tenia una tercera via: con IdTipoCliente en 0 no validaba
/// NADA y el alta reventaba mas adentro con una referencia nula. Aqui 0 no es
/// un valor valido.
/// </summary>
public class CrearClienteRequest
{
    /// <summary>1 = requiere factura, 2 = no requiere factura.</summary>
    public int IdTipoCliente { get; set; }

    public int IdColonia { get; set; }
    public int IdCondicionesCredito { get; set; }
    public int IdCondicionesCreditoAceites { get; set; }
    public int IdCondicionesCreditoCascos { get; set; }

    /// <summary>Solo existe en el alta: sp_n_ActualizarCliente no lo recibe.</summary>
    public int IdDescuento { get; set; }

    public int IdAgente { get; set; }
    public int IdGrupoCliente { get; set; }

    public string Nombre { get; set; } = string.Empty;
    public string? ApellidoPaterno { get; set; }
    public string? ApellidoMaterno { get; set; }
    public string? NombreNegocio { get; set; }

    /// <summary>
    /// Si viene vacio, el backend lo arma con nombre + apellidos, que es lo que
    /// hacia legacy. Se conserva porque la factura sale con ese texto y dejarlo
    /// en blanco deja una factura sin razon social.
    /// </summary>
    public string? NombreFacturacion { get; set; }

    public string? RFC { get; set; }
    public string? CURP { get; set; }
    public string? Calle { get; set; }
    public string? NumeroExterior { get; set; }
    public string? NumeroInterior { get; set; }
    public string? Telefono1 { get; set; }
    public string? Telefono2 { get; set; }
    public string? Email { get; set; }
    public string? Email2 { get; set; }
    public string? Email3 { get; set; }
    public string? Email4 { get; set; }
    public string? Email5 { get; set; }
    public string? Observaciones { get; set; }
    public string? IdFacturama { get; set; }

    public bool Redondear { get; set; }

    /// <summary>
    /// Limite de credito.
    ///
    /// EN TAURO EL PROCEDIMIENTO LO IGNORA. sp_n_InsertarCliente hace
    /// "IF @Funcionalidad = 'TAU' SELECT @LimiteCredito = LimiteCreditoNuevos"
    /// (de Constantes), asi que el valor que se manda se descarta y todos los
    /// clientes nuevos nacen con el limite de la empresa — comprobado en
    /// Produccion_svr: se mando 900 y quedo 10000. En Zaragoza solo lo sustituye
    /// si llega en cero. El campo se conserva porque en otras empresas si cuenta,
    /// pero la pantalla no deberia prometer que se puede fijar: para cambiarlo
    /// despues esta la modificacion, que si lo escribe.
    /// </summary>
    public decimal LimiteCredito { get; set; }

    /// <summary>El numero visible del cliente cuando la empresa lo captura a mano.</summary>
    public int NumeroCapturado { get; set; }

    // Formas de pago que el cliente tiene permitidas.
    public bool Efectivo { get; set; }
    public bool Cheque { get; set; }
    public bool Transferencia { get; set; }
    public bool DepositoEfectivo { get; set; }

    /// <summary>Exige la factura para poder pagar.</summary>
    public bool FacturaParaPago { get; set; }

    /// <summary>Referencia bancaria con la que se identifican sus transferencias.</summary>
    public string? Referencia { get; set; }
}

/// <summary>
/// Modificacion de cliente. Pide la ficha COMPLETA: el UPDATE de legacy pisa
/// todas las columnas (ver la nota de arriba).
/// </summary>
public class ActualizarClienteRequest : CrearClienteRequest
{
    /// <summary>1 = Activo, 2 = Cancelado.</summary>
    public int IdStatus { get; set; } = 1;

    /// <summary>Acepta cascos a cambio. Solo existe en la modificacion.</summary>
    public bool CascosCambio { get; set; }
}

/// <summary>
/// Cambia SOLO el IDFacturama del cliente.
///
/// Es la rama @Accion = 1 de sp_n_ActualizarCliente, que hace un UPDATE de esa
/// unica columna y regresa. Tiene su propia ruta porque es lo unico que se puede
/// cambiar sin mandar la ficha entera, y usarla para eso evita el riesgo de
/// pisar treinta columnas por querer guardar un id de Facturama.
/// </summary>
public class CambiarFacturamaClienteRequest
{
    public string? IdFacturama { get; set; }
}

/// <summary>
/// Bonificaciones del cliente por tipo de usado.
///
/// Legacy mandaba DOS cadenas unidas con '~' (IDsTipoUsado y Bonificaciones) y
/// el procedimiento las apareaba por posicion, sin que nadie comprobara que
/// tenian el mismo numero de elementos: dos listas desalineadas le ponen a un
/// tipo de casco la bonificacion de otro, en silencio y en dinero. Aqui viaja
/// una lista de pares y el backend arma las cadenas; si sobra o falta un
/// elemento, ya no existe la forma de equivocarse.
/// </summary>
public class BonificacionesClienteRequest
{
    public List<BonificacionTipoUsado> Bonificaciones { get; set; } = new();
}

public class BonificacionTipoUsado
{
    public int IdTipoUsado { get; set; }
    public decimal Bonificacion { get; set; }
}

/// <summary>
/// En que centros de servicio puede comprar el cliente. Mismo caso que las
/// bonificaciones: dos listas paralelas en legacy, una lista de pares aqui.
/// </summary>
public class CentrosServicioClienteRequest
{
    public List<PermisoCentroCliente> Centros { get; set; } = new();
}

public class PermisoCentroCliente
{
    public int IdCentro { get; set; }
    public bool Permitido { get; set; }
}

/// <summary>Alta de un domicilio de facturacion del cliente.</summary>
public class CrearClienteFacturacionRequest
{
    public int IdCliente { get; set; }
    public int IdColonia { get; set; }
    public string NombreFacturacion { get; set; } = string.Empty;
    public string RFC { get; set; } = string.Empty;
    public string Calle { get; set; } = string.Empty;
    public string? NumeroExterior { get; set; }
    public string? NumeroInterior { get; set; }
    public string? IdFEFormaPago { get; set; }
    public string IdFERegimenFiscal { get; set; } = string.Empty;
    public string? IdFEUsoCFDI { get; set; }
    public string? IdFEMetodoPago { get; set; }
}

public class ActualizarClienteFacturacionRequest : CrearClienteFacturacionRequest
{
    /// <summary>
    /// 1 = Activo, 2 = Cancelado. Arranca en 1 a proposito: en legacy el
    /// parametro no tenia valor por omision en el cliente, asi que un PUT que
    /// no lo mandara guardaba 0 y APAGABA el domicilio de facturacion sin
    /// querer.
    /// </summary>
    public int IdStatus { get; set; } = 1;

    /// <summary>Empresa del centro de servicio. Solo existe en la modificacion.</summary>
    public int IdEmpresaCS { get; set; }
}

// ===========================================================================
// PRODUCTO
// ===========================================================================

/// <summary>
/// Alta de producto.
///
/// `Codigo` es la clave con la que se busca en caja. Si no viene codigo de
/// barras se usa el codigo, que es lo que hacia legacy.
/// </summary>
public class CrearProductoRequest
{
    public int IdCategoria { get; set; }
    public int IdMarca { get; set; }
    public int IdTipoUsado { get; set; }

    public string Codigo { get; set; } = string.Empty;
    public string? CodigoBarras { get; set; }
    public string Producto { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? DescripcionSat { get; set; }

    public decimal PrecioLista { get; set; }
    public decimal PrecioLista2 { get; set; }

    /// <summary>Precio para centros de servicio.</summary>
    public decimal PrecioListaCS { get; set; }

    public int Stock { get; set; }
    public int StockMinimo { get; set; }
    public int StockMaximo { get; set; }

    public string? Observaciones { get; set; }
    public decimal PesoKg { get; set; }

    /// <summary>La unidad de medida del SAT, en texto. Es el @UnidadMedida del SP.</summary>
    public string? UnidadMedidaSat { get; set; }

    public string? ClaveSat { get; set; }
    public string? IdFacturama { get; set; }

    /// <summary>La unidad de medida del catalogo propio.</summary>
    public int IdUnidadMedida { get; set; }

    /// <summary>
    /// Piezas por caja. Importa: 'productos' en un pedido es la SUMA de piezas.
    /// </summary>
    public int NumPiezas { get; set; }

    public decimal Litros { get; set; }
    public bool MostrarLista { get; set; }
    public bool EsCarga { get; set; }
}

/// <summary>
/// Modificacion de producto. Ficha COMPLETA: el UPDATE de legacy pisa todas las
/// columnas, incluidos el stock y los tres precios. Un PATCH aqui dejaria el
/// inventario en cero.
/// </summary>
public class ActualizarProductoRequest : CrearProductoRequest
{
    public int IdStatus { get; set; } = 1;
}

/// <summary>
/// Adjunta un archivo al producto (la ficha o la foto).
///
/// EL PRECIO VIAJA PORQUE EL PROCEDIMIENTO LO EXIGE, y ese es el defecto que
/// este endpoint tiene que no heredar: sp_n_ActualizarProductoFile recibe
/// @PrecioLista y lo escribe. En legacy, subir una foto sin mandar el precio
/// dejaba el precio en CERO. Aqui el precio no se pide: el backend lee el
/// actual del producto y se lo devuelve al procedimiento, para que subir un
/// archivo no pueda cambiar un precio.
/// </summary>
public class ArchivoProductoRequest
{
    public int IdArchivo { get; set; }
    public string Archivo { get; set; } = string.Empty;
}

// ===========================================================================
// PROVEEDOR
// ===========================================================================

public class CrearProveedorRequest
{
    public int IdColonia { get; set; }
    public int IdCondicionesCredito { get; set; }

    /// <summary>Solo existe en el alta: sp_n_ActualizarProveedor no lo recibe.</summary>
    public int IdDescuento { get; set; }

    public string Nombre { get; set; } = string.Empty;
    public string? ApellidoPaterno { get; set; }
    public string? ApellidoMaterno { get; set; }
    public string RFC { get; set; } = string.Empty;
    public string CalleNumero { get; set; } = string.Empty;
    public string? Telefono1 { get; set; }
    public string? Telefono2 { get; set; }
    public string? Email { get; set; }
    public string? Observaciones { get; set; }
    public string? PaginaWeb { get; set; }

    /// <summary>Solo existe en el alta: sp_n_ActualizarProveedor no lo recibe.</summary>
    public string? IdFacturama { get; set; }
}

public class ActualizarProveedorRequest
{
    public int IdColonia { get; set; }
    public int IdCondicionesCredito { get; set; }
    public int IdStatus { get; set; } = 1;
    public string Nombre { get; set; } = string.Empty;
    public string? ApellidoPaterno { get; set; }
    public string? ApellidoMaterno { get; set; }
    public string RFC { get; set; } = string.Empty;
    public string CalleNumero { get; set; } = string.Empty;
    public string? Telefono1 { get; set; }
    public string? Telefono2 { get; set; }
    public string? Email { get; set; }
    public string? Observaciones { get; set; }
    public string? PaginaWeb { get; set; }
}

public class CrearProveedorApoderadoRequest
{
    public int IdProveedor { get; set; }
    public int IdPuesto { get; set; }
    public string Apoderado { get; set; } = string.Empty;
}

public class ActualizarProveedorApoderadoRequest : CrearProveedorApoderadoRequest
{
    public int IdStatus { get; set; } = 1;
}

public class CrearProveedorDomicilioRequest
{
    public int IdProveedor { get; set; }
    public int IdTipoDomicilio { get; set; }
    public int IdColonia { get; set; }
    public string Domicilio { get; set; } = string.Empty;
    public string CalleNumero { get; set; } = string.Empty;
}

public class ActualizarProveedorDomicilioRequest : CrearProveedorDomicilioRequest
{
    public int IdStatus { get; set; } = 1;
}

/// <summary>
/// Documento del proveedor.
///
/// NO LLEVA 'Escaneado' NI 'IdStatus' EN EL ALTA, y no es un olvido: el
/// repositorio de legacy le manda a sp_n_InsertarProveedorDocumento un
/// @IDStatus y un @Escaneado que el procedimiento NO TIENE. Leido de
/// sys.parameters en Produccion_svr, sus parametros son IDProveedor,
/// IDTipoDocumento, Documento, Archivo, ArchivoFtp, IDUsuario y Equipo, y nada
/// mas. Es decir que el alta de documentos de proveedor de MacServicios2 falla
/// en esta base antes de tocar la tabla. Aqui se manda lo que el procedimiento
/// pide, que es lo unico que puede funcionar.
/// </summary>
public class CrearProveedorDocumentoRequest
{
    public int IdProveedor { get; set; }
    public int IdTipoDocumento { get; set; }
    public string Documento { get; set; } = string.Empty;
    public string? Archivo { get; set; }
    public string? ArchivoFtp { get; set; }
}

public class ActualizarProveedorDocumentoRequest : CrearProveedorDocumentoRequest
{
    public int IdStatus { get; set; } = 1;
}
