namespace ISL_Service.Application.DTOs.Cheques;

public class ChequeDetalleDto
{
    public Guid Id { get; set; }
    public int IDCliente { get; set; }
    public string? ClienteNombre { get; set; }
    public int IDBanco { get; set; }
    public string? BancoNombre { get; set; }
    public string NumeroCheque { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public DateTime FechaCheque { get; set; }
    public byte EstatusCheque { get; set; }
    public string? EstatusChequeNombre { get; set; }
    public byte IDStatus { get; set; }
    public string? StatusNombre { get; set; }
    public DateTime? FechaCobro { get; set; }
    public DateTime? FechaDevolucion { get; set; }
    public DateTime? FechaCancelacion { get; set; }
    public string? MotivoDevolucion { get; set; }
    public string? MotivoCancelacion { get; set; }
    public string? Observaciones { get; set; }
    public Guid? ResponsableCobroId { get; set; }
    public string? ResponsableCobroNombre { get; set; }
    public DateTime FechaCreacion { get; set; }
    public Guid UsuarioCreacionId { get; set; }
    public string? UsuarioCreacionNombre { get; set; }
    public DateTime? FechaActualizacion { get; set; }
    public Guid? UsuarioActualizacionId { get; set; }
    public string? UsuarioActualizacionNombre { get; set; }
    public Guid? UsuarioCobroId { get; set; }
    public string? UsuarioCobroNombre { get; set; }
    public Guid? UsuarioDevolucionId { get; set; }
    public string? UsuarioDevolucionNombre { get; set; }
    public Guid? UsuarioCancelacionId { get; set; }
    public string? UsuarioCancelacionNombre { get; set; }

    public List<ChequeHistorialDto> Historial { get; set; } = new();
}
