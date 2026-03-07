namespace ISL_Service.Application.DTOs.Cheques;

public class CambiarEstatusChequeRequest
{
    // 2 = Cobrado, 3 = Devuelto, 4 = Cancelado
    public byte EstatusChequeNuevo { get; set; }
    public string? Motivo { get; set; }
    public string? Observaciones { get; set; }
    public DateTime? FechaMovimiento { get; set; }
}
