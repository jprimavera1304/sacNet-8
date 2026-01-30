using System;
using System.Linq;
using System.Threading.Tasks;
using ISL_Service.Application.DTOs.Persona;
using ISL_Service.Application.Interfaces;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Application.Services
{
    public class PersonasService : IPersonasService
    {
        private readonly IPersonasRepository _repository;

        public PersonasService(IPersonasRepository repository)
        {
            _repository = repository;
        }

        private static bool EsTextoVacio(string? value)
        {
            return string.IsNullOrWhiteSpace(value);
        }

        private static string Normaliza(string? value)
        {
            return value?.Trim() ?? string.Empty;
        }

        public async Task<PersonaResponse> ConsultarAsync(int? idPersona, int? idStatus)
        {
            var response = new PersonaResponse();

            try
            {
                var list = await _repository.ConsultarAsync(idPersona, idStatus);

                response.Success = true;
                response.Message = "Consulta exitosa.";
                response.List = list;

                if (idPersona.HasValue)
                {
                    response.Data = list.FirstOrDefault();
                }

                return response;
            }
            catch (SqlException ex)
            {
                response.Success = false;
                response.Message = ex.Message;
                return response;
            }
            catch (Exception)
            {
                response.Success = false;
                response.Message = "Ocurrió un error inesperado al consultar personas.";
                return response;
            }
        }

        public async Task<PersonaResponse> InsertarAsync(PersonaInputDTO input)
        {
            var response = new PersonaResponse();

            try
            {
                if (input == null)
                {
                    response.Success = false;
                    response.Message = "El cuerpo de la solicitud es requerido.";
                    return response;
                }

                if (input.IDUsuario <= 0)
                {
                    response.Success = false;
                    response.Message = "IDUsuario es requerido.";
                    return response;
                }

                input.Equipo = Normaliza(input.Equipo);

                if (EsTextoVacio(input.Equipo))
                {
                    response.Success = false;
                    response.Message = "Equipo es requerido.";
                    return response;
                }

                var idPersona = await _repository.InsertarAsync(input);

                var list = await _repository.ConsultarAsync(idPersona, null);

                response.Success = true;
                response.Message = "Persona insertada correctamente.";
                response.Data = list.FirstOrDefault();

                return response;
            }
            catch (SqlException ex)
            {
                response.Success = false;
                response.Message = ex.Message;
                return response;
            }
            catch (Exception)
            {
                response.Success = false;
                response.Message = "Ocurrió un error inesperado al insertar persona.";
                return response;
            }
        }

        public async Task<PersonaResponse> ActualizarAsync(PersonaInputDTO input)
        {
            var response = new PersonaResponse();

            try
            {
                if (input == null)
                {
                    response.Success = false;
                    response.Message = "El cuerpo de la solicitud es requerido.";
                    return response;
                }

                if (!input.IDPersona.HasValue || input.IDPersona.Value <= 0)
                {
                    response.Success = false;
                    response.Message = "IDPersona es requerido para actualizar.";
                    return response;
                }

                if (input.IDUsuario <= 0)
                {
                    response.Success = false;
                    response.Message = "IDUsuario es requerido para actualizar.";
                    return response;
                }

                input.Equipo = Normaliza(input.Equipo);

                if (EsTextoVacio(input.Equipo))
                {
                    response.Success = false;
                    response.Message = "Equipo es requerido para actualizar.";
                    return response;
                }

                await _repository.ActualizarAsync(input);

                var list = await _repository.ConsultarAsync(input.IDPersona.Value, null);

                response.Success = true;
                response.Message = "Persona actualizada correctamente.";
                response.Data = list.FirstOrDefault();

                return response;
            }
            catch (SqlException ex)
            {
                response.Success = false;
                response.Message = ex.Message;
                return response;
            }
            catch (Exception)
            {
                response.Success = false;
                response.Message = "Ocurrió un error inesperado al actualizar persona.";
                return response;
            }
        }

        public async Task<PersonaResponse> CancelarAsync(int idPersona, PersonaInputDTO input)
        {
            var response = new PersonaResponse();

            try
            {
                if (input == null)
                {
                    response.Success = false;
                    response.Message = "El cuerpo de la solicitud es requerido.";
                    return response;
                }

                if (idPersona <= 0)
                {
                    response.Success = false;
                    response.Message = "IDPersona inválido.";
                    return response;
                }

                if (input.IDUsuario <= 0)
                {
                    response.Success = false;
                    response.Message = "IDUsuario es requerido para cancelar.";
                    return response;
                }

                input.Equipo = Normaliza(input.Equipo);

                if (EsTextoVacio(input.Equipo))
                {
                    response.Success = false;
                    response.Message = "Equipo es requerido para cancelar.";
                    return response;
                }

                await _repository.CancelarAsync(idPersona, input.IDUsuario, input.Equipo);

                var list = await _repository.ConsultarAsync(idPersona, null);

                response.Success = true;
                response.Message = "Persona cancelada correctamente.";
                response.Data = list.FirstOrDefault();

                return response;
            }
            catch (SqlException ex)
            {
                response.Success = false;
                response.Message = ex.Message;
                return response;
            }
            catch (Exception)
            {
                response.Success = false;
                response.Message = "Ocurrió un error inesperado al cancelar persona.";
                return response;
            }
        }
    }
}