using ISL_Service.Application.DTOs.VentasEscrituras;
using ISL_Service.Application.DTOs.VentasGarantias;

namespace ISL_Service.Application.Interfaces;

/// <summary>Los nueve procedimientos de garantias de legacy.</summary>
public interface IVentasGarantiasRepository
{
    Task<ResultadoLegacyDto> CrearAsync(GarantiaCrearRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> ActualizarAsync(GarantiaActualizarRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> CancelarAsync(GarantiaCancelarRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarAsync(GarantiaConsultaRequest request, int idUsuarioActual, CancellationToken ct);

    Task<ResultadoLegacyDto> AceptarAsync(GarantiaAceptarRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarAceptarAsync(GarantiaConsultaRequest request, int idUsuarioActual, CancellationToken ct);

    Task<ResultadoLegacyDto> CrearRegistroAsync(GarantiaRegistroCrearRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> CancelarRegistroAsync(GarantiaRegistroCancelarRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarRegistroAsync(GarantiaRegistroConsultaRequest request, CancellationToken ct);

    /// <summary>
    /// Los cuatro catalogos del formulario: empresas, los almacenes DEL USUARIO,
    /// repartidores y productos de carga. El id de usuario sale del token.
    /// </summary>
    Task<GarantiaCatalogosResponse> ConsultarCatalogosAsync(int idUsuarioActual, CancellationToken ct);
}

/// <summary>Las reglas de garantias que no son del procedimiento.</summary>
public interface IVentasGarantiasService
{
    Task<ResultadoLegacyDto> CrearAsync(GarantiaCrearRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> ActualizarAsync(GarantiaActualizarRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> CancelarAsync(GarantiaCancelarRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarAsync(GarantiaConsultaRequest request, int idUsuarioActual, CancellationToken ct);

    Task<ResultadoLegacyDto> AceptarAsync(GarantiaAceptarRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarAceptarAsync(GarantiaConsultaRequest request, int idUsuarioActual, CancellationToken ct);

    Task<ResultadoLegacyDto> CrearRegistroAsync(GarantiaRegistroCrearRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> CancelarRegistroAsync(GarantiaRegistroCancelarRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarRegistroAsync(GarantiaRegistroConsultaRequest request, CancellationToken ct);

    /// <summary>
    /// Los cuatro catalogos del formulario: empresas, los almacenes DEL USUARIO,
    /// repartidores y productos de carga. El id de usuario sale del token.
    /// </summary>
    Task<GarantiaCatalogosResponse> ConsultarCatalogosAsync(int idUsuarioActual, CancellationToken ct);
}
