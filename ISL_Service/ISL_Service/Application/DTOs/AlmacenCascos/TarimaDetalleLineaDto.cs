namespace ISL_Service.Application.DTOs.AlmacenCascos;

public class TarimaDetalleLineaDto
{
    public int IdDetalle { get; set; }
    public int? IdTarima { get; set; }
    public int IdTipoCasco { get; set; }
    public string? TipoCascoDescripcion { get; set; }
    public int Piezas { get; set; }
}
