namespace ISL_Service.Application.DTOs.SeguridadMac;

/*
  USUARIOS DE MAC31, HUELLAS, CONSTANTES Y PARAMETROS.

  Lo que une a estas pantallas: son las que configuran el sistema, y en legacy
  las cuatro tenian el mismo defecto de forma. Tres de ellas (almacenes del
  usuario, procesos del usuario, fechas del usuario) ejecutaban el procedimiento
  N VECES EN UN CICLO, sin transaccion, recorriendo dos cadenas unidas con '~'
  apareadas por posicion. Si reventaba en la quinta de ocho, las cuatro
  anteriores quedaban aplicadas y la respuesta hablaba de la ultima vuelta: el
  usuario quedaba con la mitad de sus permisos cambiados y la pantalla decia que
  todo bien. Aqui cada una recibe una LISTA DE PARES y se ejecuta dentro de UNA
  transaccion.

  OJO CON EL USUARIO DE MAC31 Y EL USUARIO DEL WEB: son dos cosas distintas.
  El del web vive en dbo.UsuarioWeb, tiene llave Guid y contrasena con BCrypt, y
  lo administra api/usuarios. El de Mac31 vive en dbo.Usuarios, tiene llave
  entera y su contrasena la guarda legacy como legacy la guarda. Por eso estas
  rutas cuelgan de api/usuarios/mac31 y no se mezclan con las otras: un id 7 y un
  Guid no se confunden, pero dos pantallas que dicen "usuarios" si.
*/

/// <summary>El par result/mensaje de legacy, traducido.</summary>
public class SeguridadEscrituraResponse
{
    public bool Guardo { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public int Id { get; set; }
}

/// <summary>
/// Resultado de una operacion en lote (almacenes, procesos o fechas de un
/// usuario). Va dentro de una transaccion: <c>Guardo = false</c> significa que no
/// se aplico NINGUNO.
/// </summary>
public class LoteSeguridadResponse
{
    public bool Guardo { get; set; }
    public string Mensaje { get; set; } = string.Empty;

    /// <summary>Cuantos renglones se aplicaron. 0 cuando se deshizo todo.</summary>
    public int Aplicados { get; set; }
}

// ===========================================================================
// USUARIO DE MAC31
// ===========================================================================

/// <summary>
/// Alta de usuario de Mac31 (dbo.Usuarios).
///
/// La contrasena viaja y se guarda como la guarda legacy: el procedimiento
/// escribe la misma cadena en Contrasena y en ContrasenaDesc. No se le aplica
/// hash aqui a proposito — si el web guardara un hash, Mac31 no podria validar el
/// acceso y el usuario no entraria a la aplicacion de escritorio. El modelo de
/// contrasenas de Mac31 no se arregla desde el web; el usuario del WEB si usa
/// BCrypt y vive en otra tabla.
/// </summary>
public class CrearUsuarioMacRequest
{
    public int IdPerfil { get; set; }

    /// <summary>El numero visible del usuario. No es el IDUsuario.</summary>
    public int NumUsuario { get; set; }

    public string Usuario { get; set; } = string.Empty;
    public string Contrasena { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Si al autorizar operaciones se le pide una segunda contrasena.</summary>
    public bool AplicaContrasenaAutorizacion { get; set; }

    public string? ContrasenaAutorizacion { get; set; }

    /// <summary>Puede trabajar en varias cajas.</summary>
    public bool Multicajas { get; set; }
}

/// <summary>
/// Modificacion del usuario de Mac31. Pide la ficha completa, incluida la
/// contrasena: el UPDATE del procedimiento escribe todas esas columnas, asi que
/// un campo que no venga se guarda vacio.
/// </summary>
public class ActualizarUsuarioMacRequest : CrearUsuarioMacRequest
{
    /// <summary>1 activo, 2 inactivo, 3 bloqueado.</summary>
    public int IdStatus { get; set; } = 1;
}

/// <summary>Un almacen que el usuario puede usar, o no.</summary>
public class AlmacenDeUsuario
{
    public int IdAlmacen { get; set; }

    /// <summary>1 = activo (lo puede usar), 2 = cancelado.</summary>
    public int IdStatus { get; set; } = 1;
}

/// <summary>
/// Los almacenes del usuario.
///
/// IMPORTA MAS DE LO QUE PARECE: sin renglon en UsuariosAlmacenes, al repartidor
/// le sale vacia la pantalla de "Mis pedidos" aunque el pedido si se haya
/// guardado. Es la causa mas comun de ese sintoma.
/// </summary>
public class AlmacenesDeUsuarioRequest
{
    public List<AlmacenDeUsuario> Almacenes { get; set; } = new();
}

/// <summary>Un proceso (boton de una forma de Mac31) que el usuario puede ejecutar, o no.</summary>
public class ProcesoDeUsuario
{
    public int IdProceso { get; set; }

    /// <summary>1 = permitido, 2 = no permitido.</summary>
    public int IdStatus { get; set; } = 1;
}

/// <summary>
/// Los procesos del usuario: el mapa de seguridad de Mac31.
///
/// CUIDADO: estas filas son la FUENTE de cuatro familias de permisos del web
/// (reportes.*, ventas.*, empleados.*, prestamos.*). Lo que se guarde aqui
/// gobierna lo que el web muestra; al contrario no: lo que se dé desde la
/// pantalla de permisos del web para esas cuatro familias se borra en el
/// siguiente recalculo.
/// </summary>
public class ProcesosDeUsuarioRequest
{
    public List<ProcesoDeUsuario> Procesos { get; set; } = new();
}

/// <summary>Un dia en el que el usuario puede operar, o no.</summary>
public class FechaDeUsuario
{
    public DateTime Fecha { get; set; }

    /// <summary>1 = puede operar ese dia, 2 = no.</summary>
    public int IdStatus { get; set; } = 1;
}

/// <summary>Las fechas en las que el usuario puede operar.</summary>
public class FechasDeUsuarioRequest
{
    public List<FechaDeUsuario> Fechas { get; set; } = new();
}

// ===========================================================================
// HUELLAS
// ===========================================================================

/// <summary>
/// Registra o actualiza la huella de un usuario, un empleado o un cliente.
///
/// Es un upsert: el procedimiento se llama sp_n_ActualizarHuella y sirve para las
/// dos cosas. Por eso la ruta es un PUT y no un POST.
///
/// Las dos plantillas llegan en base64 y se mandan como varbinary(MAX)
/// EXPLICITO. Dos cosas que legacy hacia mal aqui:
///  - no acotaba el tipo, asi que ADO.NET podia inferir un varbinary(8000) y
///    recortar la plantilla sin avisar: una huella truncada no identifica a
///    nadie y el error aparece semanas despues, en el checador;
///  - `huella2` nula reventaba la llamada con una excepcion de argumento nulo, y
///    tener solo una plantilla es el caso normal.
/// </summary>
public class GuardarHuellaRequest
{
    /// <summary>De quien es la huella. Exactamente UNO de los tres va con valor.</summary>
    public int IdUsuario { get; set; }

    public int IdEmpleado { get; set; }
    public int IdCliente { get; set; }

    /// <summary>Que mano. 1 izquierda, 2 derecha (el catalogo de Mac31).</summary>
    public int IdMano { get; set; }

    /// <summary>Que dedo.</summary>
    public int IdDedo { get; set; }

    /// <summary>1 = activa, 2 = cancelada.</summary>
    public int IdStatus { get; set; } = 1;

    /// <summary>La plantilla, en base64.</summary>
    public string Huella { get; set; } = string.Empty;

    /// <summary>La segunda plantilla, en base64. Opcional: lo normal es tener una.</summary>
    public string? Huella2 { get; set; }
}

// ===========================================================================
// CONTRASENAS CAPTURADAS (bitacora de autorizaciones)
// ===========================================================================

/// <summary>
/// Apunta un intento de contrasena de autorizacion: que forma y que proceso la
/// pidieron, si la que se escribio era correcta, y cual se escribio.
///
/// GUARDA LA CONTRASENA ESCRITA, Y ESO ES ASI EN LA BASE DE LEGACY. No se
/// reinventa aqui porque Mac31 escribe en la misma tabla y la pantalla que la
/// revisa espera esa columna; cambiarlo desde el web dejaria media bitacora con
/// dato y media sin. Queda dicho para que nadie piense que es un descuido del
/// backend nuevo.
/// </summary>
public class RegistrarContrasenaCapturadaRequest
{
    public int IdForma { get; set; }
    public int IdProceso { get; set; }

    /// <summary>El nombre de la forma que pidio la contrasena.</summary>
    public string Forma { get; set; } = string.Empty;

    /// <summary>Si la contrasena escrita era la correcta.</summary>
    public bool Correcto { get; set; }

    public string? Contrasena { get; set; }
}

// ===========================================================================
// CONSTANTES
// ===========================================================================

/// <summary>
/// Los cuatro parametros de operacion que guarda sp_n_ActualizarConstante.
///
/// LOS CUATRO SON OBLIGATORIOS, y eso es el cambio. Legacy no validaba nada: un
/// PUT con el cuerpo vacio escribia CERO en los cuatro, y con
/// MaximaDiferenciaUsados en cero deja de cuadrar la revision de usados de todas
/// las ventas. Son constantes de empresa, se cambian a mano una vez al ano y
/// pedirlas completas es lo correcto: la pantalla ya las tiene leidas.
/// </summary>
public class ActualizarConstantesRequest
{
    /// <summary>Hasta cuanto puede diferir el conteo de usados sin bloquear el cobro.</summary>
    public decimal MaximaDiferenciaUsados { get; set; }

    public int HorasLaborales { get; set; }
    public int DiasFolioConcentrado { get; set; }
    public int NumDiasPedidosRechazados { get; set; }
}

/// <summary>
/// Mueve la fecha de operacion de PAGOS.
///
/// No confundir con la fecha de operacion del negocio, que la rueda
/// sp_n_ConsultaConstantes en el cierre del dia. Esta es la que decide con que
/// fecha entran los cobros.
/// </summary>
public class ActualizarFechaOperacionPagosRequest
{
    /// <summary>
    /// OBLIGATORIA. Si llega vacia el procedimiento NO entiende "no la cambies":
    /// entiende "avanza la fecha de VENTAS", porque su rama de ventas es
    /// justamente IF @FechaOperacionPagos = ''. El controlador la exige antes de
    /// llamar; para mover la de ventas esta su propio endpoint.
    /// </summary>
    public DateTime? FechaOperacionPagos { get; set; }

    /// <summary>
    /// El interruptor que el procedimiento usa para validar o no el cambio. Con
    /// Valido = 1 se salta SOLO el tope inferior (FechaOperacion - NoDiasAtrasados);
    /// el tope superior no se puede saltar. Mac31 lo pone en 1 despues de pedir una
    /// contrasena de autorizacion (FechaOperacionVentas.cs:112-121).
    /// </summary>
    public bool Valido { get; set; }
}

/// <summary>
/// AVANZA LA FECHA DE OPERACION DE VENTAS UN DIA.
///
/// No lleva fecha, y no es un recorte: el procedimiento no acepta una.
/// sp_n_ActualizarFechaOperacion, con @FechaOperacionPagos = '', hace
/// SET FechaOperacion = DATEADD(dd, 1, FechaOperacion) y antes rechaza el cambio si
/// eso dejaria la fecha mas de un dia adelante de hoy. Mac31 hace lo mismo: su
/// boton "guardar fecha de ventas" manda un DTO sin fecha
/// (FechaOperacionVentas.cs:49-52).
///
/// LO QUE ESTA OPERACION ARRASTRA, Y HAY QUE DECIRLE AL USUARIO ANTES:
///   - borra AlmacenProductoHist del dia que se cierra y lo vuelve a escribir con
///     las existencias actuales (el "freeze" del inventario);
///   - apunta el cambio en FechaOperacionLog con tipo 'V';
///   - a partir de ahi, toda la operacion del dia entra con la fecha nueva.
/// No es reversible desde la aplicacion.
/// </summary>
public class AvanzarFechaOperacionVentasRequest
{
    /// <summary>
    /// La fecha a la que el cliente CREE que va a quedar. Opcional y solo sirve de
    /// seguro: si no coincide con FechaOperacion + 1 dia, el backend contesta 409 y
    /// no escribe. Evita el caso real de dos pestanas abiertas -o el cierre
    /// automatico de sp_n_ConsultaConstantes- avanzando el dia dos veces.
    /// </summary>
    public DateOnly? FechaEsperada { get; set; }
}

// ===========================================================================
// PARAMETROS DE REPORTE
// ===========================================================================

/// <summary>
/// Los veinte parametros con los que se abre un reporte.
///
/// Es la tabla Parametro de legacy: la pantalla guarda aqui lo que eligio el
/// usuario y despues abre el reporte pasando solo el id de este renglon. Los
/// veinte campos son texto libre porque cada reporte los usa a su manera; el
/// mapeo vive en cada reporte, no aqui.
///
/// Los nulos se mandan como cadena vacia. En legacy no tenian valor por omision y
/// cualquiera nulo tiraba la llamada con una excepcion de argumento nulo, asi que
/// el cliente tenia que mandar los veinte aunque usara tres.
/// </summary>
public class GuardarParametrosRequest
{
    public int IdReporte { get; set; }

    public string? Param1 { get; set; }
    public string? Param2 { get; set; }
    public string? Param3 { get; set; }
    public string? Param4 { get; set; }
    public string? Param5 { get; set; }
    public string? Param6 { get; set; }
    public string? Param7 { get; set; }
    public string? Param8 { get; set; }
    public string? Param9 { get; set; }
    public string? Param10 { get; set; }
    public string? Param11 { get; set; }
    public string? Param12 { get; set; }
    public string? Param13 { get; set; }
    public string? Param14 { get; set; }
    public string? Param15 { get; set; }
    public string? Param16 { get; set; }
    public string? Param17 { get; set; }
    public string? Param18 { get; set; }
    public string? Param19 { get; set; }
    public string? Param20 { get; set; }

    /// <summary>Los veinte en orden, con los nulos convertidos a cadena vacia.</summary>
    public string[] EnOrden() => new[]
    {
        Param1 ?? string.Empty, Param2 ?? string.Empty, Param3 ?? string.Empty, Param4 ?? string.Empty,
        Param5 ?? string.Empty, Param6 ?? string.Empty, Param7 ?? string.Empty, Param8 ?? string.Empty,
        Param9 ?? string.Empty, Param10 ?? string.Empty, Param11 ?? string.Empty, Param12 ?? string.Empty,
        Param13 ?? string.Empty, Param14 ?? string.Empty, Param15 ?? string.Empty, Param16 ?? string.Empty,
        Param17 ?? string.Empty, Param18 ?? string.Empty, Param19 ?? string.Empty, Param20 ?? string.Empty
    };
}
