namespace ISL_Service.Application.DTOs.VentasConsulta;

public class VentasConsultaCatalogoItem
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Clave { get; set; } = string.Empty;
}

public class VentasConsultaCatalogosResponse
{
    public List<VentasConsultaCatalogoItem> Empresas { get; set; } = new();
    public List<VentasConsultaCatalogoItem> Almacenes { get; set; } = new();
    public List<VentasConsultaCatalogoItem> Agentes { get; set; } = new();
    public List<VentasConsultaCatalogoItem> TiposDocumento { get; set; } = new();
    public List<VentasConsultaCatalogoItem> EstatusVenta { get; set; } = new();
    public VentasConsultaFechasOperacion FechasOperacion { get; set; } = new();

    /*
      LAS DOS VARIABLES DE EMPRESA QUE MAC31 LEE AL ARRANCAR

      Variables.funcionalidad y Variables.EsCentroServicio (Utils/Globales.cs:470).
      De ellas depende media barra de botones de Consultar ventas, y la pantalla
      las necesita ANTES de pulsar nada: el navegador solo deja abrir una pestaña
      nueva dentro del mismo clic, asi que decidir "¿abro la pestaña del PDF o
      abro un dialogo?" no puede costar una ida al servidor.

      Que vengan aqui no las convierte en la regla: la regla vive en
      VentasImpresionReglas y se vuelve a aplicar entera al firmar el pase.
    */
    public string Funcionalidad { get; set; } = string.Empty;
    public int EsCentroServicio { get; set; }
}

public class VentasConsultaFechasOperacion
{
    public string FechaOperacion { get; set; } = string.Empty;
    public string FechaOperacionFtm { get; set; } = string.Empty;
    public string FechaOperacionPagos { get; set; } = string.Empty;
    public string FechaOperacionPagosFtm { get; set; } = string.Empty;
}

public class VentasConsultaRequest
{
    public List<int> IDsVentaLst { get; set; } = new();
    public List<int> IDsClientesLst { get; set; } = new();
    public List<int> IDsProductoLst { get; set; } = new();
    public List<int> FoliosLst { get; set; } = new();
    public List<int> NumerosClienteLst { get; set; } = new();
    public List<string> ClavesProductoLst { get; set; } = new();
    public string? IDsVenta { get; set; }
    public string? IDsClientes { get; set; }
    public string? IDsProducto { get; set; }
    public string? ClaveProducto { get; set; }
    public int IDVenta { get; set; }
    public int IDEmpresa { get; set; }
    public int IDCliente { get; set; }
    public int IDUsuario { get; set; }
    public int IDAgente { get; set; }
    public int IDTipoDocumento { get; set; }
    public int IDStatusPedido { get; set; }
    public int IDUsuarioActual { get; set; }
    public int IDProducto { get; set; }
    public int TipoDocumento { get; set; }
    public string? FolioInicial { get; set; }
    public string? FolioFinal { get; set; }
    public string? FechaEmisionInicial { get; set; }
    public string? FechaEmisionFinal { get; set; }
    public string? FechaCancelInicial { get; set; }
    public string? FechaCancelFinal { get; set; }
    public string? FechaPagoInicial { get; set; }
    public string? FechaPagoFinal { get; set; }
    public string? FechaInicial { get; set; }
    public string? FechaFinal { get; set; }
    public int Formato { get; set; }
    public bool DiferenciaUsados { get; set; }
    public int IDCobro { get; set; }
    public bool DineroExcedente { get; set; }
    // Cuando es true (lo usa la app movil "Mis pedidos") se filtra por el
    // usuario del token, el MISMO que se guarda al crear el pedido. Asi el
    // usuario solo ve los pedidos que el hizo. El web no lo manda -> ve todos.
    public bool SoloMisPedidos { get; set; }
}

public class VentasConsultaRowsResponse
{
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public int Total { get; set; }
}

/*
  ABRIR EL REPORTE DE UNA REMISION

  El reporte lo genera ESTE backend: lee las mismas plantillas de Template_Html
  y llama los mismos sp_n_ que usa Mac31, y arma el PDF. Antes se delegaba en
  MacReportes —el servidor de reportes viejo— y eso obligaba a tener esa
  aplicacion viva y alcanzable desde el navegador del usuario.

  Lo que se conserva de aquel flujo es lo que de verdad importaba: las
  plantillas y los procedimientos. Por eso el papel sigue saliendo igual al de
  Mac31 y se sigue actualizando solo si alguien ajusta una plantilla.

  El POST no devuelve el PDF: devuelve la direccion donde esta. La pestana ya
  se abrio en el navegador antes de pedir (si no, la bloquea), asi que lo unico
  que falta es a donde mandarla.
*/
public class VentasReporteRequest
{
    /// Las ventas a incluir. Mac31 manda varias cuando hay varias marcadas, y
    /// salen todas en el mismo PDF, una por hoja.
    public List<int> IdsVenta { get; set; } = new();

    /// 0 = ver en pantalla, 1 = descargar el archivo.
    public int Descargar { get; set; }

    /*
      QUE BOTON LO PIDIO. No es cosmetico: de esto dependen las banderas de
      impresion, si hay que pedir la contrasena rotatoria y que folios se
      pueden sacar. Ver VentasImpresionReglas.

        "pantalla"    btnPantalla_Click   (ConsultarVentas.cs:3771)
        "imprimir"    btnImprimir_Click   (ConsultarVentas.cs:4213)
        "reimprimir"  btnReimprimir_Click (ConsultarVentas.cs:4221)
    */
    public string Accion { get; set; } = "pantalla";

    /// La pestaña desde la que se pidio: "remisiones" es la Emitidas de Mac31,
    /// la unica que pide contrasena para imprimir (ConsultarVentas.cs:4324).
    public string Vista { get; set; } = "remisiones";

    /// La contrasena rotatoria, cuando el paso anterior dijo que hacia falta.
    public string? Contrasena { get; set; }
}

/*
  LO QUE HAY QUE PREGUNTAR ANTES DE ABRIR EL PAPEL

  Mac31 pregunta en este orden y por eso aqui se devuelve todo junto: primero
  se ve si algun folio no se puede, luego la contrasena, luego la confirmacion
  de impresora. La pantalla necesita saberlo ANTES de abrir la pestaña nueva,
  porque una pestaña que se abre para cerrarse en tres segundos es peor que no
  abrirla.
*/
public class VentasReportePreparacionResponse
{
    /// "TAU" o "ZARA". Lo que Mac31 lee de Constantes en Variables.funcionalidad.
    public string Funcionalidad { get; set; } = string.Empty;
    public int EsCentroServicio { get; set; }

    public bool RequiereContrasena { get; set; }

    /// Solo Tauro enseña el dialogo de impresora (ConsultarVentas.cs:4377-4400).
    public bool ConfirmaImpresora { get; set; }

    /// Solo "reimprimir": "SE HARÁ LA PRIMER IMPRESIÓN..." (ConsultarVentas.cs:4355).
    public bool ConfirmaReimpresion { get; set; }

    /// Los folios que legacy no deja sacar por este camino, con el motivo.
    public List<VentasReporteBloqueo> Bloqueos { get; set; } = new();

    /// Las ventas que SI se pueden pedir. Si queda vacia, no hay nada que abrir.
    public List<int> IdsVenta { get; set; } = new();
}

public class VentasReporteBloqueo
{
    public int IdVenta { get; set; }
    public string FolioFtm { get; set; } = string.Empty;
    public string Motivo { get; set; } = string.Empty;
}

/*
  UN RENGLON DE "PEDIDOS FALTANTES"

  Las columnas son las que devuelve sp_n_ConsultaPedidoFaltante y las que
  enseña ConsultarResultado.SetColumnasPedidoFaltante en Mac31
  (Legacy/Mac31/Mac31/Forms/ConsultarResultado.cs, region SetColumnasPedidoFaltante).

  Es lo que un cliente PIDIO y no se le pudo surtir hoy: cantidad solicitada
  contra cantidad que si entro al pedido, y la diferencia.
*/
public class VentasPedidoFaltanteItem
{
    public string Numero { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string FechaFtm { get; set; } = string.Empty;
    public string GrupoCategoria { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string Marca { get; set; } = string.Empty;
    public string Clave { get; set; } = string.Empty;
    public decimal CantidadSolicitada { get; set; }
    public decimal CantidadPedido { get; set; }
    public decimal CantidadFaltante { get; set; }
}

public class VentasReporteResponse
{
    public bool Ok { get; set; }
    public string Message { get; set; } = string.Empty;
    /// La direccion completa, lista para abrirse en otra pestana.
    public string Url { get; set; } = string.Empty;
}
