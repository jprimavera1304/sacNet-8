using System.Collections.Concurrent;
using System.Data;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Data;

/*
  QUE SE PUEDE ABANDONAR A MEDIAS Y QUE NO.

  Una lectura se puede cancelar: si quien pregunto ya se fue, abandonarla libera
  el hilo y no deja nada abierto. Una ESCRITURA de legacy no.

  POR QUE NO. Los procedimientos de legacy abren su propia transaccion dentro y
  corren con XACT_ABORT OFF. Cuando la cancelacion llega de verdad a SQL Server
  -que es lo que hace `ExecuteReaderAsync(ct)`-, el lote se aborta a media
  transaccion y esa transaccion NO se deshace. La conexion vuelve al pool con
  `@@TRANCOUNT` mayor que cero, arrastrando candados exclusivos sobre las tablas
  que estaba tocando, y el `BEGIN CATCH` del procedimiento tampoco corre, porque
  una cancelacion no es un error de T-SQL: ni se deshace, ni se libera el
  applock, ni queda rastro. Quien tome esa conexion despues espera esos candados
  hasta agotar su CommandTimeout, que en ventas son 500 segundos.

  Eso es lo que convertia un agente que pierde la señal a mitad de guardar en
  todos los demas bloqueados.

  POR QUE NO SE DECIDE EN CADA LLAMADA. Son del orden de cien escrituras
  repartidas en veinte repositorios. Marcarlas una por una garantiza que tres se
  queden sin marcar, y las tres que se queden seran las que mas tarden en
  aparecer. Aqui la decision se toma UNA vez, leyendo el propio comando.

  DE QUE LADO SE FALLA. Lo que no se reconoce como consulta se trata como
  escritura. Equivocarse hacia "escritura" cuesta un hilo retenido mientras el
  procedimiento termina -decimas de segundo en una escritura real-; equivocarse
  hacia "consulta" cuesta un pedido a medias y a los demas bloqueados. No es
  simetrico, asi que no se elige por elegancia.

  LO QUE SIGUE RECIBIENDO EL TOKEN: `conn.OpenAsync(ct)`. Cancelar ANTES de que
  el lote arranque es gratis y es justo lo que libera el hilo.
*/
public static class CancelacionSegura
{
    private static readonly string[] VerbosDeConsulta =
    {
        "Consulta", "Consultar", "rpt", "Buscar", "Obtener", "Listar"
    };

    /*
      LOS QUE MIENTEN CON EL NOMBRE.

      `sp_n_ConsultaConstantes` se llama "Consulta" y hace el cierre de
      constantes: contadas en la base, siete sentencias de escritura. La unica
      forma de saberlo es haber leido su cuerpo, asi que el dia que aparezca
      otro igual este es el sitio donde se agrega.
    */
    private static readonly string[] ConsultasQueEscriben =
    {
        "sp_n_ConsultaConstantes"
    };

    /*
      Se memoriza por texto de comando porque esto se pregunta tambien dentro
      del bucle de filas, y ahi un analisis de cadena por renglon seria pagar
      por nada. El numero de procedimientos distintos es finito y pequeño.
    */
    private static readonly ConcurrentDictionary<string, bool> _memoria = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// El token si el comando solo lee; ninguno si escribe.
    /// </summary>
    public static CancellationToken SeñalPara(SqlCommand cmd, CancellationToken ct)
        => SoloLee(cmd) ? ct : CancellationToken.None;

    /// <summary>
    /// El token si el procedimiento solo lee; ninguno si escribe.
    /// </summary>
    public static CancellationToken SeñalPara(string procedimiento, CancellationToken ct)
        => EsNombreDeConsulta(procedimiento) ? ct : CancellationToken.None;

    private static bool SoloLee(SqlCommand cmd)
    {
        var texto = cmd.CommandText ?? string.Empty;
        if (texto.Length == 0) return false;

        return _memoria.GetOrAdd(texto, t => cmd.CommandType == CommandType.StoredProcedure
            ? EsNombreDeConsulta(t)
            : EsSqlDeSoloLectura(t));
    }

    /*
      SQL escrito a mano. Solo se da por lectura un SELECT o un WITH limpios: un
      "SELECT ... INTO", por ejemplo, crea una tabla y por tanto escribe.
    */
    private static bool EsSqlDeSoloLectura(string sql)
    {
        var t = sql.TrimStart();
        var empieza = t.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
                   || t.StartsWith("WITH", StringComparison.OrdinalIgnoreCase);

        if (!empieza) return false;

        return !t.Contains(" INTO ", StringComparison.OrdinalIgnoreCase);
    }

    private static bool EsNombreDeConsulta(string? procedimiento)
    {
        var nombre = (procedimiento ?? string.Empty).Trim();
        if (nombre.Length == 0) return false;

        // El esquema viaja en unos nombres y no en otros: "dbo.sp_n_..." y
        // "sp_n_..." conviven entre los repositorios.
        var punto = nombre.LastIndexOf('.');
        if (punto >= 0) nombre = nombre[(punto + 1)..];

        foreach (var mentiroso in ConsultasQueEscriben)
            if (nombre.Equals(mentiroso, StringComparison.OrdinalIgnoreCase))
                return false;

        // Se quita el prefijo de familia (sp_n_, sp_w_, sp_mo_) y se mira el verbo.
        var primero = nombre.IndexOf('_');
        if (primero >= 0)
        {
            var segundo = nombre.IndexOf('_', primero + 1);
            if (segundo >= 0) nombre = nombre[(segundo + 1)..];
        }

        foreach (var verbo in VerbosDeConsulta)
            if (nombre.StartsWith(verbo, StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }
}
