namespace OrderManagement.AntiCorruptionLayer;

using OrderManagement.Application.Commands;
using Trellis.Authorization;

public class ReturnOrderResourceLoader(IOrderRepository orderRepository)
    : ResourceLoaderById<ReturnOrderCommand, Order, OrderId>
{
    protected override OrderId GetId(ReturnOrderCommand message) => message.OrderId;

    protected override Task<Result<Order>> GetByIdAsync(OrderId id, CancellationToken ct) =>
        orderRepository.GetByIdAsync(id, ct);
}
