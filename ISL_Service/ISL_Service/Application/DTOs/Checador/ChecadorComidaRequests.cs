namespace ISL_Service.Application.DTOs.Checador;

// Eventos de la checada de comida. Son los mismos valores que guarda la columna
// Tipo de WEmpleadoChecada, no un enum propio del backend: si aqui se inventa
// otro nombre, el SP lo rechaza.
//
// El tipo puede venir VACIO a proposito. El checador fisico solo tiene la huella
// del empleado (no hay boton de "salgo" / "regreso"), asi que manda vacio y el SP
// deduce si toca inicio o fin segun la ultima checada del dia. El web si lo manda
// explicito porque ahi el capturista si elige.
public static class ChecadaComidaTipo
{
    public const string Inicio = "COMIDA_INICIO";
    public const string Fin = "COMIDA_FIN";

    /// <summary>
    /// Vacio/null -> cadena vacia (el SP deduce). Valor no reconocido -> null
    /// (el service responde 400). Se aceptan los alias cortos "inicio"/"fin"
    /// porque son los que resulta natural mandar desde un checador.
    /// </summary>
    public static string? Normalizar(string? tipo)
    {
        if (string.IsNullOrWhiteSpace(tipo)) return string.Empty;

        return tipo.Trim().ToUpperInvariant() switch
        {
            "COMIDA_INICIO" or "INICIO" => Inicio,
            "COMIDA_FIN" or "FIN" => Fin,
            _ => null
        };
    }
}

// De donde vino la checada. Sirve para auditar: una checada con Origen=WEB la
// captura una persona a mano y por eso lleva IDUsuario; una CHECADOR la genera
// el lector de huella.
public static class ChecadaComidaOrigen
{
    public const string Checador = "CHECADOR";
    public const string Web = "WEB";

    /// <summary>
    /// Vacio/null -> WEB (default del SP). Valor no reconocido -> null.
    /// </summary>
    public static string? Normalizar(string? origen)
    {
        if (string.IsNullOrWhiteSpace(origen)) return Web;

        return origen.Trim().ToUpperInvariant() switch
        {
            Checador => Checador,
            Web => Web,
            _ => null
        };
    }
}

public class RegistrarChecadaComidaRequest
{
    public int IDEmpleado { get; set; }
    // Vacio = que el SP decida si es inicio o fin.
    public string? Tipo { get; set; }
    // Solo cuando la checada viene del lector: identifica que huella se leyo.
    public int? IDEmpleadoHuella { get; set; }
    // "CHECADOR" | "WEB". Vacio = WEB.
    public string? Origen { get; set; }
}

public class CancelarChecadaComidaRequest
{
    // La checada no se borra: se marca Cancelada con motivo, para que el calculo
    // de nomina pueda auditar por que se descarto.
    public string Motivo { get; set; } = string.Empty;
}
