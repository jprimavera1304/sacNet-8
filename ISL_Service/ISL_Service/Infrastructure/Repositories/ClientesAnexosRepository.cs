using System.Data;
using ISL_Service.Application.DTOs.ClientesAnexos;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// Las cuatro lecturas de las pestanas de la ficha del cliente.
///
/// NINGUNA PASA POR sp_n_ConsultaConstantes: son lecturas de verdad. Los
/// endpoints de legacy que lo llaman hacen el cierre del dia como efecto
/// colateral y quedaron fuera de esta entrega.
/// </summary>
public class ClientesAnexosRepository : IClientesAnexosRepository
{
    private const string SpBonificaciones = "dbo.sp_n_ConsultarClienteBonificacion";
    private const string SpCentrosServicio = "dbo.sp_n_ConsultarClienteCentroServicio";
    private const string SpLog = "dbo.sp_n_ConsultarClientesLog";
    private const string SpFacturacion = "dbo.sp_n_ConsultarClientesFacturacion";

    private const int FormatoTodosLosCampos = 0;

    private readonly IConfiguration _configuration;

    public ClientesAnexosRepository(IConfiguration configuration)
    {
        _configuration = configuration;
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

    private async Task<DataTable> EjecutarAsync(string sp, Action<SqlCommand> parametros, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(sp, conn) { CommandType = CommandType.StoredProcedure };
        parametros(cmd);

        var dt = new DataTable();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        dt.Load(reader);
        return dt;
    }

    public async Task<List<ClienteBonificacionDto>> ConsultarBonificacionesAsync(int idCliente, CancellationToken ct)
    {
        var dt = await EjecutarAsync(SpBonificaciones, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCliente", idCliente);
        }, ct);

        var lista = Funciones.DataTableToList<ClienteBonificacionDto>(dt);

        /*
          EL ISNULL DEL SP DEVUELVE DOS COMILLAS, NO UNA CADENA VACIA.
          sp_n_ConsultarClienteBonificacion arma su SELECT dentro de una cadena y
          escribe ISNULL(UsuModifico.Usuario, '''') — al salir del escape queda
          ISNULL(..., '') desde el punto de vista de SQL, pero el texto literal
          que llega cuando el valor es nulo es `''`: dos apostrofos. Legacy lo
          pintaba tal cual en la pantalla. Aqui se traduce a nulo, que es lo que
          significa: "no se ha modificado".
        */
        foreach (var fila in lista)
        {
            if (fila.UsuarioModifico == "''") fila.UsuarioModifico = null;
        }

        return lista;
    }

    public async Task<List<ClienteCentroServicioDto>> ConsultarCentrosServicioAsync(int idCliente, int idEmpresa, CancellationToken ct)
    {
        var dt = await EjecutarAsync(SpCentrosServicio, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCliente", idCliente);
            // En legacy este parametro se llama @IDEmpresaCS y se alimenta del
            // campo IDEmpresa del DTO. El SP lo usa para decidir a que base
            // apunta (BDZara / BDLuro / la local).
            cmd.Parameters.AddWithValue("@IDEmpresaCS", idEmpresa);
        }, ct);

        return Funciones.DataTableToList<ClienteCentroServicioDto>(dt);
    }

    public async Task<List<ClienteLogDto>> ConsultarLogAsync(int idCliente, CancellationToken ct)
    {
        var dt = await EjecutarAsync(SpLog, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCliente", idCliente);
            /*
              @FechaInicial Y @FechaFinal NO SE MANDAN, A PROPOSITO.
              El SP los declara con defaults de prueba ('12-01-2024 00:00:00' /
              '12-30-2024 23:59:59') y su WHERE de fechas esta COMENTADO, asi que
              no filtran nada. Legacy tampoco los mandaba. Mandarlos aqui haria
              creer que el rango se respeta: no se respeta, y el endpoint no
              ofrece filtro por fecha (ver el controlador).
            */
        }, ct);

        return Funciones.DataTableToList<ClienteLogDto>(dt);
    }

    public async Task<List<ClienteFacturacionDto>> ConsultarFacturacionAsync(ClienteFacturacionFiltro filtro, CancellationToken ct)
    {
        var dt = await EjecutarAsync(SpFacturacion, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDClienteFacturacion", filtro.IDClienteFacturacion);
            cmd.Parameters.AddWithValue("@IDCliente", filtro.IDCliente);
            cmd.Parameters.AddWithValue("@IDStatus", filtro.IDStatus);
            cmd.Parameters.AddWithValue("@NombreFacturacion", filtro.NombreFacturacion ?? string.Empty);
            cmd.Parameters.AddWithValue("@RFC", filtro.Rfc ?? string.Empty);
            // @Formato = 1 devuelve cuatro columnas con los encabezados en
            // espanol ("[Fecha Alta]") para pegar en Excel. No se expone.
            cmd.Parameters.AddWithValue("@Formato", FormatoTodosLosCampos);
            cmd.Parameters.AddWithValue("@Identico", filtro.Exacto ? 1 : 0);
            cmd.Parameters.AddWithValue("@IDEmpresaCS", filtro.IDEmpresaCS);
        }, ct);

        return Funciones.DataTableToList<ClienteFacturacionDto>(dt);
    }
}
