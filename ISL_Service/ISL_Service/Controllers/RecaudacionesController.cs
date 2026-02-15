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
        private readonly ILogger<RecaudacionesController> _logger;

        // Inyección del servicio a través del constructor
        public RecaudacionesController(RecaudacionService recaudacionService, ILogger<RecaudacionesController> logger)
        {
            _recaudacionService = recaudacionService;
            _logger = logger;
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
                if (string.IsNullOrWhiteSpace(qr))
                {
                    return BadRequest(new { message = "Ticket invalido.", traceId = HttpContext.TraceIdentifier });
                }

                Encryptacion encryptacion = new Encryptacion();

                // Compatibilidad con formatos viejo y nuevo.
                qr = qr.Replace("@", "/");
                qr = qr.Replace("CBA-_ABC", "/");
                qr = qr.Replace("_", "=");
                qr = qr.Replace("-", "+");
                qr = qr.Replace("*", "&");

                // IDRecaudacion + | + IDCaja + | + Dv1
                cadenaDesencriptada = encryptacion.Decrypt(qr);
                var partes = cadenaDesencriptada.Split('|');
                if (partes.Length < 3 ||
                    !int.TryParse(partes[0], out IDRecaudacion) ||
                    !int.TryParse(partes[1], out IDCaja) ||
                    !int.TryParse(partes[2], out Dv1))
                {
                    _logger.LogWarning("QR invalido en Recaudaciones. rawLen={RawLen}, traceId={TraceId}",
                        qr.Length, HttpContext.TraceIdentifier);
                    return BadRequest(new { message = "Ticket invalido.", traceId = HttpContext.TraceIdentifier });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al desencriptar QR en Recaudaciones. traceId={TraceId}",
                    HttpContext.TraceIdentifier);
                return BadRequest(new { message = "Ticket invalido.", traceId = HttpContext.TraceIdentifier });
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
                _logger.LogError(ex, "Error consultando recaudacion. id={IDRecaudacion}, caja={IDCaja}, traceId={TraceId}",
                    IDRecaudacion, IDCaja, HttpContext.TraceIdentifier);
                return StatusCode(500, new { message = "Error interno al consultar ticket.", traceId = HttpContext.TraceIdentifier });
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
