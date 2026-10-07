using ISL_Service.Application.DTOs.InventarioOperaciones;
using ISL_Service.Application.DTOs.VentasEscrituras;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Ajustes de inventario, inventario de pedidos y catalogo de tipos de casco.
/// Los nueve procedimientos son los de legacy, sin tocar.
/// </summary>
public interface IInventarioOperacionesRepository
{
    Task<ResultadoLegacyDto> CrearAjusteAsync(AjusteInventarioCrearRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> CancelarAjusteAsync(AjusteInventarioCancelarRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarAjustesAsync(AjusteInventarioConsultaRequest request, int idUsuarioActual, CancellationToken ct);

    Task<ResultadoLegacyDto> CrearInventarioPedidoAsync(InventarioPedidoCrearRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> CancelarInventarioPedidoAsync(InventarioPedidoCancelarRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarInventarioPedidosAsync(InventarioPedidoConsultaRequest request, CancellationToken ct);

    Task<ResultadoLegacyDto> CrearTipoUsadoAsync(TipoUsadoCrearRequest request, CancellationToken ct);
    Task<ResultadoLegacyDto> ActualizarTipoUsadoAsync(TipoUsadoActualizarRequest request, int idStatus, CancellationToken ct);

    /// <summary>
    /// El renglon del catalogo tal como esta hoy —sus doce campos y su estatus—,
    /// para poder cambiarle uno sin pisar los otros. `null` si no existe.
    ///
    /// Hace falta porque sp_n_ActualizarTipoUsado no sabe actualizar un campo:
    /// sobrescribe el renglon entero.
    /// </summary>
    Task<TipoUsadoGuardado?> LeerTipoUsadoAsync(int idTipoUsado, CancellationToken ct);
}

/// <summary>Las reglas de inventario que no son del procedimiento.</summary>
public interface IInventarioOperacionesService
{
    Task<ResultadoLegacyDto> CrearAjusteAsync(AjusteInventarioCrearRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> CancelarAjusteAsync(AjusteInventarioCancelarRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarAjustesAsync(AjusteInventarioConsultaRequest request, int idUsuarioActual, CancellationToken ct);

    Task<ResultadoLegacyDto> CrearInventarioPedidoAsync(InventarioPedidoCrearRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> CancelarInventarioPedidoAsync(InventarioPedidoCancelarRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarInventarioPedidosAsync(InventarioPedidoConsultaRequest request, CancellationToken ct);

    Task<ResultadoLegacyDto> CrearTipoUsadoAsync(TipoUsadoCrearRequest request, CancellationToken ct);
    Task<ResultadoLegacyDto> ActualizarTipoUsadoAsync(TipoUsadoActualizarRequest request, CancellationToken ct);
    Task<ResultadoLegacyDto> CambiarStatusTipoUsadoAsync(TipoUsadoCambiarStatusRequest request, CancellationToken ct);
}
