namespace ISL_Service.Application.DTOs.CatalogosComplementos;

/*
  LOS CATALOGOS QUE NO ESTABAN EN LA PRIMERA OLA.

  Son siete lecturas de catalogo que en legacy NO viven bajo /api/cat*
  (`/api/marcas/consultar`, `/api/tiposdomicilio/consultar`,
  `/api/empresas/series/consultar`) o que lo hacen con otra caja
  (`/api/CatCondicionesCredito`, `/api/CatFEFormasPago`,
  `/api/CatFEMetodosPago`, `/api/CatFEMotivosCancelacion`).

  POR QUE VIVEN EN api/catalogos Y NO EN UN MODULO NUEVO
  -----------------------------------------------------
  Son exactamente lo mismo que los 28 de la ola 1: desplegables de captura. Un
  segundo prefijo para desplegables obligaria al front a saber de cuales de los
  dos lados pedir cada combo, y ese reparto no tiene ninguna logica de negocio —
  solo refleja en que orden se migraron. Van al mismo prefijo, con el mismo
  permiso, en archivo aparte para que las dos entregas no se pisen.

  CINCO DE LOS SIETE NO TENIAN DTO EN LEGACY
  ------------------------------------------
  `tiposdomicilio`, `empresas/series` y los tres `CatFE*` devolvian
  `GenericResponse(List<DataRow>)`, o sea una lista de `System.Data.DataRow`
  serializada por Newtonsoft: lo que salia por el cable no eran las columnas,
  eran las propiedades publicas de DataRow (`ItemArray`, `Table`, `RowState`,
  `HasErrors`...). El cliente que lo consumiera tenia que leer `itemArray` por
  POSICION. Aqui se declara el DTO con las columnas que el SP devuelve de
  verdad, leidas de su definicion. Es una diferencia deliberada y es una mejora.
*/

/// <summary>Tipo de domicilio ([Catalogo Tipos Domicilio]).</summary>
public class TipoDomicilioDto
{
    public int IDTipoDomicilio { get; set; }
    public int IDStatus { get; set; }
    public string TipoDomicilio { get; set; } = string.Empty;
    public string Estatus { get; set; } = string.Empty;
}

/// <summary>Condicion de credito: el plazo que se le concede a un cliente.</summary>
public class CondicionCreditoDto
{
    public int IDCondicionCredito { get; set; }
    public int IDStatus { get; set; }
    public string CondicionCredito { get; set; } = string.Empty;
    public int DiasCredito { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>Una serie de folios de una empresa, con la empresa a la que pertenece.</summary>
public class EmpresaSerieDto
{
    public int IDEmpresaSerie { get; set; }
    public int IDEmpresa { get; set; }
    public int IDStatus { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Folio { get; set; }
    public string? RazonSocial { get; set; }
    public string? NombreCorto { get; set; }
    public string? Rfc { get; set; }
    public string Status { get; set; } = string.Empty;
    /// <summary>
    /// Id de la empresa en Facturama. Es TEXTO, no un numero: la columna
    /// `Empresas.IDFacturama` es varchar y trae un token como
    /// "dwySOmFMFMoLpfwWbLem7A2". Declararlo int reventaba la consulta completa
    /// con "The input string ... was not in a correct format", y el nombre
    /// "ID" invita a equivocarse, de ahi esta nota.
    /// </summary>
    public string? IDFacturama { get; set; }
}

/// <summary>
/// Un renglon de los tres catalogos del SAT que tienen la MISMA forma: formas
/// de pago, metodos de pago y motivos de cancelacion.
///
/// Un DTO para los tres, y no tres iguales, porque lo unico que cambia es el
/// nombre de la columna del id y del texto (IDFEFormaPago/FEFormaPago,
/// IDFEMetodoPago/FEMetodoPago, IDFEMotivoCancelacion/FEMotivo) y la ruta ya
/// dice de cual se trata.
/// </summary>
public class CatalogoFeDto
{
    public int Id { get; set; }
    public int IDStatus { get; set; }
    /// <summary>La clave oficial del SAT ("03", "PUE", "01").</summary>
    public string FEClave { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Una marca de producto. Las columnas `Columna*` son el nombre de la columna
/// que esa marca ocupa en los reportes de concentrado, plantilla e inventario:
/// son datos de presentacion que viven en el catalogo desde Mac31.
/// </summary>
public class MarcaCatalogoDto
{
    public int IDMarca { get; set; }
    public int IDStatus { get; set; }
    public string Marca { get; set; } = string.Empty;
    public string? MarcaCorto { get; set; }
    public string? ColumnaConcentrado { get; set; }
    public string? ColumnaTemplate { get; set; }
    public string? ColumnaInventario { get; set; }
    /// <summary>Posicion en la lista de precios. El SP la deja en 20 cuando esta nula.</summary>
    public int OrdenListaPrecios { get; set; }
    public string Status { get; set; } = string.Empty;
}
