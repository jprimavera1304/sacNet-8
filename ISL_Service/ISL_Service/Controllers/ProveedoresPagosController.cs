using ISL_Service.Application.DTOs.ProveedoresPagos;
using ISL_Service.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProveedoresPagosController : ControllerBase
    {
        private readonly ProveedoresPagosService _service;

        public ProveedoresPagosController(ProveedoresPagosService service)
        {
            _service = service;
        }

        // POST api/ProveedoresPagos/consultar
        [HttpPost("consultar")]
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
                return StatusCode(500, "Error en la aplicacion: " + ex.Message);
            }
        }

        // POST api/ProveedoresPagos/insertar
        [HttpPost("insertar")]
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
                return StatusCode(500, "Error en la aplicacion: " + ex.Message);
            }
        }

        // POST api/ProveedoresPagos/actualizar
        [HttpPost("actualizar")]
        public IActionResult Actualizar([FromBody] ProveedorPagoInputDTO input)
        {
            try
            {
                _service.Actualizar(input);
                return Ok("OK");
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Error en la aplicacion: " + ex.Message);
            }
        }

        // POST api/ProveedoresPagos/cancelar
        [HttpPost("cancelar")]
        public IActionResult Cancelar([FromBody] ProveedorPagoInputDTO input)
        {
            try
            {
                _service.Cancelar(input);
                return Ok("OK");
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Error en la aplicacion: " + ex.Message);
            }
        }
    }
}
