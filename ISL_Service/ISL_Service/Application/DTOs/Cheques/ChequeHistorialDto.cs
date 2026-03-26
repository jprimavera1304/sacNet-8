namespace ISL_Service.Application.DTOs.Cheques;

public class ChequeHistorialDto
{
    public Guid Id { get; set; }
    public Guid ChequeId { get; set; }
    public byte? EstatusAnterior { get; set; }
    public string? EstatusAnteriorNombre { get; set; }
    public byte EstatusNuevo { get; set; }
    public string? EstatusNuevoNombre { get; set; }
    public string? Accion { get; set; }
    public string? Motivo { get; set; }
    public string? Observaciones { get; set; }
    public DateTime FechaMovimiento { get; set; }
    public Guid UsuarioMovimientoId { get; set; }
    public string? UsuarioMovimientoNombre { get; set; }
}
