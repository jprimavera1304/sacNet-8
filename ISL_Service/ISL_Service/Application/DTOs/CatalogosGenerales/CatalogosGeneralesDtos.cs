/*
  CATALOGOS GENERALES DE MAC31: POR QUE TODOS LOS DTO VIVEN EN UN SOLO ARCHIVO.

  Son 28 catalogos de SOLO LECTURA que en Legacy (MacServicios2) estaban repartidos en 28
  controladores, 28 servicios, 28 repositorios y ~80 archivos de DTO, todos copia-pega del
  mismo cuerpo. Aqui son 28 listas planas sin reglas de negocio: partirlas en 28 carpetas
  reproduciria el problema que la migracion viene a quitar. La convencion del proyecto ya
  admite agrupar los DTO de un modulo grande en un solo archivo
  (ver Application/DTOs/VentasMultiPago/VentasMultiPagoDtos.cs).

  QUE SE DEJO FUERA A PROPOSITO, EN TODOS ELLOS:
  * `result` y `mensaje`. Legacy los pega a CADA renglon de CADA catalogo con valor 0/null:
    eran el hueco donde sus SP de escritura devolvian el codigo de error. En una consulta no
    significan nada y el sobre { ok, message, data } ya dice si la llamada salio.
  * Los campos de auditoria (IDUsuarioAlta, FechaAlta, EquipoAlta, ...). Un desplegable no
    necesita saber quien dio de alta el renglon, y son datos de personal.
  * Los textos ya concatenados (`NumeroNombreAgente`, `FEDescripcion` donde no hace falta).
    Legacy los armaba en la base porque Mac31 pintaba combos de WinForms; el front nuevo
    concatena en su capa.

  LOS TEXTOS VIENEN RECORTADOS. Las columnas son `char(n)` en varias tablas de catalogo, asi
  que Legacy devuelve "AGUASCALIENTES      " con el relleno adentro. Se recorta aqui: el
  relleno es un detalle del tipo de la columna, no un dato.
*/

namespace ISL_Service.Application.DTOs.CatalogosGenerales;

/// <summary>Numero de agente libre para dar de alta. sp_n_ConsultaAgentesDisponibles.</summary>
public class AgenteDisponibleDto
{
    public int NumeroAgente { get; set; }
}

/// <summary>Caja de cobro. sp_n_ConsultaCajas.</summary>
public class CajaDto
{
    public int IdCaja { get; set; }
    /// <summary>1 = Activo, 2 = Inactivo</summary>
    public int IdStatus { get; set; }
    public int NumCaja { get; set; }
    public string NombreCaja { get; set; } = string.Empty;
    public string EquipoCaja { get; set; } = string.Empty;
    /// <summary>1 cuando la caja es la maestra del equipo.</summary>
    public int MaestraCaja { get; set; }
    public int IdColonia { get; set; }
    public string Direccion { get; set; } = string.Empty;
}

/// <summary>Centro de servicio. sp_n_ConsultaCentrosServicio.</summary>
public class CentroServicioDto
{
    public int IdCentro { get; set; }
    /// <summary>1 = Activo, 2 = Inactivo</summary>
    public int IdStatus { get; set; }
    public string BaseCentro { get; set; } = string.Empty;
    public string NombreCentro { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

/// <summary>Ciudad del catalogo territorial. sp_n_ConsultaCiudades.</summary>
public class CiudadDto
{
    public string IdEstado { get; set; } = string.Empty;
    public string IdCiudad { get; set; } = string.Empty;
    public string Ciudad { get; set; } = string.Empty;
}

/// <summary>Colonia con su jerarquia territorial resuelta. sp_n_ConsultaColonias.</summary>
public class ColoniaDto
{
    public int IdColonia { get; set; }
    public string IdEstado { get; set; } = string.Empty;
    public string IdCiudad { get; set; } = string.Empty;
    public string IdMunicipio { get; set; } = string.Empty;
    public string IdAsentamiento { get; set; } = string.Empty;
    public string Colonia { get; set; } = string.Empty;
    public string CodigoPostal { get; set; } = string.Empty;
    public string DelegacionMunicipio { get; set; } = string.Empty;
    public string Ciudad { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string Asentamiento { get; set; } = string.Empty;
}

/// <summary>Concepto de recaudacion. sp_n_ConsultaConceptos.</summary>
public class ConceptoDto
{
    public int IdConcepto { get; set; }
    /// <summary>1 = Activo, 2 = Cancelado</summary>
    public int IdStatus { get; set; }
    public string NumConcepto { get; set; } = string.Empty;
    public string NombreConcepto { get; set; } = string.Empty;
    public string Estatus { get; set; } = string.Empty;
}

/// <summary>Documento requerido. sp_n_ConsultaDocumentos.</summary>
public class DocumentoDto
{
    public int IdDocumento { get; set; }
    /// <summary>1 = Activo, 2 = Inactivo</summary>
    public int IdStatus { get; set; }
    public string Documento { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    /// <summary>1 cuando el documento es obligatorio.</summary>
    public int Obligatorio { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>Auxiliar de una recaudacion por economico. sp_n_ConsultaEconomicosAuxiliares.</summary>
public class EconomicoAuxiliarDto
{
    public int IdEconomicoAuxiliar { get; set; }
    public int IdRecaudacion { get; set; }
    /// <summary>1 = Activo, 2 = Cancelado</summary>
    public int IdStatus { get; set; }
    public string Estatus { get; set; } = string.Empty;
    /// <summary>1 cuando el auxiliar ya se pago.</summary>
    public int Pagado { get; set; }
    public int Folio { get; set; }
    public int IdPermisionario { get; set; }
    public string NumPermisionario { get; set; } = string.Empty;
    public string NombrePermisionario { get; set; } = string.Empty;
    public int IdEconomico { get; set; }
    public int NumEconomico { get; set; }
    public string NombreEconomico { get; set; } = string.Empty;
    public int IdCuenta { get; set; }
    public string NumCuenta { get; set; } = string.Empty;
    public string NombreCuenta { get; set; } = string.Empty;
    public int IdConcepto { get; set; }
    public string NumConcepto { get; set; } = string.Empty;
    public string NombreConcepto { get; set; } = string.Empty;
    public decimal ImporteCuenta { get; set; }
    public string DebeHaber { get; set; } = string.Empty;
    public int IdCaja { get; set; }
    public int NumCaja { get; set; }
}

/// <summary>Estado de la republica. sp_n_ConsultaEstados.</summary>
public class EstadoDto
{
    public string IdEstado { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
}

/// <summary>Regimen fiscal del SAT. sp_n_ConsultaCatFERegimenesFiscales.</summary>
public class FeRegimenFiscalDto
{
    public int IdFeRegimenFiscal { get; set; }
    /// <summary>1 = Activo, 2 = Inactivo</summary>
    public int IdStatus { get; set; }
    public string FeClave { get; set; } = string.Empty;
    public string FeRegimenFiscal { get; set; } = string.Empty;
    /// <summary>1 cuando aplica a persona fisica.</summary>
    public int FeFisica { get; set; }
    /// <summary>1 cuando aplica a persona moral.</summary>
    public int FeMoral { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>Tipo de relacion entre CFDI. sp_n_ConsultaCatFERelacionFacturas.</summary>
public class FeRelacionFacturaDto
{
    public int IdFeRelacionFactura { get; set; }
    /// <summary>1 = Activo, 2 = Inactivo</summary>
    public int IdStatus { get; set; }
    public string FeClave { get; set; } = string.Empty;
    public string FeRelacionFactura { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

/// <summary>Uso de CFDI del SAT. sp_n_ConsultaCatFEUsosCFDI.</summary>
public class FeUsoCfdiDto
{
    public int IdFeUsoCfdi { get; set; }
    /// <summary>1 = Activo, 2 = Inactivo</summary>
    public int IdStatus { get; set; }
    public string FeClave { get; set; } = string.Empty;
    public string FeUsoCfdi { get; set; } = string.Empty;
    /// <summary>1 cuando aplica a persona fisica.</summary>
    public int FeFisica { get; set; }
    /// <summary>1 cuando aplica a persona moral.</summary>
    public int FeMoral { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>Gallero con su saldo. sp_n_ConsultaGalleros.</summary>
public class GalleroDto
{
    public int IdGallero { get; set; }
    /// <summary>1 = Activo, 2 = Inactivo</summary>
    public int IdStatus { get; set; }
    public int Numero { get; set; }
    public string Gallero { get; set; } = string.Empty;
    public decimal Saldo { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>Grupo al que pertenece un cliente. sp_n_ConsultaCatGruposCliente.</summary>
public class GrupoClienteDto
{
    public int IdGrupoCliente { get; set; }
    /// <summary>1 = Activo, 2 = Inactivo</summary>
    public int IdStatus { get; set; }
    public string GrupoCliente { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

/// <summary>Municipio del catalogo territorial. sp_n_ConsultaMunicipios.</summary>
public class MunicipioDto
{
    public string IdEstado { get; set; } = string.Empty;
    public string IdMunicipio { get; set; } = string.Empty;
    public string Municipio { get; set; } = string.Empty;
}

/// <summary>Puesto de un empleado. sp_n_ConsultaPuestos.</summary>
public class PuestoDto
{
    public int IdPuesto { get; set; }
    /// <summary>1 = Activo, 2 = Inactivo</summary>
    public int IdStatus { get; set; }
    public string Puesto { get; set; } = string.Empty;
    /// <summary>1 cuando el puesto cuenta como agente de ventas.</summary>
    public int EsAgente { get; set; }
}

/// <summary>Motivo de una nota de credito. sp_n_ConsultaTipoNotasCredito.</summary>
public class TipoNotaCreditoDto
{
    public int IdTipoNotaCredito { get; set; }
    /// <summary>1 = Activo, 2 = Inactivo</summary>
    public int IdStatus { get; set; }
    public string TipoNotaCredito { get; set; } = string.Empty;
}

/// <summary>Forma de pago de la cobranza. sp_n_ConsultaTiposPagos.</summary>
public class TipoPagoDto
{
    public int IdTipoPago { get; set; }
    /// <summary>1 = Activo, 2 = Inactivo</summary>
    public int IdStatus { get; set; }
    public string TipoPago { get; set; } = string.Empty;
    public string TipoPagoCorto { get; set; } = string.Empty;
    /// <summary>1 cuando el tipo resta en vez de sumar.</summary>
    public int EsDescuento { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>Tipo de sueldo de nomina. sp_n_ConsultaTipoSueldos.</summary>
public class TipoSueldoDto
{
    public int IdTipoSueldo { get; set; }
    /// <summary>1 = Activo, 2 = Inactivo</summary>
    public int IdStatus { get; set; }
    public string TipoSueldo { get; set; } = string.Empty;
    /// <summary>1 cuando el sueldo se paga por comision.</summary>
    public int EsComision { get; set; }
}

/// <summary>Tipo de documento de venta. sp_n_ConsultaCatTiposDocumentos.</summary>
public class TipoDocumentoDto
{
    public int IdTipoDocumento { get; set; }
    public string Documento { get; set; } = string.Empty;
    public string NombreDocumento { get; set; } = string.Empty;
    public string NombreDocumentoCorto { get; set; } = string.Empty;
    /// <summary>1 cuando el tipo se ofrece en pantalla.</summary>
    public int MostrarPantalla { get; set; }
}

/// <summary>Tipo de recordatorio. sp_n_ConsultaTiposRecordatorios.</summary>
public class TipoRecordatorioDto
{
    public int IdTipoRecordatorio { get; set; }
    /// <summary>1 = Activo, 2 = Inactivo</summary>
    public int IdStatus { get; set; }
    public string TipoRecordatorio { get; set; } = string.Empty;
    public int EsUrgente { get; set; }
    public int EsImportante { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>Unidad de medida de un producto o servicio. sp_n_ConsultaUnidadesServicio.</summary>
public class UnidadMedidaDto
{
    public int IdUnidadMedida { get; set; }
    /// <summary>1 = Activo, 2 = Inactivo</summary>
    public int IdStatus { get; set; }
    public string UnidadMedida { get; set; } = string.Empty;
    public int EsPieza { get; set; }
    public int EsCaja { get; set; }
    public int EsServicio { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>Unidad territorial del SAT. sp_n_ConsultaCatUnidadesTerritoriales.</summary>
public class UnidadTerritorialDto
{
    public int IdUnidadTerritorial { get; set; }
    /// <summary>1 = Activo, 2 = Inactivo</summary>
    public int IdStatus { get; set; }
    public string Clave { get; set; } = string.Empty;
    public string Denominacion { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

/// <summary>Marca frente a una categoria, con la palomita de la relacion. sp_n_ConsultaCategoriasMarcas.</summary>
public class CategoriaMarcaDto
{
    public int IdCategoria { get; set; }
    public int IdMarca { get; set; }
    public string Categoria { get; set; } = string.Empty;
    public string Marca { get; set; } = string.Empty;
    /// <summary>1 cuando la marca esta relacionada con la categoria.</summary>
    public int Marcado { get; set; }
}

/// <summary>
/// Marca frente a una categoria, con el grupo de categoria arriba.
/// sp_n_ConsultaGrupoCategoriasMarcasRelacion.
/// </summary>
public class GrupoCategoriaMarcaDto
{
    public int IdGrupoCategoria { get; set; }
    public int IdCategoria { get; set; }
    public int IdMarca { get; set; }
    public string GrupoCategoria { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string Marca { get; set; } = string.Empty;
    /// <summary>1 cuando la marca esta relacionada con la categoria.</summary>
    public int Marcado { get; set; }
}

/// <summary>Forma (pantalla) de Mac31. sp_n_ConsultaFormas.</summary>
public class FormaDto
{
    public int IdForma { get; set; }
    public string FormaNombre { get; set; } = string.Empty;
    public string FormaDescripcion { get; set; } = string.Empty;
    public int FormaOrden { get; set; }
}
