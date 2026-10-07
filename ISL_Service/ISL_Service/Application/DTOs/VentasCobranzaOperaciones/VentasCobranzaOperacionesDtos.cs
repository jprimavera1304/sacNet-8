namespace ISL_Service.Application.DTOs.VentasCobranzaOperaciones;

/*
  COBRANZA, CONCENTRADO Y COBRO: LAS TRES PANTALLAS DE LA RUTA

  Una COBRANZA es el paquete de remisiones que sale con un repartidor.
  Un CONCENTRADO es lo mismo pero para devoluciones.
  Un COBRO es lo que el repartidor regresa: el dinero y los cascos.

  Las tres comparten DTO en legacy (uno por controlador, con los campos de las
  tres operaciones juntos) y las tres mandan las listas de ids como una CADENA
  ya pegada por el cliente. Eso significa que el cliente tiene que saber que el
  separador es la virgulilla, y si se equivoca el procedimiento no falla: parte
  en un solo pedazo, no encuentra el id y no agrega nada. Aqui las listas son
  listas y el pegado lo hace el repositorio.

  `IdUsuario` y `Equipo` no estan en ningun request: salen del token.
*/

/// <summary>Crea una cobranza con las remisiones que lleva el repartidor.</summary>
public class CobranzaCrearRequest
{
    public int IdRepartidor { get; set; }

    /// <summary>Las remisiones que entran a la cobranza.</summary>
    public List<int> IdsVentaAgregar { get; set; } = new();

    public string? Observaciones { get; set; }

    /// <summary>
    /// El cobro al que pertenece, cuando la cobranza se crea desde la pantalla
    /// de cobro. 0 cuando se crea sola.
    /// </summary>
    public int IdCobro { get; set; }

    /// <summary>
    /// 1 cuando la llamada viene de la pantalla de cobro. El procedimiento la
    /// usa para decidir si crea el cobro ademas de la cobranza.
    /// </summary>
    public int EsCobro { get; set; }

    public int IdAlmacen { get; set; }
}

/// <summary>Modifica una cobranza: quita, devuelve y agrega remisiones.</summary>
public class CobranzaActualizarRequest
{
    public int IdCobranza { get; set; }

    /// <summary>Renglones que se sacan de la cobranza.</summary>
    public List<int> IdsDetalleEliminar { get; set; } = new();

    /// <summary>
    /// Renglones que se marcan como DEVUELTOS. No es lo mismo que eliminarlos:
    /// una remision devuelta sigue en la cobranza con su historia, una
    /// eliminada sale como si nunca hubiera entrado. El concentrado no tiene
    /// esta distincion.
    /// </summary>
    public List<int> IdsDetalleDevolver { get; set; } = new();

    public List<int> IdsVentaAgregar { get; set; } = new();

    public int IdRepartidor { get; set; }

    public string? Observaciones { get; set; }
}

/// <summary>Cancela una o varias cobranzas.</summary>
public class CobranzaCancelarRequest
{
    public List<int> IdsCobranza { get; set; } = new();

    /// <summary>Los cobros que se cancelan con ellas, si los hay.</summary>
    public List<int> IdsCobro { get; set; } = new();
}

/// <summary>Filtros de la pantalla de cobranzas.</summary>
public class CobranzaConsultaRequest
{
    public int IdCobranza { get; set; }
    public int IdRepartidor { get; set; }
    public int IdUsuarioCobranza { get; set; }
    public int FolioInicialCobranza { get; set; }
    public int FolioFinalCobranza { get; set; }
    public DateTime? FechaInicialCobranza { get; set; }
    public DateTime? FechaFinalCobranza { get; set; }
    public int IdEmpresaVenta { get; set; }
    public int FolioInicialVenta { get; set; }
    public int FolioFinalVenta { get; set; }
}

/// <summary>Crea un concentrado de devoluciones.</summary>
public class ConcentradoCrearRequest
{
    public int IdRepartidor { get; set; }
    public List<int> IdsVentaAgregar { get; set; } = new();
    public string? Observaciones { get; set; }
    public int IdCobro { get; set; }
    public int EsCobro { get; set; }
    public int IdAlmacen { get; set; }

    /// <summary>1 para armar el concentrado solo con las remisiones devueltas.</summary>
    public int SoloDevueltas { get; set; }
}

/// <summary>Modifica un concentrado: quita y agrega remisiones.</summary>
public class ConcentradoActualizarRequest
{
    public int IdConcentrado { get; set; }
    public List<int> IdsDetalleEliminar { get; set; } = new();
    public List<int> IdsVentaAgregar { get; set; } = new();
    public int IdRepartidor { get; set; }
    public string? Observaciones { get; set; }
}

/// <summary>Cancela uno o varios concentrados.</summary>
public class ConcentradoCancelarRequest
{
    public List<int> IdsConcentrado { get; set; } = new();
    public List<int> IdsCobro { get; set; } = new();
}

/// <summary>
/// Abre, cierra o cancela cobros. Las tres operaciones reciben lo mismo; cerrar
/// acepta ademas unas observaciones.
/// </summary>
public class CobroLoteRequest
{
    public List<int> IdsCobro { get; set; } = new();

    /// <summary>
    /// Los folios, cuando la pantalla trabaja por folio y no por id. El
    /// procedimiento acepta cualquiera de las dos listas.
    /// </summary>
    public List<int> FoliosCobro { get; set; } = new();

    /// <summary>Solo lo usa cerrar.</summary>
    public string? Observaciones { get; set; }
}

/// <summary>Reasigna un cobro a otro concentrado o cobranza, o cambia su nota.</summary>
public class CobroActualizarRequest
{
    public int IdCobro { get; set; }
    public int IdConcentrado { get; set; }
    public int IdCobranza { get; set; }
    public string? Observaciones { get; set; }
}

/// <summary>Filtros de la pantalla de cobros.</summary>
public class CobroConsultaRequest
{
    public int IdCobro { get; set; }
    public int IdCliente { get; set; }
    public int IdRepartidor { get; set; }
    public int IdUsuarioCobro { get; set; }
    public int FolioInicialCobro { get; set; }
    public int FolioFinalCobro { get; set; }
    public DateTime? FechaInicialCobro { get; set; }
    public DateTime? FechaFinalCobro { get; set; }
    public int IdEmpresaVenta { get; set; }
    public int FolioInicialVenta { get; set; }
    public int FolioFinalVenta { get; set; }
}

/// <summary>
/// Los pendientes y vencidos de la cobranza.
///
/// Siete de los catorce parametros del procedimiento NO se ofrecen: legacy los
/// pisa con una constante justo antes de llamar (Status=4 "pendientes y
/// vencidos", Formato=1 "detallado", y cinco mas en cero) sin importar lo que
/// mande el cliente. Ofrecer un filtro que el servidor va a ignorar es ofrecer
/// algo que no existe, asi que aqui solo estan los cuatro que de verdad
/// cambian el resultado.
/// </summary>
public class CobranzaPendientesVencidosRequest
{
    public int IdEmpresa { get; set; }

    /// <summary>La fecha de corte del saldo. Obligatoria.</summary>
    public DateTime? FechaOperacion { get; set; }

    public DateTime? FechaInicial { get; set; }
    public DateTime? FechaFinal { get; set; }

    /// <summary>Como agrupa el reporte. Es el unico "formato" que el cliente decide.</summary>
    public int FormatoMostrar { get; set; }

    public int IdAgente { get; set; }
    public int IdCliente { get; set; }
}
