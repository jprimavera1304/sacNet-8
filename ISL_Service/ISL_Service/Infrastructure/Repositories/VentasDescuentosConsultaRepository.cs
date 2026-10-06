using ISL_Service.Application.DTOs.ConsultasLegacy;
using ISL_Service.Application.DTOs.VentasDescuentos;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Infrastructure.Repositories;

public class VentasDescuentosConsultaRepository : ConsultaLegacyRepositoryBase, IVentasDescuentosConsultaRepository
{
    private const string SpDetalle = "dbo.sp_n_ConsultaDescuentosDetalle";
    private const string SpDetalleLog = "dbo.sp_n_ConsultarDescuentosDetalleLog";

    public VentasDescuentosConsultaRepository(IConfiguration configuration) : base(configuration)
    {
    }

    /*
      LAS TRES LISTAS VAN CON VIRGULILLA, NO CON COMA.

      Legacy las une con `Variables.DELIMITADOR_PARAM_TILDE_SP`, que vale "~"
      (DescuentoDetalleService.cs:34-36 de MacServicios2), y el procedimiento las
      parte por ese caracter. Con coma, y con DOS O MAS elementos, el filtro no
      encuentra nada; con UNO funciona igual porque no hay nada que partir. Es el
      mismo fallo silencioso que ya estaba documentado para `sp_n_ConsultaVentas`.
    */
    public Task<ConsultaFilasResponse> ConsultarDetalleAsync(DescuentoDetalleRequest request, CancellationToken ct)
    {
        return EjecutarFilasAsync(SpDetalle, p =>
        {
            p.AddWithValue("@IDsMarca", Ids(request.IDsMarca, null));
            p.AddWithValue("@IDsCategoria", Ids(request.IDsCategoria, null));
            p.AddWithValue("@IDsGrupoCategoria", Ids(request.IDsGrupoCategoria, null));
            p.AddWithValue("@IDDescuentoDetalle", request.IDDescuentoDetalle);
            p.AddWithValue("@IDDescuento", request.IDDescuento);
            p.AddWithValue("@IDVenta", request.IDVenta);
        }, ct);
    }

    /*
      SOLO TRES PARAMETROS, COMO LEGACY.

      El procedimiento declara cinco (@IDDescuento, @IDCategoria, @IDMarca,
      @FechaInicial, @FechaFinal) pero legacy manda tres
      (DescuentoDetalleRepository.cs:115-117) y los dos de fecha tienen valor por
      omision. Se comprobo ejecutandolo con tres parametros contra
      Produccion_svr: corre y devuelve los 6,495 renglones de la bitacora.

      No se expone el rango de fechas aunque el procedimiento lo acepte: seria
      ofrecer un filtro que esta pantalla nunca ha tenido y que nadie ha probado.
      Si algun dia hace falta, se agrega aqui y se nota en el caso.
    */
    public Task<ConsultaFilasResponse> ConsultarDetalleLogAsync(DescuentoDetalleLogRequest request, CancellationToken ct)
    {
        return EjecutarFilasAsync(SpDetalleLog, p =>
        {
            p.AddWithValue("@IDDescuento", request.IDDescuento);
            p.AddWithValue("@IDCategoria", request.IDCategoria);
            p.AddWithValue("@IDMarca", request.IDMarca);
        }, ct);
    }
}
