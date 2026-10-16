namespace ISL_Service.Infrastructure.Data;

/// <summary>
/// LA CADENA DE LA BASE PRINCIPAL DE LA EMPRESA, EN UN SOLO SITIO.
/// </summary>
/// <remarks>
/// Hoy el repo tiene 275 copias de estas cuatro lineas
/// (`Main ?? Mac3 ?? Local ?? Default`), una por repositorio. Cada copia es un
/// sitio mas donde el dia que haya que anadir un fallback o cambiar el orden
/// habria que acordarse, y "acordarse" no es un plan — es la misma leccion que
/// ya esta escrita en PermisoControllerBase.
///
/// NO SE MIGRAN LAS 275 DE GOLPE. Esto existe para que lo nuevo nazca con un
/// solo camino y para que los repositorios lo adopten de uno en uno cuando les
/// toque. Mientras tanto las dos formas dan exactamente la misma cadena: el
/// orden de los fallbacks es identico a proposito.
/// </remarks>
public static class CadenaDeLaEmpresa
{
    /// <summary>Los nombres que se prueban, en orden. El primero que exista gana.</summary>
    public static readonly string[] Nombres = { "Main", "Mac3", "Local", "Default" };

    /// <summary>La cadena principal. Lanza si no hay ninguna configurada.</summary>
    public static string De(IConfiguration configuration)
    {
        var cs = Buscar(configuration);
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("ConnectionString (Main/Mac3/Local/Default) no encontrada.");
        return cs!;
    }

    /// <summary>La cadena principal, o null si no hay ninguna.</summary>
    public static string? Buscar(IConfiguration configuration)
    {
        foreach (var nombre in Nombres)
        {
            var cs = configuration.GetConnectionString(nombre);
            if (!string.IsNullOrWhiteSpace(cs)) return cs;
        }
        return null;
    }
}
