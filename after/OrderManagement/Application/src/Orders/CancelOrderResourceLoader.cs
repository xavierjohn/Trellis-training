namespace OrderManagement.Application.Orders;

using OrderManagement.Application.Repositories;
using OrderManagement.Domain.Aggregates;
using OrderManagement.Domain.ValueObjects;
using Trellis.Authorization;

public sealed class CancelOrderResourceLoader(IOrderRepository orderRepository)
    : ResourceLoaderById<CancelOrderCommand, Order, OrderId>
{
    protected override OrderId GetId(CancelOrderCommand message) => message.OrderId;

    protected override async Task<Result<Order>> GetByIdAsync(OrderId id, CancellationToken ct) =>
        await orderRepository.GetByIdAsync(id, ct);
}
