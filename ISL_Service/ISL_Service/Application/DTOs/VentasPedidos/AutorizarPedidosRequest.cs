namespace ISL_Service.Application.DTOs.VentasPedidos;

public class AutorizarPedidosRequest
{
    public List<int> IdsPedido { get; set; } = new();
    public bool AsyncMode { get; set; } = true;

    /*
      AQUI VIAJABA LA AUTORIA, Y YA NO.

      Habia seis campos -IdUsuario, IdUsuarioProcesar, IdUsuarioAutorizar,
      Equipo, EquipoProcesar, EquipoAutorizar- y el token era el ULTIMO recurso:
      si el cuerpo traia un numero, ese numero era el que quedaba escrito en
      Pedidos.IDUsuarioProceso y Pedidos.IDUsuarioAutorizo. O sea que cualquiera
      con el token mas flojo de la casa podia autorizar un pedido a nombre del
      gerente, y en la pantalla de Mac31 se leeria el nombre del gerente.

      La regla del proyecto es que la identidad NUNCA viene del cuerpo (esta
      escrita literalmente en una docena de controladores). Quitados los campos,
      quien autoriza y desde que equipo sale de IdUsuarioLegacy() y Equipo().

      Un cliente que los siga mandando no se rompe: System.Text.Json ignora las
      propiedades que no existen en el DTO. Simplemente deja de servirle.
    */

    public int? IDTipoPagoCS { get; set; }
    public int? IDBancoTransferCS { get; set; }
    public string? TransferenciaCS { get; set; }
    public int? IDBancoTarjetaCS { get; set; }
    public string? TarjetaCS { get; set; }
    public int? IDBancoDepositoEfeCS { get; set; }
    public string? DepositoEfectivoNumeroCS { get; set; }
    public decimal? MontoTotalCS { get; set; }
    public int? TipoTarjeta { get; set; }
}
