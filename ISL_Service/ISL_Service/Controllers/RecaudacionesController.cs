using ISL_Service.Application.DTOs.Recaudacion;
using ISL_Service.Application.Services;
using ISL_Service.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RecaudacionesController : ControllerBase
    {
        private readonly RecaudacionService _recaudacionService;
        private readonly ILogger<RecaudacionesController> _logger;

        // Inyeccion del servicio a traves del constructor
        public RecaudacionesController(
            RecaudacionService recaudacionService,
            ILogger<RecaudacionesController> logger)
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

        [HttpGet("{qr}")]
        public IActionResult Get(string qr)
        {
            if (string.IsNullOrWhiteSpace(qr))
            {
                return BadRequest(new { message = "El token QR es requerido." });
            }

            var tokenRaw = qr;
            var tokenNormalized = tokenRaw.Contains('%') ? Uri.UnescapeDataString(tokenRaw) : tokenRaw;
            tokenNormalized = tokenNormalized.Replace("CBA-_ABC", "/")
                                             .Replace("_", "=")
                                             .Replace("-", "+")
                                             .Replace("*", "&");

            if (!TryParseQrPayload(tokenNormalized, out var idRecaudacion, out var idCaja, out var dv1))
            {
                _logger.LogWarning(
                    "QR invalido en Recaudaciones. rawLen={RawLength}, normLen={NormalizedLength}, traceId={TraceId}",
                    tokenRaw.Length,
                    tokenNormalized.Length,
                    HttpContext.TraceIdentifier);

                return BadRequest(new
                {
                    message = "Ticket invalido.",
                    traceId = HttpContext.TraceIdentifier
                });
            }

            try
            {
                var recaudacionInputDTO = new RecaudacionInputDTO
                {
                    // IDRecaudacion + | + IDCaja + | + Dv1
                    IDRecaudacion = idRecaudacion,
                    IDCaja = idCaja,
                    Dv1 = dv1
                };

                var result = _recaudacionService.GetById(recaudacionInputDTO);
                if (result == null || result.Count == 0)
                {
                    return NotFound(new { message = "No se encontraron registros.", traceId = HttpContext.TraceIdentifier });
                }

                return Ok(result);
            }
            catch (SqlException ex)
            {
                _logger.LogError(
                    ex,
                    "Error SQL en /api/Recaudaciones/{Qr}. idRecaudacion={IdRecaudacion}, idCaja={IdCaja}, traceId={TraceId}",
                    tokenRaw,
                    idRecaudacion,
                    idCaja,
                    HttpContext.TraceIdentifier);

                return StatusCode(500, new
                {
                    message = "Error de base de datos al consultar la recaudacion.",
                    traceId = HttpContext.TraceIdentifier
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error no controlado en /api/Recaudaciones/{Qr}. idRecaudacion={IdRecaudacion}, idCaja={IdCaja}, traceId={TraceId}",
                    tokenRaw,
                    idRecaudacion,
                    idCaja,
                    HttpContext.TraceIdentifier);

                return StatusCode(500, new
                {
                    message = "Error interno al procesar la solicitud.",
                    traceId = HttpContext.TraceIdentifier
                });
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

        private static bool TryParseQrPayload(string token, out int idRecaudacion, out int idCaja, out int dv1)
        {
            idRecaudacion = 0;
            idCaja = 0;
            dv1 = 0;

            var decrypt = new Encryptacion().Decrypt(token);
            if (string.IsNullOrWhiteSpace(decrypt))
            {
                return false;
            }

            var parts = decrypt.Split('|');
            if (parts.Length < 3)
            {
                return false;
            }

            return int.TryParse(parts[0], out idRecaudacion)
                && int.TryParse(parts[1], out idCaja)
                && int.TryParse(parts[2], out dv1);
        }
    }
}
