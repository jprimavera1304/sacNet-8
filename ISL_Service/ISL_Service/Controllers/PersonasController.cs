using System.Threading.Tasks;
using ISL_Service.Application.DTOs.Persona;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PersonasController : ControllerBase
    {
        private readonly IPersonasService _service;

        public PersonasController(IPersonasService service)
        {
            _service = service;
        }

        /// <summary>
        /// Consulta personas.
        /// Permite filtrar por IDPersona y/o IDStatus.
        /// </summary>
        /// <param name="idPersona">ID de la persona (opcional)</param>
        /// <param name="idStatus">1 = Activo, 2 = Cancelado (opcional)</param>
        [HttpGet("consultar")]
        public async Task<IActionResult> Consultar(
            [FromQuery] int? idPersona,
            [FromQuery] int? idStatus)
        {
            var result = await _service.ConsultarAsync(idPersona, idStatus);
            return Ok(result);
        }

        /// <summary>
        /// Inserta una nueva persona.
        /// IDStatus siempre se crea en 1 (Activo).
        /// </summary>
        [HttpPost("insertar")]
        public async Task<IActionResult> Insertar(
            [FromBody] PersonaInputDTO input)
        {
            var result = await _service.InsertarAsync(input);
            return Ok(result);
        }

        /// <summary>
        /// Actualiza una persona existente.
        /// No modifica el IDStatus.
        /// </summary>
        [HttpPut("actualizar")]
        public async Task<IActionResult> Actualizar(
            [FromBody] PersonaInputDTO input)
        {
            var result = await _service.ActualizarAsync(input);
            return Ok(result);
        }

        /// <summary>
        /// Cancela una persona.
        /// Cambia IDStatus a 2.
        /// No permite cancelar si ya esta cancelada.
        /// </summary>
        [HttpPut("cancelar")]
        public async Task<IActionResult> Cancelar(
            [FromBody] PersonaInputDTO input)
        {
            if (!input.IDPersona.HasValue)
            {
                return BadRequest("IDPersona es requerido para cancelar.");
            }

            var result = await _service.CancelarAsync(input.IDPersona.Value, input);
            return Ok(result);
        }
    }
}