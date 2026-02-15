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
            if (!TryParseQrPayload(tokenRaw, out var idRecaudacion, out var idCaja, out var dv1, out var parseMode))
            {
                _logger.LogWarning(
                    "QR invalido en Recaudaciones. rawLen={RawLength}, traceId={TraceId}",
                    tokenRaw.Length,
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
                    "Error SQL en /api/Recaudaciones/{Qr}. idRecaudacion={IdRecaudacion}, idCaja={IdCaja}, parseMode={ParseMode}, traceId={TraceId}",
                    tokenRaw,
                    idRecaudacion,
                    idCaja,
                    parseMode,
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
                    "Error no controlado en /api/Recaudaciones/{Qr}. idRecaudacion={IdRecaudacion}, idCaja={IdCaja}, parseMode={ParseMode}, traceId={TraceId}",
                    tokenRaw,
                    idRecaudacion,
                    idCaja,
                    parseMode,
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

        private static bool TryParseQrPayload(
            string token,
            out int idRecaudacion,
            out int idCaja,
            out int dv1,
            out string parseMode)
        {
            idRecaudacion = 0;
            idCaja = 0;
            dv1 = 0;
            parseMode = "none";

            // 1) Soporte directo para payload plano "id|caja|dv1"
            if (TryParsePayloadParts(token, out idRecaudacion, out idCaja, out dv1))
            {
                parseMode = "plain";
                return true;
            }

            // 2) Soporte para id simple (fallback extremo para no bloquear consulta)
            if (int.TryParse(token, out idRecaudacion) && idRecaudacion > 0)
            {
                idCaja = 0;
                dv1 = 0;
                parseMode = "id_only";
                return true;
            }

            // 3) Intentar con varias normalizaciones + decrypt (compatibilidad legacy/multi)
            foreach (var candidate in BuildTokenCandidates(token))
            {
                var decrypt = new Encryptacion().Decrypt(candidate);
                if (!string.IsNullOrWhiteSpace(decrypt) &&
                    TryParsePayloadParts(decrypt, out idRecaudacion, out idCaja, out dv1))
                {
                    parseMode = "decrypt";
                    return true;
                }
            }

            return false;
        }

        private static bool TryParsePayloadParts(string payload, out int idRecaudacion, out int idCaja, out int dv1)
        {
            idRecaudacion = 0;
            idCaja = 0;
            dv1 = 0;

            var parts = payload.Split('|');
            if (parts.Length < 3)
            {
                return false;
            }

            return int.TryParse(parts[0], out idRecaudacion)
                && int.TryParse(parts[1], out idCaja)
                && int.TryParse(parts[2], out dv1);
        }

        private static IEnumerable<string> BuildTokenCandidates(string tokenRaw)
        {
            var candidates = new List<string>();
            if (string.IsNullOrWhiteSpace(tokenRaw))
            {
                return candidates;
            }

            void AddIfMissing(string value)
            {
                if (!string.IsNullOrWhiteSpace(value) && !candidates.Contains(value))
                {
                    candidates.Add(value);
                }
            }

            AddIfMissing(tokenRaw);

            var unescaped = tokenRaw.Contains('%') ? Uri.UnescapeDataString(tokenRaw) : tokenRaw;
            AddIfMissing(unescaped);

            var legacy = unescaped.Replace("CBA-_ABC", "/")
                                  .Replace("_", "=")
                                  .Replace("-", "+")
                                  .Replace("*", "&");
            AddIfMissing(legacy);

            // Base64Url estandar (por si llega con formato url-safe)
            var base64Url = unescaped.Replace('-', '+').Replace('_', '/');
            var mod4 = base64Url.Length % 4;
            if (mod4 > 0)
            {
                base64Url = base64Url.PadRight(base64Url.Length + (4 - mod4), '=');
            }
            AddIfMissing(base64Url);

            return candidates;
        }
    }
}
