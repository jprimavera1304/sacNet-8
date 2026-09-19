using ISL_Service.Application.DTOs.VentasSaldos;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/*
  Los saldos del cliente que se enseñan al lado de la consulta de ventas.

  Va en su propio controlador y no dentro de VentasConsultaController porque no
  es una vista mas de la consulta: no depende del periodo, ni del agente, ni de
  la pestaña. Depende de UN cliente y se pide cada vez que se cambia de renglon.
*/
[ApiController]
[Route("api/ventas/saldos")]
[Authorize]
public class VentasSaldosController : ControllerBase
{
    private readonly IVentasSaldosService _service;

    public VentasSaldosController(IVentasSaldosService service)
    {
        _service = service;
    }

    [HttpGet("cliente/{idCliente:int}")]
    public async Task<IActionResult> SaldosDeCliente(int idCliente, CancellationToken ct)
    {
        var data = await _service.ConsultarSaldosClienteAsync(
            new VentasSaldosRequest { IdCliente = idCliente },
            ct);

        /* No se cachea: es un saldo, y uno viejo es peor que ninguno. */
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Saldos consultados.", data });
    }
}
