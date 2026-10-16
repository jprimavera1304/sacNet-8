namespace ISL_Service.Application.Centros;

/// <summary>
/// COMO LLEGA EL CENTRO EN LA PETICION. Cabecera X-Centro, entero, 0 = matriz.
/// </summary>
/// <remarks>
/// POR QUE UNA CABECERA Y NO UN CLAIM DEL TOKEN
/// --------------------------------------------
/// Porque el centro CAMBIA durante la sesion: es un selector, no una identidad.
/// Meterlo en el JWT obligaria a re-firmar el token cada vez que alguien
/// cambia de sucursal.
///
/// Y NO DEBILITA NADA, por el mismo motivo que X-Cliente-App no lo debilita
/// (ver CanalDeAcceso): esta cabecera no AFIRMA un privilegio, solo ESCOGE.
/// Quien la manda no dice "puedo entrar al 2007", dice "quiero el 2007"; si
/// puede o no lo decide el servidor contra el catalogo y contra sus permisos,
/// en cada peticion. Mentir aqui no abre nada — a lo mas consigue un 409.
///
/// LO QUE NUNCA VIAJA ES EL NOMBRE DE LA BASE. Ni de ida ni de vuelta. El
/// cliente manda un IDCentro y el servidor resuelve el nombre contra
/// dbo.CentrosServicio. Dejar que el cliente nombre la base seria dejarle
/// escoger a que base entra, que es otra cosa completamente distinta.
///
/// UN VALOR BASURA NO SE INTERPRETA COMO MATRIZ. "abc" o "-3" no se redondean
/// a 0: eso convertiria un error del cliente en datos de la matriz, que es
/// justo la confusion que todo este contrato intenta evitar. Se rechaza.
/// </remarks>
public static class CentroPedido
{
    /// <summary>El nombre de la cabecera. Un solo sitio.</summary>
    public const string Cabecera = "X-Centro";

    /// <summary>La matriz.</summary>
    public const int Matriz = 0;

    /// <summary>
    /// Lee el valor de la cabecera. Ausente o vacia = <see cref="Matriz"/>.
    /// Devuelve false si el valor no es un centro posible.
    /// </summary>
    public static bool Interpretar(string? valorDeLaCabecera, out int idCentro)
    {
        idCentro = Matriz;

        var texto = (valorDeLaCabecera ?? "").Trim();
        if (texto.Length == 0) return true;                 // ausente = matriz

        if (!int.TryParse(texto, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var numero))
            return false;

        if (numero < 0) return false;

        idCentro = numero;
        return true;
    }

    /// <summary>
    /// Cuando el cuerpo trae su propio idCentro (los reportes lo hacen hoy),
    /// MANDA EL DEL CUERPO.
    /// </summary>
    /// <remarks>
    /// POR QUE GANA EL CUERPO Y NO LA CABECERA
    /// ---------------------------------------
    /// Porque el cuerpo es lo que YA FUNCIONA. `POST /api/reportes/ventas/*/generar`
    /// manda `idCentro` desde el web y desde los APK que ya estan instalados.
    /// El dia del despliegue, un APK viejo seguira mandando el cuerpo y NINGUNA
    /// cabecera; si la cabecera ganara, ese APK sacaria reportes de la matriz
    /// creyendo que son del centro. Nadie pierde nada: la cabecera es lo nuevo,
    /// y lo nuevo cede.
    ///
    /// EL DESACUERDO SE REGISTRA. Si los dos vienen y no coinciden, no es un
    /// error del usuario: es un cliente que tiene dos ideas distintas de donde
    /// esta. Se escoge el del cuerpo y se deja escrito, porque si alguna vez
    /// pasa hay que poder verlo en el log en vez de deducirlo.
    /// </remarks>
    /// <returns>El centro que gana, y si hubo desacuerdo que registrar.</returns>
    public static (int Gana, bool Desacuerdo) Conciliar(int idCentroDelCuerpo, int idCentroDeLaCabecera)
    {
        /* El cuerpo "no dice nada" cuando viene en 0: hoy eso significa matriz
           y no se puede distinguir de "no lo mandaron". Con cabecera puesta y
           cuerpo en 0 se respeta la cabecera, que es lo unico que dijo algo. */
        if (idCentroDelCuerpo <= 0) return (idCentroDeLaCabecera, false);

        return (idCentroDelCuerpo, idCentroDelCuerpo != idCentroDeLaCabecera && idCentroDeLaCabecera != Matriz);
    }
}
