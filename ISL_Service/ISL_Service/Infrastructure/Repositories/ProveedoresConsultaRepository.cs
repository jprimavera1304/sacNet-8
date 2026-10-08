using System.Data;
using ISL_Service.Application.DTOs.ProveedoresConsulta;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// El padron de proveedores. Llama a sp_n_ConsultaProveedores, el mismo que usa
/// Mac31: su WHERE entra por fn_RegresaTablaStatus y su domicilio sale de cuatro
/// INNER JOIN del catalogo territorial. No se reescribe.
/// </summary>
public class ProveedoresConsultaRepository : IProveedoresConsultaRepository
{
    private const string SpConsultaProveedores = "dbo.sp_n_ConsultaProveedores";

    /// <summary>@Formato = 1 son las 12 columnas con encabezados en espanol para Excel. No se expone.</summary>
    private const int FormatoTodosLosCampos = 0;

    private readonly IConfiguration _configuration;

    public ProveedoresConsultaRepository(IConfiguration configuration)
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

    public async Task<List<ProveedorConsultaDto>> ConsultarAsync(ProveedoresConsultaFiltro filtro, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpConsultaProveedores, conn) { CommandType = CommandType.StoredProcedure };

        cmd.Parameters.AddWithValue("@IDProveedor", filtro.IDProveedor);
        cmd.Parameters.AddWithValue("@IDCondicionesCredito", filtro.IDCondicionesCredito);

        /*
          @IDDescuento SE MANDA Y EL PROCEDIMIENTO LO IGNORA.
          sp_n_ConsultaProveedores lo declara en su firma y NUNCA lo mete en el
          WHERE (a diferencia de @IDCondicionesCredito, que si tiene su IF). Se
          manda igual, porque el parametro existe y omitirlo no cambia nada, pero
          queda dicho aqui para que nadie reporte como bug que `idDescuento=3` no
          filtra: no filtra en Mac31 tampoco. No se arregla el SP porque es
          `sp_n_` y se lo lleva el front viejo.
        */
        cmd.Parameters.AddWithValue("@IDDescuento", filtro.IDDescuento);

        cmd.Parameters.AddWithValue("@IDStatus", filtro.IDStatus);
        cmd.Parameters.AddWithValue("@Numero", filtro.Numero ?? string.Empty);
        cmd.Parameters.AddWithValue("@Nombre", filtro.Nombre ?? string.Empty);
        cmd.Parameters.AddWithValue("@ApellidoPaterno", filtro.ApellidoPaterno ?? string.Empty);
        cmd.Parameters.AddWithValue("@ApellidoMaterno", filtro.ApellidoMaterno ?? string.Empty);
        cmd.Parameters.AddWithValue("@RFC", filtro.Rfc ?? string.Empty);
        cmd.Parameters.AddWithValue("@Formato", FormatoTodosLosCampos);
        cmd.Parameters.AddWithValue("@Identico", filtro.Exacto ? 1 : 0);

        // El SP llena #Proveedores con un INSERT ... EXEC (que no entrega rowset)
        // y luego hace el SELECT final: el primer resultado del lector es ese
        // SELECT.
        var dt = new DataTable();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            dt.Load(reader);
        }

        return Funciones.DataTableToList<ProveedorConsultaDto>(dt);
    }
}
