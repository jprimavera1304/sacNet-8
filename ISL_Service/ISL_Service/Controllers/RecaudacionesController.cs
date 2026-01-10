using ISL_Service.Application.DTOs.Recaudacion;
using ISL_Service.Application.Services;
using ISL_Service.Utils;
using Microsoft.AspNetCore.Mvc;

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


        //[HttpGet("{r}/{c}")]
        //public IActionResult Get(int r, int c)

        [HttpGet("{qr}")]
        public IActionResult Get(string qr)
        {

            string cadenaDesencriptada = "";
            int IDRecaudacion = 0, IDCaja = 0, Dv1 = 0;

            try
            {
                Encryptacion encryptacion = new Encryptacion();

                qr = qr.Replace("@", "/");
                qr = qr.Replace("_", "=");
                qr = qr.Replace("*", "&");

                // IDRecaudacion + | + IDCaja + | + Dv1
                cadenaDesencriptada = encryptacion.Decrypt(qr);

                IDRecaudacion = int.Parse(cadenaDesencriptada.Split("|")[0].ToString());
                IDCaja = int.Parse(cadenaDesencriptada.Split("|")[1].ToString());
                Dv1 = int.Parse(cadenaDesencriptada.Split("|")[2].ToString());
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Ticket Inválido");
            }

            try
                {
                RecaudacionInputDTO recaudacionInputDTO = new RecaudacionInputDTO();

                // IDRecaudacion + | + IDCaja + | + Dv1
                recaudacionInputDTO.IDRecaudacion = IDRecaudacion;
                recaudacionInputDTO.IDCaja = IDCaja;
                recaudacionInputDTO.Dv1 = Dv1;

                var result = _recaudacionService.GetById(recaudacionInputDTO);
                if (result == null || result.Count == 0) return NotFound("No se encontraron registros.");

                return Ok(result);
            }
            catch (Exception ex)
            {
                //if (ex.Message.Contains("Error al ejecutar el procedimiento almacenado"))
                //{
                //    return StatusCode(500, "Error en la conexión o al ejecutar el procedimiento almacenado: " + ex.Message);
                //}

                return StatusCode(500, "Error en la aplicación: " + ex.Message);
            }
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