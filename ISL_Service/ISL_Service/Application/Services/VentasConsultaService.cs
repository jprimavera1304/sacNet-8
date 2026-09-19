using ISL_Service.Application.DTOs.VentasConsulta;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;

namespace ISL_Service.Application.Services;

public class VentasConsultaService : IVentasConsultaService
{
    private readonly IVentasConsultaRepository _repository;
    private readonly IConfiguration _configuration;

    /*
      SE REUSAN DOS REPOSITORIOS DE AL LADO, DE SOLO LECTURA

      _cancelacion trae tres cosas que ya existen y que aqui hacian falta
      enteras: las Constantes (funcionalidad, EsCentroServicio y la fecha de
      operacion con la que se arma la contrasena), los datos crudos de cada
      venta —folio con prefijo y tipo de documento, que es con lo que legacy
      decide si un folio se puede imprimir— y la bitacora de intentos de
      contrasena.

      Copiar esas consultas aqui habria sido tener dos versiones de la misma
      regla; la de cancelaciones no se toca, solo se llama.
    */
    private readonly IVentasCancelacionRepository _cancelacion;
    private readonly IRemisionImpresionRepository _impresiones;

    public VentasConsultaService(
        IVentasConsultaRepository repository,
        IVentasCancelacionRepository cancelacion,
        IRemisionImpresionRepository impresiones,
        IConfiguration configuration)
    {
        _repository = repository;
        _cancelacion = cancelacion;
        _impresiones = impresiones;
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

    /// Sin parametros a proposito: btnPedidosFaltantes_Click no pide nada —ni
    /// fila seleccionada, ni fechas, ni filtros— y el procedimiento decide el
    /// dia por su cuenta. Ver ConsultarPedidosFaltantesAsync en el repositorio.
    public Task<List<VentasPedidoFaltanteItem>> ConsultarPedidosFaltantesAsync(CancellationToken ct)
        => _repository.ConsultarPedidosFaltantesAsync(ct);

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
    public async Task<string> CrearTicketReporteAsync(
        VentasReporteRequest request,
        int idUsuarioToken,
        string equipo,
        CancellationToken ct)
    {
        var req = request ?? new VentasReporteRequest();
        var ids = (req.IdsVenta ?? new List<int>()).Where(x => x > 0).Distinct().ToList();
        if (ids.Count == 0)
            throw new InvalidOperationException("Selecciona una remisión.");

        var llave = _configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(llave))
            throw new InvalidOperationException("Falta Jwt:Key; sin llave no se puede firmar el acceso al reporte.");

        var constantes = await _cancelacion.ConsultarConstantesAsync(ct);
        var accion = VentasImpresionReglas.Normalizar(req.Accion);

        /*
          SE VUELVE A VALIDAR TODO, AUNQUE EL PASO ANTERIOR YA LO HIZO

          El "preparar" de antes es para pintar los dialogos, no es la puerta.
          Entre uno y otro alguien pudo facturar, cancelar o imprimir la misma
          remision desde Mac31, y ademas nada obliga a que la pantalla pase por
          ahi: quien llame directo a esta direccion tiene que toparse con las
          mismas reglas.
        */
        var bloqueos = await BloqueosAsync(ids, accion, constantes.Funcionalidad, ct);
        var permitidas = ids.Where(id => bloqueos.All(b => b.IdVenta != id)).ToList();

        if (permitidas.Count == 0)
        {
            throw new ConflictException(bloqueos.Count > 0
                ? string.Join(" ", bloqueos.Select(b => $"{b.FolioFtm}: {b.Motivo}"))
                : "Selecciona una remisión.");
        }

        if (VentasImpresionReglas.RequiereContrasena(accion, req.Vista, constantes.Funcionalidad, constantes.EsCentroServicio))
            await ValidarContrasenaAsync(req.Contrasena, idUsuarioToken, equipo, constantes, ct);

        var (primerImpresion, reimpresion) = VentasImpresionReglas.Banderas(accion, constantes.Funcionalidad);

        return RemisionTicket.Firmar(llave, permitidas, idUsuarioToken, req.Descargar, primerImpresion, reimpresion, equipo);
    }

    /*
      LAS PREGUNTAS DE ANTES DE ABRIR LA PESTAÑA

      Mac31 las hace en este orden y aqui se contestan todas de un golpe, porque
      la pantalla necesita saber si va a abrir una pestaña o un dialogo ANTES de
      hacer nada: en el navegador, la pestaña hay que abrirla en el mismo clic o
      la bloquea el bloqueador de emergentes, asi que no se puede "abrir y luego
      ver si hacia falta la contrasena".
    */
    public async Task<VentasReportePreparacionResponse> PrepararReporteAsync(
        VentasReporteRequest request,
        CancellationToken ct)
    {
        var req = request ?? new VentasReporteRequest();
        var ids = (req.IdsVenta ?? new List<int>()).Where(x => x > 0).Distinct().ToList();
        if (ids.Count == 0)
            throw new InvalidOperationException("Selecciona una remisión.");

        var constantes = await _cancelacion.ConsultarConstantesAsync(ct);
        var accion = VentasImpresionReglas.Normalizar(req.Accion);

        var bloqueos = await BloqueosAsync(ids, accion, constantes.Funcionalidad, ct);

        return new VentasReportePreparacionResponse
        {
            Funcionalidad = constantes.Funcionalidad,
            EsCentroServicio = constantes.EsCentroServicio,
            RequiereContrasena = VentasImpresionReglas.RequiereContrasena(
                accion, req.Vista, constantes.Funcionalidad, constantes.EsCentroServicio),
            ConfirmaImpresora = VentasImpresionReglas.ConfirmaImpresora(accion, constantes.Funcionalidad),
            ConfirmaReimpresion = VentasImpresionReglas.ConfirmaReimpresion(accion),
            Bloqueos = bloqueos,
            IdsVenta = ids.Where(id => bloqueos.All(b => b.IdVenta != id)).ToList()
        };
    }

    private async Task<List<VentasReporteBloqueo>> BloqueosAsync(
        IReadOnlyCollection<int> ids,
        string accion,
        string funcionalidad,
        CancellationToken ct)
    {
        var ventas = await _cancelacion.ConsultarVentasAsync(ids, ct);
        var bloqueos = new List<VentasReporteBloqueo>();

        foreach (var venta in ventas)
        {
            var motivo = VentasImpresionReglas.Bloqueo(venta, accion, funcionalidad);
            if (motivo is null)
                continue;

            bloqueos.Add(new VentasReporteBloqueo
            {
                IdVenta = venta.IdVenta,
                FolioFtm = venta.FolioFtm,
                Motivo = motivo
            });
        }

        /*
          LA PRIMERA IMPRESION NO SE REPITE

          Es la regla del propio procedimiento: con @PrimerImpresion = 1,
          sp_n_VentasInformacion devuelve result = 0 y el texto "EL FOLIO ... YA
          FUE IMPRESO POR ...". Se adelanta aqui para poder decirlo en la
          pantalla, con la misma frase, en vez de dejar que aparezca como una
          linea de texto suelta en una pestaña que se acaba de abrir.

          Solo aplica donde la bandera viaja de verdad: en Zaragoza siempre va
          en cero, asi que alla no se bloquea nada (igual que en Mac31, donde
          Imprimir de Zaragoza entrega el papel sin preguntar quien lo imprimio).
        */
        var (primerImpresion, _) = VentasImpresionReglas.Banderas(accion, funcionalidad);
        if (primerImpresion != 1)
            return bloqueos;

        var pendientes = ids.Where(id => bloqueos.All(b => b.IdVenta != id)).ToList();
        foreach (var previa in await _impresiones.ConsultarImpresionesPreviasAsync(pendientes, ct))
        {
            bloqueos.Add(new VentasReporteBloqueo
            {
                IdVenta = previa.IdVenta,
                FolioFtm = previa.FolioFtm,
                Motivo = $"EL FOLIO: {previa.FolioFtm} YA FUE IMPRESO POR: {previa.Usuario}" +
                         $"  A LAS: {previa.Cuando}" +
                         (previa.Equipo.Length > 0 ? $"  ({previa.Equipo})" : "") +
                         ". Usa Reimprimir."
            });
        }

        return bloqueos;
    }

    /*
      LA CONTRASENA ROTATORIA DE IMPRESION

      Es la MISMA de cancelar —el codigo del minuto de Mac31
      (ConfirmarContrasena.GenerarContrasena)— y por eso se reusa la de alla en
      vez de copiarla: dos versiones de una contrasena se despegan el dia que
      alguien ajuste una. Aqui solo cambia la razon por la que se pide.

      Se valida en el servidor y no en el navegador, por lo mismo que en
      cancelaciones: en Mac31 el cliente es la maquina de la oficina; una pagina
      web no lo es. Y el intento queda en la misma bitacora, con el nombre de
      forma "ConsultarVentas", para que los dos canales se lean juntos.

      LOS INTENTOS SON ILIMITADOS, igual que en Mac31 (ConfirmarContrasena no
      lleva contador): la contrasena cambia cada minuto, asi que el limite lo
      pone el reloj.
    */
    private async Task ValidarContrasenaAsync(
        string? capturada,
        int idUsuario,
        string equipo,
        ConstantesCancelacion constantes,
        CancellationToken ct)
    {
        var texto = (capturada ?? "").Trim();
        if (texto.Length == 0)
            throw new ConflictException("INGRESE LA CONTRASEÑA.");

        var propia = await _cancelacion.ConsultarContrasenaAutorizacionAsync(idUsuario, ct);

        var esperada = propia is { Length: > 0 }
            ? propia
            : VentasCancelacionService.ContrasenaDelMinuto(constantes.FechaOperacion, DateTime.Now);

        var correcta = string.Equals(texto, esperada, StringComparison.OrdinalIgnoreCase);

        await _cancelacion.RegistrarIntentoContrasenaAsync("ConsultarVentas", correcta, texto, idUsuario, equipo, ct);

        if (!correcta)
            throw new ConflictException("LA CONTRASEÑA ES INCORRECTA.");
    }
}
