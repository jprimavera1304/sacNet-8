using System.Data;
using System.Globalization;
using ISL_Service.Application.DTOs.Empleados;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// Las escrituras de los catalogos de Mac31. Llama a los sp_n_Insertar* y
/// sp_n_Actualizar* TAL CUAL: ningun sp_w_ de por medio, porque no hay nada que
/// agregarles y cada capa extra es un lugar donde se pierde el par
/// result/mensaje del que dependen los avisos de la pantalla
/// ("EL AGENTE YA ESTA REGISTRADO").
///
/// POR QUE LA BAJA LEE ANTES DE ESCRIBIR
/// -------------------------------------
/// Los sp_n_Actualizar* de catalogo hacen un UPDATE que pisa TODAS las columnas,
/// no solo el estatus. El DELETE de MacServicios2 llamaba a ese mismo SP con lo
/// que trajera el cuerpo y su controlador solo exigia el id, asi que una baja
/// hecha "bien" segun su propia validacion dejaba el renglon con el nombre vacio
/// y los numeros en cero. La baja de aqui (<see cref="CambiarStatusAsync"/>) LEE
/// los valores actuales del renglon y se los devuelve al SP junto con el estatus
/// nuevo. Comprobado leyendo los UPDATE de los 18 procedimientos: todos pisan
/// todo.
///
/// Esa lectura es un SELECT directo sobre la tabla de legacy y no el
/// sp_n_Consulta* correspondiente, por la razon de siempre: esos SP terminan en
/// EXEC (@Sql) y devuelven veinte columnas formateadas para un combo de
/// WinForms; aqui hacen falta las tres o cuatro que el UPDATE va a pisar.
/// </summary>
public partial class CatalogosAdminRepository : ICatalogosAdminRepository
{
    // Las fechas entran a legacy como varchar en el formato de
    // Constantes.FormatoFecha, que vale 'MM-dd-yyyy' en las empresas vivas.
    // Es el mismo constante que ya usan EmpleadosRepository y PrestamosRepository.
    private const string FormatoFechaLegacy = "MM-dd-yyyy";

    private readonly IConfiguration _configuration;

    public CatalogosAdminRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Que hace falta para cambiarle el estatus a un renglon de catalogo sin
    /// perder sus datos: de que tabla se lee, con que llave, a que SP se le
    /// devuelve y que columnas hay que acarrear.
    /// </summary>
    /// <param name="Tabla">Nombre con corchetes si lleva espacios: "[Catalogo Bancos]".</param>
    /// <param name="ColumnaId">La llave en la tabla.</param>
    /// <param name="ParamId">Como se llama esa llave en el SP (no siempre coincide: IDCondicionesCredito -> @IDCondicionCredito).</param>
    /// <param name="Sp">El sp_n_Actualizar* del catalogo.</param>
    /// <param name="Conservar">Pares (parametro del SP, columna de la tabla) que se leen y se devuelven sin tocar.</param>
    /// <param name="PideUsuario">El SP recibe @IDUsuario.</param>
    /// <param name="PideEquipo">El SP recibe @Equipo.</param>
    private sealed record CatalogoStatus(
        string Tabla,
        string ColumnaId,
        string ParamId,
        string Sp,
        (string Param, string Columna)[] Conservar,
        bool PideUsuario = false,
        bool PideEquipo = false);

    /// <summary>
    /// Cambia el estatus de un renglon de catalogo conservando sus datos.
    /// Devuelve null cuando el renglon no existe (el controlador contesta 404).
    /// </summary>
    private async Task<ResultadoLegacy?> CambiarStatusAsync(
        CatalogoStatus def, int id, int idStatus, int idUsuario, string equipo, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        // 1. Los valores actuales. Sin esto el UPDATE del SP los dejaria vacios.
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

        // 2. El mismo SP de siempre, con el estatus nuevo y lo demas igual.
        await using var cmd = Sp(def.Sp, conn);
        cmd.Parameters.AddWithValue(def.ParamId, id);
        cmd.Parameters.AddWithValue("@IDStatus", idStatus);
        foreach (var par in def.Conservar)
            cmd.Parameters.AddWithValue(par.Param, valores[par.Param] ?? DBNull.Value);
        if (def.PideUsuario) cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
        if (def.PideEquipo) cmd.Parameters.AddWithValue("@Equipo", equipo);

        var dt = await FillAsync(cmd, ct);
        return LeerResultado(dt);
    }

    /// <summary>
    /// Ejecuta un sp_n_ de escritura de catalogo y traduce su renglon
    /// result/mensaje. Es el 90% de este repositorio.
    /// </summary>
    private async Task<ResultadoLegacy> EjecutarAsync(
        string sp, Action<SqlCommand> parametros, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(sp, conn);
        parametros(cmd);

        var dt = await FillAsync(cmd, ct);
        return LeerResultado(dt);
    }

    /// <summary>
    /// Lo mismo, pero devolviendo tambien el id que algunos SP regresan en el
    /// mismo renglon (columna id*, por ejemplo idAgente o idEmpresaSerie).
    /// </summary>
    private async Task<(ResultadoLegacy Resultado, int Id)> EjecutarConIdAsync(
        string sp, string columnaId, Action<SqlCommand> parametros, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(sp, conn);
        parametros(cmd);

        var dt = await FillAsync(cmd, ct);
        var resultado = LeerResultado(dt);
        var id = dt.Rows.Count > 0 ? LeerInt(dt.Rows[0], columnaId) : 0;
        return (resultado, id);
    }

    /// <summary>
    /// El ultimo id de una tabla. Se usa SOLO despues de un alta que salio bien
    /// y cuyo SP no devuelve el id, para poder contestar 201 con la ubicacion
    /// del renglon nuevo. No sirve para reservar nada.
    /// </summary>
    private async Task<int> UltimoIdAsync(string tabla, string columnaId, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(
            $"SELECT ISNULL(MAX({Corchetes(columnaId)}), 0) FROM dbo.{tabla}", conn);
        var valor = await cmd.ExecuteScalarAsync(ct);
        return valor is null or DBNull ? 0 : Convert.ToInt32(valor, CultureInfo.InvariantCulture);
    }

    // -----------------------------------------------------------------------
    // Ayudantes
    // -----------------------------------------------------------------------

    /// <summary>
    /// Corchetea el nombre de la columna si hace falta. Las tablas de Mac31
    /// tienen columnas con espacios ("Condicion de Credito", "Codigo Postal").
    /// </summary>
    private static string Corchetes(string nombre)
        => nombre.StartsWith('[') ? nombre : $"[{nombre}]";

    /// <summary>Fecha -> 'MM-dd-yyyy'. Sin fecha, cadena vacia: es lo que legacy entiende por "no hay".</summary>
    private static string AFechaLegacy(DateTime? fecha)
        => fecha.HasValue ? fecha.Value.ToString(FormatoFechaLegacy, CultureInfo.InvariantCulture) : string.Empty;

    /// <summary>
    /// Texto para legacy: recortado, nunca nulo, y CON el apostrofo.
    /// Legacy hacia Replace("'", "") aqui y convertia O'BRIEN en OBRIEN para
    /// siempre. En una escritura el valor viaja como SqlParameter a un
    /// INSERT/UPDATE: no se concatena en ningun EXEC(@Sql), asi que quitarle el
    /// apostrofo solo mutilaria el dato.
    /// </summary>
    private static string Texto(string? valor) => (valor ?? string.Empty).Trim();

    private static ResultadoLegacy LeerResultado(DataTable? dt)
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

    private static int LeerInt(DataRow row, string columna)
    {
        if (!row.Table.Columns.Contains(columna)) return 0;
        var v = row[columna];
        return v == DBNull.Value ? 0 : Convert.ToInt32(v, CultureInfo.InvariantCulture);
    }

    private static string LeerString(DataRow row, string columna)
    {
        if (!row.Table.Columns.Contains(columna)) return string.Empty;
        var v = row[columna];
        return v == DBNull.Value ? string.Empty : Convert.ToString(v, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static SqlCommand Sp(string sp, SqlConnection conn)
        => new(sp, conn) { CommandType = CommandType.StoredProcedure };

    private static async Task<DataTable> FillAsync(SqlCommand cmd, CancellationToken ct)
    {
        var ds = new DataSet();
        using var adapter = new SqlDataAdapter(cmd);
        await Task.Run(() => adapter.Fill(ds), ct);
        return ds.Tables.Count > 0 ? ds.Tables[0] : new DataTable();
    }

    private SqlConnection GetConnection()
    {
        var cs = _configuration.GetConnectionString("Main")
            ?? _configuration.GetConnectionString("Mac3")
            ?? _configuration.GetConnectionString("Local")
            ?? _configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("ConnectionString (Main/Mac3/Local/Default) no encontrada.");
        return new Mac3SqlServerConnector(cs).GetConnection;
    }
}
