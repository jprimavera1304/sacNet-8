using System.Data;
using ISL_Service.Application.DTOs.VentasCancelacion;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/*
  CANCELAR REMISIONES: LOS MISMOS PROCEDIMIENTOS QUE MAC31

  Ni uno nuevo. Los cuatro que se llaman aqui son los que llama Mac31 por debajo
  (Legacy/MacServicios2, repositorios VentasCancelacion, ConsultaConcentrados,
  VentasConcentrado y CatFormaProceso):

    sp_n_CancelarVenta                 cancela, y es quien decide de verdad
    sp_n_ConsultarConcentrados         ¿el folio sigue en un concentrado abierto?
    sp_n_ConsultarConcentradoFolio     el renglon del concentrado que lo tiene
    sp_n_ActualizarConcentradoDetalle  lo saca del concentrado
    sp_n_InsertarContrasenaCodigo      deja constancia del intento de contrasena

  La unica consulta escrita a mano es la de los datos de la venta, y es una
  lectura de la tabla Ventas: no hay procedimiento de legacy que devuelva
  "cancelada / pagada / abonos / facturado" de una lista de ventas sin arrastrar
  la consulta completa de la pantalla, que tarda segundos.
*/
public class VentasCancelacionRepository : IVentasCancelacionRepository
{
    private const int CommandTimeoutSeconds = 500;

    private readonly IConfiguration _configuration;

    public VentasCancelacionRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<ConstantesCancelacion> ConsultarConstantesAsync(CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(
            "SELECT TOP 1 UPPER(ISNULL(Funcionalidad,'')) AS Funcionalidad, ISNULL(EsCentroServicio,0) AS EsCentroServicio, FechaOperacion FROM Constantes;",
            conn)
        { CommandTimeout = CommandTimeoutSeconds };

        await using var rd = await cmd.ExecuteReaderAsync(ct);
        if (!await rd.ReadAsync(ct))
            return new ConstantesCancelacion();

        return new ConstantesCancelacion
        {
            Funcionalidad = rd.GetString(0),
            EsCentroServicio = rd.GetInt32(1),
            FechaOperacion = rd.IsDBNull(2) ? DateTime.Today : rd.GetDateTime(2)
        };
    }

    public async Task<List<VentaParaCancelar>> ConsultarVentasAsync(IReadOnlyCollection<int> idsVenta, CancellationToken ct)
    {
        var lista = new List<VentaParaCancelar>();
        if (idsVenta.Count == 0) return lista;

        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        /*
          Los ids van como PARAMETROS, uno por uno, y no pegados en el texto. Son
          numeros que llegan del navegador; concatenarlos seria abrir la puerta a
          que alguien mande algo que no es un numero.

          El folio con prefijo sale de fn_DevuelvePrefijoFolio —la misma funcion
          que usa sp_n_CancelarVenta— porque el prefijo "CV-" es justo lo que
          Mac31 mira para negarse a cancelar un cambio de vigencia.
        */
        var marcadores = idsVenta.Select((_, i) => "@id" + i).ToList();

        await using var cmd = new SqlCommand($@"
SELECT V.IDVenta,
       V.Folio,
       dbo.fn_DevuelvePrefijoFolio(V.IDVenta, V.Folio, V.SoloServicios, V.SoloAceites, V.SoloLogistica, V.SoloDevueltaCliente, V.IDTipoDocumento) AS FolioFtm,
       V.IDEmpresa,
       V.IDTipoDocumento,
       CASE WHEN V.[Fecha Cancelacion] IS NULL THEN 0 ELSE 1 END AS Cancelada,
       CASE WHEN V.[Fecha Pago] IS NULL THEN 0 ELSE 1 END AS Pagada,
       ISNULL(V.[Total a Pagar], 0) AS TotalPagar,
       ISNULL(V.Abonos, 0) AS Abonos,
       ISNULL(V.TotalFacturado, 0) AS TotalFacturado,
       LTRIM(ISNULL(C.[Apellido Paterno], '') + ' ' + ISNULL(C.[Apellido Materno], '') + ' ' + ISNULL(C.Nombre, '')) AS NombreCliente
FROM Ventas V
     LEFT JOIN Clientes C ON C.IDCliente = V.IDCliente
WHERE V.IDVenta IN ({string.Join(", ", marcadores)});", conn)
        { CommandTimeout = CommandTimeoutSeconds };

        var i = 0;
        foreach (var id in idsVenta)
            cmd.Parameters.AddWithValue("@id" + i++, id);

        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
        {
            lista.Add(new VentaParaCancelar
            {
                IdVenta = rd.GetInt32(0),
                Folio = rd.GetInt32(1),
                FolioFtm = rd.IsDBNull(2) ? "" : rd.GetString(2).Trim(),
                IdEmpresa = rd.GetInt32(3),
                IdTipoDocumento = rd.GetInt32(4),
                Cancelada = rd.GetInt32(5) == 1,
                Pagada = rd.GetInt32(6) == 1,
                TotalPagar = rd.GetDecimal(7),
                Abonos = rd.GetDecimal(8),
                TotalFacturado = rd.GetDecimal(9),
                NombreCliente = rd.IsDBNull(10) ? "" : rd.GetString(10).Trim()
            });
        }

        return lista;
    }

    /*
      Mac31 pregunta por FOLIO y no por IDVenta (ConsultaConcentrados), y mira
      una sola cosa del primer renglon: si el concentrado NO tiene fecha de
      cancelacion, el folio sigue dentro y hay que sacarlo antes.
    */
    public async Task<ConcentradoDeVenta?> ConsultarConcentradoAbiertoAsync(int folioVenta, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand("sp_n_ConsultarConcentrados", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };
        cmd.Parameters.AddWithValue("@IDConcentrado", 0);
        cmd.Parameters.AddWithValue("@IDRepartidor", 0);
        cmd.Parameters.AddWithValue("@IDUsuarioConcentrado", 0);
        cmd.Parameters.AddWithValue("@FolioInicialConcentrado", 0);
        cmd.Parameters.AddWithValue("@FolioFinalConcentrado", 0);
        cmd.Parameters.AddWithValue("@FechaInicialConcentrado", "");
        cmd.Parameters.AddWithValue("@FechaFinalConcentrado", "");
        cmd.Parameters.AddWithValue("@IDEmpresaVenta", 0);
        cmd.Parameters.AddWithValue("@FolioInicialVenta", folioVenta);
        cmd.Parameters.AddWithValue("@FolioFinalVenta", folioVenta);
        cmd.Parameters.AddWithValue("@IDUsuarioActual", 0);

        await using var rd = await cmd.ExecuteReaderAsync(ct);
        if (!await rd.ReadAsync(ct)) return null;

        var fechaCancelacion = LeerTexto(rd, "FechaCancelacionFtm");
        if (fechaCancelacion.Length > 0) return null;

        return new ConcentradoDeVenta
        {
            IdConcentrado = LeerEntero(rd, "IDConcentrado"),
            FolioConcentrado = LeerEntero(rd, "Folio")
        };
    }

    public async Task<string?> ConsultarContrasenaAutorizacionAsync(int idUsuario, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(
            "SELECT TOP 1 ISNULL(AplicaContrasenaAutorizacion, 0), ISNULL(ContrasenaAutorizacion, '') FROM Usuarios WHERE IDUsuario = @IDUsuario;",
            conn)
        { CommandTimeout = CommandTimeoutSeconds };
        cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);

        await using var rd = await cmd.ExecuteReaderAsync(ct);
        if (!await rd.ReadAsync(ct)) return null;

        return rd.GetInt32(0) == 1 ? rd.GetString(1).Trim() : null;
    }

    public async Task RegistrarIntentoContrasenaAsync(string forma, bool correcto, string contrasena, int idUsuario, string equipo, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand("sp_n_InsertarContrasenaCodigo", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };
        cmd.Parameters.AddWithValue("@IDForma", 0);
        cmd.Parameters.AddWithValue("@IDProceso", 0);
        cmd.Parameters.AddWithValue("@Forma", forma);
        cmd.Parameters.AddWithValue("@Correcto", correcto ? 1 : 0);
        cmd.Parameters.AddWithValue("@Contrasena", contrasena);
        cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
        cmd.Parameters.AddWithValue("@Equipo", equipo);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    /*
      DOS PASOS, COMO EN MAC31

      El concentrado se guarda por renglon (IDConcentradoDetalle) y lo que
      tenemos es el folio, asi que primero hay que ir a buscar ese renglon
      —sp_n_ConsultarConcentradoFolio— y luego pedir que lo borre
      —sp_n_ActualizarConcentradoDetalle—.

      Si la busqueda no devuelve nada, no se hace nada: es lo mismo que hace
      Mac31 (`if (dt.Rows.Count > 0)`). El folio ya no estaba asignado.
    */
    public async Task QuitarDelConcentradoAsync(VentaParaCancelar venta, ConcentradoDeVenta concentrado, int idUsuario, string equipo, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        int idConcentradoDetalle;

        await using (var buscar = new SqlCommand("sp_n_ConsultarConcentradoFolio", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        })
        {
            buscar.Parameters.AddWithValue("@IDEmpresaVenta", venta.IdEmpresa);
            buscar.Parameters.AddWithValue("@FolioVenta", venta.Folio);
            buscar.Parameters.AddWithValue("@IDConcentradoActual", concentrado.IdConcentrado);
            buscar.Parameters.AddWithValue("@IDUsuarioActual", idUsuario);
            buscar.Parameters.AddWithValue("@IDTipoDocumento", venta.IdTipoDocumento);

            await using var rd = await buscar.ExecuteReaderAsync(ct);
            if (!await rd.ReadAsync(ct)) return;
            idConcentradoDetalle = LeerEntero(rd, "IDConcentradoDetalle");
        }

        if (idConcentradoDetalle <= 0) return;

        await using var quitar = new SqlCommand("sp_n_ActualizarConcentradoDetalle", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };
        quitar.Parameters.AddWithValue("@IDConcentrado", concentrado.IdConcentrado);
        quitar.Parameters.AddWithValue("@IDsConcentradoDetalleEliminar", idConcentradoDetalle.ToString());
        /*
          Vacios a proposito: el procedimiento entiende "0" y "" como "dejalo
          como esta" y rellena con lo que ya tiene el concentrado. Aqui solo se
          quita un renglon; el repartidor y las observaciones no son asunto
          nuestro.
        */
        quitar.Parameters.AddWithValue("@IDsVentaAgregar", "");
        quitar.Parameters.AddWithValue("@IDRepartidor", 0);
        /*
          Mac31 manda IDUsuario 0 aqui, por un descuido suyo: el DTO se crea
          vacio y la pantalla solo llena dos campos. Nosotros mandamos el usuario
          de verdad, que es lo que el procedimiento guarda como "quien modifico".
          Un cero ahi es una firma en blanco.
        */
        quitar.Parameters.AddWithValue("@IDUsuario", idUsuario);
        quitar.Parameters.AddWithValue("@Equipo", equipo);
        quitar.Parameters.AddWithValue("@Observaciones", "");

        await quitar.ExecuteNonQueryAsync(ct);
    }

    public async Task<VentasCancelacionResultado> CancelarAsync(VentaParaCancelar venta, int idUsuario, string equipo, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand("sp_n_CancelarVenta", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };
        cmd.Parameters.AddWithValue("@IDVenta", venta.IdVenta);
        cmd.Parameters.AddWithValue("@IDUsuarioCancelo", idUsuario);
        cmd.Parameters.AddWithValue("@EquipoCancelo", equipo);

        await using var rd = await cmd.ExecuteReaderAsync(ct);

        if (!await rd.ReadAsync(ct))
        {
            // El procedimiento SIEMPRE devuelve un renglon. Si no, algo se rompio.
            return new VentasCancelacionResultado
            {
                IdVenta = venta.IdVenta,
                Folio = venta.FolioFtm,
                Cancelada = false,
                Mensaje = "El procedimiento de cancelación no devolvió resultado."
            };
        }

        var result = LeerEntero(rd, "result");
        var mensaje = LeerTexto(rd, "mensaje");
        var folio = LeerTexto(rd, "Folio");

        return new VentasCancelacionResultado
        {
            IdVenta = venta.IdVenta,
            Folio = folio.Length > 0 ? folio : venta.FolioFtm,
            Cancelada = result == 1,
            /*
              "@@@@" es el salto de linea de legacy dentro de un mensaje. Se
              traduce, no se quita: sin el, dos frases quedan pegadas.
            */
            Mensaje = mensaje.Replace("@@@@", "\n")
        };
    }

    /* ------------------------------------------------------------------ */

    private static int LeerEntero(SqlDataReader rd, string columna)
    {
        var i = rd.GetOrdinal(columna);
        if (rd.IsDBNull(i)) return 0;
        return int.TryParse(Convert.ToString(rd.GetValue(i)), out var v) ? v : 0;
    }

    private static string LeerTexto(SqlDataReader rd, string columna)
    {
        var i = rd.GetOrdinal(columna);
        return rd.IsDBNull(i) ? "" : (Convert.ToString(rd.GetValue(i)) ?? "").Trim();
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
