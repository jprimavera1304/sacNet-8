using ISL_Service.Application.DTOs.Reportes;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Reportes;
using ISL_Service.Infrastructure.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISL_Service.Application.Security;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/reportes/ventas")]
[Authorize]
public class ReportesVentasController : PermisoControllerBase
{
    private readonly IReportesVentasService _service;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly ICentrosServicioRepository _centros;
    private readonly IConfiguration _configuration;

    public ReportesVentasController(
        IReportesVentasService service,
        ICurrentUserAccessor currentUserAccessor,
        ICentrosServicioRepository centros,
        IPermissionService permissionService,
        IConfiguration configuration)
        : base(currentUserAccessor, permissionService)
    {
        _service = service;
        _currentUserAccessor = currentUserAccessor;
        _centros = centros;
        _configuration = configuration;
    }

    [HttpGet("acumuladores-productos/catalogos")]
    [Authorize(Policy = "perm:reportes.ver_modulo|app_movil.reportes")]
    [ProducesResponseType(typeof(ReportesVentasCatalogosResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarCatalogosAcumuladoresProductos(
        [FromQuery] int? idGrupoCategoria,
        [FromQuery] int[]? idCategorias,
        CancellationToken ct)
    {
        var result = await _service.ConsultarCatalogosAcumuladoresProductosAsync(
            idGrupoCategoria,
            idCategorias ?? Array.Empty<int>(),
            ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(result);
    }

    [HttpGet("acumuladores-productos/clientes")]
    [Authorize(Policy = "perm:reportes.ver_modulo|app_movil.reportes")]
    [ProducesResponseType(typeof(List<ReportesVentasClienteItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConsultarClientes(
        [FromQuery] int? numero,
        [FromQuery] int? idCliente,
        CancellationToken ct)
    {
        var result = await _service.ConsultarClientesAsync(numero, idCliente, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(result);
    }

    [HttpGet("remisiones/catalogos")]
    [Authorize(Policy = "perm:reportes.ver_modulo|app_movil.reportes")]
    [ProducesResponseType(typeof(ReportesVentasCatalogosResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarCatalogosRemisiones(CancellationToken ct)
    {
        var result = await _service.ConsultarCatalogosRemisionesAsync(ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(result);
    }

    [HttpPost("acumuladores-productos/generar")]
    [Authorize(Policy = "perm:reportes.ver_modulo|app_movil.reportes")]
    [ProducesResponseType(typeof(ReportesVentasGenerateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerarAcumuladoresProductos(
        [FromBody] ReportesVentasAcumuladoresProductosRequest request,
        CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        if (await ExigirPermisoReporteAsync("acumuladores_y_productos", ct) is { } negado)
            return negado;

        if (await ExigirPermisoCentroAsync(request.IdCentro, ct) is { } centroNegado)
            return centroNegado;

        HydrateLegacyContext(request);
        var result = await _service.GenerarAcumuladoresProductosAsync(request, ct);
        result.Url = BuildReportesV3WUrl(result.ParametrosLegacy, request.IdCentro);

        Response.Headers["Cache-Control"] = "no-store";
        return Ok(result);
    }

    [HttpPost("remisiones/generar")]
    [Authorize(Policy = "perm:reportes.ver_modulo|app_movil.reportes")]
    [ProducesResponseType(typeof(ReportesVentasGenerateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerarRemisiones(
        [FromBody] ReportesVentasRemisionesRequest request,
        CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        if (await ExigirPermisoReporteAsync("remisiones", ct) is { } negado)
            return negado;

        if (await ExigirPermisoCentroAsync(request.IdCentro, ct) is { } centroNegado)
            return centroNegado;

        HydrateLegacyContext(request);
        var result = await _service.GenerarRemisionesAsync(request, ct);
        result.Url = BuildReportesV3WUrl(result.ParametrosLegacy, request.IdCentro);

        Response.Headers["Cache-Control"] = "no-store";
        return Ok(result);
    }

    [HttpPost("folios/generar")]
    [Authorize(Policy = "perm:reportes.ver_modulo|app_movil.reportes")]
    [ProducesResponseType(typeof(ReportesVentasGenerateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerarFolios(
        [FromBody] ReportesVentasFoliosRequest request,
        CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        if (await ExigirPermisoReporteAsync("folios", ct) is { } negado)
            return negado;

        if (await ExigirPermisoCentroAsync(request.IdCentro, ct) is { } centroNegado)
            return centroNegado;

        HydrateLegacyContext(request);
        var result = await _service.GenerarFoliosAsync(request, ct);
        result.Url = BuildReportesV3WUrl(result.ParametrosLegacy, request.IdCentro);

        Response.Headers["Cache-Control"] = "no-store";
        return Ok(result);
    }

    [HttpPost("facturas/generar")]
    [Authorize(Policy = "perm:reportes.ver_modulo|app_movil.reportes")]
    [ProducesResponseType(typeof(ReportesVentasGenerateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerarFacturas(
        [FromBody] ReportesVentasFacturasRequest request,
        CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        if (await ExigirPermisoReporteAsync("facturas", ct) is { } negado)
            return negado;

        if (await ExigirPermisoCentroAsync(request.IdCentro, ct) is { } centroNegado)
            return centroNegado;

        HydrateLegacyContext(request);
        var result = await _service.GenerarFacturasAsync(request, ct);
        result.Url = BuildReportesV3WUrl(result.ParametrosLegacy, request.IdCentro);

        Response.Headers["Cache-Control"] = "no-store";
        return Ok(result);
    }

    [HttpPost("concentrados/generar")]
    [Authorize(Policy = "perm:reportes.ver_modulo|app_movil.reportes")]
    [ProducesResponseType(typeof(ReportesVentasGenerateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerarConcentrados(
        [FromBody] ReportesVentasConcentradosRequest request,
        CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        if (await ExigirPermisoReporteAsync("concentrados", ct) is { } negado)
            return negado;

        if (await ExigirPermisoCentroAsync(request.IdCentro, ct) is { } centroNegado)
            return centroNegado;

        HydrateLegacyContext(request);
        var result = await _service.GenerarConcentradosAsync(request, ct);
        result.Url = BuildReportesV3WUrl(result.ParametrosLegacy, request.IdCentro);

        Response.Headers["Cache-Control"] = "no-store";
        return Ok(result);
    }

    [HttpPost("cobranza/generar")]
    [Authorize(Policy = "perm:reportes.ver_modulo|app_movil.reportes")]
    [ProducesResponseType(typeof(ReportesVentasGenerateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerarCobranza(
        [FromBody] ReportesVentasCobranzaRequest request,
        CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        if (await ExigirPermisoReporteAsync("cobranza", ct) is { } negado)
            return negado;

        if (await ExigirPermisoCentroAsync(request.IdCentro, ct) is { } centroNegado)
            return centroNegado;

        HydrateLegacyContext(request);
        var result = await _service.GenerarCobranzaAsync(request, ct);
        result.Url = BuildReportesV3WUrl(result.ParametrosLegacy, request.IdCentro);

        Response.Headers["Cache-Control"] = "no-store";
        return Ok(result);
    }

    /*
      LA PUERTA DE ESTE ENDPOINT NO ES UNA SOLA, Y POR ESO NO ES UN ATRIBUTO.

      Para un NODO del arbol la puerta es `reportes.ver_modulo`, el menu de
      reportes, y eso es lo que decia el atributo que estaba aqui. Para el
      DOCUMENTO DE UN BOTON no: su puerta es la pantalla de la que sale. El
      atributo no sabe mirar el `reporteKey`, asi que decidia igual para los
      dos y dejaba fuera a quien en Mac31 si puede imprimir.

      MEDIDO el 2026-10-13 con una cuenta aislada que tenia los SEIS botones de
      `ConsultarCompras` y ni un proceso de `ReportesMenu` -o sea, exactamente
      lo que tiene quien trabaja en compras y no entra a reportes-: el doble
      clic sobre una compra contestaba 403 antes de llegar a ninguna
      comprobacion. En Mac31 ese mismo usuario imprime.

      La decision se mueve a ExigirEntradaDeReporteAsync, que lee el modulo
      dueno del catalogo. No se concede nada nuevo: quien no puede abrir la
      pantalla sigue sin poder sacar su papel.
    */
    [HttpPost("legacy/generar")]
    [ProducesResponseType(typeof(ReportesVentasGenerateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerarLegacyVentas(
        [FromBody] ReportesVentasLegacyRequest request,
        CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        // EL REPORTE SE PIDE POR reporteKey Y NADA MAS.
        //
        // El repositorio tambien aceptaba un IDReporte crudo del cliente, y con
        // permisos por reporte eso es una puerta falsa: mandas la clave de un
        // reporte que si puedes y el id del que no, y el permiso que se revisa
        // no es el del reporte que sale. Ni el web ni la app mandan idReporte.
        if (request.IDReporte != 0)
            return BadRequest(new { message = "No mandes idReporte: el reporte se pide por reporteKey." });

        if (ReportesCatalogo.Buscar(request.ReporteKey) is null)
            return BadRequest(new { message = $"reporteKey desconocido: \"{request.ReporteKey}\"." });

        if (await ExigirEntradaDeReporteAsync(request.ReporteKey, ct) is { } sinEntrada)
            return sinEntrada;

        if (await ExigirPermisoReporteAsync(request.ReporteKey, ct) is { } negado)
            return negado;

        if (await ExigirPermisoCentroAsync(request.IdCentro, ct) is { } centroNegado)
            return centroNegado;

        /*
          EL ESTADO DE CUENTA SIN CLIENTE SE RECHAZA AQUI, NO ABAJO.

          sp_n_rptVentasEstadoCuenta no respeta las fechas (trae todo desde 2024)
          y recorre el resultado con cursores anidados: con un cliente tarda 3.5 s
          y sin cliente pasa de 400 s, que en Azure es un 502 a los 230. Mac31
          nunca llega a ese caso porque su forma fuerza "por cliente"
          (ReportesMenu.cs:1378-1381).

          Es un error del cliente, asi que contesta 400 y lo dice; el repositorio
          mantiene la misma guarda por si alguien entra por otro camino.
        */
        if (string.Equals(request.ReporteKey?.Trim(), "estado_de_cuenta", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(request.TipoReporte?.Trim(), "cliente", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "El estado de cuenta es de un cliente: manda tipoReporte \"cliente\" y el cliente en idClientes." });
        }

        /*
          LOS FILTROS QUE EL SP NO SABE DAR VACIOS (106 y 1245).

          El porque, con la linea de cada SP y el mensaje de Mac31, esta en
          FiltrosObligatoriosDeReporte. Resumen: sin ellos el backend contestaba
          500 "Error de base de datos" y le echaba la culpa a la base de un dato
          que faltaba en la peticion.
        */
        if (FiltrosObligatoriosDeReporte.QueFalta(
                request.ReporteKey,
                request.IDGrupoCategoria,
                request.IDDiasSemana.Count,
                request.IDSubcategorias.Count) is { } filtroQueFalta)
        {
            return BadRequest(new { message = filtroQueFalta });
        }

        HydrateLegacyContext(request);
        var result = await _service.GenerarLegacyVentasAsync(request, ct);
        result.Url = BuildReportesV3WUrl(result.ParametrosLegacy, request.IdCentro);

        Response.Headers["Cache-Control"] = "no-store";
        return Ok(result);
    }

    [HttpPost("acumuladores-productos/vista-previa")]
    [Authorize(Policy = "perm:reportes.ver_modulo|app_movil.reportes")]
    [ProducesResponseType(typeof(ReportesVentasPreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConsultarAcumuladoresProductos(
        [FromBody] ReportesVentasAcumuladoresProductosRequest request,
        CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        if (await ExigirPermisoReporteAsync("acumuladores_y_productos", ct) is { } negado)
            return negado;

        if (await ExigirPermisoCentroAsync(request.IdCentro, ct) is { } centroNegado)
            return centroNegado;

        HydrateLegacyContext(request);
        var result = await _service.ConsultarAcumuladoresProductosAsync(request, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(result);
    }

    [HttpGet("ReportesV3W")]
    [HttpGet("ReportesV2")]
    [HttpGet("acumuladores-productos/pantalla")]
    [AllowAnonymous]
    /// <param name="psp">El renglon de parametros que dejo "generar".</param>
    /// <param name="c">
    /// Centro de servicio; 0 o ausente = la base principal.
    ///
    /// Viaja en la direccion porque el reporte entero vive en UNA base: los
    /// parametros se guardaron en la del centro, asi que hay que leerlos de
    /// ahi. Sin este dato se buscarian en la matriz y saldria "no encontrado",
    /// o peor, el reporte de otro.
    /// </param>
    public async Task<IActionResult> VerAcumuladoresProductosPantalla(
        [FromQuery] int psp,
        [FromQuery] int c,
        [FromQuery] string? t,
        CancellationToken ct)
    {
        if (psp <= 0)
            return BadRequest("psp requerido.");

        /*
          SIN PASE NO HAY REPORTE.

          Esta direccion es anonima porque el reporte se abre en una pestaña
          nueva y una pestaña nueva no manda Authorization. Lo que autoriza es
          el pase firmado que puso "generar".

          Antes bastaba con `psp`, que es un entero CONSECUTIVO de una tabla
          compartida: quien probara numeros veia los reportes de los demas.

          Se comprueba ademas que el pase sea PARA ESTE psp y ESTE centro, no
          solo que sea un pase valido: si no, cualquiera con un pase propio
          podria cambiarle el numero en la barra del navegador y leer el de
          otro, que es exactamente el agujero que se esta cerrando.
        */
        var llaveDelPase = _configuration["Jwt:Key"] ?? "";
        var pase = ReporteTicket.Validar(llaveDelPase, t);
        if (pase is null || pase.Psp != psp || pase.IdCentro != c)
            return BadRequest("El enlace del reporte no es valido o ya vencio. Vuelve a generarlo desde el modulo de reportes.");

        try
        {
            var excel = await _service.GenerarReporteVentasExcelPorParametrosAsync(psp, c, ct);
            Response.Headers["Cache-Control"] = "no-store";
            return File(excel.Content, excel.ContentType, excel.FileName);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("salida Excel", StringComparison.OrdinalIgnoreCase))
        {
        }

        var result = await _service.ConsultarReporteVentasPorParametrosAsync(psp, c, ct);
        /* El nombre del reporte es lo que se lee en la pestaña; sin el decia "pdf",
           que es el ultimo pedazo de la direccion. Ver RenderAsync. */
        var pdf = await WkhtmltopdfHtmlPdfRenderer.RenderAsync(
            result.Html,
            result.Orientacion,
            result.NombreReporte,
            ct);
        Response.Headers["Cache-Control"] = "no-store";
        return File(pdf, "application/pdf");
    }

    /// <summary>
    /// Que reportes puede ver quien pregunta. El menu del web y el de la app se
    /// pintan con esto, asi que ofrecen exactamente lo que la API va a aceptar.
    /// </summary>
    /// <remarks>
    /// Antes cada front traia su propio catalogo y "reportes.ver_modulo" era el
    /// unico candado: quien entraba al modulo podia generar los 53 reportes por
    /// mas que le hubieran quitado permisos. El mapeo reporte->permiso vive
    /// ahora en <see cref="ReportesCatalogo"/> y se publica aqui.
    /// </remarks>
    /// <summary>
    /// Los centros de servicio que ESTE usuario puede consultar.
    /// </summary>
    /// <remarks>
    /// EL SISTEMA ES EL MISMO EN LA MATRIZ Y EN CADA CENTRO; lo unico que
    /// cambia es la base de la que salen los datos. Este endpoint llena el
    /// selector con el que se elige cual.
    ///
    /// Son DOS permisos y hacen cosas distintas:
    ///
    ///   reportes.centros.cambiar      -> puede usar el selector. Sin esto se
    ///                                    contesta PuedeCambiar=false y una
    ///                                    lista vacia: el front no lo pinta y
    ///                                    todo sale de la matriz, como hasta
    ///                                    hoy. Es el interruptor de arriba.
    ///   reportes.centros.&lt;numero&gt;.ver -> a que centros puede entrar.
    ///
    /// Asi se puede apagar el selector a alguien SIN tocarle los doce permisos
    /// de abajo, que es justo lo que se pidio.
    ///
    /// El nombre de la base NO viaja: el front recibe Ids y nombres para leer.
    /// Quien traduce Id -> base es el servidor.
    /// </remarks>
    [HttpGet("centros")]
    [Authorize(Policy = "perm:reportes.ver_modulo|app_movil.reportes")]
    [ProducesResponseType(typeof(CentrosServicioResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarCentros(CancellationToken ct)
    {
        var puede = await LeerPermisosAsync(ct);
        if (!puede.TokenValido)
            return Unauthorized(new { ok = false, message = "Token invalido." });

        var respuesta = new CentrosServicioResponse
        {
            PuedeCambiar = GuardiaDeCentros.PuedeCambiar(c => puede.Tiene(c)),
            NombrePrincipal = "Principal"
        };

        if (respuesta.PuedeCambiar)
        {
            var centros = await _centros.ListarAsync(ct);
            respuesta.Centros = centros
                .Where(c => GuardiaDeCentros.PuedeVer(c.Numero, k => puede.Tiene(k)))
                .Select(c => new CentroServicioOpcion
                {
                    IdCentro = c.IdCentro,
                    Numero = c.Numero,
                    Nombre = c.Nombre
                })
                .ToList();
        }

        Response.Headers["Cache-Control"] = "no-store";
        return Ok(respuesta);
    }

    [HttpGet("permisos")]
    [Authorize(Policy = "perm:reportes.ver_modulo|app_movil.reportes")]
    [ProducesResponseType(typeof(ReportesPermisosResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarPermisosReportes(CancellationToken ct)
    {
        // Un solo snapshot para los 53. Ver PermisosDelUsuario.
        var puede = await LeerPermisosAsync(ct);
        if (!puede.TokenValido)
            return Unauthorized(new { ok = false, message = "Token invalido." });

        var reportes = ReportesCatalogo.Reportes
            .Select(x => new ReportesPermisoItem
            {
                Clave = x.Clave,
                Grupo = x.Grupo,
                Etiqueta = x.Etiqueta,
                Permiso = x.Permiso,
                // Sin permiso conocido el reporte sigue abierto: bloquear por una
                // duda deja a alguien sin su reporte sin saber por que.
                Permitido = x.Permiso is null || puede.Tiene(x.Permiso),
                // El menu solo dibuja los nodos del arbol; los documentos de
                // boton siguen aqui para que generar los acepte.
                EnArbol = x.EnArbol
            })
            .ToList();

        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new ReportesPermisosResponse
        {
            PermisoModulo = ReportesCatalogo.PermisoModulo,
            Reportes = reportes,
            ClavesPermitidas = reportes.Where(x => x.Permitido).Select(x => x.Clave).ToList()
        });
    }

    /// <summary>
    /// El 403 a regresar si el usuario no puede ese reporte, o null si puede.
    /// Un reporte sin permiso en el catalogo NO se bloquea.
    /// </summary>
    /*
      LA PUERTA DE ENTRADA DEL PAPEL: EL MENU DE REPORTES, O SU PANTALLA.

      Es lo que antes decidia el `[Authorize(Policy = "perm:reportes.ver_modulo|app_movil.reportes")]`
      del endpoint, ahora mirando QUE papel se pide. Ver la nota larga en
      GenerarLegacyVentas.

      Un papel con modulo dueno se autoriza con la MISMA lectura que exige su
      pantalla para abrirse (`ExigirLecturaDeModuloAsync`), ni mas ni menos:
      si la pantalla te deja entrar, su boton de imprimir te deja imprimir,
      que es como se comporta Mac31. Cualquier otro papel sigue pidiendo el
      menu de reportes.
    */
    private async Task<IActionResult?> ExigirEntradaDeReporteAsync(string? clave, CancellationToken ct)
    {
        var modulo = ReportesCatalogo.Buscar(clave)?.ModuloDueno;

        if (string.IsNullOrWhiteSpace(modulo))
            return await ExigirPermisoAsync(new[] { "reportes.ver_modulo" }, ct);

        return await ExigirLecturaDeModuloAsync(modulo, new[] { $"{modulo}.ver" }, ct);
    }

    private async Task<IActionResult?> ExigirPermisoReporteAsync(string? clave, CancellationToken ct)
    {
        var reporte = ReportesCatalogo.Buscar(clave);

        // Clave desconocida: no se inventa un candado. El repositorio ya rebota
        // las claves que no sabe resolver.
        if (reporte?.Permiso is null)
            return null;

        if (await ExigirPermisoAsync(new[] { reporte.Permiso }, ct) is null)
            return null;

        // Se dice CUAL reporte y CUAL permiso falta: un "no tienes permiso" a
        // secas, en una pantalla con 53 reportes, no le sirve a nadie para
        // pedirlo.
        return StatusCode(StatusCodes.Status403Forbidden, new
        {
            ok = false,
            message = $"No tienes permiso para el reporte \"{reporte.Etiqueta}\".",
            reporte = reporte.Clave,
            permiso = reporte.Permiso
        });
    }

    /*
      La direccion con la que se abre el reporte. Si salio de un centro, el
      centro VIAJA EN ELLA: los parametros se guardaron en la base de ese
      centro, asi que quien luego abra la direccion tiene que ir a buscarlos
      alla. Sin ese dato se buscarian en la matriz y saldria vacio.
    */
    /*
      LA DIRECCION DEL REPORTE VA FIRMADA.

      Antes llevaba solo `psp`, que es un entero CONSECUTIVO de una tabla
      compartida, y el GET que entrega el reporte era anonimo: quien probara
      numeros veia los reportes de los demas. Ahora ademas viaja un pase que
      dice que psp, de que centro, para que usuario y hasta cuando.

      Los fronts no cambian: el web y la app abren la direccion que les devuelve
      "generar", sin mirar lo que trae dentro.
    */
    private string BuildReportesV3WUrl(string parametrosLegacy, int idCentro = 0)
    {
        var psp = Uri.EscapeDataString(parametrosLegacy);
        var pathBase = Request.PathBase.HasValue ? Request.PathBase.Value : "";
        var centro = idCentro > 0 ? $"&c={idCentro}" : "";

        var pase = "";
        if (int.TryParse(parametrosLegacy, out var pspNumero) && pspNumero > 0)
        {
            var llave = _configuration["Jwt:Key"] ?? "";
            pase = "&t=" + Uri.EscapeDataString(
                ReporteTicket.Firmar(llave, pspNumero, idCentro, IdUsuarioLegacy()));
        }

        return $"{Request.Scheme}://{Request.Host}{pathBase}/api/reportes/ventas/ReportesV3W?psp={psp}{centro}{pase}";
    }

    /*
      PUEDE ESTE USUARIO SACAR REPORTES DE ESTE CENTRO.

      Son dos candados y los dos tienen que abrir: el de usar el selector y el
      del centro concreto. Se revisa AQUI, al generar, y no solo al pintar la
      lista: la lista es una cortesia para el front, esto es lo que de verdad
      decide de que base salen los datos.
    */
    private async Task<IActionResult?> ExigirPermisoCentroAsync(int idCentro, CancellationToken ct)
    {
        if (idCentro <= 0) return null;   // la matriz, como siempre

        var puede = await LeerPermisosAsync(ct);
        if (!puede.TokenValido)
            return Unauthorized(new { ok = false, message = "Token invalido." });

        if (!GuardiaDeCentros.PuedeCambiar(c => puede.Tiene(c)))
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                ok = false,
                message = $"Tu usuario no tiene el permiso {PermisosDeCentros.Cambiar}."
            });

        var centro = await _centros.BuscarAsync(idCentro, ct);
        if (centro is null)
            return BadRequest(new { ok = false, message = "Centro de servicio desconocido." });

        if (!GuardiaDeCentros.PuedeVer(centro.Numero, k => puede.Tiene(k)))
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                ok = false,
                message = $"Tu usuario no tiene permiso para el centro {centro.Nombre}."
            });

        return null;
    }

    private void HydrateLegacyContext(ReportesVentasAcumuladoresProductosRequest request)
    {
        request.LegacyIDUsuario = _currentUserAccessor.GetLegacyUserId(User);
        request.LegacyNombreEquipo = "WEB";
    }
}
