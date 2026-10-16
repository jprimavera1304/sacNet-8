using ISL_Service.Application.Centros;
using ISL_Service.Application.DTOs.Centros;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// EL UNICO SITIO QUE RESUELVE A QUE BASE SE LE PREGUNTA.
/// </summary>
/// <remarks>
/// EL PROBLEMA QUE CIERRA
/// ----------------------
/// Hoy hay 275 copias de `GetConnectionString("Main") ?? ...` repartidas por los
/// repositorios, y UNA sola sabe de centros: ReportesVentasRepository, con un
/// `GetConnection(string? baseDelCentro)` privado y un comentario que dice
/// "solo lo usan los REPORTES". Mientras siga asi, cada modulo que quiera
/// centros va a armar su propia cadena, y el dia que uno se equivoque el
/// sintoma sera datos de otra empresa en pantalla — sin un error, sin un log.
///
/// La regla es: NINGUN REPOSITORIO VUELVE A ARMAR SU CADENA. Pide la conexion
/// aqui, con la clave de su modulo, y la recibe ya apuntada y ya validada — o
/// recibe un 409 con el motivo.
///
/// LAS CINCO COMPROBACIONES, SIEMPRE, EN EL SERVIDOR
/// ------------------------------------------------
///   1. El token dice quien es y de que empresa.
///   2. El centro existe y esta ACTIVO en dbo.CentrosServicio de esa empresa.
///   3. LA BASE DE ESE CENTRO PERTENECE A ESA EMPRESA
///      (ver <see cref="PertenenciaDeCentro"/>: es la que protege a Tauro).
///   4. El usuario tiene `centros.&lt;n&gt;.ver` para leer y
///      `centros.&lt;n&gt;.operar` para escribir.
///   5. El modulo esta DECLARADO como compatible con centros
///      (ver <see cref="ModulosConCentros"/>).
///
/// NO SE CONFIA EN NADA DE LO QUE MANDA EL CLIENTE. La cabecera X-Centro solo
/// ESCOGE; quien puede, que base es y si el modulo lo admite se decide aqui,
/// en cada peticion.
///
/// SI ALGO FALLA: 409 CON MOTIVO LEGIBLE, Y NUNCA DATOS DE LA MATRIZ. Caer a la
/// matriz "para que algo salga" es ensenar un sitio diciendo que es otro, y eso
/// no se nota hasta que alguien toma una decision con esos numeros.
/// </remarks>
public interface IConexionDelCentro
{
    /// <summary>
    /// El centro que pidio la peticion. 0 = matriz (ausente tambien).
    /// Lanza 409 si la cabecera trae basura.
    /// </summary>
    int CentroPedido { get; }

    /// <summary>true si esta peticion va a la matriz.</summary>
    bool EsLaMatriz { get; }

    /// <summary>
    /// Una conexion ABIERTA a la base del centro pedido, para LEER, validando
    /// las cinco reglas. Con centro 0 es la de siempre: identica a hoy.
    /// </summary>
    /// <param name="modulo">La clave del modulo que pide: "reportes", "ventas"...</param>
    Task<SqlConnection> AbrirParaLeerAsync(string modulo, CancellationToken ct = default);

    /// <summary>
    /// Una conexion ABIERTA para ESCRIBIR. Ademas de las cinco reglas exige que
    /// el modulo tenga declarada ESA operacion como escribible fuera de la
    /// matriz y que el usuario tenga `centros.&lt;n&gt;.operar`.
    /// </summary>
    Task<SqlConnection> AbrirParaEscribirAsync(string modulo, string operacion, CancellationToken ct = default);

    /// <summary>
    /// El nombre de la base ya validada ("" = matriz), para los repositorios
    /// que hoy ya saben recibir un nombre y no quieren cambiar su forma de
    /// abrir — es el caso de ReportesVentasRepository. Es el camino de
    /// adopcion gradual: el repositorio no arma la cadena, solo pide el nombre
    /// que YA paso las cinco comprobaciones.
    /// </summary>
    Task<string> BaseValidadaParaLeerAsync(string modulo, int idCentro, CancellationToken ct = default);

    /// <summary>
    /// El catalogo de GET /api/centros, ya filtrado por lo que este usuario
    /// puede ver y por la pertenencia de cada base.
    /// </summary>
    Task<CentrosResponse> CatalogoAsync(CancellationToken ct = default);

    /// <summary>
    /// Cuando el cuerpo trae su propio idCentro (los reportes), MANDA EL DEL
    /// CUERPO y el desacuerdo se registra. Ver <see cref="CentroPedido.Conciliar"/>.
    /// </summary>
    int ConciliarConElCuerpo(int idCentroDelCuerpo);
}
