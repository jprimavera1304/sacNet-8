using ISL_Service.Application.DTOs.Checador;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Reglas de la checada de comida: normaliza tipo/origen, valida el rango de
/// fechas y traduce los errores de negocio del SP a 400/409.
/// </summary>
public interface IChecadorService
{
    Task<ChecadaComidaRegistradaDto> RegistrarComidaAsync(
        RegistrarChecadaComidaRequest request,
        int idUsuario,
        string equipo,
        CancellationToken ct = default);

    Task<ChecadaComidaRowsResponse> ConsultarComidasAsync(
        int idEmpleado,
        DateTime? fechaInicial,
        DateTime? fechaFinal,
        bool incluirCanceladas,
        CancellationToken ct = default);

    Task CancelarComidaAsync(
        int idEmpleadoChecada,
        string motivo,
        int idUsuario,
        string equipo,
        CancellationToken ct = default);

    Task<ChecadaMovimientoRegistradoDto> RegistrarMovimientoAsync(
        RegistrarChecadaMovimientoRequest request,
        int idUsuario,
        string equipo,
        CancellationToken ct = default);

    Task<ChecadaComidaRowsResponse> ConsultarMovimientosDiaAsync(
        DateTime? fecha,
        int idEmpleado,
        CancellationToken ct = default);

    Task<ChecadaComidaRowsResponse> ConsultarAsistenciaDiaAsync(
        DateTime? fecha,
        int idEmpleado,
        CancellationToken ct = default);

    Task<ChecadaComidaRowsResponse> ConsultarConfiguracionAsync(CancellationToken ct = default);

    Task<ChecadaComidaRowsResponse> ConsultarEmpleadosAsync(
        string? filtro,
        CancellationToken ct = default);

    Task<ChecadaComidaRowsResponse> ConsultarHuellasAsync(
        int idEmpleado,
        CancellationToken ct = default);

    Task<ChecadaComidaRowsResponse> GuardarHuellaAsync(
        GuardarHuellaEmpleadoRequest request,
        int idUsuario,
        string equipo,
        CancellationToken ct = default);

    Task BajaHuellaAsync(
        int idEmpleadoHuella,
        int idUsuario,
        string equipo,
        CancellationToken ct = default);
}
