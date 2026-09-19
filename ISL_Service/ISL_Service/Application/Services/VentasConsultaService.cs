using ISL_Service.Application.DTOs.VentasConsulta;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;

namespace ISL_Service.Application.Services;

public class VentasConsultaService : IVentasConsultaService
{
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
      la misma persona puede pedir dos reportes a la vez desde dos pestanas, y
      el procedimiento recupera el id con un MAX() por equipo y usuario: con el
      nombre repetido, las dos pestanas se llevarian el mismo reporte.
    */
    /*
      EL PASE PARA VER EL REPORTE

      Aqui no se genera nada ni se toca la base: solo se firma un pase de pocos
      minutos con las ventas y el usuario. El PDF se arma cuando el navegador
      va por el, en RemisionImpresionService.

      Por que un pase y no la lista de ventas en la direccion: porque entonces
      cualquiera podria cambiar el numero y leer la remision de otro cliente. El
      GET que entrega el PDF es anonimo —una pestana nueva no manda el token— y
      lo unico que lo autoriza es esta firma.

      El id de usuario sale del TOKEN, no del cuerpo de la peticion. En Mac31 lo
      manda el cliente porque el cliente es de confianza; aqui no lo es, y ese
      id es el que queda escrito como quien imprimio la remision.
    */
    public string CrearTicketReporte(VentasReporteRequest request, int idUsuarioToken)
    {
        var ids = (request?.IdsVenta ?? new List<int>()).Where(x => x > 0).Distinct().ToList();
        if (ids.Count == 0)
            throw new InvalidOperationException("Selecciona una remisión.");

        var llave = _configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(llave))
            throw new InvalidOperationException("Falta Jwt:Key; sin llave no se puede firmar el acceso al reporte.");

        return RemisionTicket.Firmar(llave, ids, idUsuarioToken, request!.Descargar);
    }
}
