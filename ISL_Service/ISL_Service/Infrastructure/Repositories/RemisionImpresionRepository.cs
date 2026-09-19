using System.Data;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Infrastructure.Reports;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/*
  LOS DATOS DE LA REMISION SALEN DE LOS MISMOS sp_n_ QUE USA MAC31

  Son de legacy: se LLAMAN, no se tocan. Cada uno alimenta un pedazo del papel:

    sp_n_ConsultaTemplateHtml  las plantillas (tabla Template_Html)
    sp_n_VentasInformacion     cliente, empresa, folio, marcas de reimpresion
    sp_n_VentasDetalle         los renglones, los totales y la marca de cancelada
    sp_n_VentasUsadosCargo     los cascos que NO entrego y se le cobran
    sp_n_VentasUsadosCredito   los cascos entregados (accion 0 = de esta venta, 1 = de anteriores)
    sp_n_VentasDevolucion      lo que devolvio

  No se guarda una lista de columnas de ninguno: el html se arma recorriendo
  las columnas que vengan. Por eso aqui se leen a DataTable y no a DTOs — si
  manana el procedimiento agrega una columna, la plantilla ya la puede usar sin
  tocar este archivo, igual que en legacy.

  OJO CON EL EFECTO DE BORDE: sp_n_VentasInformacion no solo lee. Si la venta
  no tenia usuario de impresion, lo GRABA (queda registrado quien la imprimio y
  desde donde) y a partir de ahi la siguiente sale como reimpresion. Es lo
  mismo que pasa hoy cuando alguien la ve desde MacReportes, asi que se llama
  igual que ahi: sin equipo y sin exigir primera impresion.
*/
public class RemisionImpresionRepository : IRemisionImpresionRepository
{
    private const int CommandTimeoutSeconds = 500;

    /// 1001 = Remision en el catalogo de tipos de plantilla de legacy.
    private const int IdTipoTemplateHtmlRemision = 1001;

    /// 1 = Mac3, la aplicacion de la que cuelgan las plantillas de la remision.
    private const int IdAplicacionMac3 = 1;

    /// 0 = usados que se entregan en esta venta, 1 = los que quedaron de antes.
    private const int UsadosCreditoActual = 0;
    private const int UsadosCreditoAnterior = 1;

    private readonly IConfiguration _configuration;

    public RemisionImpresionRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<DataTable> ConsultarPlantillasAsync(CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        return await LlenarAsync(
            conn,
            "sp_n_ConsultaTemplateHtml",
            new Dictionary<string, object>
            {
                ["@IDTemplateHtml"] = 0,
                ["@IDTipoTemplateHtml"] = IdTipoTemplateHtmlRemision,
                ["@IDAplicacion"] = IdAplicacionMac3,
                ["@IDStatus"] = 0
            },
            ct);
    }

    public async Task<RemisionDatosVentaResultado> ConsultarDatosVentaAsync(
        int idVenta,
        int idUsuarioImpresion,
        int idDescuento,
        CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var informacion = await LlenarAsync(
            conn,
            "sp_n_VentasInformacion",
            new Dictionary<string, object>
            {
                ["@IDVenta"] = idVenta,
                ["@IDUsuarioImpresion"] = idUsuarioImpresion,
                /*
                  Sin equipo y sin "primera impresion", igual que MacReportes
                  cuando la remision se ve en pantalla. Pedir primera impresion
                  haria que el procedimiento RECHAZARA toda remision que alguien
                  ya haya impreso, que es justo lo que se consulta desde el web.
                */
                ["@EquipoImpresion"] = "",
                ["@PrimerImpresion"] = 0,
                ["@Reimpresion"] = 0
            },
            ct);

        if (informacion.Rows.Count == 0)
            return new RemisionDatosVentaResultado { Mensaje = $"La venta {idVenta} no existe." };

        var row = informacion.Rows[0];
        if (informacion.Columns.Contains("result") && Convert.ToString(row["result"]) == "0")
        {
            return new RemisionDatosVentaResultado
            {
                Mensaje = Convert.ToString(row["mensaje"]) ?? $"No se puede imprimir la venta {idVenta}."
            };
        }

        var datos = new RemisionDatosVenta
        {
            IdVenta = idVenta,
            Informacion = informacion,
            Detalle = await LlenarAsync(
                conn,
                "sp_n_VentasDetalle",
                new Dictionary<string, object> { ["@IDVenta"] = idVenta, ["@IDDescuento"] = idDescuento },
                ct),
            UsadosCargo = await LlenarAsync(
                conn,
                "sp_n_VentasUsadosCargo",
                new Dictionary<string, object> { ["@IDVenta"] = idVenta },
                ct),
            UsadosCreditoActual = await LlenarAsync(
                conn,
                "sp_n_VentasUsadosCredito",
                new Dictionary<string, object> { ["@Accion"] = UsadosCreditoActual, ["@IDVenta"] = idVenta },
                ct),
            UsadosCreditoAnterior = await LlenarAsync(
                conn,
                "sp_n_VentasUsadosCredito",
                new Dictionary<string, object> { ["@Accion"] = UsadosCreditoAnterior, ["@IDVenta"] = idVenta },
                ct),
            Devolucion = await LlenarAsync(
                conn,
                "sp_n_VentasDevolucion",
                new Dictionary<string, object> { ["@IDVenta"] = idVenta },
                ct)
        };

        return new RemisionDatosVentaResultado { Datos = datos };
    }

    /*
      Todos estos procedimientos devuelven UN resultado y legacy siempre toma
      Tables[0]; se hace igual. Se usa DataTable (y no una lista de DTOs) a
      proposito: el armado del html necesita el nombre Y el tipo de cada
      columna para decidir el formato del numero.
    */
    private static async Task<DataTable> LlenarAsync(
        SqlConnection conn,
        string sp,
        Dictionary<string, object> parametros,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand(sp, conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };

        foreach (var p in parametros)
            cmd.Parameters.AddWithValue(p.Key, p.Value);

        var tabla = new DataTable();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        tabla.Load(reader);
        return tabla;
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
}
