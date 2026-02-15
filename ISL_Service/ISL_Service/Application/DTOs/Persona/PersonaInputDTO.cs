namespace ISL_Service.Application.DTOs.Persona
{
    public class PersonaInputDTO
    {
        public int? IDPersona { get; set; } // null para insertar, requerido para actualizar/cancelar
        public string? Nombre { get; set; }

        public int IDUsuario { get; set; } // alta o modificacion (segun operacion)
        public string Equipo { get; set; } = string.Empty; // alta o modificacion (segun operacion)
    }
}