using ISL_Service.Application.DTOs.ConsultasLegacy;
using ISL_Service.Application.DTOs.VentasCobranza;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/// <summary>
/// Reglas de las lecturas de cobranza. Son pocas a proposito: estas consultas
/// no tienen negocio propio, el negocio vive en los `sp_n_`.
/// </summary>
/// <remarks>
/// LO QUE AQUI **NO** SE COPIA DE LEGACY
///
/// MacServicios2 tiene en estas pantallas un patron que conviene no heredar: si
/// se llena el filtro de folio, **reconstruye el DTO desde cero**
/// (`consultaCobranzasInputDTO = new ConsultaCobranzasInputDTO()`,
/// ConsultaCobranzasService.cs:57) y con eso tira TODOS los demas filtros,
/// incluido `IDUsuarioActual`, que es el que decide el candado de almacen.
/// Aqui no hace falta imitarlo porque cada endpoint recibe solo los campos que
/// su procedimiento usa: no hay filtros de sobra que descartar.
///
/// Tampoco se copia el bug de su `else if`: en ConsultaCobranzasService.cs:62 y
/// ConsultaCobrosService.cs:61 la segunda rama repite la condicion de la
/// primera (`FolioInicialCobranza != "0"` en las dos), asi que la rama "buscar
/// por folio de venta" es codigo muerto y nunca corre.
/// </remarks>
public class VentasCobranzaConsultaService : IVentasCobranzaConsultaService
{
    private readonly IVentasCobranzaConsultaRepository _repository;

    public VentasCobranzaConsultaService(IVentasCobranzaConsultaRepository repository)
    {
        _repository = repository;
    }

    public Task<ConsultaFilasResponse> ValidarFolioCobranzaAsync(CobranzaFolioRequest request, int idUsuarioActual, CancellationToken ct)
        => _repository.ValidarFolioCobranzaAsync(request, idUsuarioActual, ct);

    public Task<ConsultaFilasResponse> ConsultarCobranzaDetalleAsync(int idCobranza, int formato, CancellationToken ct)
        => _repository.ConsultarCobranzaDetalleAsync(idCobranza, NormalizarFormato(formato), ct);

    public Task<ConsultaFilasResponse> ValidarFolioConcentradoAsync(ConcentradoFolioRequest request, int idUsuarioActual, CancellationToken ct)
        => _repository.ValidarFolioConcentradoAsync(request, idUsuarioActual, ct);

    public Task<ConsultaFilasResponse> ConsultarConcentradosAsync(ConcentradosConsultaRequest request, int idUsuarioActual, CancellationToken ct)
        => _repository.ConsultarConcentradosAsync(request, idUsuarioActual, ct);

    public Task<ConsultaFilasResponse> ConsultarConcentradoDetalleAsync(int idConcentrado, CancellationToken ct)
        => _repository.ConsultarConcentradoDetalleAsync(idConcentrado, ct);

    public Task<ConsultaFilasResponse> ConsultarCobrosCentroServicioAsync(int idCobro, int folioCobro, CancellationToken ct)
        => _repository.ConsultarCobrosCentroServicioAsync(idCobro, folioCobro, ct);

    public Task<ConsultaFilasResponse> ConsultarFoliosUsadosAsync(CobroFoliosUsadosRequest request, CancellationToken ct)
        => _repository.ConsultarFoliosUsadosAsync(request, ct);

    public Task<ConsultaFilasResponse> ConsultarCobroPagosAsync(CobroPagosRequest request, CancellationToken ct)
        => _repository.ConsultarCobroPagosAsync(request, ct);

    public Task<ConsultaFilasResponse> ConsultarFoliosSinAsignarAsync(IEnumerable<int> idsEmpresa, int devueltasA, CancellationToken ct)
    {
        // Ids limpios: un 0 en la lista la convierte en un filtro que no empareja
        // con ninguna empresa y la consulta sale vacia sin decir por que.
        var empresas = (idsEmpresa ?? Enumerable.Empty<int>()).Where(x => x > 0).Distinct().ToList();
        return _repository.ConsultarFoliosSinAsignarAsync(empresas, devueltasA, ct);
    }

    public Task<ConsultaFilasResponse> ConsultarPagosKilosAsync(PagosKilosRequest request, CancellationToken ct)
        => _repository.ConsultarPagosKilosAsync(request, ct);

    /*
      `@Presentacion` / `@Formato` solo entienden 0, 1 y 2 (el enum
      enumPresentacion de Mac31: pantalla, impresora, pdf). Cualquier otra cosa
      cae a 0, que es lo que manda legacy por omision: un numero suelto de la
      pantalla no debe llegar al procedimiento a ver que pasa.
    */
    public static int NormalizarFormato(int formato) => formato is 1 or 2 ? formato : 0;
}
