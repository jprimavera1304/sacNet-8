using System.Collections.Concurrent;

namespace ISL_Service.Infrastructure.Concurrencia;

/// <summary>
/// Cuando varios piden lo mismo a la vez, lo trae UNO y los demas esperan.
/// </summary>
/// <remarks>
/// EL PROBLEMA QUE RESUELVE, MEDIDO.
///
/// El patron que habia repetido en seis sitios era este:
///
///     if (cache.TryGetValue(llave, out var guardado)) return guardado;
///     var valor = await TraerDeLaBase();      // &lt;-- aqui entran TODOS
///     cache.Set(llave, valor, plazo);
///
/// Leer y escribir esta bien; lo que falta es que entre las dos lineas caben
/// todos los demas. Mientras el primero esta trayendo el dato, el cache sigue
/// vacio, asi que el segundo tambien falla el intento, y el tercero, y el
/// noveno. Es una estampida: N peticiones hacen N veces el mismo trabajo.
///
/// En un portatil no se nota porque el trabajo dura milisegundos. En
/// produccion la base esta al otro lado de internet -56 ms por viaje medidos
/// contra mactauro.ddns.net- y ese mismo trabajo son segundos. Medido en el
/// banco de pruebas con la latencia reproducida: nueve agentes pidiendo
/// clientes a la vez con el cache frio tardaban 36 s. Uno solo, 2,7 s.
///
/// Y hay un segundo efecto peor que el trabajo repetido: cada reconstruccion
/// ocupa un hilo del ThreadPool. El servidor tiene CUATRO nucleos, asi que el
/// pool arranca con cuatro hilos y a partir de ahi inyecta ~1 por segundo. Las
/// otras cinco peticiones no es que vayan lentas: es que todavia no tienen
/// hilo. De ahi los 11-27 s de los registros de IIS.
///
/// LO QUE NO ES. Esto no alarga la vida de ningun dato ni cambia ningun plazo:
/// el que gana guarda con la misma vigencia de siempre y los que esperan se
/// llevan ESE valor, recien traido. No puede ensenar nada mas viejo que lo que
/// ensenaba antes.
///
/// POR QUE EL CONSTRUCTOR NO RECIBE CancellationToken. Si el trabajo compartido
/// se atara al token del PRIMERO que llego, bastaria con que ese cerrara la app
/// para tumbar a los otros ocho, que no han hecho nada malo. El corte lo pone
/// el CommandTimeout de la consulta, que es de todos.
/// </remarks>
public static class UnaSolaVez
{
    /*
      Las construcciones EN VUELO, por llave. No es un cache: una entrada vive
      lo que dura su consulta y se quita en el `finally`. El cache de verdad es
      el que pasa el llamador en `guardar`.

      `Lazy` con ExecutionAndPublication es lo que garantiza que la fabrica se
      ejecuta UNA vez aunque entren veinte hilos a la vez; `GetOrAdd` por si
      solo no lo garantiza (puede llamar a la fabrica varias veces y descartar
      las sobrantes, que aqui significaria varias consultas).
    */
    private static readonly ConcurrentDictionary<string, Lazy<Task<object>>> _enVuelo = new(StringComparer.Ordinal);

    /// <param name="llave">Identifica el dato. Dos llamadas con la misma llave se juntan.</param>
    /// <param name="leer">Mira el cache del llamador. Devuelve (true, valor) si ya estaba.</param>
    /// <param name="guardar">Guarda en el cache del llamador, con el plazo que ese cache decida.</param>
    /// <param name="construir">Lo caro. Solo lo corre uno.</param>
    public static async Task<T> HazloAsync<T>(
        string llave,
        Func<(bool Hay, T? Valor)> leer,
        Action<T> guardar,
        Func<Task<T>> construir) where T : notnull
    {
        var (hay, valor) = leer();
        if (hay && valor is not null) return valor;

        /* El guardado va DENTRO de la fabrica a proposito: asi lo hace quien
           construye y una sola vez. Puesto despues del await lo harian los
           nueve, y con expiracion relativa cada rafaga correria el plazo hacia
           adelante y el dato no caducaria nunca. */
        var perezoso = _enVuelo.GetOrAdd(
            llave,
            _ => new Lazy<Task<object>>(
                async () =>
                {
                    var traido = await construir();
                    guardar(traido);
                    return (object)traido!;
                },
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return (T)await perezoso.Value;
        }
        finally
        {
            /* Fuera, para que la proxima vez que el cache venza se vuelva a
               construir. Si alguien acaba de tomar esta misma referencia, da
               igual: ya esta esperando la MISMA tarea. */
            _enVuelo.TryRemove(new KeyValuePair<string, Lazy<Task<object>>>(llave, perezoso));
        }
    }
}
