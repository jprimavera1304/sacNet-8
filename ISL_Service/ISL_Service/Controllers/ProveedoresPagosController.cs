using ISL_Service.Application.DTOs.ProveedoresPagos;
using ISL_Service.Application.Security;
using ISL_Service.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers
{
    /*
        ESTE CONTROLADOR ESTABA ABIERTO DE PAR EN PAR.

        No tenia [Authorize] ninguno: cualquiera que conociera la URL podia
        consultar los pagos a proveedores y, peor, insertarlos, modificarlos y
        cancelarlos sin identificarse. Tres de las cuatro acciones ESCRIBEN.

        Los permisos ya existian -se sembraron con el modulo, PermissionService
        los declara como pagosproveedores.ver/crear/editar/cancelar- y nadie los
        habia conectado. Asi que esto no inventa un modelo nuevo: usa el que ya
        estaba escrito y sin estrenar.

        No rompe al web: su helper de red (docs/shared/js/safety/api.js) ya
        adjunta el Bearer en toda peticion cuando hay sesion, asi que las
        pantallas que ya lo usaban siguen igual.
    */
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    /*
        Y LA AUTORIA LA ELEGIA EL CLIENTE.

        Los seis campos de auditoria del DTO (IDUsuario/Equipo,
        IDUsuarioModificacion/EquipoModificacion,
        IDUsuarioCancelacion/EquipoCancelacion) se tomaban del cuerpo, y el
        servicio ademas los EXIGIA ("IDUsuario es requerido."). O sea que quien
        quedaba firmando el alta, la modificacion o la cancelacion de un pago a
        proveedor lo decidia quien llamaba. Aqui sale dinero: la firma es lo
        unico que dice quien lo autorizo.

        Ahora los pisa SellarAutoria() con el token, como en el resto del
        backend. Lo que venga en el cuerpo se descarta.

        Tambien se quito el "Error en la aplicación: " + ex.Message de los cuatro
        catch: devolvia el mensaje del motor SQL al cliente (nombres de
        procedimiento y de columna) incluso en Production. Sin catch, la
        excepcion llega al ExceptionHandlingMiddleware, que ya decide que se
        ensena segun el entorno y registra el detalle del lado del servidor.
    */
    public class ProveedoresPagosController : ControllerBase
    {
        private readonly ProveedoresPagosService _service;
        private readonly ICurrentUserAccessor _currentUser;

        public ProveedoresPagosController(ProveedoresPagosService service, ICurrentUserAccessor currentUser)
        {
            _service = service;
            _currentUser = currentUser;
        }

        /// <summary>
        /// Quien y desde donde, del token. El cliente NUNCA los manda: si
        /// viajaran en el body, cualquiera podria firmar un pago a nombre de otro.
        /// </summary>
        private ProveedorPagoInputDTO SellarAutoria(ProveedorPagoInputDTO input)
        {
            var idUsuario = _currentUser.GetLegacyUserId(User);
            var equipo = _currentUser.GetUsername(User, Environment.MachineName);

            input.IDUsuario = idUsuario;
            input.Equipo = equipo;
            input.IDUsuarioModificacion = idUsuario;
            input.EquipoModificacion = equipo;
            input.IDUsuarioCancelacion = idUsuario;
            input.EquipoCancelacion = equipo;

            return input;
        }

        // POST api/ProveedoresPagos/consultar
        [HttpPost("consultar")]
        [Authorize(Policy = "perm:pagosproveedores.ver")]
        public IActionResult Consultar([FromBody] ProveedorPagoInputDTO input)
        {
            if (input is null) return BadRequest(new { message = "Body requerido." });

            var result = _service.Consultar(input);
            if (result == null || result.Count == 0) return NotFound("No se encontraron registros.");
            return Ok(result);
        }

        // POST api/ProveedoresPagos/insertar
        [HttpPost("insertar")]
        [Authorize(Policy = "perm:pagosproveedores.crear")]
        public IActionResult Insertar([FromBody] ProveedorPagoInputDTO input)
        {
            if (input is null) return BadRequest(new { message = "Body requerido." });

            int id = _service.Insertar(SellarAutoria(input));
            if (id <= 0) return StatusCode(500, "No se pudo insertar el pago.");
            return Ok(new { IDProveedorPago = id });
        }

        // POST api/ProveedoresPagos/actualizar
        [HttpPost("actualizar")]
        [Authorize(Policy = "perm:pagosproveedores.editar")]
        public IActionResult Actualizar([FromBody] ProveedorPagoInputDTO input)
        {
            if (input is null) return BadRequest(new { message = "Body requerido." });

            _service.Actualizar(SellarAutoria(input));
            return Ok("OK");
        }

        // POST api/ProveedoresPagos/cancelar
        [HttpPost("cancelar")]
        [Authorize(Policy = "perm:pagosproveedores.cancelar")]
        public IActionResult Cancelar([FromBody] ProveedorPagoInputDTO input)
        {
            if (input is null) return BadRequest(new { message = "Body requerido." });

            _service.Cancelar(SellarAutoria(input));
            return Ok("OK");
        }
    }
}
