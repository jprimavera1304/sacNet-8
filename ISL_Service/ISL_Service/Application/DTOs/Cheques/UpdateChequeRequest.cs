namespace ISL_Service.Application.DTOs.Cheques;

public class UpdateChequeRequest
{
    public int IDCliente { get; set; }
    public int IDBanco { get; set; }
    public string NumeroCheque { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public DateTime FechaCheque { get; set; }
    public string? Observaciones { get; set; }
    public Guid? ResponsableCobroId { get; set; }
}
