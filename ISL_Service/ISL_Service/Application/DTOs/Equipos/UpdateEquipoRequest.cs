namespace ISL_Service.Application.DTOs.Equipos;

public class UpdateEquipoRequest
{
    public string Nombre { get; set; } = string.Empty;
    public Guid CategoriaPredeterminadaId { get; set; }
    public byte DiaJuegoPredeterminado { get; set; }
    public Guid ProfesorTitularPredeterminadoId { get; set; }
    public Guid? ProfesorAuxiliarPredeterminadoId { get; set; }
}

