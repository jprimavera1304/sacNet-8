using ISL_Service.Application.DTOs.Empleados;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

public class EmpleadosService : IEmpleadosService
{
    private readonly IEmpleadosRepository _repository;

    public EmpleadosService(IEmpleadosRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<EmpleadoDto>> ConsultarAsync(int idStatus, string? filtro, bool puedeVerSueldo, CancellationToken ct = default)
    {
        var lista = await _repository.ConsultarAsync(0, 0, NormalizarStatus(idStatus), ct);
        lista = AplicarFiltro(lista, filtro);
        if (!puedeVerSueldo) foreach (var e in lista) BorrarDinero(e);
        return lista;
    }

    public async Task<EmpleadoDto?> ObtenerAsync(int idEmpleado, bool puedeVerSueldo, CancellationToken ct = default)
    {
        // idStatus = 0 a proposito: al pedir UNO por id se quiere ese, este
        // activo o no. Con 1 un empleado dado de baja daria 404 y no se podria
        // ni consultar su expediente.
        var lista = await _repository.ConsultarAsync(idEmpleado, 0, 0, ct);
        var empleado = lista.FirstOrDefault();
        if (empleado != null && !puedeVerSueldo) BorrarDinero(empleado);
        return empleado;
    }

    public Task<CatalogosEmpleadoDto> ConsultarCatalogosAsync(CancellationToken ct = default)
        => _repository.ConsultarCatalogosAsync(ct);

    public async Task<EmpleadoCreadoDto> CrearAsync(CrearEmpleadoRequest request, int idUsuario, CancellationToken ct = default)
    {
        ValidarComun(request);

        var (resultado, creado) = await _repository.InsertarAsync(request, idUsuario, ct);
        if (!resultado.Ok)
            throw new ArgumentException(Mensaje(resultado, "No se pudo dar de alta al empleado."));

        return creado;
    }

    public async Task ActualizarAsync(int idEmpleado, ActualizarEmpleadoRequest request, int idUsuario, CancellationToken ct = default)
    {
        ValidarComun(request);

        if (request.IDStatus != 1 && request.IDStatus != 2)
            throw new ArgumentException("El estatus solo puede ser 1 (activo) o 2 (inactivo).");

        var existente = await _repository.ConsultarAsync(idEmpleado, 0, 0, ct);
        if (existente.Count == 0)
            throw new NotFoundException("El empleado no existe.");

        // LA BOMBA DE LEGACY: sp_n_ActualizarEmpleado hace
        // "UPDATE EmpleadoTipoSueldo ... WHERE IDEmpleado = @IDEmpleado", SIN
        // filtrar por IDTipoSueldo. Un empleado con dos tipos de sueldo se
        // queda con los DOS renglones pisados con los mismos valores.
        //
        // No se corrige (seria inventar un comportamiento distinto al de
        // Mac31, que es el oraculo), pero tampoco se hace a escondidas: sin
        // confirmacion explicita el endpoint contesta 409 y explica que ese
        // empleado se edita en Mac31.
        var renglones = await _repository.ContarRenglonesTipoSueldoAsync(idEmpleado, ct);
        if (renglones > 1 && !request.ConfirmarMultiple)
        {
            throw new ConflictException(
                $"Este empleado tiene {renglones} tipos de sueldo. Al guardar, el sistema viejo escribe los mismos valores en todos. " +
                "Editelo en Mac31, o vuelva a mandar con confirmarMultiple = true si acepta ese comportamiento.");
        }

        var resultado = await _repository.ActualizarAsync(idEmpleado, request, idUsuario, ct);
        if (!resultado.Ok)
            throw new ArgumentException(Mensaje(resultado, "No se pudo guardar el empleado."));
    }

    /// <summary>
    /// Lo minimo que no se le puede mandar a legacy. Se valida aqui porque los
    /// sp_n_ no validan nada: con nombre vacio o puesto 0 insertan igual y
    /// dejan un empleado sin nombre que despues nadie sabe de donde salio.
    /// </summary>
    private static void ValidarComun(CrearEmpleadoRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Nombre))
            throw new ArgumentException("El nombre es requerido.");
        if (r.IDPuesto <= 0)
            throw new ArgumentException("El puesto es requerido.");
        if (r.IDTipoSueldo <= 0)
            throw new ArgumentException("El tipo de sueldo es requerido.");
        if (r.SueldoSemanal < 0 || r.Comision < 0)
            throw new ArgumentException("El sueldo y la comision no pueden ser negativos.");
    }

    private static string Mensaje(ResultadoLegacy r, string porDefecto)
        => string.IsNullOrWhiteSpace(r.Mensaje) || r.Mensaje.Trim().Equals("ERROR", StringComparison.OrdinalIgnoreCase)
            ? porDefecto
            : r.Mensaje.Trim();

    private static int NormalizarStatus(int idStatus)
        => idStatus is 1 or 2 or 0 ? idStatus : 1;

    private static void BorrarDinero(EmpleadoDto e)
    {
        e.SueldoSemanal = null;
        e.Comision = null;
        e.Asistencia = null;
        e.Puntualidad = null;
        e.SalidaTarde = null;
    }

    /// <summary>
    /// Filtro de texto sobre lo que ya se trajo. Busca en nombre, numero de
    /// empleado, puesto, RFC y agente, que es por donde la gente busca.
    /// </summary>
    private static List<EmpleadoDto> AplicarFiltro(List<EmpleadoDto> lista, string? filtro)
    {
        if (string.IsNullOrWhiteSpace(filtro)) return lista;

        var texto = filtro.Trim();
        bool Contiene(string? campo)
            => !string.IsNullOrEmpty(campo) && campo.Contains(texto, StringComparison.OrdinalIgnoreCase);

        return lista.Where(e =>
                Contiene(e.Nombre)
                || Contiene(e.Puesto)
                || Contiene(e.Rfc)
                || Contiene(e.NombreAgente)
                || e.NumeroEmpleado.ToString().Contains(texto, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
