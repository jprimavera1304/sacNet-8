namespace ISL_Service.Application.DTOs.Persona
{
    public class PersonaDTO
    {
        public int IDPersona { get; set; }
        public int IDStatus { get; set; }
        public int Cancelado { get; set; } // 0/1 derivado desde SQL

        public string? Nombre { get; set; }

        public int IDUsuario { get; set; }
        public System.DateTime Fecha { get; set; }
        public string Equipo { get; set; } = string.Empty;

        public int? IDUsuarioModificacion { get; set; }
        public System.DateTime? FechaModificacion { get; set; }
        public string? EquipoModificacion { get; set; }
    }
}