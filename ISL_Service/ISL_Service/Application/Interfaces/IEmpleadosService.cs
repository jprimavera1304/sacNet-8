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

    /// <param name="puedeVerSueldo">
    /// LA MISMA LLAVE GOBIERNA LA ESCRITURA, Y ANTES NO.
    ///
    /// Hasta aqui el recorte era solo de LECTURA: quien no tenia
    /// 'empleados.sueldo.ver' recibia los importes en null, pero con
    /// 'empleados.crear' podia MANDARLOS. El request los declara `decimal` a
    /// secas (no nullable), asi que un cliente que no los pudo leer los manda
    /// en 0 y legacy los escribe: el sueldo de alguien acaba en cero sin que
    /// nadie lo haya tecleado.
    ///
    /// Eso solo lo frenaba el front (FormularioEmpleado.tsx, que ni ensena el
    /// boton Guardar), y un front no es una autorizacion: con curl se salta.
    /// Por eso la comprobacion vive aqui.
    /// </param>
    Task<EmpleadoCreadoDto> CrearAsync(CrearEmpleadoRequest request, int idUsuario, bool puedeVerSueldo, CancellationToken ct = default);

    /// <param name="puedeVerSueldo">
    /// Si es false, los cinco importes NO se toman del request: se conservan
    /// los que ya tenia el empleado. Asi quien tiene 'empleados.editar' puede
    /// corregir un RFC sin poder tocar el sueldo, que es exactamente lo que el
    /// permiso de lectura queria decir.
    /// </param>
    Task ActualizarAsync(int idEmpleado, ActualizarEmpleadoRequest request, int idUsuario, bool puedeVerSueldo, CancellationToken ct = default);
}
