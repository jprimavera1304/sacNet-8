using ISL_Service.Application.Centros;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/*
  LA LECTURA DE LA PANTALLA DE REASIGNAR CARTERA

  La ESCRITURA no esta aqui: es `POST /api/ventas/reasignar`, en
  `VentasPagosOperacionesController`, y se queda donde esta. Aqui solo se lee.

  POR QUE HACIA FALTA UN ENDPOINT PROPIO
  --------------------------------------
  Esta en el bloque de `Application/DTOs/VentasReasignacion/`. En una linea: las
  tres rutas con las que el web arma esta pantalla devuelven, para quien solo
  tiene `cobros.reasignar`, una cartera RECORTADA sin decirlo —y una cartera
  recortada en la pantalla que decide de quien es el dinero por cobrar es una
  mentira con consecuencias en la liquidacion de dos personas.

  EL PERMISO ES `cobros.reasignar`, EL MISMO QUE LA ESCRITURA
  ----------------------------------------------------------
  Y no uno nuevo de lectura. Porque lo que esta lectura ensena es exactamente lo
  que la escritura puede mover: la cartera completa de un cliente y el catalogo
  de a quien se le puede entregar. Un permiso de "solo ver la cartera de
  cualquiera" seria una llave nueva que hoy no existe en ninguna de las dos
  empresas, y repartir llaves nuevas no es parte de esto.

  La entrada al modulo ya esta resuelta y no hace falta tocarla:
  `EntradasDeModulo.cs:143` deriva `reasignacion.ver_modulo` de
  `cobros.reasignar`, y `PermisosDelUsuario.Tiene` exige las dos cosas. Asi que
  pedir `cobros.reasignar` ya exige la puerta.

  La clave del telefono (`app_movil.reasignacion.ver`) va EN PAREJA, como en el
  resto del backend, y hoy TODAVIA NO ESTA SEMBRADA: aceptarla no afloja nada
  (nadie la tiene) y el dia que se siembre esto funciona igual. Las claves a
  sembrar estan en `lib/modules/verticals/reasignacion.permisos.md` del repo del
  movil.

  CENTROS DE SERVICIO: 409, Y NO ES PEREZA
  ----------------------------------------
  Este modulo NO esta declarado en `ModulosConCentros`, y no debe estarlo. El
  motivo esta medido contra MacZ el 2026-10-15:

    - La matriz tiene 59 agentes; la base del centro 2 (`MacZCS2`) tiene 31.
      28 ids de la matriz NO EXISTEN alla.
    - Y 4 ids apuntan a OTRA PERSONA. El mas claro: `IDAgente = 26` es el
      agente numero 3 "JOSE ANTONIO LOPEZ SANCHEZ" en la matriz y el agente
      numero 27 "TONO" en el centro 2.
    - `Ventas.IDAgente` NO tiene llave ajena (comprobado en `sys.foreign_keys`),
      asi que nada en la base impide escribir un id que alla no existe.

  Juntando las tres: si esta pantalla leyera el catalogo de la matriz y
  escribiera en la base de un centro, elegir "MARIO" podria entregarle la
  cartera a otra persona, o a un agente que no existe — y entonces la cartera
  desaparece de cualquier consulta que cruce con `Agentes`. Eso no se deshace
  con un boton: para saber a quien pertenecia cada nota hay que ir a
  `[Ventas Reasignacion]` fila por fila.

  Asi que con `X-Centro != 0` se contesta 409 CON MOTIVO y NUNCA datos de la
  matriz, que es el punto 5 del contrato. El telefono lo pinta como tarjeta con
  un boton para volver a la matriz, no como error rojo.
*/
[ApiController]
[Route("api/ventas/reasignacion")]
[Authorize]
public class VentasReasignacionController : PermisoControllerBase
{
    /// <summary>
    /// El permiso de este modulo. La del web es la que el endpoint de ESCRITURA
    /// ya exige (`VentasPagosOperacionesController.cs:46`); la del movil va en
    /// pareja y todavia no esta sembrada.
    /// </summary>
    private static readonly string[] PermisoReasignar =
    {
        "cobros.reasignar",
        "app_movil.reasignacion.ver",
    };

    /// <summary>Lo que se le dice a quien pide esto desde un centro.</summary>
    public const string MotivoSinCentros =
        "Reasignar cartera todavía solo se puede en la matriz. El catálogo de agentes " +
        "de la matriz no coincide con el de los centros —28 de los 59 agentes no existen " +
        "en la base del centro y 4 identificadores apuntan a otra persona—, así que una " +
        "reasignación hecha desde aquí podría entregarle la cartera a quien no es.";

    private readonly IVentasReasignacionService _service;

    public VentasReasignacionController(
        IVentasReasignacionService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    /// <summary>
    /// Todo lo que la pantalla necesita para abrirse: el cliente con su agente
    /// actual, el catalogo de agentes y las notas vigentes del cliente.
    /// </summary>
    /// <param name="idCliente">0 = solo el catalogo de agentes.</param>
    /// <param name="desde">ISO. Vacio = un ano hacia atras.</param>
    /// <param name="hasta">ISO. Vacio = hoy.</param>
    [HttpGet("arranque")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ArranqueAsync(
        [FromQuery] int idCliente,
        [FromQuery] string? desde,
        [FromQuery] string? hasta,
        CancellationToken ct)
    {
        var fueraDeLaMatriz = RechazarSiEsOtroCentro();
        if (fueraDeLaMatriz is not null) return fueraDeLaMatriz;

        var veto = await ExigirPermisoAsync(PermisoReasignar, ct);
        if (veto is not null) return veto;

        var data = await _service.ArranqueAsync(idCliente, desde, hasta, ct);
        // La cartera y el agente de un cliente cambian con cada reasignacion:
        // una respuesta cacheada aqui es una pantalla que ensena al agente de
        // antes despues de haberlo movido.
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Arranque de reasignacion.", data });
    }

    /// <summary>El buscador de cliente de esta pantalla.</summary>
    /// <remarks>
    /// Existe aparte de `/api/ventas/pedidos/captura/clientes/buscar` porque ese
    /// exige la politica de MODIFICAR pedidos —un cobrador recibe 403— y ademas
    /// acota los clientes al AGENTE del usuario, que es el recorte que esta
    /// pantalla no puede permitirse: para quitarle la cartera a un agente hay
    /// que poder ver a los clientes de ese agente, no a los de uno mismo.
    /// </remarks>
    [HttpGet("clientes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> BuscarClientesAsync([FromQuery] string? q, CancellationToken ct)
    {
        var fueraDeLaMatriz = RechazarSiEsOtroCentro();
        if (fueraDeLaMatriz is not null) return fueraDeLaMatriz;

        var veto = await ExigirPermisoAsync(PermisoReasignar, ct);
        if (veto is not null) return veto;

        var data = await _service.BuscarClientesAsync(q, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Clientes encontrados.", data });
    }

    /// <summary>
    /// 409 con motivo si la peticion pide un centro. Nunca datos de la matriz.
    /// </summary>
    private IActionResult? RechazarSiEsOtroCentro()
    {
        var cabecera = Request.Headers[CentroPedido.Cabecera].ToString();

        if (!CentroPedido.Interpretar(cabecera, out var idCentro))
        {
            return Conflict(new
            {
                ok = false,
                code = "CENTRO_INVALIDO",
                message = "El centro pedido no es válido.",
                detail = $"La cabecera {CentroPedido.Cabecera} tiene que ser un número entero. " +
                         "Vuelve a elegir el centro.",
            });
        }

        if (idCentro == CentroPedido.Matriz) return null;

        return Conflict(new
        {
            ok = false,
            code = "MODULO_SIN_CENTROS",
            message = "Este módulo todavía no sabe de centros.",
            // EL `detail` ES DONDE VIAJA LA FRASE QUE DICE QUE HACER. El sobre
            // de error de esta casa es {code, message, detail} y los clientes
            // pintan el detail: dejarlo vacio es dejar a la persona con un
            // "no se pudo" y nada mas.
            detail = MotivoSinCentros,
            idCentro,
        });
    }
}
