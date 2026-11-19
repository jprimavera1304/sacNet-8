using ISL_Service.Application.DTOs.Centros;
using ISL_Service.Application.Interfaces;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Tests;

/// <summary>
/// Un <see cref="IConexionDelCentro"/> que SIEMPRE es la matriz y que NO abre
/// nada.
/// </summary>
/// <remarks>
/// PARA QUE SIRVE. Varias pruebas construyen un repositorio a mano para
/// probarlo contra la base local. Desde que los repositorios que saben de
/// centros reciben esta dependencia, esas pruebas necesitan darle algo — pero
/// lo que prueban son caminos que se quedan en la matriz y que abren su propia
/// conexion (las constantes de facturacion, el precio del concepto).
///
/// POR QUE LANZA EN VEZ DE DEVOLVER UNA CONEXION. Porque si una prueba acaba
/// pidiendole una conexion, lo que ha cambiado es el camino que estaba
/// probando: una lectura que antes era de la matriz ahora pasa por la capa de
/// centros. Devolverle una conexion a la matriz lo escondería y la prueba
/// seguiria verde probando otra cosa. El mensaje dice que hacer.
/// </remarks>
public sealed class ConexionDelCentroDeMentira : IConexionDelCentro
{
    public int CentroPedido => 0;

    public bool EsLaMatriz => true;

    public Task<SqlConnection> AbrirParaLeerAsync(string modulo, CancellationToken ct = default)
        => throw new InvalidOperationException(
            $"La prueba llego a AbrirParaLeerAsync(\"{modulo}\"). Este doble no abre conexiones: " +
            "la prueba esperaba un camino que se queda en la matriz y ahora pasa por la capa de " +
            "centros. Revisa si eso es lo que querias antes de cambiar el doble.");

    public Task<SqlConnection> AbrirParaEscribirAsync(string modulo, string operacion, CancellationToken ct = default)
        => throw new InvalidOperationException(
            $"La prueba llego a AbrirParaEscribirAsync(\"{modulo}\", \"{operacion}\"). Este doble no " +
            "abre conexiones. Ver el comentario de ConexionDelCentroDeMentira.");

    public Task<string> BaseValidadaParaLeerAsync(string modulo, int idCentro, CancellationToken ct = default)
        /* "" es la matriz, que es lo que BaseDelCentro.Apuntar lee como "no toques la cadena". */
        => Task.FromResult(string.Empty);

    public Task<CentrosResponse> CatalogoAsync(CancellationToken ct = default)
        => Task.FromResult(new CentrosResponse());

    public int ConciliarConElCuerpo(int idCentroDelCuerpo) => idCentroDelCuerpo;
}
