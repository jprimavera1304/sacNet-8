namespace ISL_Service.Application.DTOs.Checador;

// Entrada y salida del dia. A diferencia de la comida, estas SI existen en
// legacy (EmpleadoAsistencias), asi que el sp_w_ las manda por el mismo
// procedimiento que usa el checador viejo y quedan visibles en Mac31.
public static class ChecadaMovimientoTipo
{
    public const string Entrada = "ENTRADA";
    public const string Salida = "SALIDA";

    /// <summary>
    /// Vacio/null -> cadena vacia, que para el SP significa "deducelo tu": mira
    /// si el empleado ya tiene hora de entrada hoy. Es el caso normal, porque el
    /// que pone el dedo no elige nada. Valor no reconocido -> null (400).
    /// </summary>
    public static string? Normalizar(string? tipo)
    {
        if (string.IsNullOrWhiteSpace(tipo)) return string.Empty;

        return tipo.Trim().ToUpperInvariant() switch
        {
            Entrada => Entrada,
            Salida => Salida,
            _ => null
        };
    }
}

public class RegistrarChecadaMovimientoRequest
{
    public int IDEmpleado { get; set; }
    // Vacio = que el SP decida entre ENTRADA y SALIDA.
    public string? Tipo { get; set; }
    // Con que huella se identifico. Sin ella la checada cuenta como manual.
    public int? IDEmpleadoHuella { get; set; }
    // "CHECADOR" | "WEB". Vacio = WEB.
    public string? Origen { get; set; }
}

/// <summary>
/// Alta o reemplazo de la huella de un dedo.
///
/// Van DOS capturas porque asi lo guarda el sistema: dos lecturas del mismo
/// dedo, para que reconozca aunque la persona lo ponga un poco corrido. No es
/// opcional; con una sola el registro queda a medias.
/// </summary>
public class GuardarHuellaEmpleadoRequest
{
    public int IDEmpleado { get; set; }
    public int IDMano { get; set; }
    public int IDDedo { get; set; }
    public string HuellaBase64 { get; set; } = string.Empty;
    public string Huella2Base64 { get; set; } = string.Empty;
}

public class ChecadaMovimientoRegistradoDto
{
    public int IDEmpleadoChecada { get; set; }
    public int IDEmpleado { get; set; }
    public string Empleado { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string HoraFtm { get; set; } = string.Empty;
    public bool PrimeraDelDia { get; set; }
}
