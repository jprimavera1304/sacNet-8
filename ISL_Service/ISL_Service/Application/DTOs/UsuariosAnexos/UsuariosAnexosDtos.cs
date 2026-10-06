namespace ISL_Service.Application.DTOs.UsuariosAnexos;

/*
  LAS TRES LECTURAS SUELTAS DE LA PANTALLA DE USUARIOS DE MAC31.

  No son el usuario (eso ya lo sirve UsuariosController): son el inventario de
  botones de Mac31 con el acceso de un usuario, el siguiente numero libre para
  dar de alta, y las fechas de operacion que ese usuario tiene abiertas.
*/

/// <summary>
/// Un boton (proceso) de una forma de Mac31 y si este usuario lo tiene
/// encendido.
///
/// Es el inventario COMPLETO de formas y procesos, no solo los del usuario: el
/// SP hace LEFT JOIN desde el catalogo, asi que viene un renglon por boton con
/// `acceso` en false donde no lo tiene. Es lo que necesita la pantalla de
/// permisos de Mac31 para pintar todas las casillas.
///
/// Con idUsuario = 0 devuelve el catalogo entero con acceso en false, que es una
/// forma legitima de preguntar "que botones existen".
/// </summary>
public class UsuarioFormaProcesoDto
{
    public int IDUsuarioFormaProceso { get; set; }
    public int IDUsuario { get; set; }
    public int IDProceso { get; set; }
    /// <summary>1 = concedido, 2 = no. Es el status del renglon, no el acceso.</summary>
    public int IDStatus { get; set; }
    public int IDForma { get; set; }
    /// <summary>Nombre interno del proceso (el `Name` del boton: "btnBuscar").</summary>
    public string Proceso { get; set; } = string.Empty;
    /// <summary>Lo que ve el usuario ("BUSCAR").</summary>
    public string ProcesoDescripcion { get; set; } = string.Empty;
    /// <summary>Nombre interno de la forma ("ConsultaEmpleados").</summary>
    public string Forma { get; set; } = string.Empty;
    /// <summary>
    /// Descripcion de la forma ("CONSULTAR EMPLEADOS"). Es por aqui por donde
    /// el puente de permisos del web busca las formas, no por `Forma`.
    /// </summary>
    public string FormaDescripcion { get; set; } = string.Empty;
    /// <summary>true solo si el renglon del usuario existe y esta en status 1.</summary>
    public bool Acceso { get; set; }
    public int IDPerfil { get; set; }
}

/// <summary>Una fecha de operacion abierta para un usuario.</summary>
public class UsuarioFechaDto
{
    public int IDUsuarioFecha { get; set; }
    public int IDUsuario { get; set; }
    public int IDStatus { get; set; }
    public string Usuario { get; set; } = string.Empty;
    public DateTime? Fecha { get; set; }
    public bool Acceso { get; set; }
}
