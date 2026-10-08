using System.Data;
using System.Globalization;
using ISL_Service.Application.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;

namespace ISL_Service.Infrastructure.Repositories;

/*
  A QUE AGENTE ESTA AMARRADO EL USUARIO QUE PREGUNTA. UN SOLO LUGAR.

  POR QUE EXISTE ESTA CLASE
  -------------------------
  Ya paso: un usuario amarrado a un agente vio el padron COMPLETO, 1,042
  clientes de 15 agentes, en una pantalla que solo le tocaba a uno. No fue un
  WHERE mal escrito. Fue que la resolucion del filtro se saltaba en silencio y
  "no se pudo resolver" devolvia el MISMO 0 que significa "ve todos". Dos cosas
  opuestas con el mismo valor de retorno.

  Un filtro de seguridad que falla ABIERTO es peor que no tener filtro, porque
  el de arriba cree que esta puesto: nadie revisa una pantalla que ya "tiene el
  filtro". Asi que aqui no se devuelve 0 cuando no se sabe. Si no se puede
  resolver, no se contesta: ConflictException (409) con el nombre del script que
  falta, y se nota en la pantalla, no en una linea del registro que nadie mira.

  QUE SIGNIFICA EL NUMERO
  -----------------------
    0   = este usuario ve TODOS los clientes (no esta amarrado a nadie).
    > 0 = ve SOLO los clientes de ese agente.

  Y hay dos caminos legitimos al 0, los dos son DATOS y no fallas:
    * idUsuario <= 0: quien pregunta no vive en la tabla Usuarios de Mac31 (un
      SuperAdmin del web, por ejemplo). No esta amarrado a nadie y no se gasta
      el viaje a la base.
    * el procedimiento contesta cero filas, o IDAgenteFiltro en nulo: el usuario
      si existe y NO esta amarrado.

  Lo que NO es un 0: que el procedimiento no este desplegado, o que haya dejado
  de devolver la columna IDAgenteFiltro. Eso es "no se sabe", y no se sabe no es
  "ve todos".

  CONVERGENCIA PENDIENTE
  ----------------------
  VentasPedidoCapturaRepository tiene su propia copia de esto
  (ConsultarAgenteFiltroAsync / ConsultarAgenteFiltroConCacheAsync), escrita
  antes que esta clase y con el mismo comportamiento, incluida la llave de cache
  "agenteFiltro:{idUsuario}" -- por eso ClaveDeCache la reproduce tal cual: las
  dos comparten entrada y un cambio de agente se nota en las dos a la vez.
  Ese archivo NO SE TOCA en esta entrega (esta en obra). Cuando salga de obra,
  sus dos metodos privados deben borrarse y delegar aqui; mientras tanto,
  cualquier cambio de conducta hay que hacerlo en los dos lados.
*/
public static class FiltroDeAgente
{
    public const string SpAgenteDelUsuario = "sp_w_ConsultaAgenteUsuario";

    /// <summary>
    /// La columna que trae la respuesta. Si falta, el procedimiento cambio de
    /// forma y no se puede confiar en lo que devuelve.
    /// </summary>
    private const string ColumnaAgenteFiltro = "IDAgenteFiltro";

    /// <summary>
    /// Lo que se le dice al usuario cuando no se puede resolver su filtro. Es el
    /// mismo texto que ya usa VentasPedidoCapturaRepository: la pantalla no tiene
    /// por que decir dos cosas distintas del mismo problema.
    /// </summary>
    private const string MensajeNoSeResuelve =
        "No se puede resolver el filtro por agente de este usuario, asi que no se devuelve el padron de clientes. Falta desplegar sp_w_ConsultaAgenteUsuario en esta base.";

    /// <summary>
    /// A que agente esta amarrado un usuario no cambia en caliente: hay que
    /// editar Usuarios.EsAgente o WUsuarioAgenteConfig a mano. 5 minutos para no
    /// pegarle a la base en cada tecla del buscador, pero corto como para que un
    /// cambio se note sin reiniciar el servicio.
    /// </summary>
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    /// <summary>
    /// La llave de cache. La comparte a proposito con
    /// VentasPedidoCapturaRepository para que las dos mitades vean el mismo
    /// numero mientras esa copia siga viva.
    /// </summary>
    public static string ClaveDeCache(int idUsuario) => $"agenteFiltro:{idUsuario}";

    /// <summary>
    /// Devuelve el agente al que esta amarrado el usuario: 0 = ve todos los
    /// clientes, mayor que 0 = ve solo los de ese agente.
    /// </summary>
    /// <param name="abrirConexion">
    /// Fabrica de conexiones del repositorio que llama (su GetConnection). La
    /// conexion se abre y se cierra aqui: cada repositorio tiene su propia
    /// resolucion de cadena y no se duplica.
    /// </param>
    /// <exception cref="ConflictException">
    /// Cuando NO se puede saber a que agente pertenece el usuario. No se degrada
    /// a 0 -- ver el comentario de arriba de la clase.
    /// </exception>
    public static async Task<int> ResolverAsync(
        Func<SqlConnection> abrirConexion,
        IMemoryCache cache,
        ILogger logger,
        int idUsuario,
        CancellationToken ct)
    {
        // Quien no tiene id de Mac31 no esta amarrado a ningun agente y no hay
        // nada que preguntarle a la base.
        if (idUsuario <= 0) return 0;

        var clave = ClaveDeCache(idUsuario);
        if (cache.TryGetValue(clave, out int cacheado)) return cacheado;

        var filtro = await ConsultarAsync(abrirConexion, logger, idUsuario, ct);

        cache.Set(clave, filtro, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheTtl
        });

        return filtro;
    }

    private static async Task<int> ConsultarAsync(
        Func<SqlConnection> abrirConexion,
        ILogger logger,
        int idUsuario,
        CancellationToken ct)
    {
        try
        {
            await using var conn = abrirConexion();
            await conn.OpenAsync(ct);

            await using var cmd = new SqlCommand(SpAgenteDelUsuario, conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);

            var tabla = new DataTable();
            await using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                tabla.Load(reader);
            }

            // Cero filas = "este usuario no esta amarrado a ningun agente". Es un
            // dato, no una falla: ve todos.
            if (tabla.Rows.Count == 0) return 0;

            /*
              LA COLUMNA TIENE QUE ESTAR. Si alguien le cambio la forma al
              procedimiento y ya no la devuelve, lo que tenemos no es "ve todos":
              es un resultado que no entendemos. Antes esto caia al 0 por la
              puerta de atras -- un lector por nombre devuelve 0 cuando la
              columna no existe -- y era la misma falla abierta, solo que mas
              dificil de ver.
            */
            if (!tabla.Columns.Contains(ColumnaAgenteFiltro))
            {
                logger.LogError(
                    "{Sp} contesto sin la columna {Columna}: NO se puede resolver el filtro por agente del usuario {IDUsuario}, asi que la consulta se RECHAZA en vez de devolver el padron completo.",
                    SpAgenteDelUsuario, ColumnaAgenteFiltro, idUsuario);

                throw new ConflictException(
                    MensajeNoSeResuelve,
                    $"El procedimiento {SpAgenteDelUsuario} ya no devuelve la columna {ColumnaAgenteFiltro}.");
            }

            var valor = tabla.Rows[0][ColumnaAgenteFiltro];

            // Nulo = tampoco esta amarrado. Igual que cero filas.
            if (valor is null || valor == DBNull.Value) return 0;

            return int.TryParse(Convert.ToString(valor, CultureInfo.InvariantCulture), out var agente) && agente > 0
                ? agente
                : 0;
        }
        catch (SqlException ex) when (ProcedimientoNoDesplegado(ex))
        {
            /*
              SE FALLA CERRADO, Y ES A PROPOSITO.

              Devolver 0 aqui no degradaria el servicio: APAGARIA el filtro. En
              Tauro (Produccion_svr) el procedimiento no esta desplegado, asi que
              esto no es hipotetico: era el comportamiento de todos los dias.
            */
            logger.LogError(ex,
                "{Sp} no esta desplegado en esta base: NO se puede resolver el filtro por agente del usuario {IDUsuario}, asi que la consulta se RECHAZA en vez de devolver el padron completo.",
                SpAgenteDelUsuario, idUsuario);

            throw new ConflictException(MensajeNoSeResuelve, ex.Message);
        }
    }

    /// <summary>
    /// 2812 es "Could not find stored procedure". Se exige ADEMAS que el mensaje
    /// nombre a este procedimiento: un 2812 de otro script es otro problema y no
    /// se puede confundir con "falta desplegar el filtro".
    /// </summary>
    private static bool ProcedimientoNoDesplegado(SqlException ex)
        => ex.Number == 2812
           && (ex.Message ?? string.Empty).Contains(SpAgenteDelUsuario, StringComparison.OrdinalIgnoreCase);
}
