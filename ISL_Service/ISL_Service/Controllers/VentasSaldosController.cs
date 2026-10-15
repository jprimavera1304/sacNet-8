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

    /*
      EL SALDO DE UN CLIENTE NO SE ENSEÑA A CUALQUIERA CON SESION.

      Esto no pedia ningun permiso. Medido el 2026-10-12 con una cuenta de
      auditoria sin un solo permiso: contestaba 200 con el saldo, el credito
      disponible y los vencimientos de cualquier cliente, solo por tener un
      token. Era el unico de los 220 GET del servicio que entregaba dinero de
      un tercero sin preguntar nada.

      LAS CINCO CLAVES SON LAS DE SUS CUATRO LLAMADORES, ni una mas. Se buscaron
      antes de cerrar, porque cerrar de mas aqui rompe la captura de una venta:

        web     ventasSaldos.ts   (pantalla de ventas)   -> ventas.consultar
                clientes.ts       (ficha del cliente)     -> clientes.ver_modulo
        movil   ventas_consulta.dart                      -> app_movil.ventas.consulta.ver
                clientes_datos.dart                       -> app_movil.clientes

      `ventas.ver_modulo` va ademas porque es la llave con la que la propia
      pantalla de ventas decide abrirse (VentasConsultaController), asi que
      quien entra a Ventas puede ver el saldo del renglon que esta mirando.

      Y van las del movil aparte a proposito: el telefono se presta y la
      computadora de la oficina no (ver PermisoControllerBase).
    */
    [Authorize(Policy = "perm:ventas.consultar|ventas.ver_modulo|clientes.ver_modulo|app_movil.ventas.consulta.ver|app_movil.clientes")]
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
