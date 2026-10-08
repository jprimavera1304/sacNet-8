using ISL_Service.Application.DTOs.VentasEscrituras;
using ISL_Service.Application.DTOs.VentasUsadosOperaciones;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Los veinte procedimientos de legacy de los cascos usados. Ninguno es nuevo y
/// ninguno se toco: son los mismos que llama Mac31.
///
/// `idUsuario` y `equipo` son parametros explicitos en vez de campos del
/// request a proposito: asi el compilador obliga a pasarlos desde el
/// controlador —que es quien tiene el token— y no hay forma de que se cuelen
/// desde el cuerpo de la peticion.
/// </summary>
public interface IVentasUsadosOperacionesRepository
{
    /* --- Cobros de dinero por cascos --- */

    Task<ResultadoLegacyDto> CrearCobroDineroAsync(
        CobroDineroUsadoCrearRequest request, int idUsuario, string equipo, CancellationToken ct);

    /// <summary>
    /// Guarda o APLICA el reparto. `aplicar` elige el procedimiento:
    /// false -> sp_n_ActualizarCobroDineroUsado, true -> sp_n_AplicarCobroDineroUsado.
    /// Los dos reciben exactamente los mismos diecisiete parametros.
    /// </summary>
    Task<ResultadoLegacyDto> RepartirCobroDineroAsync(
        CobroDineroUsadoRepartoRequest request, bool aplicar, int idUsuario, string equipo, CancellationToken ct);

    Task<ResultadoLegacyDto> CancelarCobroDineroAsync(
        CobroDineroUsadoCancelarRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<FilasLegacyDto> ConsultarCobrosDineroAsync(
        CobroDineroUsadoConsultaRequest request, CancellationToken ct);

    /// <summary>
    /// Los clientes de la hoja de cobro: el renglonaje que se reparte.
    /// `sp_n_ConsultarCobroClientes`, un solo parametro.
    /// </summary>
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

    /* --- Ingresos de cascos usados --- */

    Task<ResultadoLegacyDto> CrearCobroUsadoAsync(
        VentasCobroUsadoCrearRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<ResultadoLegacyDto> CancelarCobroUsadoAsync(
        VentasCobroUsadoCancelarRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<FilasLegacyDto> ConsultarCobrosUsadoAsync(
        VentasCobroUsadoConsultaRequest request, CancellationToken ct);

    /* --- Excedentes de cascos --- */

    Task<ResultadoLegacyDto> CrearExcedenteAsync(
        VentasUsadoExcedenteCrearRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<ResultadoLegacyDto> CancelarExcedenteAsync(
        VentasUsadoExcedenteCancelarRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<FilasLegacyDto> ConsultarExcedentesAsync(
        VentasUsadoExcedenteConsultaRequest request, CancellationToken ct);

    /* --- Movimientos del almacen de usados --- */

    Task<ResultadoLegacyDto> CrearMovimientoAsync(
        UsadoMovimientoCrearRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<ResultadoLegacyDto> CancelarMovimientoAsync(
        UsadoMovimientoCancelarRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<FilasLegacyDto> ConsultarMovimientosAsync(
        UsadoMovimientoConsultaRequest request, CancellationToken ct);

    Task<FilasLegacyDto> ConsultarMovimientosPagadosAsync(
        UsadoMovimientoPagadoConsultaRequest request, CancellationToken ct);
}
