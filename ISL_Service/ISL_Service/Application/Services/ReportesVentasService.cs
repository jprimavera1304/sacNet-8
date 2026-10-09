using ISL_Service.Application.DTOs.Reportes;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

public class ReportesVentasService : IReportesVentasService
{
    private readonly IReportesVentasRepository _repository;

    public ReportesVentasService(IReportesVentasRepository repository)
    {
        _repository = repository;
    }

    public Task<ReportesVentasCatalogosResponse> ConsultarCatalogosAcumuladoresProductosAsync(
        int? idGrupoCategoria,
        IReadOnlyCollection<int>? idCategorias,
        CancellationToken ct = default)
    {
        return _repository.ConsultarCatalogosAcumuladoresProductosAsync(idGrupoCategoria, idCategorias, ct);
    }

    public Task<List<ReportesVentasClienteItem>> ConsultarClientesAsync(
        int? numero,
        int? idCliente,
        CancellationToken ct = default)
    {
        if (numero.GetValueOrDefault() <= 0 && idCliente.GetValueOrDefault() <= 0)
            throw new ArgumentException("numero o idCliente requerido.");

        return _repository.ConsultarClientesAsync(numero, idCliente, ct);
    }

    public Task<ReportesVentasCatalogosResponse> ConsultarCatalogosRemisionesAsync(CancellationToken ct = default)
    {
        return _repository.ConsultarCatalogosRemisionesAsync(ct);
    }

    public Task<ReportesVentasGenerateResponse> GenerarAcumuladoresProductosAsync(
        ReportesVentasAcumuladoresProductosRequest request,
        CancellationToken ct = default)
    {
        Validate(request);
        return _repository.GenerarAcumuladoresProductosAsync(request, ct);
    }

    public Task<ReportesVentasGenerateResponse> GenerarRemisionesAsync(
        ReportesVentasRemisionesRequest request,
        CancellationToken ct = default)
    {
        Validate(request);
        ValidateUsuarios(request);

        return _repository.GenerarRemisionesAsync(request, ct);
    }

    public Task<ReportesVentasGenerateResponse> GenerarFoliosAsync(
        ReportesVentasFoliosRequest request,
        CancellationToken ct = default)
    {
        Validate(request);
        ValidateUsuarios(request);

        return _repository.GenerarFoliosAsync(request, ct);
    }

    public Task<ReportesVentasGenerateResponse> GenerarFacturasAsync(
        ReportesVentasFacturasRequest request,
        CancellationToken ct = default)
    {
        Validate(request);
        ValidateUsuarios(request);

        return _repository.GenerarFacturasAsync(request, ct);
    }

    public Task<ReportesVentasGenerateResponse> GenerarConcentradosAsync(
        ReportesVentasConcentradosRequest request,
        CancellationToken ct = default)
    {
        Validate(request);
        if ((request.IDRepartidores ?? new List<int>()).Where(id => id > 0).Distinct().Count() == 0)
            throw new ArgumentException("seleccione al menos un repartidor.");

        return _repository.GenerarConcentradosAsync(request, ct);
    }

    public Task<ReportesVentasGenerateResponse> GenerarCobranzaAsync(
        ReportesVentasCobranzaRequest request,
        CancellationToken ct = default)
    {
        Validate(request);
        return _repository.GenerarCobranzaAsync(request, ct);
    }

    /*
      LOS REPORTES QUE NO SON DE UN PERIODO.

      La validacion comun exige las dos fechas, y para cincuenta y dos reportes
      eso es correcto: son listados de un rango. La hoja de cobro NO: imprime
      LAS HOJAS MARCADAS en la rejilla, igual que IMPRIMIR COBRO de Mac31, y sus
      procedimientos no reciben fechas (Param2/3/4 son los tres juegos de ids).

      Se exime en vez de pedirle al front que mande dos fechas de adorno. Una
      fecha obligatoria que no filtra nada es la clase de dato que alguien acaba
      usando: el dia que se toque el reporte, el rango estaria ahi pareciendo
      significar algo.

      Es una LISTA y no un `if` suelto para que el siguiente que llegue se
      agregue aqui, con su porque al lado, en vez de volver a eximirse por otro
      camino.

      Y EL SIGUIENTE LLEGO: `concentrado`, el documento del repartidor (1005).

      Es el mismo caso exacto que la hoja de cobro -imprime LOS CONCENTRADOS
      MARCADOS en la rejilla, y `BuildConcentradoLegacyParams` solo usa Param2,
      la lista de IDConcentrado- y se habia quedado fuera de esta lista cuando se
      implemento su armador. El efecto era que el reporte estaba COMPLETO y
      MUERTO a la vez: catalogo, parametros, plantillas y
      `ConcentradoHtmlBuilder` listos, y la peticion rebotaba aqui con
      "fechaInicial es requerida." antes de llegar a ninguno. Medido al abrir el
      boton IMPRIMIR de la pantalla de concentrados: 400 contra
      /api/reportes/ventas/legacy/generar.
    */
    private static readonly string[] ReportesSinPeriodo = { "hoja_de_cobro", "concentrado" };

    public Task<ReportesVentasGenerateResponse> GenerarLegacyVentasAsync(
        ReportesVentasLegacyRequest request,
        CancellationToken ct = default)
    {
        if (request is null)
            throw new ArgumentException("Body requerido.");

        var clave = (request.ReporteKey ?? "").Trim().ToLowerInvariant();
        Validate(request, exigirFechas: !ReportesSinPeriodo.Contains(clave));

        if (string.IsNullOrWhiteSpace(request.ReporteKey) && request.IDReporte <= 0)
            throw new ArgumentException("reporteKey o idReporte requerido.");

        return _repository.GenerarLegacyVentasAsync(request, ct);
    }

    public Task<ReportesVentasPreviewResponse> ConsultarAcumuladoresProductosAsync(
        ReportesVentasAcumuladoresProductosRequest request,
        CancellationToken ct = default)
    {
        Validate(request);
        return _repository.ConsultarAcumuladoresProductosAsync(request, ct);
    }

    public Task<ReportesVentasPreviewResponse> ConsultarAcumuladoresProductosPorParametrosAsync(
        int parametrosLegacy,
        CancellationToken ct = default)
    {
        if (parametrosLegacy <= 0)
            throw new ArgumentException("psp requerido.");

        return _repository.ConsultarAcumuladoresProductosPorParametrosAsync(parametrosLegacy, ct);
    }

    public Task<ReportesVentasFileResponse> GenerarAcumuladoresProductosExcelPorParametrosAsync(
        int parametrosLegacy,
        CancellationToken ct = default)
    {
        if (parametrosLegacy <= 0)
            throw new ArgumentException("psp requerido.");

        return _repository.GenerarAcumuladoresProductosExcelPorParametrosAsync(parametrosLegacy, ct);
    }

    public Task<ReportesVentasPreviewResponse> ConsultarReporteVentasPorParametrosAsync(
        int parametrosLegacy,
        int idCentro = 0,
        CancellationToken ct = default)
    {
        if (parametrosLegacy <= 0)
            throw new ArgumentException("psp requerido.");

        return _repository.ConsultarReporteVentasPorParametrosAsync(parametrosLegacy, idCentro, ct);
    }

    public Task<ReportesVentasFileResponse> GenerarReporteVentasExcelPorParametrosAsync(
        int parametrosLegacy,
        int idCentro = 0,
        CancellationToken ct = default)
    {
        if (parametrosLegacy <= 0)
            throw new ArgumentException("psp requerido.");

        return _repository.GenerarReporteVentasExcelPorParametrosAsync(parametrosLegacy, idCentro, ct);
    }

    /*
      `exigirFechas` por omision en true: los cincuenta y dos reportes de periodo
      siguen validandose igual que siempre y ninguna de sus llamadas cambia. Ver
      ReportesSinPeriodo.
    */
    private static void Validate(
        ReportesVentasAcumuladoresProductosRequest request,
        bool exigirFechas = true)
    {
        if (request is null)
            throw new ArgumentException("Body requerido.");
        if (exigirFechas)
        {
            if (request.FechaInicial == default)
                throw new ArgumentException("fechaInicial es requerida.");
            if (request.FechaFinal == default)
                throw new ArgumentException("fechaFinal es requerida.");
            if (request.FechaFinal < request.FechaInicial)
                throw new ArgumentException("fechaFinal no puede ser menor a fechaInicial.");
        }

        var salida = (request.Salida ?? "pantalla").Trim().ToLowerInvariant();
        if (salida != "pantalla" && salida != "excel")
            throw new ArgumentException("salida debe ser 'pantalla' o 'excel'.");

        var tipoReporte = (request.TipoReporte ?? "empresa").Trim().ToLowerInvariant();
        if (tipoReporte == "agente" && (request.IDAgentes ?? new List<int>()).Where(id => id > 0).Distinct().Count() == 0)
            throw new ArgumentException("seleccione al menos un agente.");
        if (tipoReporte == "cliente" && (request.IDClientes ?? new List<int>()).Where(id => id > 0).Distinct().Count() == 0)
            throw new ArgumentException("seleccione el cliente.");
    }

    private static void ValidateUsuarios(ReportesVentasRemisionesRequest request)
    {
        if ((request.IDUsuarios ?? new List<int>()).Where(id => id > 0).Distinct().Count() == 0)
            throw new ArgumentException("seleccione al menos un usuario.");
    }
}
