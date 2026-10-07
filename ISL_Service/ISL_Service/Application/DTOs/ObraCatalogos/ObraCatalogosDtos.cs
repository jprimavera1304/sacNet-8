namespace ISL_Service.Application.DTOs.ObraCatalogos;

/*
  LAS ALTAS Y MODIFICACIONES DE LOS CATALOGOS DE OBRA (MC).

  Un DTO por FORMA, no por nombre de columna. Cuatro de estos catalogos —tipos de
  contratacion, de documento, de obra y de recurso— tienen exactamente el mismo
  renglon (un id, un status y un nombre) y lo unico que cambiaba entre ellos era
  como se llama la columna en la base (IDTipoObra/TipoObra). La ruta ya dice de
  cual se trata, asi que comparten DTO. Es la misma decision que tomo la ola 3
  para sus lecturas.

  LO QUE NO ESTA, Y POR QUE:

  * IDUsuario y Equipo. Son la firma de quien hizo el cambio y desde donde.
    Legacy los pedia EN EL CUERPO (MCPuestoInputDTO.IDUsuario), de modo que
    cualquiera podia dar de alta un puesto a nombre de otro. Aqui salen del
    token.
  * IDStatus en las altas. Los SP de alta no lo reciben: lo fijan en 1 ellos
    mismos (sp_n_Insertar_MCPuesto: `SELECT @IDStatus = 1`). Ofrecerlo seria
    mentir.
  * Formato e Identico. Son filtros de consulta, no de escritura; Legacy los
    arrastraba en el mismo DTO porque usaba uno solo para las tres operaciones.
*/

/// <summary>
/// Alta de un catalogo de obra cuyo renglon es solo un nombre:
/// tipos de contratacion, tipos de documento, tipos de obra, tipos de recurso.
/// </summary>
public class CrearObraCatalogoSimpleRequest
{
    /// <summary>El nombre del renglon. Requerido.</summary>
    public string Nombre { get; set; } = string.Empty;
}

/// <summary>
/// Modificacion de un catalogo de obra cuyo renglon es solo un nombre.
/// </summary>
public class ActualizarObraCatalogoSimpleRequest
{
    public string Nombre { get; set; } = string.Empty;

    /// <summary>1 = Activo, 2 = Inactivo.</summary>
    public int IdStatus { get; set; } = 1;
}

/// <summary>Alta de un puesto de obra.</summary>
public class CrearObraPuestoRequest
{
    public string Puesto { get; set; } = string.Empty;

    /// <summary>El puesto firma como representante legal del contratista.</summary>
    public bool EsRepresentanteLegal { get; set; }

    /// <summary>El puesto firma como representante tecnico.</summary>
    public bool EsRepresentanteTecnico { get; set; }
}

/// <summary>Modificacion de un puesto de obra.</summary>
public class ActualizarObraPuestoRequest : CrearObraPuestoRequest
{
    /// <summary>1 = Activo, 2 = Inactivo.</summary>
    public int IdStatus { get; set; } = 1;
}

/// <summary>Alta de un residente de obra.</summary>
public class CrearObraResidenteRequest
{
    /// <summary>El puesto con el que firma. Requerido.</summary>
    public int IdPuesto { get; set; }

    public string Residente { get; set; } = string.Empty;

    /// <summary>Puede ser residente de supervision.</summary>
    public bool EsSupervision { get; set; }

    /// <summary>Puede ser residente de obra.</summary>
    public bool EsObra { get; set; }
}

/// <summary>Modificacion de un residente de obra.</summary>
public class ActualizarObraResidenteRequest : CrearObraResidenteRequest
{
    /// <summary>1 = Activo, 2 = Inactivo.</summary>
    public int IdStatus { get; set; } = 1;
}

/// <summary>Alta de un gerente de obra.</summary>
public class CrearObraGerenteRequest
{
    /// <summary>Tipo de gerente. Requerido.</summary>
    public int IdTipoGerente { get; set; }

    public string Gerente { get; set; } = string.Empty;
}

/// <summary>Modificacion de un gerente de obra.</summary>
public class ActualizarObraGerenteRequest : CrearObraGerenteRequest
{
    /// <summary>1 = Activo, 2 = Inactivo.</summary>
    public int IdStatus { get; set; } = 1;
}

/// <summary>Alta de una unidad de medida de obra.</summary>
public class CrearObraUnidadMedidaRequest
{
    public string UnidadMedida { get; set; } = string.Empty;

    /// <summary>
    /// La unidad solo admite cantidades enteras (piezas, lotes), no fracciones.
    /// </summary>
    public bool SoloUnidades { get; set; }
}

/// <summary>Modificacion de una unidad de medida de obra.</summary>
public class ActualizarObraUnidadMedidaRequest : CrearObraUnidadMedidaRequest
{
    /// <summary>1 = Activo, 2 = Inactivo.</summary>
    public int IdStatus { get; set; } = 1;
}

/*
  LA PARTIDA PRESUPUESTAL: DIECISEIS CLAVES Y UN MONTO.

  Una partida del clasificador presupuestal se escribe como una cadena de
  claves (FI-F-SF-AI-PP-FF-FG-FE-AD-OR-PTDA-TG-DI-DG-PY). Los SP las reciben
  una por una, como varchar, porque son claves con ceros a la izquierda y no
  numeros. Se respeta: convertirlas a int perderia el "01".
*/

/// <summary>Alta de una partida presupuestal de obra.</summary>
public class CrearObraPartidaRequest
{
    public string Partida { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    /// <summary>El monto autorizado de la partida.</summary>
    public decimal Monto { get; set; }

    /// <summary>Finalidad.</summary>
    public string? PartidaFi { get; set; }
    /// <summary>Funcion.</summary>
    public string? PartidaF { get; set; }
    /// <summary>Subfuncion.</summary>
    public string? PartidaSf { get; set; }
    /// <summary>Actividad institucional.</summary>
    public string? PartidaAi { get; set; }
    /// <summary>Programa presupuestario.</summary>
    public string? PartidaPp { get; set; }
    /// <summary>Fuente de financiamiento.</summary>
    public string? PartidaFf { get; set; }
    /// <summary>Fondo general.</summary>
    public string? PartidaFg { get; set; }
    /// <summary>Fondo especifico.</summary>
    public string? PartidaFe { get; set; }
    /// <summary>Area digito.</summary>
    public string? PartidaAd { get; set; }
    /// <summary>Origen del recurso.</summary>
    public string? PartidaOr { get; set; }
    /// <summary>Partida presupuestal.</summary>
    public string? PartidaPtda { get; set; }
    /// <summary>Tipo de gasto.</summary>
    public string? PartidaTg { get; set; }
    /// <summary>Digito identificador.</summary>
    public string? PartidaDi { get; set; }
    /// <summary>Digito de grupo.</summary>
    public string? PartidaDg { get; set; }
    /// <summary>Proyecto.</summary>
    public string? PartidaPy { get; set; }
}

/// <summary>Modificacion de una partida presupuestal de obra.</summary>
public class ActualizarObraPartidaRequest : CrearObraPartidaRequest
{
    /// <summary>1 = Activo, 2 = Inactivo.</summary>
    public int IdStatus { get; set; } = 1;
}

/// <summary>Filtros de la consulta de partidas presupuestales.</summary>
public class ObraPartidasFiltro
{
    public int IdPartida { get; set; }
    public int IdStatus { get; set; }
    public string? Partida { get; set; }

    /// <summary>true = el nombre tiene que coincidir completo, no por prefijo.</summary>
    public bool Exacto { get; set; }
}

/// <summary>Alta de un proveedor de obra (padron de contratistas).</summary>
public class CrearObraProveedorRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string? ApellidoPaterno { get; set; }
    public string? ApellidoMaterno { get; set; }
    public string? Rfc { get; set; }
    public string? Curp { get; set; }
    public string? Telefono { get; set; }
    public string? Celular { get; set; }
    public string? Email { get; set; }
    public string? PaginaWeb { get; set; }
    public string? Observaciones { get; set; }

    // --- El resto del expediente del padron ---
    public string? NumeroRegistro { get; set; }
    public string? NumeroRegistro2 { get; set; }
    public string? ActaConstitutivaEscritura { get; set; }
    public DateOnly? ActaConstitutivaFecha { get; set; }
    public string? ActaConstitutivaNotaria { get; set; }
    public string? ActaConstitutivaModificaciones { get; set; }
    public string? Imss { get; set; }
    public string? Infonavit { get; set; }
    public decimal CapitalContable { get; set; }
    public string? Especialidades { get; set; }
    public string? EspecialidadesOtras { get; set; }
    public DateOnly? FechaInscripcion { get; set; }
    public string? ApoderadoNombre { get; set; }
    public string? ApoderadoEscritura { get; set; }
    public DateOnly? ApoderadoFecha { get; set; }
    public string? ApoderadoNotaria { get; set; }
    public string? CadenaOriginal { get; set; }
    public string? SelloDigital { get; set; }
}

/// <summary>Modificacion de un proveedor de obra.</summary>
public class ActualizarObraProveedorRequest : CrearObraProveedorRequest
{
    /// <summary>1 = Activo, 2 = Inactivo.</summary>
    public int IdStatus { get; set; } = 1;
}
