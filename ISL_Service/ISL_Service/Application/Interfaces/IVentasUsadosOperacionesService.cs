using ISL_Service.Application.DTOs.VentasEscrituras;
using ISL_Service.Application.DTOs.VentasUsadosOperaciones;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Las reglas de los cascos usados que NO son del procedimiento: limpiar las
/// listas, acotar la prioridad de filtros y exigir que las listas paralelas
/// midan lo mismo.
///
/// Lo que decide si un movimiento se puede hacer sigue siendo el sp_n_: aqui no
/// se adelanta ninguna de sus validaciones. Adelantarlas significaria tener dos
/// opiniones sobre la misma regla y que el dia que legacy cambie la suya el web
/// siga diciendo la vieja.
/// </summary>
public interface IVentasUsadosOperacionesService
{
    Task<ResultadoLegacyDto> CrearCobroDineroAsync(
        CobroDineroUsadoCrearRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<ResultadoLegacyDto> GuardarRepartoCobroDineroAsync(
        CobroDineroUsadoRepartoRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<ResultadoLegacyDto> AplicarCobroDineroAsync(
        CobroDineroUsadoRepartoRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<ResultadoLegacyDto> CancelarCobroDineroAsync(
        CobroDineroUsadoCancelarRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<FilasLegacyDto> ConsultarCobrosDineroAsync(
        CobroDineroUsadoConsultaRequest request, CancellationToken ct);

    /// <summary>Los clientes de la hoja de cobro, para poder repartir sobre ellos.</summary>
    Task<FilasLegacyDto> ConsultarClientesDelCobroAsync(
        CobroDineroUsadoClientesConsultaRequest request, CancellationToken ct);

    Task<ResultadoLegacyDto> CrearChequeAsync(
        CobroDineroUsadoChequeCrearRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<ResultadoLegacyDto> ActualizarChequeAsync(
        CobroDineroUsadoChequeActualizarRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<FilasLegacyDto> ConsultarChequesAsync(
        CobroDineroUsadoChequeConsultaRequest request, CancellationToken ct);

    Task<ResultadoLegacyDto> CrearCorteCsAsync(
        CobroDineroUsadoCorteCsRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<ResultadoLegacyDto> EliminarCorteCsAsync(
        CobroDineroUsadoCorteCsRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<FilasLegacyDto> ConsultarCortesCsAsync(
        CobroDineroUsadoCorteCsConsultaRequest request, CancellationToken ct);

    Task<ResultadoLegacyDto> CrearCobroUsadoAsync(
        VentasCobroUsadoCrearRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<ResultadoLegacyDto> CancelarCobroUsadoAsync(
        VentasCobroUsadoCancelarRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<FilasLegacyDto> ConsultarCobrosUsadoAsync(
        VentasCobroUsadoConsultaRequest request, CancellationToken ct);

    Task<ResultadoLegacyDto> CrearExcedenteAsync(
        VentasUsadoExcedenteCrearRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<ResultadoLegacyDto> CancelarExcedenteAsync(
        VentasUsadoExcedenteCancelarRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<FilasLegacyDto> ConsultarExcedentesAsync(
        VentasUsadoExcedenteConsultaRequest request, CancellationToken ct);

    Task<ResultadoLegacyDto> CrearMovimientoAsync(
        UsadoMovimientoCrearRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<ResultadoLegacyDto> CancelarMovimientoAsync(
        UsadoMovimientoCancelarRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<FilasLegacyDto> ConsultarMovimientosAsync(
        UsadoMovimientoConsultaRequest request, CancellationToken ct);

    Task<FilasLegacyDto> ConsultarMovimientosPagadosAsync(
        UsadoMovimientoPagadoConsultaRequest request, CancellationToken ct);
}
