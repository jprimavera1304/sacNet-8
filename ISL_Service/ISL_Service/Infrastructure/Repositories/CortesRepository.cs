using System.Data;
using ISL_Service.Application.DTOs.Cortes;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// Cortes de caja: las cuatro lecturas que NO pasan por
/// `sp_n_ConsultaConstantes`.
///
/// POR QUE SOLO CUATRO DE LOS SIETE DE LEGACY
/// ------------------------------------------
/// `/api/cortes/consultar`, `/api/cortes/consultar/tipogastos` y
/// `/api/cortescs/consultar` piden primero `sp_n_ConsultaConstantes` para leer
/// una sola columna (`formatoFechaDB`) y poder voltear las fechas del filtro. Ese
/// procedimiento no consulta: cuando `Constantes.FechaOperacion` esta atrasada
/// hace el cierre del dia —inserta en `FechaOperacionLog`, BORRA y rehace
/// `AlmacenProductoHist` (el congelado de existencias del dia anterior) y adelanta
/// la fecha de operacion del negocio— y en toda llamada registra actividad del
/// usuario. Son escrituras, y migrarlos como lecturas habria significado que
/// abrir una pantalla de consulta un lunes cerrara el viernes. Quedan para la
/// entrega que los trate como lo que son.
///
/// `sp_n_ConsultarCortesCSCobros` MERECE UN AVISO: recorre con un CURSOR todos
/// los renglones de `CentrosServicio` con status 1 y, por cada uno, corre la
/// misma consulta contra la base de ESE centro (`BaseCentro`). O sea: una sola
/// llamada abre tantas consultas cross-database como centros haya. Si una base de
/// centro no esta accesible, el SP falla completo. De ahi el timeout explicito.
/// </summary>
public class CortesRepository : ICortesRepository
{
    private const string SpDetalle = "dbo.sp_n_ConsultarCortesDetalle";
    private const string SpSiguienteFecha = "dbo.sp_n_ConsultarCorteFechaUltimo";
    private const string SpCobrosCs = "dbo.sp_n_ConsultarCortesCSCobros";
    private const string SpVentasCanceladasCs = "dbo.sp_n_ConsultarCortesVentasCanceladasCS";

    /// <summary>
    /// Dos minutos, solo para el de cobros: su cursor sobre CentrosServicio
    /// multiplica la consulta por el numero de centros y cada una va a otra base.
    /// </summary>
    private const int CommandTimeoutCentrosSeconds = 120;

    private readonly IConfiguration _configuration;

    public CortesRepository(IConfiguration configuration)
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

    private async Task<DataTable> EjecutarAsync(string sp, Action<SqlCommand> parametros, CancellationToken ct, int? timeout = null)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(sp, conn) { CommandType = CommandType.StoredProcedure };
        if (timeout.HasValue) cmd.CommandTimeout = timeout.Value;
        parametros(cmd);

        var dt = new DataTable();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        dt.Load(reader);
        return dt;
    }

    public async Task<List<CorteDetalleDto>> ConsultarDetalleAsync(int idCorte, CancellationToken ct)
    {
        var dt = await EjecutarAsync(SpDetalle, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCorte", idCorte);
        }, ct);

        /*
          `NoAplicadoDesglose` VIENE DOS VECES EN EL SELECT DEL SP.
          Son dos lineas distintas del mismo procedimiento que proyectan la misma
          columna. DataTable renombra la segunda a `NoAplicadoDesglose1`, que no
          empareja con nada del DTO y por tanto se descarta sola: la primera es la
          que se publica. Queda escrito para que nadie "arregle" el DTO
          agregandole un campo que no significa nada nuevo.
        */
        return Funciones.DataTableToList<CorteDetalleDto>(dt);
    }

    public async Task<CorteSiguienteFechaDto?> ConsultarSiguienteFechaAsync(CancellationToken ct)
    {
        var dt = await EjecutarAsync(SpSiguienteFecha, cmd =>
        {
            // @IDCorte SE DECLARA Y EL SP LO PISA en su primera linea con
            // MAX(IDCorte). Se manda 0 para completar la firma; el endpoint no lo
            // pide, porque pedirlo haria creer que el resultado depende de el.
            cmd.Parameters.AddWithValue("@IDCorte", 0);
        }, ct);

        var lista = Funciones.DataTableToList<CorteSiguienteFechaDto>(dt);
        return lista.Count == 0 ? null : lista[0];
    }

    public async Task<List<CorteCsCobroDto>> ConsultarCobrosCsAsync(
        int idCorte, int idCobroDineroUsado, int idTipoRetiro, CancellationToken ct)
    {
        var dt = await EjecutarAsync(SpCobrosCs, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCorte", idCorte);
            // CUIDADO: aqui el 0 NO es "todos". El SP tiene un ELSE que fuerza
            // `ISNULL(cor.IDCobroDineroUsado,0) = 0`, o sea "los que todavia no
            // estan asociados a un cobro". Esta anotado en el controlador porque
            // es lo contrario de lo que significa el 0 en todos los demas
            // filtros del sistema.
            cmd.Parameters.AddWithValue("@IDCobroDineroUsado", idCobroDineroUsado);
            cmd.Parameters.AddWithValue("@IDTipoRetiro", idTipoRetiro);
        }, ct, CommandTimeoutCentrosSeconds);

        return Funciones.DataTableToList<CorteCsCobroDto>(dt);
    }

    public async Task<CorteVentasCanceladasCsDto> ConsultarVentasCanceladasCsAsync(
        int idCorte, int idTipoRetiro, CancellationToken ct)
    {
        var dt = await EjecutarAsync(SpVentasCanceladasCs, cmd =>
        {
            // Igual que arriba: 0 NO es "todos", es `venCan.IDCorte IS NULL` —
            // los pagos cancelados que aun no se han llevado a ningun corte.
            cmd.Parameters.AddWithValue("@IDCorte", idCorte);
            cmd.Parameters.AddWithValue("@IDTipoRetiro", idTipoRetiro);
        }, ct);

        var renglones = Funciones.DataTableToList<CorteVentaCanceladaCsDto>(dt);

        // El total viene repetido en cada renglon (el SP lo calcula una vez y lo
        // pega a todas las filas). Se saca del primero y se devuelve UNA vez.
        // Legacy lo tiraba entero: su DTO no declaraba la columna.
        decimal suma = 0m;
        if (dt.Rows.Count > 0 && dt.Columns.Contains("SumaAbonos") && dt.Rows[0]["SumaAbonos"] != DBNull.Value)
            suma = Convert.ToDecimal(dt.Rows[0]["SumaAbonos"]);

        return new CorteVentasCanceladasCsDto { Renglones = renglones, SumaAbonos = suma };
    }
}
