using ISL_Service.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ISL_Service.Infrastructure.Repositories;

/*
  LA EMPRESA DE CENTRO DE SERVICIO DE ESTA INSTALACION. UN SOLO LUGAR.

  POR QUE EXISTE
  --------------
  sp_n_ConsultaClientes le pega SIEMPRE ' AND C.IDEmpresaCS = @IDEmpresaCS' al SQL
  que arma, asi que ese parametro NO es un filtro opcional por mas que lo parezca.
  En MacZ (Zaragoza) los 887 clientes del padron tienen IDEmpresaCS = 1, de modo que
  el 0 que estaba cableado como valor por omision del query string hacia que
  GET api/clientes contestara {"total":0} sobre un padron de 887, mientras que
  GET api/clientes?idEmpresaCs=1 contestaba los 887 completos.

  Un default que devuelve lista vacia no se lee como un default mal elegido: se lee
  como un filtro mal puesto, o como un padron vacio. Asi que el valor por omision
  sale de donde lo saca Mac31: dbo.Constantes.IDEmpresaCS (Variables.IDEmpresaCS,
  que Legacy puebla al arrancar).

  POR QUE NO SE USA sp_n_ConsultaConstantes PARA LEERLO
  ----------------------------------------------------
  Porque ese procedimiento NO es una consulta: HACE EL CIERRE DEL DIA. El IF de
  @ValidaActividad solo envuelve el bloque de actividad; el cierre -borrar y rehacer
  AlmacenProductoHist y adelantar Constantes.FechaOperacion- cuelga de su propia
  condicion, FUERA de ese IF (migracion/CUIDADO-consultas-que-escriben.md). Llamarlo
  desde una consulta de clientes moveria la fecha de operacion del negocio.

  Por eso se reusa ICapacidadesEmpresaRepository, que ya lee dbo.Constantes con un
  SELECT TOP 1 y sin escribir nada, y que es el mismo lector que alimenta el bloque
  `empresa` de /api/me. Un segundo lector de constantes seria un segundo lugar donde
  equivocarse sobre cual procedimiento se puede llamar.
*/
public static class EmpresaCsPorOmision
{
    /// <summary>
    /// IDEmpresaCS no cambia en caliente: hay que editar dbo.Constantes a mano, y
    /// Mac31 lo lee una sola vez al arrancar. Media hora es de sobra para no pegarle
    /// a la base en cada tecla del buscador, y corto como para que un cambio se note
    /// sin reiniciar el servicio.
    ///
    /// El cache hace falta aqui porque CapacidadesEmpresaService NO cachea, y es a
    /// proposito: por ahi viaja la fecha de operacion, que si se mueve. Lo que se
    /// guarda aqui es el entero, no la fila.
    /// </summary>
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    private const string ClaveDeCache = "constantes:IDEmpresaCS";

    /// <summary>
    /// El IDEmpresaCS de esta instalacion (1 en MacZ, la base de Zaragoza).
    /// </summary>
    public static async Task<int> ResolverAsync(
        ICapacidadesEmpresaRepository constantes,
        IMemoryCache cache,
        CancellationToken ct)
    {
        if (cache.TryGetValue(ClaveDeCache, out int cacheado)) return cacheado;

        // idUsuario = 0: aqui no interesa la fecha de pagos del usuario, solo la
        // fila de la empresa. Con 0 el lector ni consulta [Usuarios Fechas].
        var fila = await constantes.ConsultarAsync(0, ct);
        var idEmpresaCs = Math.Max(fila.IdEmpresaCS, 0);

        cache.Set(ClaveDeCache, idEmpresaCs, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheTtl
        });

        return idEmpresaCs;
    }
}
