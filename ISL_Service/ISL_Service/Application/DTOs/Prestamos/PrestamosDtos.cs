namespace ISL_Service.Application.DTOs.Prestamos;

/// <summary>
/// Los prestamos tal cual los devuelve sp_n_ConsultaEmpleadoPrestamos.
///
/// Van como diccionario porque el SP ya devuelve sus 21 columnas en camelCase
/// (idEmpleadoPrestamo, montoPrestamo, saldo, fechaInicioPagosFtm...) y
/// copiarlas a un DTO tipado solo agregaria un lugar donde equivocarse: si
/// legacy renombra una columna, un DTO la deja en cero calladito, y en dinero un
/// cero silencioso es peor que un campo faltante.
/// </summary>
public class PrestamosResponse
{
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public int Total => Rows.Count;
}

/// <summary>
/// El filtro de la pantalla, en palabras.
///
/// Existe para que el front NO conozca los numeros magicos de legacy
/// (@Pagados 0/1/2/3). Ese mapeo es un detalle del SP; si viviera en el front,
/// el dia que legacy agregue un cuarto estado habria que tocar web y movil.
/// </summary>
public static class PrestamoEstado
{
    public const string Todos = "todos";
    public const string Pagados = "pagados";
    public const string Pendientes = "pendientes";
    public const string Cancelados = "cancelados";

    /// <summary>Devuelve el @Pagados de legacy, o null si el estado no se reconoce (el controller contesta 400).</summary>
    public static int? ALegacy(string? estado)
    {
        if (string.IsNullOrWhiteSpace(estado)) return 0;

        return estado.Trim().ToLowerInvariant() switch
        {
            Todos => 0,
            Pagados => 1,
            Pendientes => 2,
            Cancelados => 3,
            _ => null
        };
    }
}

/// <summary>
/// Alta de prestamo.
///
/// Las fechas viajan como fecha de verdad y el backend las traduce a
/// 'MM-dd-yyyy', que es lo que espera legacy (Constantes.FormatoFecha en las
/// dos empresas). Mandar la cadena ya formateada desde el front seria confiar en
/// que el navegador del usuario tenga la misma cultura que el servidor, y un
/// 03-04-2026 leido al reves cambia el mes por el dia en un movimiento de dinero.
///
/// No trae idUsuario: sale del token.
/// </summary>
public class CrearPrestamoRequest
{
    public int IdEmpleado { get; set; }
    public DateTime? FechaPrestamo { get; set; }
    public decimal MontoPrestamo { get; set; }
    public string? MotivoPrestamo { get; set; }
    public DateTime? FechaInicioPagos { get; set; }

    /// <summary>El descuento SEMANAL, no el total.</summary>
    public decimal MontoPagos { get; set; }

    public bool PagoInmediato { get; set; }
    public bool AgregarDeuda { get; set; }
}

/// <summary>Lo unico que legacy deja cambiar de un prestamo ya hecho.</summary>
public class CambiarMontoPagosRequest
{
    public decimal MontoPagos { get; set; }
}

public class PrestamoCreadoDto
{
    public int IdEmpleadoPrestamo { get; set; }

    /// <summary>
    /// Aviso cuando el empleado YA tenia un prestamo abierto y legacy no uso el
    /// montoPagos que se mando. Se contesta como texto y no en silencio porque
    /// el capturista escribio un numero y tiene derecho a saber que no se aplico.
    /// </summary>
    public string? Advertencia { get; set; }
}
