using System.Collections.Concurrent;
using ISL_Service.Application.DTOs.VentasPedidos;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Infrastructure.Services;

/*
  ES TAMBIEN UN IHostedService, Y NO POR ADORNO.

  El trabajo se lanza con Task.Run y escribe en la base: convierte pedidos en
  ventas. Si el host no lo espera al apagarse -y no lo esperaba-, un despliegue
  lo parte entre dos pedidos: unos ya son venta, otros no, y el estado de la
  operacion desaparece con el proceso.

  StopAsync espera a las operaciones en vuelo. El plazo lo pone el host
  (Program.cs: 60 s, por debajo de los 70 del shutdownTimeLimit de IIS).
*/
public class AutorizarPedidosAsyncCoordinator : IAutorizarPedidosAsyncCoordinator, IHostedService
{
    private sealed class OperationState
    {
        public string OperationId { get; init; } = string.Empty;
        public string Status { get; set; } = "queued";
        public string Message { get; set; } = "En cola.";
        public int TotalPedidos { get; init; }
        public int CompletedPedidos { get; set; }
        public int FailedPedidos { get; set; }
        public string? IdsVenta { get; set; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? FinishedAtUtc { get; set; }
        public List<PedidoResultadoDto> Pedidos { get; set; } = new();
    }

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrentDictionary<string, OperationState> _ops = new(StringComparer.OrdinalIgnoreCase);

    /* Las tareas que estan escribiendo ahora mismo. Es lo que StopAsync espera. */
    private readonly ConcurrentDictionary<string, Task> _enVuelo = new(StringComparer.OrdinalIgnoreCase);

    public AutorizarPedidosAsyncCoordinator(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

    /*
      No se cancela nada: una autorizacion a medias es peor que una que tarda.
      Se ESPERA, y el plazo lo impone el host. Si se agota, el host sigue
      adelante igual, pero al menos lo normal -que terminen- ocurre.
    */
    public Task StopAsync(CancellationToken ct)
        => Task.WhenAll(_enVuelo.Values.ToArray());

    public AutorizarPedidosAsyncStartResponse Start(AutorizarPedidosRequest request, int idUsuarioToken, string equipoToken)
    {
        var opId = Guid.NewGuid().ToString("N");
        var ids = (request?.IdsPedido ?? new List<int>()).Where(x => x > 0).Distinct().ToList();
        var state = new OperationState
        {
            OperationId = opId,
            Status = "queued",
            Message = "Autorizacion en cola.",
            TotalPedidos = ids.Count,
            CreatedAtUtc = DateTime.UtcNow
        };

        _ops[opId] = state;

        var requestClone = CloneRequest(request, ids);

        var trabajo = Task.Run(async () =>
        {
            state.Status = "running";
            state.Message = "Autorizando pedidos...";
            state.StartedAtUtc = DateTime.UtcNow;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IVentasPedidosRepository>();

                var result = await repo.AutorizarPedidosAsync(requestClone, idUsuarioToken, equipoToken, CancellationToken.None);
                var errors = result?.Pedidos ?? new List<PedidoResultadoDto>();
                var failed = errors.Count;
                var completed = Math.Max(0, ids.Count - failed);

                state.Pedidos = errors;
                state.IdsVenta = result?.IdsVenta;
                state.FailedPedidos = failed;
                state.CompletedPedidos = completed;

                if (failed == 0)
                {
                    state.Status = "completed_ok";
                    state.Message = ids.Count == 1 ? "Pedido autorizado correctamente." : "Pedidos autorizados correctamente.";
                }
                else
                {
                    state.Status = "completed_error";
                    state.Message = errors.FirstOrDefault()?.Mensaje ?? "No se pudo completar la autorizacion.";
                }
            }
            catch (Exception ex)
            {
                state.Status = "completed_error";
                state.Message = string.IsNullOrWhiteSpace(ex.Message) ? "Error al autorizar pedidos." : ex.Message;
                state.FailedPedidos = ids.Count;
                state.CompletedPedidos = 0;
            }
            finally
            {
                state.FinishedAtUtc = DateTime.UtcNow;
                _enVuelo.TryRemove(opId, out _);
            }
        });

        /* Se apunta DESPUES de crearla y se quita en el finally de la propia
           tarea: si se apuntara antes, una que termine en el mismo instante
           dejaria la entrada colgada y el apagado esperaria a nada. */
        _enVuelo[opId] = trabajo;

        return new AutorizarPedidosAsyncStartResponse
        {
            OperationId = opId,
            Status = state.Status,
            TotalPedidos = state.TotalPedidos,
            CreatedAtUtc = state.CreatedAtUtc
        };
    }

    public AutorizarPedidosAsyncStatusResponse? GetStatus(string operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId)) return null;
        if (!_ops.TryGetValue(operationId, out var state)) return null;

        return new AutorizarPedidosAsyncStatusResponse
        {
            OperationId = state.OperationId,
            Status = state.Status,
            Message = state.Message,
            TotalPedidos = state.TotalPedidos,
            CompletedPedidos = state.CompletedPedidos,
            FailedPedidos = state.FailedPedidos,
            IdsVenta = state.IdsVenta,
            CreatedAtUtc = state.CreatedAtUtc,
            StartedAtUtc = state.StartedAtUtc,
            FinishedAtUtc = state.FinishedAtUtc,
            Pedidos = state.Pedidos ?? new List<PedidoResultadoDto>()
        };
    }

    private static AutorizarPedidosRequest CloneRequest(AutorizarPedidosRequest? src, List<int> ids)
    {
        return new AutorizarPedidosRequest
        {
            IdsPedido = ids,
            AsyncMode = false,
            // La autoria ya no viaja en el request: la lleva el coordinador en
            // idUsuarioToken/equipoToken, que vienen del token de quien lanzo la
            // operacion. No hay nada de identidad que clonar.
            IDTipoPagoCS = src?.IDTipoPagoCS,
            IDBancoTransferCS = src?.IDBancoTransferCS,
            TransferenciaCS = src?.TransferenciaCS,
            IDBancoTarjetaCS = src?.IDBancoTarjetaCS,
            TarjetaCS = src?.TarjetaCS,
            IDBancoDepositoEfeCS = src?.IDBancoDepositoEfeCS,
            DepositoEfectivoNumeroCS = src?.DepositoEfectivoNumeroCS,
            MontoTotalCS = src?.MontoTotalCS,
            TipoTarjeta = src?.TipoTarjeta
        };
    }
}
