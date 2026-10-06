using ISL_Service.Application.DTOs.ConsultasLegacy;
using ISL_Service.Application.DTOs.VentasDescuentos;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

public class VentasDescuentosConsultaService : IVentasDescuentosConsultaService
{
    private readonly IVentasDescuentosConsultaRepository _repository;

    public VentasDescuentosConsultaService(IVentasDescuentosConsultaRepository repository)
    {
        _repository = repository;
    }

    /*
      LAS LISTAS SE LIMPIAN ANTES DE BAJAR.

      Legacy hace `string.Join("~", lista)` sin mirar lo que hay dentro
      (DescuentoDetalleService.cs:34-36), asi que un cero o un id repetido que
      venga de la pantalla se cuela hasta el procedimiento: un "0" en la lista de
      marcas la convierte en un filtro que no empareja con ninguna marca y la
      consulta sale vacia sin decir por que. Aqui se quitan los no positivos y
      los duplicados; sin elementos utiles el filtro queda vacio, que es lo que
      el procedimiento entiende por "todas".
    */
    public Task<ConsultaFilasResponse> ConsultarDetalleAsync(DescuentoDetalleRequest request, CancellationToken ct)
    {
        var limpio = new DescuentoDetalleRequest
        {
            IDsMarca = Limpiar(request.IDsMarca),
            IDsCategoria = Limpiar(request.IDsCategoria),
            IDsGrupoCategoria = Limpiar(request.IDsGrupoCategoria),
            IDDescuentoDetalle = request.IDDescuentoDetalle,
            IDDescuento = request.IDDescuento,
            IDVenta = request.IDVenta
        };
        return _repository.ConsultarDetalleAsync(limpio, ct);
    }

    public Task<ConsultaFilasResponse> ConsultarDetalleLogAsync(DescuentoDetalleLogRequest request, CancellationToken ct)
        => _repository.ConsultarDetalleLogAsync(request, ct);

    public static List<int> Limpiar(IEnumerable<int>? ids)
        => (ids ?? Enumerable.Empty<int>()).Where(x => x > 0).Distinct().ToList();
}
