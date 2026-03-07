namespace ISL_Service.Application.DTOs.Cheques;

public class ChequeDto
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
    public DateTime? FechaMovimiento { get; set; }
    public DateTime FechaCreacion { get; set; }
    public string? UsuarioCreacionNombre { get; set; }
    public DateTime? FechaActualizacion { get; set; }
    public string? UsuarioActualizacionNombre { get; set; }
}
