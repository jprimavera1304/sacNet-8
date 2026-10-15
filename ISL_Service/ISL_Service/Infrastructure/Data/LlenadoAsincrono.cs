using System.Data;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Data;

/// <summary>
/// Trae el resultado de un comando a un <see cref="DataSet"/> SIN bloquear un hilo.
/// </summary>
/// <remarks>
/// LO QUE SUSTITUYE Y POR QUE.
///
///     var ds = new DataSet();
///     using var adapter = new SqlDataAdapter(cmd);
///     await Task.Run(() =&gt; adapter.Fill(ds), ct);
///
/// `SqlDataAdapter.Fill` NO tiene version asincrona: lee de la red con el hilo
/// que lo llama, bloqueado, hasta que termina. Meterlo en un `Task.Run` no lo
/// vuelve asincrono: lo muda a un hilo del ThreadPool y lo bloquea ahi. Con la
/// base en la misma maquina eso dura milisegundos y da igual. Con la base al
/// otro lado de internet -56 ms por viaje- dura segundos, y durante esos
/// segundos el hilo no sirve para nada mas.
///
/// El servidor tiene CUATRO nucleos. El ThreadPool arranca con cuatro hilos de
/// trabajo y, cuando se quedan ocupados, inyecta uno nuevo por segundo. Nueve
/// agentes capturando a la vez ponen nueve de estos en cola: los cuatro
/// primeros arrancan y el noveno espera ~5 s a que exista un hilo para el,
/// ANTES de hacer nada. Eso es lo que convierte una consulta de 350 ms en una
/// respuesta de 20 s, y por eso el sintoma aparece solo cuando coinciden
/// varios: suelto, el mismo codigo contesta rapido.
///
/// `Task.Run` es aqui PEOR que llamar a `Fill` directo: el `Fill` directo usa
/// el hilo que la peticion ya tiene, mientras que el `Task.Run` encola un
/// trabajo nuevo que ademas tiene que esperar turno.
///
/// Lo de abajo usa `ExecuteReaderAsync` + `ReadAsync`, que sueltan el hilo en
/// cada espera de red de verdad. Devuelve lo mismo que `Fill`: un DataSet con
/// una tabla por conjunto de resultados, con los mismos nombres de columna.
/// </remarks>
public static class LlenadoAsincrono
{
    public static async Task<DataSet> LlenarAsync(SqlCommand cmd, CancellationToken ct)
    {
        var ds = new DataSet();
        await using var lector = await cmd.ExecuteReaderAsync(ct);

        var numero = 0;
        do
        {
            /* Un conjunto de resultados sin columnas no es una tabla: es el
               recuento de filas de un UPDATE/INSERT dentro del procedimiento.
               `Fill` tampoco lo agrega, asi que se salta para no cambiar la
               forma de lo que reciben los repositorios. */
            if (lector.FieldCount == 0) continue;

            var tabla = new DataTable("Table" + (numero == 0 ? string.Empty : numero.ToString()));
            numero++;

            for (var i = 0; i < lector.FieldCount; i++)
            {
                var nombre = lector.GetName(i);
                if (string.IsNullOrEmpty(nombre)) nombre = "Column" + i;

                /* Nombres repetidos: `Fill` les pone un sufijo en vez de
                   reventar, y hay procedimientos legacy que devuelven la misma
                   columna dos veces. Se imita ese comportamiento. */
                var final = nombre;
                var sufijo = 1;
                while (tabla.Columns.Contains(final)) final = nombre + sufijo++;

                tabla.Columns.Add(final, lector.GetFieldType(i) ?? typeof(object));
            }

            var valores = new object[lector.FieldCount];
            while (await lector.ReadAsync(ct))
            {
                lector.GetValues(valores);
                tabla.Rows.Add(valores);
            }

            ds.Tables.Add(tabla);
        } while (await lector.NextResultAsync(ct));

        return ds;
    }
}
