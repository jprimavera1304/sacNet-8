using ISL_Service.Application.DTOs.Empleados;

namespace ISL_Service.Application.Interfaces;

public interface IEmpleadosService
{
    /// <param name="filtro">
    /// Texto libre. Se aplica EN MEMORIA, nunca mandandolo al SP:
    /// sp_n_ConsultaEmpleados concatena su @Nombre sin escapar dentro de un LIKE
    /// y mandarle texto del usuario seria inyeccion SQL contra legacy. Son
    /// 25-85 empleados, asi que filtrar aqui no cuesta nada.
    /// </param>
    /// <param name="puedeVerSueldo">
    /// Si es false, los campos de dinero se devuelven en null. El recorte se
    /// hace AQUI y no en el front: esconder un dato que ya viajo por la red no
    /// es esconderlo.
    /// </param>
    Task<List<EmpleadoDto>> ConsultarAsync(int idStatus, string? filtro, bool puedeVerSueldo, CancellationToken ct = default);

    Task<EmpleadoDto?> ObtenerAsync(int idEmpleado, bool puedeVerSueldo, CancellationToken ct = default);

    Task<CatalogosEmpleadoDto> ConsultarCatalogosAsync(CancellationToken ct = default);

    Task<EmpleadoCreadoDto> CrearAsync(CrearEmpleadoRequest request, int idUsuario, CancellationToken ct = default);

    Task ActualizarAsync(int idEmpleado, ActualizarEmpleadoRequest request, int idUsuario, CancellationToken ct = default);
}
