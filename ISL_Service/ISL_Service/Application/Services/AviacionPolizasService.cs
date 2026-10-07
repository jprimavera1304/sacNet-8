using ISL_Service.Application.DTOs.Aviacion;
using ISL_Service.Application.DTOs.EscriturasLegacy;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Application.Services;

/// <summary>Reglas de las polizas de cheques de aviacion.</summary>
public class AviacionPolizasService : IAviacionPolizasService
{
    private readonly IAviacionPolizasRepository _repo;

    public AviacionPolizasService(IAviacionPolizasRepository repo)
    {
        _repo = repo;
    }

    public Task<List<Dictionary<string, object?>>> ConsultarAsync(AviacionPolizasFiltro filtro, CancellationToken ct = default)
    {
        if (filtro.FechaFinal is not null && filtro.FechaInicial is null)
            throw new ArgumentException("fechaFinal necesita fechaInicial.");
        if (filtro.FechaInicial is not null && filtro.FechaFinal is not null && filtro.FechaFinal < filtro.FechaInicial)
            throw new ArgumentException("fechaFinal no puede ser anterior a fechaInicial.");

        ExigirRangoCoherente(filtro.NumChequeInicial, filtro.NumChequeFinal, "numCheque");
        ExigirRangoCoherente(filtro.NumPolizaInicial, filtro.NumPolizaFinal, "numPoliza");

        return _repo.ConsultarAsync(filtro, ct);
    }

    public Task<List<Dictionary<string, object?>>> ConsultarDetalleAsync(int idPoliza, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(idPoliza, "idPoliza");
        return _repo.ConsultarDetalleAsync(idPoliza, ct);
    }

    public Task<List<Dictionary<string, object?>>> ConsultarChequesAsync(int idPoliza, CancellationToken ct = default)
        => _repo.ConsultarChequesAsync(idPoliza, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarEncabezadosAsync(int idPolizaEncabezado, int idStatus, CancellationToken ct = default)
        => _repo.ConsultarEncabezadosAsync(idPolizaEncabezado, idStatus, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarIngresosAsync(AviacionPolizaReporteFiltro filtro, CancellationToken ct = default)
        => _repo.ConsultarIngresosAsync(ArmarParametros(filtro), ct);

    public Task<List<Dictionary<string, object?>>> ConsultarFianzasAsync(AviacionPolizaReporteFiltro filtro, CancellationToken ct = default)
        => _repo.ConsultarFianzasAsync(ArmarParametros(filtro), ct);

    public Task<List<Dictionary<string, object?>>> ConsultarDocumentosAsync(AviacionPolizaReporteFiltro filtro, CancellationToken ct = default)
        => _repo.ConsultarDocumentosAsync(ArmarParametros(filtro), ct);

    /*
      LA CADENA @Params LA ARMA EL SERVIDOR, NO EL CLIENTE.

      Legacy reenviaba al SP la cadena que el cliente hubiera concatenado, sin
      mirarla. Dos cosas podian pasar y ninguna daba error: que un valor trajera
      el propio delimitador "~" y partiera el reporte en parametros distintos de
      los que se pidieron, y que un numero decimal llegara con coma porque el
      cliente usaba configuracion regional espanola.

      Aqui se recibe la lista, se comprueba que ningun elemento traiga el
      delimitador, y se pega con "~". El SP sigue recibiendo exactamente la misma
      forma de cadena.
    */
    private static string ArmarParametros(AviacionPolizaReporteFiltro filtro)
    {
        if (filtro.Parametros.Count == 0)
            throw new ArgumentException("parametros tiene que traer al menos un valor.");

        foreach (var valor in filtro.Parametros)
        {
            if (valor is not null && valor.Contains(LegacySp.Delimitador, StringComparison.Ordinal))
                throw new ArgumentException($"Ningun parametro puede contener el delimitador \"{LegacySp.Delimitador}\".");
        }

        return string.Join(LegacySp.Delimitador, filtro.Parametros.Select(v => (v ?? string.Empty).Trim()));
    }

    public async Task<ResultadoEscrituraDto> CrearAsync(CrearAviacionPolizaRequest r, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(r.IdPolizaEncabezado, "idPolizaEncabezado");

        if (r.FechaInicial is null) throw new ArgumentException("fechaInicial es requerida.");
        if (r.FechaFinal is null) throw new ArgumentException("fechaFinal es requerida.");
        if (r.FechaFinal < r.FechaInicial)
            throw new ArgumentException("fechaFinal no puede ser anterior a fechaInicial.");

        if (r.NumChequeInicial <= 0) throw new ArgumentException("numChequeInicial es requerido.");
        if (r.NumPolizaInicial <= 0) throw new ArgumentException("numPolizaInicial es requerido.");

        ExigirPorcentaje(r.ChequePorcentaje);
        ExigirDiaDelMes(r.DiaCheques);

        /*
          LA FECHA DE LOS CHEQUES: O UNA FECHA O UN DIA DEL MES, NO LAS DOS.

          El SP recibe @FechaCheques y @DiaCheques y usa uno o el otro segun cual
          venga. Mandar los dos deja que el procedimiento decida cual gana, y la
          fecha que acabe impresa en el cheque depende del orden de sus IF y no de
          lo que quiso el usuario. Un cheque con fecha equivocada no lo paga el
          banco.
        */
        if (r.FechaCheques is not null && r.DiaCheques != 0)
            throw new ArgumentException("Manda fechaCheques o diaCheques, no los dos.");

        return EscrituraLegacyReglas.Traducir(
            await _repo.InsertarAsync(r, idUsuario, equipo, ct),
            "generar la poliza");
    }

    public async Task<ResultadoEscrituraDto> CancelarAsync(int idPoliza, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(idPoliza, "idPoliza");
        return EscrituraLegacyReglas.Traducir(
            await _repo.CancelarAsync(idPoliza, idUsuario, equipo, ct),
            "cancelar la poliza");
    }

    public async Task<ResultadoEscrituraDto> ActualizarEncabezadoAsync(ActualizarAviacionPolizaEncabezadoRequest r, int idUsuario, string equipo, CancellationToken ct = default)
    {
        ExigirPorcentaje(r.ChequePorcentaje);
        return EscrituraLegacyReglas.Traducir(
            await _repo.ActualizarEncabezadoAsync(r, idUsuario, equipo, ct),
            "guardar el encabezado de poliza");
    }

    public async Task<ResultadoEscrituraDto> ActualizarChequeAsync(int idPolizaCheque, ActualizarAviacionPolizaChequeRequest r, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(idPolizaCheque, "idPolizaCheque");
        EscrituraLegacyReglas.ExigirId(r.IdPoliza, "idPoliza");

        /*
          EL NUMERO DE POLIZA SE EXIGE, Y EN LEGACY NO.

          PUT /api/polizas/cheques validaba `NumCheque == 0` DOS VECES
          (PolizasController.cs:131, el mismo campo repetido) y nunca NumPoliza.
          Con NumPoliza en 0 el SP escribe un 0 en el renglon: el cheque se queda
          sin numero de poliza y no se puede amarrar con la contabilidad.
        */
        if (r.NumCheque <= 0) throw new ArgumentException("numCheque es requerido.");
        if (r.NumPoliza <= 0) throw new ArgumentException("numPoliza es requerido.");

        return EscrituraLegacyReglas.Traducir(
            await _repo.ActualizarChequeAsync(idPolizaCheque, r, idUsuario, equipo, ct),
            "corregir el cheque de la poliza");
    }

    private static void ExigirPorcentaje(decimal valor)
    {
        if (valor < 0 || valor > 100)
            throw new ArgumentException("chequePorcentaje tiene que estar entre 0 y 100.");
    }

    private static void ExigirDiaDelMes(int dia)
    {
        if (dia < 0 || dia > 31)
            throw new ArgumentException("diaCheques tiene que estar entre 1 y 31, o 0 para no usarlo.");
    }

    private static void ExigirRangoCoherente(int desde, int hasta, string nombre)
    {
        if (hasta != 0 && desde == 0)
            throw new ArgumentException($"{nombre}Final necesita {nombre}Inicial.");
        if (desde != 0 && hasta != 0 && hasta < desde)
            throw new ArgumentException($"{nombre}Final no puede ser menor que {nombre}Inicial.");
    }
}
