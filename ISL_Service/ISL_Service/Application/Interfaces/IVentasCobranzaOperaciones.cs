using ISL_Service.Application.DTOs.VentasCobranzaOperaciones;
using ISL_Service.Application.DTOs.VentasEscrituras;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Los catorce procedimientos de cobranza, concentrado y cobro de legacy.
/// Ninguno es nuevo; `idUsuario` y `equipo` llegan del token, nunca del cuerpo.
/// </summary>
public interface IVentasCobranzaOperacionesRepository
{
    Task<ResultadoLegacyDto> CrearCobranzaAsync(CobranzaCrearRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> ActualizarCobranzaAsync(CobranzaActualizarRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> CancelarCobranzaAsync(CobranzaCancelarRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarCobranzasAsync(CobranzaConsultaRequest request, int idUsuarioActual, CancellationToken ct);

    Task<ResultadoLegacyDto> CrearConcentradoAsync(ConcentradoCrearRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> ActualizarConcentradoAsync(ConcentradoActualizarRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> CancelarConcentradoAsync(ConcentradoCancelarRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<ResultadoLegacyDto> AbrirCobroAsync(CobroLoteRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> CerrarCobroAsync(CobroLoteRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> CancelarCobroAsync(CobroLoteRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> ActualizarCobroAsync(CobroActualizarRequest request, CancellationToken ct);

    Task<FilasLegacyDto> ConsultarCobrosAsync(CobroConsultaRequest request, int idUsuarioActual, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarCobroClientesAsync(int idCobro, CancellationToken ct);

    Task<FilasLegacyDto> ConsultarPendientesVencidosAsync(CobranzaPendientesVencidosRequest request, CancellationToken ct);
}

/// <summary>
/// Las reglas de cobranza que no son del procedimiento: limpiar las listas de
/// ids y exigir lo que legacy dejaba pasar en 0.
/// </summary>
public interface IVentasCobranzaOperacionesService
{
    Task<ResultadoLegacyDto> CrearCobranzaAsync(CobranzaCrearRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> ActualizarCobranzaAsync(CobranzaActualizarRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> CancelarCobranzaAsync(CobranzaCancelarRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarCobranzasAsync(CobranzaConsultaRequest request, int idUsuarioActual, CancellationToken ct);

    Task<ResultadoLegacyDto> CrearConcentradoAsync(ConcentradoCrearRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> ActualizarConcentradoAsync(ConcentradoActualizarRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> CancelarConcentradoAsync(ConcentradoCancelarRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<ResultadoLegacyDto> AbrirCobroAsync(CobroLoteRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> CerrarCobroAsync(CobroLoteRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> CancelarCobroAsync(CobroLoteRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> ActualizarCobroAsync(CobroActualizarRequest request, CancellationToken ct);

    Task<FilasLegacyDto> ConsultarCobrosAsync(CobroConsultaRequest request, int idUsuarioActual, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarCobroClientesAsync(int idCobro, CancellationToken ct);

    Task<FilasLegacyDto> ConsultarPendientesVencidosAsync(CobranzaPendientesVencidosRequest request, CancellationToken ct);
}
