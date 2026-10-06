using System.Data;
using ISL_Service.Application.DTOs.Huellas;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// `sp_n_ConsultaHuellas`.
///
/// EL SP DEVUELVE UN NUMERO VARIABLE DE RESULT SETS, Y ESO HAY QUE ENTENDERLO
/// ANTES DE TOCARLO
/// -------------------------------------------------------------------------
/// Son seis `IF` independientes: uno por origen (usuario / empleado / cliente)
/// para "este id" y otro para "todos" (el valor magico -1). Cada IF que se
/// cumple emite SU PROPIO SELECT. Consecuencias reales:
///
/// * Con los tres parametros en 0 NO emite NINGUN result set. Legacy hacia
///   `ds.Tables[0]` sin mirar, reventaba con IndexOutOfRange y devolvia 500
///   "[No se pudo procesar sp_n_ConsultaHuellas]". Aqui el controlador exige el
///   origen, asi que ese caso no llega.
/// * Con dos origenes a la vez emite DOS result sets y legacy leia solo el
///   primero: las huellas del segundo se perdian sin aviso. Aqui se pide UN
///   origen por llamada, que es lo que de verdad se consulta, y asi la respuesta
///   no depende de en que orden estan los IF del procedimiento.
///
/// LAS COLUMNAS SE LEEN POR POSICION, no por nombre. Los tres bloques devuelven
/// la MISMA forma con nombres distintos (`IDUsuarioHuella`/`IDUsuario` frente a
/// `IDEmpleadoHuella`/`IDEmpleado` frente a `IDClienteHuella`/`IDCliente`), en
/// el mismo orden: id de la huella, id de la persona, mano, dedo, mano, dedo,
/// huella, nombre. Leer por posicion evita tres mapeos identicos; es seguro
/// porque el orden es el del SELECT y el SELECT es explicito (no hay `*`).
/// </summary>
public class HuellasRepository : IHuellasRepository
{
    private const string SpConsultar = "dbo.sp_n_ConsultaHuellas";

    /// <summary>El valor con el que el SP entiende "todas las de este origen".</summary>
    public const int TodasLasHuellas = -1;

    private readonly IConfiguration _configuration;

    public HuellasRepository(IConfiguration configuration)
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

    public async Task<List<HuellaDto>> ConsultarAsync(HuellaOrigen origen, int id, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpConsultar, conn) { CommandType = CommandType.StoredProcedure };

        // Los otros dos van en 0 para que su IF no se cumpla y el SP emita UN
        // solo result set. El default de @IDEmpleado en el SP es -1 (= todas),
        // asi que omitirlo traeria las huellas de toda la nomina de regalo.
        cmd.Parameters.AddWithValue("@IDUsuario", origen == HuellaOrigen.Usuario ? id : 0);
        cmd.Parameters.AddWithValue("@IDEmpleado", origen == HuellaOrigen.Empleado ? id : 0);
        cmd.Parameters.AddWithValue("@IDCliente", origen == HuellaOrigen.Cliente ? id : 0);

        var etiqueta = origen switch
        {
            HuellaOrigen.Usuario => "usuario",
            HuellaOrigen.Empleado => "empleado",
            HuellaOrigen.Cliente => "cliente",
            _ => "desconocido"
        };

        var lista = new List<HuellaDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        // Si el IF del origen no se cumplio no hay result set y el bucle no
        // entra: lista vacia, no excepcion.
        while (await reader.ReadAsync(ct))
        {
            lista.Add(new HuellaDto
            {
                Origen = etiqueta,
                IdHuella = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0)),
                IdPersona = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1)),
                IDMano = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2)),
                IDDedo = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3)),
                Mano = reader.IsDBNull(4) ? string.Empty : (Convert.ToString(reader.GetValue(4)) ?? string.Empty).Trim(),
                Dedo = reader.IsDBNull(5) ? string.Empty : (Convert.ToString(reader.GetValue(5)) ?? string.Empty).Trim(),
                Huella = reader.IsDBNull(6) ? null : (byte[])reader.GetValue(6),
                Nombre = reader.IsDBNull(7) ? string.Empty : (Convert.ToString(reader.GetValue(7)) ?? string.Empty).Trim()
            });
        }

        return lista;
    }
}
