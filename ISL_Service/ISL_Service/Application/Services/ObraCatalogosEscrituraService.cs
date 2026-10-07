using ISL_Service.Application.DTOs.EscriturasLegacy;
using ISL_Service.Application.DTOs.ObraCatalogos;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/// <summary>
/// Reglas de las altas y modificaciones de los catalogos de obra (MC).
/// Las validaciones de forma estan en el controlador; aqui esta lo que el
/// negocio exige y la traduccion del renglon (result, mensaje) de los SP.
/// </summary>
public class ObraCatalogosEscrituraService : IObraCatalogosEscrituraService
{
    private readonly IObraCatalogosEscrituraRepository _repo;

    /// <summary>
    /// Los largos reales de las columnas, leidos de sys.parameters de
    /// Produccion_svr. Importan: estos SP hacen el INSERT sin truncar y un
    /// nombre mas largo que la columna revienta con un error de SQL que no dice
    /// que campo fue.
    /// </summary>
    private const int LargoNombre = 400;
    private const int LargoRfc = 100;
    private const int LargoObservaciones = 4000;

    public ObraCatalogosEscrituraService(IObraCatalogosEscrituraRepository repo)
    {
        _repo = repo;
    }

    // ------------------------------------------------------------------
    // Los cuatro catalogos de un solo nombre
    // ------------------------------------------------------------------

    public async Task<ResultadoEscrituraDto> CrearTipoContratacionAsync(CrearObraCatalogoSimpleRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        var nombre = EscrituraLegacyReglas.ExigirTexto(request.Nombre, "nombre", LargoNombre);
        return EscrituraLegacyReglas.Traducir(
            await _repo.InsertarTipoContratacionAsync(nombre, idUsuario, equipo, ct),
            "dar de alta el tipo de contratacion");
    }

    public async Task<ResultadoEscrituraDto> ActualizarTipoContratacionAsync(int id, ActualizarObraCatalogoSimpleRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(id, "idTipoContratacion");
        var nombre = EscrituraLegacyReglas.ExigirTexto(request.Nombre, "nombre", LargoNombre);
        var status = EscrituraLegacyReglas.ExigirStatus(request.IdStatus);
        return EscrituraLegacyReglas.Traducir(
            await _repo.ActualizarTipoContratacionAsync(id, nombre, status, idUsuario, equipo, ct),
            "actualizar el tipo de contratacion");
    }

    public async Task<ResultadoEscrituraDto> CrearTipoDocumentoAsync(CrearObraCatalogoSimpleRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        var nombre = EscrituraLegacyReglas.ExigirTexto(request.Nombre, "nombre", LargoNombre);
        return EscrituraLegacyReglas.Traducir(
            await _repo.InsertarTipoDocumentoAsync(nombre, idUsuario, equipo, ct),
            "dar de alta el tipo de documento");
    }

    public async Task<ResultadoEscrituraDto> ActualizarTipoDocumentoAsync(int id, ActualizarObraCatalogoSimpleRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(id, "idTipoDocumento");
        var nombre = EscrituraLegacyReglas.ExigirTexto(request.Nombre, "nombre", LargoNombre);
        var status = EscrituraLegacyReglas.ExigirStatus(request.IdStatus);
        return EscrituraLegacyReglas.Traducir(
            await _repo.ActualizarTipoDocumentoAsync(id, nombre, status, idUsuario, equipo, ct),
            "actualizar el tipo de documento");
    }

    public async Task<ResultadoEscrituraDto> CrearTipoObraAsync(CrearObraCatalogoSimpleRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        var nombre = EscrituraLegacyReglas.ExigirTexto(request.Nombre, "nombre", LargoNombre);
        return EscrituraLegacyReglas.Traducir(
            await _repo.InsertarTipoObraAsync(nombre, idUsuario, equipo, ct),
            "dar de alta el tipo de obra");
    }

    public async Task<ResultadoEscrituraDto> ActualizarTipoObraAsync(int id, ActualizarObraCatalogoSimpleRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(id, "idTipoObra");
        var nombre = EscrituraLegacyReglas.ExigirTexto(request.Nombre, "nombre", LargoNombre);
        var status = EscrituraLegacyReglas.ExigirStatus(request.IdStatus);
        return EscrituraLegacyReglas.Traducir(
            await _repo.ActualizarTipoObraAsync(id, nombre, status, idUsuario, equipo, ct),
            "actualizar el tipo de obra");
    }

    public async Task<ResultadoEscrituraDto> CrearTipoRecursoAsync(CrearObraCatalogoSimpleRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        var nombre = EscrituraLegacyReglas.ExigirTexto(request.Nombre, "nombre", LargoNombre);
        return EscrituraLegacyReglas.Traducir(
            await _repo.InsertarTipoRecursoAsync(nombre, idUsuario, equipo, ct),
            "dar de alta el tipo de recurso");
    }

    public async Task<ResultadoEscrituraDto> ActualizarTipoRecursoAsync(int id, ActualizarObraCatalogoSimpleRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(id, "idTipoRecurso");
        var nombre = EscrituraLegacyReglas.ExigirTexto(request.Nombre, "nombre", LargoNombre);
        var status = EscrituraLegacyReglas.ExigirStatus(request.IdStatus);
        return EscrituraLegacyReglas.Traducir(
            await _repo.ActualizarTipoRecursoAsync(id, nombre, status, idUsuario, equipo, ct),
            "actualizar el tipo de recurso");
    }

    // ------------------------------------------------------------------
    // Puestos
    // ------------------------------------------------------------------

    public async Task<ResultadoEscrituraDto> CrearPuestoAsync(CrearObraPuestoRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        request.Puesto = EscrituraLegacyReglas.ExigirTexto(request.Puesto, "puesto", LargoNombre);
        return EscrituraLegacyReglas.Traducir(
            await _repo.InsertarPuestoAsync(request, idUsuario, equipo, ct),
            "dar de alta el puesto");
    }

    public async Task<ResultadoEscrituraDto> ActualizarPuestoAsync(int id, ActualizarObraPuestoRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(id, "idPuesto");
        request.Puesto = EscrituraLegacyReglas.ExigirTexto(request.Puesto, "puesto", LargoNombre);
        request.IdStatus = EscrituraLegacyReglas.ExigirStatus(request.IdStatus);
        return EscrituraLegacyReglas.Traducir(
            await _repo.ActualizarPuestoAsync(id, request, idUsuario, equipo, ct),
            "actualizar el puesto");
    }

    // ------------------------------------------------------------------
    // Residentes
    // ------------------------------------------------------------------

    public async Task<ResultadoEscrituraDto> CrearResidenteAsync(CrearObraResidenteRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(request.IdPuesto, "idPuesto");
        request.Residente = EscrituraLegacyReglas.ExigirTexto(request.Residente, "residente", LargoNombre);
        ExigirAlMenosUnaFuncion(request.EsSupervision, request.EsObra);
        return EscrituraLegacyReglas.Traducir(
            await _repo.InsertarResidenteAsync(request, idUsuario, equipo, ct),
            "dar de alta el residente");
    }

    public async Task<ResultadoEscrituraDto> ActualizarResidenteAsync(int id, ActualizarObraResidenteRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(id, "idResidente");
        EscrituraLegacyReglas.ExigirId(request.IdPuesto, "idPuesto");
        request.Residente = EscrituraLegacyReglas.ExigirTexto(request.Residente, "residente", LargoNombre);
        request.IdStatus = EscrituraLegacyReglas.ExigirStatus(request.IdStatus);
        ExigirAlMenosUnaFuncion(request.EsSupervision, request.EsObra);
        return EscrituraLegacyReglas.Traducir(
            await _repo.ActualizarResidenteAsync(id, request, idUsuario, equipo, ct),
            "actualizar el residente");
    }

    /*
      UN RESIDENTE QUE NO ES NI DE OBRA NI DE SUPERVISION NO SIRVE PARA NADA.

      sp_n_Consultar_MCResidentes filtra por @EsSupervision / @EsObra, y es asi
      como las pantallas de contrato llenan los dos desplegables de residente.
      Un renglon con las dos banderas en cero no sale en ninguno de los dos:
      queda en el catalogo ocupando sitio y no se puede elegir nunca. Legacy
      dejaba darlo de alta porque no mira ninguna de las dos.
    */
    private static void ExigirAlMenosUnaFuncion(bool esSupervision, bool esObra)
    {
        if (!esSupervision && !esObra)
            throw new ArgumentException("El residente tiene que ser de supervision, de obra o de las dos cosas.");
    }

    // ------------------------------------------------------------------
    // Gerentes
    // ------------------------------------------------------------------

    public async Task<ResultadoEscrituraDto> CrearGerenteAsync(CrearObraGerenteRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(request.IdTipoGerente, "idTipoGerente");
        request.Gerente = EscrituraLegacyReglas.ExigirTexto(request.Gerente, "gerente", LargoNombre);
        return EscrituraLegacyReglas.Traducir(
            await _repo.InsertarGerenteAsync(request, idUsuario, equipo, ct),
            "dar de alta el gerente");
    }

    public async Task<ResultadoEscrituraDto> ActualizarGerenteAsync(int id, ActualizarObraGerenteRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(id, "idGerente");
        EscrituraLegacyReglas.ExigirId(request.IdTipoGerente, "idTipoGerente");
        request.Gerente = EscrituraLegacyReglas.ExigirTexto(request.Gerente, "gerente", LargoNombre);
        request.IdStatus = EscrituraLegacyReglas.ExigirStatus(request.IdStatus);
        return EscrituraLegacyReglas.Traducir(
            await _repo.ActualizarGerenteAsync(id, request, idUsuario, equipo, ct),
            "actualizar el gerente");
    }

    // ------------------------------------------------------------------
    // Unidades de medida
    // ------------------------------------------------------------------

    public async Task<ResultadoEscrituraDto> CrearUnidadMedidaAsync(CrearObraUnidadMedidaRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        request.UnidadMedida = EscrituraLegacyReglas.ExigirTexto(request.UnidadMedida, "unidadMedida", LargoNombre);
        return EscrituraLegacyReglas.Traducir(
            await _repo.InsertarUnidadMedidaAsync(request, idUsuario, equipo, ct),
            "dar de alta la unidad de medida");
    }

    public async Task<ResultadoEscrituraDto> ActualizarUnidadMedidaAsync(int id, ActualizarObraUnidadMedidaRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(id, "idUnidadMedida");
        request.UnidadMedida = EscrituraLegacyReglas.ExigirTexto(request.UnidadMedida, "unidadMedida", LargoNombre);
        request.IdStatus = EscrituraLegacyReglas.ExigirStatus(request.IdStatus);
        return EscrituraLegacyReglas.Traducir(
            await _repo.ActualizarUnidadMedidaAsync(id, request, idUsuario, equipo, ct),
            "actualizar la unidad de medida");
    }

    // ------------------------------------------------------------------
    // Partidas presupuestales
    // ------------------------------------------------------------------

    public Task<List<Dictionary<string, object?>>> ConsultarPartidasAsync(ObraPartidasFiltro filtro, CancellationToken ct = default)
        => _repo.ConsultarPartidasAsync(filtro, ct);

    public async Task<ResultadoEscrituraDto> CrearPartidaAsync(CrearObraPartidaRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        request.Partida = EscrituraLegacyReglas.ExigirTexto(request.Partida, "partida", LargoNombre);
        ExigirMontoNoNegativo(request.Monto);
        return EscrituraLegacyReglas.Traducir(
            await _repo.InsertarPartidaAsync(request, idUsuario, equipo, ct),
            "dar de alta la partida");
    }

    public async Task<ResultadoEscrituraDto> ActualizarPartidaAsync(int id, ActualizarObraPartidaRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(id, "idPartida");
        request.Partida = EscrituraLegacyReglas.ExigirTexto(request.Partida, "partida", LargoNombre);
        request.IdStatus = EscrituraLegacyReglas.ExigirStatus(request.IdStatus);
        ExigirMontoNoNegativo(request.Monto);
        return EscrituraLegacyReglas.Traducir(
            await _repo.ActualizarPartidaAsync(id, request, idUsuario, equipo, ct),
            "actualizar la partida");
    }

    /// <summary>
    /// Una partida presupuestal con monto negativo no existe: es dinero
    /// autorizado. El SP lo guarda sin mirarlo y despues los acumulados de
    /// contrato salen mal sin que nada avise.
    /// </summary>
    private static void ExigirMontoNoNegativo(decimal monto)
    {
        if (monto < 0) throw new ArgumentException("El monto de la partida no puede ser negativo.");
    }

    // ------------------------------------------------------------------
    // Padron de proveedores de obra
    // ------------------------------------------------------------------

    public async Task<ResultadoEscrituraDto> CrearProveedorAsync(CrearObraProveedorRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        NormalizarProveedor(request);
        return EscrituraLegacyReglas.Traducir(
            await _repo.InsertarProveedorAsync(request, idUsuario, equipo, ct),
            "dar de alta el proveedor de obra");
    }

    public async Task<ResultadoEscrituraDto> ActualizarProveedorAsync(int id, ActualizarObraProveedorRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(id, "idProveedor");
        request.IdStatus = EscrituraLegacyReglas.ExigirStatus(request.IdStatus);
        NormalizarProveedor(request);
        return EscrituraLegacyReglas.Traducir(
            await _repo.ActualizarProveedorAsync(id, request, idUsuario, equipo, ct),
            "actualizar el proveedor de obra");
    }

    private static void NormalizarProveedor(CrearObraProveedorRequest request)
    {
        request.Nombre = EscrituraLegacyReglas.ExigirTexto(request.Nombre, "nombre", LargoNombre);

        // El RFC se guarda en MAYUSCULAS porque es con lo que se timbra y con lo
        // que se compara contra el padron del SAT. Legacy lo guardaba como lo
        // escribieran, y despues el mismo contratista salia dos veces.
        if (!string.IsNullOrWhiteSpace(request.Rfc))
        {
            request.Rfc = request.Rfc.Trim().ToUpperInvariant();
            if (request.Rfc.Length > LargoRfc)
                throw new ArgumentException($"rfc no puede pasar de {LargoRfc} caracteres.");
        }

        if (!string.IsNullOrWhiteSpace(request.Curp))
            request.Curp = request.Curp.Trim().ToUpperInvariant();

        if (request.CapitalContable < 0)
            throw new ArgumentException("El capital contable no puede ser negativo.");

        if (!string.IsNullOrWhiteSpace(request.Observaciones) && request.Observaciones.Trim().Length > LargoObservaciones)
            throw new ArgumentException($"observaciones no puede pasar de {LargoObservaciones} caracteres.");
    }
}
