using System.Data;
using System.Globalization;
using ISL_Service.Application.DTOs.Empleados;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// La baja logica de cualquier renglon de legacy, sin perderle los datos.
///
/// EL PROBLEMA QUE RESUELVE
/// -----------------------
/// Los sp_n_Actualizar* de legacy hacen un UPDATE que pisa TODAS las columnas,
/// no solo el estatus. El DELETE de MacServicios2 llamaba a ese mismo
/// procedimiento con lo que trajera el cuerpo de la peticion, y sus
/// controladores solo exigian el id. Resultado comprobado leyendo los UPDATE de
/// los procedimientos de catalogo, clientes, productos y proveedores: una baja
/// hecha "bien" segun la validacion de legacy dejaba el renglon con el nombre
/// vacio, los telefonos vacios y los ids en cero. La baja logica se convertia en
/// un borrado de datos que dejaba la llave viva — lo peor de los dos mundos,
/// porque las ventas viejas siguen apuntando a un cliente que ya no se llama
/// como se llamaba.
///
/// COMO LO RESUELVE
/// ---------------
/// Primero LEE del renglon las columnas que el UPDATE va a pisar, y luego se las
/// devuelve al mismo procedimiento junto con el estatus nuevo. Lo unico que
/// cambia es el estatus.
///
/// POR QUE UN SELECT DIRECTO Y NO EL sp_n_Consulta* DEL MODULO
/// ----------------------------------------------------------
/// Porque esos procedimientos terminan en EXEC (@Sql) y devuelven veinte
/// columnas ya formateadas para un combo de WinForms (los *Ftm, los textos
/// preconcatenados); aqui hacen falta exactamente las que el UPDATE va a pisar,
/// con su tipo original. Es la misma decision que ya tomo EmpleadosRepository
/// para leer un dato suelto de una tabla de legacy.
///
/// LIMITE HONESTO: esto es correcto para un UPDATE que pisa columnas, no para
/// un procedimiento que ademas mueve otras tablas segun el estatus. Donde el
/// procedimiento hace mas que el UPDATE (cambios de vigencia, cortes, compras),
/// la baja tiene su propio sp_n_Cancelar* y no se usa esto.
/// </summary>
public static class BajaLogicaLegacy
{
    /// <summary>
    /// De donde se lee, a quien se le devuelve y que columnas hay que acarrear.
    /// </summary>
    /// <param name="Tabla">Nombre de la tabla, con corchetes si lleva espacios: "[Catalogo Bancos]".</param>
    /// <param name="ColumnaId">La llave en la tabla.</param>
    /// <param name="ParamId">
    /// Como se llama esa llave en el procedimiento. No siempre coincide:
    /// la columna es IDCondicionesCredito y el parametro @IDCondicionCredito.
    /// </param>
    /// <param name="Sp">El sp_n_Actualizar* del modulo.</param>
    /// <param name="Conservar">Pares (parametro, columna) que se leen y se devuelven sin tocar.</param>
    /// <param name="Fijos">
    /// Parametros con valor fijo que el procedimiento pide y no salen de la
    /// tabla: el @Accion = 0 de clientes, por ejemplo.
    /// </param>
    /// <param name="PideUsuario">El procedimiento recibe @IDUsuario (la firma de quien da la baja).</param>
    /// <param name="PideEquipo">El procedimiento recibe @Equipo.</param>
    /// <param name="ParamUsuario">Como se llama el parametro del usuario, cuando no es @IDUsuario.</param>
    public sealed record Descriptor(
        string Tabla,
        string ColumnaId,
        string ParamId,
        string Sp,
        (string Param, string Columna)[] Conservar,
        (string Param, object Valor)[]? Fijos = null,
        bool PideUsuario = false,
        bool PideEquipo = false,
        string ParamUsuario = "@IDUsuario");

    /// <summary>
    /// Cambia el estatus conservando los datos. Devuelve null cuando el renglon
    /// no existe, para que el controlador pueda contestar 404 en vez de un exito
    /// que no paso nada.
    /// </summary>
    public static async Task<ResultadoLegacy?> CambiarAsync(
        SqlConnection conn,
        Descriptor def,
        object id,
        int idStatus,
        int idUsuario,
        string equipo,
        CancellationToken ct)
    {
        // 1. Los valores actuales. Sin esto el UPDATE del procedimiento los
        //    dejaria vacios.
        var columnas = string.Join(", ", def.Conservar.Select(x => Corchetes(x.Columna)));
        var valores = new Dictionary<string, object?>(StringComparer.Ordinal);

        await using (var lectura = new SqlCommand(
            $"SELECT {columnas} FROM dbo.{def.Tabla} WHERE {Corchetes(def.ColumnaId)} = @Id", conn))
        {
            lectura.Parameters.AddWithValue("@Id", id);
            await using var reader = await lectura.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return null;

            for (var i = 0; i < def.Conservar.Length; i++)
                valores[def.Conservar[i].Param] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        }

        // 2. El mismo procedimiento de siempre, con el estatus nuevo y lo demas
        //    igual.
        await using var cmd = new SqlCommand(def.Sp, conn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue(def.ParamId, id);
        cmd.Parameters.AddWithValue("@IDStatus", idStatus);

        foreach (var par in def.Conservar)
            cmd.Parameters.AddWithValue(par.Param, valores[par.Param] ?? DBNull.Value);

        foreach (var par in def.Fijos ?? Array.Empty<(string, object)>())
            cmd.Parameters.AddWithValue(par.Param, par.Valor);

        if (def.PideUsuario) cmd.Parameters.AddWithValue(def.ParamUsuario, idUsuario);
        if (def.PideEquipo) cmd.Parameters.AddWithValue("@Equipo", equipo);

        var ds = new DataSet();
        using var adapter = new SqlDataAdapter(cmd);
        await Task.Run(() => adapter.Fill(ds), ct);
        var dt = ds.Tables.Count > 0 ? ds.Tables[0] : new DataTable();

        return LeerResultado(dt);
    }

    /// <summary>
    /// Corchetea el nombre si hace falta. Las tablas de Mac31 tienen columnas
    /// con espacios: "Apellido Paterno", "Limite Credito", "Codigo Postal".
    /// </summary>
    private static string Corchetes(string nombre)
        => nombre.StartsWith('[') ? nombre : $"[{nombre}]";

    /// <summary>El par result/mensaje que devuelven todos los sp_n_ de escritura.</summary>
    public static ResultadoLegacy LeerResultado(DataTable? dt)
    {
        if (dt == null || dt.Rows.Count == 0)
            return new ResultadoLegacy { Result = -1, Mensaje = "El procedimiento no devolvio resultado." };

        var row = dt.Rows[0];
        return new ResultadoLegacy
        {
            Result = LeerInt(row, "result"),
            Mensaje = LeerString(row, "mensaje")
        };
    }

    public static int LeerInt(DataRow row, string columna)
    {
        if (!row.Table.Columns.Contains(columna)) return 0;
        var v = row[columna];
        return v == DBNull.Value ? 0 : Convert.ToInt32(v, CultureInfo.InvariantCulture);
    }

    public static string LeerString(DataRow row, string columna)
    {
        if (!row.Table.Columns.Contains(columna)) return string.Empty;
        var v = row[columna];
        return v == DBNull.Value ? string.Empty : Convert.ToString(v, CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
