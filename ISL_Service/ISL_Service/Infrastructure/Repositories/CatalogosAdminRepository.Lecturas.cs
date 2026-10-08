using ISL_Service.Application.DTOs.CatalogosAdmin;
using ISL_Service.Application.DTOs.Empleados;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// Las LECTURAS de los catalogos que este repositorio ya sabia escribir.
///
/// POR QUE VIVEN AQUI Y NO EN CatalogosGeneralesRepository
/// ------------------------------------------------------
/// CatalogosGenerales es el de los 28 catalogos de SOLO LECTURA, y su comentario
/// de cabecera lo dice: "ninguno escribe". Estos siete si se escriben, desde
/// este mismo repositorio, y la consulta de un catalogo editable tiene que
/// devolver EL MISMO renglon que el UPDATE va a pisar — con su IDStatus, no solo
/// con el texto bonito para un desplegable. Tenerlas juntas es lo que hace que
/// al agregar un campo a la escritura se vea, en el archivo de al lado, que hay
/// que devolverlo tambien en la lectura.
///
/// SE DEVUELVE EL RENGLON CRUDO (Dictionary), como ConsultarDiasFestivosAsync
/// -------------------------------------------------------------------------
/// Es la decision que ya estaba tomada en este repositorio para dias festivos y
/// se respeta. Estos sp_n_ terminan en EXEC (@Sql) y su lista de columnas cambia
/// con @Formato; un DTO cerrado por catalogo obligaria a mantener once clases
/// que solo existen para copiar nombres, y el dia que el SP agregue una columna
/// el backend la tiraria en silencio. Con el diccionario, lo que el
/// procedimiento devuelve es lo que llega al front, y el que decide que pintar
/// es la pantalla.
///
/// LOS NOMBRES DE LAS COLUMNAS SON LOS DE LEGACY, tal cual: IDAlmacen, Almacen,
/// IDStatus, Status. No se renombran a camelCase aqui — el front ya convive con
/// los otros catalogos que llegan asi, y renombrar en el medio del camino es
/// como se acaba con dos vocabularios para la misma tabla.
/// </summary>
public partial class CatalogosAdminRepository
{
    /// <summary>
    /// El 100% de este archivo: ejecuta un sp_n_Consulta* y devuelve sus
    /// renglones. Mismo cuerpo que ConsultarDiasFestivosAsync, factorizado.
    /// </summary>
    private async Task<List<Dictionary<string, object?>>> ConsultarAsync(
        string sp, Action<SqlCommand> parametros, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(sp, conn);
        parametros(cmd);

        var dt = await FillAsync(cmd, ct);
        return Funciones.DataTableToRows(dt);
    }

    /*
      @Formato = 0 SIEMPRE, y conviene saber por que.

      Varios de estos SP tienen un @Formato donde 0 es "todos los campos" y 1 es
      "el recorte para el Excel de Mac31" (en agentes, por ejemplo, el 1 deja
      fuera IDAgente e IDStatus). Una pantalla que edita NECESITA el id y el
      estatus, asi que el formato de Excel no sirve aqui, y exponerlo en el
      contrato solo daria la posibilidad de pedir una lista con la que luego no
      se puede modificar nada. El Excel del front nuevo se arma con las columnas
      que ya tiene en pantalla.
    */
    private const int FormatoTodosLosCampos = 0;

    /// <summary>0/1 como lo espera legacy: un bool de C# no entra en un int de T-SQL.</summary>
    private static int Bandera(bool valor) => valor ? 1 : 0;

    // -----------------------------------------------------------------------
    // Almacenes
    // -----------------------------------------------------------------------

    public Task<List<Dictionary<string, object?>>> ConsultarAlmacenesAsync(AlmacenesFiltro f, CancellationToken ct)
        => ConsultarAsync("dbo.sp_n_ConsultaAlmacenes", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDAlmacen", f.IdAlmacen);
            cmd.Parameters.AddWithValue("@IDStatus", f.IdStatus);
            cmd.Parameters.AddWithValue("@Almacen", Texto(f.Almacen));
            cmd.Parameters.AddWithValue("@Identico", Bandera(f.Identico));
        }, ct);

    // -----------------------------------------------------------------------
    // Agentes
    // -----------------------------------------------------------------------

    public Task<List<Dictionary<string, object?>>> ConsultarAgentesAsync(AgentesFiltro f, CancellationToken ct)
        => ConsultarAsync("dbo.sp_n_ConsultaAgentes", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDAgente", f.IdAgente);
            cmd.Parameters.AddWithValue("@IDStatus", f.IdStatus);
            cmd.Parameters.AddWithValue("@NumeroAgente", f.NumeroAgente);
            cmd.Parameters.AddWithValue("@NombreAgente", Texto(f.NombreAgente));
            cmd.Parameters.AddWithValue("@Formato", FormatoTodosLosCampos);
            cmd.Parameters.AddWithValue("@Identico", Bandera(f.Identico));
        }, ct);

    // -----------------------------------------------------------------------
    // Tipos de gasto
    // -----------------------------------------------------------------------

    public Task<List<Dictionary<string, object?>>> ConsultarTiposGastoAsync(TiposGastoFiltro f, CancellationToken ct)
        => ConsultarAsync("dbo.sp_n_ConsultaTiposGastos", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDTipoGasto", f.IdTipoGasto);
            cmd.Parameters.AddWithValue("@IDStatus", f.IdStatus);
            cmd.Parameters.AddWithValue("@TipoGasto", Texto(f.TipoGasto));
            cmd.Parameters.AddWithValue("@Formato", FormatoTodosLosCampos);
            cmd.Parameters.AddWithValue("@Identico", Bandera(f.Identico));
        }, ct);

    // -----------------------------------------------------------------------
    // Autos
    // -----------------------------------------------------------------------

    public Task<List<Dictionary<string, object?>>> ConsultarAutosAsync(AutosFiltro f, CancellationToken ct)
        => ConsultarAsync("dbo.sp_n_ConsultaAutos", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDAuto", f.IdAuto);
            cmd.Parameters.AddWithValue("@IDStatus", f.IdStatus);
            cmd.Parameters.AddWithValue("@Nombre", Texto(f.Nombre));
        }, ct);

    // -----------------------------------------------------------------------
    // Grupos de categoria y categorias de Mac31
    //
    // Los dos reciben LISTAS de ids en un varchar separado por comas, no un id.
    // Es la firma de legacy y es la que hace falta: la pantalla de
    // subcategorias filtra por el grupo elegido, y el concentrado pide varios
    // grupos a la vez.
    // -----------------------------------------------------------------------

    public Task<List<Dictionary<string, object?>>> ConsultarGruposCategoriaAsync(GruposCategoriaFiltro f, CancellationToken ct)
        => ConsultarAsync("dbo.sp_n_ConsultaGrupoCategorias", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDsGrupoCategoria", Texto(f.IdsGrupoCategoria));
            cmd.Parameters.AddWithValue("@IDStatus", f.IdStatus);
            cmd.Parameters.AddWithValue("@GrupoCategoria", Texto(f.GrupoCategoria));
            cmd.Parameters.AddWithValue("@Identico", Bandera(f.Identico));
            cmd.Parameters.AddWithValue("@Formato", FormatoTodosLosCampos);
        }, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarCategoriasMacAsync(CategoriasMacFiltro f, CancellationToken ct)
        => ConsultarAsync("dbo.sp_n_ConsultaCategorias", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDsCategoria", Texto(f.IdsCategoria));
            cmd.Parameters.AddWithValue("@IDsGrupoCategoria", Texto(f.IdsGrupoCategoria));
            cmd.Parameters.AddWithValue("@IDStatus", f.IdStatus);
            cmd.Parameters.AddWithValue("@Categoria", Texto(f.Categoria));
            cmd.Parameters.AddWithValue("@Identico", Bandera(f.Identico));
            cmd.Parameters.AddWithValue("@Formato", FormatoTodosLosCampos);
        }, ct);

    // -----------------------------------------------------------------------
    // Tipos de usado (cascos)
    //
    // OJO: sp_n_ConsultaTiposUsados NO lista el catalogo entero. Hace un INNER
    // JOIN con EmpresaUsado y se queda solo con los tipos de LA empresa, que
    // elige el propio SP: la 9 si Constantes.Funcionalidad = 'TAU' y el
    // MIN(IDEmpresa) activo en cualquier otro caso. Es decir que el filtro de
    // empresa no esta en el contrato ni se puede pasar; esta clavado en la base.
    // Se llama igual que Mac31 para que los dos vean los mismos cascos.
    // -----------------------------------------------------------------------

    public Task<List<Dictionary<string, object?>>> ConsultarTiposUsadoAsync(TiposUsadoFiltro f, CancellationToken ct)
        => ConsultarAsync("dbo.sp_n_ConsultaTiposUsados", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDTipoUsado", f.IdTipoUsado);
            cmd.Parameters.AddWithValue("@IDStatus", f.IdStatus);
            cmd.Parameters.AddWithValue("@TipoUsado", Texto(f.TipoUsado));
            cmd.Parameters.AddWithValue("@Formato", FormatoTodosLosCampos);
            cmd.Parameters.AddWithValue("@Identico", Bandera(f.Identico));
        }, ct);

    // -----------------------------------------------------------------------
    // Las constantes de operacion
    // -----------------------------------------------------------------------

    /*
      SE LEEN CON UN SELECT, Y NO CON sp_n_ConsultaConstantes. ESTO NO ES UN
      ATAJO: ES LO UNICO CORRECTO.

      sp_n_ConsultaConstantes no es una consulta. Cuando la fecha de operacion
      esta atrasada hace el CIERRE DEL DIA del negocio: adelanta la fecha,
      rehace AlmacenProductoHist y deja apuntada la operacion. Todo el backend
      nuevo lo evita a proposito y lo dice en diez repositorios
      (CobranzasRepository, NominaBatchRepository, ConexionDeAviacion...). Una
      pantalla que ENSENA la fecha de operacion, recargada con F5 veinte veces
      al dia, seria la forma mas facil de cerrar el dia sin querer.

      Por eso aqui se lee la tabla. El cierre del dia lo sigue haciendo quien lo
      hacia: Mac31 al entrar, y el boton de "cambiar la fecha de ventas", que
      para eso tiene su propio sp_n_ActualizarFechaOperacion.

      TOP 1 porque Constantes tiene un solo renglon por base, que es como la lee
      Globales.cs de Mac31 y como la leen los otros repositorios de aqui.

      SE DEVUELVEN SOLO LOS CAMPOS DE LA PANTALLA. Constantes tiene 210
      columnas, entre ellas contrasenas en claro (Contrasena Pagos, Contrasena
      Facturar, FacturasCorreoContrasena, EncryptionKey, ftpPwd) y cadenas de
      conexion a las bases de los centros de servicio. Un SELECT * aqui seria
      publicar todo eso en un GET con permiso de lectura.
    */
    private const string SqlConstantesOperacion =
        "SELECT TOP 1 " +
        "  FechaOperacion, FechaOperacionPagos, " +
        "  FechaCambioFechaOperacion, FechaCambioFechaOperacionPagos, " +
        "  IDUsuarioFechaOperacion, IDUsuarioFechaOperacionPagos, " +
        "  EquipoFechaOperacion, EquipoFechaOperacionPagos, " +
        "  NumDiasPedidosRechazados, DiasFolioConcentrado, DiasFolioCobranza, " +
        "  NoDiasAtrasados, NoDiasAdelantados, " +
        "  MaximaDiferenciaUsados, horasLaborales AS HorasLaborales, IVA, Funcionalidad " +
        "FROM dbo.Constantes";

    public async Task<Dictionary<string, object?>?> ConsultarConstantesOperacionAsync(CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SqlConstantesOperacion, conn);
        var dt = await FillAsync(cmd, ct);
        var filas = Funciones.DataTableToRows(dt);
        return filas.Count > 0 ? filas[0] : null;
    }

    // -----------------------------------------------------------------------
    // Empresas de Mac31
    //
    // No se confunda con GET api/empresas, que es otra cosa: aquella devuelve
    // { empresaId, nombre } para el selector de inquilino. Esta devuelve la FICHA
    // FISCAL -RFC, regimen, banco, CLABE, domicilio resuelto y el Id de Facturama-,
    // que es lo que la pantalla de Empresas edita.
    // -----------------------------------------------------------------------

    public Task<List<Dictionary<string, object?>>> ConsultarEmpresasMacAsync(EmpresasMacFiltro f, CancellationToken ct)
        => ConsultarAsync("dbo.sp_n_ConsultaEmpresas", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDEmpresa", f.IdEmpresa);
            cmd.Parameters.AddWithValue("@IDStatus", f.IdStatus);
            cmd.Parameters.AddWithValue("@RazonSocial", Texto(f.RazonSocial));
            cmd.Parameters.AddWithValue("@Formato", FormatoTodosLosCampos);
            cmd.Parameters.AddWithValue("@Identico", Bandera(f.Identico));
        }, ct);

    // -----------------------------------------------------------------------
    // La relacion categoria-marca, completa
    // -----------------------------------------------------------------------

    /*
      TODO EN UNA TRANSACCION, Y POR ESO NO USA EjecutarAsync.

      `EjecutarAsync` abre su propia conexion por llamada, que es lo correcto para
      una escritura suelta. Aqui son hasta 133 EXEC del mismo procedimiento y lo
      que importa es que valgan como uno: o queda toda la relacion de la
      subcategoria como se dejo en pantalla, o no queda nada. A medias es peor que
      no guardar, porque nadie sabe cuales entraron — es justo lo que le pasa a
      Mac31, que manda una peticion HTTP por renglon.

      Se reusa el mismo `sp_n_InsertarGrupoCategoriasMarcasRelacion` de siempre
      (inserta el par si falta y lo borra si Marcado = 0): no hace falta un sp_w_
      para esto, lo unico que cambia es que las llamadas comparten transaccion.
    */
    public async Task<ResultadoLegacy> RelacionarCategoriaMarcasAsync(
        int idCategoria, IReadOnlyList<MarcaDeLaCategoria> marcas, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(ct);

        try
        {
            foreach (var m in marcas)
            {
                await using var cmd = new SqlCommand("dbo.sp_n_InsertarGrupoCategoriasMarcasRelacion", conn, tx)
                {
                    CommandType = System.Data.CommandType.StoredProcedure
                };
                cmd.Parameters.AddWithValue("@IDCategoria", idCategoria);
                cmd.Parameters.AddWithValue("@IDMarca", m.IdMarca);
                cmd.Parameters.AddWithValue("@Marcado", m.Marcado ? 1 : 0);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            return new ResultadoLegacy { Result = 1, Mensaje = string.Empty };
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
