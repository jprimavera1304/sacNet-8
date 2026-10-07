using ISL_Service.Application.DTOs.PedidosOperaciones;
using ISL_Service.Application.DTOs.VentasEscrituras;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Infrastructure.Repositories;

/*
  PROCESAR Y CANCELAR PEDIDOS, UNO POR UNO

  EL PROCEDIMIENTO SE LLAMA POR PEDIDO, Y ESO ES DELIBERADO.

  sp_n_ProcesarPedido abre su propia transaccion, consume el folio de remision,
  mueve existencias y escribe los pagos. Legacy lo llama en un ciclo, con una
  CONEXION NUEVA por iteracion y sin ninguna transaccion que los envuelva: si el
  tercero de cinco falla, los dos primeros quedan procesados.

  Se conserva, y el motivo no es pereza:

    * Es el efecto en la base que ya existe. Diez anos de datos se escribieron
      asi, y un lote a medias es un estado que el negocio sabe arreglar (se
      vuelve a procesar lo que falto).
    * Envolver N procedimientos en una transaccion externa mantendria los
      candados de Ventas, Folios y AlmacenProducto tomados durante todo el lote.
      Con un lote de cincuenta pedidos eso bloquea a quien este facturando.
    * Y lo que de verdad decide: cada sp_n_ hace su propio COMMIT. Una
      transaccion de fuera no puede deshacer lo que ya se confirmo dentro, asi
      que el "todo o nada" seria una ilusion, no una garantia.

  Lo que SI se arregla es el reporte: legacy devuelve solo los que fallaron, asi
  que un lote perfecto contesta una lista vacia y nadie sabe que folio le toco a
  cada pedido. Aqui vuelve un renglon por pedido.
*/
public class PedidosOperacionesRepository : SpLegacyRepositoryBase, IPedidosOperacionesRepository
{
    private const string SpProcesarVenta = "sp_n_ProcesarPedido";
    private const string SpProcesarCompra = "sp_n_ProcesarCompra";
    private const string SpCancelar = "sp_n_CancelarPedido";
    private const string SpAjustarUsadosCredito = "sp_n_ActualizarUsadosCredito";

    private const int LargoEquipoLargo = 1000;
    private const int LargoEquipoCancelar = 2000;
    private const int LargoTarjeta = 100;
    private const int LargoDeposito = 1000;

    public PedidosOperacionesRepository(IConfiguration configuration) : base(configuration) { }

    public async Task<PedidoResultadoLote> ProcesarVentaAsync(
        int idPedido, PedidosProcesarVentaRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        /*
          LAS TRES FIRMAS SALEN DEL TOKEN, NO DEL CUERPO.

          El procedimiento recibe tres pares usuario/equipo: quien PROCESA, quien
          AUTORIZA y quien RECHAZA. En legacy los tres viajan en el cuerpo
          (IDUsuarioProcesar, IDUsuarioAutorizar, IDUsuarioRechazar y sus
          equipos), lo que significa que cualquiera podia procesar un pedido
          dejando escrito que lo autorizo otra persona.

          Aqui quien procesa es siempre el del token, y los otros dos se llenan
          con el MISMO usuario solo cuando el request dice que ademas autoriza o
          rechaza. Si no, van en cero: el procedimiento los lee como "esta vez no
          se autorizo ni se rechazo", que es lo que querian decir.
        */
        var equipoLimpio = FiltrosLegacy.Texto(equipo, LargoEquipoLargo);
        var idAutoriza = request.Autorizar ? idUsuario : 0;
        var equipoAutoriza = request.Autorizar ? equipoLimpio : string.Empty;
        var idRechaza = request.Rechazar ? idUsuario : 0;
        var equipoRechaza = request.Rechazar ? equipoLimpio : string.Empty;

        var filas = await EjecutarFilasAsync(SpProcesarVenta, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDPedido", idPedido));
            cmd.Parameters.Add(Entero("@IDUsuarioProcesar", idUsuario));
            cmd.Parameters.Add(Varchar("@EquipoProcesar", LargoEquipoLargo, equipoLimpio));
            cmd.Parameters.Add(Entero("@IDUsuarioAutorizar", idAutoriza));
            cmd.Parameters.Add(Varchar("@EquipoAutorizar", LargoEquipoLargo, equipoAutoriza));
            cmd.Parameters.Add(Entero("@IDUsuarioRechazar", idRechaza));
            cmd.Parameters.Add(Varchar("@EquipoRechazar", LargoEquipoLargo, equipoRechaza));
            cmd.Parameters.Add(Entero("@IDTipoPagoCS", request.IdTipoPagoCs));
            cmd.Parameters.Add(Entero("@IDBancoTransferCS", request.IdBancoTransferCs));
            cmd.Parameters.Add(Varchar("@TransferenciaCS", LargoTarjeta, FiltrosLegacy.Texto(request.TransferenciaCs, LargoTarjeta)));
            cmd.Parameters.Add(Entero("@IDBancoTarjetaCS", request.IdBancoTarjetaCs));
            cmd.Parameters.Add(Varchar("@TarjetaCS", LargoTarjeta, FiltrosLegacy.Texto(request.TarjetaCs, LargoTarjeta)));
            cmd.Parameters.Add(Entero("@IDBancoDepositoEfeCS", request.IdBancoDepositoEfeCs));
            cmd.Parameters.Add(Varchar("@DepositoEfectivoNumeroCS", LargoDeposito, FiltrosLegacy.Texto(request.DepositoEfectivoNumeroCs, LargoDeposito)));
            cmd.Parameters.Add(Decimal18("@MontoTotalCS", request.MontoTotalCs));
            cmd.Parameters.Add(Entero("@TipoTarjeta", request.TipoTarjeta));
        }, ct);

        return Renglon(idPedido, filas);
    }

    public async Task<ResultadoLegacyDto> ProcesarCompraAsync(
        PedidoProcesarCompraRequest request, string equipo, CancellationToken ct)
    {
        /*
          ESTE NO RECIBE USUARIO: el procedimiento declara seis parametros y solo
          uno es de firma, @Equipo. No se le inventa un @IDUsuario que no existe.
        */
        var tablas = await EjecutarTablasAsync(SpProcesarCompra, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDPedido", request.IdPedido));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipoLargo, FiltrosLegacy.Texto(equipo, LargoEquipoLargo)));
            cmd.Parameters.Add(Varchar("@Observaciones", LargoEquipoLargo, FiltrosLegacy.Texto(request.Observaciones, LargoEquipoLargo)));
            cmd.Parameters.Add(Varchar("@Factura", LargoEquipoLargo, FiltrosLegacy.Texto(request.Factura, LargoEquipoLargo)));
            cmd.Parameters.Add(Varchar("@FechaEmision", LargoEquipoLargo, FiltrosLegacy.Fecha(request.FechaEmision)));
            cmd.Parameters.Add(Varchar("@FechaVencimiento", LargoEquipoLargo, FiltrosLegacy.Fecha(request.FechaVencimiento)));
        }, ct);

        var dto = new ResultadoLegacyDto();
        if (tablas.Count == 0) return dto;

        dto.Filas = tablas[0];
        for (var i = 1; i < tablas.Count; i++) dto.TablasExtra.Add(tablas[i]);

        var primera = dto.Filas.Count > 0 ? dto.Filas[0] : null;
        if (primera is null) return dto;

        if (primera.ContainsKey("result")) dto.Resultado = Entero(primera, "result");
        if (primera.ContainsKey("mensaje")) dto.Mensaje = Texto(primera, "mensaje");
        /* El id que devuelve es la COMPRA que se creo, no el pedido. */
        if (primera.ContainsKey("IDCompra")) dto.Id = Entero(primera, "IDCompra");

        return dto;
    }

    public async Task<PedidoResultadoLote> CancelarAsync(
        int idPedido, int idUsuario, string equipo, CancellationToken ct)
    {
        var filas = await EjecutarFilasAsync(SpCancelar, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDPedido", idPedido));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipoCancelar, FiltrosLegacy.Texto(equipo, LargoEquipoCancelar)));
        }, ct);

        return Renglon(idPedido, filas);
    }

    public async Task<ResultadoLegacyDto> AjustarUsadosCreditoAsync(
        PedidoUsadoCreditoRequest request, CancellationToken ct)
    {
        /*
          TRES PARAMETROS Y DOS TABLAS DE VUELTA.

          El procedimiento devuelve el renglon ajustado y, en una segunda tabla,
          el catalogo de tipos de casco con sus cantidades al dia. Legacy publica
          las dos (pedidoUsadosCredito y pedidoTipoUsadoCreditos); aqui la segunda
          baja en `tablasExtra` para no perderla.
        */
        var tablas = await EjecutarTablasAsync(SpAjustarUsadosCredito, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDPedido", request.IdPedido));
            cmd.Parameters.Add(Entero("@IDTipoUsado", request.IdTipoUsado));
            cmd.Parameters.Add(Entero("@Cantidad", request.Cantidad));
        }, ct);

        var dto = new ResultadoLegacyDto();
        if (tablas.Count == 0) return dto;

        dto.Filas = tablas[0];
        for (var i = 1; i < tablas.Count; i++) dto.TablasExtra.Add(tablas[i]);

        var primera = dto.Filas.Count > 0 ? dto.Filas[0] : null;
        if (primera is null) return dto;

        if (primera.ContainsKey("result")) dto.Resultado = Entero(primera, "result");
        if (primera.ContainsKey("mensaje")) dto.Mensaje = Texto(primera, "mensaje");

        return dto;
    }

    /// <summary>
    /// El renglon de resultado de UN pedido. Cuando el procedimiento no devuelve
    /// nada —pasa— se entiende como "no se pudo", porque los dos de lote siempre
    /// contestan result y mensaje cuando llegan al final.
    /// </summary>
    private static PedidoResultadoLote Renglon(int idPedido, List<Dictionary<string, object?>> filas)
    {
        if (filas.Count == 0)
        {
            return new PedidoResultadoLote
            {
                IdPedido = idPedido,
                Resultado = -1,
                Mensaje = "El procedimiento no devolvio resultado."
            };
        }

        var fila = filas[0];
        return new PedidoResultadoLote
        {
            IdPedido = idPedido,
            Resultado = Entero(fila, "result", -1),
            Mensaje = Texto(fila, "mensaje"),
            Pedido = Entero(fila, "Pedido"),
            IdVenta = Entero(fila, "IDVenta")
        };
    }
}
