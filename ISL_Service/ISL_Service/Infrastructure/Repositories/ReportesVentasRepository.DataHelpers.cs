using System.Data;
using ISL_Service.Application.DTOs.Reportes;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Infrastructure.Reports;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

public partial class ReportesVentasRepository
{
    private static int ReadInt(DataRow row, string column)
    {
        return row.Table.Columns.Contains(column) && int.TryParse(Convert.ToString(row[column]), out var value)
            ? value
            : 0;
    }

    private static string ReadString(DataRow row, string column)
    {
        return row.Table.Columns.Contains(column) ? Convert.ToString(row[column]) ?? "" : "";
    }

    private static string ReadString(DataRow row, params string[] columns)
    {
        foreach (var column in columns)
        {
            var value = ReadString(row, column);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return "";
    }

    private static DateTime ReadLegacyDate(DataRow row, string column)
    {
        var value = ReadString(row, column).Replace("@", " ");
        return DateTime.TryParse(value, out var date) ? date : DateTime.Today;
    }

    private static List<int> SplitLegacyIds(string value)
    {
        return (value ?? "")
            .Split('~', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => int.TryParse(item, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
    }

    private static async Task<DataSet> ExecuteLegacySpAsync(
        SqlConnection conn,
        string spNames,
        string spParams,
        CancellationToken ct)
    {
        var ds = new DataSet();
        var parameters = (spParams ?? "").Split('|');

        for (var paramIndex = 0; paramIndex < parameters.Length; paramIndex++)
        {
            parameters[paramIndex] = parameters[paramIndex].Replace("~", "|");
        }

        foreach (var spName in (spNames ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            await using var deriveCmd = new SqlCommand(spName, conn)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 500
            };
            SqlCommandBuilder.DeriveParameters(deriveCmd);

            await using var cmd = new SqlCommand(spName, conn)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 500
            };

            var valueIndex = 0;
            foreach (SqlParameter parameter in deriveCmd.Parameters)
            {
                if (parameter.ParameterName == "@RETURN_VALUE")
                    continue;

                var rawValue = valueIndex < parameters.Length ? parameters[valueIndex] : "";
                var value = rawValue.Replace("|", "~");
                if (rawValue.Contains("@@"))
                    value = rawValue.Replace("@@", "|");
                else if (rawValue.Contains("["))
                    value = rawValue.Replace("[", "~");

                cmd.Parameters.Add(parameter.ParameterName, parameter.SqlDbType).Value = value;
                valueIndex++;
            }

            var table = new DataTable(spName);
            using var adapter = new SqlDataAdapter(cmd);
            adapter.Fill(table);
            ds.Tables.Add(table);
        }

        await Task.CompletedTask;
        return ds;
    }

    private static async Task<DataSet> FillDataSetAsync(SqlCommand cmd, CancellationToken ct)
    {
        var ds = new DataSet();
        using var adapter = new SqlDataAdapter(cmd);
        adapter.Fill(ds);
        await Task.CompletedTask;
        return ds;
    }

    private static async Task<DataTable> ExecuteFirstTableAsync(SqlCommand cmd, CancellationToken ct)
    {
        var ds = await FillDataSetAsync(cmd, ct);
        return ds.Tables.Count > 0 ? ds.Tables[0] : new DataTable();
    }

    private static List<ReportesVentasColumnDto> BuildColumns(DataTable table)
    {
        return table.Columns
            .Cast<DataColumn>()
            .Select(column => new ReportesVentasColumnDto
            {
                Key = column.ColumnName,
                Label = SplitLabel(column.ColumnName),
                Type = ResolveColumnType(column.DataType)
            })
            .ToList();
    }

    private static List<Dictionary<string, object?>> BuildRows(DataTable table)
    {
        return table.Rows
            .Cast<DataRow>()
            .Select(row => table.Columns
                .Cast<DataColumn>()
                .ToDictionary(
                    column => column.ColumnName,
                    column => row[column] == DBNull.Value ? null : row[column]))
            .ToList();
    }

    private static string SplitLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var chars = new List<char> { value[0] };
        for (var i = 1; i < value.Length; i++)
        {
            var current = value[i];
            var previous = value[i - 1];
            if (char.IsUpper(current) && !char.IsWhiteSpace(previous) && !char.IsUpper(previous))
                chars.Add(' ');
            chars.Add(current);
        }
        return new string(chars.ToArray());
    }

    private static string ResolveColumnType(Type type)
    {
        if (type == typeof(decimal) || type == typeof(double) || type == typeof(float))
            return "number";
        if (type == typeof(int) || type == typeof(long) || type == typeof(short))
            return "number";
        if (type == typeof(DateTime))
            return "date";
        return "text";
    }

    private static DataTable BuildLegacyExcelTable(int idReporte, DataTable source)
    {
        var columns = idReporte switch
        {
            ReporteVentaProductos => VentaProductosExcelColumns,
            ReporteVentasFoliosGlobal => VentasFoliosGlobalExcelColumns,
            ReporteVentasFoliosDineroUsados => VentasFoliosDineroUsadosExcelColumns,
            ReporteVentasRemisiones => VentasRemisionesExcelColumns,
            ReporteVentasRemisionesSaldoFavor => VentasRemisionesSaldoFavorExcelColumns,
            ReporteVentasRemisionesImpuestos => VentasRemisionesImpuestosExcelColumns,
            ReporteVentasFacturas => VentasFacturasExcelColumns,
            ReporteVentasConcentradosDetalle => VentasConcentradosDetalleExcelColumns,
            ReporteVentasCobranzaDetalladoZara => VentasCobranzaDetalladoZaraExcelColumns,
            ReporteVentasCobranzaPagadasDetalladoZara => VentasCobranzaPagadasDetalladoZaraExcelColumns,
            ReporteVentasCobranzaPagadasTotalizadoZara => VentasCobranzaPagadasTotalizadoZaraExcelColumns,
            ReporteVentasCobranzaDesglosado or ReporteVentasCobranzaZaragoza => VentasCobranzaDesglosadoExcelColumns,
            ReporteHojaCobroTotalCascos => HojaCobroTotalCascosExcelColumns,
            _ => Array.Empty<LegacyExcelColumn>()
        };

        if (columns.Length == 0)
            return source.Copy();

        var table = new DataTable(source.TableName);
        foreach (var column in columns)
            table.Columns.Add(column.Name, column.Type);

        foreach (DataRow sourceRow in source.Rows)
        {
            var values = new object?[columns.Length];
            for (var i = 0; i < columns.Length; i++)
                values[i] = ReadLegacyExcelValue(sourceRow, columns[i]);
            table.Rows.Add(values);
        }

        return table;
    }

    private static object ReadLegacyExcelValue(DataRow row, LegacyExcelColumn column)
    {
        if (row.Table.Columns.Contains(column.Name) && row[column.Name] != DBNull.Value)
            return CoerceLegacyExcelValue(row[column.Name], column.Type);

        if (column.Type == typeof(string))
            return DBNull.Value;
        if (column.Type == typeof(decimal))
            return 0m;
        if (column.Type == typeof(int))
            return 0;

        return DBNull.Value;
    }

    private static object CoerceLegacyExcelValue(object value, Type type)
    {
        if (type == typeof(string))
            return Convert.ToString(value) ?? "";
        if (type == typeof(decimal))
            return decimal.TryParse(Convert.ToString(value), out var decimalValue) ? decimalValue : 0m;
        if (type == typeof(int))
            return int.TryParse(Convert.ToString(value), out var intValue) ? intValue : 0;

        return value;
    }
}
