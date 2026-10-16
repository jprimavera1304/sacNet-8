using ISL_Service.Application.Security;

namespace ISL_Service.Application.Centros;

/// <summary>
/// QUE PUEDE ESTE USUARIO CON LOS CENTROS. Codigo puro: ni base ni DI.
/// </summary>
/// <remarks>
/// Se construye con el juego de permisos YA LEIDO (un solo snapshot por
/// peticion) y con la misma funcion `Tiene` que usa el resto del backend, para
/// que el superadmin, la normalizacion de separadores y la puerta del modulo se
/// contesten igual aqui que en cualquier controlador. Si esto tuviera su propia
/// comparacion acabaria opinando distinto, que es el error que ya se pago una
/// vez en TienePermisoAsync.
///
/// Se prueba sin base porque es donde vive la regla, y una regla que solo se
/// puede comprobar levantando SQL Server no se comprueba.
/// </remarks>
public sealed class CapacidadesDeCentros
{
    private readonly bool _todoPermitido;
    private readonly Func<string, bool> _tiene;
    private readonly IReadOnlyCollection<string> _permisos;

    /// <param name="todoPermitido">SuperAdmin o instalacion sin modelo de permisos.</param>
    /// <param name="permisos">El snapshot crudo, ya filtrado por canal.</param>
    /// <param name="tiene">La misma comprobacion del resto del backend.</param>
    public CapacidadesDeCentros(
        bool todoPermitido,
        IReadOnlyCollection<string> permisos,
        Func<string, bool> tiene)
    {
        _todoPermitido = todoPermitido;
        _permisos = permisos;
        _tiene = tiene;
    }

    /// <summary>
    /// Puede usar el selector. La clave vieja de reportes vale: quien hoy puede,
    /// manana puede.
    /// </summary>
    public bool PuedeCambiar =>
        _todoPermitido || _tiene(ClavesDeCentros.Cambiar) || _tiene(ClavesDeCentros.CambiarCompatible);

    /// <summary>Puede CONSULTAR ese centro. Tambien con la clave vieja.</summary>
    public bool PuedeVer(int numero) =>
        _todoPermitido || _tiene(ClavesDeCentros.Ver(numero)) || _tiene(ClavesDeCentros.VerCompatible(numero));

    /// <summary>
    /// Puede ESCRIBIR en ese centro. Sin compatibilidad y sin deducirlo de
    /// <see cref="PuedeVer"/>: se concede a mano.
    /// </summary>
    public bool PuedeOperar(int numero) =>
        _todoPermitido || _tiene(ClavesDeCentros.Operar(numero));

    /// <summary>
    /// Puede ESCRIBIR en la matriz.
    /// </summary>
    /// <remarks>
    /// AQUI NO SE LE QUITA EL ACCESO A NADIE, Y ESO MANDA SOBRE LA PUREZA DEL
    /// MODELO.
    ///
    /// La matriz es lo que todo el mundo usa hoy, y hoy no existe ninguna clave
    /// `centros.matriz.*`: nadie la tiene. Si la matriz empezara a exigirla, el
    /// dia del despliegue la aplicacion se quedaria en blanco para todos — el
    /// peor de los errores posibles, y causado por una regla nueva, no por un
    /// permiso que alguien decidiera.
    ///
    /// Asi que la regla es: mientras a este usuario NADIE le haya configurado
    /// una clave de matriz, la matriz se comporta como hoy (puede). El dia que
    /// se le configure una —cualquiera de las dos—, la clave empieza a
    /// gobernar. El interruptor lo enciende quien reparte permisos, no el
    /// despliegue.
    /// </remarks>
    public bool PuedeOperarEnLaMatriz
    {
        get
        {
            if (_todoPermitido) return true;
            if (_tiene(ClavesDeCentros.MatrizOperar)) return true;
            return !TieneAlgunaClaveDeMatriz;
        }
    }

    /// <summary>Puede CONSULTAR la matriz. Mismo criterio que operar.</summary>
    public bool PuedeVerLaMatriz
    {
        get
        {
            if (_todoPermitido) return true;
            if (_tiene(ClavesDeCentros.MatrizVer) || _tiene(ClavesDeCentros.MatrizOperar)) return true;
            return !TieneAlgunaClaveDeMatriz;
        }
    }

    /// <summary>
    /// Si alguien le configuro explicitamente el gobierno de la matriz. Se mira
    /// sobre el snapshot CRUDO y no con <c>Tiene</c>: la pregunta no es "puede",
    /// es "se lo configuraron".
    /// </summary>
    private bool TieneAlgunaClaveDeMatriz => _permisos.Any(p =>
        string.Equals(p, ClavesDeCentros.MatrizVer, StringComparison.OrdinalIgnoreCase)
        || string.Equals(p, ClavesDeCentros.MatrizOperar, StringComparison.OrdinalIgnoreCase));
}
