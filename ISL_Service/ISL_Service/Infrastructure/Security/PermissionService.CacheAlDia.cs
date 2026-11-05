using System.Collections.Concurrent;
using System.Data;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ISL_Service.Infrastructure.Security;

/// <summary>
/// QUE LA CACHE DE PERMISOS SE ENTERE DE LOS CAMBIOS HECHOS POR BASE.
/// </summary>
/// <remarks>
/// EL DOLOR QUE RESUELVE, CON FECHA
/// --------------------------------
/// El 2026-11-04, para que una persona pudiera capturar un pedido desde el
/// telefono, hubo que conceder permisos con `INSERT` directo en
/// `WUsuarioPermiso` —no por gusto: la pantalla de permisos no podia hacerlo—.
/// Y cada concesion por base obligo a REINICIAR EL POOL DE IIS DE PRODUCCION.
/// Cuatro veces ese dia.
///
/// La razon es que la unica invalidacion que existia vivia al final de
/// `UpsertUserOverridesAsync`, o sea DENTRO DEL CAMINO DE ESCRITURA del boton
/// Guardar. Quien escribe por otro camino —un INSERT, un script, el respaldo
/// que se repone despues de un incidente— no pasa por ahi, y el backend sigue
/// sirviendo su copia en memoria: `ActiveCacheTtl` 5 minutos, y hasta 30 el
/// renglon de rescate.
///
/// Reiniciar el pool funciona y es lo peor que se puede hacer: tira TODAS las
/// sesiones, vacia TODAS las caches de la aplicacion y deja a la empresa entera
/// pagando el arranque en frio, para arreglarle un permiso a una persona.
///
/// LAS DOS MITADES DE LA SOLUCION, Y POR QUE HACEN FALTA LAS DOS
/// -------------------------------------------------------------
/// 1. UN BOTON EXPLICITO —`PermisosCacheController`—, que tira la cache de un
///    usuario (o de la empresa) cuando alguien lo pide. Es inmediato, no tiene
///    espera, y es la salida que faltaba cuando hay prisa.
///
/// 2. ESTE LATIDO, que es la mitad que de verdad cierra el agujero: la
///    invalidacion DEJA DE DEPENDER DEL CAMINO DE ESCRITURA. No importa quien
///    escribio ni por donde; si las filas de permisos de la base cambiaron, la
///    cache se tira sola. Sin el, cada cambio por base sigue necesitando que
///    alguien se acuerde de pulsar algo, y "acordarse" no es un plan — es la
///    misma frase que ya esta escrita en `PermisoControllerBase`.
///
/// COMO FUNCIONA, Y LO QUE CUESTA
/// ------------------------------
/// Antes de servir permisos se comprueba —COMO MUCHO UNA VEZ CADA
/// <see cref="LatidoTtl"/> POR EMPRESA Y POR PROCESO— una huella de las filas
/// de permisos de esa empresa:
///
///     WUsuarioPermiso   COUNT + CHECKSUM_AGG, AGRUPADO POR USUARIO
///     WRolPermiso       COUNT + CHECKSUM_AGG, de toda la empresa
///
/// Si la huella de un usuario cambio, se tira SOLO la cache de ese usuario. Si
/// cambio la de los roles, la de la empresa entera (un rol lo comparte gente).
///
/// EL COSTE SON DOS CONSULTAS CADA 15 SEGUNDOS POR EMPRESA. Hay que decirlo con
/// el numero delante porque el antecedente de esta casa es justo el contrario:
/// un `/api/permisos-web/bootstrap` hacia 5383 viajes a SQL y 4160 eran la
/// misma pregunta repetida en un bucle (ver el comentario de los memos en
/// PermissionService.cs). Aqui son DOS viajes por cuarto de minuto, no dos por
/// peticion ni dos por usuario: a ~50 ms el viaje contra la base de produccion,
/// son ~8 ms por segundo de reloj. Y se cobran en UNA sola peticion de cada
/// ventana, nunca en todas.
///
/// A cambio, el desfase maximo entre tocar la base y que el backend lo sirva
/// baja de 5-30 MINUTOS a 15 SEGUNDOS, sin reiniciar nada.
///
/// POR QUE UNA HUELLA Y NO UNA TABLA DE VERSIONES CON SU DISPARADOR
/// ----------------------------------------------------------------
/// La alternativa evidente era una tabla `WPermisoVersion` con un contador por
/// usuario y un disparador en `WUsuarioPermiso` que lo subiera. Es mas elegante
/// y se descarto a proposito:
///
///   * PIDE UN CAMBIO DE ESQUEMA EN PRODUCCION —tabla nueva mas disparador— y
///     esto hay que poder soltarlo el mismo dia. Aqui no se crea ni se altera
///     nada: solo se LEE, con dos agregados.
///   * UN DISPARADOR QUE FALLE TIRA LA ESCRITURA que lo provoco, o sea que
///     meteria al boton Guardar de la pantalla de permisos —lo mas delicado que
///     hay ahora mismo— una pieza nueva que lo puede romper.
///   * Y NO MEDIRIA MAS: un disparador se entera de los `INSERT`, igual que
///     esta huella. Lo que de verdad cambia la vida es dejar de depender del
///     camino de escritura, y eso ya lo da la huella.
///   * Encima, el disparador seria una pieza mas que hay que recordar instalar
///     EN CADA BASE. Las dos empresas tienen bases distintas y sus propios
///     `sp_w_`, y ya hay precedente de un despliegue que se quedo en una sola
///     (`sp_mo_ConsultaMisPedidos`, que existia en Zaragoza y no en Tauro).
///
/// LO QUE ESTO **NO** HACE, DICHO CLARO
/// ------------------------------------
/// * NO ES UN DETECTOR DE CAMBIOS PERFECTO. `CHECKSUM_AGG` es un XOR: dos
///   cambios distintos pueden darle la misma huella. Por eso se mira TAMBIEN el
///   numero de filas, y por eso el boton explicito sigue existiendo. Para lo
///   que se usa —ver que una persona cambio de permisos— es de sobra; para
///   garantizar que ningun cambio concebible pasa inadvertido, no.
/// * NO SE INVALIDA NADA EN LA PRIMERA MEDICION de cada proceso: no hay con que
///   comparar, y la cache acaba de nacer vacia. Se guarda la foto y punto.
/// * SI LA MEDICION FALLA —base inalcanzable, tablas de capacidades ausentes en
///   un tenant— se anota y NO se invalida nada. Comportarse como hasta hoy es
///   lo seguro: el fallo de un diagnostico no puede dejar a la empresa sin
///   permisos en cache justo cuando la base va mal.
/// * NO MIRA `WPermiso` (el catalogo de claves que EXISTEN). Sembrar una clave
///   nueva no le concede nada a nadie, asi que tirar la cache por eso seria
///   vaciarla entera en cada arranque del backend.
/// * NO MIRA MAC31. Lo que cambie en `[Usuario Forma Procesos]` sigue tardando
///   lo que tardaba (`ActiveCacheTtl`). Es otra tabla, de otra aplicacion, y
///   agregarla aqui es un viaje mas y una decision aparte.
/// </remarks>
public sealed partial class PermissionService
{
    /// <summary>
    /// Cada cuanto, COMO MUCHO, se vuelve a medir la huella de una empresa.
    ///
    /// Quince segundos es el numero con el que se puede decir a la persona
    /// "concedelo y vuelve a entrar": lo que tarda en cerrar sesion y teclear
    /// su clave otra vez. Mas corto no compra nada porque la app ademas guarda
    /// los permisos de la ultima sesion; mas largo vuelve a obligar a explicar
    /// una espera.
    /// </summary>
    private static readonly TimeSpan LatidoTtl = TimeSpan.FromSeconds(15);

    /// <summary>La ultima huella medida de cada empresa. Estatico por lo mismo
    /// que <c>_invalidadores</c>: el servicio es Scoped y la cache no.</summary>
    private static readonly ConcurrentDictionary<int, LatidoDePermisos> _latidos = new();

    private sealed record LatidoDePermisos(
        DateTime Hasta,
        bool Medido,
        long RolFilas,
        int RolHuella,
        IReadOnlyDictionary<Guid, HuellaDeUsuario> PorUsuario);

    private readonly record struct HuellaDeUsuario(long Filas, int Huella);

    /// <summary>
    /// Mira si las filas de permisos de la base cambiaron desde la ultima vez y,
    /// si cambiaron, tira la cache de quien corresponda. Barato y silencioso:
    /// en regimen normal no hace ni un viaje a la base.
    /// </summary>
    private async Task AsegurarCacheAlDiaAsync(int empresaId, CancellationToken ct)
    {
        if (_latidos.TryGetValue(empresaId, out var previo) && DateTime.UtcNow < previo.Hasta)
            return;

        /*
          EL CANDADO ES "SI ESTA OCUPADO, ME VOY", no "espero mi turno".

          Al vencer la ventana pueden entrar aqui varias peticiones a la vez.
          Que todas esperen a la de delante seria convertir un diagnostico en
          una cola —el mismo fallo que la estampida de cache que ya se midio en
          produccion—. Quien no coge el candado sigue su camino con la cache que
          ya tenia: como maximo se entera en el latido siguiente.
        */
        var candado = _candados.GetOrAdd($"latido:{empresaId}", _ => new SemaphoreSlim(1, 1));
        if (!await candado.WaitAsync(TimeSpan.Zero, ct))
            return;

        try
        {
            /* Puede haberlo medido quien iba delante mientras se cogia el candado. */
            if (_latidos.TryGetValue(empresaId, out previo) && DateTime.UtcNow < previo.Hasta)
                return;

            var medicion = await MedirHuellaDePermisosAsync(empresaId, ct);
            var hasta = DateTime.UtcNow.Add(LatidoTtl);

            if (medicion is null)
            {
                /* No se pudo medir: se conserva la foto anterior y se reintenta
                   en la ventana siguiente. Sin invalidar nada. */
                _latidos[empresaId] = (previo ?? VacioSinMedir()) with { Hasta = hasta };
                return;
            }

            var ahora = medicion with { Hasta = hasta };
            _latidos[empresaId] = ahora;

            if (previo is null || !previo.Medido)
            {
                /* Primera medicion de este proceso: no hay con que comparar. */
                return;
            }

            if (previo.RolFilas != ahora.RolFilas || previo.RolHuella != ahora.RolHuella)
            {
                _logger.LogInformation(
                    "Permisos: cambiaron los permisos de ROL en la base (empresaId={EmpresaId}, filas {FilasAntes}->{FilasAhora}). Se tira la cache de la empresa.",
                    empresaId, previo.RolFilas, ahora.RolFilas);
                InvalidateEmpresaPermissionCache(empresaId);
                return;
            }

            var cambiados = new List<Guid>();
            foreach (var userId in previo.PorUsuario.Keys.Union(ahora.PorUsuario.Keys))
            {
                var antes = previo.PorUsuario.TryGetValue(userId, out var a) ? a : default;
                var despues = ahora.PorUsuario.TryGetValue(userId, out var d) ? d : default;
                if (antes.Filas != despues.Filas || antes.Huella != despues.Huella)
                    cambiados.Add(userId);
            }

            foreach (var userId in cambiados)
                InvalidateUserPermissionCache(userId, empresaId);

            if (cambiados.Count > 0)
            {
                _logger.LogInformation(
                    "Permisos: cambiaron en la base los ajustes de {Cuantos} usuario(s) (empresaId={EmpresaId}): {Usuarios}. Cache tirada sin reiniciar nada.",
                    cambiados.Count, empresaId, string.Join(", ", cambiados));
            }
        }
        catch (OperationCanceledException)
        {
            /* El cliente se fue. No es un fallo y no se anota como tal. */
        }
        catch (Exception ex)
        {
            /* Un diagnostico que falla no puede tumbar la lectura de permisos. */
            _logger.LogDebug(ex, "Permisos: no se pudo comprobar si la base cambio (empresaId={EmpresaId}).", empresaId);
        }
        finally
        {
            candado.Release();
        }
    }

    private static LatidoDePermisos VacioSinMedir() => new(
        DateTime.MinValue, false, 0, 0,
        new Dictionary<Guid, HuellaDeUsuario>());

    /// <summary>
    /// Los dos agregados, en una sola ida a la base. Devuelve null si no se
    /// pudo medir (tenant sin tablas de capacidades, base caida...).
    /// </summary>
    private async Task<LatidoDePermisos?> MedirHuellaDePermisosAsync(int empresaId, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);

        if (!await AreCapabilityTablesAvailableAsync(conn, ct))
            return null;

        var schema = await GetSchemaAsync(conn, ct);

        /*
          SE NOMBRAN LAS COLUMNAS, no se usa CHECKSUM(*).

          Dos razones. Una: `CHECKSUM(*)` revienta con algunos tipos de columna,
          y el esquema de produccion NO es el mismo que el de local (ya paso con
          `Usuarios.EsAgente`, que alla no existe) — una columna nueva de un tipo
          raro dejaria el latido muerto en silencio. Dos: asi la huella solo
          cambia cuando cambia LO QUE DECIDE —a quien, que permiso y si es
          conceder o negar—, y no cuando alguien reescribe el Motivo.

          Los nombres salen del esquema (`GetSchemaAsync`) porque en esta casa
          esas columnas se llaman distinto segun la base.
        */
        var sql = $@"
SELECT up.{Q(schema.UserPermUserIdColumn)} AS UsuarioId,
       COUNT_BIG(*)                        AS Filas,
       CHECKSUM_AGG(CHECKSUM(up.{Q(schema.UserPermPermissionIdColumn)}, up.Tipo)) AS Huella
FROM dbo.WUsuarioPermiso up
WHERE up.EmpresaId = @EmpresaId
GROUP BY up.{Q(schema.UserPermUserIdColumn)};

SELECT COUNT_BIG(*) AS Filas,
       CHECKSUM_AGG(CHECKSUM(rp.{Q(schema.RolePermRoleIdColumn)}, rp.{Q(schema.RolePermPermissionIdColumn)})) AS Huella
FROM dbo.WRolPermiso rp
WHERE rp.EmpresaId = @EmpresaId;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });

        var porUsuario = new Dictionary<Guid, HuellaDeUsuario>();
        long rolFilas = 0;
        var rolHuella = 0;

        await using var lector = await cmd.ExecuteReaderAsync(ct);
        while (await lector.ReadAsync(ct))
        {
            if (lector.IsDBNull(0)) continue;
            porUsuario[lector.GetGuid(0)] = new HuellaDeUsuario(
                lector.GetInt64(1),
                lector.IsDBNull(2) ? 0 : lector.GetInt32(2));
        }

        if (await lector.NextResultAsync(ct) && await lector.ReadAsync(ct))
        {
            rolFilas = lector.IsDBNull(0) ? 0 : lector.GetInt64(0);
            rolHuella = lector.IsDBNull(1) ? 0 : lector.GetInt32(1);
        }

        return new LatidoDePermisos(DateTime.MinValue, true, rolFilas, rolHuella, porUsuario);
    }

    /// <summary>
    /// Tira la cache de permisos de UNA persona. Es lo que llama el endpoint de
    /// administracion; la logica es la misma que ya corre al dar Guardar.
    /// </summary>
    public void InvalidarCacheDeUsuario(int empresaId, Guid userId)
    {
        InvalidateUserPermissionCache(userId, empresaId);

        /*
          Y SE OLVIDA LA HUELLA DE LA EMPRESA.

          Si no, pasa esto: alguien concede por base, pulsa el boton, y el
          latido siguiente compara la base con una foto TOMADA ANTES de la
          concesion, ve que cambio y vuelve a invalidar — trabajo de mas por
          algo que ya se atendio. Olvidarla hace que la proxima medicion sea
          "la primera", que no invalida nada y solo vuelve a retratar.
        */
        _latidos.TryRemove(empresaId, out _);
    }

    /// <summary>
    /// Tira la cache de permisos de TODA la empresa. Para cuando se toco la base
    /// de varias personas a la vez: un script de reparto, un respaldo repuesto.
    /// </summary>
    public void InvalidarCacheDeEmpresa(int empresaId)
    {
        InvalidateEmpresaPermissionCache(empresaId);
        _latidos.TryRemove(empresaId, out _);
    }
}
