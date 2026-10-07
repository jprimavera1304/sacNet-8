using ISL_Service.Application.DTOs.Recaudacion;
using ISL_Service.Application.Services;
using ISL_Service.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers
{

    /// <summary>
    /// Consulta publica de un ticket de recaudacion por su codigo QR.
    ///
    /// EL CANDADO ES AL REVES QUE EN EL RESTO DE LA API
    /// ------------------------------------------------
    /// La clase lleva <c>[Authorize]</c> para que cualquier metodo que se
    /// agregue manana nazca cerrado. El unico metodo abierto lo declara con
    /// <c>[AllowAnonymous]</c> y explica por que (ver el metodo).
    ///
    /// AQUI VIVIAN CUATRO METODOS DE PLANTILLA DE VISUAL STUDIO
    /// -------------------------------------------------------
    /// <c>Get()</c> devolvia <c>["value1","value2"]</c>, y <c>Post</c>,
    /// <c>Put</c> y <c>Delete</c> tenian el cuerpo vacio y contestaban 200.
    /// Estaban publicados SIN autenticacion: <c>DELETE /api/Recaudaciones/5</c>
    /// respondia exito sin borrar nada. Se borraron: no eran un pendiente, eran
    /// andamio.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
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


        /// <summary>
        /// Devuelve el ticket que representa un codigo QR.
        /// </summary>
        /// <remarks>
        /// ES ANONIMO A PROPOSITO, Y ESTE ES EL PORQUE
        /// -------------------------------------------
        /// Lo escanea el ciudadano al que se le acaba de entregar un ticket de
        /// papel. Abre la camara del telefono, el QR lo lleva a la pantalla
        /// publica de consulta (Web/QR_Web, modulo custom/aviacion/qr) y esa
        /// pantalla llama aqui con <c>fetch(url, { method: "GET" })</c> y SIN
        /// cabecera <c>Authorization</c>: no tiene sesion que adjuntar. La
        /// propia pantalla no tiene login — su <c>login.html</c> es una maqueta
        /// sin API detras y el enlace "Iniciar sesion" esta oculto
        /// (<c>is-hidden</c>) en el <c>index.html</c>. Exigir token aqui
        /// apagaria la verificacion de tickets para todo el mundo.
        ///
        /// Comprobar un ticket que ya tienes en la mano no es un dato
        /// reservado: es el mismo papel, leido de la fuente.
        ///
        /// QUE SI SE CERRO
        /// ---------------
        /// 1. El pase no es un numero adivinable. No entra un
        ///    <c>IDRecaudacion</c>, entra la cadena cifrada completa
        ///    (<c>IDRecaudacion|IDCaja|Dv1</c>): sin la llave no se fabrica, y
        ///    los tres datos tienen que cuadrar entre si.
        /// 2. Ya no sale el DTO interno de 60 campos. Sale
        ///    <see cref="RecaudacionPublicaDTO"/>, que es lo que la pantalla
        ///    pinta y nada mas. Lo que mas importaba quitar: DV1 y DV2, los
        ///    digitos con los que se arma el QR; el plan de cuentas; y los ids
        ///    internos.
        /// </remarks>
        /// <response code="200">El ticket</response>
        /// <response code="400">El codigo no es un ticket valido</response>
        /// <response code="404">No existe ese ticket</response>
        [HttpGet("{qr}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<RecaudacionPublicaDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult Get(string qr)
        {

            string cadenaDesencriptada = "";
            int IDRecaudacion = 0, IDCaja = 0, Dv1 = 0;

            try
            {
                if (string.IsNullOrWhiteSpace(qr))
                {
                    return BadRequest(new { message = "Ticket inválido.", traceId = HttpContext.TraceIdentifier });
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
                    _logger.LogWarning("QR inválido en Recaudaciones. rawLen={RawLen}, traceId={TraceId}",
                        qr.Length, HttpContext.TraceIdentifier);
                    return BadRequest(new { message = "Ticket inválido.", traceId = HttpContext.TraceIdentifier });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al desencriptar QR en Recaudaciones. traceId={TraceId}",
                    HttpContext.TraceIdentifier);
                return BadRequest(new { message = "Ticket inválido.", traceId = HttpContext.TraceIdentifier });
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

                // Se recorta ANTES de salir del controlador: el endpoint es
                // anonimo y solo debe entregar lo que la pantalla publica pinta.
                return Ok(result.Select(RecaudacionPublicaDTO.Desde).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consultando recaudacion. id={IDRecaudacion}, caja={IDCaja}, traceId={TraceId}",
                    IDRecaudacion, IDCaja, HttpContext.TraceIdentifier);
                return StatusCode(500, new { message = "Error interno al consultar ticket.", traceId = HttpContext.TraceIdentifier });
            }
        }

    }

}
