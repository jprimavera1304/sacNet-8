using System.Data;
using ISL_Service.Application.DTOs.VentasDevolucion;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/*
  DEVOLUCION: LOS MISMOS PROCEDIMIENTOS QUE MAC31, NI UNO NUEVO

  Los cinco que se llaman aqui son exactamente los que llama
  Legacy/Mac31/Mac31/Forms/Ventas/VentasDevolucion.cs por debajo (via
  Legacy/MacServicios2):

    sp_n_ConsultaVentas        ConsultarVenta()            linea 106
    sp_n_VentasDetalle         ConsultarVentaDetalle()     linea 169
    sp_n_VentasUsadosCargo     ConsultarVentaUsadosCargo() linea 298
    sp_n_VentasUsadosCredito   ConsultarVentaUsadosCredito() linea 395  (@Accion=2)
    sp_n_DevolucionVenta       btnGuardar_Click()          linea 669

  No se escribio ni una consulta a mano, ni un sp_w_: todo lo que hace falta ya
  lo devuelven ellos.

  POR QUE @IDVenta Y NO @IDsVenta EN sp_n_ConsultaVentas

  El procedimiento acepta las dos formas, pero legacy usa la de entero
  (ConsultaVentasInputDTO.IDVenta, VentasDevolucion.cs:110) y hay que usar la
  misma: probando contra las dos bases, el camino de la cadena '@IDsVenta'
  devuelve la remision en Produccion_svr (Tauro) y CERO renglones en MacZ
  (Zaragoza). Pasar por ahi habria dejado la pantalla vacia en una de las dos
  empresas sin ningun error que lo explicara.
*/
public class VentasDevolucionRepository : IVentasDevolucionRepository
{
    private const int CommandTimeoutSeconds = 500;

    private readonly IConfiguration _configuration;

    public VentasDevolucionRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<string> ConsultarFuncionalidadAsync(CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(
            "SELECT TOP 1 UPPER(ISNULL(Funcionalidad, '')) FROM Constantes;", conn)
        { CommandTimeout = CommandTimeoutSeconds };

        var v = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToString(v)?.Trim() ?? string.Empty;
    }

    public async Task<(VentasDevolucionVistaResponse Cabecera, bool Existe)> ConsultarCabeceraAsync(int idVenta, CancellationToken ct)
    {
        var vista = new VentasDevolucionVistaResponse { IdVenta = idVenta };

        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand("sp_n_ConsultaVentas", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };
        cmd.Parameters.AddWithValue("@IDsVenta", "");
        cmd.Parameters.AddWithValue("@IDVenta", idVenta);
        cmd.Parameters.AddWithValue("@IDEmpresa", 0);
        cmd.Parameters.AddWithValue("@IDCliente", 0);
        cmd.Parameters.AddWithValue("@IDUsuario", 0);
        cmd.Parameters.AddWithValue("@IDAgente", 0);
        cmd.Parameters.AddWithValue("@IDTipoDocumento", 0);
        cmd.Parameters.AddWithValue("@TipoDocumento", "");
        cmd.Parameters.AddWithValue("@FolioInicial", 0);
        cmd.Parameters.AddWithValue("@FolioFinal", 0);
        cmd.Parameters.AddWithValue("@FechaEmisionInicial", "");
        cmd.Parameters.AddWithValue("@FechaEmisionFinal", "");
        cmd.Parameters.AddWithValue("@FechaCancelInicial", "");
        cmd.Parameters.AddWithValue("@FechaCancelFinal", "");
        cmd.Parameters.AddWithValue("@FechaPagoInicial", "");
        cmd.Parameters.AddWithValue("@FechaPagoFinal", "");
        cmd.Parameters.AddWithValue("@Formato", 0);
        cmd.Parameters.AddWithValue("@IDUsuarioActual", 0);
        cmd.Parameters.AddWithValue("@IDsClientes", "");
        cmd.Parameters.AddWithValue("@IDsProducto", "");
        cmd.Parameters.AddWithValue("@IDProducto", 0);

        await using var rd = await cmd.ExecuteReaderAsync(ct);
        if (!await rd.ReadAsync(ct)) return (vista, false);

        vista.Folio = LeerTexto(rd, "Folio");
        vista.Empresa = LeerTexto(rd, "Empresa");
        vista.Agente = LeerTexto(rd, "NumeroNombreAgente");
        vista.Cliente = $"{LeerTexto(rd, "NumeroCliente")} - {LeerTexto(rd, "NombreCliente")}";
        vista.MsgErr = LeerTexto(rd, "msgErr");

        /*
          El SP las devuelve como texto 'SI' / 'NO' — asi las lee Mac31
          (ConsultarVentas.cs:3606-3607) y asi se comparan, no como bit.
        */
        vista.Cancelada = LeerTexto(rd, "Cancelada").Equals("SI", StringComparison.OrdinalIgnoreCase);
        vista.Pagada = LeerTexto(rd, "Pagada").Equals("SI", StringComparison.OrdinalIgnoreCase);

        /*
          LAS DOS SUMAS DE MAC31, EN EL MISMO SITIO Y CON LOS MISMOS SUMANDOS.

          VentasDevolucion.cs:143  lblCreditos   = Creditos + IvaCreditos
          VentasDevolucion.cs:145  lblTotalPagar = TotalPagar + Cargos

          Si se enseñara Creditos a secas, en Tauro faltaria el IVA de los
          cascos y el numero no cuadraria con la pantalla de Mac31 abierta al
          lado. Lo demas son columnas tal cual.
        */
        var creditos = LeerDecimal(rd, "Creditos");
        var ivaCreditos = LeerDecimal(rd, "IvaCreditos");
        var cargos = LeerDecimal(rd, "Cargos");

        vista.Totales = new VentasDevolucionTotales
        {
            Importe = LeerDecimal(rd, "Importe"),
            Cargos = cargos,
            Creditos = creditos + ivaCreditos,
            TotalPagar = LeerDecimal(rd, "TotalPagar") + cargos,
            Abonos = LeerDecimal(rd, "Abonos"),
            Descuentos = LeerDecimal(rd, "Descuentos"),
            Saldo = LeerDecimal(rd, "Saldo")
        };

        return (vista, true);
    }

    public async Task<List<VentasDevolucionPartida>> ConsultarPartidasAsync(int idVenta, CancellationToken ct)
    {
        var lista = new List<VentasDevolucionPartida>();

        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand("sp_n_VentasDetalle", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };
        cmd.Parameters.AddWithValue("@IDVenta", idVenta);
        // Mac31 manda 0: aqui no se esta consultando un descuento concreto.
        cmd.Parameters.AddWithValue("@IDDescuento", 0);

        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
        {
            var numPiezas = LeerEntero(rd, "NumPiezas");
            lista.Add(new VentasDevolucionPartida
            {
                IdVentaDetalle = LeerEntero(rd, "IDVentaDetalle"),
                IdProducto = LeerEntero(rd, "IDProducto"),
                IdAlmacen = LeerEntero(rd, "IDAlmacen"),
                IdTipoUsado = LeerEntero(rd, "IDTipoUsado"),
                Codigo = LeerTexto(rd, "Codigo"),
                Concepto = LeerTexto(rd, "Concepto"),
                PrecioLista = LeerDecimal(rd, "PrecioLista"),
                MontoDescuento = LeerDecimal(rd, "MontoDescuento"),
                ImportePartida = LeerDecimal(rd, "ImportePartida"),
                Cantidad = LeerEntero(rd, "Cantidad"),
                CantidadMostrarFtm = LeerTexto(rd, "CantidadMostrarFtm"),
                /*
                  Un cero aqui seria una division por cero al convertir cajas a
                  piezas. Mac31 nunca lo ve porque el catalogo siempre trae al
                  menos 1; se deja el piso por si alguna fila vieja no lo trae.
                */
                NumPiezas = numPiezas <= 0 ? 1 : numPiezas,
                UnidadMedida = LeerTexto(rd, "UnidadMedida")
            });
        }

        return lista;
    }

    public async Task<VentasDevolucionCascosResumen> ConsultarCascosRemisionAsync(int idVenta, CancellationToken ct)
    {
        var resumen = new VentasDevolucionCascosResumen();

        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand("sp_n_VentasUsadosCargo", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };
        cmd.Parameters.AddWithValue("@IDVenta", idVenta);

        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
        {
            /*
              LA COLUMNA DE COSTO ES CostoVentaCargoConIva, NO CostoUsadoCargo.

              Mac31 cambio la columna de la rejilla a proposito
              (VentasDevolucion.cs:341, la linea de CostoUsadoCargo quedo
              comentada arriba): lo que se enseña es el costo CON IVA redondeado
              hacia arriba, que es el que cuadra con el importe del renglon.
              Con el otro la multiplicacion no da.
            */
            resumen.Renglones.Add(new VentasDevolucionCasco
            {
                Tipo = LeerTexto(rd, "TipoUsadoCargo"),
                Costo = LeerDecimal(rd, "CostoVentaCargoConIva"),
                Cantidad = LeerEntero(rd, "CantidadUsadoCargo"),
                Importe = LeerDecimal(rd, "TotalUsadoCargo")
            });

            // Los totales vienen repetidos en cada renglon; el ultimo vale igual.
            resumen.Importe = LeerDecimal(rd, "TotalImporteUsadoCargo");
            resumen.Iva = LeerDecimal(rd, "TotalIVAUsadoCargo");
            resumen.Total = LeerDecimal(rd, "TotalTotalUsadoCargo");
            resumen.Piezas = LeerEntero(rd, "TotalCantidadUsadoCargo");
        }

        return resumen;
    }

    public async Task<VentasDevolucionCascosResumen> ConsultarCascosEntregadosAsync(int idVenta, CancellationToken ct)
    {
        var resumen = new VentasDevolucionCascosResumen();

        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand("sp_n_VentasUsadosCredito", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };
        /*
          @Accion = 2 es UsadosCreditoActualCatalogo (Legacy/MacServicios2,
          Utils/Enums.cs:104). Es la que usa esta pantalla —ConsultarActualCatalogo,
          VentasDevolucion.cs:403— y trae TODOS los tipos de casco del catalogo,
          tambien los que van en cero. Por eso la rejilla de ENTREGADOS nunca
          sale vacia aunque no se haya entregado nada.
        */
        cmd.Parameters.AddWithValue("@Accion", 2);
        cmd.Parameters.AddWithValue("@IDVenta", idVenta);

        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
        {
            resumen.Renglones.Add(new VentasDevolucionCasco
            {
                Tipo = LeerTexto(rd, "TipoUsadoCredito"),
                Costo = LeerDecimal(rd, "CostoUsadoCredito"),
                Cantidad = LeerEntero(rd, "CantidadUsadoCredito"),
                Importe = LeerDecimal(rd, "ImporteUsadoCredito")
            });

            resumen.Importe = LeerDecimal(rd, "TotalImporteUsadoCredito");
            resumen.Iva = LeerDecimal(rd, "TotalIVAUsadoCredito");
            resumen.Total = LeerDecimal(rd, "TotalTotalUsadoCredito");
            resumen.Piezas = LeerEntero(rd, "TotalCantidadUsadoCredito");
        }

        return resumen;
    }

    public async Task<VentasDevolucionResponse> DevolverAsync(
        int idVenta,
        int idUsuarioDevolucion,
        string idsVentasDetalle,
        string cantidades,
        string equipo,
        CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand("sp_n_DevolucionVenta", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };
        cmd.Parameters.AddWithValue("@IDVenta", idVenta);
        cmd.Parameters.AddWithValue("@IDUsuarioDevolucion", idUsuarioDevolucion);
        cmd.Parameters.AddWithValue("@IDsVentasDetalle", idsVentasDetalle);
        cmd.Parameters.AddWithValue("@Cantidades", cantidades);
        cmd.Parameters.AddWithValue("@Equipo", equipo);

        await using var rd = await cmd.ExecuteReaderAsync(ct);
        if (!await rd.ReadAsync(ct))
        {
            return new VentasDevolucionResponse
            {
                Ok = false,
                IdVenta = idVenta,
                Mensaje = "El procedimiento de devolución no contestó."
            };
        }

        var result = LeerEntero(rd, "result");
        var mensaje = LeerTexto(rd, "mensaje");

        /*
          Mac31 trata como bueno UNICAMENTE result = 1 con mensaje vacio
          (VentasDevolucion.cs:731-741): si el SP devolvio texto, eso es un "no"
          de negocio aunque la llamada haya terminado bien, y se enseña tal cual.
          El '@@@@' es el salto de linea del SP.
        */
        return new VentasDevolucionResponse
        {
            Ok = result == 1 && mensaje.Length == 0,
            IdVenta = LeerEntero(rd, "IDVenta"),
            Folio = LeerTexto(rd, "Folio"),
            Mensaje = mensaje.Replace("@@@@", "\n")
        };
    }

    /* ---------------------------------------------------------------- */

    private static string LeerTexto(SqlDataReader rd, string columna)
    {
        var i = rd.GetOrdinal(columna);
        return rd.IsDBNull(i) ? "" : (Convert.ToString(rd.GetValue(i)) ?? "").Trim();
    }

    private static int LeerEntero(SqlDataReader rd, string columna)
    {
        var i = rd.GetOrdinal(columna);
        if (rd.IsDBNull(i)) return 0;
        return Convert.ToInt32(rd.GetValue(i));
    }

    private static decimal LeerDecimal(SqlDataReader rd, string columna)
    {
        var i = rd.GetOrdinal(columna);
        if (rd.IsDBNull(i)) return 0m;
        return Convert.ToDecimal(rd.GetValue(i));
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
