using System.Collections.Generic;
using System.Threading.Tasks;
using ISL_Service.Application.DTOs.Persona;

namespace ISL_Service.Application.Interfaces
{
    public interface IPersonasRepository
    {
        Task<List<PersonaDTO>> ConsultarAsync(int? idPersona, int? idStatus);
        Task<int> InsertarAsync(PersonaInputDTO input);
        Task ActualizarAsync(PersonaInputDTO input);
        Task CancelarAsync(int idPersona, int idUsuarioModificacion, string equipoModificacion);
    }
}