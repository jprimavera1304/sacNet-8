using ISL_Service.Application.DTOs.CatalogosAdmin;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Escrituras de los catalogos de Mac31, vistas desde el controlador.
///
/// Todo devuelve <see cref="CatalogoEscrituraResponse"/>, que es el par
/// result/mensaje de legacy ya traducido: <c>Guardo</c> dice si la base acepto y
/// <c>Mensaje</c> trae el texto literal del rechazo ("EL AGENTE YA ESTA
/// REGISTRADO"), que es lo que la pantalla tiene que poder mostrar.
///
/// Los metodos de baja/alta de estatus devuelven <c>null</c> cuando el renglon
/// no existe: eso es un 404, no un exito vacio.
///
/// <c>idUsuario</c> y <c>equipo</c> los recibe desde el controlador, que los saca
/// del token. Nunca del cuerpo.
/// </summary>
public interface ICatalogosAdminService
{
    // Agentes
    Task<CatalogoEscrituraResponse> CrearAgenteAsync(CrearAgenteRequest r, CancellationToken ct);
    Task<CatalogoEscrituraResponse> ActualizarAgenteAsync(int id, ActualizarAgenteRequest r, CancellationToken ct);
    Task<CatalogoEscrituraResponse?> CambiarStatusAgenteAsync(int id, int idStatus, CancellationToken ct);

    // Almacenes
    Task<CatalogoEscrituraResponse> CrearAlmacenAsync(CatalogoNombreRequest r, CancellationToken ct);
    Task<CatalogoEscrituraResponse> ActualizarAlmacenAsync(int id, CatalogoNombreUpdateRequest r, CancellationToken ct);
    Task<CatalogoEscrituraResponse?> CambiarStatusAlmacenAsync(int id, int idStatus, CancellationToken ct);

    // Bancos
    Task<CatalogoEscrituraResponse> CrearBancoAsync(CatalogoNombreRequest r, CancellationToken ct);
    Task<CatalogoEscrituraResponse> ActualizarBancoAsync(int id, CatalogoNombreUpdateRequest r, CancellationToken ct);
    Task<CatalogoEscrituraResponse?> CambiarStatusBancoAsync(int id, int idStatus, CancellationToken ct);

    // Grupos de categoria
    Task<CatalogoEscrituraResponse> CrearGrupoCategoriaAsync(CatalogoNombreRequest r, CancellationToken ct);
    Task<CatalogoEscrituraResponse> ActualizarGrupoCategoriaAsync(int id, CatalogoNombreUpdateRequest r, CancellationToken ct);
    Task<CatalogoEscrituraResponse?> CambiarStatusGrupoCategoriaAsync(int id, int idStatus, CancellationToken ct);

    // Unidades de medida
    Task<CatalogoEscrituraResponse> CrearUnidadMedidaAsync(CatalogoNombreRequest r, CancellationToken ct);
    Task<CatalogoEscrituraResponse> ActualizarUnidadMedidaAsync(int id, CatalogoNombreUpdateRequest r, CancellationToken ct);
    Task<CatalogoEscrituraResponse?> CambiarStatusUnidadMedidaAsync(int id, int idStatus, CancellationToken ct);

    // Tipos de gasto
    Task<CatalogoEscrituraResponse> CrearTipoGastoAsync(CatalogoNombreRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<CatalogoEscrituraResponse> ActualizarTipoGastoAsync(int id, CatalogoNombreUpdateRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<CatalogoEscrituraResponse?> CambiarStatusTipoGastoAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct);

    // Tipos de domicilio
    Task<CatalogoEscrituraResponse> CrearTipoDomicilioAsync(CatalogoNombreRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<CatalogoEscrituraResponse> ActualizarTipoDomicilioAsync(int id, CatalogoNombreUpdateRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<CatalogoEscrituraResponse?> CambiarStatusTipoDomicilioAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct);

    // Condiciones de credito
    Task<CatalogoEscrituraResponse> CrearCondicionCreditoAsync(CrearCondicionCreditoRequest r, CancellationToken ct);
    Task<CatalogoEscrituraResponse> ActualizarCondicionCreditoAsync(int id, ActualizarCondicionCreditoRequest r, CancellationToken ct);
    Task<CatalogoEscrituraResponse?> CambiarStatusCondicionCreditoAsync(int id, int idStatus, CancellationToken ct);

    // Documentos
    Task<CatalogoEscrituraResponse> CrearDocumentoAsync(CrearDocumentoRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<CatalogoEscrituraResponse> ActualizarDocumentoAsync(int id, ActualizarDocumentoRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<CatalogoEscrituraResponse?> CambiarStatusDocumentoAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct);

    // Galleros
    Task<CatalogoEscrituraResponse> CrearGalleroAsync(CrearGalleroRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<CatalogoEscrituraResponse> ActualizarGalleroAsync(int id, ActualizarGalleroRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<CatalogoEscrituraResponse?> CambiarStatusGalleroAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct);

    // Repartidores
    Task<CatalogoEscrituraResponse> CrearRepartidorAsync(CrearRepartidorRequest r, CancellationToken ct);
    Task<CatalogoEscrituraResponse> ActualizarRepartidorAsync(int id, ActualizarRepartidorRequest r, CancellationToken ct);
    Task<CatalogoEscrituraResponse?> CambiarStatusRepartidorAsync(int id, int idStatus, CancellationToken ct);

    // Tipos de recordatorio
    Task<CatalogoEscrituraResponse> CrearTipoRecordatorioAsync(CrearTipoRecordatorioRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<CatalogoEscrituraResponse> ActualizarTipoRecordatorioAsync(int id, ActualizarTipoRecordatorioRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<CatalogoEscrituraResponse?> CambiarStatusTipoRecordatorioAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct);

    // Dias festivos
    Task<List<Dictionary<string, object?>>> ConsultarDiasFestivosAsync(DiasFestivosFiltro filtro, CancellationToken ct);
    Task<CatalogoEscrituraResponse> CrearDiaFestivoAsync(CrearDiaFestivoRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<CatalogoEscrituraResponse> ActualizarDiaFestivoAsync(int id, ActualizarDiaFestivoRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<CatalogoEscrituraResponse?> CambiarStatusDiaFestivoAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct);

    // Territorial
    Task<CatalogoEscrituraResponse> CrearCiudadAsync(CrearCiudadRequest r, CancellationToken ct);
    Task<CatalogoEscrituraResponse> ActualizarCiudadAsync(string idCiudad, ActualizarCiudadRequest r, CancellationToken ct);
    Task<CatalogoEscrituraResponse> CrearMunicipioAsync(CrearMunicipioRequest r, CancellationToken ct);
    Task<CatalogoEscrituraResponse> ActualizarMunicipioAsync(string idMunicipio, ActualizarMunicipioRequest r, CancellationToken ct);
    Task<CatalogoEscrituraResponse> CrearColoniaAsync(CrearColoniaRequest r, CancellationToken ct);
    Task<CatalogoEscrituraResponse> ActualizarColoniaAsync(int idColonia, ActualizarColoniaRequest r, CancellationToken ct);
    Task<CatalogoEscrituraResponse> CrearUnidadTerritorialAsync(UnidadTerritorialRequest r, int idUsuario, string equipo, CancellationToken ct);
    // Solo el id: el SP de legacy no recibe nada mas (ver la implementacion del repositorio).
    Task<CatalogoEscrituraResponse> ActualizarUnidadTerritorialAsync(int id, CancellationToken ct);
    Task<CatalogoEscrituraResponse> CrearCentroServicioAsync(CentroServicioRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<CatalogoEscrituraResponse> ActualizarCentroServicioAsync(int id, CentroServicioRequest r, int idUsuario, string equipo, CancellationToken ct);

    // Categorias y marcas de Mac31
    Task<CatalogoEscrituraResponse> CrearCategoriaMacAsync(CrearCategoriaMacRequest r, CancellationToken ct);
    Task<CatalogoEscrituraResponse> ActualizarCategoriaMacAsync(int id, ActualizarCategoriaMacRequest r, CancellationToken ct);
    Task<CatalogoEscrituraResponse?> CambiarStatusCategoriaMacAsync(int id, int idStatus, CancellationToken ct);
    Task<CatalogoEscrituraResponse> CrearMarcaAsync(CrearMarcaRequest r, CancellationToken ct);
    Task<CatalogoEscrituraResponse> ActualizarMarcaAsync(int id, ActualizarMarcaRequest r, CancellationToken ct);
    Task<CatalogoEscrituraResponse?> CambiarStatusMarcaAsync(int id, int idStatus, CancellationToken ct);
    Task<CatalogoEscrituraResponse> RelacionarCategoriaMarcaAsync(RelacionCategoriaMarcaRequest r, CancellationToken ct);

    // Autos
    Task<CatalogoEscrituraResponse> CrearAutoAsync(CrearAutoRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<CatalogoEscrituraResponse> ActualizarAutoAsync(int id, ActualizarAutoRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<CatalogoEscrituraResponse?> CambiarStatusAutoAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct);

    // Empresas de Mac31 y series
    Task<CatalogoEscrituraResponse> CrearEmpresaMacAsync(CrearEmpresaMacRequest r, CancellationToken ct);
    Task<CatalogoEscrituraResponse> ActualizarEmpresaMacAsync(int id, ActualizarEmpresaMacRequest r, CancellationToken ct);
    Task<CatalogoEscrituraResponse?> CambiarStatusEmpresaMacAsync(int id, int idStatus, CancellationToken ct);
    Task<CatalogoEscrituraResponse> CrearEmpresaSerieAsync(CrearEmpresaSerieRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<CatalogoEscrituraResponse> ActualizarEmpresaSerieAsync(int id, ActualizarEmpresaSerieRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<CatalogoEscrituraResponse?> CambiarStatusEmpresaSerieAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct);
}
