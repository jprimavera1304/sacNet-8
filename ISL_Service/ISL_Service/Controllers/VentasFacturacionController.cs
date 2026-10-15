using ISL_Service.Application.DTOs.VentasFacturacion;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/*
  FACTURACION ELECTRONICA

  En legacy estos cinco endpoints viven en un controlador con [AllowAnonymous] y
  uno de ellos —sendemail— es el UNICO de todo MacServicios2 que no valida el
  token: su servicio no llama a validateJwtTokenAsync. Lo unico que lo protegia
  era el handler global, que solo comprueba que EXISTA la cabecera Authorization
  sin mirar su contenido. O sea: cualquiera con una cabecera cualquiera podia
  mandar la factura de cualquier cliente al correo que quisiera.

  Aqui los cinco llevan [Authorize] y su permiso.

  LA FAMILIA ES `facturacion.*` Y NO SE REUSA `ventas.facturar`
  ------------------------------------------------------------
  `ventas.facturar` existe (btnFacturar de la forma CONSULTA DE VENTAS) y es el
  permiso de "mandar a facturar desde la pantalla de ventas". No alcanza para esto:
  cancelar un CFDI ante el SAT y mandar una factura por correo son dos cosas
  distintas de timbrarla, y en una familia donde manda Mac31 no se pueden crear
  claves nuevas (se borran en el siguiente recalculo del snapshot). Timbrar SI
  acepta las dos, la nueva y la de Mac31, para que quien hoy factura siga
  facturando.
*/
[ApiController]
[Route("api/ventas/facturas")]
[Authorize]
public class VentasFacturacionController : PermisoControllerBase
{
    // Ver la lista Y bajar el PDF/XML: el documento viaja con el permiso de ver,
    // aqui y en el web, asi que la clave del telefono entra por el mismo sitio.
    private static readonly string[] PermisoVer =
        { "facturacion.ver", "ventas.facturar", "app_movil.facturacion.ver" };
    /*
      TIMBRAR ACEPTA TRES CLAVES, Y LA TERCERA ES LA DEL TELEFONO.

      `facturacion.timbrar` es la clave nueva y `ventas.facturar` la de Mac31
      (btnFacturar de CONSULTA DE VENTAS): las dos juntas para que quien hoy
      factura siga facturando sin que nadie le de de alta nada.

      `app_movil.ventas.facturar` se AÑADE y es la pareja movil de la segunda,
      segun la convencion del repo (`PedidosOperacionesController`: cada endpoint
      que sirve a los dos canales lleva la clave de cada canal). Van separadas a
      proposito y no es burocracia: EL TELEFONO SE PRESTA Y LA COMPUTADORA DE LA
      OFICINA NO, asi que se le puede dar a alguien facturar en el escritorio y
      no en la calle, o al reves. Timbrar es irreversible ante el SAT, y eso es
      exactamente la clase de accion que merece su propia llave por canal.

      NO se inventa `app_movil.facturacion.timbrar`: la familia `facturacion.*`
      viene de Mac31 y una clave inventada ahi se borra en el siguiente recalculo
      del snapshot de permisos. La familia `app_movil.*` SI es nuestra, y
      `app_movil.ventas.facturar` ya es la forma que usan las demas parejas
      moviles de ventas.
    */
    private static readonly string[] PermisoTimbrar =
        { "facturacion.timbrar", "ventas.facturar", "app_movil.ventas.facturar" };

    /*
      PREPARAR LLEVA EL MISMO PERMISO QUE TIMBRAR, Y NO UNO DE LECTURA.

      Parece una lectura —no escribe nada— pero lo que devuelve es un comprobante
      fiscal armado con el RFC, el domicilio y los importes del cliente, y no
      sirve para nada mas que para timbrarlo. Quien no puede facturar no tiene
      nada que hacer con eso. Es el mismo criterio que ya se aplico a
      `catalogo-cascos`.
    */
    private static readonly string[] PermisoPreparar = PermisoTimbrar;
    private static readonly string[] PermisoCancelar = { "facturacion.cancelar" };
    // Mandar la factura por correo es su propia llave en los dos canales:
    // manda un correo a un cliente de verdad.
    private static readonly string[] PermisoEnviar =
        { "facturacion.enviar", "app_movil.facturacion.enviar" };

    /*
      QUIEN PUEDE PREPARAR SOBRE REMISIONES DE OTROS.

      `preparar` lee las remisiones por el MISMO servicio que la consulta de
      ventas, y ese servicio acota la lista al usuario del token cuando no se
      tiene esta clave. Se reusa el array literal de `VentasConsultaController`
      —incluida la del telefono— porque la alternativa es que la misma remision
      se vea en la reja y no se pueda preparar, o al reves: dos opiniones sobre
      el mismo alcance acaban discrepando.

      No es un candado de facturacion: el de facturar ya lo puso
      `PermisoPreparar`. Esto solo decide SI SE VE la remision de otro agente, y
      sin la clave no es un 403 — es la lista acotada a las suyas, igual que en
      la reja.
    */
    private static readonly string[] PermisoVerRemisionesDeOtros =
    {
        "ventas.consultar", "ventas.ver_modulo", "app_movil.ventas.consulta.ver_de_otros"
    };

    private readonly IVentasFacturacionService _service;
    private readonly ICascosAFacturarRepository _cascos;
    private readonly IVentasFacturacionPreparacionService _preparacion;
    private readonly IFacturaDocumentoService _documentos;

    public VentasFacturacionController(
        IVentasFacturacionService service,
        ICascosAFacturarRepository cascos,
        IVentasFacturacionPreparacionService preparacion,
        IFacturaDocumentoService documentos,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
        _cascos = cascos;
        _preparacion = preparacion;
        _documentos = documentos;
    }

    /// <summary>
    /// Arma y valida una FACTURA de una o varias remisiones, SIN timbrarla.
    /// </summary>
    /// <remarks>
    /// POR QUE EXISTE: `POST /api/ventas/facturas` recibe el CFDI ya armado, asi
    /// que hasta ahora el que decidia QUE dice el comprobante era el navegador
    /// (`nucleo/facturarRemision.ts` + `FacturaDeRemisionPage.tsx`). Con un solo
    /// cliente se aguantaba; con el movil son dos copias de una regla fiscal, y
    /// dos copias son dos comprobantes distintos para la misma remision. Esto
    /// mueve la regla al servidor: los clientes ensenan, retocan y confirman.
    ///
    /// NO ESCRIBE NADA Y NO TIMBRA. Se puede llamar las veces que haga falta: es
    /// precisamente lo que permite que la pantalla valide mientras se teclea en
    /// vez de descubrir el rechazo al pulsar el boton irreversible.
    ///
    /// LO QUE DEVUELVE: en `timbrado`, el cuerpo EXACTO para
    /// `POST /api/ventas/facturas` —el cliente lo reenvia, no lo recompone—; en
    /// `bloqueo`, lo que impide timbrar con el campo al que llevar el foco; y en
    /// `avisos`, las divergencias medidas con Mac31 que conviene leer antes de
    /// cambiar una cifra (ver `ReglasPreparacionFactura.DivergenciasConLegacy`).
    /// </remarks>
    /// <response code="200">El comprobante armado. `bloqueo != null` = todavia no se puede timbrar.</response>
    /// <response code="400">No llegaron remisiones ni pedido, o no existe ninguna de esas remisiones.</response>
    /*
      DOS DIRECCIONES PARA LO MISMO, Y SE DICE POR QUE.

      La ruta del controlador es `api/ventas/facturas`, asi que la que sigue la
      convencion de sus hermanas (`/complemento-pago`, `/cancelar`,
      `/consultar`, `/enviar`) es `api/ventas/facturas/preparar`. Pero el
      encargo y los clientes hablan de `api/ventas/facturacion/preparar`, que es
      como se llama el modulo.

      Se publican LAS DOS, apuntando al mismo metodo, en vez de elegir una y
      que la otra conteste 404 sin cuerpo. Un 404 de ruta se diagnostica fatal:
      el sintoma que llega es "la pantalla no carga", que manda a buscar el
      fallo en la sesion o en la red en vez de en un nombre de ruta. Y no es un
      alias gratuito: `/facturas/...` es lo que ya usa este controlador y
      `/facturacion/...` es lo que pide quien lo consume.
    */
    [HttpPost("preparar")]
    [HttpPost("/api/ventas/facturacion/preparar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PrepararAsync(
        [FromBody] FacturaPrepararRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoPreparar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });

        var permisos = await LeerPermisosAsync(ct);
        var deOtros = permisos.Tiene(PermisoVerRemisionesDeOtros);

        try
        {
            var data = await _preparacion.PrepararFacturaAsync(request, IdUsuarioLegacy(), deOtros, ct);
            // no-store: lo que devuelve lleva el RFC, el domicilio y los importes
            // del cliente, y los montos cambian con cada factura que se emite.
            Response.Headers["Cache-Control"] = "no-store";
            return Ok(new { ok = true, message = "Factura preparada.", data });
        }
        catch (InvalidOperationException e)
        {
            // El mensaje se respeta TAL CUAL: trae el folio de la remision que
            // no se pudo, y es lo unico util cuando alguien llama con unos ids
            // que ya no valen.
            return BadRequest(new { ok = false, message = e.Message });
        }
    }

    /// <summary>
    /// Arma y valida el ENCABEZADO FISCAL de una nota de credito ya capturada.
    /// </summary>
    /// <remarks>
    /// LAS PARTIDAS NO SALEN DE AQUI. Mac31 las captura en un PEDIDO de verdad y
    /// manda ese `IDPedido` al timbrado (VentasNotaCredito.cs:1782 y :2864); ese
    /// pedido se IMPRIME en el comprobante ("APLICA A PEDIDOS 116379",
    /// comprobado en un PDF real de MacZ). La captura vive en
    /// `/api/ventas/facturas/nota-credito/*`, que reusa el mismo procedimiento de
    /// la captura de pedidos y por tanto los descuentos, las existencias, los
    /// cascos y el IVA ya calculados por la base.
    ///
    /// Asi que aqui se prepara lo que falta: el receptor, las claves del SAT, la
    /// relacion con la factura acreditada, y las validaciones contra el SALDO —
    /// que lo manda el procedimiento y no se recalcula, porque una resta propia
    /// podria dejar pasar una nota mayor al saldo por un centavo de redondeo.
    /// </remarks>
    [HttpPost("preparar/nota-credito")]
    [HttpPost("/api/ventas/facturacion/preparar/nota-credito")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PrepararNotaCreditoAsync(
        [FromBody] NotaCreditoPrepararRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoPreparar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });

        try
        {
            var data = await _preparacion.PrepararNotaCreditoAsync(request, ct);
            Response.Headers["Cache-Control"] = "no-store";
            return Ok(new { ok = true, message = "Nota de credito preparada.", data });
        }
        catch (InvalidOperationException e)
        {
            return BadRequest(new { ok = false, message = e.Message });
        }
    }

    /// <summary>Arma y valida un complemento de pago sobre una o varias facturas.</summary>
    /// <remarks>
    /// LA PARCIALIDAD, EL SALDO ANTERIOR Y EL SALDO INSOLUTO NO SE CALCULAN AQUI,
    /// y son justo los tres datos que el SAT exige DENTRO del complemento. Los
    /// calcula el procedimiento con lo que se paga y los abonos que la factura ya
    /// tiene: el DTO de Mac31 tiene campos para ellos (`InstallmentsLst`,
    /// `LastBalancesLst`, `UuidsLst`, `NumComplementosPago`) y la pantalla de
    /// alla NO LLENA NINGUNO. Repetirlo seria tener dos reglas sobre un dato
    /// fiscal. Va dicho tambien en los `avisos` de la respuesta, para que quien
    /// los eche en falta no tenga que leer 1,310 lineas de Mac31 para saberlo.
    ///
    /// Lo que SI se hace: el saldo por factura
    /// (`TotalPagarFactura - Abonos - NotasCredito`), el importe propuesto —el
    /// saldo completo, que es el caso normal—, y las validaciones por renglon con
    /// el folio DENTRO del mensaje: con cinco facturas marcadas, un mensaje sin
    /// folio no dice cual hay que corregir.
    /// </remarks>
    [HttpPost("preparar/complemento")]
    [HttpPost("/api/ventas/facturacion/preparar/complemento")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PrepararComplementoAsync(
        [FromBody] ComplementoPrepararRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoPreparar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });

        try
        {
            var data = await _preparacion.PrepararComplementoAsync(request, ct);
            Response.Headers["Cache-Control"] = "no-store";
            return Ok(new { ok = true, message = "Complemento de pago preparado.", data });
        }
        catch (InvalidOperationException e)
        {
            return BadRequest(new { ok = false, message = e.Message });
        }
    }

    /// <summary>
    /// Los tipos de casco que se pueden AGREGAR a una factura, con su precio
    /// con IVA y su clave del SAT.
    /// </summary>
    /// <remarks>
    /// Es el recuadro "CASCOS A FACTURAR" de `VentasFacturacion`, que no sale
    /// de la remision: sale del catalogo, con las cantidades en cero, para que
    /// la persona teclee cuantos quiere facturar. El por que no se reusa el
    /// endpoint de la pantalla de usados esta en ICascosAFacturarRepository.
    ///
    /// Va con el permiso de TIMBRAR y no con uno de solo lectura porque solo
    /// sirve para armar un comprobante: quien no puede facturar no tiene nada
    /// que hacer con esta lista.
    /// </remarks>
    [HttpGet("catalogo-cascos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CatalogoDeCascosAsync(CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoTimbrar, ct);
        if (veto is not null) return veto;

        var data = await _cascos.ConsultarCatalogoAsync(ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Catalogo de cascos.", data });
    }

    /// <summary>
    /// Timbra una factura o una nota de credito ante el SAT.
    ///
    /// ES IRREVERSIBLE: si contesta 200, hay un CFDI emitido. Cuando el timbrado
    /// funciona pero el guardado en Mac31 falla, el mensaje lo dice con el UUID y
    /// el id de Facturapi, porque ese comprobante existe ante el SAT y hay que
    /// registrarlo a mano.
    /// </summary>
    /// <response code="200">El comprobante timbrado y las facturas que se crearon.</response>
    /// <response code="409">El candado del key no paso, o Facturapi rechazo el comprobante.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> TimbrarAsync([FromBody] FacturaTimbrarRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoTimbrar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.TimbrarAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = data.Mensaje ?? "Comprobante timbrado.", data });
    }

    /// <summary>
    /// Timbra un complemento de pago. Crea UN comprobante ante el SAT y un renglon
    /// de factura en Mac31 por cada documento pagado.
    /// </summary>
    [HttpPost("complemento-pago")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> TimbrarComplementoAsync(
        [FromBody] ComplementoPagoTimbrarRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoTimbrar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.TimbrarComplementoAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = data.Mensaje ?? "Complemento de pago timbrado.", data });
    }

    /// <summary>
    /// Cancela un CFDI ante el SAT y lo registra.
    ///
    /// El estatus que se guarda es el que contesto el SAT: si el cliente rechaza la
    /// cancelacion, la factura queda VIGENTE. Legacy forzaba siempre "aceptada", lo
    /// que dejaba facturas validas ante el SAT que el sistema daba por canceladas.
    /// </summary>
    [HttpPost("cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelarAsync([FromBody] FacturaCancelarRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoCancelar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CancelarAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = data.Mensaje ?? "Cancelacion registrada.", data });
    }

    /// <summary>
    /// Consulta las facturas. A diferencia de la de legacy, esta NO escribe: no
    /// cancela nada por su cuenta.
    /// </summary>
    [HttpPost("consultar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarAsync([FromBody] FacturaConsultaRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoVer, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.ConsultarAsync(request, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Facturas.", data });
    }

    /// <summary>
    /// Baja el PDF o el XML de un comprobante ya timbrado.
    ///
    /// Son los dos botones de Mac31 (IMPRIMIR PDF F7 y DESCARGAR XML F8) que no
    /// tenian endpoint: hasta ahora lo UNICO que servia un comprobante timbrado era
    /// el correo, asi que para releer una factura habia que mandarsela a alguien.
    ///
    /// Lleva el permiso de ver y no uno nuevo: quien puede consultar la factura
    /// puede leer su propio comprobante. Mandarlo por correo es otra cosa y por eso
    /// tiene su clave aparte.
    /// </summary>
    /// <response code="200">El archivo.</response>
    /// <response code="404">La factura no existe.</response>
    /// <response code="409">El archivo no esta en disco; el mensaje trae la ruta.</response>
    [HttpGet("{idVentaFactura:int}/documento")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DocumentoAsync(
        int idVentaFactura,
        [FromQuery] string formato,
        CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoVer, ct);
        if (veto is not null) return veto;

        /*
          PASA POR EL 1003 (FacturaDocumentoService) Y NO POR LeerDocumentoAsync.

          Los dos resuelven la misma ruta y bajan de Facturapi cuando el disco no
          la tiene; la diferencia es que el 1003 GUARDA lo que baja, que es el
          paso que le faltaba al backend. Importa para el boton de al lado:
          `enviar` adjunta DESDE DISCO —igual que legacy—, asi que sin esa copia
          una factura se podia bajar una y otra vez y seguir sin poderse mandar.
        */
        var documento = await _documentos.LeerAsync(idVentaFactura, formato, ct);

        /*
          Cache-Control no-store: un CFDI puede cancelarse, y un PDF guardado en
          la cache del navegador seguiria diciendo que la factura esta vigente.
        */
        Response.Headers["Cache-Control"] = "no-store";
        return File(documento.Contenido, documento.TipoContenido, documento.NombreArchivo);
    }

    /// <summary>
    /// GENERA el PDF y el XML del comprobante y los deja en disco. Es el 1003.
    /// </summary>
    /// <remarks>
    /// EL RECUADRO QUE FALTABA. El reporte 1003 de legacy
    /// (`enumReportes.FacturaElectronica`) no dibuja nada: baja de Facturapi el
    /// PDF y el XML del CFDI y los escribe en la carpeta que dicta
    /// `sp_n_rptFacturaElectronica`. En Mac31 lo dispara la propia pantalla al
    /// terminar de timbrar y al pulsar IMPRIMIR; desde el web y el movil NADIE lo
    /// disparaba, asi que un comprobante timbrado desde esos canales se timbraba
    /// bien y no dejaba archivo, y el correo —que adjunta desde disco, igual que
    /// legacy— contestaba 409 para siempre. El porque completo, con las lineas de
    /// legacy y lo medido en las dos bases, esta en
    /// `Application/Interfaces/IFacturaDocumento.cs`.
    ///
    /// ES IDEMPOTENTE: si los dos archivos ya estan, no baja nada y lo dice en
    /// `origen`. `rehacer=true` fuerza la bajada, que es lo que hay que hacer
    /// DESPUES DE CANCELAR, porque al cancelar el procedimiento cambia el nombre
    /// del archivo (le mete "CANCELADA") y el PDF viejo se queda huerfano.
    ///
    /// LLEVA EL PERMISO DE VER, no uno nuevo ni el de timbrar. Lo que entrega es
    /// el mismo documento que ya sirve `GET {id}/documento` con esa misma clave;
    /// escribir un archivo en la carpeta de facturas no es una decision del
    /// usuario, es la cache del documento que acaba de pedir. Timbrar es otra cosa
    /// y tiene su propia llave.
    /// </remarks>
    /// <response code="200">Los archivos, con su ruta, su tamano y de donde salieron.</response>
    /// <response code="404">La factura no existe.</response>
    /// <response code="409">No esta timbrada, o Facturapi no pudo dar el archivo.</response>
    [HttpPost("{idVentaFactura:int}/documento/generar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GenerarDocumentoAsync(
        int idVentaFactura,
        [FromQuery] bool rehacer,
        CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoVer, ct);
        if (veto is not null) return veto;

        var generado = await _documentos.GenerarAsync(idVentaFactura, rehacer, ct);

        return Ok(new
        {
            ok = true,
            message = generado.Cancelada
                ? "Documentos del comprobante CANCELADO listos."
                : "Documentos del comprobante listos.",
            data = generado
        });
    }

    /// <summary>
    /// Manda la factura por correo con su PDF y su XML.
    ///
    /// Lleva permiso propio: en legacy este era el unico endpoint del sistema que
    /// no validaba el token, asi que cualquiera podia mandar la factura de
    /// cualquier cliente al correo que quisiera.
    /// </summary>
    [HttpPost("enviar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EnviarAsync([FromBody] FacturaEnviarCorreoRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoEnviar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        /*
          PRIMERO SE GENERA EL DOCUMENTO Y DESPUES SE MANDA. ES EL ORDEN DE MAC31.

          `EnviarCorreoAsync` adjunta el PDF y el XML leyendolos del disco, que es
          lo que hace legacy (VentasFacturaService.cs:956-980: si no estan,
          contesta "NO SE ENCONTRO EL ARCHIVO PDF" y la descarga esta comentada).
          En Mac31 los archivos estan porque la pantalla dispara el reporte 1003
          ANTES de mandar el correo; desde el web y el movil nadie disparaba el
          1003, asi que el archivo no existia nunca y este endpoint contestaba 409
          para un comprobante perfectamente timbrado.

          Aqui se dispara ese mismo paso. No es trabajo de mas: si los archivos ya
          estan en disco no baja nada —solo los cuenta—, y si no estan, los pone,
          que es justo lo que el correo necesita.

          `rehacer: false` a proposito: un comprobante no cambia, y volver a bajar
          dos archivos en cada correo seria pagar dos llamadas a Facturapi por
          cada cliente al que se le reenvia su factura.
        */
        /* Solo si la peticion tiene sentido: las quejas por falta de factura o de
           correos las da `EnviarCorreoAsync`, y no se baja nada para una peticion
           que va a ser rechazada de todas formas. */
        if (request.IdVentaFactura > 0 && !string.IsNullOrWhiteSpace(request.Correos))
            await _documentos.GenerarAsync(request.IdVentaFactura, rehacer: false, ct);

        var destinatarios = await _service.EnviarCorreoAsync(request, ct);
        return Ok(new { ok = true, message = $"Factura enviada a {destinatarios}.", data = new { destinatarios } });
    }
}
