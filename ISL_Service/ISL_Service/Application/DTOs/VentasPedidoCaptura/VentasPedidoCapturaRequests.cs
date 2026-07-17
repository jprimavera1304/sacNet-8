namespace ISL_Service.Application.DTOs.VentasPedidoCaptura;

public class PedidoClienteBuscarRequest
{
    public int IDCliente { get; set; }
    public string? Numero { get; set; }
    // Filtros de busqueda por texto (reusa el SP legacy sp_n_ConsultaClientes con
    // @Identico=0 -> coincidencia parcial / lista). Si vienen vacios, se busca por
    // numero/id como antes.
    public string? Nombre { get; set; }
    public string? ApellidoPaterno { get; set; }
    public string? ApellidoMaterno { get; set; }
    public string? RFC { get; set; }
    public int IDEmpresaCS { get; set; }
}

public class PedidoProductoBuscarRequest
{
    public int IDAlmacen { get; set; }
    public int IDProducto { get; set; }
    public int IDCliente { get; set; }
    public string? Clave { get; set; }
    public int IDGrupoCategoria { get; set; }
    public int IDEmpresaCS { get; set; }
}

// Modos de captura de pedido. Legacy usa el mismo formulario con SoloAceites=0/1
// (ConsultarVentas.cs: btnNuevoPedido_Click / btnAceites_Click). El cliente manda
// el modo, NUNCA el IDGrupoCategoria: la traduccion depende de la Funcionalidad de
// la empresa y eso solo lo sabe el backend (la app es multi-tenant).
public static class PedidoModo
{
    public const string Normal = "normal";
    public const string Aceites = "aceites";

    // Interruptor de servidor: permite encender aceites sin publicar app nueva.
    public const string ConfigAceitesHabilitado = "Pedidos:AceitesHabilitado";

    // Vacio/null -> normal. Valor no reconocido -> null (el controller responde 400).
    public static string? Normalizar(string? modo)
    {
        if (string.IsNullOrWhiteSpace(modo)) return Normal;

        var texto = modo.Trim().ToLowerInvariant();
        return texto == Normal || texto == Aceites ? texto : null;
    }
}

// Paginado de productos del almacen. Reusa el mismo SP legacy que
// PedidoProductoBuscarRequest, pero el filtrado (solo Existencia > 0), el orden y
// el recorte de pagina se hacen en C# sobre el resultado cacheado.
public class PedidoProductoPaginaRequest
{
    public const int TakeDefault = 50;
    public const int TakeMaximo = 100;

    public int IDAlmacen { get; set; }
    public int IDEmpresaCS { get; set; }
    // "normal" | "aceites". El backend lo traduce a IDGrupoCategoria segun la
    // Funcionalidad de la empresa.
    public string? Modo { get; set; }
    // Texto libre: filtra por Clave o Descripcion (contiene, sin distinguir
    // mayusculas ni acentos). Vacio -> todo el almacen con existencia.
    public string? Buscar { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; } = TakeDefault;

    // Se acota aqui para que el cliente no pueda pedir paginas gigantes. Valores
    // invalidos se ajustan en silencio, no son error.
    public int SkipSeguro() => Skip < 0 ? 0 : Skip;

    public int TakeSeguro() => Take <= 0 ? TakeDefault : Math.Min(Take, TakeMaximo);
}

public class PedidoAgregarDetalleRequest
{
    public int IDPedido { get; set; }
    public int IDEmpresa { get; set; }
    public int IDCliente { get; set; }
    public int IDAgenteAutoriza { get; set; }
    public int IDDomicilio { get; set; }
    public int IDTipoDocumento { get; set; } = 5;
    public string? Fecha { get; set; }
    public int IDAlmacen { get; set; }
    public int IDProducto { get; set; }
    public int Cantidad { get; set; } = 1;
    public decimal SaldoVencido { get; set; }
    public decimal SaldoPendiente { get; set; }
    public decimal Disponible { get; set; }
    public int MaxDiasVencidos { get; set; }
    public int MaxDiasVencidosAcumuladores { get; set; }
    public int MaxDiasVencidosAceites { get; set; }
    public int MaxDiasVencidosCascos { get; set; }
    public string? Observaciones { get; set; }
    public string? ObservacionesDetalle { get; set; }
    public decimal ServicioPrecioConIva { get; set; }
    public string? ServicioNombre { get; set; }
    public string? ServicioDireccion1 { get; set; }
    public string? ServicioDireccion2 { get; set; }
    public string? ServicioReferencia { get; set; }
    public string? ServicioTelefono { get; set; }
    public int SoloAceites { get; set; }
    public int SoloServicios { get; set; }
    public int SoloLogistica { get; set; }
    public int Facturar { get; set; }
    public int Simular { get; set; }
    public int IDDescuentoSimular { get; set; }
    public int PedidoSinCascosCambio { get; set; }
    public int Tipo { get; set; }
    public int IDEmpresaCS { get; set; }
    public int FacturaMayorista { get; set; }
    public int RemisionMayorista { get; set; }
    public decimal DescuentoAdicional { get; set; }
}

public class PedidoEliminarDetalleRequest
{
    public int IDPedido { get; set; }
    public int IDPedidoDetalle { get; set; }
    public string? Observaciones { get; set; }
    public int SinModificarObservaciones { get; set; }
}

public class PedidoGuardarRequest
{
    public int IDPedido { get; set; }
    public int IDDomicilio { get; set; }
    public int Productos { get; set; }
    public decimal TotalPagar { get; set; }
    public string? Observaciones { get; set; }
    public int IDAgenteAutoriza { get; set; }
    public string? ServicioNombre { get; set; }
    public string? ServicioDireccion1 { get; set; }
    public string? ServicioDireccion2 { get; set; }
    public string? ServicioReferencia { get; set; }
    public string? ServicioTelefono { get; set; }
    public int SoloLogistica { get; set; }
    public int PedidoSinCascosCambio { get; set; }
}
