using ISL_Service.Application.DTOs.Recaudacion;
using ISL_Service.Application.Services;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace ISL_Service.Controllers
{

    [Route("api/[controller]")]
    [ApiController]
    public class RecaudacionesController : ControllerBase
    {


        private readonly RecaudacionService _recaudacionService;


        // Inyección del servicio a través del constructor
        public RecaudacionesController(RecaudacionService recaudacionService)
        {
            _recaudacionService = recaudacionService;
        }


        // GET: api/<RecaudacionesController>
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        ////// GET api/<RecaudacionesController>/5
        ////[HttpGet("{id}")]
        ////public string Get(int id)
        ////{
        ////    return "value";
        ////}


        [HttpGet("{r}/{c}")]
        public IActionResult Get(int r, int c)
        {

            try
            {
                RecaudacionInputDTO recaudacionInputDTO = new RecaudacionInputDTO();
                recaudacionInputDTO.IDRecaudacion = r;
                recaudacionInputDTO.IDCaja = c;

                var result = _recaudacionService.GetById(recaudacionInputDTO);
                if (result == null || result.Count == 0) return NotFound("No se encontraron registros.");

                return Ok(result);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Error al ejecutar el procedimiento almacenado"))
                {
                    return StatusCode(500, "Error en la conexión o al ejecutar el procedimiento almacenado: " + ex.Message);
                }
                return StatusCode(500, "Ocurrió un error inesperado: " + ex.Message);
            }

            ////var result = recaudacionService.GetById(id);
            ////if (result == null) return NotFound();
            ////return Ok(result);
        }


        // POST api/<RecaudacionesController>
        [HttpPost]
        public void Post([FromBody] string value)
        {
        }

        // PUT api/<RecaudacionesController>/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/<RecaudacionesController>/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
