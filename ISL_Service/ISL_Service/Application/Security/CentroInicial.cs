namespace ISL_Service.Application.Security;

/// <summary>
/// CON QUE CENTRO SE ABRE LA SESION.
/// </summary>
/// <remarks>
/// LAS REGLAS, EN ORDEN
/// --------------------
///   1. Si hay PREFERENCIA GUARDADA del usuario y SIGUE SIENDO VALIDA, esa.
///   2. Si no, y solo hay UNO autorizado, ese.
///   3. Si hay varios, EL PRIMERO DEL CATALOGO.
///   4. Si no hay ninguno autorizado, la matriz.
///
/// "SIGUE SIENDO VALIDA" ES LA MITAD IMPORTANTE
/// --------------------------------------------
/// Una preferencia guardada se queda guardada; lo que cambia es el permiso. El
/// dia que a alguien le quitan el centro 2007, su preferencia sigue diciendo
/// 2007. Si se respetara a ciegas, esa persona abriria la sesion contra un
/// centro que ya no tiene autorizado: o el servidor le contesta 409 en la
/// primera pantalla —y parece que el sistema esta roto— o, mucho peor, alguien
/// se olvida de revisarlo mas abajo y le enseña datos de un centro que no
/// deberia ver. Por eso la preferencia se CRUZA contra lo autorizado AQUI, y si
/// no esta, se cae al primero del catalogo sin avisar feo.
///
/// POR QUE LA MATRIZ VA PRIMERA EN EL CATALOGO
/// -------------------------------------------
/// Porque es lo que pasa hoy: todo sale de la matriz hasta que alguien elige
/// otra cosa. Poner un centro de servicio por delante cambiaria, el dia del
/// despliegue y sin que nadie lo pidiera, la base de la que salen los datos de
/// doce personas en Zaragoza. "El primero del catalogo" tiene que significar lo
/// que ya significa hoy.
///
/// DONDE VIVE LA PREFERENCIA: EN EL SERVIDOR.
/// ------------------------------------------
/// En <c>dbo.WCentroPreferido</c>, una fila por usuario web y empresa, que lee y
/// escribe <c>PreferenciaDeCentroService</c>. NO en el telefono ni en el
/// navegador: el centro activo decide DE QUE BASE salen los datos, y un dato que
/// decide eso no puede vivir donde el usuario lo puede editar. El telefono si lo
/// recuerda tambien (contrato §6, KeyValueStorage) pero como cache de arranque,
/// y lo valida contra el catalogo al entrar; la verdad es la del servidor.
///
/// Esta clase NO lee la base a proposito: recibe lo autorizado y la preferencia,
/// y decide. Asi las cuatro reglas se pueden probar sin una base de datos
/// delante, que es la unica forma de que alguien las revise.
/// </remarks>
public static class CentroInicial
{
    /// <summary>La matriz, que en la cabecera X-Centro es el 0.</summary>
    public const int Matriz = 0;

    /// <summary>
    /// El centro con el que se abre la sesion.
    /// </summary>
    /// <param name="matrizAutorizada">Si puede consultar la matriz.</param>
    /// <param name="centrosAutorizados">
    /// Los IDCentro que puede consultar, EN EL ORDEN DEL CATALOGO. El orden
    /// importa: la regla 3 dice "el primero", y el primero es el que el catalogo
    /// enseña primero, no el de numero mas bajo ni el que la base devuelva.
    /// </param>
    /// <param name="preferencia">
    /// Lo que el usuario eligio la ultima vez, o <c>null</c> si nunca eligio.
    /// <see cref="Matriz"/> es una preferencia valida y significa "la matriz".
    /// </param>
    public static int Elegir(
        bool matrizAutorizada,
        IReadOnlyList<int>? centrosAutorizados,
        int? preferencia)
    {
        var centros = centrosAutorizados ?? Array.Empty<int>();

        // 1. La preferencia, si sigue valiendo.
        if (preferencia is int elegido && EsValida(elegido, matrizAutorizada, centros))
            return elegido;

        // 2 y 3. El primero del catalogo, que es la matriz cuando esta
        //        autorizada. Con uno solo autorizado el resultado es ese uno,
        //        asi que las dos reglas caen en la misma linea.
        if (matrizAutorizada) return Matriz;
        if (centros.Count > 0) return centros[0];

        /*
          4. Nada autorizado. Se cae a la matriz y NO se inventa un centro.
             Pasa en una base sin centros, y pasaria si alguien quedara sin
             ninguna clave: contestar la matriz deja que el resto del sistema
             decida con sus propios permisos, que es como funciona hoy. Elegir un
             centro aqui seria mandar a alguien a una base que no tiene
             autorizada por el camino mas silencioso que hay.
        */
        return Matriz;
    }

    /// <summary>
    /// Si esa preferencia todavia se puede usar. Publica porque el que la GUARDA
    /// tambien tiene que preguntarlo: guardar una preferencia que ya no vale es
    /// dejar una trampa para el siguiente arranque.
    /// </summary>
    public static bool EsValida(
        int centro,
        bool matrizAutorizada,
        IReadOnlyList<int>? centrosAutorizados)
    {
        if (centro <= Matriz) return matrizAutorizada;
        return (centrosAutorizados ?? Array.Empty<int>()).Contains(centro);
    }
}
