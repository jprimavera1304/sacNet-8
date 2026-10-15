using ISL_Service.Application.DTOs.CatalogosGenerales;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/*
  LOS 28 CATALOGOS DE SOLO LECTURA QUE VENIAN DE MacServicios2.

  Comparten prefijo con CatalogosController (api/catalogos) a proposito: para quien consume la
  API es UN catalogo con muchas listas, y no tiene por que saber que unas salen de una tabla
  nueva (WTarima) y otras de un sp_n_ de Mac31. Van en una clase aparte porque el candado es
  distinto: estos piden permiso `catalogos.*` y contestan con el sobre { ok, message, data },
  mientras los de CatalogosController son anteriores y devuelven la lista desnuda. No se
  tocaron esos cinco para no romper al front que ya los consume.

  TODOS SON GET. En Legacy eran POST con el filtro en el cuerpo, porque su handler de token
  exigia el token adentro del JSON y un GET no tiene cuerpo. Aqui el token va en la cabecera
  Authorization, asi que una consulta vuelve a ser un GET y se puede cachear y compartir por
  URL.

  NINGUNO ESCRIBE. Si algun dia hace falta dar de alta en uno de estos catalogos, no se agrega
  aqui: se crea su modulo con su sp_w_ y su permiso de .crear, que es otra conversacion.
*/

/// <summary>
/// Catalogos generales de Mac31 (territorial, facturacion electronica, nomina, operacion).
/// Solo lectura, para desplegables y pantallas de consulta.
/// </summary>
[ApiController]
[Route("api/catalogos")]
[Authorize]
public class CatalogosGeneralesController : ControllerBase
{
    private readonly ICatalogosGeneralesService _service;

    public CatalogosGeneralesController(ICatalogosGeneralesService service)
    {
        _service = service;
    }

    private IActionResult Lista<T>(List<T> datos, string que)
        => Ok(new { ok = true, message = $"{que}: {datos.Count}.", data = datos });

    // ------------------------------------------------------------------ territorial

    /// <summary>Estados de la republica.</summary>
    /// <param name="idEstado">Clave del estado. NULL o 0 = todos.</param>
    /// <param name="estado">Texto del nombre. Vacio = todos.</param>
    /// <param name="identico">true = nombre exacto, false = empieza con.</param>
    /// <response code="200">Lista obtenida</response>
    [HttpGet("estados")]
    [Authorize(Policy = "perm:catalogos.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarEstadosAsync(
        [FromQuery] int? idEstado, [FromQuery] string? estado, [FromQuery] bool identico, CancellationToken ct)
        => Lista(await _service.ConsultarEstadosAsync(idEstado, estado, identico, ct), "Estados");

    /// <summary>Ciudades, opcionalmente de un estado.</summary>
    [HttpGet("ciudades")]
    [Authorize(Policy = "perm:catalogos.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarCiudadesAsync(
        [FromQuery] string? idEstado, [FromQuery] string? idCiudad, [FromQuery] string? ciudad,
        [FromQuery] bool identico, CancellationToken ct)
        => Lista(await _service.ConsultarCiudadesAsync(idEstado, idCiudad, ciudad, identico, ct), "Ciudades");

    /// <summary>Municipios (delegaciones), opcionalmente de un estado.</summary>
    [HttpGet("municipios")]
    [Authorize(Policy = "perm:catalogos.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarMunicipiosAsync(
        [FromQuery] string? idEstado, [FromQuery] string? idMunicipio, [FromQuery] string? municipio,
        [FromQuery] bool identico, CancellationToken ct)
        => Lista(await _service.ConsultarMunicipiosAsync(idEstado, idMunicipio, municipio, identico, ct), "Municipios");

    /// <summary>
    /// Colonias con su jerarquia territorial resuelta. Lo normal es buscar por codigo postal.
    /// </summary>
    /// <response code="400">Se pidieron las colonias sin ningun filtro</response>
    [HttpGet("colonias")]
    // El formulario de clientes del TELEFONO busca la colonia por codigo
    // postal. Sin su clave aqui, el alta de solo-movil se queda sin colonia,
    // que es un campo obligatorio: la pantalla entera deja de servir.
    [Authorize(Policy = "perm:catalogos.ver|app_movil.clientes.crear|app_movil.clientes.editar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConsultarColoniasAsync([FromQuery] ConsultaColoniasFiltro filtro, CancellationToken ct)
    {
        filtro ??= new ConsultaColoniasFiltro();
        var error = ValidarColonias(filtro);
        if (error is not null)
            return BadRequest(error);

        return Lista(await _service.ConsultarColoniasAsync(filtro, ct), "Colonias");
    }

    /*
      POR QUE COLONIAS EXIGE AL MENOS UN FILTRO.

      El catalogo de colonias de Produccion_svr tiene 68,101 renglones y el SP hace cuatro
      INNER JOIN para resolver estado, ciudad, municipio y asentamiento de cada uno. Legacy
      permitia pedirlo entero y la respuesta son ~14 MB: un desplegable que nadie puede usar y
      un front colgado. Quien de verdad quiera el catalogo completo puede paginarlo por estado.
    */
    private static object? ValidarColonias(ConsultaColoniasFiltro filtro)
    {
        var hayFiltro = filtro.IdColonia != 0
            || !string.IsNullOrWhiteSpace(filtro.IdEstado)
            || !string.IsNullOrWhiteSpace(filtro.IdCiudad)
            || !string.IsNullOrWhiteSpace(filtro.IdMunicipio)
            || !string.IsNullOrWhiteSpace(filtro.IdAsentamiento)
            || !string.IsNullOrWhiteSpace(filtro.Colonia)
            || !string.IsNullOrWhiteSpace(filtro.CodigoPostal)
            || !string.IsNullOrWhiteSpace(filtro.Estado)
            || !string.IsNullOrWhiteSpace(filtro.Ciudad)
            || !string.IsNullOrWhiteSpace(filtro.Municipio);

        return hayFiltro
            ? null
            : new { ok = false, message = "Indica al menos un filtro: codigo postal, estado, municipio o texto de la colonia." };
    }

    /// <summary>Unidades territoriales del SAT.</summary>
    [HttpGet("unidades-territoriales")]
    [Authorize(Policy = "perm:catalogos.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarUnidadesTerritorialesAsync(
        [FromQuery] int? idUnidadTerritorial, [FromQuery] int? idStatus, [FromQuery] string? clave, CancellationToken ct)
        => Lista(await _service.ConsultarUnidadesTerritorialesAsync(idUnidadTerritorial, idStatus, clave, ct), "Unidades territoriales");

    /*
      ------------------------------------------------------------ facturacion electronica

      LOS CATALOGOS DEL SAT TAMBIEN LOS PUEDE PEDIR QUIEN FACTURA.

      `catalogos.fe.ver` se queda, y se le suman las dos claves de facturar.
      Medido: en Mac31 estas listas se cargan en el `_Load` de las pantallas de
      facturacion (`VentasFacturacion.cs:100` y `:107`, `ConsultarFEFormasPago`
      / `ConsultarFEMetodosPago`, y `:600` los usos de CFDI) SIN comprobar
      ningun permiso, y si fallan la forma se cierra. Con solo
      `catalogos.fe.ver`, un usuario que en Mac31 factura sin problema abria la
      pantalla del web con los tres desplegables vacios y un 403 en la consola.

      No se regala nada: son los catalogos PUBLICOS del SAT —formas de pago,
      metodos de pago, usos de CFDI, regimenes— los mismos que estan en el
      portal del SAT. No hay un dato de la empresa ni de un cliente en ellos.
      Lo que si protegen las claves de facturacion es TIMBRAR, que es otra cosa
      y sigue donde estaba.
    */

    /// <summary>Regimenes fiscales del SAT.</summary>
    [HttpGet("fe/regimenes-fiscales")]
    [Authorize(Policy = "perm:catalogos.fe.ver|facturacion.timbrar|ventas.facturar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarFeRegimenesFiscalesAsync(
        [FromQuery] int? idRegimen, [FromQuery] string? feClave, [FromQuery] string? feRegimenFiscal,
        [FromQuery] string? rfc, [FromQuery] bool? feFisica, [FromQuery] bool? feMoral,
        [FromQuery] bool identico, CancellationToken ct)
        => Lista(await _service.ConsultarFeRegimenesFiscalesAsync(idRegimen, feClave, feRegimenFiscal, rfc, feFisica, feMoral, identico, ct),
            "Regimenes fiscales");

    /// <summary>Tipos de relacion entre CFDI.</summary>
    [HttpGet("fe/relaciones-factura")]
    [Authorize(Policy = "perm:catalogos.fe.ver|facturacion.timbrar|ventas.facturar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarFeRelacionesFacturaAsync(
        [FromQuery] int? idRelacion, [FromQuery] string? feClave, [FromQuery] bool identico, CancellationToken ct)
        => Lista(await _service.ConsultarFeRelacionesFacturaAsync(idRelacion, feClave, identico, ct), "Relaciones de factura");

    /// <summary>Usos de CFDI del SAT.</summary>
    [HttpGet("fe/usos-cfdi")]
    [Authorize(Policy = "perm:catalogos.fe.ver|facturacion.timbrar|ventas.facturar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarFeUsosCfdiAsync(
        [FromQuery] int? idUsoCfdi, [FromQuery] string? feClave, [FromQuery] string? feUsoCfdi,
        [FromQuery] string? rfc, [FromQuery] bool? feFisica, [FromQuery] bool? feMoral,
        [FromQuery] bool identico, CancellationToken ct)
        => Lista(await _service.ConsultarFeUsosCfdiAsync(idUsoCfdi, feClave, feUsoCfdi, rfc, feFisica, feMoral, identico, ct), "Usos de CFDI");

    // --------------------------------------------------------------------- operacion

    /// <summary>Numeros de agente libres para dar de alta uno nuevo.</summary>
    /// <param name="numeroAgente">Numero actual, para que siga saliendo en la lista al editar.</param>
    [HttpGet("agentes-disponibles")]
    [Authorize(Policy = "perm:catalogos.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarAgentesDisponiblesAsync([FromQuery] int? numeroAgente, CancellationToken ct)
        => Lista(await _service.ConsultarAgentesDisponiblesAsync(numeroAgente, ct), "Agentes disponibles");

    /// <summary>Centros de servicio.</summary>
    [HttpGet("centros-servicio")]
    [Authorize(Policy = "perm:catalogos.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarCentrosServicioAsync(
        [FromQuery] int? idCentro, [FromQuery] int? idStatus, [FromQuery] string? nombreCentro,
        [FromQuery] bool identico, CancellationToken ct)
        => Lista(await _service.ConsultarCentrosServicioAsync(idCentro, idStatus, nombreCentro, identico, ct), "Centros de servicio");

    /// <summary>Documentos que puede pedirse a un cliente o empleado.</summary>
    [HttpGet("documentos")]
    [Authorize(Policy = "perm:catalogos.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarDocumentosAsync(
        [FromQuery] int? idDocumento, [FromQuery] int? idStatus, [FromQuery] string? documento,
        [FromQuery] bool identico, CancellationToken ct)
        => Lista(await _service.ConsultarDocumentosAsync(idDocumento, idStatus, documento, identico, ct), "Documentos");

    /// <summary>Grupos a los que puede pertenecer un cliente.</summary>
    /// <remarks>
    /// QUIEN DA DE ALTA UN CLIENTE TAMBIEN LO PIDE.
    ///
    /// `catalogos.ver` se queda y se le suman las dos claves del padron.
    /// Medido: `Clientes._Load` de Mac31 carga este catalogo SIN comprobar
    /// permiso y, si falla, **cierra la forma**
    /// (Legacy/Mac31/Mac31/Forms/Clientes/Clientes.cs:92-115). Son los dos
    /// desplegables que decide quien da de alta: sin ellos el alta sale con los
    /// campos vacios, que es justo lo que avisaba la app
    /// ("No se pudieron cargar: condiciones de credito, tipos de cliente").
    ///
    /// No concede nada de mas: es una lista de opciones, sin dato de ningun
    /// cliente. Crear o cambiar el catalogo sigue pidiendo lo suyo
    /// (`CatalogosAdminController`), que no se toco.
    /// </remarks>
    [HttpGet("grupos-cliente")]
    [Authorize(Policy = "perm:catalogos.ver|clientes.crear|clientes.editar|app_movil.clientes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarGruposClienteAsync(
        [FromQuery] int? idGrupoCliente, [FromQuery] int? idStatus, CancellationToken ct)
        => Lista(await _service.ConsultarGruposClienteAsync(idGrupoCliente, idStatus, ct), "Grupos de cliente");

    /// <summary>Puestos de empleado.</summary>
    [HttpGet("puestos")]
    [Authorize(Policy = "perm:catalogos.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarPuestosAsync([FromQuery] int? idStatus, CancellationToken ct)
        => Lista(await _service.ConsultarPuestosAsync(idStatus, ct), "Puestos");

    /// <summary>Motivos de nota de credito.</summary>
    /// <remarks>
    /// QUIEN TIMBRA TAMBIEN LO PIDE. Medido: `VentasNotaCredito._Load` carga
    /// este catalogo sin comprobar permiso y, si falla, **cierra la forma**
    /// (`ConsultarTipoNotasCredito` ... `this.Close()`,
    /// VentasNotaCredito.cs:124-131). Con solo `catalogos.ver`, la pantalla del
    /// web abria y se quedaba en "No tienes permiso para esta accion" sobre una
    /// nota de credito que el usuario SI puede hacer.
    ///
    /// Se suma `facturacion.timbrar` y no `ventas.facturar`: una nota de
    /// credito no es "facturar desde la pantalla de ventas", es emitir un CFDI
    /// de egreso. Es un catalogo de motivos; no hay dato de nadie en el.
    /// </remarks>
    [HttpGet("tipos-nota-credito")]
    [Authorize(Policy = "perm:catalogos.ver|facturacion.timbrar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarTiposNotaCreditoAsync([FromQuery] int? idStatus, CancellationToken ct)
        => Lista(await _service.ConsultarTiposNotaCreditoAsync(idStatus, ct), "Tipos de nota de credito");

    /// <summary>Formas de pago de la cobranza.</summary>
    /// <param name="esPago">true (default) = solo los que son pago de verdad.</param>
    [HttpGet("tipos-pago")]
    [Authorize(Policy = "perm:catalogos.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarTiposPagoAsync(
        [FromQuery] int? idTipoPago, [FromQuery] int? idStatus, [FromQuery] string? tipoPago,
        [FromQuery] bool identico, [FromQuery] bool? esPago, CancellationToken ct)
        => Lista(await _service.ConsultarTiposPagoAsync(idTipoPago, idStatus, tipoPago, identico, esPago, ct), "Tipos de pago");

    /// <summary>Tipos de sueldo de nomina.</summary>
    [HttpGet("tipos-sueldo")]
    [Authorize(Policy = "perm:catalogos.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarTiposSueldoAsync([FromQuery] int? idStatus, CancellationToken ct)
        => Lista(await _service.ConsultarTiposSueldoAsync(idStatus, ct), "Tipos de sueldo");

    /// <summary>Tipos de documento de venta (remision, garantia, aceites...).</summary>
    /// <param name="sinDevCliente">true quita DEV CLIENTE (16 en Mac31 es cambio de vigencia, 15 dev cliente).</param>
    [HttpGet("tipos-documento")]
    [Authorize(Policy = "perm:catalogos.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarTiposDocumentoAsync(
        [FromQuery] int? idTipoDocumento, [FromQuery] bool? mostrarPantalla,
        [FromQuery] bool sinDevCliente, [FromQuery] bool sinCambioVigencia, CancellationToken ct)
        => Lista(await _service.ConsultarTiposDocumentoAsync(idTipoDocumento, mostrarPantalla, sinDevCliente, sinCambioVigencia, ct),
            "Tipos de documento");

    /// <summary>Tipos de recordatorio.</summary>
    [HttpGet("tipos-recordatorio")]
    [Authorize(Policy = "perm:catalogos.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarTiposRecordatorioAsync(
        [FromQuery] int? idTipoRecordatorio, [FromQuery] int? idStatus, [FromQuery] string? tipoRecordatorio,
        [FromQuery] bool? esUrgente, [FromQuery] bool? esImportante, [FromQuery] bool identico, CancellationToken ct)
        => Lista(await _service.ConsultarTiposRecordatorioAsync(idTipoRecordatorio, idStatus, tipoRecordatorio, esUrgente, esImportante, identico, ct),
            "Tipos de recordatorio");

    /// <summary>Unidades de medida de productos y servicios.</summary>
    [HttpGet("unidades-medida")]
    [Authorize(Policy = "perm:catalogos.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarUnidadesMedidaAsync(
        [FromQuery] int? idUnidadMedida, [FromQuery] int? idStatus, [FromQuery] string? unidadMedida,
        [FromQuery] bool? esPieza, [FromQuery] bool? esCaja, [FromQuery] bool? esServicio,
        [FromQuery] bool identico, CancellationToken ct)
        => Lista(await _service.ConsultarUnidadesMedidaAsync(idUnidadMedida, idStatus, unidadMedida, esPieza, esCaja, esServicio, identico, ct),
            "Unidades de medida");

    /// <summary>
    /// Galleros con su saldo. Lleva permiso propio porque el renglon trae dinero.
    /// </summary>
    [HttpGet("galleros")]
    [Authorize(Policy = "perm:catalogos.galleros.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarGallerosAsync(
        [FromQuery] int? idGallero, [FromQuery] int? idStatus, [FromQuery] string? gallero, CancellationToken ct)
        => Lista(await _service.ConsultarGallerosAsync(idGallero, idStatus, gallero, ct), "Galleros");

    // ------------------------------------------------------------------ productos

    /// <summary>Marcas frente a una categoria, con la palomita de la relacion.</summary>
    /// <param name="idCategoria">Categoria contra la que se compara. Obligatoria.</param>
    /// <param name="soloMarcadas">true deja solo las marcas ya relacionadas.</param>
    [HttpGet("categorias/{idCategoria:int}/marcas")]
    [Authorize(Policy = "perm:catalogos.marcas.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarCategoriasMarcasAsync(
        int idCategoria, [FromQuery] int? idMarca, [FromQuery] bool soloMarcadas, CancellationToken ct)
        => Lista(await _service.ConsultarCategoriasMarcasAsync(idCategoria, idMarca, soloMarcadas, ct), "Marcas de la categoria");

    /// <summary>
    /// Lo mismo que el anterior pero arrastrando el grupo de categoria. En empresas con
    /// Constantes.Funcionalidad = 'ZARA' el SP lee los catalogos de la base de Zaragoza.
    /// </summary>
    [HttpGet("grupos-categoria/marcas")]
    [Authorize(Policy = "perm:catalogos.marcas.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarGrupoCategoriasMarcasAsync(
        [FromQuery] int idCategoria, [FromQuery] int? idMarca, CancellationToken ct)
        => Lista(await _service.ConsultarGrupoCategoriasMarcasAsync(idCategoria, idMarca, ct), "Marcas del grupo de categoria");

    // ---------------------------------------------------------- formas de seguridad de Mac31

    /// <summary>
    /// Formas (pantallas) de Mac31. Lleva permiso propio: es el inventario de pantallas del
    /// sistema viejo y quien lo lee esta administrando seguridad, no capturando.
    /// </summary>
    [HttpGet("formas")]
    [Authorize(Policy = "perm:catalogos.formas.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarFormasAsync(
        [FromQuery] int? idForma, [FromQuery] bool? aplicaContrasena, CancellationToken ct)
        => Lista(await _service.ConsultarFormasAsync(idForma, aplicaContrasena, ct), "Formas");

    /*
      FORMAS-PROCESOS: EL ENDPOINT DE LEGACY NUNCA DEVOLVIO UN PROCESO.

      /api/catformasprocesos/consultar de Legacy manda @IDForma, @IDProceso y
      @AplicaContrasena a sp_n_ConsultaFormas, que solo declara dos parametros. SQL Server
      contesta "Procedure or function sp_n_ConsultaFormas has too many arguments specified"
      y el endpoint devuelve el error: comprobado en Produccion_svr el 6-oct-2026. O sea que
      la capacidad existe en el inventario pero nunca funciono, y los procesos no estan en
      ese SP (viven en n_Procesos / UsuarioFormaProceso, que es lo que lee PermissionService).

      Se conserva la ruta para que el inventario de la migracion cierre y para no dejar un
      hueco silencioso, y devuelve lo que el SP de verdad da: las formas. El idProceso se
      acepta y se ignora, con esta nota, en vez de mandarlo y fallar igual que Legacy.
    */

    /// <summary>
    /// Formas de Mac31. Alias historico de <c>formas</c>: el SP de Legacy no devuelve procesos.
    /// </summary>
    /// <param name="idProceso">Se acepta por compatibilidad y se ignora: el SP no lo tiene.</param>
    [HttpGet("formas-procesos")]
    [Authorize(Policy = "perm:catalogos.formas.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarFormasProcesosAsync(
        [FromQuery] int? idForma, [FromQuery] int? idProceso, [FromQuery] bool? aplicaContrasena, CancellationToken ct)
    {
        _ = idProceso;
        return Lista(await _service.ConsultarFormasAsync(idForma, aplicaContrasena, ct), "Formas");
    }

    // --------------------------------------------------------------- recaudaciones

    /// <summary>Cajas de cobro. Modulo de recaudaciones.</summary>
    [HttpGet("cajas")]
    [Authorize(Policy = "perm:catalogos.recaudaciones.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarCajasAsync(
        [FromQuery] int? idCaja, [FromQuery] int? idStatus, [FromQuery] int? numCaja,
        [FromQuery] string? nombreCaja, [FromQuery] string? equipoCaja, CancellationToken ct)
        => Lista(await _service.ConsultarCajasAsync(idCaja, idStatus, numCaja, nombreCaja, equipoCaja, ct), "Cajas");

    /// <summary>Conceptos de recaudacion.</summary>
    [HttpGet("conceptos")]
    [Authorize(Policy = "perm:catalogos.recaudaciones.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarConceptosAsync(
        [FromQuery] int? idConcepto, [FromQuery] int? idStatus, [FromQuery] string? numConcepto,
        [FromQuery] string? nombreConcepto, CancellationToken ct)
        => Lista(await _service.ConsultarConceptosAsync(idConcepto, idStatus, numConcepto, nombreConcepto, ct), "Conceptos");

    /// <summary>Siguiente numero de cuenta libre dentro de un concepto.</summary>
    [HttpGet("conceptos/{idConcepto:int}/siguiente-numero-cuenta")]
    [Authorize(Policy = "perm:catalogos.recaudaciones.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarSiguienteNumeroCuentaAsync(int idConcepto, CancellationToken ct)
        => Lista(await _service.ConsultarSiguienteNumeroCuentaAsync(idConcepto, ct), "Siguiente numero de cuenta");

    /// <summary>Auxiliares de recaudacion por economico.</summary>
    [HttpGet("economicos-auxiliares")]
    [Authorize(Policy = "perm:catalogos.recaudaciones.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarEconomicosAuxiliaresAsync(
        [FromQuery] ConsultaEconomicosAuxiliaresFiltro filtro, CancellationToken ct)
        => Lista(await _service.ConsultarEconomicosAuxiliaresAsync(filtro ?? new ConsultaEconomicosAuxiliaresFiltro(), ct),
            "Economicos auxiliares");
}
