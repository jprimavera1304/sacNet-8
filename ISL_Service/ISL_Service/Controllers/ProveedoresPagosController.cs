using ISL_Service.Application.DTOs.ProveedoresPagos;
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
    public class ProveedoresPagosController : ControllerBase
    {
        private readonly ProveedoresPagosService _service;

        public ProveedoresPagosController(ProveedoresPagosService service)
        {
            _service = service;
        }

        // POST api/ProveedoresPagos/consultar
        [HttpPost("consultar")]
        [Authorize(Policy = "perm:pagosproveedores.ver")]
        public IActionResult Consultar([FromBody] ProveedorPagoInputDTO input)
        {
            try
            {
                var result = _service.Consultar(input);
                if (result == null || result.Count == 0) return NotFound("No se encontraron registros.");
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Error en la aplicación: " + ex.Message);
            }
        }

        // POST api/ProveedoresPagos/insertar
        [HttpPost("insertar")]
        [Authorize(Policy = "perm:pagosproveedores.crear")]
        public IActionResult Insertar([FromBody] ProveedorPagoInputDTO input)
        {
            try
            {
                int id = _service.Insertar(input);
                if (id <= 0) return StatusCode(500, "No se pudo insertar el pago.");
                return Ok(new { IDProveedorPago = id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Error en la aplicación: " + ex.Message);
            }
        }

        // POST api/ProveedoresPagos/actualizar
        [HttpPost("actualizar")]
        [Authorize(Policy = "perm:pagosproveedores.editar")]
        public IActionResult Actualizar([FromBody] ProveedorPagoInputDTO input)
        {
            try
            {
                _service.Actualizar(input);
                return Ok("OK");
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Error en la aplicación: " + ex.Message);
            }
        }

        // POST api/ProveedoresPagos/cancelar
        [HttpPost("cancelar")]
        [Authorize(Policy = "perm:pagosproveedores.cancelar")]
        public IActionResult Cancelar([FromBody] ProveedorPagoInputDTO input)
        {
            try
            {
                _service.Cancelar(input);
                return Ok("OK");
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Error en la aplicación: " + ex.Message);
            }
        }
    }
}
