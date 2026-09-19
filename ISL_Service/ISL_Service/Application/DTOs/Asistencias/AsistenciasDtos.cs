namespace ISL_Service.Application.DTOs.Asistencias;

/// <summary>
/// Un periodo de nomina para el combo de la pantalla.
///
/// Se renombran las columnas de legacy a proposito: el SP devuelve trece
/// columnas (IDPeriodicidad, NumeroDias, Status, ...) que la pantalla no usa, y
/// la descripcion viene bajo el nombre 'PeriodoTipoSueldo', que no se entiende.
/// Lo que la pantalla necesita son cuatro cosas.
/// </summary>
public class PeriodoNominaDto
{
    public int IdPeriodoTipoSueldo { get; set; }
    public int IdTipoSueldo { get; set; }

    /// <summary>Ya viene formateada por legacy: "SEMANA 07   DEL: 02/03/2026   AL: 08/03/2026".</summary>
    public string Descripcion { get; set; } = string.Empty;

    public DateTime FechaInicial { get; set; }
    public DateTime FechaFinal { get; set; }
    public int NumeroSemana { get; set; }
}

/// <summary>
/// Un dia del periodo. Es lo que arma los encabezados de la rejilla.
///
/// El id NO se usa para escribir desde el front: guardar y borrar viajan por
/// FECHA y el cruce lo hace sp_w_GuardarAsistenciaDia adentro de la base. Si el
/// id viajara por HTTP, un front con un bug escribiria la asistencia en la
/// semana equivocada y eso no se nota hasta que sale mal la nomina.
/// </summary>
public class DiaPeriodoDto
{
    public int IdPeriodoTipoSueldoDia { get; set; }
    public DateTime Fecha { get; set; }
}

/// <summary>
/// La rejilla del periodo: un renglon por empleado, siete dias por renglon.
///
/// Los renglones van como diccionario y no como DTO tipado porque son 41
/// columnas numeradas (fechaDia1..7, diaEntrada1..7, diaSalida1..7,
/// diaHorasLaboradas1..7, diaMinutosLaborados1..7, mas minsRetardo,
/// asistencias, inasistencias y vacaciones). Ver Funciones.DataTableToRows.
///
/// CUIDADO CON LOS NULL: en Zaragoza 'asistencias' e 'inasistencias' llegan en
/// NULL para algunos empleados. No es un bug nuestro: a la copia de
/// sp_n_ConsultaEmpleadosPeriodoAsistencia de esa base le falta la linea
/// ISNULL(@diaHorasLaboradas,0) que si tiene la de Tauro, y basta con que un
/// empleado tenga un dia con hora de SALIDA sin hora de ENTRADA para que la
/// suma del periodo entero se le vuelva NULL. Se pasa el null tal cual: la
/// pantalla debe pintar "--", no "0".
/// </summary>
public class RejillaAsistenciaResponse
{
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public int Total => Rows.Count;

    /// <summary>Cuanto tardo la consulta. La rejilla es un doble cursor de legacy y en Zaragoza pasa de 3 segundos.</summary>
    public long Milisegundos { get; set; }
}

/// <summary>
/// Captura manual de un dia.
///
/// Viaja FECHA, no IDPeriodoTipoSueldoDia (ver DiaPeriodoDto).
/// No trae idUsuario ni equipo: salen del token.
/// </summary>
public class GuardarAsistenciaDiaRequest
{
    public int IdEmpleado { get; set; }
    public DateTime? Fecha { get; set; }

    /// <summary>"HH:mm". Vacio o null = no hay hora.</summary>
    public string? HoraEntrada { get; set; }
    public string? HoraSalida { get; set; }

    public bool Inasistencia { get; set; }
    public bool Vacacion { get; set; }
}

/// <summary>Que estatus de empleado pide la rejilla.</summary>
public static class AsistenciaStatus
{
    public const int Activos = 1;
    public const int Inactivos = 2;
    public const int Todos = 3;

    /// <summary>
    /// Legacy toma 0 como "activos" por default. Se normaliza aqui para que un
    /// query string vacio no signifique una cosa distinta segun quien lo lea.
    /// </summary>
    public static int Normalizar(int idStatus)
        => idStatus == Inactivos || idStatus == Todos ? idStatus : Activos;
}
