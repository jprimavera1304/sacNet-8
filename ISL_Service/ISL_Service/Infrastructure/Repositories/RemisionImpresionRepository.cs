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

    /*
      LOS DOS NUMEROS DE LA REMISION DE ZARAGOZA.

      1202 "Remision Zaragoza Generico" es la que imprime Mac31 hoy. 1201
      "Remision Sin Precio" es la que elige el codigo de MacServicios2 que
      tenemos a la vista. Se prefiere la 1202 cuando la base la tiene y se cae a
      la 1201 cuando no: asi sale lo que la gente ve en su pantalla, sin
      depender de cual de las dos versiones del servicio este publicada.
    */
    private const int IdReporteRemisionZaragoza = 1202;
    private const int IdReporteRemisionZaragozaPrevia = 1201;

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
        int primerImpresion,
        int reimpresion,
        string equipoImpresion,
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
                  LAS BANDERAS AHORA LLEGAN DE ARRIBA, NO SE FIJAN EN CERO.

                  Antes iban en cero siempre, con el argumento de que pedir
                  primera impresion haria que el procedimiento rechazara toda
                  remision ya impresa. Eso es cierto, pero es exactamente lo que
                  hace Mac31 cuando alguien aprieta IMPRIMIR: el rechazo es la
                  funcion del boton, no un estorbo (ConsultarVentas.cs:4213).

                  Quien decide que valores llegan aqui es VentasImpresionReglas:
                  Pantalla manda 0 y 0 —y por eso ver una remision sigue sin
                  bloquear nada, como hasta ahora—, Imprimir manda 1 y 0, y
                  Reimprimir manda 0 y 1. En Zaragoza siempre llegan 0 y 0
                  porque alla legacy ni siquiera pasa por este camino.

                  El equipo se manda igual que legacy —alli va el nombre de la
                  maquina— porque el procedimiento lo GUARDA en Ventas y en
                  Pedidos; dejarlo vacio dejaba el rastro a medias.
                */
                ["@EquipoImpresion"] = equipoImpresion ?? "",
                ["@PrimerImpresion"] = primerImpresion,
                ["@Reimpresion"] = reimpresion
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
      LA UNICA CONSULTA DE ESTE ARCHIVO QUE NO ES UN sp_n_

      Y es a proposito: no existe procedimiento de legacy que conteste "¿quien
      imprimio esto?" sin imprimirlo de paso. El unico que lo sabe es
      sp_n_VentasInformacion, y ese ESCRIBE (sella la primera impresion), asi
      que llamarlo solo para preguntar seria marcar papeles sin querer.

      La consulta es la misma que arma el mensaje dentro del procedimiento
      —Ventas + Usuarios y fn_DevuelvePrefijoFolio para el folio con prefijo—,
      copiada del bloque `IF @PrimerImpresion = 1`. No se toca ninguna tabla.

      La funcion del folio existe igual en las dos bases; se comprobo
      ejecutandola en Produccion_svr y en MacZ.
    */
    public async Task<List<RemisionImpresionPrevia>> ConsultarImpresionesPreviasAsync(
        IReadOnlyCollection<int> idsVenta,
        CancellationToken ct)
    {
        var ids = idsVenta.Where(x => x > 0).Distinct().ToList();
        if (ids.Count == 0)
            return new List<RemisionImpresionPrevia>();

        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        /* Un parametro por id: nada de concatenar la lista dentro del texto. */
        var marcadores = string.Join(",", ids.Select((_, i) => "@id" + i));

        await using var cmd = new SqlCommand(
            "SELECT V.IDVenta, " +
            "       dbo.fn_DevuelvePrefijoFolio(V.IDVenta, V.Folio, V.SoloServicios, V.SoloAceites, " +
            "                                   V.SoloLogistica, V.SoloDevueltaCliente, V.IDTipoDocumento) AS FolioFtm, " +
            "       ISNULL(U.Usuario, '') AS Usuario, " +
            "       ISNULL(FORMAT(V.FechaImpresion, 'dd-MM-yyyy HH:mm'), '') AS Cuando, " +
            "       ISNULL(V.EquipoImpresion, '') AS Equipo " +
            "FROM Ventas V " +
            "     LEFT JOIN Usuarios U ON U.IDUsuario = V.IDUsuarioImpresion " +
            $"WHERE V.IDVenta IN ({marcadores}) AND ISNULL(V.IDUsuarioImpresion, 0) <> 0",
            conn)
        {
            CommandTimeout = CommandTimeoutSeconds
        };

        for (var i = 0; i < ids.Count; i++)
            cmd.Parameters.AddWithValue("@id" + i, ids[i]);

        var previas = new List<RemisionImpresionPrevia>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            previas.Add(new RemisionImpresionPrevia
            {
                IdVenta = reader.GetInt32(0),
                FolioFtm = Convert.ToString(reader[1]) ?? "",
                Usuario = Convert.ToString(reader[2]) ?? "",
                Cuando = Convert.ToString(reader[3]) ?? "",
                Equipo = Convert.ToString(reader[4]) ?? ""
            });
        }

        return previas;
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

    public async Task<string> ConsultarFuncionalidadAsync(CancellationToken ct)
    {
        var constantes = await ConsultarConstantesAsync(ct);
        return constantes.Rows.Count > 0 ? Celda(constantes.Rows[0], "Funcionalidad") : "";
    }

    public async Task<RemisionLogos> ConsultarLogosAsync(CancellationToken ct)
    {
        var constantes = await ConsultarConstantesAsync(ct);
        if (constantes.Rows.Count == 0)
            return new RemisionLogos();

        var fila = constantes.Rows[0];

        /*
          Igual que en la remision de Tauro: legacy usa PathImagenes, que es una
          carpeta local del servidor donde corre MacReportes. Este backend no
          corre ahi, asi que si la carpeta no existe se usa PathImagenesServer,
          que es la MISMA carpeta publicada por http. Sin esto el logo sale como
          imagen rota.
        */
        var local = Celda(fila, "PathImagenes");
        var ruta = local != "" && Directory.Exists(local) ? local : Celda(fila, "PathImagenesServer");
        if (ruta == "") ruta = local;

        return new RemisionLogos
        {
            Logo = ruta + Celda(fila, "LogoMacReportes"),
            MarcaDeAgua = ruta + Celda(fila, "LogoMacWM")
        };
    }

    public async Task<DataTable> ConsultarPlantillasZaragozaAsync(CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var plantillas = await PlantillasDeAsync(conn, IdReporteRemisionZaragoza, ct);
        if (plantillas.Rows.Count > 0)
            return plantillas;

        return await PlantillasDeAsync(conn, IdReporteRemisionZaragozaPrevia, ct);
    }

    public async Task<RemisionZaragozaResultado> ConsultarDatosVentaZaragozaAsync(
        int idVenta,
        int idUsuarioImpresion,
        CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var datos = await LlenarAsync(
            conn,
            "sp_n_rptVentasRemisionSinPrecio",
            new Dictionary<string, object>
            {
                ["@IDVenta"] = idVenta,
                ["@IDUsuarioImpresion"] = idUsuarioImpresion,
                /*
                  AQUI SI SE QUEDAN EN CERO, Y NO ES UN OLVIDO.

                  En Zaragoza, Imprimir y Reimprimir NO mandan banderas: la rama
                  de ZARA en Imprimir() (ConsultarVentas.cs, bloque
                  `funcionalidad.Contains("ZARA")` con EsCentroServicio == 0)
                  llama a VerVenta y se sale antes del servicio que las lleva, y
                  la de Centro de Servicio declara `PrimerImpresion = 0,
                  Reimpresion = 0` con todas sus letras.

                  O sea: alla los tres botones entregan el mismo papel. Poner un
                  1 aqui haria que el web de Zaragoza rechazara remisiones que
                  Mac31 imprime sin chistar. Ver VentasImpresionReglas.Banderas.
                */
                ["@EquipoImpresion"] = "",
                ["@PrimerImpresion"] = 0,
                ["@Reimpresion"] = 0
            },
            ct);

        if (datos.Rows.Count == 0)
            return new RemisionZaragozaResultado { Mensaje = $"La venta {idVenta} no existe." };

        var fila = datos.Rows[0];
        if (datos.Columns.Contains("result") && Celda(fila, "result") == "0")
        {
            return new RemisionZaragozaResultado
            {
                Mensaje = Celda(fila, "mensaje") is { Length: > 0 } m ? m : $"No se puede imprimir la venta {idVenta}."
            };
        }

        return new RemisionZaragozaResultado { Datos = datos };
    }

    private async Task<DataTable> PlantillasDeAsync(SqlConnection conn, int idReporte, CancellationToken ct)
        => await LlenarAsync(
            conn,
            "sp_n_ConsultaTemplateHtml",
            new Dictionary<string, object>
            {
                ["@IDTemplateHtml"] = 0,
                ["@IDTipoTemplateHtml"] = idReporte,
                ["@IDAplicacion"] = IdAplicacionMac3,
                ["@IDStatus"] = 0
            },
            ct);

    private async Task<DataTable> ConsultarConstantesAsync(CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        /*
          Sin validar actividad: aqui solo se quieren las rutas de las imagenes y
          la funcionalidad de la empresa. Con @ValidaActividad en 1 el
          procedimiento ademas registra al usuario, y esto no es una accion del
          usuario, es armar un papel.
        */
        return await LlenarAsync(
            conn,
            "sp_n_ConsultaConstantes",
            new Dictionary<string, object>
            {
                ["@ValidaActividad"] = 0,
                ["@IDUsuario"] = 0,
                ["@Equipo"] = ""
            },
            ct);
    }

    private static string Celda(DataRow fila, string columna)
    {
        if (!fila.Table.Columns.Contains(columna))
            return "";

        var valor = fila[columna];
        return valor == DBNull.Value ? "" : Convert.ToString(valor) ?? "";
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
