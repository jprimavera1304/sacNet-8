using System.Threading.Tasks;
using ISL_Service.Application.DTOs.Persona;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    /*
        LA FIRMA DE QUIEN DA DE ALTA UNA PERSONA VENIA EN EL CUERPO.

        PersonaInputDTO trae IDUsuario y Equipo, y el servicio los EXIGIA del
        cliente ("IDUsuario es requerido."). Esos dos campos son lo que queda
        escrito en Personas.IDUsuario/Equipo y en IDUsuarioModificacion/
        EquipoModificacion: la autoria del alta, de la modificacion, de la
        cancelacion y de la reactivacion. Viajando en el cuerpo, cualquiera con
        el permiso podia dar de alta un proveedor a nombre de otra persona.

        La regla del proyecto -repetida literalmente en una docena de
        controladores- es que la identidad NUNCA viene del cuerpo. Aqui se habia
        colado. Ahora SellarAutoria() la pisa con el token en las cuatro
        escrituras y lo que manda el cliente se descarta.
    */
    public class PersonasController : ControllerBase
    {
        private readonly IPersonasService _service;
        private readonly ICurrentUserAccessor _currentUser;

        public PersonasController(IPersonasService service, ICurrentUserAccessor currentUser)
        {
            _service = service;
            _currentUser = currentUser;
        }

        /// <summary>
        /// Quien y desde donde, del token.
        /// </summary>
        private PersonaInputDTO SellarAutoria(PersonaInputDTO input)
        {
            input.IDUsuario = _currentUser.GetLegacyUserId(User);
            input.Equipo = _currentUser.GetUsername(User, Environment.MachineName);
            return input;
        }

        /// <summary>
        /// Consulta personas.
        /// Permite filtrar por IDPersona y/o IDStatus.
        /// </summary>
        /// <param name="idPersona">ID de la persona (opcional)</param>
        /// <param name="idStatus">1 = Activo, 2 = Cancelado (opcional)</param>
        [HttpGet("consultar")]
        /* "modulo:proveedores" = lo que Mac31 hace: CONSULTA DE PROVEEDORES se
           abre con cualquier boton concedido y el que falta sale en gris. Sin
           esto, quien solo tiene NUEVO (XOCHITL) o solo ESTADO (JAZMIN) veia la
           tarjeta y la pantalla la rebotaba. El detalle y las escrituras siguen
           pidiendo su propia clave. */
        [Authorize(Policy = "perm:proveedores.ver|modulo:proveedores")]
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
        [Authorize(Policy = "perm:proveedores.crear")]
        public async Task<IActionResult> Insertar(
            [FromBody] PersonaInputDTO input)
        {
            var result = await _service.InsertarAsync(SellarAutoria(input));
            return Ok(result);
        }

        /// <summary>
        /// Actualiza una persona existente.
        /// No modifica el IDStatus.
        /// </summary>
        [HttpPut("actualizar")]
        [Authorize(Policy = "perm:proveedores.editar")]
        public async Task<IActionResult> Actualizar(
            [FromBody] PersonaInputDTO input)
        {
            var result = await _service.ActualizarAsync(SellarAutoria(input));
            return Ok(result);
        }

        /// <summary>
        /// Cancela una persona.
        /// Cambia IDStatus a 2.
        /// No permite cancelar si ya esta cancelada.
        /// </summary>
        [HttpPut("cancelar")]
        [Authorize(Policy = "perm:proveedores.estado.editar")]
        public async Task<IActionResult> Cancelar(
            [FromBody] PersonaInputDTO input)
        {
            if (!input.IDPersona.HasValue)
            {
                return BadRequest("IDPersona es requerido para cancelar.");
            }

            var result = await _service.CancelarAsync(input.IDPersona.Value, SellarAutoria(input));
            return Ok(result);
        }

        /// <summary>
        /// Reactiva una persona.
        /// Cambia IDStatus a 1.
        /// </summary>
        [HttpPut("reactivar")]
        [Authorize(Policy = "perm:proveedores.estado.editar")]
        public async Task<IActionResult> Reactivar(
            [FromBody] PersonaInputDTO input)
        {
            if (!input.IDPersona.HasValue)
            {
                return BadRequest("IDPersona es requerido para reactivar.");
            }

            var result = await _service.ReactivarAsync(input.IDPersona.Value, SellarAutoria(input));
            return Ok(result);
        }
    }
}
