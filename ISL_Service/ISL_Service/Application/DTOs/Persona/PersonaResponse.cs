using System.Collections.Generic;

namespace ISL_Service.Application.DTOs.Persona
{
    public class PersonaResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public PersonaDTO? Data { get; set; }
        public List<PersonaDTO>? List { get; set; }
    }
}