namespace ISL_Service.Application.DTOs.AlmacenCascos;

public class SalidaTarimaDto
{
    public int NumeroTarima { get; set; }
    public List<SalidaTarimaLineaDto> Lineas { get; set; } = new();
}
