namespace ISL_Service.Application.DTOs.Empleados;

/// <summary>
/// Un empleado como lo devuelve sp_n_ConsultaEmpleadosTipoSueldo.
///
/// Los nombres de las propiedades son EXACTAMENTE los de las columnas del SP
/// porque Funciones.DataTableToList compara nombre contra nombre: si aqui se
/// escribe "IdEmpleado" en vez de "IDEmpleado", la propiedad se queda en cero
/// sin decir nada. Hacia afuera salen en camelCase, que es lo que hace de por
/// si el serializador del API.
///
/// POR QUE ESTE SP Y NO sp_n_ConsultaEmpleados
/// -------------------------------------------
/// sp_n_ConsultaEmpleados solo trae la tabla Empleados, que NO tiene puesto ni
/// sueldo: esos viven en EmpleadoTipoSueldo. Con ese SP el formulario no se
/// puede llenar. Ademas concatena el filtro de nombre sin escapar dentro de un
/// LIKE, o sea que mandarle texto del usuario es inyeccion SQL contra legacy;
/// por eso el filtro por texto se hace en memoria y NUNCA se manda al SP.
/// </summary>
public class EmpleadoDto
{
    public int IDEmpleado { get; set; }
    public int IDPuesto { get; set; }
    public int IDStatus { get; set; }
    public int IDTipoSueldo { get; set; }
    public int IDAgente { get; set; }
    public int IDChecador { get; set; }
    public int NumeroEmpleado { get; set; }
    public string Puesto { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public DateTime? FechaIngreso { get; set; }
    public string FechaIngresoFtm { get; set; } = string.Empty;
    public string NumeroSeguro { get; set; } = string.Empty;
    public string Rfc { get; set; } = string.Empty;
    public string Curp { get; set; } = string.Empty;
    public string TipoSueldo { get; set; } = string.Empty;

    // ---- BLOQUE DE DINERO ----
    // Son nullable a proposito. Sin el permiso 'empleados.sueldo.ver' el
    // backend los pone en null ANTES de contestar. Si fueran decimal a secas,
    // "no puedes ver el sueldo" y "gana cero" se verian igual, y ademas el
    // valor real seguiria viajando por la red esperando que el front lo
    // escondiera. Esconder en el front no es esconder.
    public decimal? SueldoSemanal { get; set; }
    public decimal? Comision { get; set; }
    public decimal? Asistencia { get; set; }
    public decimal? Puntualidad { get; set; }
    public decimal? SalidaTarde { get; set; }

    public string Estatus { get; set; } = string.Empty;
    public int Agente { get; set; }
    public string NombreAgente { get; set; } = string.Empty;
    public string NumeroNombreAgente { get; set; } = string.Empty;
    public string Huellas { get; set; } = string.Empty;
    public int TotalEmpleados { get; set; }
}

/// <summary>Los cuatro catalogos que necesita el formulario, en una sola ida.</summary>
public class CatalogosEmpleadoDto
{
    public List<PuestoDto> Puestos { get; set; } = new();
    public List<TipoSueldoDto> TiposSueldo { get; set; } = new();
    public List<AgenteDto> Agentes { get; set; } = new();
    public List<EstatusDto> Estatus { get; set; } = new();
}

public class PuestoDto
{
    public int IDPuesto { get; set; }
    public string Puesto { get; set; } = string.Empty;

    /// <summary>
    /// Con esto la pantalla decide si muestra el combo de agentes. Hoy solo
    /// "AGENTE DE VENTAS" lo trae en 1, en las dos empresas; sale del catalogo
    /// y no escrito a mano para que el dia que marquen otro puesto en Mac31 la
    /// pantalla se entere sola.
    /// </summary>
    public int EsAgente { get; set; }
}

public class TipoSueldoDto
{
    public int IDTipoSueldo { get; set; }
    public string TipoSueldo { get; set; } = string.Empty;
    public int EsComision { get; set; }
}

public class AgenteDto
{
    public int IDAgente { get; set; }
    public int Agente { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string NumeroNombreAgente { get; set; } = string.Empty;
}

public class EstatusDto
{
    public int IDStatus { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Alta de empleado.
///
/// No trae IDUsuario: quien hace el movimiento sale del token, nunca del body.
/// Si viajara en el body, cualquiera podria firmar un alta a nombre de otro.
///
/// Tampoco trae NumeroEmpleado ni IDStatus: el numero lo genera legacy
/// (MAX + 1) y el alta siempre entra ACTIVA. Aceptarlos seria prometer algo que
/// el SP de legacy ignora.
/// </summary>
public class CrearEmpleadoRequest
{
    public string Nombre { get; set; } = string.Empty;
    public int IDPuesto { get; set; }
    public int IDTipoSueldo { get; set; }
    public int IDAgente { get; set; }
    public int IDChecador { get; set; }

    /// <summary>Fecha en formato ISO (yyyy-MM-dd) o vacio. El backend la traduce al formato de legacy.</summary>
    public string? FechaIngreso { get; set; }

    public string? NumeroSeguro { get; set; }
    public string? Rfc { get; set; }
    public string? Curp { get; set; }

    public decimal SueldoSemanal { get; set; }
    public decimal Comision { get; set; }
    public decimal Asistencia { get; set; }
    public decimal Puntualidad { get; set; }
    public decimal SalidaTarde { get; set; }
}

/// <summary>
/// Cambio de empleado. Igual que el alta, mas IDStatus (aqui SI se puede dar de
/// baja poniendolo en 2).
///
/// OJO CON LA BAJA: legacy pone IDStatus = 2 y NUNCA llena IDUsuarioBaja,
/// FechaBaja ni MotivoBaja. Se deja igual de mudo que Mac31 a proposito;
/// llenarlas seria un UPDATE nuestro sobre una tabla de legacy.
/// </summary>
public class ActualizarEmpleadoRequest : CrearEmpleadoRequest
{
    public int IDStatus { get; set; } = 1;

    /// <summary>
    /// Permiso explicito para guardar un empleado que tiene MAS DE UN renglon
    /// en EmpleadoTipoSueldo.
    ///
    /// sp_n_ActualizarEmpleado hace el UPDATE sin filtrar por IDTipoSueldo, asi
    /// que a esa persona le pisa TODOS sus renglones con los mismos valores.
    /// Ya pasa hoy en Mac31; lo que no puede pasar es que el web lo haga sin
    /// que nadie se entere. Sin esta bandera el endpoint contesta 409 y explica
    /// que hay que editarlo en Mac31.
    /// </summary>
    public bool ConfirmarMultiple { get; set; }
}

/// <summary>Lo que devuelve el alta: lo que legacy calculo por dentro.</summary>
public class EmpleadoCreadoDto
{
    public int IdEmpleado { get; set; }
    public int NumeroEmpleado { get; set; }
}

/// <summary>result/mensaje tal cual los devuelven los SP de escritura de legacy.</summary>
public class ResultadoLegacy
{
    public int Result { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    /// <summary>Legacy contesta 1 en exito y -1 en error. Cualquier otra cosa se trata como error.</summary>
    public bool Ok => Result > 0;
}
