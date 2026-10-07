using ISL_Service.Application.DTOs.Cobranzas;
using ISL_Service.Application.DTOs.EscriturasLegacy;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/// <summary>Reglas de las cobranzas.</summary>
public class CobranzasService : ICobranzasService
{
    private readonly ICobranzasRepository _repo;

    public CobranzasService(ICobranzasRepository repo)
    {
        _repo = repo;
    }

    public Task<List<Dictionary<string, object?>>> ConsultarAsync(CobranzasFiltro f, int idUsuarioActual, CancellationToken ct = default)
    {
        ExigirRangoDeFechas(f.FechaInicialCobranza, f.FechaFinalCobranza);
        ExigirRango(f.FolioInicialCobranza, f.FolioFinalCobranza, "folioCobranza");
        ExigirRango(f.FolioInicialVenta, f.FolioFinalVenta, "folioVenta");

        /*
          BUSCAR POR FOLIO DE VENTA NECESITA LA EMPRESA.

          El folio de venta se repite entre empresas: el SP compara
          IDEmpresa + Folio. Sin empresa, el filtro trae las ventas con ese folio de
          todas las empresas. Legacy no lo exigia en la consulta (si en la
          validacion de folio, que es el mismo dato).
        */
        if (f.FolioInicialVenta != 0 && f.IdEmpresaVenta == 0)
            throw new ArgumentException("Buscar por folio de venta necesita idEmpresaVenta.");

        return _repo.ConsultarAsync(f, idUsuarioActual, ct);
    }

    public Task<List<Dictionary<string, object?>>> ConsultarDetalleAsync(CobranzaDetalleFiltro f, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(f.IdCobranza, "idCobranza");
        return _repo.ConsultarDetalleAsync(f, ct);
    }

    public Task<List<Dictionary<string, object?>>> ValidarFolioAsync(CobranzaValidarFolioFiltro f, int idUsuarioActual, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(f.IdEmpresaVenta, "idEmpresaVenta");
        EscrituraLegacyReglas.ExigirId(f.FolioVenta, "folioVenta");
        return _repo.ValidarFolioAsync(f, idUsuarioActual, ct);
    }

    public async Task<List<Dictionary<string, object?>>> ConsultarPendientesVencidosAsync(CobranzaPendientesVencidosFiltro f, CancellationToken ct = default)
    {
        if (f.FechaInicial is null) throw new ArgumentException("fechaInicial es requerida.");
        if (f.FechaFinal is null) throw new ArgumentException("fechaFinal es requerida.");
        if (f.FechaFinal < f.FechaInicial)
            throw new ArgumentException("fechaFinal no puede ser anterior a fechaInicial.");

        // La fecha de operacion la pone la base, no el cliente: ver el comentario
        // de CobranzasRepository.LeerFechaDeOperacionAsync.
        var fechaOperacion = await _repo.LeerFechaDeOperacionAsync(ct);
        return await _repo.ConsultarPendientesVencidosAsync(f, fechaOperacion, ct);
    }

    public async Task<ResultadoEscrituraDto> CrearAsync(CrearCobranzaRequest r, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(r.IdRepartidor, "idRepartidor");
        ExigirLista(r.IdsVenta, "idsVenta", obligatoria: true);

        /*
          SI LA COBRANZA ES PARTE DE UN COBRO, EL COBRO TIENE QUE VENIR.

          El SP recibe @esCobro y @IDCobro por separado. Con esCobro = 1 e IDCobro
          en 0, la cobranza se marca como parte de un cobro que no existe, y el
          cobro se queda sin su cobranza: dos renglones que se buscan y no se
          encuentran. Legacy mandaba los dos tal como vinieran.
        */
        if (r.EsCobro && r.IdCobro <= 0)
            throw new ArgumentException("Si la cobranza es parte de un cobro, idCobro es requerido.");

        if (!r.EsCobro && r.IdCobro > 0)
            throw new ArgumentException("idCobro solo aplica cuando esCobro es true.");

        return EscrituraLegacyReglas.Traducir(
            await _repo.InsertarAsync(r, idUsuario, equipo, ct),
            "armar la cobranza");
    }

    public async Task<ResultadoEscrituraDto> ActualizarDetalleAsync(int idCobranza, ActualizarCobranzaDetalleRequest r, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(idCobranza, "idCobranza");
        EscrituraLegacyReglas.ExigirId(r.IdRepartidor, "idRepartidor");

        ExigirLista(r.IdsDetalleEliminar, "idsDetalleEliminar", obligatoria: false);
        ExigirLista(r.IdsDetalleDevolver, "idsDetalleDevolver", obligatoria: false);
        ExigirLista(r.IdsVentaAgregar, "idsVentaAgregar", obligatoria: false);

        /*
          UNA LLAMADA QUE NO CAMBIA NADA NO SE MANDA.

          Las tres listas vacias significan "no quites, no devuelvas y no agregues
          nada": el SP corre su transaccion entera, reescribe el repartidor y las
          observaciones y deja la bitacora de modificacion, para nada. Legacy lo
          permitia y era un camino facil de pisar el repartidor de una cobranza sin
          querer.
        */
        if (r.IdsDetalleEliminar.Count == 0 && r.IdsDetalleDevolver.Count == 0 && r.IdsVentaAgregar.Count == 0)
            throw new ArgumentException("Hay que quitar, devolver o agregar al menos un renglon.");

        /*
          UN RENGLON NO SE QUITA Y SE DEVUELVE A LA VEZ.

          Son dos destinos distintos: quitar lo saca de la cobranza y devolver lo
          marca como devuelto por el repartidor. El SP procesa las dos listas una
          tras otra, asi que el mismo renglon en las dos acaba en el estado de la
          segunda, y cual es la segunda es un detalle del procedimiento.
        */
        var enLasDos = r.IdsDetalleEliminar.Intersect(r.IdsDetalleDevolver).ToList();
        if (enLasDos.Count > 0)
            throw new ArgumentException($"El renglon {enLasDos[0]} viene para eliminar y para devolver a la vez.");

        return EscrituraLegacyReglas.Traducir(
            await _repo.ActualizarDetalleAsync(idCobranza, r, idUsuario, equipo, ct),
            "actualizar la cobranza");
    }

    public async Task<ResultadoEscrituraDto> CancelarAsync(CancelarCobranzasRequest r, int idUsuario, string equipo, CancellationToken ct = default)
    {
        ExigirLista(r.IdsCobranza, "idsCobranza", obligatoria: true);
        ExigirLista(r.IdsCobro, "idsCobro", obligatoria: false);

        return EscrituraLegacyReglas.Traducir(
            await _repo.CancelarAsync(r, idUsuario, equipo, ct),
            "cancelar las cobranzas");
    }

    /// <summary>
    /// Sin ceros, sin negativos y sin repetidos: estos SP pegan la lista y la
    /// parten dentro, y un 0 se cuela en el IN sin cambiar nada visible.
    /// </summary>
    private static void ExigirLista(List<int> lista, string nombre, bool obligatoria)
    {
        if (lista.Count == 0)
        {
            if (obligatoria) throw new ArgumentException($"{nombre} tiene que traer al menos un elemento.");
            return;
        }

        if (lista.Any(x => x <= 0))
            throw new ArgumentException($"{nombre} no puede traer ceros ni negativos.");

        if (lista.Distinct().Count() != lista.Count)
            throw new ArgumentException($"{nombre} trae el mismo id dos veces.");
    }

    private static void ExigirRangoDeFechas(DateOnly? desde, DateOnly? hasta)
    {
        if (hasta is not null && desde is null)
            throw new ArgumentException("La fecha final necesita la fecha inicial.");
        if (desde is not null && hasta is not null && hasta < desde)
            throw new ArgumentException("La fecha final no puede ser anterior a la inicial.");
    }

    private static void ExigirRango(int desde, int hasta, string nombre)
    {
        if (hasta != 0 && desde == 0)
            throw new ArgumentException($"{nombre}Final necesita {nombre}Inicial.");
        if (desde != 0 && hasta != 0 && hasta < desde)
            throw new ArgumentException($"{nombre}Final no puede ser menor que {nombre}Inicial.");
    }
}
