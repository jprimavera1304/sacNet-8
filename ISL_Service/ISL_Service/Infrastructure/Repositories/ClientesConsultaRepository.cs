using System.Data;
using ISL_Service.Application.DTOs.ClientesConsulta;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// El padron de clientes, con el filtro por agente puesto desde el servidor.
///
/// Llama a sp_n_ConsultaClientes, el MISMO que usa Mac31. No se reescribe el
/// SELECT: el SP decide a que base apunta segun @IDEmpresaCS (Zaragoza, Luro o
/// la local) y arma el domicilio con cuatro INNER JOIN de catalogo territorial.
/// Reescribirlo seria reinventar esas reglas y descubrirlas rotas en produccion.
/// </summary>
public class ClientesConsultaRepository : IClientesConsultaRepository
{
    private const string SpConsultaClientes = "dbo.sp_n_ConsultaClientes";

    /// <summary>@Formato = 1 son las 15 columnas con encabezados en espanol para pegar en Excel. No se expone.</summary>
    private const int FormatoTodosLosCampos = 0;

    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ClientesConsultaRepository> _logger;

    /// <summary>
    /// El lector de dbo.Constantes que ya existe (el que alimenta el bloque
    /// `empresa` de /api/me). Se reusa en vez de escribir un segundo lector: ahi ya
    /// esta IDEmpresaCS y ahi ya esta la decision de NO llamar a
    /// sp_n_ConsultaConstantes, que hace el cierre del dia.
    /// </summary>
    private readonly ICapacidadesEmpresaRepository _constantes;

    public ClientesConsultaRepository(
        IConfiguration configuration,
        IMemoryCache cache,
        ILogger<ClientesConsultaRepository> logger,
        ICapacidadesEmpresaRepository constantes)
    {
        _configuration = configuration;
        _cache = cache;
        _logger = logger;
        _constantes = constantes;
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

    public async Task<List<ClienteConsultaDto>> ConsultarAsync(
        ClientesConsultaFiltro filtro, int idUsuario, CancellationToken ct)
    {
        /*
          EL FILTRO POR AGENTE SE RESUELVE AQUI, Y NO SE PUEDE PEDIR DESDE FUERA.
          ======================================================================

          @IDAgente NO sale del query string. Sale de FiltroDeAgente.ResolverAsync
          con el id de usuario del TOKEN. Si viniera del cliente, un agente
          pediria `idAgente=0` y veria el padron completo: el filtro seria una
          sugerencia, no un filtro.

          Si no se puede resolver, ResolverAsync LANZA (409) y esta consulta no
          contesta nada. No se degrada a 0, porque 0 significa "ve todos" y ese
          fue exactamente el error que se arreglo: un usuario amarrado a un
          agente viendo 1,042 clientes de 15 agentes.

          QUE SI SE ACEPTA DE FUERA: `IDAgenteAcotar`, y solo para ACOTAR.
          Cuando el filtro resuelto es 0 (el usuario ve a todos) se obedece, para
          que la pantalla pueda decir "ensename solo los del agente 7". Cuando el
          filtro resuelto es > 0 se IGNORA y manda el resuelto. O sea: puede
          estrechar lo que ya se puede ver, nunca ampliarlo.
        */
        var agenteDelUsuario = await FiltroDeAgente.ResolverAsync(GetConnection, _cache, _logger, idUsuario, ct);

        var idAgente = agenteDelUsuario > 0
            ? agenteDelUsuario
            : Math.Max(filtro.IDAgenteAcotar, 0);

        /*
          @IDEmpresaCS NO ES UN FILTRO OPCIONAL, POR MAS QUE LO PAREZCA.
          ==============================================================

          sp_n_ConsultaClientes le pega SIEMPRE ' AND C.IDEmpresaCS = @IDEmpresaCS'
          al SQL que arma, sin importar por donde se busque. Medido en MacZ:
          SELECT DISTINCT IDEmpresaCS FROM Clientes devuelve 1 para los 887
          clientes, asi que el 0 que estaba cableado como valor por omision del
          endpoint hacia que GET api/clientes contestara {"total":0} sobre un
          padron de 887, mientras ?idEmpresaCs=1 contestaba los 887.

          Nulo = no lo mandaron -> se resuelve desde dbo.Constantes.IDEmpresaCS,
          igual que Mac31 (Variables.IDEmpresaCS, poblado al arrancar). Se lee con
          un SELECT reusando ICapacidadesEmpresaRepository y NUNCA con
          sp_n_ConsultaConstantes: ese procedimiento HACE EL CIERRE DEL DIA (su IF
          de @ValidaActividad solo envuelve el bloque de actividad; el cierre
          cuelga de su propia condicion, afuera), y una consulta de clientes no
          tiene por que mover la fecha de operacion del negocio.

          Si viene explicito se obedece, tambien el 0: quien pida ?idEmpresaCs=0
          esta pidiendo el comportamiento viejo a proposito.
        */
        var idEmpresaCs = filtro.IDEmpresaCS
            ?? await EmpresaCsPorOmision.ResolverAsync(_constantes, _cache, ct);

        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpConsultaClientes, conn) { CommandType = CommandType.StoredProcedure };

        cmd.Parameters.AddWithValue("@IDCliente", filtro.IDCliente);

        /*
          CONDICIONES DE CREDITO Y DESCUENTO VAN EN 0 A PROPOSITO.
          Son filtros que la pantalla de Mac31 no ofrece en esta consulta (viven
          en la ficha del cliente, no en el buscador). 0 = no filtrar.
        */
        cmd.Parameters.AddWithValue("@IDCondicionesCredito", 0);
        cmd.Parameters.AddWithValue("@IDCondicionesCreditoAceites", 0);
        cmd.Parameters.AddWithValue("@IDCondicionesCreditoCascos", 0);
        cmd.Parameters.AddWithValue("@IDDescuento", 0);

        /*
          El SP aplica este @IDAgente con AND sobre CUALQUIER busqueda, tambien
          la que va por IDCliente o por Numero (arma ' AND C.IDAgente = N'), asi
          que no hay forma de saltarselo pidiendo un cliente por su id.
        */
        cmd.Parameters.AddWithValue("@IDAgente", idAgente);

        cmd.Parameters.AddWithValue("@IDStatus", filtro.IDStatus);
        cmd.Parameters.AddWithValue("@Numero", filtro.Numero ?? string.Empty);
        cmd.Parameters.AddWithValue("@Nombre", filtro.Nombre ?? string.Empty);
        cmd.Parameters.AddWithValue("@ApellidoPaterno", filtro.ApellidoPaterno ?? string.Empty);
        cmd.Parameters.AddWithValue("@ApellidoMaterno", filtro.ApellidoMaterno ?? string.Empty);
        cmd.Parameters.AddWithValue("@RFC", filtro.Rfc ?? string.Empty);
        cmd.Parameters.AddWithValue("@IDTipoCliente", filtro.IDTipoCliente);
        cmd.Parameters.AddWithValue("@ConHuella", filtro.ConHuella);
        cmd.Parameters.AddWithValue("@IDEmpresaCS", idEmpresaCs);
        cmd.Parameters.AddWithValue("@EsMostrador", filtro.EsMostrador);
        cmd.Parameters.AddWithValue("@Referencia", filtro.Referencia ?? string.Empty);
        cmd.Parameters.AddWithValue("@Formato", FormatoTodosLosCampos);
        cmd.Parameters.AddWithValue("@Identico", filtro.Exacto ? 1 : 0);

        /*
          EL PRIMER RESULTADO CON COLUMNAS ES EL BUENO.
          El SP llena #Clientes con un EXEC(@Sql) y luego hace el SELECT final.
          Un DataTable.Load sobre el lector se queda con el primer resultado, que
          es ese SELECT: el INSERT ... EXEC no entrega rowset.
        */
        var dt = new DataTable();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            dt.Load(reader);
        }

        return Funciones.DataTableToList<ClienteConsultaDto>(dt);
    }
}
