namespace ISL_Service.Application.DTOs.AlmacenCascos;

public class TarimaDetalleAgrupadoDto
{
    public int NumeroTarima { get; set; }
    public List<TarimaDetalleLineaDto> Lineas { get; set; } = new();
}
