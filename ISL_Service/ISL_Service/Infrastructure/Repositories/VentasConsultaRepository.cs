using System.Data;
using ISL_Service.Application.DTOs.VentasConsulta;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using System.Globalization;

namespace ISL_Service.Infrastructure.Repositories;

public class VentasConsultaRepository : IVentasConsultaRepository
{
    private const int CommandTimeoutSeconds = 500;

    private readonly IConfiguration _configuration;

    public VentasConsultaRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<VentasConsultaCatalogosResponse> ConsultarCatalogosAsync(CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var constantes = await ConsultarConstantesDeVentasAsync(conn, ct);

        return new VentasConsultaCatalogosResponse
        {
            Empresas = await ConsultarEmpresasAsync(conn, ct),
            Almacenes = await ConsultarAlmacenesAsync(conn, ct),
            Agentes = await ConsultarAgentesAsync(conn, ct),
            TiposDocumento = BuildTiposDocumento(),
            EstatusVenta = BuildEstatusVenta(),
            FechasOperacion = constantes.Fechas,
            Funcionalidad = constantes.Funcionalidad,
            EsCentroServicio = constantes.EsCentroServicio
        };
    }

    public async Task<VentasConsultaRowsResponse> ConsultarRemisionesAsync(VentasConsultaRequest request, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand("sp_n_ConsultaVentas", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };

        AddVentasCommonParams(cmd, request);
        var response = ApplyAccumulatedFilters(await ExecuteRowsAsync(cmd, ct), request);
        await EnrichPedidoTimelineAsync(conn, response, ct);
        return response;
    }

    public async Task<VentasConsultaRowsResponse> ConsultarPedidosAsync(VentasConsultaRequest request, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand("sp_n_ConsultaVentasPedidos", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };

        cmd.Parameters.AddWithValue("@IDPedido", 0);
        cmd.Parameters.AddWithValue("@IDVenta", request.IDVenta);
        cmd.Parameters.AddWithValue("@IDEmpresa", request.IDEmpresa);
        cmd.Parameters.AddWithValue("@IDCliente", request.IDCliente);
        cmd.Parameters.AddWithValue("@IDUsuario", request.IDUsuario);
        cmd.Parameters.AddWithValue("@IDAgente", request.IDAgente);
        cmd.Parameters.AddWithValue("@IDTipoDocumento", request.IDTipoDocumento);
        cmd.Parameters.AddWithValue("@IDStatusPedido", request.IDStatusPedido);
        cmd.Parameters.AddWithValue("@FolioInicial", NormalizeLegacyFolio(request.FolioInicial));
        cmd.Parameters.AddWithValue("@FolioFinal", NormalizeLegacyFolio(request.FolioFinal));
        cmd.Parameters.AddWithValue("@FechaInicial", NormalizeLegacyDateTime(request.FechaInicial ?? request.FechaEmisionInicial, false));
        cmd.Parameters.AddWithValue("@FechaFinal", NormalizeLegacyDateTime(request.FechaFinal ?? request.FechaEmisionFinal, true));
        cmd.Parameters.AddWithValue("@FechaCancelInicial", NormalizeLegacyDateTime(request.FechaCancelInicial, false));
        cmd.Parameters.AddWithValue("@FechaCancelFinal", NormalizeLegacyDateTime(request.FechaCancelFinal, true));
        cmd.Parameters.AddWithValue("@Formato", 0);
        cmd.Parameters.AddWithValue("@IDUsuarioActual", request.IDUsuarioActual);

        // El filtro de fechas NO se aplica aqui para el web. Ver ApplyDateFilter.
        var response = ApplyAccumulatedFilters(await ExecuteRowsAsync(cmd, ct), request);
        if (request.SoloMisPedidos)
            response = ApplyDateFilter(response, request);
        if (request.IDStatusPedido == 2)
            await EnrichPedidoTimelineAsync(conn, response, ct);

        return response;
    }

    public async Task<VentasConsultaRowsResponse> ConsultarPendientesImprimirAsync(VentasConsultaRequest request, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand("sp_n_ConsultaVentasPendientesImprimir", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };
        cmd.Parameters.AddWithValue("@IDPedido", request.IDVenta);

        return await ExecuteRowsAsync(cmd, ct);
    }

    /*
      PEDIDOS FALTANTES

      Mac31 llama a este procedimiento con los dos parametros en blanco:
      ConsultaPedidoFaltante() (ConsultarVentas.cs:3503) arma un PedidoInputDTO
      SIN fijar ningun campo, asi que @Fecha va vacio y @IDEmpresaCS va en 0.

      No es descuido de legacy: @Fecha esta declarado pero el procedimiento no
      lo usa —filtra con `CAST(pedDetFal.Fecha as date) = CAST(GETDATE() as date)`
      por dentro—, y por eso el mensaje de cuando no hay nada dice "EN EL DÍA
      ACTUAL". Se copia igual para no inventarse un filtro que alla no existe.

      Y por eso mismo no hace falta ramificar por empresa: el propio
      procedimiento lee Funcionalidad de Constantes y decide de que base saca
      catalogos, clientes e inventario. Se comprobo ejecutandolo en
      Produccion_svr (TAU) y en MacZ (ZARA): mismas columnas, misma definicion
      (diff sin espacios: identica), y en las dos corre sin error.
    */
    public async Task<List<VentasPedidoFaltanteItem>> ConsultarPedidosFaltantesAsync(CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand("sp_n_ConsultaPedidoFaltante", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };
        cmd.Parameters.AddWithValue("@Fecha", "");
        cmd.Parameters.AddWithValue("@IDEmpresaCS", 0);

        var filas = new List<VentasPedidoFaltanteItem>();

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            filas.Add(new VentasPedidoFaltanteItem
            {
                Numero = Texto(reader, "Numero"),
                Nombre = Texto(reader, "Nombre"),
                FechaFtm = Texto(reader, "FechaFtm"),
                GrupoCategoria = Texto(reader, "GrupoCategoria"),
                Categoria = Texto(reader, "Categoria"),
                Marca = Texto(reader, "Marca"),
                Clave = Texto(reader, "Clave"),
                CantidadSolicitada = Numero(reader, "CantidadSolicitada"),
                CantidadPedido = Numero(reader, "CantidadPedido"),
                CantidadFaltante = Numero(reader, "CantidadFaltante")
            });
        }

        return filas;
    }

    /* Por nombre de columna y tolerante: el procedimiento arma su SELECT con
       SQL dinamico, y una columna que se agregue alla no debe tirar esto. */
    private static string Texto(SqlDataReader reader, string columna)
    {
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (!string.Equals(reader.GetName(i), columna, StringComparison.OrdinalIgnoreCase))
                continue;
            return reader.IsDBNull(i) ? "" : Convert.ToString(reader.GetValue(i)) ?? "";
        }
        return "";
    }

    private static decimal Numero(SqlDataReader reader, string columna)
    {
        var texto = Texto(reader, columna);
        return decimal.TryParse(texto, out var valor) ? valor : 0m;
    }

    public async Task<VentasConsultaRowsResponse> ConsultarPagosAsync(VentasConsultaRequest request, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand("sp_n_ConsultaVentasPagos", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };
        cmd.Parameters.AddWithValue("@IDVenta", request.IDVenta);
        cmd.Parameters.AddWithValue("@IDPagoVenta", 0);
        cmd.Parameters.AddWithValue("@DiferenciaUsados", request.DiferenciaUsados ? 1 : 0);
        cmd.Parameters.AddWithValue("@IDCobro", request.IDCobro);
        cmd.Parameters.AddWithValue("@DineroExcedente", request.DineroExcedente ? 1 : 0);

        return await ExecuteRowsAsync(cmd, ct);
    }

    private SqlConnection GetConnection()
    {
        var cs = _configuration.GetConnectionString("Main")
            ?? _configuration.GetConnectionString("Mac3")
            ?? _configuration.GetConnectionString("Local")
            ?? _configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("ConnectionString (Main/Mac3/Local/Default) no encontrada.");

        var connector = new Mac3SqlServerConnector(cs);
        return connector.GetConnection;
    }

    private static void AddVentasCommonParams(SqlCommand cmd, VentasConsultaRequest request)
    {
        /*
          LA LISTA DE VENTAS VA CON VIRGULILLA, NO CON COMA.

          `sp_n_ConsultaVentas` NO recibe el separador: lo tiene escrito a mano
          dentro (`SET @Delimitador = '~'`), que es el delimitador de toda la
          familia legacy (Globales.DELIMITADOR_PARAM_TILDE_SP en Mac31).

          Con coma y DOS O MAS ventas el procedimiento truena al convertir
          '370852,370851' a entero. Comprobado ejecutandolo contra la base: con
          coma da error, con virgulilla devuelve las dos.

          Y es el peor tipo de fallo: con UNA sola venta funciona igual, porque
          no hay nada que partir. Asi que pasa desapercibido hasta que alguien
          marca dos folios.
        */
        cmd.Parameters.AddWithValue("@IDsVenta", JoinIds(request.IDsVentaLst, request.IDsVenta, "~"));
        cmd.Parameters.AddWithValue("@IDVenta", request.IDVenta);
        cmd.Parameters.AddWithValue("@IDEmpresa", request.IDEmpresa);
        cmd.Parameters.AddWithValue("@IDCliente", request.IDCliente);
        cmd.Parameters.AddWithValue("@IDUsuario", request.IDUsuario);
        cmd.Parameters.AddWithValue("@IDAgente", request.IDAgente);
        cmd.Parameters.AddWithValue("@IDTipoDocumento", request.IDTipoDocumento);
        cmd.Parameters.AddWithValue("@TipoDocumento", request.TipoDocumento);
        cmd.Parameters.AddWithValue("@FolioInicial", NormalizeLegacyFolio(request.FolioInicial));
        cmd.Parameters.AddWithValue("@FolioFinal", NormalizeLegacyFolio(request.FolioFinal));
        cmd.Parameters.AddWithValue("@FechaEmisionInicial", NormalizeLegacyDateTime(request.FechaEmisionInicial ?? request.FechaInicial, false));
        cmd.Parameters.AddWithValue("@FechaEmisionFinal", NormalizeLegacyDateTime(request.FechaEmisionFinal ?? request.FechaFinal, true));
        cmd.Parameters.AddWithValue("@FechaCancelInicial", NormalizeLegacyDateTime(request.FechaCancelInicial, false));
        cmd.Parameters.AddWithValue("@FechaCancelFinal", NormalizeLegacyDateTime(request.FechaCancelFinal, true));
        cmd.Parameters.AddWithValue("@FechaPagoInicial", NormalizeLegacyDateTime(request.FechaPagoInicial, false));
        cmd.Parameters.AddWithValue("@FechaPagoFinal", NormalizeLegacyDateTime(request.FechaPagoFinal, true));
        cmd.Parameters.AddWithValue("@Formato", request.Formato);
        cmd.Parameters.AddWithValue("@IDUsuarioActual", request.IDUsuarioActual);
        cmd.Parameters.AddWithValue("@IDsClientes", JoinIds(request.IDsClientesLst, request.IDsClientes));
        cmd.Parameters.AddWithValue("@IDsProducto", JoinIds(request.IDsProductoLst, request.IDsProducto));
        cmd.Parameters.AddWithValue("@IDProducto", request.IDProducto);
    }

    private static async Task<List<VentasConsultaCatalogoItem>> ConsultarEmpresasAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaEmpresas", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };
        cmd.Parameters.AddWithValue("@IDEmpresa", 0);
        cmd.Parameters.AddWithValue("@IDStatus", 1);
        cmd.Parameters.AddWithValue("@RazonSocial", "");
        cmd.Parameters.AddWithValue("@Formato", 0);
        cmd.Parameters.AddWithValue("@Identico", 0);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return table.AsEnumerable()
            .Select(row => new VentasConsultaCatalogoItem
            {
                Id = ReadInt(row, "IDEmpresa"),
                Nombre = ReadString(row, "NombreCorto", "RazonSocial", "Empresa"),
                Clave = ReadString(row, "Clave", "NombreCorto")
            })
            .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Nombre))
            .ToList();
    }

    private static async Task<List<VentasConsultaCatalogoItem>> ConsultarAlmacenesAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaAlmacenes", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };
        cmd.Parameters.AddWithValue("@IDAlmacen", 0);
        cmd.Parameters.AddWithValue("@IDStatus", 1);
        cmd.Parameters.AddWithValue("@Almacen", "");
        cmd.Parameters.AddWithValue("@Identico", 0);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return table.AsEnumerable()
            .Select(row => new VentasConsultaCatalogoItem
            {
                Id = ReadInt(row, "IDAlmacen"),
                Nombre = ReadString(row, "Almacen"),
                Clave = ReadString(row, "Almacen")
            })
            .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Nombre))
            .ToList();
    }

    private static async Task<List<VentasConsultaCatalogoItem>> ConsultarAgentesAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaAgentes", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };
        cmd.Parameters.AddWithValue("@IDAgente", 0);
        cmd.Parameters.AddWithValue("@IDStatus", 1);
        cmd.Parameters.AddWithValue("@NumeroAgente", 0);
        cmd.Parameters.AddWithValue("@NombreAgente", "");
        cmd.Parameters.AddWithValue("@Formato", 0);
        cmd.Parameters.AddWithValue("@Identico", 0);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return table.AsEnumerable()
            .Select(row => new VentasConsultaCatalogoItem
            {
                Id = ReadInt(row, "IDAgente"),
                Nombre = ReadString(row, "NombreAgente"),
                Clave = ReadString(row, "NumeroAgente")
            })
            .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Nombre))
            .OrderBy(x => x.Nombre)
            .ToList();
    }

    private static List<VentasConsultaCatalogoItem> BuildTiposDocumento()
    {
        return new List<VentasConsultaCatalogoItem>
        {
            new() { Id = 0, Nombre = "TODOS", Clave = "todos" },
            new() { Id = 10, Nombre = "REMISIONES", Clave = "remisiones" },
            new() { Id = 11, Nombre = "PEDIDOS", Clave = "pedidos" }
        };
    }

    private static List<VentasConsultaCatalogoItem> BuildEstatusVenta()
    {
        return new List<VentasConsultaCatalogoItem>
        {
            new() { Id = 0, Nombre = "REMISIONES", Clave = "remisiones" },
            new() { Id = 1, Nombre = "PEDIDOS", Clave = "pedidos" },
            new() { Id = 2, Nombre = "PENDIENTES DE IMPRIMIR", Clave = "pendientes-imprimir" },
            new() { Id = 3, Nombre = "PENDIENTES AUTORIZAR", Clave = "pendientes-autorizar" },
            new() { Id = 4, Nombre = "RECHAZADOS", Clave = "rechazados" }
        };
    }

    /*
      Las fechas de operacion y las dos variables de empresa salen del MISMO
      renglon de Constantes, asi que se leen de una sola llamada: Mac31 tambien
      las saca juntas al arrancar (Utils/Globales.cs, region Constantes).
    */
    private sealed class ConstantesDeVentas
    {
        public VentasConsultaFechasOperacion Fechas { get; init; } = new();
        public string Funcionalidad { get; init; } = "";
        public int EsCentroServicio { get; init; }
    }

    private static async Task<ConstantesDeVentas> ConsultarConstantesDeVentasAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaConstantes", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };
        cmd.Parameters.AddWithValue("@ValidaActividad", 0);
        cmd.Parameters.AddWithValue("@IDUsuario", 0);
        cmd.Parameters.AddWithValue("@Equipo", "");

        var table = await ExecuteFirstTableAsync(cmd, ct);
        if (table.Rows.Count == 0)
            return new ConstantesDeVentas();

        var row = table.Rows[0];
        var fechaOperacion = ReadDateIso(row, "FechaOperacion");
        var fechaPagos = ReadDateIso(row, "FechaOperacionPagosUsuario", "FechaOperacionPagos");

        return new ConstantesDeVentas
        {
            Fechas = new VentasConsultaFechasOperacion
            {
                FechaOperacion = fechaOperacion,
                FechaOperacionFtm = ReadString(row, "FechaOperacionFtm"),
                FechaOperacionPagos = fechaPagos,
                FechaOperacionPagosFtm = ReadString(row, "FechaOperacionPagosFtm")
            },
            Funcionalidad = ReadString(row, "Funcionalidad").ToUpperInvariant(),
            EsCentroServicio = ReadInt(row, "EsCentroServicio")
        };
    }

    private static async Task<VentasConsultaRowsResponse> ExecuteRowsAsync(SqlCommand cmd, CancellationToken ct)
    {
        var table = await ExecuteFirstTableAsync(cmd, ct);
        var rows = DataTableToRows(table);
        return new VentasConsultaRowsResponse { Rows = rows, Total = rows.Count };
    }

    private static async Task<DataTable> ExecuteFirstTableAsync(SqlCommand cmd, CancellationToken ct)
    {
        var table = new DataTable();
        using var adapter = new SqlDataAdapter(cmd);
        await Task.Run(() => adapter.Fill(table), ct);
        return table;
    }

    private static List<Dictionary<string, object?>> DataTableToRows(DataTable table)
    {
        var rows = new List<Dictionary<string, object?>>();
        foreach (DataRow row in table.Rows)
        {
            var item = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (DataColumn col in table.Columns)
                item[col.ColumnName] = row[col] == DBNull.Value ? null : row[col];
            rows.Add(item);
        }
        return rows;
    }

    private static int ReadInt(DataRow row, params string[] names)
    {
        foreach (var name in names)
        {
            if (!row.Table.Columns.Contains(name)) continue;
            var value = row[name];
            if (value == DBNull.Value || value == null) continue;
            if (int.TryParse(Convert.ToString(value), out var parsed)) return parsed;
        }
        return 0;
    }

    private static string ReadString(DataRow row, params string[] names)
    {
        foreach (var name in names)
        {
            if (!row.Table.Columns.Contains(name)) continue;
            var value = row[name];
            if (value == DBNull.Value || value == null) continue;
            var text = Convert.ToString(value)?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }
        return string.Empty;
    }

    private static string ReadDateIso(DataRow row, params string[] names)
    {
        foreach (var name in names)
        {
            if (!row.Table.Columns.Contains(name)) continue;
            var value = row[name];
            if (value == DBNull.Value || value == null) continue;
            if (value is DateTime date)
                return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (DateTime.TryParse(Convert.ToString(value), out var parsed))
                return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        return string.Empty;
    }

    /*
      Los otros dos (@IDsClientes, @IDsProducto) se quedan con coma a proposito:
      solo se comprobo contra la base el de ventas, y cambiar a ciegas un
      separador que hoy no da problemas es arriesgar un fallo silencioso en dos
      filtros que sí funcionan. Si algun dia se ven vacios con varios elementos,
      el sospechoso es este.
    */
    private static string JoinIds(IEnumerable<int>? values, string? raw, string separador = ",")
    {
        if (!string.IsNullOrWhiteSpace(raw)) return raw.Trim();
        if (values == null) return string.Empty;
        return string.Join(separador, values.Where(x => x > 0).Distinct());
    }

    private static string NormalizeLegacyFolio(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "0" : value.Trim();
    }

    private static string NormalizeLegacyDateTime(string? value, bool endOfDay)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var text = value.Trim();
        var compactSlashDate = text.Contains('/') ? text.Replace(" ", "") : text;
        var formats = new[]
        {
            "yyyy-MM-dd",
            "yyyy-MM-dd HH:mm:ss",
            "dd/MM/yyyy",
            "d/M/yyyy",
            "dd-MM-yyyy",
            "d-M-yyyy",
            "MM-dd-yyyy",
            "M-d-yyyy"
        };

        if (!DateTime.TryParseExact(
                compactSlashDate,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed)
            && !DateTime.TryParse(
                text,
                CultureInfo.GetCultureInfo("es-MX"),
                DateTimeStyles.None,
                out parsed))
        {
            return text;
        }

        var normalized = endOfDay
            ? parsed.Date.AddDays(1).AddSeconds(-1)
            : parsed.Date;

        return normalized.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    // Devuelve la fecha ya parseada, o null si no viene o no se entiende.
    private static DateTime? ParseLegacyDate(string? value)
    {
        var normalized = NormalizeLegacyDateTime(value, false);
        if (string.IsNullOrWhiteSpace(normalized)) return null;
        return DateTime.TryParse(
                normalized,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed)
            ? parsed.Date
            : null;
    }

    // Filtra los renglones por rango de fechas EN MEMORIA.
    //
    // `sp_n_ConsultaVentasPedidos` IGNORA @FechaInicial y @FechaFinal: en cada
    // rama de @IDStatusPedido (0, 1, 2 y 3) los sobreescribe con '' antes de
    // armar el WHERE, asi que el rango que le mandemos nunca llega a la consulta.
    //
    // ESO NO ES UN DESCUIDO DE LEGACY, ES LA REGLA, y por eso el web ya NO lo
    // corrige. Un pedido pendiente de imprimir de noviembre SIGUE pendiente hoy:
    // acotarlo al dia consultado lo esconde, y esconder trabajo pendiente es
    // exactamente lo que no debe hacer esa pestaña. Se comprobo poniendo las dos
    // pantallas lado a lado: Mac31 enseñaba diez folios y el web uno.
    //
    // Se conserva SOLO para la app movil ("Mis pedidos"), que si pide un rango y
    // lo espera respetado; ahi la lista es del usuario y del periodo que eligio.
    private static VentasConsultaRowsResponse ApplyDateFilter(
        VentasConsultaRowsResponse response,
        VentasConsultaRequest request)
    {
        var desde = ParseLegacyDate(request.FechaInicial ?? request.FechaEmisionInicial);
        var hasta = ParseLegacyDate(request.FechaFinal ?? request.FechaEmisionFinal);
        if (desde is null && hasta is null) return response;

        // El rango es por dia completo e inclusivo en ambos extremos: "Hoy" tiene
        // que traer lo capturado a las 23:59.
        var finDelDia = hasta?.AddDays(1).AddSeconds(-1);

        var rows = response.Rows.Where(row =>
        {
            var fecha = ReadRowDate(row, "Fecha", "fecha");
            // Sin fecha legible no se puede decidir: se conserva en vez de
            // desaparecer un pedido de la vista del usuario.
            if (fecha is null) return true;
            if (desde is not null && fecha < desde) return false;
            if (finDelDia is not null && fecha > finDelDia) return false;
            return true;
        }).ToList();

        return new VentasConsultaRowsResponse { Rows = rows, Total = rows.Count };
    }

    private static DateTime? ReadRowDate(IDictionary<string, object?> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!row.TryGetValue(key, out var value) || value is null) continue;
            if (value is DateTime dt) return dt;
            if (DateTime.TryParse(
                    value.ToString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsed))
            {
                return parsed;
            }
        }
        return null;
    }

    private static async Task EnrichPedidoTimelineAsync(SqlConnection conn, VentasConsultaRowsResponse response, CancellationToken ct)
    {
        var idsVenta = response.Rows
            .Select(row => ReadRowInt(row, "IDVenta", "idVenta"))
            .Where(id => id > 0)
            .Distinct()
            .Take(1800)
            .ToList();

        if (idsVenta.Count == 0) return;

        var paramNames = idsVenta.Select((_, index) => $"@id{index}").ToArray();
        var sql = $@"
WITH PedidoTimeline AS (
    SELECT
        Ped.IDVenta,
        CASE WHEN Ped.[Fecha Impreso] IS NULL THEN '----'
             ELSE FORMAT(Ped.[Fecha Impreso], 'dd-MM-yyyy HH:mm') + ' - ' + ISNULL(UsuImp.Usuario, '') + ' - ' + ISNULL(Ped.EquipoImpresion, '') END AS FechaImpresoFtm,
        CASE WHEN Ped.[Fecha Proceso] IS NULL THEN '----'
             ELSE FORMAT(Ped.[Fecha Proceso], 'dd-MM-yyyy HH:mm') + ' - ' + ISNULL(UsuProc.Usuario, '') + ' - ' + ISNULL(Ped.EquipoProceso, '') END AS FechaProcesoFtm,
        CASE WHEN Ped.[Fecha Autorizo] IS NULL THEN '----'
             ELSE FORMAT(Ped.[Fecha Autorizo], 'dd-MM-yyyy HH:mm') + ' - ' + ISNULL(UsuAut.Usuario, '') + ' - ' + ISNULL(Ped.EquipoAutorizo, '') END AS FechaAutorizoFtm,
        ROW_NUMBER() OVER (PARTITION BY Ped.IDVenta ORDER BY Ped.IDPedido DESC) AS rn
    FROM Pedidos Ped
    LEFT JOIN Usuarios UsuImp ON UsuImp.IDUsuario = Ped.IDUsuarioImpreso
    LEFT JOIN Usuarios UsuProc ON UsuProc.IDUsuario = Ped.IDUsuarioProceso
    LEFT JOIN Usuarios UsuAut ON UsuAut.IDUsuario = Ped.IDUsuarioAutorizo
    WHERE Ped.IDVenta IN ({string.Join(",", paramNames)})
)
SELECT IDVenta, FechaImpresoFtm, FechaProcesoFtm, FechaAutorizoFtm
FROM PedidoTimeline
WHERE rn = 1;";

        await using var cmd = new SqlCommand(sql, conn)
        {
            CommandType = CommandType.Text,
            CommandTimeout = CommandTimeoutSeconds
        };

        for (var i = 0; i < idsVenta.Count; i++)
            cmd.Parameters.AddWithValue(paramNames[i], idsVenta[i]);

        var timelineByVenta = new Dictionary<int, Dictionary<string, object?>>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var idVenta = reader.GetInt32(reader.GetOrdinal("IDVenta"));
                timelineByVenta[idVenta] = new Dictionary<string, object?>
                {
                    ["FechaImpresoFtm"] = reader["FechaImpresoFtm"],
                    ["FechaProcesoFtm"] = reader["FechaProcesoFtm"],
                    ["FechaAutorizoFtm"] = reader["FechaAutorizoFtm"]
                };
            }
        }

        foreach (var row in response.Rows)
        {
            var idVenta = ReadRowInt(row, "IDVenta", "idVenta");
            if (idVenta <= 0 || !timelineByVenta.TryGetValue(idVenta, out var timeline)) continue;
            row["FechaImpresoFtm"] = timeline["FechaImpresoFtm"];
            row["FechaProcesoFtm"] = timeline["FechaProcesoFtm"];
            row["FechaAutorizoFtm"] = timeline["FechaAutorizoFtm"];
        }
    }

    private static VentasConsultaRowsResponse ApplyAccumulatedFilters(VentasConsultaRowsResponse response, VentasConsultaRequest request)
    {
        var folios = request.FoliosLst.Where(x => x > 0).Select(x => x.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var numerosCliente = request.NumerosClienteLst.Where(x => x > 0).Select(x => x.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var clavesProducto = request.ClavesProductoLst
            .Append(request.ClaveProducto ?? string.Empty)
            .Select(NormalizeText)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (folios.Count == 0 && numerosCliente.Count == 0 && clavesProducto.Count == 0)
            return response;

        var rows = response.Rows.Where(row =>
            MatchesAny(row, folios, "FolioFtm", "Folio", "folioFtm", "folio") &&
            MatchesAny(row, numerosCliente, "NumeroCliente", "Numero", "No", "NO", "numeroCliente", "numero") &&
            MatchesAny(row, clavesProducto, "ClaveProducto", "Clave", "Producto", "DescripcionProducto", "claveProducto", "clave")
        ).ToList();

        return new VentasConsultaRowsResponse { Rows = rows, Total = rows.Count };
    }

    private static bool MatchesAny(Dictionary<string, object?> row, HashSet<string> accepted, params string[] keys)
    {
        if (accepted.Count == 0) return true;
        foreach (var key in keys)
        {
            if (!row.TryGetValue(key, out var value) || value == null) continue;
            var normalized = NormalizeText(value);
            if (accepted.Contains(normalized)) return true;
            if (accepted.Any(x => !string.IsNullOrWhiteSpace(x) && normalized.Contains(x, StringComparison.OrdinalIgnoreCase))) return true;
        }
        return false;
    }

    private static string NormalizeText(object? value)
    {
        return Convert.ToString(value)?.Trim().ToUpperInvariant() ?? string.Empty;
    }

    private static int ReadRowInt(Dictionary<string, object?> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!row.TryGetValue(key, out var value) || value == null) continue;
            if (int.TryParse(Convert.ToString(value), out var parsed)) return parsed;
        }
        return 0;
    }
}
