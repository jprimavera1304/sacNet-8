namespace ISL_Service.Application.DTOs.AlmacenCascos;

public class MovimientoCascoDetalleAgrupadoDto
{
    public int IdMovimiento { get; set; }
    public List<TarimaDetalleAgrupadoDto> Tarimas { get; set; } = new();
}
