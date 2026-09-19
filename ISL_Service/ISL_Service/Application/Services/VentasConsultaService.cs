using ISL_Service.Application.DTOs.VentasConsulta;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

public class VentasConsultaService : IVentasConsultaService
{
    /*
      El separador de la lista de ventas es "~", igual que
      Globales.DELIMITADOR_PARAM_TILDE_SP en Mac31. Si aqui se pusiera una coma,
      la pagina de reportes leeria una sola venta con un texto raro adentro.
    */
    private const string SeparadorIds = "~";

    private readonly IVentasConsultaRepository _repository;
    private readonly IConfiguration _configuration;

    public VentasConsultaService(IVentasConsultaRepository repository, IConfiguration configuration)
    {
        _repository = repository;
        _configuration = configuration;
    }

    public Task<VentasConsultaCatalogosResponse> ConsultarCatalogosAsync(CancellationToken ct)
    {
        return _repository.ConsultarCatalogosAsync(ct);
    }

    public Task<VentasConsultaRowsResponse> ConsultarRemisionesAsync(VentasConsultaRequest request, int idUsuarioToken, CancellationToken ct)
    {
        var safe = Prepare(request, idUsuarioToken);
        return _repository.ConsultarRemisionesAsync(safe, ct);
    }

    public Task<VentasConsultaRowsResponse> ConsultarPedidosAsync(VentasConsultaRequest request, int idUsuarioToken, CancellationToken ct)
    {
        var safe = Prepare(request, idUsuarioToken);
        safe.Formato = 0;
        // "Solo mis pedidos" (app movil): filtra por el usuario del token, el
        // mismo que se guarda al crear el pedido -> garantiza que empaten.
        if (safe.SoloMisPedidos && idUsuarioToken > 0)
            safe.IDUsuario = idUsuarioToken;
        return _repository.ConsultarPedidosAsync(safe, ct);
    }

    public Task<VentasConsultaRowsResponse> ConsultarPendientesImprimirAsync(VentasConsultaRequest request, int idUsuarioToken, CancellationToken ct)
    {
        var safe = Prepare(request, idUsuarioToken);
        return _repository.ConsultarPendientesImprimirAsync(safe, ct);
    }

    public Task<VentasConsultaRowsResponse> ConsultarPagosAsync(VentasConsultaRequest request, int idUsuarioToken, CancellationToken ct)
    {
        var safe = Prepare(request, idUsuarioToken);
        return _repository.ConsultarPagosAsync(safe, ct);
    }

    private static VentasConsultaRequest Prepare(VentasConsultaRequest? request, int idUsuarioToken)
    {
        var safe = request ?? new VentasConsultaRequest();
        if (safe.IDUsuarioActual <= 0 && idUsuarioToken > 0)
            safe.IDUsuarioActual = idUsuarioToken;
        return safe;
    }

    /*
      PREPARAR EL REPORTE: DEJAR LOS PARAMETROS Y ARMAR LA DIRECCION

      Son los dos pasos que hace Mac31 en Funciones.VerVenta, ni uno mas:

        1. Guardar los parametros y quedarse con el id que devuelve.
        2. Abrir  <reportes>/Reportes?IDReporte=..&ids=..&idu=..&d=..&idd=..

      El reporte en si no se toca. Lo pinta MacReportes, la misma aplicacion que
      abre Mac31, asi que sale identico por construccion y no por parecido.

      EL NOMBRE DE EQUIPO ES UNICO POR LLAMADA. En Mac31 es el nombre de la
      maquina, que ahi identifica a un solo usuario sentado enfrente. En el web
      la misma persona puede pedir dos reportes a la vez desde dos pestañas, y
      el procedimiento recupera el id con un MAX() por equipo y usuario: con el
      nombre repetido, las dos pestañas se llevarian el mismo reporte.
    */
    public async Task<VentasReporteResponse> PrepararReporteAsync(
        VentasReporteRequest request,
        int idUsuarioToken,
        CancellationToken ct)
    {
        var ids = (request?.IdsVenta ?? new List<int>()).Where(x => x > 0).Distinct().ToList();
        if (ids.Count == 0)
            return new VentasReporteResponse { Ok = false, Message = "Selecciona una remisión." };

        var baseUrl = (_configuration["Reportes:BaseUrl"] ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return new VentasReporteResponse
            {
                Ok = false,
                Message = "Falta configurar Reportes:BaseUrl. Sin eso no se sabe a qué servidor de reportes ir."
            };
        }

        if (!baseUrl.EndsWith("/", StringComparison.Ordinal))
            baseUrl += "/";

        var nombreEquipo = $"WEB-{Guid.NewGuid():N}"[..20];
        var idReporte = request!.IdReporte > 0 ? request.IdReporte : 5;

        var idParametros = await _repository.RegistrarParametrosReporteAsync(
            nombreEquipo,
            idUsuarioToken,
            idReporte,
            string.Join(SeparadorIds, ids),
            ct);

        if (idParametros <= 0)
            return new VentasReporteResponse { Ok = false, Message = "No se pudieron guardar los parámetros del reporte." };

        var url = $"{baseUrl}Reportes?IDReporte={idReporte}&ids={idParametros}&idu={idUsuarioToken}" +
                  $"&d={(request.Descargar == 1 ? 1 : 0)}&idd=0";

        return new VentasReporteResponse { Ok = true, IdParametros = idParametros, Url = url };
    }
}
