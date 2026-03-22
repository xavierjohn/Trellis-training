namespace OrderManagement.Application.Commands;

using Mediator;
using OrderManagement.Application.Abstractions;

public sealed class ShipOrderCommandHandler(IOrderRepository orderRepository)
    : ICommandHandler<ShipOrderCommand, Result<Order>>
{
    public async ValueTask<Result<Order>> Handle(ShipOrderCommand command, CancellationToken cancellationToken) =>
        await orderRepository.GetByIdAsync(command.OrderId, cancellationToken)
            .BindAsync(order => order.Ship())
            .TapAsync(order => orderRepository.SaveAsync(order, cancellationToken));
}
