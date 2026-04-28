using System.Collections.Concurrent;
using ISL_Service.Application.DTOs.VentasPedidos;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Infrastructure.Services;

public class AutorizarPedidosAsyncCoordinator : IAutorizarPedidosAsyncCoordinator
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

    public AutorizarPedidosAsyncCoordinator(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

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

        _ = Task.Run(async () =>
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
            }
        });

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
            IdUsuario = src?.IdUsuario,
            IdUsuarioProcesar = src?.IdUsuarioProcesar,
            IdUsuarioAutorizar = src?.IdUsuarioAutorizar,
            Equipo = src?.Equipo,
            EquipoProcesar = src?.EquipoProcesar,
            EquipoAutorizar = src?.EquipoAutorizar,
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
