using ISL_Service.Application.DTOs.CatalogosAdmin;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Las siete consultas de listado que le faltaban a api/catalogos.
///
/// QUE PROBLEMA RESUELVE ESTE ARCHIVO
/// ----------------------------------
/// api/catalogos ya tenia POST, PUT, inhabilitar y habilitar de almacenes,
/// agentes, tipos de gasto, autos, grupos de categoria, categorias de Mac31 y
/// tipos de usado, pero ningun GET que los listara. O sea que el front podia dar
/// de alta un almacen y no podia enseñar los almacenes: la pantalla de catalogo
/// no era construible. Las siete que ya tenian GET (bancos, marcas, galleros,
/// repartidores, condiciones de credito, tipos de recordatorio, dias festivos)
/// se dejan donde estan.
///
/// SON GET, como el resto de la ola 3: en Legacy eran POST porque su manejador
/// de token exigia el token dentro del JSON. Aqui el token va en la cabecera, asi
/// que una consulta vuelve a ser una consulta y se puede compartir por URL.
///
/// EL PERMISO
/// ----------
/// `catalogos.ver` para los cinco generales, y `catalogos.marcas.ver` para
/// grupos de categoria y categorias, porque es el mismo permiso con el que ya se
/// leen las marcas y su relacion (CatalogosGeneralesController) y las tres
/// pantallas son el catalogo de productos: separarlas dejaria que alguien viera
/// las subcategorias y no las marcas de la misma pantalla.
///
/// `ExigirPermisoAsync` y no `[Authorize(Policy = ...)]` para seguir a
/// ConsultarDiasFestivos, que es el GET que ya vivia en este controlador: los
/// POST/PUT de aqui tampoco usan la politica, usan el mismo metodo de
/// PermisoControllerBase. Una sola forma por controlador.
/// </summary>
public partial class CatalogosAdminController
{
    private static readonly string[] PermisosVer = { "catalogos.ver" };
    private static readonly string[] PermisosMarcasVer = { "catalogos.marcas.ver" };

    /// <summary>
    /// El sobre de un listado. `total` va aparte de `data` porque la barra de
    /// filtros del front pinta el conteo antes de recorrer la lista, igual que el
    /// "TOTAL ALMACENES: n" de Mac31.
    /// </summary>
    private IActionResult Listado(List<Dictionary<string, object?>> data, string que)
        => Ok(new { ok = true, message = $"{que}: {data.Count}.", data, total = data.Count });

    /// <summary>
    /// Consulta de almacenes. <paramref name="idStatus"/>: 0 todos, 1 activos, 2 inactivos.
    /// </summary>
    [HttpGet("almacenes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarAlmacenes(
        [FromQuery] int idAlmacen = 0,
        [FromQuery] int idStatus = 0,
        [FromQuery] string? almacen = null,
        [FromQuery] bool identico = false,
        CancellationToken ct = default)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        return Listado(await _service.ConsultarAlmacenesAsync(
            new AlmacenesFiltro { IdAlmacen = idAlmacen, IdStatus = idStatus, Almacen = almacen, Identico = identico }, ct),
            "Almacenes");
    }

    /// <summary>
    /// Consulta de agentes. Devuelve el numero, el nombre y el estatus.
    ///
    /// No se confunda con `agentes-disponibles`: ese entrega los NUMEROS que
    /// todavia no tiene nadie, para el desplegable del alta. Este entrega los
    /// agentes que existen.
    /// </summary>
    [HttpGet("agentes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarAgentes(
        [FromQuery] int idAgente = 0,
        [FromQuery] int idStatus = 0,
        [FromQuery] int numeroAgente = 0,
        [FromQuery] string? nombreAgente = null,
        [FromQuery] bool identico = false,
        CancellationToken ct = default)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        return Listado(await _service.ConsultarAgentesAsync(
            new AgentesFiltro
            {
                IdAgente = idAgente,
                IdStatus = idStatus,
                NumeroAgente = numeroAgente,
                NombreAgente = nombreAgente,
                Identico = identico
            }, ct),
            "Agentes");
    }

    /// <summary>Consulta de tipos de gasto.</summary>
    [HttpGet("tipos-gasto")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarTiposGasto(
        [FromQuery] int idTipoGasto = 0,
        [FromQuery] int idStatus = 0,
        [FromQuery] string? tipoGasto = null,
        [FromQuery] bool identico = false,
        CancellationToken ct = default)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        return Listado(await _service.ConsultarTiposGastoAsync(
            new TiposGastoFiltro { IdTipoGasto = idTipoGasto, IdStatus = idStatus, TipoGasto = tipoGasto, Identico = identico }, ct),
            "Tipos de gasto");
    }

    /// <summary>
    /// Consulta de autos, con verificacion, seguro y rendimiento.
    ///
    /// El SP busca el nombre por LIKE con comodin a los dos lados y no acepta
    /// coincidencia exacta; por eso aqui no hay `identico`.
    /// </summary>
    [HttpGet("autos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarAutos(
        [FromQuery] int idAuto = 0,
        [FromQuery] int idStatus = 0,
        [FromQuery] string? nombre = null,
        CancellationToken ct = default)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        return Listado(await _service.ConsultarAutosAsync(
            new AutosFiltro { IdAuto = idAuto, IdStatus = idStatus, Nombre = nombre }, ct),
            "Autos");
    }

    /// <summary>
    /// Consulta de grupos de categoria. En la pantalla de Mac31 estos se llaman
    /// "CATEGORIAS" y las categorias se llaman "SUB CATEGORIAS"; el nombre de la
    /// tabla dice lo contrario y aqui se respeta el de la tabla, que es el que
    /// usa el resto del API.
    /// </summary>
    [HttpGet("grupos-categoria")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarGruposCategoria(
        [FromQuery] string? idsGrupoCategoria = null,
        [FromQuery] int idStatus = 0,
        [FromQuery] string? grupoCategoria = null,
        [FromQuery] bool identico = false,
        CancellationToken ct = default)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosMarcasVer, ct);
        if (sinPermiso != null) return sinPermiso;

        return Listado(await _service.ConsultarGruposCategoriaAsync(
            new GruposCategoriaFiltro
            {
                IdsGrupoCategoria = idsGrupoCategoria,
                IdStatus = idStatus,
                GrupoCategoria = grupoCategoria,
                Identico = identico
            }, ct),
            "Grupos de categoria");
    }

    /// <summary>
    /// Consulta de categorias de Mac31 (las "sub categorias" de la pantalla).
    /// Cada renglon trae su grupo, que es lo que la pantalla enseña en la primera
    /// columna.
    /// </summary>
    [HttpGet("categorias-mac")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarCategoriasMac(
        [FromQuery] string? idsCategoria = null,
        [FromQuery] string? idsGrupoCategoria = null,
        [FromQuery] int idStatus = 0,
        [FromQuery] string? categoria = null,
        [FromQuery] bool identico = false,
        CancellationToken ct = default)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosMarcasVer, ct);
        if (sinPermiso != null) return sinPermiso;

        return Listado(await _service.ConsultarCategoriasMacAsync(
            new CategoriasMacFiltro
            {
                IdsCategoria = idsCategoria,
                IdsGrupoCategoria = idsGrupoCategoria,
                IdStatus = idStatus,
                Categoria = categoria,
                Identico = identico
            }, ct),
            "Categorias");
    }

    /// <summary>
    /// Consulta de tipos de usado (los grupos de casco), con sus precios con y
    /// sin IVA, bonificacion, peso y existencia.
    ///
    /// La lista sale ya filtrada por empresa y el filtro NO esta en el contrato:
    /// el propio SP elige la empresa (la 9 cuando Constantes.Funcionalidad es
    /// 'TAU', el MIN(IDEmpresa) activo en cualquier otro caso). Se deja asi para
    /// que el web vea los mismos cascos que Mac31.
    /// </summary>
    [HttpGet("tipos-usado")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarTiposUsado(
        [FromQuery] int idTipoUsado = 0,
        [FromQuery] int idStatus = 0,
        [FromQuery] string? tipoUsado = null,
        [FromQuery] bool identico = false,
        CancellationToken ct = default)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        return Listado(await _service.ConsultarTiposUsadoAsync(
            new TiposUsadoFiltro { IdTipoUsado = idTipoUsado, IdStatus = idStatus, TipoUsado = tipoUsado, Identico = identico }, ct),
            "Tipos de usado");
    }

    // -----------------------------------------------------------------------
    // Las constantes de operacion
    // -----------------------------------------------------------------------

    /*
      POR QUE ESTA RUTA ES ABSOLUTA Y VIVE EN ESTE CONTROLADOR.

      La direccion correcta es /api/constantes: ahi estan ya los tres PUT que la
      escriben (ConfiguracionMacController). Lo que no habia era con que LEERLA,
      y la pantalla de fecha de operacion no se puede construir sin eso: enseña
      la fecha de ventas, la de pagos, quien las movio y los dos contadores de
      dias antes de dejar cambiar nada.

      Se publica desde aqui, con ruta absoluta -como ya hacen VentasPagos-
      Operaciones y CascosCambio-, porque el SELECT que la resuelve vive en
      CatalogosAdminRepository y moverlo a SeguridadMacRepository solo para que
      el archivo del controlador cuadre seria mover codigo para que un nombre
      quede bonito. Para quien consume el API no hay diferencia: GET y PUT de
      /api/constantes.

      EL PERMISO ES `constantes.ver`, nuevo, no `constantes.editar`: ver la fecha
      de operacion lo necesita mucha mas gente que moverla.
    */
    [HttpGet("/api/constantes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConsultarConstantesOperacion(CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(new[] { "constantes.ver" }, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.ConsultarConstantesOperacionAsync(ct);
        if (data is null)
            return NotFound(new { ok = false, message = "La tabla Constantes no tiene ningun renglon en esta base." });

        return Ok(new { ok = true, message = "Constantes de operacion consultadas.", data });
    }

    /// <summary>
    /// Consulta de empresas de Mac31, con su ficha fiscal completa.
    ///
    /// NO ES `GET api/empresas`. Aquella devuelve { empresaId, nombre } y es la
    /// lista de inquilinos para el selector de empresa; esta devuelve lo que la
    /// pantalla edita: razon social, RFC, regimen fiscal, banco, cuenta, CLABE, el
    /// domicilio ya resuelto por el procedimiento y el Id de Facturama. Dos cosas
    /// distintas con el mismo nombre, y por eso esta cuelga de api/catalogos.
    ///
    /// Permiso `catalogos.ver`: la ficha trae el RFC y la cuenta bancaria, pero no
    /// una contrasena ni un token. Para ESCRIBIRLA hace falta
    /// `catalogos.empresas.editar`, que es donde esta el candado que importa.
    /// </summary>
    [HttpGet("empresas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarEmpresasMac(
        [FromQuery] int idEmpresa = 0,
        [FromQuery] int idStatus = 0,
        [FromQuery] string? razonSocial = null,
        [FromQuery] bool identico = false,
        CancellationToken ct = default)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        return Listado(await _service.ConsultarEmpresasMacAsync(
            new EmpresasMacFiltro
            {
                IdEmpresa = idEmpresa,
                IdStatus = idStatus,
                RazonSocial = razonSocial,
                Identico = identico
            }, ct),
            "Empresas");
    }

    /// <summary>
    /// Guarda de un golpe TODA la relacion de una subcategoria con las marcas.
    ///
    /// POR QUE NO BASTA CON EL DE A UNO (`PUT categorias-mac/marcas`).
    /// La pantalla de relacion tiene una casilla por marca —133 en esta base—, asi
    /// que guardar un cambio con el endpoint de a uno son 133 peticiones. Es lo que
    /// hace Mac31, y de ahi salen sus dos defectos: si la numero 70 falla, las 69
    /// primeras ya se escribieron y nadie sabe cuales; y su bucle recorre
    /// `dataGridView1.Rows`, que es solo LA PAGINA VISIBLE del grid paginado, asi
    /// que guardar estando en la pagina 2 no guarda la 1.
    ///
    /// Este recibe el conjunto completo y lo escribe en UNA transaccion: o queda
    /// como se dejo en pantalla, o no queda nada. Sigue siendo idempotente.
    ///
    /// El de a uno se queda donde esta: sirve para mover una sola marca y hay quien
    /// ya lo llama.
    /// </summary>
    [HttpPut("categorias-mac/{idCategoria:int}/marcas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RelacionarCategoriaMarcasEnLote(
        [FromRoute] int idCategoria,
        [FromBody] RelacionCategoriaMarcasLoteRequest? request,
        CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        if (idCategoria <= 0) return BadRequest(new { ok = false, message = "La categoria es requerida." });
        if (request.Marcas.Count == 0)
            return BadRequest(new { ok = false, message = "No viene ninguna marca." });
        if (request.Marcas.Any(m => m.IdMarca <= 0))
            return BadRequest(new { ok = false, message = "Hay una marca sin clave." });

        /*
          UN TOPE, porque esto escribe en bucle dentro de una transaccion. 2,000 es
          holgado -la base tiene 133 marcas- y evita que un cuerpo armado a mano
          deje la tabla bloqueada un rato largo.
        */
        if (request.Marcas.Count > 2000)
            return BadRequest(new { ok = false, message = "Son demasiadas marcas en una sola llamada." });

        var sinPermiso = await ExigirPermisoAsync(PermisosMarcasEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.RelacionarCategoriaMarcasAsync(idCategoria, request.Marcas, ct);
        return Ok(new { ok = data.Guardo, message = "Relacion guardada.", data });
    }
}
