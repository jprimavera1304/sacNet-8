using ISL_Service.Application.DTOs.Aviacion;
using ISL_Service.Application.DTOs.EscriturasLegacy;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/// <summary>Reglas del cobro de caja de aviacion.</summary>
public class AviacionRecaudacionesService : IAviacionRecaudacionesService
{
    private readonly IAviacionRecaudacionesRepository _repo;

    public AviacionRecaudacionesService(IAviacionRecaudacionesRepository repo)
    {
        _repo = repo;
    }

    public Task<List<Dictionary<string, object?>>> ConsultarAsync(AviacionRecaudacionesFiltro f, CancellationToken ct = default)
    {
        /*
          TODOS LOS FILTROS SE APLICAN JUNTOS.

          Aqui esta lo que NO se hereda: Legacy, al recibir un IDRecaudacion o una
          lista, reconstruia el DTO y tiraba el resto; y en el caso normal
          —IDRecaudacion con la lista vacia— tambien perdia el propio id, asi que
          pedir una recaudacion devolvia TODAS. El detalle esta en el bloque de
          AviacionRecaudacionesDtos.
        */
        ExigirRangoDeFechas(f.FechaInicial, f.FechaFinal);
        ExigirListaLimpia(f.IdsRecaudacion, "idsRecaudacion");
        ExigirListaLimpia(f.IdsCaja, "idsCaja");
        ExigirListaLimpia(f.IdsConcepto, "idsConcepto");
        ExigirListaLimpia(f.IdsCuenta, "idsCuenta");
        ExigirListaLimpia(f.IdsEconomico, "idsEconomico");
        ExigirListaLimpia(f.IdsPermisionario, "idsPermisionario");
        ExigirListaLimpia(f.IdsOperador, "idsOperador");

        ExigirRango(f.NumCajaInicial, f.NumCajaFinal, "numCaja");
        ExigirRango(f.NumConceptoInicial, f.NumConceptoFinal, "numConcepto");
        ExigirRango(f.NumCuentaInicial, f.NumCuentaFinal, "numCuenta");
        ExigirRango(f.NumEconomicoInicial, f.NumEconomicoFinal, "numEconomico");
        ExigirRango(f.NumPermisionarioInicial, f.NumPermisionarioFinal, "numPermisionario");
        ExigirRango(f.NumNominaInicial, f.NumNominaFinal, "numNomina");
        ExigirRango(f.FolioInicial, f.FolioFinal, "folio");

        return _repo.ConsultarAsync(f, ct);
    }

    public Task<List<Dictionary<string, object?>>> ConsultarRecurrentesAsync(AviacionRecaudacionesRecurrentesFiltro f, CancellationToken ct = default)
    {
        ExigirRangoDeFechas(f.FechaInicial, f.FechaFinal);
        ExigirListaLimpia(f.IdsCuenta, "idsCuenta");
        ExigirListaLimpia(f.IdsEconomico, "idsEconomico");
        ExigirListaLimpia(f.IdsPermisionario, "idsPermisionario");
        ExigirListaLimpia(f.IdsOperador, "idsOperador");
        return _repo.ConsultarRecurrentesAsync(f, ct);
    }

    public Task<List<Dictionary<string, object?>>> ConsultarCortesAsync(AviacionRecaudacionesCortesFiltro f, CancellationToken ct = default)
        => _repo.ConsultarCortesAsync(f, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarSiguienteFolioAsync(int idCaja, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(idCaja, "idCaja");
        return _repo.ConsultarSiguienteFolioAsync(idCaja, ct);
    }

    public async Task<ResultadoEscrituraDto> CrearAsync(CrearAviacionRecaudacionRequest r, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(r.IdEconomico, "idEconomico");
        EscrituraLegacyReglas.ExigirId(r.IdCaja, "idCaja");

        if (r.IdsCuenta.Count == 0)
            throw new ArgumentException("idsCuenta tiene que traer al menos una cuenta.");

        if (r.ImportesCuenta.Count != r.IdsCuenta.Count)
            throw new ArgumentException($"importesCuenta tiene {r.ImportesCuenta.Count} elementos y idsCuenta tiene {r.IdsCuenta.Count}: tienen que ser iguales.");

        ExigirListaLimpia(r.IdsCuenta, "idsCuenta");

        if (r.ImportesCuenta.Any(x => x < 0))
            throw new ArgumentException("Ningun importe de cuenta puede ser negativo.");

        /*
          EL TOTAL TIENE QUE SER LA SUMA, Y numCuentas EL NUMERO DE CUENTAS.

          El SP guarda los tres numeros tal como llegan —@NumCuentas,
          @ImporteCuentas y @ImporteTotal— y no recalcula ninguno a partir de las
          listas. Legacy los pasaba tal cual desde el cliente. Si el cliente
          calcula mal o manda una cuenta mas de las que suma, el cobro queda con
          un total que no es el de sus renglones: el corte del dia no cuadra y la
          diferencia se le carga al cajero.
        */
        var suma = r.ImportesCuenta.Sum();
        if (Math.Abs(suma - r.ImporteCuentas) > 0.01m)
            throw new ArgumentException($"importeCuentas ({r.ImporteCuentas}) no cuadra con la suma de los importes ({suma}).");

        if (r.NumCuentas != 0 && r.NumCuentas != r.IdsCuenta.Count)
            throw new ArgumentException($"numCuentas dice {r.NumCuentas} y la lista trae {r.IdsCuenta.Count} cuentas.");

        if (r.ImporteTotal < 0)
            throw new ArgumentException("El importe total no puede ser negativo.");

        if (r.MontoTransferencia < 0)
            throw new ArgumentException("El monto por transferencia no puede ser negativo.");

        if (r.MontoTransferencia > r.ImporteTotal)
            throw new ArgumentException("El monto por transferencia no puede pasar del importe total.");

        /*
          UN REVERSO NO ES LAS DOS COSAS A LA VEZ.

          @IDRecaudacionOriginal dice "este cobro corrige aquel" y
          @IDRecaudacionReverso dice "aquel corrige a este". Mandar los dos deja un
          renglon que es a la vez el original y el reverso de algo, y el reporte de
          cancelaciones lo cuenta dos veces.
        */
        if (r.IdRecaudacionOriginal > 0 && r.IdRecaudacionReverso > 0)
            throw new ArgumentException("Un cobro no puede ser a la vez el reverso de otro y el reversado.");

        // numCuentas se completa si no vino: es informacion que ya esta en la
        // lista y pedirsela al cliente solo da oportunidad de equivocarse.
        if (r.NumCuentas == 0) r.NumCuentas = r.IdsCuenta.Count;

        return EscrituraLegacyReglas.Traducir(
            await _repo.InsertarAsync(r, idUsuario, equipo, ct),
            "registrar el cobro");
    }

    public async Task<ResultadoEscrituraDto> CancelarAsync(int idRecaudacion, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(idRecaudacion, "idRecaudacion");
        return EscrituraLegacyReglas.Traducir(
            await _repo.CancelarAsync(idRecaudacion, idUsuario, equipo, ct),
            "cancelar el cobro");
    }

    public async Task<ResultadoEscrituraDto> CrearCorteAsync(CrearAviacionRecaudacionCorteRequest r, int idUsuario, string equipo, CancellationToken ct = default)
    {
        if (r.FechaCorte is null) throw new ArgumentException("fechaCorte es requerida.");

        /*
          NO SE CIERRA UN DIA QUE NO HA PASADO.

          El corte cierra la recaudacion de un dia. Con una fecha futura se cierra
          un dia en el que todavia se va a cobrar, y lo que se cobre despues queda
          fuera del corte sin que nada avise. Legacy aceptaba cualquier fecha.
        */
        if (r.FechaCorte > DateOnly.FromDateTime(DateTime.Today))
            throw new ArgumentException("fechaCorte no puede ser futura.");

        return EscrituraLegacyReglas.Traducir(
            await _repo.InsertarCorteAsync(r, idUsuario, equipo, ct),
            "cerrar el dia de recaudacion");
    }

    private static void ExigirRangoDeFechas(DateOnly? desde, DateOnly? hasta)
    {
        if (hasta is not null && desde is null)
            throw new ArgumentException("fechaFinal necesita fechaInicial.");
        if (desde is not null && hasta is not null && hasta < desde)
            throw new ArgumentException("fechaFinal no puede ser anterior a fechaInicial.");
    }

    private static void ExigirRango(int desde, int hasta, string nombre)
    {
        if (hasta != 0 && desde == 0)
            throw new ArgumentException($"{nombre}Final necesita {nombre}Inicial.");
        if (desde != 0 && hasta != 0 && hasta < desde)
            throw new ArgumentException($"{nombre}Final no puede ser menor que {nombre}Inicial.");
    }

    /*
      EL CERO NO VIAJA EN LAS LISTAS DE IDS.

      Estos SP arman el WHERE con la lista pegada y la parten dentro; un 0 en la
      lista queda como `IDCuenta IN (0)` y devuelve cero renglones, o peor, se
      cuela en un IN junto a ids buenos y no cambia nada visible. Es el mismo
      defecto que la ola 3 encontro en la lista de marcas de productos.
    */
    private static void ExigirListaLimpia(List<int> lista, string nombre)
    {
        if (lista.Count == 0) return;
        if (lista.Any(x => x <= 0))
            throw new ArgumentException($"{nombre} no puede traer ceros ni negativos.");
        if (lista.Distinct().Count() != lista.Count)
            throw new ArgumentException($"{nombre} trae el mismo id dos veces.");
    }
}
