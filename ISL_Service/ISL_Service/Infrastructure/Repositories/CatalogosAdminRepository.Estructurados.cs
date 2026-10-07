using ISL_Service.Application.DTOs.CatalogosAdmin;
using ISL_Service.Application.DTOs.Empleados;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// Los catalogos con varios campos: territorial (ciudades, municipios,
/// colonias, unidades territoriales, centros de servicio), categorias y marcas
/// de Mac31, autos, y las empresas de Mac31 con sus series de folios.
/// </summary>
public partial class CatalogosAdminRepository
{
    // -----------------------------------------------------------------------
    // Territorial
    //
    // NO HAY BAJA EN CIUDADES, MUNICIPIOS NI COLONIAS.
    // Sus tres tablas no tienen IDStatus y sus sp_n_Actualizar* tampoco reciben
    // el parametro. El DELETE de MacServicios2 sobre ellas llamaba al mismo
    // updateAsync con el cuerpo que trajera, asi que no daba de baja nada:
    // dejaba el renglon con el nombre vacio. No se expone.
    //
    // Las claves son TEXTO, no enteros: IDEstado y IDCiudad son varchar(2) y
    // IDMunicipio varchar(3) en [Catalogo Colonias]. Un "09" convertido a 9 deja
    // de encontrar su renglon.
    // -----------------------------------------------------------------------

    public Task<ResultadoLegacy> InsertarCiudadAsync(CrearCiudadRequest r, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_InsertarCiudad", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDEstado", Texto(r.IdEstado));
            cmd.Parameters.AddWithValue("@Ciudad", Texto(r.Ciudad));
        }, ct);

    public Task<ResultadoLegacy> ActualizarCiudadAsync(string idCiudad, ActualizarCiudadRequest r, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarCiudad", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCiudad", Texto(idCiudad));
            cmd.Parameters.AddWithValue("@IDEstado", Texto(r.IdEstado));
            cmd.Parameters.AddWithValue("@Ciudad", Texto(r.Ciudad));
        }, ct);

    public Task<ResultadoLegacy> InsertarMunicipioAsync(CrearMunicipioRequest r, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_InsertarMunicipio", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDEstado", Texto(r.IdEstado));
            cmd.Parameters.AddWithValue("@Municipio", Texto(r.Municipio));
        }, ct);

    public Task<ResultadoLegacy> ActualizarMunicipioAsync(string idMunicipio, ActualizarMunicipioRequest r, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarMunicipio", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDMunicipio", Texto(idMunicipio));
            cmd.Parameters.AddWithValue("@IDEstado", Texto(r.IdEstado));
            cmd.Parameters.AddWithValue("@Municipio", Texto(r.Municipio));
        }, ct);

    public Task<ResultadoLegacy> InsertarColoniaAsync(CrearColoniaRequest r, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_InsertarColonia", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDEstado", Texto(r.IdEstado));
            cmd.Parameters.AddWithValue("@IDCiudad", Texto(r.IdCiudad));
            cmd.Parameters.AddWithValue("@IDMunicipio", Texto(r.IdMunicipio));
            cmd.Parameters.AddWithValue("@Colonia", Texto(r.Colonia));
            cmd.Parameters.AddWithValue("@CodigoPostal", Texto(r.CodigoPostal));
        }, ct);

    public Task<ResultadoLegacy> ActualizarColoniaAsync(int idColonia, ActualizarColoniaRequest r, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarColonia", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDColonia", idColonia);
            cmd.Parameters.AddWithValue("@IDEstado", Texto(r.IdEstado));
            cmd.Parameters.AddWithValue("@IDCiudad", Texto(r.IdCiudad));
            cmd.Parameters.AddWithValue("@IDMunicipio", Texto(r.IdMunicipio));
            cmd.Parameters.AddWithValue("@Colonia", Texto(r.Colonia));
            cmd.Parameters.AddWithValue("@CodigoPostal", Texto(r.CodigoPostal));
        }, ct);

    /// <summary>
    /// Alta de unidad territorial. El SP no esta en Produccion_svr (es de otra
    /// instalacion), asi que esto falla aqui con el mensaje de SQL Server —
    /// igual que falla Legacy.
    /// </summary>
    public Task<ResultadoLegacy> InsertarUnidadTerritorialAsync(UnidadTerritorialRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_InsertarCatUnidadTerritorial", cmd =>
        {
            cmd.Parameters.AddWithValue("@Clave", Texto(r.Clave));
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    /// <summary>
    /// Modificacion de unidad territorial. <b>SOLO LLEVA EL ID</b>, y ahora la
    /// firma lo dice.
    /// </summary>
    /// <remarks>
    /// SE MANDA SOLO EL ID, A PROPOSITO. Legacy manda unicamente
    /// @IDUnidadTerritorial (CatUnidadTerritorialRepository.cs:174), aunque su
    /// propia cadena de depuracion de al lado arma cinco valores (id, status,
    /// clave, usuario, equipo): el que escribio el repositorio copio la cadena
    /// de debug y se le olvidaron cuatro AddWithValue. El SP no esta en ninguna
    /// de las dos bases, asi que no hay forma de saber cuantos parametros acepta
    /// de verdad; inventar los otros cuatro seria apostar a que la firma es la
    /// del comentario y romper la llamada en la instalacion donde hoy funciona.
    ///
    /// LO QUE SE ARREGLO: antes esta firma RECIBIA un
    /// <c>UnidadTerritorialRequest</c> entero y lo descartaba, y el controlador
    /// lo publicaba como cuerpo obligatorio del PUT. Mandar una clave nueva era
    /// un no-op silencioso: quien llamaba creia haber renombrado la unidad.
    /// Ahora el cuerpo no esta en el contrato, asi que nadie puede mandar un
    /// dato que no va a ningun sitio. Cuando aparezca el SP y se sepa su firma,
    /// se vuelve a agregar el cuerpo con los campos que de verdad viajen.
    /// </remarks>
    public Task<ResultadoLegacy> ActualizarUnidadTerritorialAsync(int id, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarCatUnidadTerritorial",
            cmd => cmd.Parameters.AddWithValue("@IDUnidadTerritorial", id), ct);

    /// <summary>Alta de centro de servicio. El SP tampoco esta en esta base.</summary>
    public Task<ResultadoLegacy> InsertarCentroServicioAsync(CentroServicioRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_InsertarCentroServicio", cmd =>
        {
            cmd.Parameters.AddWithValue("@NombreCentro", Texto(r.CentroServicio));
            cmd.Parameters.AddWithValue("@BaseCentro", Texto(r.BaseDatos));
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    public Task<ResultadoLegacy> ActualizarCentroServicioAsync(int id, CentroServicioRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarCentroServicio", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCentro", id);
            cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);
            cmd.Parameters.AddWithValue("@NombreCentro", Texto(r.CentroServicio));
            cmd.Parameters.AddWithValue("@BaseCentro", Texto(r.BaseDatos));
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    // -----------------------------------------------------------------------
    // Categorias y marcas de Mac31
    // -----------------------------------------------------------------------

    // La columna que guarda el grupo de la categoria se llama Tipo, no
    // IDGrupoCategoria: sp_n_ActualizarCategoria hace "SET Tipo = @IDGrupoCategoria".
    private static readonly CatalogoStatus StatusCategoriaMac = new(
        "Categorias", "IDCategoria", "@IDCategoria", "dbo.sp_n_ActualizarCategoria",
        new[] { ("@IDGrupoCategoria", "Tipo"), ("@Categoria", "Categoria") });

    public Task<(ResultadoLegacy, int)> InsertarCategoriaMacAsync(CrearCategoriaMacRequest r, CancellationToken ct)
        => InsertarConUltimoIdAsync("dbo.sp_n_InsertarCategoria", "Categorias", "IDCategoria", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDGrupoCategoria", r.IdGrupoCategoria);
            cmd.Parameters.AddWithValue("@Categoria", Texto(r.Categoria));
        }, ct);

    public Task<ResultadoLegacy> ActualizarCategoriaMacAsync(int id, ActualizarCategoriaMacRequest r, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarCategoria", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCategoria", id);
            cmd.Parameters.AddWithValue("@IDGrupoCategoria", r.IdGrupoCategoria);
            cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);
            cmd.Parameters.AddWithValue("@Categoria", Texto(r.Categoria));
        }, ct);

    public Task<ResultadoLegacy?> CambiarStatusCategoriaMacAsync(int id, int idStatus, CancellationToken ct)
        => CambiarStatusAsync(StatusCategoriaMac, id, idStatus, 0, string.Empty, ct);

    private static readonly CatalogoStatus StatusMarca = new(
        "Marcas", "IDMarca", "@IDMarca", "dbo.sp_n_ActualizarMarca",
        new[]
        {
            ("@Marca", "Marca"),
            ("@MarcaCorto", "MarcaCorto"),
            ("@ColumnaConcentrado", "ColumnaConcentrado"),
            ("@ColumnaInventario", "ColumnaInventario"),
            ("@ColumnaTemplate", "ColumnaTemplate"),
            ("@OrdenListaPrecios", "OrdenListaPrecios")
        });

    public Task<(ResultadoLegacy, int)> InsertarMarcaAsync(CrearMarcaRequest r, CancellationToken ct)
        => InsertarConUltimoIdAsync("dbo.sp_n_InsertarMarca", "Marcas", "IDMarca", cmd =>
        {
            cmd.Parameters.AddWithValue("@Marca", Texto(r.Marca));
            cmd.Parameters.AddWithValue("@MarcaCorto", Texto(r.MarcaCorto));
            cmd.Parameters.AddWithValue("@ColumnaConcentrado", r.ColumnaConcentrado);
            cmd.Parameters.AddWithValue("@ColumnaInventario", r.ColumnaInventario);
            cmd.Parameters.AddWithValue("@ColumnaTemplate", r.ColumnaTemplate);
            cmd.Parameters.AddWithValue("@OrdenListaPrecios", r.OrdenListaPrecios);
        }, ct);

    public Task<ResultadoLegacy> ActualizarMarcaAsync(int id, ActualizarMarcaRequest r, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarMarca", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDMarca", id);
            cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);
            cmd.Parameters.AddWithValue("@Marca", Texto(r.Marca));
            cmd.Parameters.AddWithValue("@MarcaCorto", Texto(r.MarcaCorto));
            cmd.Parameters.AddWithValue("@ColumnaConcentrado", r.ColumnaConcentrado);
            cmd.Parameters.AddWithValue("@ColumnaInventario", r.ColumnaInventario);
            cmd.Parameters.AddWithValue("@ColumnaTemplate", r.ColumnaTemplate);
            cmd.Parameters.AddWithValue("@OrdenListaPrecios", r.OrdenListaPrecios);
        }, ct);

    public Task<ResultadoLegacy?> CambiarStatusMarcaAsync(int id, int idStatus, CancellationToken ct)
        => CambiarStatusAsync(StatusMarca, id, idStatus, 0, string.Empty, ct);

    /// <summary>
    /// Prende o apaga la relacion categoria - marca. El SP es un interruptor:
    /// con @Marcado = 1 inserta el par si falta, con 0 lo borra. Es el unico
    /// borrado fisico de este modulo y lo es porque la tabla
    /// [Categorias Marcas] solo tiene las dos llaves: no hay nada que dar de
    /// baja.
    /// </summary>
    public Task<ResultadoLegacy> RelacionarCategoriaMarcaAsync(RelacionCategoriaMarcaRequest r, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_InsertarGrupoCategoriasMarcasRelacion", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCategoria", r.IdCategoria);
            cmd.Parameters.AddWithValue("@IDMarca", r.IdMarca);
            cmd.Parameters.AddWithValue("@Marcado", r.Marcado ? 1 : 0);
        }, ct);

    // -----------------------------------------------------------------------
    // Autos
    // -----------------------------------------------------------------------

    private static readonly CatalogoStatus StatusAuto = new(
        "[Catalogo Autos]", "IDAuto", "@IDAuto", "dbo.sp_n_ActualizarAuto",
        new[]
        {
            ("@Nombre", "Nombre"),
            ("@Modelo", "Modelo"),
            ("@Placas", "Placas"),
            ("@Verifica", "Verifica"),
            ("@VerificacionFecha1erSemestre", "VerificacionFecha1erSemestre"),
            ("@VerificacionFecha2doSemestre", "VerificacionFecha2doSemestre"),
            ("@TieneSeguro", "TieneSeguro"),
            ("@SeguroFechaInicio", "SeguroFechaInicio"),
            ("@SeguroFechaTermino", "SeguroFechaTermino"),
            ("@NumeroSerie", "NumeroSerie"),
            ("@NumeroMotor", "NumeroMotor"),
            ("@NombreTarjetaCirculacion", "NombreTarjetaCirculacion"),
            ("@NombreAseguradora", "NombreAseguradora"),
            ("@NumeroPoliza", "NumeroPoliza"),
            ("@MontoPoliza", "MontoPoliza"),
            ("@KmPorLitroMinimo", "KmPorLitroMinimo"),
            ("@KmPorLitroMaximo", "KmPorLitroMaximo"),
            ("@TotalTanqueLleno", "TotalTanqueLleno")
        },
        PideUsuario: true, PideEquipo: true);

    public Task<(ResultadoLegacy, int)> InsertarAutoAsync(CrearAutoRequest r, int idUsuario, string equipo, CancellationToken ct)
        => InsertarConUltimoIdAsync("dbo.sp_n_InsertarAuto", "[Catalogo Autos]", "IDAuto",
            cmd => ParametrosAuto(cmd, r, idUsuario, equipo), ct);

    public Task<ResultadoLegacy> ActualizarAutoAsync(int id, ActualizarAutoRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarAuto", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDAuto", id);
            cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);
            ParametrosAuto(cmd, r, idUsuario, equipo);
        }, ct);

    public Task<ResultadoLegacy?> CambiarStatusAutoAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct)
        => CambiarStatusAsync(StatusAuto, id, idStatus, idUsuario, equipo, ct);

    /// <summary>Los 20 parametros que comparten el alta y la modificacion del auto.</summary>
    private static void ParametrosAuto(
        Microsoft.Data.SqlClient.SqlCommand cmd, CrearAutoRequest r, int idUsuario, string equipo)
    {
        cmd.Parameters.AddWithValue("@Nombre", Texto(r.Nombre));
        cmd.Parameters.AddWithValue("@Modelo", r.Modelo);
        cmd.Parameters.AddWithValue("@Placas", Texto(r.Placas));
        cmd.Parameters.AddWithValue("@Verifica", r.Verifica ? 1 : 0);
        cmd.Parameters.AddWithValue("@VerificacionFecha1erSemestre", AFechaLegacy(r.VerificacionFecha1erSemestre));
        cmd.Parameters.AddWithValue("@VerificacionFecha2doSemestre", AFechaLegacy(r.VerificacionFecha2doSemestre));
        cmd.Parameters.AddWithValue("@TieneSeguro", r.TieneSeguro ? 1 : 0);
        cmd.Parameters.AddWithValue("@SeguroFechaInicio", AFechaLegacy(r.SeguroFechaInicio));
        cmd.Parameters.AddWithValue("@SeguroFechaTermino", AFechaLegacy(r.SeguroFechaTermino));
        cmd.Parameters.AddWithValue("@NumeroSerie", Texto(r.NumeroSerie));
        cmd.Parameters.AddWithValue("@NumeroMotor", Texto(r.NumeroMotor));
        cmd.Parameters.AddWithValue("@NombreTarjetaCirculacion", Texto(r.NombreTarjetaCirculacion));
        cmd.Parameters.AddWithValue("@NombreAseguradora", Texto(r.NombreAseguradora));
        cmd.Parameters.AddWithValue("@NumeroPoliza", Texto(r.NumeroPoliza));
        cmd.Parameters.AddWithValue("@MontoPoliza", r.MontoPoliza);
        cmd.Parameters.AddWithValue("@KmPorLitroMinimo", r.KmPorLitroMinimo);
        cmd.Parameters.AddWithValue("@KmPorLitroMaximo", r.KmPorLitroMaximo);
        cmd.Parameters.AddWithValue("@TotalTanqueLleno", r.TotalTanqueLleno);
        cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
        cmd.Parameters.AddWithValue("@Equipo", equipo);
    }

    // -----------------------------------------------------------------------
    // Empresas de Mac31 y sus series
    // -----------------------------------------------------------------------

    private static readonly CatalogoStatus StatusEmpresaMac = new(
        "Empresas", "IDEmpresa", "@IDEmpresa", "dbo.sp_n_ActualizarEmpresa",
        new[]
        {
            ("@IDColonia", "IDColonia"),
            ("@NombreCorto", "Nombre Corto"),
            ("@RazonSocial", "Razon Social"),
            ("@Descripcion1", "Descripcion1"),
            ("@Descripcion2", "Descripcion2"),
            ("@RFC", "RFC"),
            ("@Calle", "Calle"),
            ("@NumeroExterior", "NumeroExterior"),
            ("@NumeroInterior", "NumeroInterior"),
            ("@Telefono1", "Telefono 1"),
            ("@Telefono2", "Telefono 2"),
            ("@Email", "Email"),
            ("@IDFacturama", "IDFacturama"),
            ("@IDBanco", "IDBanco"),
            ("@NoCuenta", "NoCuenta"),
            ("@Clabe", "Clabe"),
            ("@IDFERegimenFiscal", "IDFERegimenFiscal")
        });

    public Task<(ResultadoLegacy, int)> InsertarEmpresaMacAsync(CrearEmpresaMacRequest r, CancellationToken ct)
        => InsertarConUltimoIdAsync("dbo.sp_n_InsertarEmpresa", "Empresas", "IDEmpresa", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDColonia", r.IdColonia);
            cmd.Parameters.AddWithValue("@NombreCorto", Texto(r.NombreCorto));
            cmd.Parameters.AddWithValue("@RazonSocial", Texto(r.RazonSocial));
            cmd.Parameters.AddWithValue("@Descripcion1", Texto(r.Descripcion1));
            cmd.Parameters.AddWithValue("@Descripcion2", Texto(r.Descripcion2));
            cmd.Parameters.AddWithValue("@RFC", Texto(r.RFC));
            cmd.Parameters.AddWithValue("@Calle", Texto(r.Calle));
            cmd.Parameters.AddWithValue("@NumeroExterior", Texto(r.NumeroExterior));
            cmd.Parameters.AddWithValue("@NumeroInterior", Texto(r.NumeroInterior));
            cmd.Parameters.AddWithValue("@Telefono1", Texto(r.Telefono1));
            cmd.Parameters.AddWithValue("@Telefono2", Texto(r.Telefono2));
            cmd.Parameters.AddWithValue("@Email", Texto(r.Email));
            cmd.Parameters.AddWithValue("@IDFacturama", Texto(r.IdFacturama));
            cmd.Parameters.AddWithValue("@IDBanco", r.IdBanco);
            cmd.Parameters.AddWithValue("@NoCuenta", Texto(r.NoCuenta));
            cmd.Parameters.AddWithValue("@Clabe", Texto(r.Clabe));
            cmd.Parameters.AddWithValue("@IDFERegimenFiscal", r.IdFERegimenFiscal);
            cmd.Parameters.AddWithValue("@EsCentroServicio", r.EsCentroServicio ? 1 : 0);
        }, ct);

    public Task<ResultadoLegacy> ActualizarEmpresaMacAsync(int id, ActualizarEmpresaMacRequest r, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarEmpresa", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDEmpresa", id);
            cmd.Parameters.AddWithValue("@IDColonia", r.IdColonia);
            cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);
            cmd.Parameters.AddWithValue("@NombreCorto", Texto(r.NombreCorto));
            cmd.Parameters.AddWithValue("@RazonSocial", Texto(r.RazonSocial));
            cmd.Parameters.AddWithValue("@Descripcion1", Texto(r.Descripcion1));
            cmd.Parameters.AddWithValue("@Descripcion2", Texto(r.Descripcion2));
            cmd.Parameters.AddWithValue("@RFC", Texto(r.RFC));
            cmd.Parameters.AddWithValue("@Calle", Texto(r.Calle));
            cmd.Parameters.AddWithValue("@NumeroExterior", Texto(r.NumeroExterior));
            cmd.Parameters.AddWithValue("@NumeroInterior", Texto(r.NumeroInterior));
            cmd.Parameters.AddWithValue("@Telefono1", Texto(r.Telefono1));
            cmd.Parameters.AddWithValue("@Telefono2", Texto(r.Telefono2));
            cmd.Parameters.AddWithValue("@Email", Texto(r.Email));
            cmd.Parameters.AddWithValue("@IDFacturama", Texto(r.IdFacturama));
            cmd.Parameters.AddWithValue("@IDBanco", r.IdBanco);
            cmd.Parameters.AddWithValue("@NoCuenta", Texto(r.NoCuenta));
            cmd.Parameters.AddWithValue("@Clabe", Texto(r.Clabe));
            cmd.Parameters.AddWithValue("@IDFERegimenFiscal", r.IdFERegimenFiscal);
        }, ct);

    public Task<ResultadoLegacy?> CambiarStatusEmpresaMacAsync(int id, int idStatus, CancellationToken ct)
        => CambiarStatusAsync(StatusEmpresaMac, id, idStatus, 0, string.Empty, ct);

    private static readonly CatalogoStatus StatusEmpresaSerie = new(
        "EmpresasSeries", "IDEmpresaSerie", "@IDEmpresaSerie", "dbo.sp_n_ActualizarEmpresaSerie",
        new[]
        {
            ("@IDEmpresa", "IDEmpresa"),
            ("@Nombre", "Nombre"),
            ("@Descripcion", "Descripcion"),
            ("@Folio", "Folio")
        },
        PideUsuario: true, PideEquipo: true);

    public Task<(ResultadoLegacy, int)> InsertarEmpresaSerieAsync(CrearEmpresaSerieRequest r, int idUsuario, string equipo, CancellationToken ct)
        => InsertarConUltimoIdAsync("dbo.sp_n_InsertarEmpresaSerie", "EmpresasSeries", "IDEmpresaSerie", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDEmpresa", r.IdEmpresa);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Nombre", Texto(r.Nombre));
            cmd.Parameters.AddWithValue("@Descripcion", Texto(r.Descripcion));
            cmd.Parameters.AddWithValue("@Folio", r.Folio);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    public Task<ResultadoLegacy> ActualizarEmpresaSerieAsync(int id, ActualizarEmpresaSerieRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarEmpresaSerie", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDEmpresaSerie", id);
            cmd.Parameters.AddWithValue("@IDEmpresa", r.IdEmpresa);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);
            cmd.Parameters.AddWithValue("@Nombre", Texto(r.Nombre));
            cmd.Parameters.AddWithValue("@Descripcion", Texto(r.Descripcion));
            cmd.Parameters.AddWithValue("@Folio", r.Folio);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    public Task<ResultadoLegacy?> CambiarStatusEmpresaSerieAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct)
        => CambiarStatusAsync(StatusEmpresaSerie, id, idStatus, idUsuario, equipo, ct);
}
