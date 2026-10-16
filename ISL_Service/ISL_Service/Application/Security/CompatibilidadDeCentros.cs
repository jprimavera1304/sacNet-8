namespace ISL_Service.Application.Security;

/// <summary>
/// QUE NADIE PIERDA EL ACCESO QUE YA TENIA EL DIA QUE SUBA LA FAMILIA NUEVA.
/// </summary>
/// <remarks>
/// LA REGLA, TAL CUAL SE PIDIO
/// ---------------------------
/// "Ningun empleado que actualmente tenga acceso autorizado debe perderlo por la
/// actualizacion" y "no elimines los permisos actuales ni obligues a
/// configurarlos nuevamente".
///
/// Eso no se cumple sembrando las claves nuevas: una clave recien sembrada no la
/// tiene nadie, asi que el dia del despliegue el selector de centros se apagaria
/// para TODOS -doce centros repartidos a mano en Zaragoza, perdidos- hasta que
/// alguien volviera a configurar persona por persona. Por eso la equivalencia se
/// aplica AQUI, sobre el juego de permisos, y no se le pide a nadie que reparta
/// nada.
///
/// LAS TRES EQUIVALENCIAS
/// ----------------------
///   1. `reportes.centros.cambiar`      vale como `centros.cambiar`.
///   2. `reportes.centros.&lt;n&gt;.ver`      vale como `centros.&lt;n&gt;.ver`.
///   3. `centros.matriz.ver` y `centros.matriz.operar` se CONCEDEN a todo el
///      mundo. Ver el apartado siguiente, que es el que hay que leer antes de
///      tocar esto.
///
/// POR QUE LA MATRIZ SE CONCEDE A TODOS Y NO "a quien tenga algo"
/// -------------------------------------------------------------
/// Porque hoy la matriz NO SE RIGE POR NINGUN PERMISO DE CENTRO. Se comprobo en
/// el codigo que decide: `ReportesVentasController.ExigirPermisoCentroAsync`
/// abre con `if (idCentro &lt;= 0) return null;` — la matriz pasa sin mirar
/// ninguna clave de centros— y todos los demas modulos del sistema trabajan
/// contra la matriz sin saber que existen los centros. O sea: el universo de
/// "quien hoy entra a la matriz" es TODO EL MUNDO que entra al sistema, y quien
/// hoy escribe en la matriz es todo el que tenga el permiso fino de su
/// operacion.
///
/// Si `centros.matriz.ver` se exigiera sin esta concesion, el dia del
/// despliegue el sistema entero dejaria de contestar: no es que se perdiera el
/// acceso a los centros, es que se perderia el acceso a todo. Y si
/// `centros.matriz.operar` se dejara fuera "porque operar no se deduce", nadie
/// podria volver a guardar nada en la empresa hasta que alguien repartiera una
/// clave nueva a toda la plantilla. Las dos cosas son exactamente lo que la
/// regla del usuario prohibe.
///
/// ESTO NO CONTRADICE "operar no se deduce de ver". Esa regla es sobre los
/// CENTROS, que es territorio nuevo: alla escribir es una facultad que hoy nadie
/// tiene y que se concede a mano, y por eso `centros.&lt;n&gt;.operar` NO aparece
/// nunca en la lista que devuelve este archivo —compruebalo en
/// <see cref="ClavesQueSeAgregan"/>, y hay prueba que falla si alguien lo
/// agrega—. La matriz no es territorio nuevo: es donde ya se trabaja.
///
/// LO QUE SE NIEGA A MANO SE RESPETA
/// ---------------------------------
/// La concesion de la matriz no pisa una NEGACION explicita (`Tipo = deny` en
/// WUsuarioPermiso). Sin eso, apagarle la matriz a alguien a proposito seria
/// imposible: la equivalencia se la devolveria en la siguiente lectura y la
/// pantalla de permisos mentiria. El resto de las equivalencias tampoco pisa una
/// negacion.
///
/// SE AGREGA, NUNCA SE QUITA
/// -------------------------
/// Este archivo solo puede AÑADIR claves. Si alguna vez tiene que borrar una,
/// esta en el lugar equivocado: quitar acceso es una decision que se toma en la
/// pantalla de permisos, no en una capa de compatibilidad.
/// </remarks>
public static class CompatibilidadDeCentros
{
    /// <summary>
    /// Las claves de la familia nueva que le toca a alguien por lo que YA tiene.
    /// Solo las que le faltan, y en orden, para que el resultado sea estable.
    /// </summary>
    /// <param name="concedidas">El juego de permisos del usuario.</param>
    /// <param name="negadas">
    /// Las claves que ese usuario tiene NEGADAS a proposito. Lo que esta aqui no
    /// se concede aunque la equivalencia lo pidiera.
    /// </param>
    public static IReadOnlyList<string> ClavesQueSeAgregan(
        IEnumerable<string>? concedidas,
        IEnumerable<string>? negadas = null)
    {
        var tiene = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in concedidas ?? Array.Empty<string>())
            if (!string.IsNullOrWhiteSpace(c)) tiene.Add(c.Trim());

        var nunca = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in negadas ?? Array.Empty<string>())
            if (!string.IsNullOrWhiteSpace(d)) nunca.Add(d.Trim());

        var faltan = new List<string>();

        void Agregar(string clave)
        {
            if (nunca.Contains(clave)) return;
            if (tiene.Contains(clave)) return;
            if (faltan.Contains(clave, StringComparer.OrdinalIgnoreCase)) return;
            faltan.Add(clave);
        }

        /* 3. La matriz, a todo el mundo. Va primero porque es la que sostiene el
              dia del despliegue; el apartado de arriba explica por que. */
        Agregar(PermisosDeCentros.MatrizVer);
        Agregar(PermisosDeCentros.MatrizOperar);

        /* 1. El selector. */
        if (tiene.Contains(PermisosDeCentros.CambiarLegado))
            Agregar(PermisosDeCentros.Cambiar);

        /* 2. Un centro por cada centro viejo. Se recorre lo que tiene y se
              traduce, en vez de pedir el catalogo de centros: asi funciona
              igual para un centro que ya se dio de baja y cuya clave alguien
              todavia conserva. */
        foreach (var clave in tiene)
        {
            if (!clave.StartsWith(PermisosDeCentros.PrefijoLegado, StringComparison.OrdinalIgnoreCase))
                continue;

            var numero = PermisosDeCentros.NumeroDeClaveDeVer(clave);
            if (numero is null) continue;

            Agregar(PermisosDeCentros.Ver(numero.Value));
        }

        /*
          NO SE AGREGA NINGUN `centros.<n>.operar`. A proposito, y con prueba.
          Escribir en un centro se concede a mano desde el web; deducirlo de
          `ver` repartiria de golpe la facultad de escribir en doce centros a
          todo el que hoy pueda consultarlos.
        */

        return faltan;
    }

    /// <summary>
    /// Mete en el juego de permisos las claves que toquen. Devuelve cuantas.
    /// </summary>
    public static int Aplicar(List<string> permisos, IEnumerable<string>? negadas = null)
    {
        ArgumentNullException.ThrowIfNull(permisos);

        var faltan = ClavesQueSeAgregan(permisos, negadas);
        permisos.AddRange(faltan);
        return faltan.Count;
    }
}
