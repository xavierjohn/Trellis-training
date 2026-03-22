namespace OrderManagement.AntiCorruptionLayer;

using OrderManagement.Application.Commands;
using Trellis.Authorization;

public class CancelOrderResourceLoader(IOrderRepository orderRepository)
    : ResourceLoaderById<CancelOrderCommand, Order, OrderId>
{
    protected override OrderId GetId(CancelOrderCommand message) => message.OrderId;

    protected override Task<Result<Order>> GetByIdAsync(OrderId id, CancellationToken ct) =>
        orderRepository.GetByIdAsync(id, ct);
}
