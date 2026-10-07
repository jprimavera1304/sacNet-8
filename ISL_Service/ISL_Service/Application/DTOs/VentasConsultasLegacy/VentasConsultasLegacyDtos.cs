namespace ISL_Service.Application.DTOs.VentasConsultasLegacy;

/*
  LAS CABECERAS DE PANTALLA DE VENTAS QUE FALTABAN

  Son consultas, pero estaban marcadas como escrituras en el tablero y con razon:
  en legacy TODAS empiezan leyendo `formatoFechaDB` con sp_n_ConsultaConstantes,
  y ese procedimiento no consulta — cuando la fecha de operacion esta atrasada
  hace el CIERRE DEL DIA: borra y rehace AlmacenProductoHist y adelanta
  Constantes.FechaOperacion. O sea que abrir una pantalla de consulta un lunes
  contra una base que se quedo en viernes congelaba el inventario del viernes y
  movia la fecha del negocio.

  Aqui NINGUNA de estas consultas llama a ese procedimiento. Las fechas se mandan
  en "yyyy-MM-dd HH:mm:ss", que SQL Server entiende con cualquier DATEFORMAT, asi
  que no hace falta preguntarle a Constantes como vienen. Con eso estas
  capacidades vuelven a ser lo que su nombre dice: lecturas.
*/

/// <summary>Filtros del detalle de pedidos de la consulta de ventas.</summary>
public class VentasPedidoDetalleConsultaRequest
{
    public List<int> IdsVenta { get; set; } = new();
    public int IdVenta { get; set; }
    public int IdEmpresa { get; set; }
    public int IdCliente { get; set; }
    public int IdUsuario { get; set; }
    public int IdAgente { get; set; }
    public int IdTipoDocumento { get; set; }
    public int FolioInicial { get; set; }
    public int FolioFinal { get; set; }
    public DateTime? FechaEmisionInicial { get; set; }
    public DateTime? FechaEmisionFinal { get; set; }
    public DateTime? FechaCancelInicial { get; set; }
    public DateTime? FechaCancelFinal { get; set; }
    public DateTime? FechaPagoInicial { get; set; }
    public DateTime? FechaPagoFinal { get; set; }

    /// <summary>0, 1 o 2: el enumPresentacion de Mac31.</summary>
    public int Formato { get; set; }

    public List<int> IdsCliente { get; set; } = new();
    public List<int> IdsProducto { get; set; } = new();
    public int IdProducto { get; set; }

    /// <summary>
    /// true para traer TODAS las remisiones, sin rango de emision. En legacy esto
    /// se pedia con un campo `TodasRemisiones` que ponia la fecha inicial en
    /// "01-01-2000" literal y la final en la fecha de operacion leida de
    /// Constantes. Aqui se resuelve sin leer Constantes: simplemente no se manda
    /// el rango.
    /// </summary>
    public bool TodasLasRemisiones { get; set; }
}

/// <summary>Filtros de las remisiones en transito.</summary>
public class VentasTransitoConsultaRequest
{
    /// <summary>El estatus de inventario que tipifica legacy.</summary>
    public int StatusInventario { get; set; }

    public int IdCliente { get; set; }
    public DateTime? FechaEmisionInicial { get; set; }
    public DateTime? FechaEmisionFinal { get; set; }

    /// <summary>1 para agrupar por cliente en vez de renglon por remision.</summary>
    public int Agrupado { get; set; }
}

/// <summary>Filtros de las ventas pendientes de autorizar o rechazar.</summary>
public class VentasPendienteConsultaRequest
{
    public int IdVentaPendiente { get; set; }
    public int IdVenta { get; set; }
    public int IdEmpresa { get; set; }
    public int IdCliente { get; set; }
    public int IdUsuario { get; set; }
    public int IdAgente { get; set; }
    public int IdTipoDocumento { get; set; }
    public int IdAlmacen { get; set; }
    public int IdUsuarioAutorizacion { get; set; }
    public int IdUsuarioRechazo { get; set; }
    public DateTime? FechaEmisionInicial { get; set; }
    public DateTime? FechaEmisionFinal { get; set; }
    public DateTime? FechaAutorizacionInicial { get; set; }
    public DateTime? FechaAutorizacionFinal { get; set; }
    public DateTime? FechaRechazoInicial { get; set; }
    public DateTime? FechaRechazoFinal { get; set; }
    public int Formato { get; set; }

    /// <summary>El modo de filtrado que tipifica legacy.</summary>
    public int Filtrar { get; set; }
}

/// <summary>Filtros de los pagos de una remision (esquema viejo).</summary>
public class VentasPagosConsultaRequest
{
    public int IdVenta { get; set; }
    public int IdPagoVenta { get; set; }

    /// <summary>1 para ver solo la diferencia de usados.</summary>
    public int DiferenciaUsados { get; set; }

    public int IdCobro { get; set; }

    /// <summary>1 para ver solo el dinero excedente.</summary>
    public int DineroExcedente { get; set; }
}

/// <summary>Filtros de los pagos del esquema nuevo.</summary>
public class VentasNuevosPagosConsultaRequest
{
    public int IdPagoVenta { get; set; }
    public int IdCliente { get; set; }
    public DateTime? FechaInicial { get; set; }
    public DateTime? FechaFinal { get; set; }
}

/// <summary>Filtros de los pagos de centro de servicio.</summary>
public class VentasPagosCentroServicioConsultaRequest
{
    /// <summary>
    /// Los tipos de pago. El procedimiento lo declara varchar(4000), asi que
    /// acepta una lista.
    /// </summary>
    public List<int> IdsTipoPago { get; set; } = new();

    public int IdCliente { get; set; }
    public DateTime? FechaInicial { get; set; }
    public DateTime? FechaFinal { get; set; }
    public int IdCorte { get; set; }
}

/// <summary>Los pagos de una fecha de liquidacion.</summary>
public class VentasPagosLiquidacionConsultaRequest
{
    public DateTime? FechaLiquidacion { get; set; }
}

/// <summary>Filtros del reporte de folios de venta.</summary>
public class VentasFoliosConsultaRequest
{
    public List<int> IdsEmpresa { get; set; } = new();
    public List<int> IdsAlmacen { get; set; } = new();
    public List<int> IdsTipoDocumento { get; set; } = new();
    public int IdCliente { get; set; }
    public int IdAgente { get; set; }
    public DateTime? FechaInicial { get; set; }
    public DateTime? FechaFinal { get; set; }

    /// <summary>El estatus del folio que tipifica legacy.</summary>
    public int StatusFolio { get; set; }

    /// <summary>
    /// 0, 1 o 2. El procedimiento lo declara y legacy NO se lo manda, asi que
    /// siempre le llegaba su valor por defecto. Se ofrece porque la base ya lo
    /// soporta y mandar 0 da el comportamiento de hoy.
    /// </summary>
    public int Presentacion { get; set; }

    /// <summary>
    /// Los usuarios que capturaron. Tambien lo declara el procedimiento y legacy
    /// no lo mandaba.
    /// </summary>
    public List<int> IdsUsuario { get; set; } = new();

    public int Formato { get; set; }
}
