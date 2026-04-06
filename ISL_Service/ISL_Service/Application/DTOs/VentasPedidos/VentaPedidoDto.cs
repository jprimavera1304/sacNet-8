namespace ISL_Service.Application.DTOs.VentasPedidos;

public class VentaPedidoDto
{
    public int PedidosPendientesImprimir { get; set; }

    public int IDPedido { get; set; }
    public int IDStatusPedido { get; set; }
    public int IDEmpresa { get; set; }
    public int IDCliente { get; set; }
    public int IDUsuario { get; set; }
    public int IDTipoAutorizacion { get; set; }
    public int IDDomilicio { get; set; }
    public int IDAgente { get; set; }
    public int IDTipoDocumento { get; set; }
    public int IDVenta { get; set; }
    public int IDGarantia { get; set; }
    public int IDAlmacen { get; set; }
    public string? Almacen { get; set; }
    public int Pedido { get; set; }
    public int Folio { get; set; }
    public string? FolioFtm { get; set; }
    public DateTime Fecha { get; set; }
    public string? FechaFtm { get; set; }
    public string? Equipo { get; set; }
    public string? Empresa { get; set; }
    public string? Documento { get; set; }
    public int NumeroCliente { get; set; }
    public string? NombreCliente { get; set; }
    public int Agente { get; set; }
    public string? NombreAgente { get; set; }
    public string? NumeroNombreAgente { get; set; }
    public int Productos { get; set; }
    public decimal TotalPagar { get; set; }
    public string? Autorizacion { get; set; }
    public decimal SaldoVencido { get; set; }
    public decimal SaldoPendiente { get; set; }
    public decimal Disponible { get; set; }

    public int MaxDiasVencidos { get; set; }
    public int MaxDiasVencidosAcumuladores { get; set; }
    public int MaxDiasVencidosAceites { get; set; }
    public int MaxDiasVencidosCascos { get; set; }
    public string? MaxDiasVencidosMostrar { get; set; }

    public string? Usuario { get; set; }
    public string? Domicilio { get; set; }

    public string? Procesado { get; set; }
    public DateTime? FechaProceso { get; set; }
    public string? FechaProcesoFtm { get; set; }

    public string? Autorizado { get; set; }
    public DateTime? FechaAutorizo { get; set; }
    public string? FechaAutorizoFtm { get; set; }

    public string? Rechazado { get; set; }
    public DateTime? FechaRechazo { get; set; }
    public string? FechaRechazoFtm { get; set; }

    public string? Cancelado { get; set; }
    public DateTime? FechaCancelo { get; set; }
    public string? FechaCanceloFtm { get; set; }

    public string? Impreso { get; set; }
    public DateTime? FechaImpreso { get; set; }
    public string? FechaImpresoFtm { get; set; }

    public string? Cancelada { get; set; }
    public string? Pagada { get; set; }
    public decimal Abonos { get; set; }
}
