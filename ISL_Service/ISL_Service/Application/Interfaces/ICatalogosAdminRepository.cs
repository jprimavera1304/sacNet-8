using ISL_Service.Application.DTOs.CatalogosAdmin;
using ISL_Service.Application.DTOs.Empleados;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Escrituras de los catalogos de Mac31.
///
/// La firma que se repite: devuelve <see cref="ResultadoLegacy"/> (el par
/// result/mensaje del SP) y, en las bajas, <c>null</c> cuando el renglon no
/// existe para que el controlador pueda contestar 404 en vez de un exito falso.
/// <c>idUsuario</c> y <c>equipo</c> SIEMPRE salen del token, nunca del cuerpo.
/// </summary>
public interface ICatalogosAdminRepository
{
    // --- Agentes
    Task<(ResultadoLegacy Resultado, int Id)> InsertarAgenteAsync(CrearAgenteRequest r, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarAgenteAsync(int id, ActualizarAgenteRequest r, CancellationToken ct);
    Task<ResultadoLegacy?> CambiarStatusAgenteAsync(int id, int idStatus, CancellationToken ct);

    // --- Almacenes
    Task<(ResultadoLegacy Resultado, int Id)> InsertarAlmacenAsync(string nombre, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarAlmacenAsync(int id, string nombre, int idStatus, CancellationToken ct);
    Task<ResultadoLegacy?> CambiarStatusAlmacenAsync(int id, int idStatus, CancellationToken ct);

    // --- Bancos
    Task<(ResultadoLegacy Resultado, int Id)> InsertarBancoAsync(string nombre, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarBancoAsync(int id, string nombre, int idStatus, CancellationToken ct);
    Task<ResultadoLegacy?> CambiarStatusBancoAsync(int id, int idStatus, CancellationToken ct);

    // --- Grupos de categoria
    Task<(ResultadoLegacy Resultado, int Id)> InsertarGrupoCategoriaAsync(string nombre, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarGrupoCategoriaAsync(int id, string nombre, int idStatus, CancellationToken ct);
    Task<ResultadoLegacy?> CambiarStatusGrupoCategoriaAsync(int id, int idStatus, CancellationToken ct);

    // --- Unidades de medida
    Task<(ResultadoLegacy Resultado, int Id)> InsertarUnidadMedidaAsync(string nombre, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarUnidadMedidaAsync(int id, string nombre, int idStatus, CancellationToken ct);
    Task<ResultadoLegacy?> CambiarStatusUnidadMedidaAsync(int id, int idStatus, CancellationToken ct);

    // --- Tipos de gasto
    Task<(ResultadoLegacy Resultado, int Id)> InsertarTipoGastoAsync(string nombre, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarTipoGastoAsync(int id, string nombre, int idStatus, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy?> CambiarStatusTipoGastoAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct);

    // --- Tipos de domicilio
    Task<(ResultadoLegacy Resultado, int Id)> InsertarTipoDomicilioAsync(string nombre, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarTipoDomicilioAsync(int id, string nombre, int idStatus, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy?> CambiarStatusTipoDomicilioAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct);

    // --- Condiciones de credito
    Task<(ResultadoLegacy Resultado, int Id)> InsertarCondicionCreditoAsync(CrearCondicionCreditoRequest r, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarCondicionCreditoAsync(int id, ActualizarCondicionCreditoRequest r, CancellationToken ct);
    Task<ResultadoLegacy?> CambiarStatusCondicionCreditoAsync(int id, int idStatus, CancellationToken ct);

    // --- Documentos
    Task<(ResultadoLegacy Resultado, int Id)> InsertarDocumentoAsync(CrearDocumentoRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarDocumentoAsync(int id, ActualizarDocumentoRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy?> CambiarStatusDocumentoAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct);

    // --- Galleros
    Task<(ResultadoLegacy Resultado, int Id)> InsertarGalleroAsync(CrearGalleroRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarGalleroAsync(int id, ActualizarGalleroRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy?> CambiarStatusGalleroAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct);

    // --- Repartidores
    Task<(ResultadoLegacy Resultado, int Id)> InsertarRepartidorAsync(CrearRepartidorRequest r, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarRepartidorAsync(int id, ActualizarRepartidorRequest r, CancellationToken ct);
    Task<ResultadoLegacy?> CambiarStatusRepartidorAsync(int id, int idStatus, CancellationToken ct);

    // --- Tipos de recordatorio
    Task<(ResultadoLegacy Resultado, int Id)> InsertarTipoRecordatorioAsync(CrearTipoRecordatorioRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarTipoRecordatorioAsync(int id, ActualizarTipoRecordatorioRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy?> CambiarStatusTipoRecordatorioAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct);

    // --- Dias festivos
    Task<List<Dictionary<string, object?>>> ConsultarDiasFestivosAsync(DiasFestivosFiltro filtro, CancellationToken ct);
    Task<(ResultadoLegacy Resultado, int Id)> InsertarDiaFestivoAsync(DateTime dia, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarDiaFestivoAsync(int id, ActualizarDiaFestivoRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy?> CambiarStatusDiaFestivoAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct);

    // --- Territorial
    Task<ResultadoLegacy> InsertarCiudadAsync(CrearCiudadRequest r, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarCiudadAsync(string idCiudad, ActualizarCiudadRequest r, CancellationToken ct);
    Task<ResultadoLegacy> InsertarMunicipioAsync(CrearMunicipioRequest r, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarMunicipioAsync(string idMunicipio, ActualizarMunicipioRequest r, CancellationToken ct);
    Task<ResultadoLegacy> InsertarColoniaAsync(CrearColoniaRequest r, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarColoniaAsync(int idColonia, ActualizarColoniaRequest r, CancellationToken ct);
    Task<ResultadoLegacy> InsertarUnidadTerritorialAsync(UnidadTerritorialRequest r, int idUsuario, string equipo, CancellationToken ct);
    // Solo el id: el SP de legacy no recibe nada mas (ver la implementacion).
    Task<ResultadoLegacy> ActualizarUnidadTerritorialAsync(int id, CancellationToken ct);
    Task<ResultadoLegacy> InsertarCentroServicioAsync(CentroServicioRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarCentroServicioAsync(int id, CentroServicioRequest r, int idUsuario, string equipo, CancellationToken ct);

    // --- Categorias y marcas de Mac31
    Task<(ResultadoLegacy Resultado, int Id)> InsertarCategoriaMacAsync(CrearCategoriaMacRequest r, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarCategoriaMacAsync(int id, ActualizarCategoriaMacRequest r, CancellationToken ct);
    Task<ResultadoLegacy?> CambiarStatusCategoriaMacAsync(int id, int idStatus, CancellationToken ct);
    Task<(ResultadoLegacy Resultado, int Id)> InsertarMarcaAsync(CrearMarcaRequest r, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarMarcaAsync(int id, ActualizarMarcaRequest r, CancellationToken ct);
    Task<ResultadoLegacy?> CambiarStatusMarcaAsync(int id, int idStatus, CancellationToken ct);
    Task<ResultadoLegacy> RelacionarCategoriaMarcaAsync(RelacionCategoriaMarcaRequest r, CancellationToken ct);

    // --- Autos
    Task<(ResultadoLegacy Resultado, int Id)> InsertarAutoAsync(CrearAutoRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarAutoAsync(int id, ActualizarAutoRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy?> CambiarStatusAutoAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct);

    // --- Empresas de Mac31 y series
    Task<(ResultadoLegacy Resultado, int Id)> InsertarEmpresaMacAsync(CrearEmpresaMacRequest r, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarEmpresaMacAsync(int id, ActualizarEmpresaMacRequest r, CancellationToken ct);
    Task<ResultadoLegacy?> CambiarStatusEmpresaMacAsync(int id, int idStatus, CancellationToken ct);
    Task<(ResultadoLegacy Resultado, int Id)> InsertarEmpresaSerieAsync(CrearEmpresaSerieRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarEmpresaSerieAsync(int id, ActualizarEmpresaSerieRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy?> CambiarStatusEmpresaSerieAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct);

    /*
      --- LECTURAS DE LOS SIETE CATALOGOS EDITABLES

      Devuelven el renglon crudo (Dictionary) por la misma razon que
      ConsultarDiasFestivosAsync, que ya estaba aqui: sus sp_n_ terminan en
      EXEC (@Sql) y la lista de columnas cambia con @Formato, asi que un DTO
      cerrado por catalogo seria once clases que solo copian nombres y que
      tiran en silencio la columna que el SP agregue manana.
    */
    Task<List<Dictionary<string, object?>>> ConsultarAlmacenesAsync(AlmacenesFiltro f, CancellationToken ct);
    Task<List<Dictionary<string, object?>>> ConsultarAgentesAsync(AgentesFiltro f, CancellationToken ct);
    Task<List<Dictionary<string, object?>>> ConsultarTiposGastoAsync(TiposGastoFiltro f, CancellationToken ct);
    Task<List<Dictionary<string, object?>>> ConsultarAutosAsync(AutosFiltro f, CancellationToken ct);
    Task<List<Dictionary<string, object?>>> ConsultarGruposCategoriaAsync(GruposCategoriaFiltro f, CancellationToken ct);
    Task<List<Dictionary<string, object?>>> ConsultarCategoriasMacAsync(CategoriasMacFiltro f, CancellationToken ct);
    Task<List<Dictionary<string, object?>>> ConsultarTiposUsadoAsync(TiposUsadoFiltro f, CancellationToken ct);

    /// <summary>
    /// Las constantes de operacion de la empresa, LEIDAS CON UN SELECT.
    /// Ver el comentario del repositorio: sp_n_ConsultaConstantes no es una
    /// consulta, es el cierre del dia.
    /// </summary>
    Task<Dictionary<string, object?>?> ConsultarConstantesOperacionAsync(CancellationToken ct);
}
