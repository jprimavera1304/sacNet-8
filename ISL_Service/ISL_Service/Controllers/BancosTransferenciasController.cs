using ISL_Service.Application.DTOs.OperacionMac;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Transferencias bancarias: el estado de cuenta que se sube, los movimientos que
/// se identifican con un cliente y los pagos que se aplican a las ventas.
/// AQUI SE MUEVE DINERO.
/// </summary>
[ApiController]
[Route("api/bancos-transferencias")]
[Authorize]
public class BancosTransferenciasController : PermisoControllerBase
{
    private static readonly string[] PermisosVer = { "transferencias.ver" };

    // Cargar el estado de cuenta no mueve dinero, pero es lo que se concilia
    // despues: un archivo mal cargado descuadra todo lo que venga detras.
    private static readonly string[] PermisosCargar = { "transferencias.cargar" };

    private static readonly string[] PermisosIdentificar = { "transferencias.identificar" };

    // Aplicar y cancelar SI mueven dinero: tocan Ventas, [Ventas Pagos] y el
    // saldo del cliente. Cada uno con su permiso.
    private static readonly string[] PermisosAplicar = { "transferencias.aplicar" };
    private static readonly string[] PermisosCancelar = { "transferencias.cancelar" };

    private readonly IOperacionMacService _service;

    public BancosTransferenciasController(
        IOperacionMacService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    /// <summary>
    /// Estados de cuenta.
    ///
    /// La precedencia de los filtros es EXPLICITA y en este orden: `idBancoTransferencia`,
    /// luego `archivo`, luego el resto. Es la cascada de legacy, que alla vivia en
    /// tres reemplazos del objeto de filtros y dejaba fuera, sin avisar, los
    /// filtros que el usuario habia puesto.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Consultar([FromQuery] TransferenciasFiltro? filtro, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        filtro ??= new TransferenciasFiltro();
        var error = ValidaRango(filtro.Desde, filtro.Hasta);
        if (error != null) return BadRequest(error);

        var data = await _service.ConsultarTransferenciasAsync(filtro, ct);
        return Ok(new { ok = true, message = "Estados de cuenta consultados.", data, total = data.Count });
    }

    /// <summary>
    /// Movimientos de los estados de cuenta. Aqui todos los filtros conviven: el
    /// procedimiento los combina y legacy tampoco descartaba ninguno.
    /// </summary>
    [HttpPost("movimientos/consultar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConsultarMovimientos([FromBody] MovimientosTransferenciaFiltro? filtro, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        filtro ??= new MovimientosTransferenciaFiltro();
        var error = ValidaRango(filtro.Desde, filtro.Hasta);
        if (error != null) return BadRequest(error);

        var data = await _service.ConsultarMovimientosTransferenciaAsync(filtro, ct);
        return Ok(new { ok = true, message = "Movimientos consultados.", data, total = data.Count });
    }

    /// <summary>
    /// Carga un estado de cuenta completo: la cabecera y TODOS sus movimientos, en
    /// una sola peticion y en una sola transaccion.
    ///
    /// ESTO ES LO QUE CAMBIA DE FONDO RESPECTO A LEGACY. Alla el procedimiento
    /// inserta un movimiento por llamada, y el bucle vivia en el cliente: N
    /// peticiones HTTP sueltas, sin transaccion. Un archivo de 400 renglones que
    /// fallara en el 250 dejaba 249 movimientos cargados y el estado de cuenta a
    /// medias, sin forma de saber desde donde seguir ni de deshacerlo. Y un estado
    /// de cuenta incompleto se concilia mal contra los pagos: faltan depositos que
    /// si entraron al banco.
    ///
    /// Tambien se comprueba que `numMovimientos` (lo que dice el archivo) coincida
    /// con los renglones que llegan. Legacy mandaba el numero a la base sin
    /// compararlo con nada.
    /// </summary>
    /// <response code="201">Estado de cuenta cargado completo</response>
    /// <response code="400">Datos invalidos, o la base rechazo un movimiento (no se cargo ninguno)</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cargar([FromBody] CargarTransferenciasRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        if (string.IsNullOrWhiteSpace(request.Archivo))
            return BadRequest(new { ok = false, message = "El nombre del archivo es requerido." });
        if (request.Movimientos.Count == 0)
            return BadRequest(new { ok = false, message = "El archivo no trae movimientos." });
        if (request.NumMovimientos > 0 && request.NumMovimientos != request.Movimientos.Count)
            return BadRequest(new
            {
                ok = false,
                message = $"El archivo dice traer {request.NumMovimientos} movimientos y llegaron {request.Movimientos.Count}. No se cargo nada."
            });
        if (request.Movimientos.Any(x => x.Fecha == default))
            return BadRequest(new { ok = false, message = "Hay un movimiento sin fecha." });

        var sinPermiso = await ExigirPermisoAsync(PermisosCargar, ct);
        if (sinPermiso != null) return sinPermiso;

        var (resultado, cargados) = await _service.CargarTransferenciasAsync(request, IdUsuarioLegacy(), Equipo(), ct);

        if (!resultado.Guardo)
            return BadRequest(new { ok = false, message = resultado.Mensaje, data = resultado });

        return StatusCode(StatusCodes.Status201Created, new
        {
            ok = true,
            message = $"Estado de cuenta cargado con {cargados} movimientos.",
            data = resultado,
            cargados
        });
    }

    /// <summary>
    /// Dice a que cliente pertenecen uno o varios movimientos.
    ///
    /// El cliente es obligatorio: en legacy no se validaba y se podia identificar
    /// contra el cliente 0, con lo que pasara a hacer el procedimiento con un cero.
    /// </summary>
    [HttpPut("movimientos/identificar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Identificar([FromBody] IdentificarTransferenciasRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        if (request.IdsMovimiento.Count == 0)
            return BadRequest(new { ok = false, message = "No hay movimientos que identificar." });
        if (request.IdsMovimiento.Any(x => x <= 0))
            return BadRequest(new { ok = false, message = "Hay un movimiento sin id." });
        if (request.IdCliente <= 0)
            return BadRequest(new { ok = false, message = "El cliente es requerido." });

        var sinPermiso = await ExigirPermisoAsync(PermisosIdentificar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(
            await _service.IdentificarTransferenciasAsync(request, IdUsuarioLegacy(), Equipo(), ct),
            "Movimientos identificados.");
    }

    /// <summary>
    /// Aplica un movimiento bancario a una o varias ventas. AQUI SE MUEVE DINERO.
    ///
    /// DOS COSAS QUE CAMBIAN:
    ///  - Los pagos viajan como lista de pares venta/monto. En legacy eran dos
    ///    cadenas unidas con '~' apareadas por posicion, y nadie comprobaba que
    ///    tuvieran la misma longitud: un pago aplicado a la venta equivocada, en
    ///    silencio y sin rastro.
    ///  - `aplicarA` es un valor obligatorio ("dinero" o "cascos"), no dos
    ///    banderas. En legacy eran dos booleanos con dos `if` sueltos: con los dos
    ///    en true ganaba cascos sin avisar, y con los dos en false se armaba un
    ///    procedimiento con nombre VACIO y el usuario leia "[No se pudo procesar ]
    ///    - ..." sin saber que le faltaba elegir.
    /// </summary>
    [HttpPost("movimientos/aplicar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Aplicar([FromBody] AplicarTransferenciaRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        if (request.IdMovimiento <= 0)
            return BadRequest(new { ok = false, message = "El movimiento es requerido." });
        if (request.Pagos.Count == 0)
            return BadRequest(new { ok = false, message = "No hay pagos que aplicar." });
        if (request.Pagos.Any(x => x.IdVenta <= 0))
            return BadRequest(new { ok = false, message = "Hay un pago sin venta." });
        if (request.Pagos.Any(x => x.Monto <= 0))
            return BadRequest(new { ok = false, message = "Hay un pago con monto cero o negativo." });
        if (request.Pagos.Select(x => x.IdVenta).Distinct().Count() != request.Pagos.Count)
            return BadRequest(new { ok = false, message = "Hay una venta repetida: no se sabria que monto aplicarle." });

        var sinPermiso = await ExigirPermisoAsync(PermisosAplicar, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.AplicarTransferenciaAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        if (data is null)
            return BadRequest(new
            {
                ok = false,
                message = "Falta decir a que se aplica: 'dinero' o 'cascos'."
            });

        return Responder(data, "Transferencia aplicada.");
    }

    /// <summary>
    /// Cancela un movimiento ya aplicado: revierte el pago en Ventas,
    /// [Ventas Pagos], [Ventas Pagos Usados] y el saldo del cliente.
    ///
    /// LA RUTA DICE MOVIMIENTO, NO ESTADO DE CUENTA, y eso corrige una operacion
    /// que hoy esta ROTA. El procedimiento se llama sp_n_CancelarBancoTransferencia
    /// pero su parametro es @IDBancoTransferenciaMovimiento; el repositorio de
    /// legacy le manda @IDBancoTransferencia, un parametro que no existe, asi que
    /// SQL Server rechaza la llamada antes de ejecutar nada. Cancelar una
    /// transferencia desde MacServicios2 no funciona.
    /// </summary>
    [HttpPost("movimientos/{idMovimiento:int}/cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelarMovimiento([FromRoute] int idMovimiento, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosCancelar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(
            await _service.CancelarMovimientoTransferenciaAsync(idMovimiento, IdUsuarioLegacy(), Equipo(), ct),
            "Movimiento cancelado.");
    }

    private IActionResult Responder(OperacionEscrituraResponse r, string mensajeExito)
        => Ok(new
        {
            ok = r.Guardo,
            message = r.Guardo
                ? mensajeExito
                : (string.IsNullOrWhiteSpace(r.Mensaje) ? "La base rechazo la operacion." : r.Mensaje),
            data = r
        });

    private static object? ValidaRango(DateTime? desde, DateTime? hasta)
    {
        if (desde.HasValue != hasta.HasValue)
            return new { ok = false, message = "El rango de fechas necesita las dos fechas." };
        if (desde.HasValue && hasta!.Value < desde.Value)
            return new { ok = false, message = "El rango de fechas termina antes de empezar." };
        return null;
    }
}
