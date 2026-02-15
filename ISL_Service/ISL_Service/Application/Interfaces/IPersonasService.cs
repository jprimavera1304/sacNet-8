using System.Threading.Tasks;
using ISL_Service.Application.DTOs.Persona;

namespace ISL_Service.Application.Interfaces
{
    public interface IPersonasService
    {
        Task<PersonaResponse> ConsultarAsync(int? idPersona, int? idStatus);
        Task<PersonaResponse> InsertarAsync(PersonaInputDTO input);
        Task<PersonaResponse> ActualizarAsync(PersonaInputDTO input);
        Task<PersonaResponse> CancelarAsync(int idPersona, PersonaInputDTO input);
        Task<PersonaResponse> ReactivarAsync(int idPersona, PersonaInputDTO input);
    }
}
