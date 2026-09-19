using ISL_Service.Application.DTOs.Empleados;

namespace ISL_Service.Application.Interfaces;

public interface IEmpleadosRepository
{
    /// <param name="idStatus">1 activos, 2 inactivos, 0 todos.</param>
    Task<List<EmpleadoDto>> ConsultarAsync(int idEmpleado, int idTipoSueldo, int idStatus, CancellationToken ct = default);

    Task<CatalogosEmpleadoDto> ConsultarCatalogosAsync(CancellationToken ct = default);

    Task<(ResultadoLegacy Resultado, EmpleadoCreadoDto Creado)> InsertarAsync(
        CrearEmpleadoRequest request, int idUsuario, CancellationToken ct = default);

    Task<ResultadoLegacy> ActualizarAsync(
        int idEmpleado, ActualizarEmpleadoRequest request, int idUsuario, CancellationToken ct = default);

    /// <summary>
    /// Cuantos renglones tiene el empleado en EmpleadoTipoSueldo.
    ///
    /// Hace falta ANTES de guardar porque sp_n_ActualizarEmpleado los pisa
    /// todos sin distinguir el tipo de sueldo. Se pregunta aparte y no se
    /// deduce del DTO: la lista puede venir filtrada por estatus y esconder
    /// justo el renglon que se va a pisar.
    /// </summary>
    Task<int> ContarRenglonesTipoSueldoAsync(int idEmpleado, CancellationToken ct = default);
}
