namespace ISL_Service.Application.DTOs.Obra;

/*
  LOS CATALOGOS Y CONTRATOS DE OBRA (las tablas MC_* de Mac31).

  "MC" es el prefijo que legacy le puso a este modulo en la base (MC_Contratos,
  MC_Gerentes, MC_Proveedores...). No es un catalogo general: es obra publica —
  contratos, grupos, conceptos y estimaciones— y por eso vive en su propio
  modulo `obra` y no entre los desplegables de captura.

  DOS COSAS QUE NO SE HEREDAN DE LEGACY, Y ESTAN EN TODOS LOS DTO DE AQUI:

  1. FUERA LOS CAMPOS `*Ftm`. Los SP devuelven cada numero y cada fecha DOS
     veces: el valor y su texto ya formateado (`TotalConcepto` y
     `TotalConceptoFtm` con FORMAT(...,'###,###,##0.00'), `FechaInicio` y
     `FechaInicioFtm` con 'dd-MM-yyyy'). Eso lo armaba la base porque los
     combos y las rejillas de WinForms pintaban texto. Un front que recibe el
     numero lo formatea con la configuracion regional del que mira la pantalla;
     uno que recibe el texto ya no puede. Se manda el valor.

  2. FUERA `@Formato`. Es el interruptor "0 todos los campos / 1 Excel" de
     Mac31: con 1 el mismo SP devuelve OTRAS columnas, con los encabezados ya
     en espanol y con espacios ("[Tipo de Gerente]"). El Excel lo arma el front
     con los mismos datos; dos formas de la misma respuesta son dos contratos
     que se desincronizan.

  La auditoria SI se conserva en contratos, grupos, conceptos y estimaciones
  —a diferencia de los desplegables— porque ahi "quien cerro el contrato y
  cuando" es parte del documento, no ruido de captura.
*/

/// <summary>Un gerente de obra. IDStatus: 1 = Activo, 2 = Inactivo.</summary>
public class ObraGerenteDto
{
    public int IDGerente { get; set; }
    public int IDTipoGerente { get; set; }
    public int IDStatus { get; set; }
    public string TipoGerente { get; set; } = string.Empty;
    public string Gerente { get; set; } = string.Empty;
    public string Estatus { get; set; } = string.Empty;
}

/// <summary>Un puesto de obra. EsRepresentanteLegal/EsRepresentanteTecnico: 0/1.</summary>
public class ObraPuestoDto
{
    public int IDPuesto { get; set; }
    public int IDStatus { get; set; }
    public string Puesto { get; set; } = string.Empty;
    public string Estatus { get; set; } = string.Empty;
    public int EsRepresentanteLegal { get; set; }
    public int EsRepresentanteTecnico { get; set; }
}

/// <summary>Un residente. EsSupervision/EsObra: 0/1 — el mismo residente puede ser los dos.</summary>
public class ObraResidenteDto
{
    public int IDResidente { get; set; }
    public int IDPuesto { get; set; }
    public int IDStatus { get; set; }
    public string Residente { get; set; } = string.Empty;
    public string Puesto { get; set; } = string.Empty;
    public int EsSupervision { get; set; }
    public int EsObra { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Estatus { get; set; } = string.Empty;
}

/// <summary>
/// Renglon de los cuatro catalogos de obra que tienen la MISMA forma
/// (id, status, nombre, estatus): tipos de contratacion, de documento, de obra
/// y de recurso.
///
/// Un DTO por los cuatro y no cuatro DTO iguales: el nombre de la columna del
/// SP cambia (TipoObra, TipoRecurso...) pero el contrato que se publica no
/// tiene por que repetirlo cuatro veces — la ruta ya dice de que catalogo es.
/// Si manana uno de los cuatro gana una columna propia, se separa ESE.
/// </summary>
public class ObraTipoDto
{
    public int Id { get; set; }
    public int IDStatus { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Estatus { get; set; } = string.Empty;
}

/// <summary>Unidad de medida de obra. EsPorcentaje/SoloUnidades: 0/1.</summary>
public class ObraUnidadMedidaDto
{
    public int IDUnidadMedida { get; set; }
    public int IDStatus { get; set; }
    public string UnidadMedida { get; set; } = string.Empty;
    public string Estatus { get; set; } = string.Empty;
    public int EsPorcentaje { get; set; }
    public int SoloUnidades { get; set; }
}

/// <summary>
/// Un proveedor de obra (MC_Proveedores). NO es el catalogo de proveedores de
/// compras: son dos tablas distintas en la misma base.
/// </summary>
public class ObraProveedorDto
{
    public int IDProveedor { get; set; }
    public int IDStatus { get; set; }
    public int Numero { get; set; }
    public string ApellidoPaterno { get; set; } = string.Empty;
    public string ApellidoMaterno { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    /// <summary>"Paterno Materno Nombre", como lo arma el SP con LTRIM.</summary>
    public string NombreProveedor { get; set; } = string.Empty;
    public string? Rfc { get; set; }
    public string? Curp { get; set; }
    public string? Telefono { get; set; }
    public string? Celular { get; set; }
    public DateTime? FechaAlta { get; set; }
    public string? Email { get; set; }
    public string Estatus { get; set; } = string.Empty;
    public string? Observaciones { get; set; }
    public string? PaginaWeb { get; set; }
}

/// <summary>Un grupo de conceptos dentro de un contrato de obra.</summary>
public class ObraContratoGrupoDto
{
    public int IDContratoGrupo { get; set; }
    public int IDContrato { get; set; }
    public int IDStatus { get; set; }
    public string? Clave { get; set; }
    public string Grupo { get; set; } = string.Empty;
    /// <summary>Posicion dentro del contrato. El SP ordena por aqui.</summary>
    public int Orden { get; set; }
    public string Estatus { get; set; } = string.Empty;
}

/// <summary>
/// Un concepto (partida ejecutable) de un contrato de obra, con el contrato al
/// que pertenece repetido en cada renglon, como lo devuelve el SP.
/// </summary>
public class ObraContratoConceptoDto
{
    public int IDContratoConcepto { get; set; }
    public int IDContratoGrupo { get; set; }
    public int IDContrato { get; set; }
    public int IDUnidadMedida { get; set; }
    public int IDStatus { get; set; }
    public string Grupo { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;
    public string? Clave { get; set; }
    public string Concepto { get; set; } = string.Empty;
    public decimal PrecioUnitario { get; set; }
    public decimal CantidadConcepto { get; set; }
    public decimal TotalConcepto { get; set; }
    public int OrdenGrupo { get; set; }
    public int OrdenConcepto { get; set; }
    public string Estatus { get; set; } = string.Empty;

    // El contrato, tal como lo trae el SP pegado a cada concepto.
    public int IDProveedor { get; set; }
    public string NombreProveedor { get; set; } = string.Empty;
    public string Contrato { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? Ubicacion { get; set; }
    public DateTime? FechaContratacion { get; set; }
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaTermino { get; set; }
    public decimal Total { get; set; }
    public string? Observaciones { get; set; }
    public int NumeroEstimaciones { get; set; }

    /// <summary>1 = el contrato ya se cerro y no admite mas movimientos.</summary>
    public int ContratoCerrado { get; set; }
    public int IDUsuarioCierre { get; set; }
    public string? UsuarioCierre { get; set; }
    public DateTime? FechaCierre { get; set; }
    public string? EquipoCierre { get; set; }
    public int ContratoTerminado { get; set; }
    public DateTime? FechaRealTermino { get; set; }

    // Auditoria del concepto.
    public DateTime? Fecha { get; set; }
    public int IDUsuario { get; set; }
    public string? Usuario { get; set; }
    public string? Equipo { get; set; }
    public DateTime? FechaModificacion { get; set; }
    public int IDUsuarioModificacion { get; set; }
    public string? UsuarioModificacion { get; set; }
    public string? EquipoModificacion { get; set; }
}

/// <summary>
/// Una estimacion de obra: lo que se reclama cobrar de un contrato en un
/// periodo. Trae el contrato completo pegado, incluida la supervision externa.
/// </summary>
public class ObraContratoEstimacionDto
{
    public int IDContratoEstimacion { get; set; }
    public int IDContrato { get; set; }
    public int IDStatus { get; set; }
    public DateTime? FechaElaboracion { get; set; }
    public DateTime? FechaInicioEstimacion { get; set; }
    public DateTime? FechaTerminoEstimacion { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal TotalEstimacionProgramada { get; set; }
    public decimal TotalEstimacion { get; set; }
    public string Estatus { get; set; } = string.Empty;

    public int IDProveedor { get; set; }
    public string NombreProveedor { get; set; } = string.Empty;
    public int IDProveedorRepresentanteLegal { get; set; }
    public string? ProveedorRepresentanteLegal { get; set; }
    public int IDProveedorRepresentanteTecnico { get; set; }
    public string? ProveedorRepresentanteTecnico { get; set; }

    public string Contrato { get; set; } = string.Empty;
    public int ContratoCerrado { get; set; }
    public int IDUsuarioCierre { get; set; }
    public string? UsuarioCierre { get; set; }
    public DateTime? FechaCierre { get; set; }
    public string? EquipoCierre { get; set; }
    public string? Descripcion { get; set; }
    public string? Ubicacion { get; set; }
    public int Ejercicio { get; set; }

    // Supervision externa: otro contrato, con su propio proveedor.
    public int IDProveedorSupExt { get; set; }
    public string? NombreProveedorSupExt { get; set; }
    public int IDProveedorRepresentanteLegalSupExt { get; set; }
    public string? ProveedorRepresentanteLegalSupExt { get; set; }
    public int IDProveedorRepresentanteTecnicoSupExt { get; set; }
    public string? ProveedorRepresentanteTecnicoSupExt { get; set; }
    public int IDContratoSupervisionExterna { get; set; }
    public string? ContratoSupervisionExterna { get; set; }

    public DateTime? FechaContratacion { get; set; }
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaTermino { get; set; }
    public decimal Total { get; set; }
    public string? Observaciones { get; set; }

    public int ContratoTerminado { get; set; }
    public DateTime? FechaRealTermino { get; set; }
    public int IDContratoEstimacionUltima { get; set; }

    public DateTime? Fecha { get; set; }
    public int IDUsuario { get; set; }
    public string? Usuario { get; set; }
    public string? Equipo { get; set; }
    public DateTime? FechaModificacion { get; set; }
    public int IDUsuarioModificacion { get; set; }
    public string? UsuarioModificacion { get; set; }
    public string? EquipoModificacion { get; set; }
}

/// <summary>
/// Filtro comun de los catalogos de obra: los tres parametros que todos los
/// `sp_n_Consultar_MC*` aceptan igual.
/// </summary>
public class ObraCatalogoFiltro
{
    /// <summary>Un id concreto. 0 = todos.</summary>
    public int Id { get; set; }
    /// <summary>0 = todos, 1 = activos, 2 = inactivos. Lo resuelve fn_RegresaTablaStatus.</summary>
    public int IDStatus { get; set; }
    /// <summary>Texto a buscar en el nombre.</summary>
    public string? Texto { get; set; }
    /// <summary>true = igualdad exacta; false (default) = LIKE %texto%.</summary>
    public bool Exacto { get; set; }
}
