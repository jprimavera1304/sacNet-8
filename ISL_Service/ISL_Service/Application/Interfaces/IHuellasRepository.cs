using ISL_Service.Application.DTOs.Huellas;

namespace ISL_Service.Application.Interfaces;

/// <summary>Lectura de las huellas registradas.</summary>
public interface IHuellasRepository
{
    /// <summary>
    /// Huellas de un origen. <paramref name="id"/> es el id concreto, o -1 para
    /// todas las de ese origen (es el valor magico que entiende el SP).
    /// </summary>
    Task<List<HuellaDto>> ConsultarAsync(HuellaOrigen origen, int id, CancellationToken ct);
}

/// <summary>De quien son las huellas: de un usuario, de un empleado o de un cliente.</summary>
public enum HuellaOrigen
{
    Usuario = 1,
    Empleado = 2,
    Cliente = 3
}

/// <summary>Valida el origen y el id antes de llamar al SP.</summary>
public interface IHuellasService
{
    Task<List<HuellaDto>> ConsultarAsync(HuellaOrigen origen, int id, bool todas, CancellationToken ct);
}
