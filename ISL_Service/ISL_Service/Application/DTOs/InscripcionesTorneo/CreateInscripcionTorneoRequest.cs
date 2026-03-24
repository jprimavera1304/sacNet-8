namespace ISL_Service.Application.DTOs.InscripcionesTorneo;

public class CreateInscripcionTorneoRequest
{
    public Guid TorneoId { get; set; }
    public Guid EquipoId { get; set; }
    public Guid? CategoriaId { get; set; }
    public byte? DiaJuego { get; set; }
    public Guid? ProfesorTitularId { get; set; }
    public Guid? ProfesorAuxiliarId { get; set; }
}