using ISL_Service.Application.DTOs.CatalogosAdmin;
using ISL_Service.Application.DTOs.Empleados;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/// <summary>
/// Capa de aplicacion de las escrituras de catalogo.
///
/// Es delgada a proposito: la unica decision que toma es traducir el par
/// result/mensaje de legacy al DTO de salida. Las validaciones de forma
/// (requerido, longitud) viven en el controlador, como en el resto del
/// proyecto, y las reglas de negocio (el nombre repetido, el numero de agente
/// ocupado) viven en los sp_n_ y llegan aqui como el texto que el usuario tiene
/// que leer.
///
/// No hay traduccion de mensajes: se devuelve el de legacy tal cual. Si el SP
/// dice "EL NÚMERO DE AGENTE YA ESTA REGISTRADO", eso es lo que ve la persona,
/// con las mismas palabras que ve en Mac31. Dos textos distintos para la misma
/// regla hacen dudar de si son dos reglas.
/// </summary>
public class CatalogosAdminService : ICatalogosAdminService
{
    private readonly ICatalogosAdminRepository _repo;

    public CatalogosAdminService(ICatalogosAdminRepository repo)
    {
        _repo = repo;
    }

    // -----------------------------------------------------------------------
    // Traduccion
    // -----------------------------------------------------------------------

    private static CatalogoEscrituraResponse Traducir(ResultadoLegacy r, int id = 0)
        => new() { Guardo = r.Ok, Mensaje = r.Mensaje, Id = id };

    /// <summary>
    /// Para las bajas: null se queda null porque significa "ese renglon no
    /// existe", y eso es un 404. Lleva otro nombre a proposito: con el mismo
    /// nombre, el compilador elegia esta version tambien para las altas, que no
    /// devuelven nulo nunca.
    /// </summary>
    private static CatalogoEscrituraResponse? TraducirOpcional(ResultadoLegacy? r)
        => r is null ? null : Traducir(r);

    private static CatalogoEscrituraResponse Traducir((ResultadoLegacy Resultado, int Id) x)
        => Traducir(x.Resultado, x.Id);

    // -----------------------------------------------------------------------
    // Agentes
    // -----------------------------------------------------------------------

    public async Task<CatalogoEscrituraResponse> CrearAgenteAsync(CrearAgenteRequest r, CancellationToken ct)
        => Traducir(await _repo.InsertarAgenteAsync(r, ct));

    public async Task<CatalogoEscrituraResponse> ActualizarAgenteAsync(int id, ActualizarAgenteRequest r, CancellationToken ct)
        => Traducir(await _repo.ActualizarAgenteAsync(id, r, ct), id);

    public async Task<CatalogoEscrituraResponse?> CambiarStatusAgenteAsync(int id, int idStatus, CancellationToken ct)
        => TraducirOpcional(await _repo.CambiarStatusAgenteAsync(id, idStatus, ct));

    // -----------------------------------------------------------------------
    // Almacenes
    // -----------------------------------------------------------------------

    public async Task<CatalogoEscrituraResponse> CrearAlmacenAsync(CatalogoNombreRequest r, CancellationToken ct)
        => Traducir(await _repo.InsertarAlmacenAsync(r.Nombre, ct));

    public async Task<CatalogoEscrituraResponse> ActualizarAlmacenAsync(int id, CatalogoNombreUpdateRequest r, CancellationToken ct)
        => Traducir(await _repo.ActualizarAlmacenAsync(id, r.Nombre, r.IdStatus, ct), id);

    public async Task<CatalogoEscrituraResponse?> CambiarStatusAlmacenAsync(int id, int idStatus, CancellationToken ct)
        => TraducirOpcional(await _repo.CambiarStatusAlmacenAsync(id, idStatus, ct));

    // -----------------------------------------------------------------------
    // Bancos
    // -----------------------------------------------------------------------

    public async Task<CatalogoEscrituraResponse> CrearBancoAsync(CatalogoNombreRequest r, CancellationToken ct)
        => Traducir(await _repo.InsertarBancoAsync(r.Nombre, ct));

    public async Task<CatalogoEscrituraResponse> ActualizarBancoAsync(int id, CatalogoNombreUpdateRequest r, CancellationToken ct)
        => Traducir(await _repo.ActualizarBancoAsync(id, r.Nombre, r.IdStatus, ct), id);

    public async Task<CatalogoEscrituraResponse?> CambiarStatusBancoAsync(int id, int idStatus, CancellationToken ct)
        => TraducirOpcional(await _repo.CambiarStatusBancoAsync(id, idStatus, ct));

    // -----------------------------------------------------------------------
    // Grupos de categoria
    // -----------------------------------------------------------------------

    public async Task<CatalogoEscrituraResponse> CrearGrupoCategoriaAsync(CatalogoNombreRequest r, CancellationToken ct)
        => Traducir(await _repo.InsertarGrupoCategoriaAsync(r.Nombre, ct));

    public async Task<CatalogoEscrituraResponse> ActualizarGrupoCategoriaAsync(int id, CatalogoNombreUpdateRequest r, CancellationToken ct)
        => Traducir(await _repo.ActualizarGrupoCategoriaAsync(id, r.Nombre, r.IdStatus, ct), id);

    public async Task<CatalogoEscrituraResponse?> CambiarStatusGrupoCategoriaAsync(int id, int idStatus, CancellationToken ct)
        => TraducirOpcional(await _repo.CambiarStatusGrupoCategoriaAsync(id, idStatus, ct));

    // -----------------------------------------------------------------------
    // Unidades de medida
    // -----------------------------------------------------------------------

    public async Task<CatalogoEscrituraResponse> CrearUnidadMedidaAsync(CatalogoNombreRequest r, CancellationToken ct)
        => Traducir(await _repo.InsertarUnidadMedidaAsync(r.Nombre, ct));

    public async Task<CatalogoEscrituraResponse> ActualizarUnidadMedidaAsync(int id, CatalogoNombreUpdateRequest r, CancellationToken ct)
        => Traducir(await _repo.ActualizarUnidadMedidaAsync(id, r.Nombre, r.IdStatus, ct), id);

    public async Task<CatalogoEscrituraResponse?> CambiarStatusUnidadMedidaAsync(int id, int idStatus, CancellationToken ct)
        => TraducirOpcional(await _repo.CambiarStatusUnidadMedidaAsync(id, idStatus, ct));

    // -----------------------------------------------------------------------
    // Tipos de gasto
    // -----------------------------------------------------------------------

    public async Task<CatalogoEscrituraResponse> CrearTipoGastoAsync(CatalogoNombreRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.InsertarTipoGastoAsync(r.Nombre, idUsuario, equipo, ct));

    public async Task<CatalogoEscrituraResponse> ActualizarTipoGastoAsync(int id, CatalogoNombreUpdateRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.ActualizarTipoGastoAsync(id, r.Nombre, r.IdStatus, idUsuario, equipo, ct), id);

    public async Task<CatalogoEscrituraResponse?> CambiarStatusTipoGastoAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct)
        => TraducirOpcional(await _repo.CambiarStatusTipoGastoAsync(id, idStatus, idUsuario, equipo, ct));

    // -----------------------------------------------------------------------
    // Tipos de domicilio
    // -----------------------------------------------------------------------

    public async Task<CatalogoEscrituraResponse> CrearTipoDomicilioAsync(CatalogoNombreRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.InsertarTipoDomicilioAsync(r.Nombre, idUsuario, equipo, ct));

    public async Task<CatalogoEscrituraResponse> ActualizarTipoDomicilioAsync(int id, CatalogoNombreUpdateRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.ActualizarTipoDomicilioAsync(id, r.Nombre, r.IdStatus, idUsuario, equipo, ct), id);

    public async Task<CatalogoEscrituraResponse?> CambiarStatusTipoDomicilioAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct)
        => TraducirOpcional(await _repo.CambiarStatusTipoDomicilioAsync(id, idStatus, idUsuario, equipo, ct));

    // -----------------------------------------------------------------------
    // Condiciones de credito
    // -----------------------------------------------------------------------

    public async Task<CatalogoEscrituraResponse> CrearCondicionCreditoAsync(CrearCondicionCreditoRequest r, CancellationToken ct)
        => Traducir(await _repo.InsertarCondicionCreditoAsync(r, ct));

    public async Task<CatalogoEscrituraResponse> ActualizarCondicionCreditoAsync(int id, ActualizarCondicionCreditoRequest r, CancellationToken ct)
        => Traducir(await _repo.ActualizarCondicionCreditoAsync(id, r, ct), id);

    public async Task<CatalogoEscrituraResponse?> CambiarStatusCondicionCreditoAsync(int id, int idStatus, CancellationToken ct)
        => TraducirOpcional(await _repo.CambiarStatusCondicionCreditoAsync(id, idStatus, ct));

    // -----------------------------------------------------------------------
    // Documentos
    // -----------------------------------------------------------------------

    public async Task<CatalogoEscrituraResponse> CrearDocumentoAsync(CrearDocumentoRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.InsertarDocumentoAsync(r, idUsuario, equipo, ct));

    public async Task<CatalogoEscrituraResponse> ActualizarDocumentoAsync(int id, ActualizarDocumentoRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.ActualizarDocumentoAsync(id, r, idUsuario, equipo, ct), id);

    public async Task<CatalogoEscrituraResponse?> CambiarStatusDocumentoAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct)
        => TraducirOpcional(await _repo.CambiarStatusDocumentoAsync(id, idStatus, idUsuario, equipo, ct));

    // -----------------------------------------------------------------------
    // Galleros
    // -----------------------------------------------------------------------

    public async Task<CatalogoEscrituraResponse> CrearGalleroAsync(CrearGalleroRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.InsertarGalleroAsync(r, idUsuario, equipo, ct));

    public async Task<CatalogoEscrituraResponse> ActualizarGalleroAsync(int id, ActualizarGalleroRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.ActualizarGalleroAsync(id, r, idUsuario, equipo, ct), id);

    public async Task<CatalogoEscrituraResponse?> CambiarStatusGalleroAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct)
        => TraducirOpcional(await _repo.CambiarStatusGalleroAsync(id, idStatus, idUsuario, equipo, ct));

    // -----------------------------------------------------------------------
    // Repartidores
    // -----------------------------------------------------------------------

    public async Task<CatalogoEscrituraResponse> CrearRepartidorAsync(CrearRepartidorRequest r, CancellationToken ct)
        => Traducir(await _repo.InsertarRepartidorAsync(r, ct));

    public async Task<CatalogoEscrituraResponse> ActualizarRepartidorAsync(int id, ActualizarRepartidorRequest r, CancellationToken ct)
        => Traducir(await _repo.ActualizarRepartidorAsync(id, r, ct), id);

    public async Task<CatalogoEscrituraResponse?> CambiarStatusRepartidorAsync(int id, int idStatus, CancellationToken ct)
        => TraducirOpcional(await _repo.CambiarStatusRepartidorAsync(id, idStatus, ct));

    // -----------------------------------------------------------------------
    // Tipos de recordatorio
    // -----------------------------------------------------------------------

    public async Task<CatalogoEscrituraResponse> CrearTipoRecordatorioAsync(CrearTipoRecordatorioRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.InsertarTipoRecordatorioAsync(r, idUsuario, equipo, ct));

    public async Task<CatalogoEscrituraResponse> ActualizarTipoRecordatorioAsync(int id, ActualizarTipoRecordatorioRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.ActualizarTipoRecordatorioAsync(id, r, idUsuario, equipo, ct), id);

    public async Task<CatalogoEscrituraResponse?> CambiarStatusTipoRecordatorioAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct)
        => TraducirOpcional(await _repo.CambiarStatusTipoRecordatorioAsync(id, idStatus, idUsuario, equipo, ct));

    // -----------------------------------------------------------------------
    // Dias festivos
    // -----------------------------------------------------------------------

    public Task<List<Dictionary<string, object?>>> ConsultarDiasFestivosAsync(DiasFestivosFiltro filtro, CancellationToken ct)
        => _repo.ConsultarDiasFestivosAsync(filtro, ct);

    public async Task<CatalogoEscrituraResponse> CrearDiaFestivoAsync(CrearDiaFestivoRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.InsertarDiaFestivoAsync(r.DiaFestivo, idUsuario, equipo, ct));

    public async Task<CatalogoEscrituraResponse> ActualizarDiaFestivoAsync(int id, ActualizarDiaFestivoRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.ActualizarDiaFestivoAsync(id, r, idUsuario, equipo, ct), id);

    public async Task<CatalogoEscrituraResponse?> CambiarStatusDiaFestivoAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct)
        => TraducirOpcional(await _repo.CambiarStatusDiaFestivoAsync(id, idStatus, idUsuario, equipo, ct));

    // -----------------------------------------------------------------------
    // Territorial
    // -----------------------------------------------------------------------

    public async Task<CatalogoEscrituraResponse> CrearCiudadAsync(CrearCiudadRequest r, CancellationToken ct)
        => Traducir(await _repo.InsertarCiudadAsync(r, ct));

    public async Task<CatalogoEscrituraResponse> ActualizarCiudadAsync(string idCiudad, ActualizarCiudadRequest r, CancellationToken ct)
        => Traducir(await _repo.ActualizarCiudadAsync(idCiudad, r, ct));

    public async Task<CatalogoEscrituraResponse> CrearMunicipioAsync(CrearMunicipioRequest r, CancellationToken ct)
        => Traducir(await _repo.InsertarMunicipioAsync(r, ct));

    public async Task<CatalogoEscrituraResponse> ActualizarMunicipioAsync(string idMunicipio, ActualizarMunicipioRequest r, CancellationToken ct)
        => Traducir(await _repo.ActualizarMunicipioAsync(idMunicipio, r, ct));

    public async Task<CatalogoEscrituraResponse> CrearColoniaAsync(CrearColoniaRequest r, CancellationToken ct)
        => Traducir(await _repo.InsertarColoniaAsync(r, ct));

    public async Task<CatalogoEscrituraResponse> ActualizarColoniaAsync(int idColonia, ActualizarColoniaRequest r, CancellationToken ct)
        => Traducir(await _repo.ActualizarColoniaAsync(idColonia, r, ct), idColonia);

    public async Task<CatalogoEscrituraResponse> CrearUnidadTerritorialAsync(UnidadTerritorialRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.InsertarUnidadTerritorialAsync(r, idUsuario, equipo, ct));

    public async Task<CatalogoEscrituraResponse> ActualizarUnidadTerritorialAsync(int id, CancellationToken ct)
        => Traducir(await _repo.ActualizarUnidadTerritorialAsync(id, ct), id);

    public async Task<CatalogoEscrituraResponse> CrearCentroServicioAsync(CentroServicioRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.InsertarCentroServicioAsync(r, idUsuario, equipo, ct));

    public async Task<CatalogoEscrituraResponse> ActualizarCentroServicioAsync(int id, CentroServicioRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.ActualizarCentroServicioAsync(id, r, idUsuario, equipo, ct), id);

    // -----------------------------------------------------------------------
    // Categorias y marcas de Mac31
    // -----------------------------------------------------------------------

    public async Task<CatalogoEscrituraResponse> CrearCategoriaMacAsync(CrearCategoriaMacRequest r, CancellationToken ct)
        => Traducir(await _repo.InsertarCategoriaMacAsync(r, ct));

    public async Task<CatalogoEscrituraResponse> ActualizarCategoriaMacAsync(int id, ActualizarCategoriaMacRequest r, CancellationToken ct)
        => Traducir(await _repo.ActualizarCategoriaMacAsync(id, r, ct), id);

    public async Task<CatalogoEscrituraResponse?> CambiarStatusCategoriaMacAsync(int id, int idStatus, CancellationToken ct)
        => TraducirOpcional(await _repo.CambiarStatusCategoriaMacAsync(id, idStatus, ct));

    public async Task<CatalogoEscrituraResponse> CrearMarcaAsync(CrearMarcaRequest r, CancellationToken ct)
        => Traducir(await _repo.InsertarMarcaAsync(r, ct));

    public async Task<CatalogoEscrituraResponse> ActualizarMarcaAsync(int id, ActualizarMarcaRequest r, CancellationToken ct)
        => Traducir(await _repo.ActualizarMarcaAsync(id, r, ct), id);

    public async Task<CatalogoEscrituraResponse?> CambiarStatusMarcaAsync(int id, int idStatus, CancellationToken ct)
        => TraducirOpcional(await _repo.CambiarStatusMarcaAsync(id, idStatus, ct));

    public async Task<CatalogoEscrituraResponse> RelacionarCategoriaMarcaAsync(RelacionCategoriaMarcaRequest r, CancellationToken ct)
        => Traducir(await _repo.RelacionarCategoriaMarcaAsync(r, ct));

    // -----------------------------------------------------------------------
    // Autos
    // -----------------------------------------------------------------------

    public async Task<CatalogoEscrituraResponse> CrearAutoAsync(CrearAutoRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.InsertarAutoAsync(r, idUsuario, equipo, ct));

    public async Task<CatalogoEscrituraResponse> ActualizarAutoAsync(int id, ActualizarAutoRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.ActualizarAutoAsync(id, r, idUsuario, equipo, ct), id);

    public async Task<CatalogoEscrituraResponse?> CambiarStatusAutoAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct)
        => TraducirOpcional(await _repo.CambiarStatusAutoAsync(id, idStatus, idUsuario, equipo, ct));

    // -----------------------------------------------------------------------
    // Empresas de Mac31 y series
    // -----------------------------------------------------------------------

    public async Task<CatalogoEscrituraResponse> CrearEmpresaMacAsync(CrearEmpresaMacRequest r, CancellationToken ct)
        => Traducir(await _repo.InsertarEmpresaMacAsync(r, ct));

    public async Task<CatalogoEscrituraResponse> ActualizarEmpresaMacAsync(int id, ActualizarEmpresaMacRequest r, CancellationToken ct)
        => Traducir(await _repo.ActualizarEmpresaMacAsync(id, r, ct), id);

    public async Task<CatalogoEscrituraResponse?> CambiarStatusEmpresaMacAsync(int id, int idStatus, CancellationToken ct)
        => TraducirOpcional(await _repo.CambiarStatusEmpresaMacAsync(id, idStatus, ct));

    public async Task<CatalogoEscrituraResponse> CrearEmpresaSerieAsync(CrearEmpresaSerieRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.InsertarEmpresaSerieAsync(r, idUsuario, equipo, ct));

    public async Task<CatalogoEscrituraResponse> ActualizarEmpresaSerieAsync(int id, ActualizarEmpresaSerieRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.ActualizarEmpresaSerieAsync(id, r, idUsuario, equipo, ct), id);

    public async Task<CatalogoEscrituraResponse?> CambiarStatusEmpresaSerieAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct)
        => TraducirOpcional(await _repo.CambiarStatusEmpresaSerieAsync(id, idStatus, idUsuario, equipo, ct));
}
