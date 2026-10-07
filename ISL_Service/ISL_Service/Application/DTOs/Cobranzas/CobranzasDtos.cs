namespace ISL_Service.Application.DTOs.Cobranzas;

/*
  COBRANZAS: EL FAJO DE REMISIONES QUE SE LLEVA UN REPARTIDOR A COBRAR.

  Una cobranza es un folio con un repartidor y una lista de ventas. Se arma
  (InsertarCobranzaDetalle), se le quitan o se le devuelven renglones
  (ActualizarCobranzaDetalle) y se cancela entera (CancelarCobranza).

  Los cuatro sp_n_ de este modulo SI estan en Produccion_svr con la firma que
  espera Legacy, asi que esto se compara.

  LO QUE NO VIAJA EN EL CUERPO
  ----------------------------
  sp_n_ConsultarCobranzas recibe @IDUsuarioActual, y los tres de escritura reciben
  @IDUsuario / @IDUsuarioCancelo y @Equipo / @EquipoCancelo. Legacy los pedia en el
  cuerpo de la peticion: con eso, cualquiera podia cancelar la cobranza de otro y
  dejar su nombre en la bitacora. Todos salen del token.
*/

/// <summary>Filtros de la consulta de cobranzas.</summary>
public class CobranzasFiltro
{
    public int IdCobranza { get; set; }
    public int IdRepartidor { get; set; }

    /// <summary>El usuario que armo la cobranza. 0 = cualquiera.</summary>
    public int IdUsuarioCobranza { get; set; }

    public int FolioInicialCobranza { get; set; }
    public int FolioFinalCobranza { get; set; }

    public DateOnly? FechaInicialCobranza { get; set; }
    public DateOnly? FechaFinalCobranza { get; set; }

    /// <summary>La empresa de las ventas que se buscan por folio.</summary>
    public int IdEmpresaVenta { get; set; }

    public int FolioInicialVenta { get; set; }
    public int FolioFinalVenta { get; set; }
}

/// <summary>Filtros del detalle de una cobranza.</summary>
public class CobranzaDetalleFiltro
{
    /// <summary>Requerido.</summary>
    public int IdCobranza { get; set; }

    /// <summary>
    /// Como agrupa el SP el detalle. 0 es la presentacion normal.
    /// </summary>
    public int Presentacion { get; set; }
}

/// <summary>
/// Valida si una venta se puede meter en una cobranza.
///
/// Es el "ya la trae otra cobranza" / "ya esta pagada" de la pantalla.
/// </summary>
public class CobranzaValidarFolioFiltro
{
    /// <summary>Requerido.</summary>
    public int IdEmpresaVenta { get; set; }

    /// <summary>El folio de la venta. Requerido.</summary>
    public int FolioVenta { get; set; }

    /// <summary>La cobranza que se esta armando, para no chocar consigo misma.</summary>
    public int IdCobranzaActual { get; set; }

    public int IdTipoDocumento { get; set; }
}

/// <summary>Arma una cobranza con un repartidor y una lista de ventas.</summary>
public class CrearCobranzaRequest
{
    /// <summary>El repartidor que se la lleva. Requerido.</summary>
    public int IdRepartidor { get; set; }

    /// <summary>Las ventas que entran. Requerido, al menos una.</summary>
    public List<int> IdsVenta { get; set; } = new();

    public string? Observaciones { get; set; }

    /// <summary>
    /// El cobro al que pertenece, cuando la cobranza se arma desde un cobro.
    /// 0 = ninguno.
    /// </summary>
    public int IdCobro { get; set; }

    /// <summary>true = la cobranza se arma como parte de un cobro.</summary>
    public bool EsCobro { get; set; }

    /// <summary>El almacen del que salen las remisiones.</summary>
    public int IdAlmacen { get; set; }
}

/// <summary>
/// Cambia el contenido de una cobranza ya armada: quita renglones, los devuelve,
/// y/o agrega ventas.
/// </summary>
public class ActualizarCobranzaDetalleRequest
{
    /// <summary>El repartidor. Requerido: el SP lo guarda tal como llega.</summary>
    public int IdRepartidor { get; set; }

    /// <summary>Renglones que se quitan de la cobranza.</summary>
    public List<int> IdsDetalleEliminar { get; set; } = new();

    /// <summary>Renglones que el repartidor devuelve sin cobrar.</summary>
    public List<int> IdsDetalleDevolver { get; set; } = new();

    /// <summary>Ventas que se agregan.</summary>
    public List<int> IdsVentaAgregar { get; set; } = new();

    public string? Observaciones { get; set; }
}

/// <summary>Cancela una o varias cobranzas.</summary>
public class CancelarCobranzasRequest
{
    /// <summary>Las cobranzas a cancelar. Requerido, al menos una.</summary>
    public List<int> IdsCobranza { get; set; } = new();

    /// <summary>Los cobros asociados, cuando las cobranzas vienen de cobros.</summary>
    public List<int> IdsCobro { get; set; } = new();
}

/// <summary>
/// Filtros de la cobranza pendiente y vencida.
///
/// Esta pantalla es un caso particular del reporte de saldos
/// (sp_n_rptVentasSaldosPrincipal): de sus catorce parametros, SIETE los fijaba
/// Legacy en C# y no son del usuario, porque son lo que define la pantalla
/// (CobranzaPendientesVencidosService.cs:45-62):
///
///   Status = 4           pendientes Y vencidas
///   Formato = 1          detallado
///   IDTipoDocumento = 0  todos
///   Criterio = 0         todos
///   DiasVencimiento = 0  todos
///   Mostrar = 0          contado y credito
///   IDAlmacen = 0        todos
///
/// Se conservan tal cual. Exponerlos convertiria esta pantalla en el reporte de
/// saldos, que ya existe aparte.
/// </summary>
public class CobranzaPendientesVencidosFiltro
{
    public int IdEmpresa { get; set; }

    /// <summary>Requerida: el reporte es "desde".</summary>
    public DateOnly? FechaInicial { get; set; }

    /// <summary>Requerida: el reporte es "hasta".</summary>
    public DateOnly? FechaFinal { get; set; }

    public int IdAgente { get; set; }
    public int IdCliente { get; set; }

    /// <summary>Como ordena y agrupa el reporte.</summary>
    public int FormatoMostrar { get; set; }
}
