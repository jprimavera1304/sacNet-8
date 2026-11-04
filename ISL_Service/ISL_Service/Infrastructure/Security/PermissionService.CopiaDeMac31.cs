using System.Data;
using ISL_Service.Application.DTOs.Responses;
using ISL_Service.Application.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ISL_Service.Infrastructure.Security;

/// <summary>
/// COPIAR DE MAC31 UNA VEZ, EN VEZ DE HEREDAR SIEMPRE.
/// </summary>
/// <remarks>
/// LO QUE HAY HOY, Y POR QUE NO ALCANZA
/// ------------------------------------
/// El puente de familias (PermissionService.FamiliasLegacy.cs) hereda EN VIVO:
/// en cada calculo BORRA del web todo lo que empiece por el prefijo de cada
/// familia de Mac31 y lo repone leyendo [Usuario Forma Procesos]. Eso es lo
/// correcto mientras el web sea un espejo del escritorio, y es lo que quita la
/// doble captura. Pero convierte el web en un espejo SIN EXCEPCIONES, y el
/// 2026-11-04 se midio en Tauro lo que eso cuesta:
///
///   * marcar "quitado" en la pantalla de permisos no pega: Mac31 lo devuelve
///     en el siguiente calculo;
///   * conceder `ventas.pedidos.crear` con un INSERT queda escrito y sin
///     efecto, por el mismo borrado por prefijo;
///   * no existe forma de decir "esta persona SI puede esto en Mac31 pero NO
///     en el web", que es justo lo que pide quien administra: el web tiene
///     pantallas que el escritorio no tiene y al reves.
///
/// LA SALIDA: UNIDOS POR UNA COPIA, NO POR UN ESPEJO
/// -------------------------------------------------
/// Un boton en la pantalla de permisos copia UNA VEZ lo que Mac31 le da hoy a
/// esa persona y lo deja escrito como excepciones propias del web. Desde ese
/// momento esa persona sale de la herencia en vivo: manda el web. Y como el
/// web deja de ser espejo, QUITARLE ALGO EN EL WEB NO SE LO QUITA EN MAC31
/// (ver el candado del write-back en UpsertUserOverridesAsync).
///
/// LAS CUATRO DECISIONES, Y SU PORQUE
/// ----------------------------------
/// 1) LA MARCA ES POR PERSONA, NO POR FAMILIA. La pregunta que trae quien abre
///    la pantalla es de una persona. Por familia serian 26 interruptores por
///    persona y un estado mitad-espejo imposible de explicar ni de auditar: al
///    mirar una clave habria que saber primero en que modo esta su familia para
///    esa persona. La granularidad fina existe donde sirve: el DETALLE de la
///    copia guarda clave por clave lo que se escribio y lo que habia antes, y
///    es lo que permite deshacerla exactamente.
///
/// 2) QUIEN NO SE HA COPIADO NO CAMBIA EN NADA. Esto es lo mas importante de
///    todo el cambio y por eso se sostiene en tres cosas, no en una:
///      * la pregunta es "¿tiene fila en WPermisoCopiaMac31?", y sin fila el
///        codigo recorre EXACTAMENTE las mismas lineas que antes;
///      * si las tablas no existen en la base, la respuesta es NO para todo el
///        mundo (TableExistsAsync, memorizado), nunca un error;
///      * el catalogo de Mac31 se sigue sembrando igual para todos, copiados o
///        no, porque es "que claves existen" y de ahi vive la pantalla.
///
/// 3) LO QUE EN MAC31 LE DEN DESPUES, NO LE LLEGA. Es la consecuencia directa
///    de copiar en vez de heredar, no un defecto: lo contrario seria seguir
///    siendo un espejo. La pantalla lo dice con palabras de quien administra
///    (ver el texto en ModalPermisos.tsx) y la copia se puede deshacer.
///
/// 4) COPIAR NO PUEDE CAMBIAR LO QUE LA PERSONA PUEDE HACER, NI UN PERMISO.
///    Por eso la copia no se calcula "a ojo" desde los botones de Mac31, sino
///    desde el SNAPSHOT que esa persona tiene hoy, que es el unico sitio donde
///    ya estan resueltas todas las reglas del puente: el borrado por prefijo,
///    las semillas que lo sobreviven, las entradas derivadas (`.ver_modulo`),
///    la lectura derivada, los modulos apagados y los dos vocabularios de
///    `Tipo`. Lo que se escribe es:
///
///      CONCEDER  las claves del territorio de Mac31 que hoy tiene.
///      QUITAR    las claves del territorio de Mac31 que su ROL del web da y
///                que hoy NO tiene.
///
///    El segundo renglon es el que nadie ve venir y sin el la copia REGALA
///    accesos: hoy el borrado por prefijo se lleva por delante lo que el rol
///    del web concede en esas familias (por eso Mac31 "manda"); al dejar de
///    heredar, esas claves del rol volverian a contar. Dejarlas quitadas a mano
///    es la traduccion fiel del borrado por prefijo.
///
/// LO QUE ESTE ARCHIVO NO HACE
/// ---------------------------
/// No escribe NADA en Mac31. De legacy solo se lee, y aqui ni eso: lee el
/// snapshot que ya calculo el resto del servicio. Las dos tablas que usa son
/// nuevas y llevan prefijo W (ver Querrys\64 Copiar Permisos De Mac31).
/// </remarks>
public sealed partial class PermissionService : ICopiaDeMac31Service
{
    private const string TablaDeCopia = "WPermisoCopiaMac31";
    private const string TablaDeCopiaClaves = "WPermisoCopiaMac31Clave";

    /// <summary>
    /// Si esta persona YA NO hereda en vivo de Mac31 porque se le copiaron sus
    /// permisos.
    /// </summary>
    /// <remarks>
    /// Devuelve false -o sea "hereda, como siempre"- cuando las tablas de la
    /// copia no existen todavia. Esa es la razon por la que este cambio se
    /// puede desplegar antes de correr el script sin que nadie note nada.
    ///
    /// Se memoriza en la MEMORIA DE LA PETICION, no en el cache de catalogo: es
    /// un dato de un usuario, y un dato de usuario que sobreviva a la peticion
    /// es exactamente el agujero que ya documenta `_memoDeLaPeticion`. Dentro
    /// de una peticion se pregunta hasta tres veces (snapshot, pantalla,
    /// guardado) y la respuesta no puede cambiar a la mitad.
    /// </remarks>
    private async Task<bool> TienePermisosPropiosAsync(
        SqlConnection conn,
        int empresaId,
        Guid userId,
        CancellationToken ct)
    {
        var llave = $"copiamac31|{conn.Database}|{empresaId}|{userId}";
        var memoria = _memoDeLaPeticion.Value;
        if (memoria is not null && memoria.TryGetValue(llave, out var guardado) && guardado is bool vigente)
            return vigente;

        var tiene = false;
        if (await TableExistsAsync(conn, TablaDeCopia, ct))
        {
            await using var cmd = new SqlCommand($@"
SELECT TOP 1 1
FROM dbo.{TablaDeCopia}
WHERE EmpresaId = @EmpresaId
  AND UsuarioWebId = @UsuarioWebId;", conn);
            cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            cmd.Parameters.Add(new SqlParameter("@UsuarioWebId", SqlDbType.UniqueIdentifier) { Value = userId });
            tiene = await cmd.ExecuteScalarAsync(ct) is not null;
        }

        if (memoria is not null)
            memoria[llave] = tiene;
        return tiene;
    }

    /* ------------------------------------------------------------------ */
    /* EL TERRITORIO DE MAC31                                             */
    /* ------------------------------------------------------------------ */

    /// <summary>
    /// Que claves gobierna Mac31 en ESTA base, y cuales son las puertas que el
    /// puente deriva en vez de leerlas de un boton.
    /// </summary>
    /// <remarks>
    /// Se resuelve CONTRA LA CONEXION y no con una lista a mano, por lo mismo
    /// que ya explica PermissionService.FamiliasLegacy.cs: una base donde Mac31
    /// no tenga una forma dada de alta trae el catalogo de esa familia vacio, y
    /// entonces esa familia NO gobierna nada —el puente no le borra nada al
    /// web—. Si aqui se diera por gobernada, la copia le pondria "quitado" a
    /// claves que hoy si funcionan.
    /// </remarks>
    private sealed record TerritorioDeMac31(
        Func<string, bool> LaManda,
        HashSet<string> PuertasDerivadas);

    private async Task<TerritorioDeMac31> LeerTerritorioDeMac31Async(
        SqlConnection conn,
        int empresaId,
        CancellationToken ct)
    {
        var reportes = await EnsureLegacyReportPermissionsSyncedAsync(conn, empresaId, ct);
        var ventas = await EnsureVentasPermissionsSyncedAsync(conn, empresaId, ct);
        var empleados = await EnsureLegacyModulePermissionsSyncedAsync(conn, empresaId, EmpleadosBinding, ct);
        var prestamos = await EnsureLegacyModulePermissionsSyncedAsync(conn, empresaId, PrestamosBinding, ct);
        var familias = await EnsureFamiliasLegacySyncedAsync(conn, empresaId, ct);

        bool LaManda(string clave)
        {
            if (string.IsNullOrWhiteSpace(clave)) return false;

            // Las cuatro viejas, con la MISMA condicion que el snapshot: si su
            // catalogo viene vacio, el snapshot no borra nada y por tanto esa
            // familia no gobierna.
            if (reportes.Count > 0 && EsReporteQueMandaLegacy(clave)) return true;
            if (ventas.Count > 0 && EsDeFamiliaVieja(clave, VentasModuleKey, VentasWebPermissionSeeds)) return true;
            if (empleados.Count > 0 && EsDeFamiliaVieja(clave, EmpleadosModuleKey, EmpleadosWebPermissionSeeds)) return true;
            if (prestamos.Count > 0 && EsDeFamiliaVieja(clave, PrestamosModuleKey, PrestamosWebPermissionSeeds)) return true;

            foreach (var familia in FamiliasLegacyAmpliadas)
            {
                if (!familias.TryGetValue(familia.Prefijo, out var catalogo) || catalogo.Count == 0)
                    continue;
                if (LaMandaLegacy(familia, clave))
                    return true;
            }
            return false;
        }

        /*
          LAS PUERTAS NO LAS MANDA MAC31 Y AUN ASI HAY QUE COPIARLAS.

          Los `.ver_modulo` (y las lecturas derivadas tipo "compras.ver") son
          SEMILLAS DEL WEB: sobreviven al borrado por prefijo a proposito, y el
          puente las ENCIENDE cuando Mac31 le dio algo de esa familia. O sea
          que hoy una persona puede entrar al modulo sin tener ninguna fila en
          el web que se lo conceda.

          Si la copia no las escribiera, al dejar de heredar se quedaria con los
          permisos finos y sin la puerta: 403 en todo el modulo. Es el mismo
          agujero que ya se midio al exigir la entrada en el backend.
        */
        var puertas = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ReportesViewPermission,
            VentasViewPermission,
            EmpleadosViewPermission,
            PrestamosViewPermission
        };
        foreach (var familia in FamiliasLegacyAmpliadas)
        {
            if (familia.ClaveDeEntrada is { Length: > 0 } entradaDeFamilia)
                puertas.Add(entradaDeFamilia);
            foreach (var binding in familia.Formas)
            {
                if (binding.ClaveDeEntrada is { Length: > 0 } entradaDeForma)
                    puertas.Add(entradaDeForma);
                if (binding.ClaveDeLectura is { Length: > 0 } lecturaDeForma)
                    puertas.Add(lecturaDeForma);
            }
        }

        return new TerritorioDeMac31(LaManda, puertas);
    }

    /// <summary>
    /// La regla de las cuatro familias viejas: todo lo del prefijo MENOS sus
    /// semillas del web. Es textualmente la condicion que usan los
    /// `permissions.RemoveAll(...)` del snapshot.
    /// </summary>
    private static bool EsDeFamiliaVieja(string clave, string moduleKey, IReadOnlyList<PermissionSeed> semillas) =>
        clave.StartsWith($"{moduleKey}.", StringComparison.OrdinalIgnoreCase)
        && !semillas.Any(s => s.Key.Equals(clave, StringComparison.OrdinalIgnoreCase));

    /* ------------------------------------------------------------------ */
    /* CONSULTAR                                                          */
    /* ------------------------------------------------------------------ */

    public async Task<IReadOnlyList<CopiaDeMac31Item>> ListarCopiasDeMac31Async(int empresaId, CancellationToken ct)
    {
        var lista = new List<CopiaDeMac31Item>();

        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);

        // Sin tablas no hay nadie copiado. No es un error: es una base donde
        // todavia no se instalo, y ahi todo el mundo hereda como siempre.
        if (!await TableExistsAsync(conn, TablaDeCopia, ct))
            return lista;

        await using var cmd = new SqlCommand($@"
SELECT c.UsuarioWebId, c.Fecha, c.CopiadoPor, c.Claves
FROM dbo.{TablaDeCopia} c
WHERE c.EmpresaId = @EmpresaId
ORDER BY c.Fecha DESC;", conn);
        cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            lista.Add(new CopiaDeMac31Item
            {
                UserId = reader.GetGuid(0),
                Fecha = reader.GetDateTime(1),
                CopiadoPor = reader.IsDBNull(2) ? null : reader.GetString(2),
                Claves = reader.GetInt32(3)
            });
        }

        return lista;
    }

    /* ------------------------------------------------------------------ */
    /* COPIAR                                                             */
    /* ------------------------------------------------------------------ */

    public async Task<CopiaDeMac31Resultado> CopiarDeMac31Async(
        int empresaId,
        Guid userId,
        string? copiadoPor,
        CancellationToken ct)
    {
        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);

        if (!await AreCapabilityTablesAvailableAsync(conn, ct))
            throw new KeyNotFoundException("Capacidades no disponibles para este tenant/base.");

        if (!await TableExistsAsync(conn, TablaDeCopia, ct) || !await TableExistsAsync(conn, TablaDeCopiaClaves, ct))
            throw new KeyNotFoundException("Esta base todavia no tiene preparada la copia de permisos.");

        if (await TienePermisosPropiosAsync(conn, empresaId, userId, ct))
        {
            return new CopiaDeMac31Resultado
            {
                Copiado = false,
                Mensaje = "Esta persona ya tiene sus propios permisos en esta plataforma."
            };
        }

        var schema = await GetSchemaAsync(conn, ct);
        var rol = await GetUserRoleAsync(conn, empresaId, userId, ct);

        /*
          LAS DOS FOTOS, Y POR QUE HACEN FALTA LAS DOS.

          `hoy`    lo que la persona puede HOY, con la herencia puesta. Es la
                   unica foto donde ya estan resueltas todas las reglas del
                   puente, y es lo que la copia tiene que dejar igual.
          `soloWeb` lo que diria el web por si solo (rol + ajustes), sin la
                   herencia. Lo que este aqui dentro del territorio de Mac31 y
                   NO este en `hoy` es exactamente lo que el borrado por
                   prefijo le esta quitando: al dejar de heredar hay que
                   quitarlo a mano o la persona ganaria accesos nuevos.
        */
        var hoy = (await BuildSnapshotAsync(userId, empresaId, rol, ct)).Permissions;
        var soloWeb = await LoadEffectivePermissionsAsync(conn, schema, userId, empresaId, rol, ct);
        var territorio = await LeerTerritorioDeMac31Async(conn, empresaId, ct);

        var hoySet = new HashSet<string>(hoy, StringComparer.OrdinalIgnoreCase);

        var conceder = hoy
            .Where(k => territorio.LaManda(k) || territorio.PuertasDerivadas.Contains(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var quitar = soloWeb
            .Where(k => territorio.LaManda(k) && !hoySet.Contains(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var overrideTypes = await GetOverrideTypeTokensAsync(conn, ct);
        var userPermCols = await GetUserPermColumnsAsync(conn, null, ct);

        var concedidos = 0;
        var quitados = 0;
        var sinCatalogo = 0;

        await using var tx = await conn.BeginTransactionAsync(ct);
        var transaccion = (SqlTransaction)tx;

        await EnsureUserExistsAsync(conn, transaccion, empresaId, userId, ct);

        var ids = await GetPermissionIdsByKeysAsync(
            conn, transaccion, schema, empresaId,
            conceder.Concat(quitar).Distinct(StringComparer.OrdinalIgnoreCase).ToList(), ct);

        // La marca va PRIMERO porque el detalle tiene una FK contra ella.
        await using (var marcaCmd = new SqlCommand($@"
INSERT INTO dbo.{TablaDeCopia} (EmpresaId, UsuarioWebId, Fecha, CopiadoPor, Claves)
VALUES (@EmpresaId, @UsuarioWebId, SYSUTCDATETIME(), @CopiadoPor, 0);", conn, transaccion))
        {
            marcaCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            marcaCmd.Parameters.Add(new SqlParameter("@UsuarioWebId", SqlDbType.UniqueIdentifier) { Value = userId });
            marcaCmd.Parameters.Add(new SqlParameter("@CopiadoPor", SqlDbType.NVarChar, 160)
            {
                Value = string.IsNullOrWhiteSpace(copiadoPor) ? DBNull.Value : copiadoPor.Trim()
            });
            await marcaCmd.ExecuteNonQueryAsync(ct);
        }

        foreach (var (clave, esConceder) in conceder.Select(k => (k, true)).Concat(quitar.Select(k => (k, false))))
        {
            if (!ids.TryGetValue(clave, out var permisoId))
            {
                // La clave no esta en el catalogo de esta base. No se inventa:
                // se cuenta y se reporta, que es lo que permite enterarse.
                sinCatalogo++;
                continue;
            }

            var tipoAnterior = await QuitarAjusteAsync(conn, transaccion, schema, empresaId, userId, permisoId, ct);

            await InsertUserOverrideAsync(
                conn, transaccion, schema, userPermCols, empresaId, userId, permisoId,
                esConceder ? overrideTypes.AllowToken : overrideTypes.DenyToken, ct);

            await using (var detalleCmd = new SqlCommand($@"
INSERT INTO dbo.{TablaDeCopiaClaves} (EmpresaId, UsuarioWebId, Clave, Tipo, TipoAnterior)
VALUES (@EmpresaId, @UsuarioWebId, @Clave, @Tipo, @TipoAnterior);", conn, transaccion))
            {
                detalleCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
                detalleCmd.Parameters.Add(new SqlParameter("@UsuarioWebId", SqlDbType.UniqueIdentifier) { Value = userId });
                detalleCmd.Parameters.Add(new SqlParameter("@Clave", SqlDbType.NVarChar, 200) { Value = clave });
                detalleCmd.Parameters.Add(new SqlParameter("@Tipo", SqlDbType.VarChar, 10) { Value = esConceder ? "allow" : "deny" });
                detalleCmd.Parameters.Add(new SqlParameter("@TipoAnterior", SqlDbType.VarChar, 10)
                {
                    Value = tipoAnterior is null ? DBNull.Value : tipoAnterior
                });
                await detalleCmd.ExecuteNonQueryAsync(ct);
            }

            if (esConceder) concedidos++;
            else quitados++;
        }

        await using (var cuentaCmd = new SqlCommand($@"
UPDATE dbo.{TablaDeCopia}
SET Claves = @Claves
WHERE EmpresaId = @EmpresaId
  AND UsuarioWebId = @UsuarioWebId;", conn, transaccion))
        {
            cuentaCmd.Parameters.Add(new SqlParameter("@Claves", SqlDbType.Int) { Value = concedidos + quitados });
            cuentaCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            cuentaCmd.Parameters.Add(new SqlParameter("@UsuarioWebId", SqlDbType.UniqueIdentifier) { Value = userId });
            await cuentaCmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);

        /* La respuesta anterior sigue guardada en el cache de permisos y en el
           memo de esta peticion: sin esto, la persona seguiria heredando hasta
           que venciera el plazo. */
        OlvidarQueHereda(conn, empresaId, userId);
        InvalidateUserPermissionCache(userId, empresaId);

        return new CopiaDeMac31Resultado
        {
            Copiado = true,
            Concedidos = concedidos,
            Quitados = quitados,
            SinCatalogo = sinCatalogo,
            Mensaje = $"Se copiaron {concedidos + quitados} permisos."
        };
    }

    /* ------------------------------------------------------------------ */
    /* DESHACER                                                           */
    /* ------------------------------------------------------------------ */

    public async Task<bool> DeshacerCopiaDeMac31Async(int empresaId, Guid userId, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);

        if (!await TableExistsAsync(conn, TablaDeCopia, ct) || !await TableExistsAsync(conn, TablaDeCopiaClaves, ct))
            return false;

        if (!await TienePermisosPropiosAsync(conn, empresaId, userId, ct))
            return false;

        var schema = await GetSchemaAsync(conn, ct);
        var userPermCols = await GetUserPermColumnsAsync(conn, null, ct);

        /*
          SE DESHACE CON EL DETALLE, CLAVE POR CLAVE, Y NO BORRANDO TODO.

          Borrarle al usuario todos sus ajustes seria mas corto y perderia lo
          que alguien le hubiera configurado A MANO antes de copiar. El detalle
          guarda que habia en cada fila antes de que la copia la tocara:
          sin fila previa se borra, y con fila previa se repone su tipo.
        */
        var detalle = new List<(string Clave, string? TipoAnterior)>();
        await using (var leer = new SqlCommand($@"
SELECT Clave, TipoAnterior
FROM dbo.{TablaDeCopiaClaves}
WHERE EmpresaId = @EmpresaId
  AND UsuarioWebId = @UsuarioWebId;", conn))
        {
            leer.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            leer.Parameters.Add(new SqlParameter("@UsuarioWebId", SqlDbType.UniqueIdentifier) { Value = userId });
            await using var reader = await leer.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                detalle.Add((reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1)));
        }

        await using var tx = await conn.BeginTransactionAsync(ct);
        var transaccion = (SqlTransaction)tx;

        var ids = await GetPermissionIdsByKeysAsync(
            conn, transaccion, schema, empresaId,
            detalle.Select(d => d.Clave).ToList(), ct);

        foreach (var (clave, tipoAnterior) in detalle)
        {
            if (!ids.TryGetValue(clave, out var permisoId))
                continue;

            await QuitarAjusteAsync(conn, transaccion, schema, empresaId, userId, permisoId, ct);

            if (tipoAnterior is not null)
            {
                await InsertUserOverrideAsync(
                    conn, transaccion, schema, userPermCols, empresaId, userId, permisoId, tipoAnterior, ct);
            }
        }

        await using (var borrarDetalle = new SqlCommand($@"
DELETE FROM dbo.{TablaDeCopiaClaves}
WHERE EmpresaId = @EmpresaId
  AND UsuarioWebId = @UsuarioWebId;", conn, transaccion))
        {
            borrarDetalle.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            borrarDetalle.Parameters.Add(new SqlParameter("@UsuarioWebId", SqlDbType.UniqueIdentifier) { Value = userId });
            await borrarDetalle.ExecuteNonQueryAsync(ct);
        }

        await using (var borrarMarca = new SqlCommand($@"
DELETE FROM dbo.{TablaDeCopia}
WHERE EmpresaId = @EmpresaId
  AND UsuarioWebId = @UsuarioWebId;", conn, transaccion))
        {
            borrarMarca.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            borrarMarca.Parameters.Add(new SqlParameter("@UsuarioWebId", SqlDbType.UniqueIdentifier) { Value = userId });
            await borrarMarca.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);

        OlvidarQueHereda(conn, empresaId, userId);
        InvalidateUserPermissionCache(userId, empresaId);
        return true;
    }

    /* ------------------------------------------------------------------ */
    /* AYUDANTES                                                          */
    /* ------------------------------------------------------------------ */

    /// <summary>
    /// Quita el ajuste que ese usuario tenga sobre ese permiso y devuelve el
    /// `Tipo` que tenia, o null si no tenia ninguno.
    /// </summary>
    /// <remarks>
    /// Se borra y se vuelve a insertar en vez de hacer un UPDATE a proposito:
    /// la insercion pasa por <see cref="InsertUserOverrideAsync"/>, que es la
    /// unica que sabe negociar el vocabulario de la columna `Tipo` (hay bases
    /// con 'allow'/'deny' y bases con 'A'/'D'). Un UPDATE con el token
    /// equivocado chocaria con el CHECK y nadie sabria por que.
    /// </remarks>
    private async Task<string?> QuitarAjusteAsync(
        SqlConnection conn,
        SqlTransaction tx,
        CapabilitySchema schema,
        int empresaId,
        Guid userId,
        object permisoId,
        CancellationToken ct)
    {
        var sql = $@"
DELETE FROM dbo.WUsuarioPermiso
OUTPUT deleted.Tipo
WHERE EmpresaId = @EmpresaId
  AND {Q(schema.UserPermUserIdColumn)} = @UsuarioWebId
  AND {Q(schema.UserPermPermissionIdColumn)} = @WPermisoId;";

        await using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
        cmd.Parameters.Add(new SqlParameter("@UsuarioWebId", SqlDbType.UniqueIdentifier) { Value = userId });
        cmd.Parameters.Add(new SqlParameter("@WPermisoId", permisoId));
        var anterior = await cmd.ExecuteScalarAsync(ct);
        return anterior is null || anterior is DBNull ? null : Convert.ToString(anterior)?.Trim();
    }

    /// <summary>
    /// Olvida la respuesta memorizada de <see cref="TienePermisosPropiosAsync"/>
    /// para esta peticion.
    /// </summary>
    private static void OlvidarQueHereda(SqlConnection conn, int empresaId, Guid userId)
        => _memoDeLaPeticion.Value?.Remove($"copiamac31|{conn.Database}|{empresaId}|{userId}");
}
