using ISL_Service.Application.DTOs.RecepcionCascos;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/// <summary>
/// Recepcion de cascos usados.
///
/// POR QUE VALIDA AQUI SI EL PROCEDIMIENTO YA VALIDA
/// Porque son dos cosas distintas. El procedimiento es el ultimo candado y el
/// que no se puede saltar ni con curl; estas comprobaciones son las que
/// contestan rapido y con el mensaje de la pantalla sin gastar un viaje a la
/// base. Las dos tienen que existir: quitar las de aqui hace lenta cada
/// equivocacion, y quitar las de alla deja la puerta abierta a quien no use la
/// pantalla.
///
/// LO QUE NO SE HACE AQUI, A PROPOSITO: ningun total se suma en C#. Los totales
/// salen del procedimiento, que es el que ve el detalle guardado.
/// </summary>
public class RecepcionCascosService : IRecepcionCascosService
{
    /*
      Tope de renglones del detalle. El catalogo tiene 8 tipos activos; 40 deja
      sitio de sobra para que crezca y corta de golpe una peticion armada a
      mano con miles de renglones.
    */
    private const int MaximoRenglones = 40;

    /*
      Tope de recepciones por cancelacion. La pantalla deja marcar lo que trajo
      la consulta de un periodo; 500 es mas de lo que cabe en un dia de trabajo
      y evita que una sola peticion arrastre la tabla entera.
    */
    private const int MaximoACancelar = 500;

    private readonly IRecepcionCascosRepository _repository;

    public RecepcionCascosService(IRecepcionCascosRepository repository)
    {
        _repository = repository;
    }

    public Task<List<TipoRecepcionCascoDto>> ConsultarTiposAsync(CancellationToken ct = default)
        => _repository.ConsultarTiposAsync(ct);

    public Task<List<RepartidorRecepcionCascoDto>> ConsultarRepartidoresAsync(CancellationToken ct = default)
        => _repository.ConsultarRepartidoresAsync(ct);

    public Task<List<RecepcionCascoDto>> ConsultarAsync(
        DateTime? fechaInicio, DateTime? fechaFin, int? idRepartidor,
        int? estatus, bool incluirCanceladas, CancellationToken ct = default)
    {
        /*
          Rango invertido: se endereza en vez de devolver vacio. Pasa al
          teclear las fechas y un listado vacio se lee como "no hubo nada",
          que es una respuesta falsa.
        */
        if (fechaInicio.HasValue && fechaFin.HasValue && fechaFin < fechaInicio)
            (fechaInicio, fechaFin) = (fechaFin, fechaInicio);

        return _repository.ConsultarAsync(
            fechaInicio, fechaFin, idRepartidor, estatus, incluirCanceladas, ct);
    }

    public Task<List<DetalleRecepcionCascoDto>> ConsultarDetalleAsync(
        int idRecepcion, CancellationToken ct = default)
        => _repository.ConsultarDetalleAsync(idRecepcion, ct);

    public Task<List<BitacoraRecepcionCascoDto>> ConsultarBitacoraAsync(
        int idRecepcion, CancellationToken ct = default)
        => _repository.ConsultarBitacoraAsync(idRecepcion, ct);

    public Task<int> EstatusDeAsync(int idRecepcion, CancellationToken ct = default)
        => _repository.EstatusDeAsync(idRecepcion, ct);

    public async Task<ResultadoRecepcionCascoDto> CrearAsync(
        CrearRecepcionCascoRequest request, int idUsuario, string usuario, string? canal,
        CancellationToken ct = default)
    {
        var falla = Revisar(request);
        if (falla != null) return No(falla, 0);

        return await _repository.InsertarAsync(request, idUsuario, usuario, canal, ct);
    }

    public async Task<ResultadoRecepcionCascoDto> ActualizarAsync(
        ActualizarRecepcionCascoRequest request, int idUsuario, string usuario, string? canal,
        CancellationToken ct = default)
    {
        if (request.IdRecepcion <= 0)
            return No("No se recibio la recepcion que se quiere modificar.", 0);

        var falla = Revisar(request);
        if (falla != null) return No(falla, request.IdRecepcion);

        return await _repository.ActualizarAsync(request, idUsuario, usuario, canal, ct);
    }

    public async Task<ResultadoCancelacionRecepcionCascoDto> CancelarAsync(
        CancelarRecepcionesCascoRequest request, int idUsuario, string usuario, string? canal,
        CancellationToken ct = default)
    {
        var ids = (request.Ids ?? new List<int>()).Where(i => i > 0).Distinct().ToList();

        if (ids.Count == 0)
            return NoCancelo("Elija al menos una recepcion.");

        if (ids.Count > MaximoACancelar)
            return NoCancelo($"Son demasiadas recepciones de una vez. Cancele hasta {MaximoACancelar}.");

        var motivo = (request.Motivo ?? string.Empty).Trim();
        if (motivo.Length == 0)
            return NoCancelo("Escriba el motivo de la cancelacion.");

        return await _repository.CancelarAsync(ids, motivo, idUsuario, usuario, canal, ct);
    }

    public Task<DatosReporteRecepcionCascos> ConsultarDatosDeReporteAsync(
        DateTime fechaInicio, DateTime fechaFin, bool incluirCanceladas = false,
        CancellationToken ct = default)
    {
        if (fechaFin < fechaInicio) (fechaInicio, fechaFin) = (fechaFin, fechaInicio);
        return _repository.ConsultarDatosDeReporteAsync(
            fechaInicio, fechaFin, incluirCanceladas, ct);
    }

    public async Task<(string Logo, string Empresa)> ConsultarMarcaParaReporteAsync(
        CancellationToken ct = default)
    {
        var (pathImagenes, logo, empresa) = await _repository.ConsultarMarcaEmpresaAsync(ct);
        if (string.IsNullOrWhiteSpace(logo)) return ("", empresa);

        try
        {
            var ruta = RutaDelLogo(pathImagenes, logo);
            if (ruta is null) return ("", empresa);

            var tipo = Path.GetExtension(ruta).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".svg" => "image/svg+xml",
                ".webp" => "image/webp",
                _ => "image/png"
            };

            var bytes = await File.ReadAllBytesAsync(ruta, ct);
            return ($"data:{tipo};base64,{Convert.ToBase64String(bytes)}", empresa);
        }
        catch
        {
            /* Un logo ilegible no puede tumbar el reporte: sale sin el. */
            return ("", empresa);
        }
    }

    /*
      DONDE ESTA EL LOGO: PRIMERO EL DE LEGACY, LUEGO EL EMPAQUETADO

      1. La carpeta de legacy (Constantes.PathImagenes). Es la fuente buena: si
         alguien cambia ahi el logo, el reporte lo recoge sin desplegar nada.
      2. Assets/Logos, que viaja con el backend, con el MISMO nombre de archivo.

      El segundo camino existe porque en produccion este backend corre fuera de
      la maquina de legacy y esa carpeta no esta ahi: el logo salia vacio en
      produccion y completo en local. Es el mismo criterio que ya sigue el
      reporte de usados a cambio.

      Null si no esta en ninguno de los dos.
    */
    private static string? RutaDelLogo(string pathImagenes, string logo)
    {
        if (!string.IsNullOrWhiteSpace(pathImagenes))
        {
            var deLegacy = Path.Combine(pathImagenes, logo);
            if (File.Exists(deLegacy)) return deLegacy;
        }

        var empaquetado = Path.Combine(AppContext.BaseDirectory, "Assets", "Logos", logo);
        return File.Exists(empaquetado) ? empaquetado : null;
    }

    /* ---------------------------------------------------------------- */

    /// <summary>Lo que esta mal, o null si todo esta bien.</summary>
    private static string? Revisar(CrearRecepcionCascoRequest request)
    {
        if (request.Fecha == null)
            return "La fecha es obligatoria.";

        if (request.Fecha.Value.Date > DateTime.Today)
            return "La fecha no puede ser posterior a hoy.";

        /*  Que el repartidor EXISTA y siga activo no se comprueba aqui sino en
            el procedimiento, que es quien tiene el catalogo delante. Aqui solo
            se para lo que ni siquiera es un id.  */
        if (request.IdRepartidor <= 0)
            return "Elija el repartidor que entrego los cascos.";

        var piezas = request.Piezas ?? new List<PiezasPorTipoRecepcionRequest>();

        if (piezas.Count > MaximoRenglones)
            return "Llegaron demasiados tipos de casco en la misma recepcion.";

        if (piezas.Any(p => p != null && p.Piezas < 0))
            return "Las piezas no pueden ser negativas.";

        if (!piezas.Any(p => p != null && p.IdTipoUsado > 0 && p.Piezas > 0))
            return "Capture al menos un tipo de casco con piezas.";

        return null;
    }

    private static ResultadoRecepcionCascoDto No(string mensaje, int id) => new()
    {
        ok = false, mensaje = mensaje, idRecepcion = id, advertencia = string.Empty
    };

    private static ResultadoCancelacionRecepcionCascoDto NoCancelo(string mensaje) => new()
    {
        ok = false, mensaje = mensaje, canceladas = 0, advertencia = string.Empty
    };
}
