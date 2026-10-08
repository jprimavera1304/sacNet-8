using System.Data;
using System.Globalization;
using ISL_Service.Application.DTOs.InventarioTransito;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// La consulta del inventario en transito. Llama a sp_n_ConsultaVentasTransito,
/// el mismo procedimiento que la pantalla InventarioTransito de Mac31
/// (Legacy/Mac31/Mac31/Forms/Productos/InventarioTransito.cs, los metodos
/// ConsultaInventarioTransito y ConsultaInventarioTransitoDetalle, que lo llaman
/// dos veces: agrupado para la rejilla de arriba, detalle para la de abajo).
/// </summary>
public class InventarioTransitoConsultaRepository : IInventarioTransitoConsultaRepository
{
    private const string SpConsultaVentasTransito = "dbo.sp_n_ConsultaVentasTransito";

    /// <summary>
    /// Constantes.FormatoFecha, que vale 'MM-dd-yyyy' en las empresas vivas. El
    /// parametro es varchar(20) y el SP lo concatena dentro de su BETWEEN, asi que
    /// el formato lo decide el llamador. Se usa el mismo que traen los defaults
    /// del propio procedimiento.
    /// </summary>
    private const string FormatoFechaLegacy = "MM-dd-yyyy HH:mm:ss";

    private readonly IConfiguration _configuration;

    public InventarioTransitoConsultaRepository(IConfiguration configuration)
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

    public async Task<List<InventarioTransitoDto>> ConsultarAsync(InventarioTransitoFiltro filtro, CancellationToken ct)
    {
        /*
          ======================================================================
          ESTE "CONSULTA" PUEDE ESCRIBIR. HAY QUE IMPEDIRSELO DESDE AQUI.
          ======================================================================

          sp_n_ConsultaVentasTransito no es una consulta pura. Tiene un bloque,
          al final, que se dispara con la condicion

              @StatusInventario = 1  AND  @FechaEmisionInicial = ''

          y que abre un CURSOR sobre lo que acaba de encontrar y llama
          sp_n_ProcesarInventarioTransito por cada renglon, con @IDUsuario = 1057
          ('SISTEMA'). O sea: ACEPTA todo el inventario en transito y mueve
          existencias. En ese camino ni devuelve el listado — devuelve la tabla de
          resultados del procesado.

          Un GET que mueve el inventario es una trampa, y la trampa se arma con un
          parametro VACIO, que es exactamente lo que manda un filtro sin llenar.
          Asi que la fecha inicial es OBLIGATORIA y nunca viaja vacia: con fecha
          inicial presente el SP se queda en la rama de consulta y el cursor no se
          ejecuta.

          No se arregla el procedimiento porque es `sp_n_` y es el que usa Mac31
          (su pantalla manda siempre las dos fechas, por eso alla no se nota). Se
          cierra la puerta del lado que si es nuestro, y se valida tambien en el
          controlador para que el 400 llegue antes de abrir conexion.
        */
        if (filtro.FechaEmisionInicial == default)
        {
            throw new ArgumentException(
                "La fecha de emision inicial es obligatoria en la consulta de inventario en transito.");
        }

        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpConsultaVentasTransito, conn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@StatusInventario", filtro.StatusInventario);

        // 0 = el SP toma Constantes.IDClienteCS y, si ahi tampoco hay centro,
        // trae los de TODOS los centros de servicio (IN (SELECT IDCliente FROM
        // CentrosServicio)). Es la conducta de Mac31 en la matriz.
        cmd.Parameters.AddWithValue("@IDCliente", filtro.IDCliente);

        cmd.Parameters.AddWithValue("@FechaEmisionInicial", Fecha(filtro.FechaEmisionInicial));
        cmd.Parameters.AddWithValue("@FechaEmisionFinal", Fecha(filtro.FechaEmisionFinal));
        cmd.Parameters.AddWithValue("@Agrupado", filtro.Agrupado ? 1 : 0);

        /*
          OJO CON EL RANGO DE FECHAS CUANDO SE PIDE "EN TRANSITO".
          Con @StatusInventario = 1 el SP NO aplica el BETWEEN: solo filtra
          VD.FechaAcepto IS NULL, o sea TODO lo que sigue sin aceptarse, sin
          importar cuando se emitio. El BETWEEN solo entra con 0 (todos) y con
          2 (aceptado). Es la conducta de Mac31 y se respeta; el rango se exige de
          todos modos porque es lo que mantiene apagado el cursor de escritura.
        */

        var dt = new DataTable();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            dt.Load(reader);
        }

        return Funciones.DataTableToList<InventarioTransitoDto>(dt);
    }

    private static string Fecha(DateTime valor)
        => valor.ToString(FormatoFechaLegacy, CultureInfo.InvariantCulture);
}
