using ISL_Service.Application.DTOs.PedidosOperaciones;
using ISL_Service.Application.DTOs.VentasEscrituras;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Application.Services;

/*
  LOS LOTES DE PEDIDOS

  EL ORDEN DE PROCESAMIENTO IMPORTA, Y LEGACY LO ORDENA MAL A PROPOSITO.

  `processVentaAsync` hace `IDPedidoLst.Sort()` sobre una `List<string>`, asi que
  ordena como TEXTO: ["10","2","9"] se queda ["10","2","9"], y ["2","10"] se
  vuelve ["10","2"]. Eso no es cosmetico: el orden decide en que secuencia se
  consumen los folios de remision, y por lo tanto que folio le toca a cada
  pedido.

  Se conserva el MISMO orden, por una razon y no por respeto: es el que ya
  produjo diez anos de folios, y un lote procesado hoy desde el web tiene que
  dejar la misma secuencia que el mismo lote procesado desde Mac31. Ordenar
  numericamente —que es "lo correcto"— haria que las dos mitades del sistema
  asignaran folios distintos a los mismos pedidos, y eso es peor que un orden
  raro. Queda anotado como diferencia que algun dia conviene revisar, pero no
  aqui y no en silencio.

  `cancelAsync` NO ordena: se recorre en el orden en que llega. Tambien se
  conserva, y ahi no hay folio en juego.
*/
public class PedidosOperacionesService : IPedidosOperacionesService
{
    private readonly IPedidosOperacionesRepository _repository;

    public PedidosOperacionesService(IPedidosOperacionesRepository repository)
    {
        _repository = repository;
    }

    public async Task<PedidosLoteResponse> ProcesarVentasAsync(
        PedidosProcesarVentaRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var pedidos = FiltrosLegacy.LimpiarIds(request.IdsPedido);
        if (pedidos.Count == 0)
            throw new ArgumentException("Hay que indicar al menos un pedido.");

        if (request.Autorizar && request.Rechazar)
            throw new ArgumentException("Un pedido se autoriza o se rechaza, no las dos cosas.");

        /*
          SI SE COBRA DE CONTADO, EL MONTO TIENE QUE CUADRAR CON EL TIPO DE PAGO.

          El procedimiento acepta un tipo de pago sin monto y un monto sin tipo de
          pago, y en los dos casos escribe un pago que no cuadra: uno de cero
          pesos con forma de pago, u otro con importe y sin forma. Legacy no
          valida ninguno de los dos.
        */
        if (request.IdTipoPagoCs > 0 && request.MontoTotalCs <= 0)
            throw new ArgumentException("Si se cobra de contado, el monto tiene que ser mayor que cero.");
        if (request.MontoTotalCs > 0 && request.IdTipoPagoCs <= 0)
            throw new ArgumentException("Si hay monto de contado, hace falta el tipo de pago.");

        var enOrden = OrdenDeLegacy(pedidos);

        var respuesta = new PedidosLoteResponse();
        foreach (var idPedido in enOrden)
        {
            ct.ThrowIfCancellationRequested();

            var renglon = await _repository.ProcesarVentaAsync(idPedido, request, idUsuario, equipo, ct);
            respuesta.Resultados.Add(renglon);
            if (renglon.Resultado > 0 && renglon.IdVenta > 0)
                respuesta.IdsVenta.Add(renglon.IdVenta);
        }

        return respuesta;
    }

    public async Task<ResultadoLegacyDto> ProcesarCompraAsync(
        PedidoProcesarCompraRequest request, string equipo, CancellationToken ct)
    {
        if (request.IdPedido <= 0)
            throw new ArgumentException("El pedido es obligatorio.");

        /*
          SI HAY FACTURA, HACEN FALTA SUS FECHAS.

          El procedimiento guarda la factura del proveedor con su emision y su
          vencimiento, y de esas dos fechas sale la antiguedad del saldo con el
          proveedor. Una factura sin fechas entra con 1900-01-01 o con null y
          aparece vencida desde siempre. Legacy formatea las dos fechas SIN
          comprobar que vengan, asi que si faltan revienta con un error de
          Substring: el dato era obligatorio de hecho.
        */
        if (!string.IsNullOrWhiteSpace(request.Factura))
        {
            if (request.FechaEmision is null)
                throw new ArgumentException("Si hay factura, la fecha de emision es obligatoria.");
            if (request.FechaVencimiento is null)
                throw new ArgumentException("Si hay factura, la fecha de vencimiento es obligatoria.");
            if (request.FechaVencimiento < request.FechaEmision)
                throw new ArgumentException("El vencimiento no puede ser anterior a la emision.");
        }

        return await _repository.ProcesarCompraAsync(request, equipo, ct);
    }

    public async Task<PedidosLoteResponse> CancelarAsync(
        PedidosCancelarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var pedidos = FiltrosLegacy.LimpiarIds(request.IdsPedido);
        if (pedidos.Count == 0)
            throw new ArgumentException("Hay que indicar al menos un pedido.");

        /*
          SIN USUARIO DE MAC31 NO SE CANCELA, Y TAMBIEN ES EL PROCEDIMIENTO.

          Se midio: sp_n_CancelarPedido NO cambia IDStatusPedido. Lo unico que deja
          escrito es la firma —IDUsuarioCancelo y EquipoCancelo— y con @IDUsuario = 0
          no deja nada, mientras contesta result = 1. Es decir que una cancelacion
          firmada con 0 es indistinguible de no haber cancelado, y la pantalla dice
          que si.

          El id sale del token y llega en 0 cuando el usuario web no esta ligado a
          Mac31.
        */
        if (idUsuario <= 0)
            throw new ConflictException(
                "Tu usuario web no esta ligado a un usuario de Mac31, y cancelar un "
                + "pedido necesita esa firma: es lo unico que queda escrito de la "
                + "cancelacion.");

        var respuesta = new PedidosLoteResponse();
        foreach (var idPedido in pedidos)
        {
            ct.ThrowIfCancellationRequested();
            respuesta.Resultados.Add(await _repository.CancelarAsync(idPedido, idUsuario, equipo, ct));
        }

        return respuesta;
    }

    public async Task<ResultadoLegacyDto> AjustarUsadosCreditoAsync(
        PedidoUsadoCreditoRequest request, CancellationToken ct)
    {
        if (request.IdPedido <= 0)
            throw new ArgumentException("El pedido es obligatorio.");

        /*
          EL TIPO DE CASCO ES OBLIGATORIO, Y LEGACY NO LO PEDIA.

          Su controlador valida solo IDPedido, aunque el DTO declare
          [Required] en IDTipoUsado y Cantidad: [Required] sobre un int no
          bloquea el cero, asi que ModelState lo deja pasar. Con IDTipoUsado = 0
          el procedimiento no encuentra el renglon y no ajusta nada.
        */
        if (request.IdTipoUsado <= 0)
            throw new ArgumentException("El tipo de casco es obligatorio.");
        if (request.Cantidad < 0)
            throw new ArgumentException("La cantidad no puede ser negativa.");

        return await _repository.AjustarUsadosCreditoAsync(request, ct);
    }

    /// <summary>
    /// El orden en que legacy procesa un lote: ordinal por la REPRESENTACION EN
    /// TEXTO del id, porque su lista es de cadenas y usa List&lt;string&gt;.Sort().
    ///
    /// Se replica para que el mismo lote consuma los folios en la misma
    /// secuencia desde el web y desde Mac31. Esta probado sin base en
    /// PedidosOrdenDeLoteTests.
    /// </summary>
    public static List<int> OrdenDeLegacy(IEnumerable<int> pedidos)
        => pedidos
            .OrderBy(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparer.Ordinal)
            .ToList();
}
