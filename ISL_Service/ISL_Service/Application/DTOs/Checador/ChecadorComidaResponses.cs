namespace ISL_Service.Application.DTOs.Checador;

/// <summary>
/// Lo que devuelve sp_w_RegistrarChecadaComida. Interesa sobre todo el Tipo: como
/// el cliente puede mandarlo vacio, esta es la unica forma de saber si lo que
/// quedo registrado fue la salida o el regreso.
/// </summary>
public class ChecadaComidaRegistradaDto
{
    public int IDEmpleadoChecada { get; set; }
    public int IDEmpleado { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public DateTime FechaHora { get; set; }
    /// <summary>Null cuando fue COMIDA_INICIO: los minutos solo se saben al cerrar el par.</summary>
    public int? MinutosComida { get; set; }
}

/// <summary>
/// Resultado de sp_w_ConsultarChecadasComida. Se devuelve como filas sueltas y no
/// como DTO tipado a proposito: el SP lo esta armando otro equipo y agregar una
/// columna (turno, area, etc.) no deberia obligar a tocar el backend. Es el mismo
/// trato que se le da a las consultas de captura de pedidos.
/// </summary>
public class ChecadaComidaRowsResponse
{
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public int Total => Rows.Count;
}
