using ISL_Service.Application.DTOs.CatalogosAdmin;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Segunda mitad de las escrituras de catalogo: dias festivos, territorial,
/// categorias y marcas de Mac31, autos, y las empresas de Mac31 con sus series.
/// </summary>
public partial class CatalogosAdminController
{
    // =======================================================================
    // Dias festivos
    // =======================================================================

    /// <summary>
    /// Consulta de dias festivos.
    ///
    /// Es un GET aunque en Legacy fuera POST: alla el token viajaba en el JSON
    /// y por eso no podia ser GET; aqui va en la cabecera.
    /// </summary>
    [HttpGet("dias-festivos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarDiasFestivos(
        [FromQuery] int idDiaFestivo = 0,
        [FromQuery] int idStatus = 0,
        [FromQuery] DateTime? desde = null,
        [FromQuery] DateTime? hasta = null,
        CancellationToken ct = default)
    {
        var sinPermiso = await ExigirLecturaDeModuloAsync("catalogos", new[] { "catalogos.ver" }, ct);
        if (sinPermiso != null) return sinPermiso;

        var data = await _service.ConsultarDiasFestivosAsync(
            new DiasFestivosFiltro
            {
                IdDiaFestivo = idDiaFestivo,
                IdStatus = idStatus,
                DiaFestivoInicial = desde,
                DiaFestivoFinal = hasta
            }, ct);

        return Ok(new { ok = true, message = "Dias festivos consultados.", data, total = data.Count });
    }

    /// <summary>Marca un dia como festivo.</summary>
    [HttpPost("dias-festivos")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearDiaFestivo([FromBody] CrearDiaFestivoRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        if (request.DiaFestivo == default)
            return BadRequest(new { ok = false, message = "La fecha del dia festivo es requerida." });

        var sinPermiso = await ExigirPermisoAsync(PermisosCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        return RespondeAlta(await _service.CrearDiaFestivoAsync(request, IdUsuarioLegacy(), Equipo(), ct),
            "Dia festivo dado de alta.");
    }

    /// <summary>
    /// Cambia la fecha de un dia festivo.
    ///
    /// OJO CON LO QUE HACE LEGACY AQUI: si la fecha nueva no existe todavia en
    /// DiasFestivos, sp_n_ActualizarDiaFestivo da de BAJA el renglon del id e
    /// INSERTA uno nuevo con la fecha nueva. O sea que no modifica el renglon:
    /// lo reemplaza, y el id deja de ser el mismo. Se respeta tal cual porque es
    /// lo que hace Mac31 y el calendario tiene que verse igual en los dos; queda
    /// escrito para que nadie se sorprenda de que el id cambie.
    /// </summary>
    [HttpPut("dias-festivos/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarDiaFestivo([FromRoute] int id, [FromBody] ActualizarDiaFestivoRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        if (request.DiaFestivo == default)
            return BadRequest(new { ok = false, message = "La fecha del dia festivo es requerida." });

        var sinPermiso = await ExigirPermisoAsync(PermisosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await _service.ActualizarDiaFestivoAsync(id, request, IdUsuarioLegacy(), Equipo(), ct),
            "Dia festivo actualizado.");
    }

    [HttpPost("dias-festivos/{id:int}/inhabilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InhabilitarDiaFestivo([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusDiaFestivoAsync(id, StatusCancelado, IdUsuarioLegacy(), Equipo(), ct),
            "Dia festivo inhabilitado.", "El dia festivo no existe.");
    }

    [HttpPost("dias-festivos/{id:int}/habilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HabilitarDiaFestivo([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusDiaFestivoAsync(id, StatusActivo, IdUsuarioLegacy(), Equipo(), ct),
            "Dia festivo habilitado.", "El dia festivo no existe.");
    }

    // =======================================================================
    // Territorial
    //
    // Ciudades, municipios y colonias NO tienen baja: sus tablas no tienen
    // IDStatus. Legacy exponia un DELETE que, por reusar el UPDATE, solo dejaba
    // el nombre vacio. No se hereda.
    // =======================================================================

    [HttpPost("ciudades")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearCiudad([FromBody] CrearCiudadRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.Ciudad, 250, "nombre de la ciudad")
                    ?? ValidaNombre(request.IdEstado, 2, "estado");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosTerritorialCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        return RespondeAlta(await _service.CrearCiudadAsync(request, ct), "Ciudad dada de alta.");
    }

    /// <summary>
    /// Modificacion de ciudad. El id es TEXTO: [Catalogo Ciudades].IDCiudad es
    /// varchar, y un "09" convertido a 9 no encuentra su renglon.
    /// </summary>
    [HttpPut("ciudades/{idCiudad}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarCiudad([FromRoute] string idCiudad, [FromBody] ActualizarCiudadRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(idCiudad, 100, "id de la ciudad")
                    ?? ValidaNombre(request.Ciudad, 250, "nombre de la ciudad")
                    ?? ValidaNombre(request.IdEstado, 2, "estado");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosTerritorialEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await _service.ActualizarCiudadAsync(idCiudad, request, ct), "Ciudad actualizada.");
    }

    [HttpPost("municipios")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearMunicipio([FromBody] CrearMunicipioRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.Municipio, 250, "nombre del municipio")
                    ?? ValidaNombre(request.IdEstado, 2, "estado");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosTerritorialCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        return RespondeAlta(await _service.CrearMunicipioAsync(request, ct), "Municipio dado de alta.");
    }

    [HttpPut("municipios/{idMunicipio}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarMunicipio([FromRoute] string idMunicipio, [FromBody] ActualizarMunicipioRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(idMunicipio, 100, "id del municipio")
                    ?? ValidaNombre(request.Municipio, 250, "nombre del municipio")
                    ?? ValidaNombre(request.IdEstado, 2, "estado");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosTerritorialEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await _service.ActualizarMunicipioAsync(idMunicipio, request, ct), "Municipio actualizado.");
    }

    [HttpPost("colonias")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearColonia([FromBody] CrearColoniaRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.Colonia, 100, "nombre de la colonia")
                    ?? ValidaNombre(request.IdEstado, 2, "estado")
                    ?? ValidaNombre(request.IdCiudad, 2, "ciudad")
                    ?? ValidaNombre(request.IdMunicipio, 3, "municipio")
                    ?? ValidaNombre(request.CodigoPostal, 250, "codigo postal");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosTerritorialCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        return RespondeAlta(await _service.CrearColoniaAsync(request, ct), "Colonia dada de alta.");
    }

    [HttpPut("colonias/{idColonia:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarColonia([FromRoute] int idColonia, [FromBody] ActualizarColoniaRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.Colonia, 100, "nombre de la colonia")
                    ?? ValidaNombre(request.IdEstado, 2, "estado")
                    ?? ValidaNombre(request.IdCiudad, 2, "ciudad")
                    ?? ValidaNombre(request.IdMunicipio, 3, "municipio")
                    ?? ValidaNombre(request.CodigoPostal, 250, "codigo postal");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosTerritorialEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await _service.ActualizarColoniaAsync(idColonia, request, ct), "Colonia actualizada.");
    }

    /// <summary>
    /// Alta de unidad territorial. El SP no esta en esta base (es de otra
    /// instalacion): aqui la llamada falla igual que falla Legacy.
    /// </summary>
    [HttpPost("unidades-territoriales")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearUnidadTerritorial([FromBody] UnidadTerritorialRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.Clave, 100, "clave");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosTerritorialCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        return RespondeAlta(await _service.CrearUnidadTerritorialAsync(request, IdUsuarioLegacy(), Equipo(), ct),
            "Unidad territorial dada de alta.");
    }

    /// <summary>
    /// Modificacion de unidad territorial. <b>Sin cuerpo</b>: el SP de legacy
    /// recibe unicamente el id.
    /// </summary>
    /// <remarks>
    /// AQUI SE EXIGIA UN CUERPO QUE SE TIRABA. El PUT declaraba
    /// <c>UnidadTerritorialRequest</c> como obligatorio (clave, idStatus,
    /// unidadTerritorial) y el repositorio no mandaba ninguno de los tres al
    /// procedimiento: renombrar una unidad era un no-op silencioso con 200 de
    /// respuesta. El motivo de fondo esta en
    /// <c>CatalogosAdminRepository.ActualizarUnidadTerritorialAsync</c> — Legacy
    /// manda solo el id y su SP no existe en ninguna de las dos bases, asi que no
    /// se puede adivinar la firma. Se quito del contrato en vez de seguir
    /// pidiendolo. Un cuerpo que mande un front viejo se ignora sin romperse.
    /// </remarks>
    [HttpPut("unidades-territoriales/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarUnidadTerritorial([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosTerritorialEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await _service.ActualizarUnidadTerritorialAsync(id, ct),
            "Unidad territorial actualizada.");
    }

    /// <summary>Alta de centro de servicio. Su SP tampoco esta en esta base.</summary>
    [HttpPost("centros-servicio")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearCentroServicio([FromBody] CentroServicioRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.CentroServicio, 400, "nombre del centro");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosTerritorialCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        return RespondeAlta(await _service.CrearCentroServicioAsync(request, IdUsuarioLegacy(), Equipo(), ct),
            "Centro de servicio dado de alta.");
    }

    [HttpPut("centros-servicio/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarCentroServicio([FromRoute] int id, [FromBody] CentroServicioRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.CentroServicio, 400, "nombre del centro");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosTerritorialEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await _service.ActualizarCentroServicioAsync(id, request, IdUsuarioLegacy(), Equipo(), ct),
            "Centro de servicio actualizado.");
    }

    // =======================================================================
    // Grupos de categoria, categorias y marcas de Mac31
    // =======================================================================

    [HttpPost("grupos-categoria")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearGrupoCategoria([FromBody] CatalogoNombreRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.Nombre, 250);
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosMarcasCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        return RespondeAlta(await _service.CrearGrupoCategoriaAsync(request, ct), "Grupo de categoria dado de alta.");
    }

    [HttpPut("grupos-categoria/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarGrupoCategoria([FromRoute] int id, [FromBody] CatalogoNombreUpdateRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.Nombre, 250);
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosMarcasEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await _service.ActualizarGrupoCategoriaAsync(id, request, ct), "Grupo de categoria actualizado.");
    }

    [HttpPost("grupos-categoria/{id:int}/inhabilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InhabilitarGrupoCategoria([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosMarcasBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusGrupoCategoriaAsync(id, StatusCancelado, ct),
            "Grupo de categoria inhabilitado.", "El grupo de categoria no existe.");
    }

    [HttpPost("grupos-categoria/{id:int}/habilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HabilitarGrupoCategoria([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosMarcasBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusGrupoCategoriaAsync(id, StatusActivo, ct),
            "Grupo de categoria habilitado.", "El grupo de categoria no existe.");
    }

    /// <summary>
    /// Alta de categoria de PRODUCTO de Mac31 (tabla Categorias).
    ///
    /// La ruta es api/catalogos/categorias-mac y no api/categorias a proposito:
    /// api/categorias ya existe y es de otra aplicacion (torneos), con llave
    /// Guid. Son dos catalogos distintos que se llaman igual; mezclarlos en una
    /// ruta seria invitar a borrar el equivocado.
    /// </summary>
    [HttpPost("categorias-mac")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearCategoriaMac([FromBody] CrearCategoriaMacRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.Categoria, 1000, "nombre de la categoria")
                    ?? ValidaId(request.IdGrupoCategoria, "grupo de categoria");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosMarcasCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        return RespondeAlta(await _service.CrearCategoriaMacAsync(request, ct), "Categoria dada de alta.");
    }

    [HttpPut("categorias-mac/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarCategoriaMac([FromRoute] int id, [FromBody] ActualizarCategoriaMacRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.Categoria, 2000, "nombre de la categoria")
                    ?? ValidaId(request.IdGrupoCategoria, "grupo de categoria");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosMarcasEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await _service.ActualizarCategoriaMacAsync(id, request, ct), "Categoria actualizada.");
    }

    [HttpPost("categorias-mac/{id:int}/inhabilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InhabilitarCategoriaMac([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosMarcasBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusCategoriaMacAsync(id, StatusCancelado, ct),
            "Categoria inhabilitada.", "La categoria no existe.");
    }

    [HttpPost("categorias-mac/{id:int}/habilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HabilitarCategoriaMac([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosMarcasBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusCategoriaMacAsync(id, StatusActivo, ct),
            "Categoria habilitada.", "La categoria no existe.");
    }

    [HttpPost("marcas")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearMarca([FromBody] CrearMarcaRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.Marca, 250, "nombre de la marca")
                    ?? ValidaNombre(request.MarcaCorto, 250, "nombre corto de la marca");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosMarcasCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        return RespondeAlta(await _service.CrearMarcaAsync(request, ct), "Marca dada de alta.");
    }

    [HttpPut("marcas/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarMarca([FromRoute] int id, [FromBody] ActualizarMarcaRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.Marca, 250, "nombre de la marca")
                    ?? ValidaNombre(request.MarcaCorto, 250, "nombre corto de la marca");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosMarcasEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await _service.ActualizarMarcaAsync(id, request, ct), "Marca actualizada.");
    }

    [HttpPost("marcas/{id:int}/inhabilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InhabilitarMarca([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosMarcasBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusMarcaAsync(id, StatusCancelado, ct),
            "Marca inhabilitada.", "La marca no existe.");
    }

    [HttpPost("marcas/{id:int}/habilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HabilitarMarca([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosMarcasBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusMarcaAsync(id, StatusActivo, ct),
            "Marca habilitada.", "La marca no existe.");
    }

    /// <summary>
    /// Prende o apaga la relacion entre una categoria y una marca.
    ///
    /// Es un PUT y no un POST/DELETE porque es un interruptor idempotente:
    /// mandar marcado=true dos veces deja el mismo estado. El SP inserta el par
    /// si falta y lo borra si marcado=false — es el unico borrado fisico de
    /// este controlador, y lo es porque la tabla solo tiene las dos llaves.
    /// </summary>
    [HttpPut("categorias-mac/marcas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RelacionarCategoriaMarca([FromBody] RelacionCategoriaMarcaRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaId(request.IdCategoria, "categoria") ?? ValidaId(request.IdMarca, "marca");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosMarcasEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await _service.RelacionarCategoriaMarcaAsync(request, ct),
            request.Marcado ? "Marca relacionada con la categoria." : "Relacion quitada.");
    }

    // =======================================================================
    // Autos
    // =======================================================================

    [HttpPost("autos")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearAuto([FromBody] CrearAutoRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaAuto(request);
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosAutosCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        return RespondeAlta(await _service.CrearAutoAsync(request, IdUsuarioLegacy(), Equipo(), ct),
            "Auto dado de alta.");
    }

    [HttpPut("autos/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarAuto([FromRoute] int id, [FromBody] ActualizarAutoRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaAuto(request);
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosAutosEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await _service.ActualizarAutoAsync(id, request, IdUsuarioLegacy(), Equipo(), ct),
            "Auto actualizado.");
    }

    [HttpPost("autos/{id:int}/inhabilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InhabilitarAuto([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosAutosBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusAutoAsync(id, StatusCancelado, IdUsuarioLegacy(), Equipo(), ct),
            "Auto inhabilitado.", "El auto no existe.");
    }

    [HttpPost("autos/{id:int}/habilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HabilitarAuto([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosAutosBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusAutoAsync(id, StatusActivo, IdUsuarioLegacy(), Equipo(), ct),
            "Auto habilitado.", "El auto no existe.");
    }

    /// <summary>
    /// Validacion del auto. Si dice que verifica o que tiene seguro, las fechas
    /// de eso son obligatorias: un auto "asegurado" sin vigencia es un dato que
    /// no sirve para lo unico que se usa, avisar antes de que venza.
    /// </summary>
    private static object? ValidaAuto(CrearAutoRequest r)
    {
        var error = ValidaNombre(r.Nombre, 400, "nombre del auto") ?? ValidaNombre(r.Placas, 100, "placas");
        if (error != null) return error;

        if (r.Modelo is < 1900 or > 2200)
            return new { ok = false, message = "El modelo no parece un anio valido." };

        if (r.TieneSeguro && (r.SeguroFechaInicio is null || r.SeguroFechaTermino is null))
            return new { ok = false, message = "Si el auto tiene seguro, la vigencia es requerida." };

        if (r.SeguroFechaInicio.HasValue && r.SeguroFechaTermino.HasValue
            && r.SeguroFechaTermino.Value < r.SeguroFechaInicio.Value)
            return new { ok = false, message = "La vigencia del seguro termina antes de empezar." };

        if (r.MontoPoliza < 0 || r.KmPorLitroMinimo < 0 || r.KmPorLitroMaximo < 0 || r.TotalTanqueLleno < 0)
            return new { ok = false, message = "Los montos y rendimientos no pueden ser negativos." };

        if (r.KmPorLitroMaximo > 0 && r.KmPorLitroMinimo > r.KmPorLitroMaximo)
            return new { ok = false, message = "El rendimiento minimo no puede ser mayor que el maximo." };

        return null;
    }

    // =======================================================================
    // Empresas de Mac31 y sus series de folios
    // =======================================================================

    /// <summary>
    /// Alta de empresa de Mac31 (tabla Empresas): la razon social que factura.
    /// No es la EmpresaWeb del modelo de permisos, que vive en api/empresas.
    /// </summary>
    [HttpPost("empresas")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearEmpresaMac([FromBody] CrearEmpresaMacRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.NombreCorto, 400, "nombre corto")
                    ?? ValidaNombre(request.RazonSocial, 2000, "razon social")
                    ?? ValidaNombre(request.RFC, 100, "RFC")
                    ?? ValidaId(request.IdColonia, "colonia");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosEmpresasCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        return RespondeAlta(await _service.CrearEmpresaMacAsync(request, ct), "Empresa dada de alta.");
    }

    [HttpPut("empresas/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarEmpresaMac([FromRoute] int id, [FromBody] ActualizarEmpresaMacRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.NombreCorto, 400, "nombre corto")
                    ?? ValidaNombre(request.RazonSocial, 2000, "razon social")
                    ?? ValidaNombre(request.RFC, 100, "RFC")
                    ?? ValidaId(request.IdColonia, "colonia");
        if (error != null) return BadRequest(error);

        var sinPermiso = await ExigirPermisoAsync(PermisosEmpresasEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await _service.ActualizarEmpresaMacAsync(id, request, ct), "Empresa actualizada.");
    }

    [HttpPost("empresas/{id:int}/inhabilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InhabilitarEmpresaMac([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosEmpresasBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusEmpresaMacAsync(id, StatusCancelado, ct),
            "Empresa inhabilitada.", "La empresa no existe.");
    }

    [HttpPost("empresas/{id:int}/habilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HabilitarEmpresaMac([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosEmpresasBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusEmpresaMacAsync(id, StatusActivo, ct),
            "Empresa habilitada.", "La empresa no existe.");
    }

    /// <summary>
    /// Alta de serie de folios. El folio inicial importa: de aqui sale el
    /// consecutivo con el que se factura.
    /// </summary>
    [HttpPost("empresas-series")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearEmpresaSerie([FromBody] CrearEmpresaSerieRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.Nombre, 400, "nombre de la serie")
                    ?? ValidaId(request.IdEmpresa, "empresa");
        if (error != null) return BadRequest(error);
        if (request.Folio < 0)
            return BadRequest(new { ok = false, message = "El folio no puede ser negativo." });

        var sinPermiso = await ExigirPermisoAsync(PermisosEmpresasCrear, ct);
        if (sinPermiso != null) return sinPermiso;

        return RespondeAlta(await _service.CrearEmpresaSerieAsync(request, IdUsuarioLegacy(), Equipo(), ct),
            "Serie dada de alta.");
    }

    [HttpPut("empresas-series/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarEmpresaSerie([FromRoute] int id, [FromBody] ActualizarEmpresaSerieRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        var error = ValidaNombre(request.Nombre, 400, "nombre de la serie")
                    ?? ValidaId(request.IdEmpresa, "empresa");
        if (error != null) return BadRequest(error);
        if (request.Folio < 0)
            return BadRequest(new { ok = false, message = "El folio no puede ser negativo." });

        var sinPermiso = await ExigirPermisoAsync(PermisosEmpresasEditar, ct);
        if (sinPermiso != null) return sinPermiso;

        return Responder(await _service.ActualizarEmpresaSerieAsync(id, request, IdUsuarioLegacy(), Equipo(), ct),
            "Serie actualizada.");
    }

    [HttpPost("empresas-series/{id:int}/inhabilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InhabilitarEmpresaSerie([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosEmpresasBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusEmpresaSerieAsync(id, StatusCancelado, IdUsuarioLegacy(), Equipo(), ct),
            "Serie inhabilitada.", "La serie no existe.");
    }

    [HttpPost("empresas-series/{id:int}/habilitar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HabilitarEmpresaSerie([FromRoute] int id, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosEmpresasBaja, ct);
        if (sinPermiso != null) return sinPermiso;
        return Responder(await _service.CambiarStatusEmpresaSerieAsync(id, StatusActivo, IdUsuarioLegacy(), Equipo(), ct),
            "Serie habilitada.", "La serie no existe.");
    }
}
