using ISL_Service.Application.DTOs.Empresa;
using ISL_Service.Application.Empresa;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Lectura de dbo.Constantes para publicar las capacidades de la empresa.
/// NO llama a sp_n_ConsultaConstantes: ese procedimiento cierra el dia.
/// </summary>
public interface ICapacidadesEmpresaRepository
{
    /// <summary>
    /// La fila de constantes, cruda. <paramref name="idUsuario"/> es el IDUsuario
    /// de Mac31 y solo se usa en Zaragoza, donde la fecha de operacion de pagos es
    /// de cada usuario ([Usuarios Fechas]). Con 0 se devuelve la de la empresa.
    /// </summary>
    Task<ConstantesDeEmpresa> ConsultarAsync(int idUsuario, CancellationToken ct);
}

/// <summary>
/// Las capacidades de la empresa en la que corre esta instalacion, listas para el
/// front. Lo consumen el endpoint propio y /api/me.
/// </summary>
public interface ICapacidadesEmpresaService
{
    Task<CapacidadesEmpresaResponse> ConsultarAsync(int idUsuarioLegacy, CancellationToken ct);
}
