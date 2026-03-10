namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Application.Repositories;
using OrderManagement.Domain.Aggregates;

public sealed class ShipOrderHandler(IOrderRepository orderRepository)
    : ICommandHandler<ShipOrderCommand, Result<Order>>
{
    public async ValueTask<Result<Order>> Handle(ShipOrderCommand command, CancellationToken cancellationToken) =>
        await orderRepository.GetByIdAsync(command.OrderId, cancellationToken)
            .BindAsync(order => order.Ship())
            .BindAsync((Func<Order, Task<Result<Order>>>)(async order =>
                await orderRepository.SaveAsync(order, cancellationToken)));
}
